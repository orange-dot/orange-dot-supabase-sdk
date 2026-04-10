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
            options.PublishableKey = "publishable-key";
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
        Assert.Equal("publishable-key", firstClient.AnonKey);
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
            options.PublishableKey = "publishable-key";
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
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance);
        var startupService = new SupabaseStartupService(
            Options.Create(new SupabaseOptions
            {
                Url = "https://abc.supabase.co",
                PublishableKey = "publishable-key"
            }),
            shell,
            NullLogger<SupabaseStartupService>.Instance,
            NullLoggerFactory.Instance,
            new OrangeDot.Supabase.Auth.AuthStateObserver());

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

    [Fact]
    public async Task Hosted_stop_disposes_initialized_client()
    {
        using var host = CreateHost(options =>
        {
            options.Url = "https://abc.supabase.co/";
            options.PublishableKey = "publishable-key";
        });

        var client = host.Services.GetRequiredService<ISupabaseClient>();

        await host.StartAsync();
        await client.Ready;
        await host.StopAsync();

        Assert.Throws<ObjectDisposedException>(() => _ = client.Auth);
    }

    [Fact]
    public async Task Shell_disposed_before_init_immediately_disposes_passed_client()
    {
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance);
        var client = await CreateRuntimeClient();

        shell.Dispose();
        shell.SetInitializedClient(client);

        Assert.Throws<ObjectDisposedException>(() => _ = client.Auth);
    }

    [Fact]
    public async Task Shell_disposes_client_when_ready_source_already_faulted()
    {
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance);
        shell.SetInitializationFailed(new InvalidOperationException("forced"));

        var client = await CreateRuntimeClient();
        shell.SetInitializedClient(client);

        Assert.Throws<ObjectDisposedException>(() => _ = client.Auth);
    }

    [Fact]
    public async Task StopAsync_is_idempotent()
    {
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance);
        var service = new SupabaseStartupService(
            Options.Create(new SupabaseOptions
            {
                Url = "https://abc.supabase.co",
                PublishableKey = "publishable-key"
            }),
            shell,
            NullLogger<SupabaseStartupService>.Instance,
            NullLoggerFactory.Instance,
            new OrangeDot.Supabase.Auth.AuthStateObserver());

        await service.StartAsync(CancellationToken.None);
        await shell.Ready;

        await service.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Throws<ObjectDisposedException>(() => _ = shell.Auth);
    }

    [Fact]
    public async Task StopAsync_before_StartAsync_cancels_ready_and_prevents_ready_publication()
    {
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance);
        var service = new SupabaseStartupService(
            Options.Create(new SupabaseOptions
            {
                Url = "https://abc.supabase.co",
                PublishableKey = "publishable-key"
            }),
            shell,
            NullLogger<SupabaseStartupService>.Instance,
            NullLoggerFactory.Instance,
                new OrangeDot.Supabase.Auth.AuthStateObserver());

        await service.StopAsync(CancellationToken.None);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await shell.Ready);
        await service.StartAsync(CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await shell.Ready);
        Assert.True(shell.Ready.IsCanceled);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);
    }

    [Fact]
    public async Task StartAsync_after_stop_does_not_leave_client_accessible()
    {
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance);
        var service = new SupabaseStartupService(
            Options.Create(new SupabaseOptions
            {
                Url = "https://abc.supabase.co",
                PublishableKey = "publishable-key"
            }),
            shell,
            NullLogger<SupabaseStartupService>.Instance,
            NullLoggerFactory.Instance,
            new OrangeDot.Supabase.Auth.AuthStateObserver());

        await service.StartAsync(CancellationToken.None);
        await shell.Ready;
        await service.StopAsync(CancellationToken.None);

        Assert.Throws<ObjectDisposedException>(() => _ = shell.Auth);
        Assert.True(shell.Ready.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Concurrent_StopAsync_during_pre_publish_window_cancels_ready_and_prevents_publication()
    {
        var shell = new SupabaseClientShell(NullLogger<SupabaseClientShell>.Instance);
        var service = new SupabaseStartupService(
            Options.Create(new SupabaseOptions
            {
                Url = "https://abc.supabase.co",
                PublishableKey = "publishable-key"
            }),
            shell,
            NullLogger<SupabaseStartupService>.Instance,
            NullLoggerFactory.Instance,
            new OrangeDot.Supabase.Auth.AuthStateObserver());

        var reachedPrePublishWindow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.BeforePublishTestHookAsync = () =>
        {
            reachedPrePublishWindow.TrySetResult();
            return resumeStart.Task;
        };

        var startTask = Task.Run(() => service.StartAsync(CancellationToken.None));

        await reachedPrePublishWindow.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(shell.Ready.IsCompleted);

        await service.StopAsync(CancellationToken.None);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await shell.Ready);
        Assert.True(shell.Ready.IsCanceled);

        resumeStart.TrySetResult();
        await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(shell.Ready.IsCanceled);
        Assert.Throws<InvalidOperationException>(() => _ = shell.Auth);
    }

    private static async Task<SupabaseClient> CreateRuntimeClient()
    {
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            PublishableKey = "publishable-key"
        });

        var hydrated = await configured.LoadPersistedSessionAsync();
        return await hydrated.InitializeAsync();
    }

    private static IHost CreateHost(Action<SupabaseOptions> configure)
    {
        return new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSupabaseHosted(configure);
            })
            .Build();
    }
}
