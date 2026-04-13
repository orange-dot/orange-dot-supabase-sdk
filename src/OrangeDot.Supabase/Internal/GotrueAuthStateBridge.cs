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
    private readonly IRuntimeTraceSink _traceSink;
    private readonly CanonicalAuthStateMachine _stateMachine = new();
    private int _disposed;

    internal GotrueAuthStateBridge(
        global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> auth,
        AuthStateObserver observer,
        ILogger<GotrueAuthStateBridge> logger,
        SupabaseMetrics? metrics,
        ISupabaseSessionStore? sessionStore = null,
        IRuntimeTraceSink? traceSink = null)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(logger);

        _auth = auth;
        _observer = observer;
        _logger = logger;
        _metrics = metrics;
        _sessionStore = sessionStore ?? NoOpSupabaseSessionStore.Instance;
        _traceSink = traceSink ?? NoOpRuntimeTraceSink.Instance;

        _auth.AddStateChangedListener(HandleAuthStateChanged);
        PublishCurrentSessionIfPresent();
    }

    private void PublishCurrentSessionIfPresent()
    {
        if (TryCreateSessionSnapshot(_auth.CurrentSession, out var sessionSnapshot))
        {
            var authenticatedState = _stateMachine.AdvanceAuthenticated(sessionSnapshot);
            TryPublish(authenticatedState, "initial_session", AuthTraceKind.InitialSessionPublished);
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

        List<(OrangeDotAuthState State, string Source, AuthTraceKind TraceKind, bool IncrementTokenRefresh)> states = [];

        switch (stateChanged)
        {
            case GotrueAuthState.SignedIn:
                if (TryCreateSessionSnapshot(sender.CurrentSession, out var signedInSnapshot))
                {
                    if (_stateMachine.IsRedundantAuthenticatedProjection(signedInSnapshot))
                    {
                        _logger.LogDebug(
                            "Ignored duplicate Gotrue auth state {State} for an already-projected session.",
                            stateChanged);
                        break;
                    }

                    PersistSessionOrThrow(sender.CurrentSession!, stateChanged);
                    states.Add((_stateMachine.AdvanceAuthenticated(signedInSnapshot), stateChanged.ToString(), AuthTraceKind.SignedInPublished, false));
                }

                break;
            case GotrueAuthState.UserUpdated:
                if (TryCreateSessionSnapshot(sender.CurrentSession, out var userUpdatedSnapshot))
                {
                    PersistSessionOrThrow(sender.CurrentSession!, stateChanged);
                    states.Add((_stateMachine.AdvanceAuthenticated(userUpdatedSnapshot), stateChanged.ToString(), AuthTraceKind.UserUpdatedPublished, false));
                }
                else
                {
                    ClearPersistedSessionOrThrow(stateChanged);
                    states.Add((_stateMachine.Fault("User update event did not provide a valid session."), stateChanged.ToString(), AuthTraceKind.FaultedPublished, false));
                }

                break;
            case GotrueAuthState.TokenRefreshed:
                if (_stateMachine.TryIgnoreStaleRefreshResultAfterSignOut())
                {
                    _logger.LogWarning(
                        "Ignored stale Gotrue auth state {State} because canonical state is signed out.",
                        stateChanged);
                    return;
                }

                if (TryCreateSessionSnapshot(sender.CurrentSession, out var refreshedSnapshot))
                {
                    PersistSessionOrThrow(sender.CurrentSession!, stateChanged);
                    states.Add((_stateMachine.BeginRefresh(refreshedSnapshot), $"{stateChanged}.begin", AuthTraceKind.RefreshBeginPublished, false));
                    states.Add((_stateMachine.CompleteRefresh(refreshedSnapshot), stateChanged.ToString(), AuthTraceKind.RefreshCompletedPublished, true));
                }
                else
                {
                    ClearPersistedSessionOrThrow(stateChanged);
                    states.Add((_stateMachine.Fault("Token refresh completed without a valid session."), stateChanged.ToString(), AuthTraceKind.FaultedPublished, false));
                }

                break;
            case GotrueAuthState.SignedOut:
                if (_stateMachine.IsSignedOut)
                {
                    _logger.LogDebug("Ignored duplicate Gotrue auth state {State}.", stateChanged);
                    break;
                }

                ClearPersistedSessionOrThrow(stateChanged);
                _stateMachine.TrySignOut(out var signedOutState);
                states.Add((signedOutState, stateChanged.ToString(), AuthTraceKind.SignedOutPublished, false));
                break;
            case GotrueAuthState.PasswordRecovery:
                _logger.LogWarning("Observed non-canonical Gotrue auth state {State}. No canonical transition was published.", stateChanged);
                break;
            case GotrueAuthState.MfaChallengeVerified:
                if (TryCreateSessionSnapshot(sender.CurrentSession, out var mfaVerifiedSnapshot))
                {
                    if (_stateMachine.IsRedundantAuthenticatedProjection(mfaVerifiedSnapshot))
                    {
                        _logger.LogDebug(
                            "Ignored duplicate Gotrue auth state {State} for an already-projected session.",
                            stateChanged);
                        break;
                    }

                    PersistSessionOrThrow(sender.CurrentSession!, stateChanged);
                    states.Add((_stateMachine.AdvanceAuthenticated(mfaVerifiedSnapshot), stateChanged.ToString(), AuthTraceKind.MfaChallengeVerifiedPublished, false));
                }
                else
                {
                    ClearPersistedSessionOrThrow(stateChanged);
                    states.Add((_stateMachine.Fault("MFA verification completed without a valid session."), stateChanged.ToString(), AuthTraceKind.FaultedPublished, false));
                }

                break;
            case GotrueAuthState.Shutdown:
                _logger.LogInformation("Observed Gotrue shutdown event.");
                break;
            default:
                _logger.LogWarning("Unhandled Gotrue auth state: {State}", stateChanged);
                break;
        }

        foreach (var (state, source, traceKind, incrementTokenRefresh) in states)
        {
            TryPublish(state, source, traceKind, incrementTokenRefresh);
        }
    }

    private void TryPublish(
        OrangeDotAuthState state,
        string source,
        AuthTraceKind traceKind,
        bool incrementTokenRefresh = false)
    {
        using var activity = SupabaseTelemetry.Source.StartActivity("supabase.auth.state_change");
        activity?.SetTag("supabase.auth.source", source);
        activity?.SetTag("supabase.auth.state", ToMetricState(state));

        try
        {
            _traceSink.Record(new AuthTraceEvent(
                traceKind,
                CanonicalAuthStateMachine.ToStateName(state),
                state.CanonicalVersion,
                CanonicalAuthStateMachine.GetPendingRefreshVersion(state)));
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
        if (SessionStoreSyncContext.IsBridgePersistenceSuppressed)
        {
            return;
        }

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
        if (SessionStoreSyncContext.IsBridgePersistenceSuppressed)
        {
            return;
        }

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
}
