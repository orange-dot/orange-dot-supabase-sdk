using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrangeDot.Supabase.Errors;
using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase.Tests.DependencyInjection;

public sealed class SupabaseHostedStartupTests
{
    [Fact]
    public async Task Hosted_startup_transitions_shell_to_ready_state()
    {
        using var host = CreateHost(options =>
        {
            options.Url = "https://abc.supabase.co/";
            options.AnonKey = "anon-key";
        });

        var firstClient = host.Services.GetRequiredService<ISupabaseClient>();
        var secondClient = host.Services.GetRequiredService<ISupabaseClient>();

        Assert.Same(firstClient, secondClient);
        Assert.False(firstClient.Ready.IsCompleted);
        Assert.Throws<InvalidOperationException>(() => _ = firstClient.Auth);
        Assert.Throws<InvalidOperationException>(() => _ = firstClient.Postgrest);
        Assert.Throws<InvalidOperationException>(() => _ = firstClient.Realtime);
        Assert.Throws<InvalidOperationException>(() => _ = firstClient.Storage);
        Assert.Throws<InvalidOperationException>(() => _ = firstClient.Functions);
        Assert.Throws<InvalidOperationException>(() => _ = firstClient.Url);
        Assert.Throws<InvalidOperationException>(() => _ = firstClient.AnonKey);
        Assert.Throws<InvalidOperationException>(() => _ = firstClient.Urls);

        await host.StartAsync();
        await firstClient.Ready;

        Assert.True(firstClient.Ready.IsCompletedSuccessfully);
        Assert.Equal("https://abc.supabase.co", firstClient.Url);
        Assert.Equal("anon-key", firstClient.AnonKey);
        Assert.Equal("https://abc.supabase.co", firstClient.Urls.NormalizedBaseUrl);
        Assert.NotNull(firstClient.Auth);
        Assert.NotNull(firstClient.Postgrest);
        Assert.NotNull(firstClient.Realtime);
        Assert.NotNull(firstClient.Storage);
        Assert.NotNull(firstClient.Functions);
    }

    [Fact]
    public async Task Hosted_startup_failure_faults_ready_and_keeps_property_gate_closed()
    {
        using var host = CreateHost(options =>
        {
            options.Url = "not a url";
            options.AnonKey = "anon-key";
        });

        var client = host.Services.GetRequiredService<ISupabaseClient>();

        var startException = await Assert.ThrowsAsync<SupabaseConfigurationException>(() => host.StartAsync());
        var readyException = await Assert.ThrowsAsync<SupabaseConfigurationException>(async () => await client.Ready);

        Assert.Equal(SupabaseErrorCode.ConfigurationInvalid, startException.ErrorCode);
        Assert.Equal(SupabaseErrorCode.ConfigurationInvalid, readyException.ErrorCode);
        Assert.Throws<InvalidOperationException>(() => _ = client.Auth);
        Assert.Throws<InvalidOperationException>(() => _ = client.Postgrest);
        Assert.Throws<InvalidOperationException>(() => _ = client.Realtime);
        Assert.Throws<InvalidOperationException>(() => _ = client.Storage);
        Assert.Throws<InvalidOperationException>(() => _ = client.Functions);
        Assert.Throws<InvalidOperationException>(() => _ = client.Url);
        Assert.Throws<InvalidOperationException>(() => _ = client.AnonKey);
        Assert.Throws<InvalidOperationException>(() => _ = client.Urls);
    }

    [Fact]
    public async Task Hosted_startup_cancellation_cancels_ready_and_keeps_property_gate_closed()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance);
        var startupService = new SupabaseStartupService(
            Options.Create(new SupabaseOptions
            {
                Url = "https://abc.supabase.co",
                AnonKey = "anon-key"
            }),
            shell,
            NullLogger<SupabaseStartupService>.Instance,
            NullLoggerFactory.Instance,
            new OrangeDot.Supabase.Auth.AuthStateObserver(),
            services);

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startupService.StartAsync(cancellationTokenSource.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await shell.Ready);

        Assert.True(shell.Ready.IsCanceled);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Postgrest);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Realtime);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Storage);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Functions);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Url);
        Assert.Throws<InvalidOperationException>(() => _ = shell.AnonKey);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Urls);
    }

    private static IHost CreateHost(Action<SupabaseOptions> configure)
    {
        return new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSupabase(configure);
            })
            .Build();
    }
}
