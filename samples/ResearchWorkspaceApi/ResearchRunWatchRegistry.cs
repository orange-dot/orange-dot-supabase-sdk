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
    private readonly SupabaseServerOptions _options;
    private readonly ILogger<ResearchRunWatchRegistry> _logger;
    private readonly ConcurrentDictionary<string, ActiveRunWatch> _watches = new(StringComparer.Ordinal);

    public ResearchRunWatchRegistry(
        IOptions<SupabaseServerOptions> options,
        ILogger<ResearchRunWatchRegistry> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RunWatchStartedResponse> StartAsync(string accessToken, string experimentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(experimentId);

        var publishableKey = _options.PublishableKey
            ?? throw new InvalidOperationException("Supabase publishable key is not configured.");
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
        var userId = RequestAuth.GetRequiredUserId(accessToken);
        var debugHandler = new global::Supabase.Realtime.Interfaces.IRealtimeDebugger.DebugEventHandler(
            (_, message, exception) =>
            {
                _logger.LogDebug(exception, "Realtime watch {WatchId}: {Message}", watchId, message);
            });

        client.AddDebugHandler(debugHandler);
        var watch = new ActiveRunWatch(watchId, userId, experimentId, client, debugHandler);

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

                    watch.Events.Enqueue(new RunWatchEvent(
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

            return new RunWatchStartedResponse(watchId, experimentId, 0);
        }
        catch
        {
            await watch.DisposeAsync();
            throw;
        }
    }

    public RunWatchSnapshot? GetSnapshot(string watchId, string requestingUserId)
    {
        if (!_watches.TryGetValue(watchId, out var watch))
        {
            return null;
        }

        if (!string.Equals(watch.UserId, requestingUserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Run watch '{watchId}' is not available to the current user.");
        }

        return new RunWatchSnapshot(
            watch.WatchId,
            watch.ExperimentId,
            watch.Connected,
            watch.Events.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var watch in _watches.Values)
        {
            await watch.DisposeAsync();
        }

        _watches.Clear();
    }

    private sealed class ActiveRunWatch : IAsyncDisposable
    {
        public ActiveRunWatch(
            string watchId,
            string userId,
            string experimentId,
            global::Supabase.Realtime.Client client,
            global::Supabase.Realtime.Interfaces.IRealtimeDebugger.DebugEventHandler debugHandler)
        {
            WatchId = watchId;
            UserId = userId;
            ExperimentId = experimentId;
            Client = client;
            DebugHandler = debugHandler;
        }

        public string WatchId { get; }

        public string UserId { get; }

        public string ExperimentId { get; }

        public global::Supabase.Realtime.Client Client { get; }

        public global::Supabase.Realtime.Interfaces.IRealtimeDebugger.DebugEventHandler DebugHandler { get; }

        public ConcurrentQueue<RunWatchEvent> Events { get; } = new();

        public IRealtimeChannel? Channel { get; set; }

        public bool Connected { get; set; }

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
}
