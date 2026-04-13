using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Internal;
using OrangeDot.Supabase.Tests.Internal;
using GotrueAuthState = global::Supabase.Gotrue.Constants.AuthState;

namespace OrangeDot.Supabase.Tests.Auth;

public sealed class AuthTraceToModelActionTranslatorTests
{
    [Fact]
    public void Runtime_trace_translates_to_model_actions_for_sign_in_refresh_sign_out_and_stale_refresh()
    {
        var trace = CreateRuntimeTrace(
            new AuthTraceScenarioStep.SignIn(CreateSession("signed-in-token", "refresh-token", 1800)),
            new AuthTraceScenarioStep.Refresh(CreateSession("refreshed-token", "refresh-token-2", 3600)),
            new AuthTraceScenarioStep.SignOut(),
            new AuthTraceScenarioStep.IgnoreStaleRefreshAfterSignOut(CreateSession("stale-token", "stale-refresh-token", 3600)));

        var actions = new AuthTraceToModelActionTranslator().Translate(trace);

        Assert.Equal(
            [
                StartBinding("Postgrest"),
                StartBinding("Storage"),
                StartBinding("Functions"),
                StartBinding("Realtime"),
                Action(AuthModelActionKind.SignIn),
                Project("Postgrest"),
                Project("Storage"),
                Project("Functions"),
                Project("Realtime"),
                Action(AuthModelActionKind.BeginRefresh),
                Action(AuthModelActionKind.CompleteRefresh),
                Project("Postgrest"),
                Project("Storage"),
                Project("Functions"),
                Project("Realtime"),
                Action(AuthModelActionKind.SignOut),
                Action(AuthModelActionKind.IgnoreStaleRefreshResult)
            ],
            actions);
    }

    [Fact]
    public void Runtime_trace_translates_to_model_actions_for_missing_refresh_session_fault()
    {
        var trace = CreateRuntimeTrace(
            new AuthTraceScenarioStep.SignIn(CreateSession("signed-in-token", "refresh-token", 1800)),
            new AuthTraceScenarioStep.FaultedRefresh("Token refresh completed without a valid session."));

        var actions = new AuthTraceToModelActionTranslator().Translate(trace);

        Assert.Equal(
            [
                StartBinding("Postgrest"),
                StartBinding("Storage"),
                StartBinding("Functions"),
                StartBinding("Realtime"),
                Action(AuthModelActionKind.SignIn),
                Project("Postgrest"),
                Project("Storage"),
                Project("Functions"),
                Project("Realtime"),
                Action(AuthModelActionKind.RefreshFail),
                Clear("Postgrest"),
                Clear("Storage"),
                Clear("Functions"),
                Clear("Realtime")
            ],
            actions);
    }

    [Fact]
    public void Runtime_trace_translates_user_update_and_mfa_verification_to_authenticated_publications()
    {
        var trace = CreateRuntimeTrace(
            new AuthTraceScenarioStep.SignIn(CreateSession("signed-in-token", "refresh-token", 1800)),
            new AuthTraceScenarioStep.UserUpdated(CreateSession("user-updated-token", "refresh-token-2", 1800)),
            new AuthTraceScenarioStep.MfaChallengeVerified(CreateSession("mfa-token", "refresh-token-3", 1800)));

        var actions = new AuthTraceToModelActionTranslator().Translate(trace);

        Assert.Equal(
            [
                StartBinding("Postgrest"),
                StartBinding("Storage"),
                StartBinding("Functions"),
                StartBinding("Realtime"),
                Action(AuthModelActionKind.SignIn),
                Project("Postgrest"),
                Project("Storage"),
                Project("Functions"),
                Project("Realtime"),
                Action(AuthModelActionKind.SignIn),
                Project("Postgrest"),
                Project("Storage"),
                Project("Functions"),
                Project("Realtime"),
                Action(AuthModelActionKind.SignIn),
                Project("Postgrest"),
                Project("Storage"),
                Project("Functions"),
                Project("Realtime")
            ],
            actions);
    }

