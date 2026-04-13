using System;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Internal;
using OrangeDot.Supabase.Tests.Internal;

namespace OrangeDot.Supabase.Tests.Lifecycle;

public sealed class ShellRuntimeTraceConformanceTests
{
    [Fact]
    public async Task Shell_trace_matches_pre_ready_then_ready_access_sequence()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var lifecycle = CreateInitializingLifecycle();

        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);
        lifecycle.AttemptPublicOperation();

        shell.SetInitializedClient(await CreateReadyClient());
        lifecycle.SignalReady();

        _ = shell.Auth;
        lifecycle.AttemptPublicOperation();

        Assert.Equal(
            [
                new LifecycleTraceEvent(LifecycleTraceKind.PublicAccessDenied, "Auth"),
                new LifecycleTraceEvent(LifecycleTraceKind.ReadyCompleted),
                new LifecycleTraceEvent(LifecycleTraceKind.PublicAccessAllowed, "Auth")
            ],
            traceSink.Snapshot());

        var snapshot = lifecycle.CaptureSnapshot();
        Assert.Equal(LifecyclePhase.Ready, snapshot.Phase);
        Assert.Equal(2, snapshot.PublicAttempts);
        Assert.Equal(1, snapshot.PublicDenied);
        Assert.Equal(1, snapshot.ChildCalls);
    }

    [Fact]
    public void Shell_trace_matches_faulted_readiness_sequence()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var lifecycle = CreateInitializingLifecycle();

        shell.SetInitializationFailed(new InvalidOperationException("forced"));
        lifecycle.FailReady();

        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);
        lifecycle.AttemptPublicOperation();

        Assert.Equal(
            [
                new LifecycleTraceEvent(LifecycleTraceKind.ReadyFaulted),
                new LifecycleTraceEvent(LifecycleTraceKind.PublicAccessDenied, "Auth")
            ],
            traceSink.Snapshot());

        var snapshot = lifecycle.CaptureSnapshot();
        Assert.Equal(LifecyclePhase.Faulted, snapshot.Phase);
        Assert.Equal(1, snapshot.PublicAttempts);
        Assert.Equal(1, snapshot.PublicDenied);
        Assert.Equal(0, snapshot.ChildCalls);
    }

    [Fact]
    public void Shell_trace_matches_canceled_readiness_sequence()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var lifecycle = CreateInitializingLifecycle();

        shell.SetInitializationCanceled(default);
        lifecycle.CancelReady();

        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);
        lifecycle.AttemptPublicOperation();

        Assert.Equal(
            [
                new LifecycleTraceEvent(LifecycleTraceKind.ReadyCanceled),
                new LifecycleTraceEvent(LifecycleTraceKind.PublicAccessDenied, "Auth")
            ],
            traceSink.Snapshot());

        var snapshot = lifecycle.CaptureSnapshot();
        Assert.Equal(LifecyclePhase.Canceled, snapshot.Phase);
        Assert.Equal(1, snapshot.PublicAttempts);
        Assert.Equal(1, snapshot.PublicDenied);
        Assert.Equal(0, snapshot.ChildCalls);
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

    private static async Task<SupabaseClient> CreateReadyClient()
    {
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            PublishableKey = "publishable-key"
        });

        var hydrated = await configured.LoadPersistedSessionAsync();
        return await hydrated.InitializeAsync();
    }
}
