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
        var builder = ShellLifecycleTraceExpectationBuilder
            .Create(CreateInitializingLifecycle())
            .Apply(
                new ShellLifecycleTraceScenarioStep.Deny("Auth"),
                new ShellLifecycleTraceScenarioStep.ReadyCompleted(),
                new ShellLifecycleTraceScenarioStep.Allow("Auth"));

        await ExecuteScenario(shell,
            new ShellLifecycleTraceScenarioStep.Deny("Auth"),
            new ShellLifecycleTraceScenarioStep.ReadyCompleted(),
            new ShellLifecycleTraceScenarioStep.Allow("Auth"));

        RuntimeTraceAssert.EqualSequence(builder.Build(), traceSink.Snapshot());

        var snapshot = builder.CaptureSnapshot();
        Assert.Equal(LifecyclePhase.Ready, snapshot.Phase);
        Assert.Equal(2, snapshot.PublicAttempts);
        Assert.Equal(1, snapshot.PublicDenied);
        Assert.Equal(1, snapshot.ChildCalls);
    }

    [Fact]
    public async Task Shell_trace_matches_faulted_readiness_sequence()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var builder = ShellLifecycleTraceExpectationBuilder
            .Create(CreateInitializingLifecycle())
            .Apply(
                new ShellLifecycleTraceScenarioStep.ReadyFaulted(),
                new ShellLifecycleTraceScenarioStep.Deny("Auth"));

        await ExecuteScenario(shell,
            new ShellLifecycleTraceScenarioStep.ReadyFaulted(),
            new ShellLifecycleTraceScenarioStep.Deny("Auth"));

        RuntimeTraceAssert.EqualSequence(builder.Build(), traceSink.Snapshot());

        var snapshot = builder.CaptureSnapshot();
        Assert.Equal(LifecyclePhase.Faulted, snapshot.Phase);
        Assert.Equal(1, snapshot.PublicAttempts);
        Assert.Equal(1, snapshot.PublicDenied);
        Assert.Equal(0, snapshot.ChildCalls);
    }

    [Fact]
    public async Task Shell_trace_matches_canceled_readiness_sequence()
    {
        var traceSink = new RecordingRuntimeTraceSink();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance, traceSink);
        var builder = ShellLifecycleTraceExpectationBuilder
            .Create(CreateInitializingLifecycle())
            .Apply(
                new ShellLifecycleTraceScenarioStep.ReadyCanceled(),
                new ShellLifecycleTraceScenarioStep.Deny("Auth"));

        await ExecuteScenario(shell,
            new ShellLifecycleTraceScenarioStep.ReadyCanceled(),
            new ShellLifecycleTraceScenarioStep.Deny("Auth"));

        RuntimeTraceAssert.EqualSequence(builder.Build(), traceSink.Snapshot());

        var snapshot = builder.CaptureSnapshot();
        Assert.Equal(LifecyclePhase.Canceled, snapshot.Phase);
        Assert.Equal(1, snapshot.PublicAttempts);
        Assert.Equal(1, snapshot.PublicDenied);
        Assert.Equal(0, snapshot.ChildCalls);
    }

    private static async Task ExecuteScenario(SupabaseClientShell shell, params ShellLifecycleTraceScenarioStep[] steps)
    {
        foreach (var step in steps)
        {
            switch (step)
            {
                case ShellLifecycleTraceScenarioStep.Deny(var memberName):
                    Assert.Throws<InvalidOperationException>(() => AccessMember(shell, memberName));
                    break;
                case ShellLifecycleTraceScenarioStep.ReadyCompleted:
                    shell.SetInitializedClient(await CreateReadyClient());
                    break;
                case ShellLifecycleTraceScenarioStep.ReadyFaulted:
                    shell.SetInitializationFailed(new InvalidOperationException("forced"));
                    break;
                case ShellLifecycleTraceScenarioStep.ReadyCanceled:
                    shell.SetInitializationCanceled(default);
                    break;
                case ShellLifecycleTraceScenarioStep.Allow(var memberName):
                    _ = AccessMember(shell, memberName);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, "Unknown shell lifecycle trace scenario step.");
            }
        }
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

    private static object? AccessMember(SupabaseClientShell shell, string memberName)
    {
        return memberName switch
        {
            nameof(ISupabaseClient.Auth) => shell.Auth,
            nameof(ISupabaseClient.Postgrest) => shell.Postgrest,
            nameof(ISupabaseClient.Realtime) => shell.Realtime,
            nameof(ISupabaseClient.Storage) => shell.Storage,
            nameof(ISupabaseClient.Functions) => shell.Functions,
            nameof(ISupabaseClient.Url) => shell.Url,
            nameof(ISupabaseClient.AnonKey) => shell.AnonKey,
            nameof(ISupabaseClient.Urls) => shell.Urls,
            "Table" => shell.Table<TraceModel>(),
            _ => throw new ArgumentOutOfRangeException(nameof(memberName), memberName, "Unknown shell member name.")
        };
    }

    private sealed class TraceModel : global::Supabase.Postgrest.Models.BaseModel
    {
    }
}
