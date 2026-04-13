using System;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase.Tests.Internal;

public sealed class RuntimeTraceExpectationBuilderTests
{
    [Fact]
    public void Auth_builder_emits_publish_then_binding_projection_sequence()
    {
        var initialAnonymous = new AuthState.Anonymous();
        var signedInSession = CreateSession("signed-in-token", "refresh-token", 1800);
        var refreshedSession = CreateSession("refreshed-token", "refresh-token-2", 3600);
        var expected = new RuntimeTraceEvent[]
        {
            CreateBindingTrace(BindingTarget.Header, initialAnonymous),
            CreateBindingTrace(BindingTarget.Realtime, initialAnonymous),
            CreateAuthTrace(AuthTraceKind.SignedInPublished, 1, "Authenticated"),
            CreateBindingTrace(BindingTarget.Header, new AuthState.Authenticated(1, "signed-in-token", "refresh-token", CreateExpiresAt(signedInSession))),
            CreateBindingTrace(BindingTarget.Realtime, new AuthState.Authenticated(1, "signed-in-token", "refresh-token", CreateExpiresAt(signedInSession))),
            CreateAuthTrace(AuthTraceKind.RefreshBeginPublished, 1, "Refreshing", pendingRefreshVersion: 2),
            new BindingProjectionTraceEvent(BindingTarget.Header, BindingProjectionAction.Applied, "Refreshing", 1, 2, 1),
            new BindingProjectionTraceEvent(BindingTarget.Realtime, BindingProjectionAction.Applied, "Refreshing", 1, 2, 1),
            CreateAuthTrace(AuthTraceKind.RefreshCompletedPublished, 2, "Authenticated"),
            CreateBindingTrace(BindingTarget.Header, new AuthState.Authenticated(2, "refreshed-token", "refresh-token-2", CreateExpiresAt(refreshedSession))),
            CreateBindingTrace(BindingTarget.Realtime, new AuthState.Authenticated(2, "refreshed-token", "refresh-token-2", CreateExpiresAt(refreshedSession))),
            CreateAuthTrace(AuthTraceKind.SignedOutPublished, 2, "SignedOut"),
            new BindingProjectionTraceEvent(BindingTarget.Header, BindingProjectionAction.Cleared, "SignedOut", 2, 0, 0),
            new BindingProjectionTraceEvent(BindingTarget.Realtime, BindingProjectionAction.Cleared, "SignedOut", 2, 0, 0)
        };

        var builder = AuthTraceExpectationBuilder
            .StartWithAnonymousBindings()
            .Apply(
                new AuthTraceScenarioStep.SignIn(signedInSession),
                new AuthTraceScenarioStep.Refresh(refreshedSession),
                new AuthTraceScenarioStep.SignOut());

        RuntimeTraceAssert.EqualSequence(expected, builder.Build());
        Assert.Equal("SignedOut", builder.CaptureSnapshot().AuthState);
        Assert.Equal(2, builder.CaptureSnapshot().CanonicalVersion);
    }

    [Fact]
    public void Shell_builder_updates_trace_and_snapshot_for_ready_path()
    {
        var builder = ShellLifecycleTraceExpectationBuilder
            .Create(CreateInitializingLifecycle())
            .Apply(
                new ShellLifecycleTraceScenarioStep.Deny("Auth"),
                new ShellLifecycleTraceScenarioStep.ReadyCompleted(),
                new ShellLifecycleTraceScenarioStep.Allow("Auth"));

        RuntimeTraceAssert.EqualSequence(
            [
                new LifecycleTraceEvent(LifecycleTraceKind.PublicAccessDenied, "Auth"),
                new LifecycleTraceEvent(LifecycleTraceKind.ReadyCompleted),
                new LifecycleTraceEvent(LifecycleTraceKind.PublicAccessAllowed, "Auth")
            ],
            builder.Build());

        var snapshot = builder.CaptureSnapshot();
        Assert.Equal(LifecyclePhase.Ready, snapshot.Phase);
        Assert.Equal(2, snapshot.PublicAttempts);
        Assert.Equal(1, snapshot.PublicDenied);
        Assert.Equal(1, snapshot.ChildCalls);
    }

    private static RuntimeTraceEvent CreateAuthTrace(
        AuthTraceKind kind,
        long canonicalVersion,
        string state,
        long pendingRefreshVersion = 0)
    {
        return new AuthTraceEvent(kind, state, canonicalVersion, pendingRefreshVersion);
    }

    private static RuntimeTraceEvent CreateBindingTrace(BindingTarget target, AuthState state)
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

    private static DateTimeOffset CreateExpiresAt(global::Supabase.Gotrue.Session session)
    {
        return new DateTimeOffset(session.CreatedAt).AddSeconds(session.ExpiresIn);
    }

    private static LifecycleStateMachine CreateInitializingLifecycle()
    {
        var lifecycle = new LifecycleStateMachine();
        lifecycle.LoadPersistedSessionStart();
        lifecycle.LoadPersistedSessionComplete();
        lifecycle.InitializeStart();
        lifecycle.InitializeComplete();
        return lifecycle;
    }
}
