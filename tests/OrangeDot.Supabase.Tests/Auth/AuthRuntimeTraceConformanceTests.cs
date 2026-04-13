using System;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Internal;
using OrangeDot.Supabase.Tests.Internal;
using GotrueAuthState = global::Supabase.Gotrue.Constants.AuthState;

namespace OrangeDot.Supabase.Tests.Auth;

public sealed class AuthRuntimeTraceConformanceTests
{
    [Fact]
    public void Runtime_trace_matches_canonical_sequence_for_sign_in_refresh_sign_out_and_stale_refresh()
    {
        var observer = new AuthStateObserver();
        var traceSink = new RecordingRuntimeTraceSink();
        var dynamicHeaders = new DynamicAuthHeaders("anon-key");
        var realtime = CreateRealtimeClient();
        using var headerBinding = new HeaderAuthBinding(observer, dynamicHeaders, NullLogger<HeaderAuthBinding>.Instance, traceSink);
        using var realtimeBinding = new RealtimeTokenBinding(observer, realtime, NullLogger<RealtimeTokenBinding>.Instance, traceSink);
        var auth = CreateAuthClient();
        using var bridgeWithEvents = new GotrueAuthStateBridge(
            auth,
            observer,
            NullLogger<GotrueAuthStateBridge>.Instance,
            metrics: null,
            traceSink: traceSink);

        var steps = new AuthTraceScenarioStep[]
        {
            new AuthTraceScenarioStep.SignIn(CreateSession("signed-in-token", "refresh-token", 1800)),
            new AuthTraceScenarioStep.Refresh(CreateSession("refreshed-token", "refresh-token-2", 3600)),
            new AuthTraceScenarioStep.SignOut(),
            new AuthTraceScenarioStep.IgnoreStaleRefreshAfterSignOut(CreateSession("stale-token", "stale-refresh-token", 3600))
        };
        var expected = AuthTraceExpectationBuilder
            .StartWithAnonymousBindings()
            .Apply(steps)
            .Build();

        ExecuteScenario(auth, steps);

        RuntimeTraceAssert.EqualSequence(expected, traceSink.Snapshot());
        Assert.DoesNotContain("Authorization", dynamicHeaders.Build().Keys);
        Assert.Equal(string.Empty, ReadPrivateStringMember(realtime, "AccessToken"));
    }

    [Fact]
    public void Runtime_trace_matches_canonical_sequence_for_missing_refresh_session_fault()
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

        var steps = new AuthTraceScenarioStep[]
        {
            new AuthTraceScenarioStep.SignIn(CreateSession("signed-in-token", "refresh-token", 1800)),
            new AuthTraceScenarioStep.FaultedRefresh("Token refresh completed without a valid session.")
        };
        var expected = AuthTraceExpectationBuilder
            .StartWithAnonymousBindings()
            .Apply(steps)
            .Build();

        ExecuteScenario(auth, steps);

        RuntimeTraceAssert.EqualSequence(expected, traceSink.Snapshot());
        Assert.DoesNotContain("Authorization", dynamicHeaders.Build().Keys);
        Assert.Equal(string.Empty, ReadPrivateStringMember(realtime, "AccessToken"));
    }

    private static void ExecuteScenario(global::Supabase.Gotrue.Client auth, params AuthTraceScenarioStep[] steps)
    {
        foreach (var step in steps)
        {
            switch (step)
            {
                case AuthTraceScenarioStep.SignIn(var session):
                    SetCurrentSession(auth, session);
                    auth.NotifyAuthStateChange(GotrueAuthState.SignedIn);
                    break;
                case AuthTraceScenarioStep.Refresh(var session):
                    SetCurrentSession(auth, session);
                    auth.NotifyAuthStateChange(GotrueAuthState.TokenRefreshed);
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
                    throw new ArgumentOutOfRangeException(nameof(step), step, "Unknown auth trace scenario step.");
            }
        }
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

    private static string ReadPrivateStringMember(object instance, string memberName)
    {
        var property = instance.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property is not null)
        {
            return property.GetValue(instance) as string ?? string.Empty;
        }

        var field = instance.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        return field!.GetValue(instance) as string ?? string.Empty;
    }
}
