using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Errors;
using OrangeDot.Supabase.Observability;
using GotrueAuthState = global::Supabase.Gotrue.Constants.AuthState;
using OrangeDotAuthState = OrangeDot.Supabase.Auth.AuthState;

namespace OrangeDot.Supabase.Internal;

internal sealed class GotrueAuthStateBridge : IDisposable
{
    private readonly global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> _auth;
    private readonly AuthStateObserver _observer;
    private readonly ILogger<GotrueAuthStateBridge> _logger;
    private readonly SupabaseMetrics? _metrics;
    private readonly ISupabaseSessionStore _sessionStore;
    private readonly object _gate = new();
    private long _canonicalVersion;
    private long _pendingRefreshVersion;
    private OrangeDotAuthState _currentState = new OrangeDotAuthState.Anonymous();
    private OrangeDotAuthState.Authenticated? _lastAuthenticatedState;
    private int _disposed;

    internal GotrueAuthStateBridge(
        global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> auth,
        AuthStateObserver observer,
        ILogger<GotrueAuthStateBridge> logger,
        SupabaseMetrics? metrics,
        ISupabaseSessionStore? sessionStore = null)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(logger);

        _auth = auth;
        _observer = observer;
        _logger = logger;
        _metrics = metrics;
        _sessionStore = sessionStore ?? NoOpSupabaseSessionStore.Instance;

