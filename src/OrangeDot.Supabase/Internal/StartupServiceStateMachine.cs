using System;

namespace OrangeDot.Supabase.Internal;

internal enum StartupServicePhase
{
    Idle,
    PreparingClient,
    PrePublishWindow,
    ReadyPublished,
    PublicationSkippedBecauseStopping,
    Faulted,
    Canceled
}

internal sealed record StartupServiceStateMachineSnapshot(
    StartupServicePhase Phase,
    bool StopRequested,
    int StartAttempts,
    int StopAttempts,
    int PrePublishWindows,
    int PublicationSkips);

internal sealed class StartupServiceStateMachine
{
    private StartupServicePhase _phase;
    private bool _stopRequested;
    private int _startAttempts;
    private int _stopAttempts;
    private int _prePublishWindows;
    private int _publicationSkips;

    internal StartupServicePhase Phase => _phase;

    internal void StartRequested()
    {
        Require(
            _phase is StartupServicePhase.Idle
                or StartupServicePhase.ReadyPublished
                or StartupServicePhase.PublicationSkippedBecauseStopping
                or StartupServicePhase.Faulted
                or StartupServicePhase.Canceled,
            "start_requested requires an idle or completed startup phase.");
        _startAttempts++;
        _phase = StartupServicePhase.PreparingClient;
    }

    internal void EnterPrePublishWindow()
    {
        Require(
            _phase == StartupServicePhase.PreparingClient,
            "enter_pre_publish_window requires a prepared client.");
        _prePublishWindows++;
        _phase = StartupServicePhase.PrePublishWindow;
    }

    internal void PublishReady()
    {
        Require(
            _phase is StartupServicePhase.PreparingClient or StartupServicePhase.PrePublishWindow,
            "publish_ready requires a prepared client.");
        Require(!_stopRequested, "publish_ready cannot occur after stop was requested.");
        _phase = StartupServicePhase.ReadyPublished;
    }

    internal void FailStart()
    {
        Require(
            _phase is StartupServicePhase.PreparingClient or StartupServicePhase.PrePublishWindow,
            "fail_start requires a prepared client.");
        _phase = StartupServicePhase.Faulted;
    }

    internal void CancelStart()
    {
        Require(
            _phase is StartupServicePhase.PreparingClient or StartupServicePhase.PrePublishWindow,
            "cancel_start requires a prepared client.");
        _phase = StartupServicePhase.Canceled;
    }

    internal void RequestStop()
    {
        _stopAttempts++;
        _stopRequested = true;
    }

    internal void SkipReadyPublicationBecauseStopping()
    {
        Require(_stopRequested, "skip_ready_publication_because_stopping requires stop to be requested.");
        Require(
            _phase is StartupServicePhase.PreparingClient or StartupServicePhase.PrePublishWindow,
            "skip_ready_publication_because_stopping requires a prepared client.");
        _publicationSkips++;
        _phase = StartupServicePhase.PublicationSkippedBecauseStopping;
    }

    internal StartupServiceStateMachineSnapshot CaptureSnapshot()
    {
        return new StartupServiceStateMachineSnapshot(
            _phase,
            _stopRequested,
            _startAttempts,
            _stopAttempts,
            _prePublishWindows,
            _publicationSkips);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
