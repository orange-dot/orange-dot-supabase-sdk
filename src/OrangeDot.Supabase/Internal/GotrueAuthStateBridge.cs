using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Observability;
using GotrueAuthState = global::Supabase.Gotrue.Constants.AuthState;
using OrangeDotAuthState = OrangeDot.Supabase.Auth.AuthState;

namespace OrangeDot.Supabase.Internal;

internal sealed class GotrueAuthStateBridge
{
    private readonly global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> _auth;
    private readonly AuthStateObserver _observer;
    private readonly ILogger<GotrueAuthStateBridge> _logger;
    private readonly SupabaseMetrics? _metrics;

    internal GotrueAuthStateBridge(
        global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> auth,
        AuthStateObserver observer,
        ILogger<GotrueAuthStateBridge> logger,
        SupabaseMetrics? metrics)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(logger);

        _auth = auth;
        _observer = observer;
        _logger = logger;
        _metrics = metrics;

        _auth.AddStateChangedListener(HandleAuthStateChanged);
        PublishCurrentSessionIfPresent();
    }

    private void PublishCurrentSessionIfPresent()
    {
        if (TryCreateAuthenticatedState(_auth.CurrentSession, out var authenticatedState))
        {
            TryPublish(authenticatedState, "initial_session");
        }
    }

    private void HandleAuthStateChanged(
        global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> sender,
        GotrueAuthState stateChanged)
    {
        switch (stateChanged)
        {
            case GotrueAuthState.SignedIn:
                if (TryCreateAuthenticatedState(sender.CurrentSession, out var signedInState))
                {
                    TryPublish(signedInState, stateChanged.ToString());
                }

                break;
            case GotrueAuthState.TokenRefreshed:
                if (TryCreateAuthenticatedState(sender.CurrentSession, out var refreshedState))
                {
                    TryPublish(refreshedState, stateChanged.ToString(), incrementTokenRefresh: true);
                }

                break;
            case GotrueAuthState.SignedOut:
                TryPublish(new OrangeDotAuthState.SignedOut(), stateChanged.ToString());
                break;
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

    internal static bool TryCreateAuthenticatedState(
        global::Supabase.Gotrue.Session? session,
        out OrangeDotAuthState.Authenticated authenticatedState)
    {
        if (session is not null &&
            !string.IsNullOrWhiteSpace(session.AccessToken) &&
            !string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            authenticatedState = new OrangeDotAuthState.Authenticated(
                session.AccessToken,
                session.RefreshToken,
                CalculateExpiresAt(session));
            return true;
        }

        authenticatedState = null!;
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
            OrangeDotAuthState.SignedOut => "signed_out",
            OrangeDotAuthState.Anonymous => "anonymous",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown auth state.")
        };
    }
}
