using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrangeDot.Supabase.Auth;
using IGotrueClient = global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session>;

namespace OrangeDot.Supabase.Internal;

internal sealed class GotrueAutoRefreshCoordinator : IDisposable
{
    private readonly AuthStateObserver _authStateObserver;
    private readonly Func<global::Supabase.Gotrue.Session?> _getCurrentSession;
    private readonly int _maximumRefreshWaitTimeSeconds;
    private readonly Func<Task> _refreshTokenAsync;
    private readonly ILogger<GotrueAutoRefreshCoordinator> _logger;
    private readonly IDisposable _subscription;
    private readonly object _gate = new();
    private Timer? _refreshTimer;
    private long _timerVersion;
    private int _refreshInFlight;
    private int _disposed;

    internal GotrueAutoRefreshCoordinator(
        AuthStateObserver authStateObserver,
        global::Supabase.Gotrue.Client auth,
        ILogger<GotrueAutoRefreshCoordinator> logger)
        : this(
            authStateObserver,
            () => auth.CurrentSession,
            auth.Options.MaximumRefreshWaitTime,
            () => ((IGotrueClient)auth).RefreshToken(),
            logger)
    {
    }

    internal GotrueAutoRefreshCoordinator(
        AuthStateObserver authStateObserver,
        Func<global::Supabase.Gotrue.Session?> getCurrentSession,
        int maximumRefreshWaitTimeSeconds,
        Func<Task> refreshTokenAsync,
        ILogger<GotrueAutoRefreshCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(authStateObserver);
        ArgumentNullException.ThrowIfNull(getCurrentSession);
        ArgumentNullException.ThrowIfNull(refreshTokenAsync);
        ArgumentNullException.ThrowIfNull(logger);

        _authStateObserver = authStateObserver;
        _getCurrentSession = getCurrentSession;
        _maximumRefreshWaitTimeSeconds = maximumRefreshWaitTimeSeconds;
        _refreshTokenAsync = refreshTokenAsync;
        _logger = logger;
        _subscription = authStateObserver.Subscribe(Apply);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _subscription.Dispose();
        ClearScheduledRefresh();
    }

    private void Apply(AuthState state)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        switch (state)
        {
            case AuthState.Authenticated:
                ScheduleRefresh(_getCurrentSession());
                break;
            case AuthState.Refreshing:
                break;
            case AuthState.Anonymous:
            case AuthState.SignedOut:
            case AuthState.Faulted:
                ClearScheduledRefresh();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown auth state.");
        }
    }

    private void ScheduleRefresh(global::Supabase.Gotrue.Session? session)
    {
        if (!TryCreateRefreshSchedule(
                session,
                _maximumRefreshWaitTimeSeconds,
                DateTimeOffset.UtcNow,
                out var dueTime))
        {
            ClearScheduledRefresh();
            return;
        }

        lock (_gate)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            _timerVersion++;
            var timerVersion = _timerVersion;

            _refreshTimer?.Dispose();
            _refreshTimer = new Timer(HandleRefreshTimerTick, timerVersion, dueTime, Timeout.InfiniteTimeSpan);
        }

        _logger.LogDebug(
            "Scheduled background auth token refresh in {DueTime}.",
            dueTime);
    }

    private void ClearScheduledRefresh()
    {
        lock (_gate)
        {
            _timerVersion++;
            _refreshTimer?.Dispose();
            _refreshTimer = null;
        }
    }

    private void HandleRefreshTimerTick(object? state)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (state is not long timerVersion)
        {
            return;
        }

        lock (_gate)
        {
            if (timerVersion != _timerVersion)
            {
                return;
            }

            _refreshTimer?.Dispose();
            _refreshTimer = null;
        }

        if (Interlocked.Exchange(ref _refreshInFlight, 1) != 0)
        {
            return;
        }

        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            await _refreshTokenAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                _logger.LogWarning(
                    exception,
                    "Background auth token refresh failed.");
            }
        }
        finally
        {
            Interlocked.Exchange(ref _refreshInFlight, 0);
            RescheduleIfStillAuthenticated();
        }
    }

    private void RescheduleIfStillAuthenticated()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var currentState = _authStateObserver.Current;
        if (currentState is not AuthState.Authenticated &&
            currentState is not AuthState.Refreshing)
        {
            return;
        }

        lock (_gate)
        {
            if (_refreshTimer is not null)
            {
                return;
            }
        }

        ScheduleRefresh(_getCurrentSession());
    }

    internal static bool TryCreateRefreshSchedule(
        global::Supabase.Gotrue.Session? session,
        int maximumRefreshWaitTimeSeconds,
        DateTimeOffset now,
        out TimeSpan dueTime)
    {
        if (session is null ||
            string.IsNullOrWhiteSpace(session.AccessToken) ||
            string.IsNullOrWhiteSpace(session.RefreshToken) ||
            session.ExpiresIn <= 0)
        {
            dueTime = default;
            return false;
        }

        var createdAt = session.CreatedAt.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(session.CreatedAt),
            DateTimeKind.Local => new DateTimeOffset(session.CreatedAt.ToUniversalTime()),
            _ => new DateTimeOffset(DateTime.SpecifyKind(session.CreatedAt, DateTimeKind.Utc))
        };

        var intervalSeconds = (long)Math.Floor(session.ExpiresIn * 4.0 / 5.0);
        var refreshAt = createdAt.AddSeconds(intervalSeconds);
        dueTime = refreshAt - now;

        if (dueTime < TimeSpan.Zero)
        {
            dueTime = TimeSpan.Zero;
        }

        var maximumWaitTime = TimeSpan.FromSeconds(Math.Max(0, maximumRefreshWaitTimeSeconds));
        if (dueTime > maximumWaitTime)
        {
            dueTime = maximumWaitTime;
        }

        return true;
    }
}
