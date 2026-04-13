using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Errors;
using OrangeDot.Supabase.Internal;
using OrangeDot.Supabase.Tests.Internal;

namespace OrangeDot.Supabase.Tests.Lifecycle;

public sealed class LifecycleTraceToModelActionTranslatorTests
{
    [Fact]
    public async Task Runtime_trace_translates_to_model_actions_for_successful_start_then_allowed_access()
    {
        var trace = await CaptureTraceForSuccessfulStartThenAllowedAccess();

        var actions = new LifecycleTraceToModelActionTranslator().Translate(trace);

        Assert.Equal(
            [
                Action(LifecycleModelActionKind.StartRequested),
                Action(LifecycleModelActionKind.PrePublishWindowEntered),
                Action(LifecycleModelActionKind.ReadyCompleted),
                Action(LifecycleModelActionKind.PublicAccessAllowed, "Auth")
            ],
            actions);
    }

    [Fact]
    public async Task Runtime_trace_translates_to_model_actions_for_faulted_start_then_denied_access()
    {
        var trace = await CaptureTraceForFaultedStartThenDeniedAccess();

        var actions = new LifecycleTraceToModelActionTranslator().Translate(trace);

        Assert.Equal(
            [
                Action(LifecycleModelActionKind.StartRequested),
                Action(LifecycleModelActionKind.StartFaulted),
                Action(LifecycleModelActionKind.PublicAccessDenied, "Auth")
            ],
            actions);
    }

    [Fact]
    public async Task Runtime_trace_translates_to_model_actions_for_canceled_start_then_denied_access()
    {
        var trace = await CaptureTraceForCanceledStartThenDeniedAccess();

        var actions = new LifecycleTraceToModelActionTranslator().Translate(trace);

        Assert.Equal(
            [
                Action(LifecycleModelActionKind.StartRequested),
                Action(LifecycleModelActionKind.StartCanceled),
                Action(LifecycleModelActionKind.PublicAccessDenied, "Auth")
            ],
            actions);
    }

    [Fact]
    public async Task Runtime_trace_translates_to_model_actions_for_stop_before_start_then_skipped_publication()
    {
        var trace = await CaptureTraceForStopBeforeStartThenSkippedPublication();

        var actions = new LifecycleTraceToModelActionTranslator().Translate(trace);

        Assert.Equal(
            [
                Action(LifecycleModelActionKind.StopRequested),
                Action(LifecycleModelActionKind.StartRequested),
                Action(LifecycleModelActionKind.PrePublishWindowEntered),
                Action(LifecycleModelActionKind.ReadyPublicationSkippedBecauseStopping),
                Action(LifecycleModelActionKind.PublicAccessDenied, "Auth")
            ],
            actions);
    }

    [Fact]
    public async Task Runtime_trace_translates_to_model_actions_for_pre_publish_stop_overlap()
    {
        var trace = await CaptureTraceForPrePublishStopOverlap();

        var actions = new LifecycleTraceToModelActionTranslator().Translate(trace);

        Assert.Equal(
            [
                Action(LifecycleModelActionKind.StartRequested),
                Action(LifecycleModelActionKind.PrePublishWindowEntered),
                Action(LifecycleModelActionKind.StopRequested),
                Action(LifecycleModelActionKind.ReadyPublicationSkippedBecauseStopping),
                Action(LifecycleModelActionKind.PublicAccessDenied, "Auth")
            ],
            actions);
    }

    private static async Task<IReadOnlyList<RuntimeTraceEvent>> CaptureTraceForSuccessfulStartThenAllowedAccess()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var service = CreateStartupService(shell, CreateValidOptions(), traceSink);

        await service.StartAsync(CancellationToken.None);
        await shell.Ready;
        _ = shell.Auth;

        return CaptureStartupAndLifecycleTrace(traceSink);
    }

    private static async Task<IReadOnlyList<RuntimeTraceEvent>> CaptureTraceForFaultedStartThenDeniedAccess()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var service = CreateStartupService(shell, new SupabaseOptions
        {
            Url = "not a url",
            PublishableKey = "publishable-key"
        }, traceSink);

        await Assert.ThrowsAsync<SupabaseConfigurationException>(() => service.StartAsync(CancellationToken.None));
        await Assert.ThrowsAsync<SupabaseConfigurationException>(async () => await shell.Ready);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);

        return CaptureStartupAndLifecycleTrace(traceSink);
    }

    private static async Task<IReadOnlyList<RuntimeTraceEvent>> CaptureTraceForCanceledStartThenDeniedAccess()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var service = CreateStartupService(shell, CreateValidOptions(), traceSink);

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StartAsync(cancellationTokenSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await shell.Ready);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);

        return CaptureStartupAndLifecycleTrace(traceSink);
    }

    private static async Task<IReadOnlyList<RuntimeTraceEvent>> CaptureTraceForStopBeforeStartThenSkippedPublication()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var service = CreateStartupService(shell, CreateValidOptions(), traceSink);

        await service.StopAsync(CancellationToken.None);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await shell.Ready);
        await service.StartAsync(CancellationToken.None);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);

        return CaptureStartupAndLifecycleTrace(traceSink);
    }

    private static async Task<IReadOnlyList<RuntimeTraceEvent>> CaptureTraceForPrePublishStopOverlap()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var service = CreateStartupService(shell, CreateValidOptions(), traceSink);

        var reachedPrePublishWindow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.BeforePublishTestHookAsync = () =>
        {
            reachedPrePublishWindow.TrySetResult();
            return resumeStart.Task;
        };

        var startTask = Task.Run(() => service.StartAsync(CancellationToken.None));

        await reachedPrePublishWindow.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await shell.Ready);

        resumeStart.TrySetResult();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);

        return CaptureStartupAndLifecycleTrace(traceSink);
    }

    private static IReadOnlyList<RuntimeTraceEvent> CaptureStartupAndLifecycleTrace(RecordingRuntimeTraceSink traceSink)
    {
        return traceSink.Snapshot()
            .Where(static traceEvent => traceEvent is StartupTraceEvent or LifecycleTraceEvent)
            .ToArray();
    }

    private static LifecycleModelAction Action(LifecycleModelActionKind kind, string? memberName = null)
    {
        return new LifecycleModelAction(kind, memberName);
    }

    private static SupabaseStartupService CreateStartupService(
        SupabaseClientShell shell,
        SupabaseOptions options,
        IRuntimeTraceSink traceSink)
    {
        return new SupabaseStartupService(
            Options.Create(options),
            shell,
            NullLogger<SupabaseStartupService>.Instance,
            NullLoggerFactory.Instance,
            new AuthStateObserver(),
            meterFactory: null,
            traceSink: traceSink);
    }

    private static SupabaseOptions CreateValidOptions()
    {
        return new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            PublishableKey = "publishable-key"
        };
    }
}
