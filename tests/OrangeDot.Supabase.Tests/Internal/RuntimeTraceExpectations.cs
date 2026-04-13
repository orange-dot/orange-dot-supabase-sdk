using System;
using System.Collections.Generic;
using System.Linq;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Internal;
using Xunit;

namespace OrangeDot.Supabase.Tests.Internal;

internal abstract record AuthTraceScenarioStep
{
    internal sealed record SignIn(global::Supabase.Gotrue.Session Session) : AuthTraceScenarioStep;

    internal sealed record Refresh(global::Supabase.Gotrue.Session Session) : AuthTraceScenarioStep;

    internal sealed record SignOut() : AuthTraceScenarioStep;

    internal sealed record FaultedRefresh(string Reason) : AuthTraceScenarioStep;

    internal sealed record IgnoreStaleRefreshAfterSignOut(global::Supabase.Gotrue.Session Session) : AuthTraceScenarioStep;
}

internal sealed class AuthTraceExpectationBuilder
{
    private static readonly BindingTarget[] BindingTargets = [BindingTarget.Header, BindingTarget.Realtime];

    private readonly CanonicalAuthStateMachine _machine;
    private readonly List<RuntimeTraceEvent> _events = [];

    private AuthTraceExpectationBuilder(CanonicalAuthStateMachine machine)
    {
        _machine = machine;
    }

    internal static AuthTraceExpectationBuilder StartWithAnonymousBindings(CanonicalAuthStateMachine? machine = null)
    {
        var builder = new AuthTraceExpectationBuilder(machine ?? new CanonicalAuthStateMachine());

        foreach (var target in BindingTargets)
        {
            builder.AppendBindingProjection(target, new AuthState.Anonymous());
        }

        return builder;
    }

    internal AuthTraceExpectationBuilder Apply(params AuthTraceScenarioStep[] steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        foreach (var step in steps)
        {
            ArgumentNullException.ThrowIfNull(step);

            switch (step)
            {
                case AuthTraceScenarioStep.SignIn(var session):
                    AppendPublishedAndProjected(AuthTraceKind.SignedInPublished, _machine.AdvanceAuthenticated(CreateSnapshot(session)));
                    break;
                case AuthTraceScenarioStep.Refresh(var session):
                    var snapshot = CreateSnapshot(session);
                    AppendPublishedAndProjected(AuthTraceKind.RefreshBeginPublished, _machine.BeginRefresh(snapshot));
                    AppendPublishedAndProjected(AuthTraceKind.RefreshCompletedPublished, _machine.CompleteRefresh(snapshot));
                    break;
                case AuthTraceScenarioStep.SignOut:
                    Assert.True(_machine.TrySignOut(out var signedOut));
                    AppendPublishedAndProjected(AuthTraceKind.SignedOutPublished, signedOut);
                    break;
                case AuthTraceScenarioStep.FaultedRefresh(var reason):
                    AppendPublishedAndProjected(AuthTraceKind.FaultedPublished, _machine.Fault(reason));
                    break;
                case AuthTraceScenarioStep.IgnoreStaleRefreshAfterSignOut:
                    Assert.True(_machine.TryIgnoreStaleRefreshResultAfterSignOut());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, "Unknown auth trace scenario step.");
            }
        }

        return this;
    }

    internal AuthTraceExpectationBuilder AppendAuthPublish(AuthTraceKind kind, AuthState state)
    {
        _events.Add(new AuthTraceEvent(
            kind,
            CanonicalAuthStateMachine.ToStateName(state),
            state.CanonicalVersion,
            CanonicalAuthStateMachine.GetPendingRefreshVersion(state)));
        return this;
    }

    internal AuthTraceExpectationBuilder AppendBindingProjection(BindingTarget target, AuthState state)
    {
        var action = state is AuthState.Authenticated or AuthState.Refreshing
            ? BindingProjectionAction.Applied
            : BindingProjectionAction.Cleared;

        _events.Add(new BindingProjectionTraceEvent(
            target,
            action,
            CanonicalAuthStateMachine.ToStateName(state),
            state.CanonicalVersion,
            CanonicalAuthStateMachine.GetPendingRefreshVersion(state),
            CanonicalAuthStateMachine.GetProjectionVersion(state)));
        return this;
    }

    internal RuntimeTraceEvent[] Build()
    {
        return _events.ToArray();
    }

    internal CanonicalAuthSnapshot CaptureSnapshot()
    {
        return _machine.CaptureSnapshot();
    }

    private void AppendPublishedAndProjected(AuthTraceKind kind, AuthState state)
    {
        AppendAuthPublish(kind, state);

        foreach (var target in BindingTargets)
        {
            AppendBindingProjection(target, state);
        }
    }

    private static SessionSnapshot CreateSnapshot(global::Supabase.Gotrue.Session session)
    {
        Assert.True(GotrueAuthStateBridge.TryCreateSessionSnapshot(session, out var snapshot));
        return snapshot;
    }
}