    private static IReadOnlyList<RuntimeTraceEvent> CreateRuntimeTrace(params AuthTraceScenarioStep[] steps)
    {
        var observer = new AuthStateObserver();
        var traceSink = new RecordingRuntimeTraceSink();
        var dynamicHeaders = new DynamicAuthHeaders("anon-key");
        var realtime = CreateRealtimeClient();
        using var headerBinding = new HeaderAuthBinding(observer, dynamicHeaders, NullLogger<HeaderAuthBinding>.Instance, traceSink);
        using var realtimeBinding = new RealtimeTokenBinding(observer, realtime, NullLogger<RealtimeTokenBinding>.Instance, traceSink);
        var auth = CreateAuthClient();
        using var bridge = new GotrueAuthStateBridge(
            auth,
            observer,
            NullLogger<GotrueAuthStateBridge>.Instance,
            metrics: null,
            traceSink: traceSink);

        foreach (var step in steps)
        {
            switch (step)
            {
                case AuthTraceScenarioStep.SignIn(var session):
                    SetCurrentSession(auth, session);
                    auth.NotifyAuthStateChange(GotrueAuthState.SignedIn);
                    break;
                case AuthTraceScenarioStep.UserUpdated(var session):
                    SetCurrentSession(auth, session);
                    auth.NotifyAuthStateChange(GotrueAuthState.UserUpdated);
                    break;
                case AuthTraceScenarioStep.Refresh(var session):
                    SetCurrentSession(auth, session);
                    auth.NotifyAuthStateChange(GotrueAuthState.TokenRefreshed);
                    break;
                case AuthTraceScenarioStep.MfaChallengeVerified(var session):
                    SetCurrentSession(auth, session);
                    auth.NotifyAuthStateChange(GotrueAuthState.MfaChallengeVerified);
                    break;
                case AuthTraceScenarioStep.SignOut:
                    SetCurrentSession(auth, null);
                    auth.NotifyAuthStateChange(GotrueAuthState.SignedOut);
                    break;
                case AuthTraceScenarioStep.FaultedRefresh:
                    SetCurrentSession(auth, null);
                    auth.NotifyAuthStateChange(GotrueAuthState.TokenRefreshed);
                    break;
                case AuthTraceScenarioStep.IgnoreStaleRefreshAfterSignOut(var session):
                    SetCurrentSession(auth, session);
                    auth.NotifyAuthStateChange(GotrueAuthState.TokenRefreshed);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(steps), step, "Unknown auth trace scenario step.");
            }
        }

        return traceSink.Snapshot();
    }

    private static AuthModelAction Action(AuthModelActionKind kind)
    {
        return new AuthModelAction(kind);
    }

    private static AuthModelAction StartBinding(string bindingName)
    {
        return new AuthModelAction(AuthModelActionKind.StartBinding, bindingName);
    }

    private static AuthModelAction Project(string bindingName)
    {
        return new AuthModelAction(AuthModelActionKind.ProjectCurrentToBinding, bindingName);
    }

    private static AuthModelAction Clear(string bindingName)
    {
        return new AuthModelAction(AuthModelActionKind.ClearBindingProjection, bindingName);
    }

    private static global::Supabase.Gotrue.Client CreateAuthClient()
    {
        return new global::Supabase.Gotrue.Client(new global::Supabase.Gotrue.ClientOptions
        {
            Url = "https://abc.supabase.co/auth/v1",
            Headers = new Dictionary<string, string>
            {
                ["apikey"] = "anon-key"
            }
        });
    }

    private static global::Supabase.Realtime.Client CreateRealtimeClient()
    {
        var realtime = new global::Supabase.Realtime.Client("wss://abc.supabase.co/realtime/v1");
        SetSocket(realtime, new global::Supabase.Realtime.RealtimeSocket(
            "wss://abc.supabase.co/realtime/v1",
            new global::Supabase.Realtime.ClientOptions()));
        return realtime;
    }

    private static global::Supabase.Gotrue.Session CreateSession(string accessToken, string refreshToken, long expiresIn)
    {
        return new global::Supabase.Gotrue.Session
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            CreatedAt = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static void SetCurrentSession(global::Supabase.Gotrue.Client auth, global::Supabase.Gotrue.Session? session)
    {
        var property = typeof(global::Supabase.Gotrue.Client).GetProperty(
            nameof(global::Supabase.Gotrue.Client.CurrentSession),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(property);
        property!.SetValue(auth, session);
    }

    private static void SetSocket(global::Supabase.Realtime.Client client, global::Supabase.Realtime.RealtimeSocket socket)
    {
        var field = typeof(global::Supabase.Realtime.Client).GetField(
            "<Socket>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        field!.SetValue(client, socket);
    }
}
