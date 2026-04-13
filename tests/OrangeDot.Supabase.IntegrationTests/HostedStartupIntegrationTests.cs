using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase.IntegrationTests;

public sealed class HostedStartupIntegrationTests
{
    [LocalSupabaseFact]
    public async Task AddSupabaseHosted_hosted_startup_initializes_ready_client_against_local_stack()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();

        await IntegrationTestEnvironment.EnsureOptInAndReachableAsync(settings);

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSupabaseHosted(options =>
                {
                    options.Url = settings.Url;
                    options.PublishableKey = settings.AnonKey;
                });
            })
            .Build();

        await host.StartAsync();

        try
        {
            var client = host.Services.GetRequiredService<ISupabaseClient>();

            await client.Ready;

            var response = await client.Table<IntegrationTodo>()
                .Limit(1)
                .Get();

            Assert.NotNull(response);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [LocalSupabaseFact]
    public async Task Live_hosted_startup_trace_translates_to_model_actions_against_local_stack()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();

        await IntegrationTestEnvironment.EnsureOptInAndReachableAsync(settings);

        var traceSink = new IntegrationRecordingRuntimeTraceSink();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IRuntimeTraceSink>(traceSink);
                services.AddSupabaseHosted(options =>
                {
                    options.Url = settings.Url;
                    options.PublishableKey = settings.AnonKey;
                });
            })
            .Build();

        await host.StartAsync();

        try
        {
            var client = host.Services.GetRequiredService<ISupabaseClient>();

            await client.Ready;

            var response = await client.Table<IntegrationTodo>()
                .Limit(1)
                .Get();

            Assert.NotNull(response);

            var lifecycleTrace = FilterLifecycleTrace(traceSink.Snapshot());
            var actions = new LifecycleTraceToModelActionTranslator().Translate(lifecycleTrace);

            Assert.Equal(
                [
                    Action(LifecycleModelActionKind.StartRequested),
                    Action(LifecycleModelActionKind.PrePublishWindowEntered),
                    Action(LifecycleModelActionKind.ReadyCompleted),
                    Action(LifecycleModelActionKind.PublicAccessAllowed, "Table")
                ],
                actions);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static IReadOnlyList<RuntimeTraceEvent> FilterLifecycleTrace(IReadOnlyList<RuntimeTraceEvent> trace)
    {
        var filtered = new List<RuntimeTraceEvent>();

        foreach (var traceEvent in trace)
        {
            if (traceEvent is StartupTraceEvent or LifecycleTraceEvent)
            {
                filtered.Add(traceEvent);
            }
        }

        return filtered;
    }

    private static LifecycleModelAction Action(LifecycleModelActionKind kind, string? memberName = null)
    {
        return new LifecycleModelAction(kind, memberName);
    }
}
