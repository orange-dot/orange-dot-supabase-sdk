using System;

namespace OrangeDot.Supabase.Internal;

internal enum LifecyclePhase
{
    Configured,
    LoadingSession,
    Initializing,
    Faulted,
    Canceled,
    Ready,
    Disposed
}

internal sealed record LifecycleStateMachineSnapshot(
    LifecyclePhase Phase,
    int ChildCalls,
    int PublicAttempts,
    int PublicDenied);

internal sealed class LifecycleStateMachine
{
    private LifecyclePhase _phase;
    private int _childCalls;
    private int _publicAttempts;
    private int _publicDenied;

    internal LifecycleStateMachine()
        : this(LifecyclePhase.Configured)
    {
    }

    internal LifecycleStateMachine(
        LifecyclePhase phase,
        int childCalls = 0,
        int publicAttempts = 0,
        int publicDenied = 0)
    {
        _phase = phase;
        _childCalls = childCalls;
        _publicAttempts = publicAttempts;
        _publicDenied = publicDenied;
    }

    internal LifecyclePhase Phase => _phase;

    internal void AttemptPublicOperation()
    {
        _publicAttempts++;

        if (_phase == LifecyclePhase.Ready)
        {
            _childCalls++;
            return;
        }

        _publicDenied++;
    }

    internal void LoadPersistedSessionStart()
    {
        Require(_phase == LifecyclePhase.Configured, "load_persisted_session_start requires the configured phase.");
        _phase = LifecyclePhase.LoadingSession;
    }

    internal void LoadPersistedSessionComplete()
    {
        Require(_phase == LifecyclePhase.LoadingSession, "load_persisted_session_complete requires the loading phase.");
    }

    internal void InitializeStart()
    {
        Require(_phase == LifecyclePhase.LoadingSession, "initialize_start requires the loading phase.");
        _phase = LifecyclePhase.Initializing;
    }

    internal void InitializeComplete()
    {
        Require(_phase == LifecyclePhase.Initializing, "initialize_complete requires the initializing phase.");
    }

    internal void SignalReady()
    {
        Require(_phase == LifecyclePhase.Initializing, "signal_ready requires the initializing phase.");
        _phase = LifecyclePhase.Ready;
    }

    internal void FailReady()
    {
        Require(_phase is LifecyclePhase.Configured or LifecyclePhase.LoadingSession or LifecyclePhase.Initializing,
            "fail_ready requires a pre-ready lifecycle phase.");
        _phase = LifecyclePhase.Faulted;
    }

    internal void CancelReady()
    {
        Require(_phase is LifecyclePhase.Configured or LifecyclePhase.LoadingSession or LifecyclePhase.Initializing,
            "cancel_ready requires a pre-ready lifecycle phase.");
        _phase = LifecyclePhase.Canceled;
    }

    internal LifecycleStateMachineSnapshot CaptureSnapshot()
    {
        return new LifecycleStateMachineSnapshot(
            _phase,
            _childCalls,
            _publicAttempts,
            _publicDenied);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
