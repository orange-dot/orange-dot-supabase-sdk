using System;
using System.Collections.Generic;

namespace OrangeDot.Supabase.Internal;

internal enum LifecycleModelActionKind
{
    StartRequested,
    PrePublishWindowEntered,
    StopRequested,
    ReadyCompleted,
    StartFaulted,
    StartCanceled,
    ReadyPublicationSkippedBecauseStopping,
    PublicAccessDenied,
    PublicAccessAllowed
}

internal sealed record LifecycleModelAction(LifecycleModelActionKind Kind, string? MemberName = null);

internal sealed class LifecycleTraceToModelActionTranslator
{
    private StartupServiceStateMachine _startup = new();
    private LifecycleStateMachine _lifecycle = new();
    private bool _awaitingReadyFaultAck;
    private bool _awaitingReadyCanceledAck;

    internal IReadOnlyList<LifecycleModelAction> Translate(IEnumerable<RuntimeTraceEvent> traceEvents)
    {
        ArgumentNullException.ThrowIfNull(traceEvents);
        Reset();

        var actions = new List<LifecycleModelAction>();

        foreach (var traceEvent in traceEvents)
        {
            switch (traceEvent)
            {
                case StartupTraceEvent startupTrace:
                    TranslateStartupTrace(startupTrace, actions);
                    break;
                case LifecycleTraceEvent lifecycleTrace:
                    TranslateLifecycleTrace(lifecycleTrace, actions);
                    break;
            }
        }

        if (_awaitingReadyFaultAck)
        {
            throw new InvalidOperationException("Lifecycle trace ended before the expected ReadyFaulted acknowledgement.");
        }

        if (_awaitingReadyCanceledAck)
        {
            throw new InvalidOperationException("Lifecycle trace ended before the expected ReadyCanceled acknowledgement.");
        }

        return actions.ToArray();
    }

    private void Reset()
    {
        _startup = new StartupServiceStateMachine();
        _lifecycle = new LifecycleStateMachine();
        _awaitingReadyFaultAck = false;
        _awaitingReadyCanceledAck = false;
    }

    private void TranslateStartupTrace(StartupTraceEvent startupTrace, List<LifecycleModelAction> actions)
    {
        switch (startupTrace.Kind)
        {
            case StartupTraceKind.StartRequested:
                _startup.StartRequested();

                if (_lifecycle.Phase == LifecyclePhase.Configured)
                {
                    _lifecycle.LoadPersistedSessionStart();
                    _lifecycle.LoadPersistedSessionComplete();
                    _lifecycle.InitializeStart();
                    _lifecycle.InitializeComplete();
                }

                actions.Add(new LifecycleModelAction(LifecycleModelActionKind.StartRequested));
                return;
            case StartupTraceKind.PrePublishWindowEntered:
                _startup.EnterPrePublishWindow();
                actions.Add(new LifecycleModelAction(LifecycleModelActionKind.PrePublishWindowEntered));
                return;
            case StartupTraceKind.StopRequested:
                _startup.RequestStop();

                if (IsPreReadyPhase(_lifecycle.Phase))
                {
                    _lifecycle.CancelReady();
                    _awaitingReadyCanceledAck = true;
                }

                actions.Add(new LifecycleModelAction(LifecycleModelActionKind.StopRequested));
                return;
            case StartupTraceKind.StartFaulted:
                _startup.FailStart();
                _lifecycle.FailReady();
                _awaitingReadyFaultAck = true;
                actions.Add(new LifecycleModelAction(LifecycleModelActionKind.StartFaulted));
                return;
            case StartupTraceKind.StartCanceled:
                _startup.CancelStart();
                _lifecycle.CancelReady();
                _awaitingReadyCanceledAck = true;
                actions.Add(new LifecycleModelAction(LifecycleModelActionKind.StartCanceled));
                return;
            case StartupTraceKind.ReadyPublicationSkippedBecauseStopping:
                _startup.SkipReadyPublicationBecauseStopping();
                actions.Add(new LifecycleModelAction(LifecycleModelActionKind.ReadyPublicationSkippedBecauseStopping));
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(startupTrace), startupTrace.Kind, "Unknown startup trace kind.");
        }
    }

    private void TranslateLifecycleTrace(LifecycleTraceEvent lifecycleTrace, List<LifecycleModelAction> actions)
    {
        switch (lifecycleTrace.Kind)
        {
            case LifecycleTraceKind.ReadyCompleted:
                _startup.PublishReady();
                _lifecycle.SignalReady();
                actions.Add(new LifecycleModelAction(LifecycleModelActionKind.ReadyCompleted));
                return;
            case LifecycleTraceKind.ReadyFaulted:
                if (!_awaitingReadyFaultAck)
                {
                    throw new InvalidOperationException("ReadyFaulted was observed without a preceding StartFaulted action.");
                }

                _awaitingReadyFaultAck = false;
                return;
            case LifecycleTraceKind.ReadyCanceled:
                if (!_awaitingReadyCanceledAck)
                {
                    throw new InvalidOperationException("ReadyCanceled was observed without a preceding cancellation action.");
                }

                _awaitingReadyCanceledAck = false;
                return;
            case LifecycleTraceKind.PublicAccessDenied:
                _lifecycle.AttemptPublicOperation();
                actions.Add(new LifecycleModelAction(LifecycleModelActionKind.PublicAccessDenied, lifecycleTrace.MemberName));
                return;
            case LifecycleTraceKind.PublicAccessAllowed:
                _lifecycle.AttemptPublicOperation();
                actions.Add(new LifecycleModelAction(LifecycleModelActionKind.PublicAccessAllowed, lifecycleTrace.MemberName));
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifecycleTrace), lifecycleTrace.Kind, "Unknown lifecycle trace kind.");
        }
    }

    private static bool IsPreReadyPhase(LifecyclePhase phase)
    {
        return phase is LifecyclePhase.Configured or LifecyclePhase.LoadingSession or LifecyclePhase.Initializing;
    }
}
