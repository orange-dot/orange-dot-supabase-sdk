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

public sealed class StartupServiceRuntimeTraceConformanceTests
{
    [Fact]
    public async Task Startup_service_trace_matches_successful_start_then_public_access_sequence()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var service = CreateStartupService(shell, CreateValidOptions(), traceSink);
        var builder = StartupServiceTraceExpectationBuilder
            .Create()
            .Apply(
                new StartupServiceTraceScenarioStep.StartRequested(),
                new StartupServiceTraceScenarioStep.PrePublishWindowEntered(),
                new StartupServiceTraceScenarioStep.ReadyCompleted(),
                new StartupServiceTraceScenarioStep.Allow("Auth"));

        await service.StartAsync(CancellationToken.None);
        await shell.Ready;
        _ = shell.Auth;

        RuntimeTraceAssert.EqualSequence(builder.Build(), CaptureStartupAndLifecycleTrace(traceSink));

        var startup = builder.CaptureStartupSnapshot();
        Assert.Equal(StartupServicePhase.ReadyPublished, startup.Phase);
        Assert.False(startup.StopRequested);

        var lifecycle = builder.CaptureLifecycleSnapshot();
        Assert.Equal(LifecyclePhase.Ready, lifecycle.Phase);
        Assert.Equal(1, lifecycle.PublicAttempts);
        Assert.Equal(1, lifecycle.ChildCalls);
    }

    [Fact]
    public async Task Startup_service_trace_matches_faulted_start_sequence()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var service = CreateStartupService(shell, new SupabaseOptions
        {
            Url = "not a url",
            PublishableKey = "publishable-key"
        }, traceSink);
        var builder = StartupServiceTraceExpectationBuilder
            .Create()
            .Apply(
                new StartupServiceTraceScenarioStep.StartRequested(),
                new StartupServiceTraceScenarioStep.StartFaulted(),
                new StartupServiceTraceScenarioStep.Deny("Auth"));

        await Assert.ThrowsAsync<SupabaseConfigurationException>(() => service.StartAsync(CancellationToken.None));
        await Assert.ThrowsAsync<SupabaseConfigurationException>(async () => await shell.Ready);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);

        RuntimeTraceAssert.EqualSequence(builder.Build(), CaptureStartupAndLifecycleTrace(traceSink));
    }

    [Fact]
    public async Task Startup_service_trace_matches_canceled_start_sequence()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var service = CreateStartupService(shell, CreateValidOptions(), traceSink);
        var builder = StartupServiceTraceExpectationBuilder
            .Create()
            .Apply(
                new StartupServiceTraceScenarioStep.StartRequested(),
                new StartupServiceTraceScenarioStep.StartCanceled(),
                new StartupServiceTraceScenarioStep.Deny("Auth"));

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StartAsync(cancellationTokenSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await shell.Ready);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);

        RuntimeTraceAssert.EqualSequence(builder.Build(), CaptureStartupAndLifecycleTrace(traceSink));
    }

    [Fact]
    public async Task Startup_service_trace_matches_stop_before_start_sequence()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var service = CreateStartupService(shell, CreateValidOptions(), traceSink);
        var builder = StartupServiceTraceExpectationBuilder
            .Create()
            .Apply(
                new StartupServiceTraceScenarioStep.StopRequested(),
                new StartupServiceTraceScenarioStep.StartRequested(),
                new StartupServiceTraceScenarioStep.PrePublishWindowEntered(),
                new StartupServiceTraceScenarioStep.ReadyPublicationSkippedBecauseStopping(),
                new StartupServiceTraceScenarioStep.Deny("Auth"));

        await service.StopAsync(CancellationToken.None);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await shell.Ready);
        await service.StartAsync(CancellationToken.None);

        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);
        RuntimeTraceAssert.EqualSequence(builder.Build(), CaptureStartupAndLifecycleTrace(traceSink));
    }

    [Fact]
    public async Task Startup_service_trace_matches_pre_publish_stop_overlap_sequence()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var service = CreateStartupService(shell, CreateValidOptions(), traceSink);
        var builder = StartupServiceTraceExpectationBuilder
            .Create()
            .Apply(
                new StartupServiceTraceScenarioStep.StartRequested(),
                new StartupServiceTraceScenarioStep.PrePublishWindowEntered(),
                new StartupServiceTraceScenarioStep.StopRequested(),
                new StartupServiceTraceScenarioStep.ReadyPublicationSkippedBecauseStopping(),
                new StartupServiceTraceScenarioStep.Deny("Auth"));

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
        RuntimeTraceAssert.EqualSequence(builder.Build(), CaptureStartupAndLifecycleTrace(traceSink));

        var startup = builder.CaptureStartupSnapshot();
        Assert.Equal(StartupServicePhase.PublicationSkippedBecauseStopping, startup.Phase);
        Assert.True(startup.StopRequested);
        Assert.Equal(1, startup.PublicationSkips);
    }

    private static RuntimeTraceEvent[] CaptureStartupAndLifecycleTrace(RecordingRuntimeTraceSink traceSink)
    {
        return traceSink.Snapshot()
            .Where(static traceEvent => traceEvent is StartupTraceEvent or LifecycleTraceEvent)
            .ToArray();
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