internal abstract record ShellLifecycleTraceScenarioStep
{
    internal sealed record Deny(string MemberName) : ShellLifecycleTraceScenarioStep;

    internal sealed record ReadyCompleted() : ShellLifecycleTraceScenarioStep;

    internal sealed record ReadyFaulted() : ShellLifecycleTraceScenarioStep;

    internal sealed record ReadyCanceled() : ShellLifecycleTraceScenarioStep;

    internal sealed record Allow(string MemberName) : ShellLifecycleTraceScenarioStep;
}

internal sealed class ShellLifecycleTraceExpectationBuilder
{
    private readonly LifecycleStateMachine _machine;
    private readonly List<RuntimeTraceEvent> _events = [];

    private ShellLifecycleTraceExpectationBuilder(LifecycleStateMachine machine)
    {
        _machine = machine;
    }

    internal static ShellLifecycleTraceExpectationBuilder Create(LifecycleStateMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        return new ShellLifecycleTraceExpectationBuilder(machine);
    }

    internal ShellLifecycleTraceExpectationBuilder Apply(params ShellLifecycleTraceScenarioStep[] steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        foreach (var step in steps)
        {
            ArgumentNullException.ThrowIfNull(step);

            switch (step)
            {
                case ShellLifecycleTraceScenarioStep.Deny(var memberName):
                    _machine.AttemptPublicOperation();
                    AppendShellDenied(memberName);
                    break;
                case ShellLifecycleTraceScenarioStep.ReadyCompleted:
                    _machine.SignalReady();
                    AppendShellReadyCompleted();
                    break;
                case ShellLifecycleTraceScenarioStep.ReadyFaulted:
                    _machine.FailReady();
                    AppendShellReadyFaulted();
                    break;
                case ShellLifecycleTraceScenarioStep.ReadyCanceled:
                    _machine.CancelReady();
                    AppendShellReadyCanceled();
                    break;
                case ShellLifecycleTraceScenarioStep.Allow(var memberName):
                    _machine.AttemptPublicOperation();
                    AppendShellAllowed(memberName);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, "Unknown shell lifecycle trace scenario step.");
            }
        }

        return this;
    }

    internal ShellLifecycleTraceExpectationBuilder AppendShellDenied(string memberName)
    {
        _events.Add(new LifecycleTraceEvent(LifecycleTraceKind.PublicAccessDenied, memberName));
        return this;
    }

    internal ShellLifecycleTraceExpectationBuilder AppendShellReadyCompleted()
    {
        _events.Add(new LifecycleTraceEvent(LifecycleTraceKind.ReadyCompleted));
        return this;
    }

    internal ShellLifecycleTraceExpectationBuilder AppendShellReadyFaulted()
    {
        _events.Add(new LifecycleTraceEvent(LifecycleTraceKind.ReadyFaulted));
        return this;
    }

    internal ShellLifecycleTraceExpectationBuilder AppendShellReadyCanceled()
    {
        _events.Add(new LifecycleTraceEvent(LifecycleTraceKind.ReadyCanceled));
        return this;
    }

    internal ShellLifecycleTraceExpectationBuilder AppendShellAllowed(string memberName)
    {
        _events.Add(new LifecycleTraceEvent(LifecycleTraceKind.PublicAccessAllowed, memberName));
        return this;
    }

    internal RuntimeTraceEvent[] Build()
    {
        return _events.ToArray();
    }

    internal LifecycleStateMachineSnapshot CaptureSnapshot()
    {
        return _machine.CaptureSnapshot();
    }
}

internal static class RuntimeTraceAssert
{
    internal static void EqualSequence(IEnumerable<RuntimeTraceEvent> expected, IReadOnlyList<RuntimeTraceEvent> actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        Assert.Equal(expected.ToArray(), actual.ToArray());
    }
}
