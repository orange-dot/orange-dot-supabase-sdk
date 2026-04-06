using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase.Tests.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void Add_supabase_throws_for_null_arguments()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddSupabase(null!, static _ => { }));
        Assert.Throws<ArgumentNullException>(() => services.AddSupabase((Action<SupabaseOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => services.AddSupabase((Action<IServiceProvider, SupabaseOptions>)null!));
    }

    [Fact]
    public void Add_supabase_registers_expected_services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSupabase(options =>
        {
            options.Url = "https://abc.supabase.co/";
            options.AnonKey = "anon-key";
        });

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<SupabaseOptions>>().Value;
        var observer = provider.GetRequiredService<IAuthStateObserver>();
        var firstClient = provider.GetRequiredService<ISupabaseClient>();
        var secondClient = provider.GetRequiredService<ISupabaseClient>();
        var hostedServices = provider.GetServices<IHostedService>().ToArray();

        Assert.Equal("https://abc.supabase.co/", options.Url);
        Assert.Equal("anon-key", options.AnonKey);
        Assert.IsType<AuthStateObserver>(observer);
        Assert.Same(firstClient, secondClient);
        Assert.Single(hostedServices);
        Assert.IsType<SupabaseStartupService>(hostedServices[0]);
    }

    [Fact]
    public async Task Add_supabase_service_provider_overload_can_use_other_registrations()
    {
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(new OptionSeed("https://seeded.supabase.co/", "seeded-anon-key"));
                services.AddSupabase((serviceProvider, options) =>
                {
                    var seed = serviceProvider.GetRequiredService<OptionSeed>();
                    options.Url = seed.Url;
                    options.AnonKey = seed.AnonKey;
                });
            })
            .Build();

        var client = host.Services.GetRequiredService<ISupabaseClient>();

        await host.StartAsync();
        await client.Ready;

        Assert.Equal("https://seeded.supabase.co", client.Url);
        Assert.Equal("seeded-anon-key", client.AnonKey);
        Assert.Equal("https://seeded.supabase.co", client.Urls.NormalizedBaseUrl);
    }

    private sealed record OptionSeed(string Url, string AnonKey);
}
