using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
}