        _auth.AddStateChangedListener(HandleAuthStateChanged);
        PublishCurrentSessionIfPresent();
    }

    private void PublishCurrentSessionIfPresent()
    {
        if (TryCreateSessionSnapshot(_auth.CurrentSession, out var sessionSnapshot))
        {
            var authenticatedState = BuildAuthenticatedState(sessionSnapshot, advanceVersion: true);
            TryPublish(authenticatedState, "initial_session");
        }
    }

    private void HandleAuthStateChanged(
        global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> sender,
        GotrueAuthState stateChanged)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        List<(OrangeDotAuthState State, string Source, bool IncrementTokenRefresh)> states = [];

        switch (stateChanged)
        {
            case GotrueAuthState.SignedIn:
                if (TryCreateSessionSnapshot(sender.CurrentSession, out var signedInSnapshot))
                {
                    PersistSessionOrThrow(sender.CurrentSession!, stateChanged);
                    states.Add((BuildAuthenticatedState(signedInSnapshot, advanceVersion: true), stateChanged.ToString(), false));
                }

                break;
            case GotrueAuthState.UserUpdated:
                if (TryCreateSessionSnapshot(sender.CurrentSession, out var userUpdatedSnapshot))
                {
                    PersistSessionOrThrow(sender.CurrentSession!, stateChanged);
                    states.Add((BuildAuthenticatedState(userUpdatedSnapshot, advanceVersion: true), stateChanged.ToString(), false));
                }
                else
                {
                    ClearPersistedSessionOrThrow(stateChanged);
                    states.Add((BuildFaultedState("User update event did not provide a valid session."), stateChanged.ToString(), false));
                }

                break;
            case GotrueAuthState.TokenRefreshed:
                if (TryClearPendingRefreshForSignedOut())
                {
                    _logger.LogWarning(
                        "Ignored stale Gotrue auth state {State} because canonical state is signed out.",
                        stateChanged);
                    return;
                }

                if (TryCreateSessionSnapshot(sender.CurrentSession, out var refreshedSnapshot))
                {
                    PersistSessionOrThrow(sender.CurrentSession!, stateChanged);
                    states.Add((BuildRefreshingState(refreshedSnapshot), $"{stateChanged}.begin", false));
                    states.Add((BuildAuthenticatedState(refreshedSnapshot, advanceVersion: false), stateChanged.ToString(), true));
                }
                else
                {
                    ClearPersistedSessionOrThrow(stateChanged);
                    states.Add((BuildFaultedState("Token refresh completed without a valid session."), stateChanged.ToString(), false));
                }

                break;
            case GotrueAuthState.SignedOut:
                ClearPersistedSessionOrThrow(stateChanged);
                states.Add((BuildSignedOutState(), stateChanged.ToString(), false));
                break;
            case GotrueAuthState.PasswordRecovery:
            case GotrueAuthState.MfaChallengeVerified:
                _logger.LogWarning("Observed non-canonical Gotrue auth state {State}. No canonical transition was published.", stateChanged);
                break;
            case GotrueAuthState.Shutdown:
                _logger.LogInformation("Observed Gotrue shutdown event.");
                break;
            default:
                _logger.LogWarning("Unhandled Gotrue auth state: {State}", stateChanged);
                break;
        }

        foreach (var (state, source, incrementTokenRefresh) in states)
        {
            TryPublish(state, source, incrementTokenRefresh);
        }
    }

    private void TryPublish(OrangeDotAuthState state, string source, bool incrementTokenRefresh = false)
    {
        using var activity = SupabaseTelemetry.Source.StartActivity("supabase.auth.state_change");
        activity?.SetTag("supabase.auth.source", source);
        activity?.SetTag("supabase.auth.state", ToMetricState(state));

        try
        {
            _observer.Publish(state);
            _metrics?.RecordAuthStateChange(ToMetricState(state));

            if (incrementTokenRefresh)
            {
                _metrics?.RecordAuthTokenRefresh();
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation(
                "Published auth state {AuthState} from Gotrue source {Source}.",
                ToMetricState(state),
                source);
        }
        catch (Exception exception)
        {
            _metrics?.RecordAuthBindingFailure("publish");
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            _logger.LogError(
                exception,
                "Failed to publish auth state {AuthState} from Gotrue source {Source}.",
                ToMetricState(state),
                source);
        }
    }

    private void PersistSessionOrThrow(global::Supabase.Gotrue.Session session, GotrueAuthState source)
    {
        try
        {
            _sessionStore.PersistAsync(session).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            _metrics?.RecordAuthBindingFailure("session_store");
            _logger.LogError(
                exception,
                "Failed to persist auth session after Gotrue source {Source}.",
                source);
            throw SessionStoreAuthExceptions.Create(source, persist: true, exception);
        }
    }

    private void ClearPersistedSessionOrThrow(GotrueAuthState source)
    {
        try
        {
            _sessionStore.ClearAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            _metrics?.RecordAuthBindingFailure("session_store");
            _logger.LogError(
                exception,
                "Failed to clear persisted auth session after Gotrue source {Source}.",
                source);
            throw SessionStoreAuthExceptions.Create(source, persist: false, exception);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _auth.RemoveStateChangedListener(HandleAuthStateChanged);
    }

    internal static bool TryCreateSessionSnapshot(
        global::Supabase.Gotrue.Session? session,
        out SessionSnapshot snapshot)
    {
        if (session is not null &&
            !string.IsNullOrWhiteSpace(session.AccessToken) &&
            !string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            snapshot = new SessionSnapshot(
                session.AccessToken,
                session.RefreshToken,
                CalculateExpiresAt(session));
            return true;
        }

        snapshot = default;
        return false;
    }

    private OrangeDotAuthState.Authenticated BuildAuthenticatedState(SessionSnapshot snapshot, bool advanceVersion)
    {
        lock (_gate)
        {
            if (advanceVersion)
            {
                _canonicalVersion++;
            }
            else
            {
                _canonicalVersion = _pendingRefreshVersion > _canonicalVersion
                    ? _pendingRefreshVersion
                    : _canonicalVersion + 1;
            }

            _pendingRefreshVersion = 0;
            var state = new OrangeDotAuthState.Authenticated(
                _canonicalVersion,
                snapshot.AccessToken,
                snapshot.RefreshToken,
                snapshot.ExpiresAt);
            _currentState = state;
            _lastAuthenticatedState = state;
            return state;
        }
    }

    private OrangeDotAuthState.Refreshing BuildRefreshingState(SessionSnapshot currentSession)
    {
        lock (_gate)
        {
            _pendingRefreshVersion = _canonicalVersion + 1;
            var source = _lastAuthenticatedState;

            var state = new OrangeDotAuthState.Refreshing(
                _canonicalVersion,
                _pendingRefreshVersion,
                source?.AccessToken ?? currentSession.AccessToken,
                source?.RefreshToken ?? currentSession.RefreshToken,
                source?.ExpiresAt ?? currentSession.ExpiresAt);
            _currentState = state;
            return state;
        }
    }

    private OrangeDotAuthState.SignedOut BuildSignedOutState()
    {
        lock (_gate)
        {
            _pendingRefreshVersion = 0;
            _lastAuthenticatedState = null;
            var state = new OrangeDotAuthState.SignedOut(_canonicalVersion);
            _currentState = state;
            return state;
        }
    }

    private OrangeDotAuthState.Faulted BuildFaultedState(string reason)
    {
        lock (_gate)
        {
            var state = new OrangeDotAuthState.Faulted(_canonicalVersion, _pendingRefreshVersion, reason);
            _pendingRefreshVersion = 0;
            _lastAuthenticatedState = null;
            _currentState = state;
            return state;
        }
    }

    private bool TryClearPendingRefreshForSignedOut()
    {
        lock (_gate)
        {
            if (_currentState is not OrangeDotAuthState.SignedOut)
            {
                return false;
            }

            _pendingRefreshVersion = 0;
            return true;
        }
    }

    private static DateTimeOffset CalculateExpiresAt(global::Supabase.Gotrue.Session session)
    {
        var createdAt = session.CreatedAt.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(session.CreatedAt),
            DateTimeKind.Local => new DateTimeOffset(session.CreatedAt.ToUniversalTime()),
            _ => new DateTimeOffset(DateTime.SpecifyKind(session.CreatedAt, DateTimeKind.Utc))
        };

        return createdAt.AddSeconds(session.ExpiresIn);
    }

    private static string ToMetricState(OrangeDotAuthState state)
    {
        return state switch
        {
            OrangeDotAuthState.Authenticated => "authenticated",
            OrangeDotAuthState.Refreshing => "refreshing",
            OrangeDotAuthState.SignedOut => "signed_out",
            OrangeDotAuthState.Anonymous => "anonymous",
            OrangeDotAuthState.Faulted => "faulted",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown auth state.")
        };
    }

    internal readonly record struct SessionSnapshot(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiresAt);
}
