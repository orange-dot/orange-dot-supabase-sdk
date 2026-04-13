using System;

namespace OrangeDot.Supabase.Internal;

internal interface IRuntimeTraceSink
{
    void Record(RuntimeTraceEvent traceEvent);
}

internal sealed class NoOpRuntimeTraceSink : IRuntimeTraceSink
{
    internal static IRuntimeTraceSink Instance { get; } = new NoOpRuntimeTraceSink();

    private NoOpRuntimeTraceSink()
    {
    }

    public void Record(RuntimeTraceEvent traceEvent)
    {
        ArgumentNullException.ThrowIfNull(traceEvent);
    }
}

internal abstract record RuntimeTraceEvent;

internal enum AuthTraceKind
{
    InitialSessionPublished,
    SignedInPublished,
    UserUpdatedPublished,
    RefreshBeginPublished,
    RefreshCompletedPublished,
    SignedOutPublished,
    FaultedPublished,
    MfaChallengeVerifiedPublished
}

internal sealed record AuthTraceEvent(
    AuthTraceKind Kind,
    string State,
    long CanonicalVersion,
    long PendingRefreshVersion) : RuntimeTraceEvent;

internal enum BindingTarget
{
    Header,
    Realtime
}

internal enum BindingProjectionAction
{
    Applied,
    Cleared
}

internal sealed record BindingProjectionTraceEvent(
    BindingTarget Target,
    BindingProjectionAction Action,
    string State,
    long CanonicalVersion,
    long PendingRefreshVersion,
    long ProjectedVersion) : RuntimeTraceEvent;

internal enum StartupTraceKind
{
    StartRequested,
    PrePublishWindowEntered,
    ReadyPublicationSkippedBecauseStopping,
    StopRequested,
    StartCanceled,
    StartFaulted
}

internal sealed record StartupTraceEvent(StartupTraceKind Kind) : RuntimeTraceEvent;

internal enum LifecycleTraceKind
{
    PublicAccessDenied,
    ReadyCompleted,
    ReadyCanceled,
    ReadyFaulted,
    PublicAccessAllowed
}

internal sealed record LifecycleTraceEvent(
    LifecycleTraceKind Kind,
    string? MemberName = null) : RuntimeTraceEvent;
