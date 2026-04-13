using System;
using System.Collections.Generic;
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

        var expected = new List<RuntimeTraceEvent>
        {
            CreateBindingTrace(BindingTarget.Header, new AuthState.Anonymous()),
            CreateBindingTrace(BindingTarget.Realtime, new AuthState.Anonymous())
        };
        var machine = new CanonicalAuthStateMachine();

        ApplySignedIn(auth, machine, expected, "signed-in-token", "refresh-token", 1800);
        ApplyRefresh(auth, machine, expected, "refreshed-token", "refresh-token-2", 3600);
        ApplySignOut(auth, machine, expected);
        ApplyStaleRefreshAfterSignOut(auth, machine, "stale-token", "stale-refresh-token", 3600);

        Assert.Equal(expected, traceSink.Snapshot());
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

        var expected = new List<RuntimeTraceEvent>
        {
            CreateBindingTrace(BindingTarget.Header, new AuthState.Anonymous()),
            CreateBindingTrace(BindingTarget.Realtime, new AuthState.Anonymous())
        };
        var machine = new CanonicalAuthStateMachine();

        ApplySignedIn(auth, machine, expected, "signed-in-token", "refresh-token", 1800);
        ApplyFaultedRefresh(auth, machine, expected);

        Assert.Equal(expected, traceSink.Snapshot());
        Assert.DoesNotContain("Authorization", dynamicHeaders.Build().Keys);
        Assert.Equal(string.Empty, ReadPrivateStringMember(realtime, "AccessToken"));
    }

    private static void ApplySignedIn(
        global::Supabase.Gotrue.Client auth,
        CanonicalAuthStateMachine machine,
        List<RuntimeTraceEvent> expected,
        string accessToken,
        string refreshToken,
        long expiresIn)
    {
        var session = CreateSession(accessToken, refreshToken, expiresIn);
        SetCurrentSession(auth, session);
        var snapshot = CreateSnapshot(session);
        var state = machine.AdvanceAuthenticated(snapshot);

        AppendPublishedAndProjected(expected, AuthTraceKind.SignedInPublished, state);
        auth.NotifyAuthStateChange(GotrueAuthState.SignedIn);
    }

    private static void ApplyRefresh(
        global::Supabase.Gotrue.Client auth,
        CanonicalAuthStateMachine machine,
        List<RuntimeTraceEvent> expected,
        string accessToken,
        string refreshToken,
        long expiresIn)
    {
        var session = CreateSession(accessToken, refreshToken, expiresIn);
        SetCurrentSession(auth, session);
        var snapshot = CreateSnapshot(session);

        var refreshing = machine.BeginRefresh(snapshot);
        AppendPublishedAndProjected(expected, AuthTraceKind.RefreshBeginPublished, refreshing);

        var authenticated = machine.CompleteRefresh(snapshot);
        AppendPublishedAndProjected(expected, AuthTraceKind.RefreshCompletedPublished, authenticated);

        auth.NotifyAuthStateChange(GotrueAuthState.TokenRefreshed);
    }

    private static void ApplySignOut(
        global::Supabase.Gotrue.Client auth,
        CanonicalAuthStateMachine machine,
        List<RuntimeTraceEvent> expected)
    {
        Assert.True(machine.TrySignOut(out var signedOut));
        SetCurrentSession(auth, null);
        AppendPublishedAndProjected(expected, AuthTraceKind.SignedOutPublished, signedOut);
        auth.NotifyAuthStateChange(GotrueAuthState.SignedOut);
    }

    private static void ApplyStaleRefreshAfterSignOut(
        global::Supabase.Gotrue.Client auth,
        CanonicalAuthStateMachine machine,
        string accessToken,
        string refreshToken,
        long expiresIn)
    {
        Assert.True(machine.TryIgnoreStaleRefreshResultAfterSignOut());
        SetCurrentSession(auth, CreateSession(accessToken, refreshToken, expiresIn));
        auth.NotifyAuthStateChange(GotrueAuthState.TokenRefreshed);
    }

    private static void ApplyFaultedRefresh(
        global::Supabase.Gotrue.Client auth,
        CanonicalAuthStateMachine machine,
        List<RuntimeTraceEvent> expected)
    {
        SetCurrentSession(auth, null);
        var faulted = machine.Fault("Token refresh completed without a valid session.");
        AppendPublishedAndProjected(expected, AuthTraceKind.FaultedPublished, faulted);
        auth.NotifyAuthStateChange(GotrueAuthState.TokenRefreshed);
    }

    private static void AppendPublishedAndProjected(List<RuntimeTraceEvent> expected, AuthTraceKind kind, AuthState state)
    {
        expected.Add(new AuthTraceEvent(
            kind,
            CanonicalAuthStateMachine.ToStateName(state),
            state.CanonicalVersion,
            CanonicalAuthStateMachine.GetPendingRefreshVersion(state)));
        expected.Add(CreateBindingTrace(BindingTarget.Header, state));
        expected.Add(CreateBindingTrace(BindingTarget.Realtime, state));
    }

    private static BindingProjectionTraceEvent CreateBindingTrace(BindingTarget target, AuthState state)
    {
        var action = state is AuthState.Authenticated or AuthState.Refreshing
            ? BindingProjectionAction.Applied
            : BindingProjectionAction.Cleared;

        return new BindingProjectionTraceEvent(
            target,
            action,
            CanonicalAuthStateMachine.ToStateName(state),
            state.CanonicalVersion,
            CanonicalAuthStateMachine.GetPendingRefreshVersion(state),
            CanonicalAuthStateMachine.GetProjectionVersion(state));
    }

    private static SessionSnapshot CreateSnapshot(global::Supabase.Gotrue.Session session)
    {
        Assert.True(GotrueAuthStateBridge.TryCreateSessionSnapshot(session, out var snapshot));
        return snapshot;
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
