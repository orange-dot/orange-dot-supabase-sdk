using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using OrangeDot.Supabase;
using OrangeDot.Supabase.Urls;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.PostgresChanges;

namespace ResearchWorkspaceApi;

public sealed class ResearchRunWatchRegistry : IAsyncDisposable
{
    private const int MaxEventsPerWatch = 64;
    private static readonly TimeSpan WatchIdleTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);

    private readonly SupabaseServerOptions _options;
    private readonly ILogger<ResearchRunWatchRegistry> _logger;
    private readonly ConcurrentDictionary<string, ActiveRunWatch> _watches = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private readonly Task _cleanupLoop;

    public ResearchRunWatchRegistry(
        IOptions<SupabaseServerOptions> options,
        ILogger<ResearchRunWatchRegistry> logger)
    {
        _options = options.Value;
        _logger = logger;
        _cleanupLoop = RunCleanupLoopAsync(_disposeTokenSource.Token);
    }

    public async Task<RunWatchStartedResponse> StartAsync(ResearchWorkspaceIdentity identity, string experimentId)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.AccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(experimentId);

        await CleanupExpiredWatchesAsync(DateTime.UtcNow);

        var publishableKey = _options.PublishableKey
            ?? throw new InvalidOperationException("Supabase publishable key is not configured.");
        var accessToken = identity.AccessToken;
        var realtimeUrl = SupabaseUrls.FromBaseUrl(
            _options.Url ?? throw new InvalidOperationException("Supabase URL is not configured.")).RealtimeUrl;
        var clientOptions = new global::Supabase.Realtime.ClientOptions();
        clientOptions.Headers.Add("apikey", publishableKey);
        clientOptions.Parameters.ApiKey = publishableKey;
        clientOptions.Parameters.Token = accessToken;

        var client = new global::Supabase.Realtime.Client(realtimeUrl, clientOptions)
        {
            GetHeaders = () => new Dictionary<string, string>
            {
                ["apikey"] = publishableKey,
                ["Authorization"] = $"Bearer {accessToken}"
            }
        };
        client.SetAuth(accessToken);

        var watchId = Guid.NewGuid().ToString("N");
        var debugHandler = new global::Supabase.Realtime.Interfaces.IRealtimeDebugger.DebugEventHandler(
            (_, message, exception) =>
            {
                _logger.LogDebug(exception, "Realtime watch {WatchId}: {Message}", watchId, message);
            });

        client.AddDebugHandler(debugHandler);
        var watch = new ActiveRunWatch(watchId, identity.UserId, experimentId, client, debugHandler, MaxEventsPerWatch);

        try
        {
            await client.ConnectAsync();

            var channel = client.Channel(watchId);
            channel.Options.Parameters ??= new Dictionary<string, string>(StringComparer.Ordinal);
            channel.Options.Parameters["user_token"] = accessToken;
            channel.Register(new PostgresChangesOptions(
                schema: "public",
                table: "research_runs",
                eventType: PostgresChangesOptions.ListenType.Updates,
                filter: $"experiment_id=eq.{experimentId}"));
            channel.AddPostgresChangeHandler(
                PostgresChangesOptions.ListenType.Updates,
                (_, change) =>
                {
                    var model = change.Model<ResearchRunRecord>();
                    if (model?.Id is null || model.Status is null)
                    {
                        return;
                    }

                    _logger.LogInformation(
                        "Watch {WatchId} observed run {RunId} status {Status} for experiment {ExperimentId}.",
                        watchId,
                        model.Id,
                        model.Status,
                        experimentId);

                    watch.RecordEvent(new RunWatchEvent(
                        change.Payload?.Data?.Type.ToString() ?? "Unknown",
                        model.Id,
                        model.Status,
                        DateTime.UtcNow));
                });

            await channel.Subscribe();

            watch.Channel = channel;
            watch.Connected = channel.State == Constants.ChannelState.Joined;

            // Local Supabase Realtime can take an extra moment to fully arm postgres-changes
            // delivery after the join ACK. A short settle window keeps the sample watch flow reliable.
            await Task.Delay(TimeSpan.FromSeconds(3));
            _watches[watchId] = watch;

            _logger.LogInformation(
                "Started research run watch {WatchId} for experiment {ExperimentId}. Joined={Joined}.",
                watchId,
                experimentId,
                watch.Connected);

            return new RunWatchStartedResponse(watchId, experimentId, watch.EventCount, watch.ExpiresAt);
        }
        catch
        {
            await watch.DisposeAsync();
            throw;
        }
    }

    public async Task<RunWatchSnapshot?> GetSnapshotAsync(string watchId, string requestingUserId)
    {
        var now = DateTime.UtcNow;
        await CleanupExpiredWatchesAsync(now);

        if (!_watches.TryGetValue(watchId, out var watch))
        {
            return null;
        }

        if (!string.Equals(watch.UserId, requestingUserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Run watch '{watchId}' is not available to the current user.");
        }

        if (watch.IsExpired(now))
        {
            await RemoveWatchAsync(watch, "expired-on-read");
            return null;
        }

        watch.Touch(now);

        return new RunWatchSnapshot(
            watch.WatchId,
            watch.ExperimentId,
            watch.Connected,
            watch.GetEventsSnapshot(),
            watch.ExpiresAt);
    }

    public async Task<bool> DeleteAsync(string watchId, string requestingUserId)
    {
        var now = DateTime.UtcNow;
        await CleanupExpiredWatchesAsync(now);

        if (!_watches.TryGetValue(watchId, out var watch))
        {
            return false;
        }

        if (!string.Equals(watch.UserId, requestingUserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Run watch '{watchId}' is not available to the current user.");
        }

        return await RemoveWatchAsync(watch, "deleted");
    }

    public async ValueTask DisposeAsync()
    {
        _disposeTokenSource.Cancel();

        try
        {
            await _cleanupLoop;
        }
        catch (OperationCanceledException)
        {
            // Disposal intentionally cancels the cleanup loop.
        }

        foreach (var watch in _watches.Values)
        {
            await watch.DisposeAsync();
        }

        _watches.Clear();
        _disposeTokenSource.Dispose();
    }

    private async Task RunCleanupLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(CleanupInterval, cancellationToken);
                await CleanupExpiredWatchesAsync(DateTime.UtcNow);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Background watch cleanup loop stopped unexpectedly.");
        }
    }

    private async Task CleanupExpiredWatchesAsync(DateTime utcNow)
    {
        foreach (var watch in _watches.Values)
        {
            if (watch.IsExpired(utcNow))
            {
                await RemoveWatchAsync(watch, "expired");
            }
        }
    }

    private async Task<bool> RemoveWatchAsync(ActiveRunWatch watch, string reason)
    {
        if (!_watches.TryRemove(watch.WatchId, out var removed))
        {
            return false;
        }

        await removed.DisposeAsync();
        _logger.LogInformation(
            "Removed research run watch {WatchId} for experiment {ExperimentId}. Reason={Reason}.",
            removed.WatchId,
            removed.ExperimentId,
            reason);

        return true;
    }

    private sealed class ActiveRunWatch : IAsyncDisposable
    {
        private long _lastTouchedTicks;

        public ActiveRunWatch(
            string watchId,
            string userId,
            string experimentId,
            global::Supabase.Realtime.Client client,
            global::Supabase.Realtime.Interfaces.IRealtimeDebugger.DebugEventHandler debugHandler,
            int maxEvents)
        {
            WatchId = watchId;
            UserId = userId;
            ExperimentId = experimentId;
            Client = client;
            DebugHandler = debugHandler;
            Events = new BoundedRunWatchEvents(maxEvents);
            _lastTouchedTicks = DateTime.UtcNow.Ticks;
        }

        public string WatchId { get; }

        public string UserId { get; }

        public string ExperimentId { get; }

        public global::Supabase.Realtime.Client Client { get; }

        public global::Supabase.Realtime.Interfaces.IRealtimeDebugger.DebugEventHandler DebugHandler { get; }

        public BoundedRunWatchEvents Events { get; }

        public IRealtimeChannel? Channel { get; set; }

        public bool Connected { get; set; }

        public int EventCount => Events.Count;

        public DateTime ExpiresAt => LastTouchedAtUtc.Add(WatchIdleTtl);

        private DateTime LastTouchedAtUtc => new(Interlocked.Read(ref _lastTouchedTicks), DateTimeKind.Utc);

        public void RecordEvent(RunWatchEvent item)
        {
            Events.Append(item);
            Touch(item.ObservedAt);
        }

        public void Touch(DateTime utcNow)
        {
            Interlocked.Exchange(ref _lastTouchedTicks, utcNow.Ticks);
        }

        public bool IsExpired(DateTime utcNow)
        {
            return utcNow - LastTouchedAtUtc >= WatchIdleTtl;
        }

        public IReadOnlyList<RunWatchEvent> GetEventsSnapshot()
        {
            return Events.Snapshot();
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                if (Channel is not null)
                {
                    Channel.Unsubscribe();
                    Client.Remove((global::Supabase.Realtime.RealtimeChannel)Channel);
                }
            }
            catch
            {
                // Teardown is best-effort for sample watcher sessions.
            }
            finally
            {
                Client.RemoveDebugHandler(DebugHandler);
                Client.Disconnect();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class BoundedRunWatchEvents
    {
        private readonly int _capacity;
        private readonly Queue<RunWatchEvent> _items = new();
        private readonly object _gate = new();

        public BoundedRunWatchEvents(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
        }

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _items.Count;
                }
            }
        }

        public void Append(RunWatchEvent item)
        {
            lock (_gate)
            {
                if (_items.Count == _capacity)
                {
                    _items.Dequeue();
                }

                _items.Enqueue(item);
            }
        }

        public IReadOnlyList<RunWatchEvent> Snapshot()
        {
            lock (_gate)
            {
                return _items.ToArray();
            }
        }
    }
}
