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
    public void Add_supabase_hosted_throws_for_null_arguments()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddSupabaseHosted(null!, static _ => { }));
        Assert.Throws<ArgumentNullException>(() => services.AddSupabaseHosted((Action<SupabaseOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => services.AddSupabaseHosted((Action<IServiceProvider, SupabaseOptions>)null!));
    }

    [Fact]
    public void Add_supabase_server_throws_for_null_arguments()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddSupabaseServer(null!, static _ => { }));
        Assert.Throws<ArgumentNullException>(() => services.AddSupabaseServer((Action<SupabaseServerOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => services.AddSupabaseServer((Action<IServiceProvider, SupabaseServerOptions>)null!));
    }

    [Fact]
    public void Add_supabase_hosted_registers_expected_services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSupabaseHosted(options =>
        {
            options.Url = "https://abc.supabase.co/";
            options.AnonKey = "anon-key";
        });

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<SupabaseOptions>>().Value;
        var observer = provider.GetRequiredService<IAuthStateObserver>();
        var concreteObserver = provider.GetRequiredService<AuthStateObserver>();
        var firstClient = provider.GetRequiredService<ISupabaseClient>();
        var secondClient = provider.GetRequiredService<ISupabaseClient>();
        var hostedServices = provider.GetServices<IHostedService>().ToArray();

        Assert.Equal("https://abc.supabase.co/", options.Url);
        Assert.Equal("anon-key", options.AnonKey);
        Assert.IsType<AuthStateObserver>(observer);
        Assert.Same(concreteObserver, observer);
        Assert.Same(firstClient, secondClient);
        Assert.Single(hostedServices);
        Assert.IsType<SupabaseStartupService>(hostedServices[0]);
    }

    [Fact]
    public void Add_supabase_server_registers_expected_services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSupabaseServer(options =>
        {
            options.Url = "https://abc.supabase.co/";
            options.AnonKey = "anon-key";
            options.ServiceRoleKey = "service-role-key";
        });

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<SupabaseServerOptions>>().Value;
        var firstFactory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();
        var secondFactory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();
        var hostedServices = provider.GetServices<IHostedService>().ToArray();

        Assert.Equal("https://abc.supabase.co/", options.Url);
        Assert.Equal("anon-key", options.AnonKey);
        Assert.Equal("service-role-key", options.ServiceRoleKey);
        Assert.Same(firstFactory, secondFactory);
        Assert.Null(provider.GetService<ISupabaseClient>());
        Assert.Empty(hostedServices);
    }

    [Fact]
    public async Task Add_supabase_hosted_service_provider_overload_can_use_other_registrations()
    {
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(new OptionSeed("https://seeded.supabase.co/", "seeded-anon-key"));
                services.AddSupabaseHosted((serviceProvider, options) =>
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
        Assert.NotNull(client.Auth);
        Assert.NotNull(client.Postgrest);
        Assert.NotNull(client.Realtime);
        Assert.NotNull(client.Storage);
        Assert.NotNull(client.Functions);
    }

    [Fact]
    public void Add_supabase_server_service_provider_overload_can_use_other_registrations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new ServerOptionSeed("https://seeded.supabase.co/", "seeded-anon-key", "seeded-service-role-key"));
        services.AddSupabaseServer((serviceProvider, options) =>
        {
            var seed = serviceProvider.GetRequiredService<ServerOptionSeed>();
            options.Url = seed.Url;
            options.AnonKey = seed.AnonKey;
            options.ServiceRoleKey = seed.ServiceRoleKey;
        });

        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();
        var serviceClient = factory.CreateService();

        Assert.Equal("https://seeded.supabase.co", serviceClient.Url);
        Assert.Equal("seeded-anon-key", serviceClient.AnonKey);
        Assert.Equal("https://seeded.supabase.co", serviceClient.Urls.NormalizedBaseUrl);
    }

    [Fact]
    public async Task Hosted_and_server_profiles_can_coexist_with_independent_options()
    {
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSupabaseHosted(options =>
                {
                    options.Url = "https://hosted.supabase.co/";
                    options.AnonKey = "hosted-anon-key";
                });
                services.AddSupabaseServer(options =>
                {
                    options.Url = "https://server.supabase.co/";
                    options.AnonKey = "server-anon-key";
                    options.ServiceRoleKey = "server-service-role-key";
                });
            })
            .Build();

        var hostedOptions = host.Services.GetRequiredService<IOptions<SupabaseOptions>>().Value;
        var serverOptions = host.Services.GetRequiredService<IOptions<SupabaseServerOptions>>().Value;
        var hostedClient = host.Services.GetRequiredService<ISupabaseClient>();
        var factory = host.Services.GetRequiredService<ISupabaseStatelessClientFactory>();

        await host.StartAsync();
        await hostedClient.Ready;

        var serverClient = factory.CreateService();

        Assert.Equal("https://hosted.supabase.co/", hostedOptions.Url);
        Assert.Equal("hosted-anon-key", hostedOptions.AnonKey);
        Assert.Equal("https://server.supabase.co/", serverOptions.Url);
        Assert.Equal("server-anon-key", serverOptions.AnonKey);
        Assert.Equal("server-service-role-key", serverOptions.ServiceRoleKey);

        Assert.Equal("https://hosted.supabase.co", hostedClient.Url);
        Assert.Equal("hosted-anon-key", hostedClient.AnonKey);
        Assert.Equal("https://server.supabase.co", serverClient.Url);
        Assert.Equal("server-anon-key", serverClient.AnonKey);
        Assert.Equal("Bearer server-service-role-key", Assert.IsType<global::Supabase.Storage.Client>(serverClient.Storage).Headers["Authorization"]);
    }

    private sealed record OptionSeed(string Url, string AnonKey);

    private sealed record ServerOptionSeed(string Url, string AnonKey, string ServiceRoleKey);
}
