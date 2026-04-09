using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Errors;
using OrangeDot.Supabase.Internal;
using Xunit;

namespace OrangeDot.Supabase.Tests.Lifecycle;

public sealed class LifecycleTransitionTests
{
    [Fact]
    public void Configure_throws_for_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => SupabaseClient.Configure(null!));
    }

    [Fact]
    public async Task Happy_path_preserves_snapshotted_values_and_derived_urls()
    {
        var options = new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key"
        };

        var configured = SupabaseClient.Configure(options);
        var hydrated = await configured.LoadPersistedSessionAsync();
        var client = await hydrated.InitializeAsync();

        Assert.Equal("https://abc.supabase.co", client.Url);
        Assert.Equal("anon-key", client.AnonKey);
        Assert.Equal("https://abc.supabase.co", client.Urls.NormalizedBaseUrl);
        Assert.Equal("https://abc.supabase.co/auth/v1", client.Urls.AuthUrl);
        Assert.NotNull(client.Auth);
        Assert.NotNull(client.Postgrest);
        Assert.NotNull(client.Realtime);
        Assert.NotNull(client.Storage);
        Assert.NotNull(client.Functions);
    }

    [Fact]
    public void Configure_throws_configuration_missing_for_missing_url()
    {
        var options = new SupabaseOptions
        {
            AnonKey = "anon-key"
        };

        var exception = Assert.Throws<SupabaseConfigurationException>(() => SupabaseClient.Configure(options));

        Assert.Equal(SupabaseErrorCode.ConfigurationMissing, exception.ErrorCode);
        Assert.Equal("Configure", exception.Operation);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Configure_throws_configuration_missing_for_missing_anon_key()
    {
        var options = new SupabaseOptions
        {
            Url = "https://abc.supabase.co"
        };

        var exception = Assert.Throws<SupabaseConfigurationException>(() => SupabaseClient.Configure(options));

        Assert.Equal(SupabaseErrorCode.ConfigurationMissing, exception.ErrorCode);
        Assert.Equal("Configure", exception.Operation);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Configure_throws_configuration_invalid_for_invalid_url()
    {
        var options = new SupabaseOptions
        {
            Url = "not a url",
            AnonKey = "anon-key"
        };

        var exception = Assert.Throws<SupabaseConfigurationException>(() => SupabaseClient.Configure(options));

        Assert.Equal(SupabaseErrorCode.ConfigurationInvalid, exception.ErrorCode);
        Assert.Equal("Configure", exception.Operation);
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void Configure_throws_configuration_invalid_for_unsupported_url_scheme()
    {
        var options = new SupabaseOptions
        {
            Url = "ftp://example.com",
            AnonKey = "anon-key"
        };

        var exception = Assert.Throws<SupabaseConfigurationException>(() => SupabaseClient.Configure(options));

        Assert.Equal(SupabaseErrorCode.ConfigurationInvalid, exception.ErrorCode);
        Assert.Equal("Configure", exception.Operation);
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public async Task Client_url_is_normalized_not_raw_input()
    {
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co/",
            AnonKey = "anon-key"
        });

        var client = await (await configured.LoadPersistedSessionAsync()).InitializeAsync();

        Assert.Equal("https://abc.supabase.co", client.Url);
        Assert.Equal(client.Urls.NormalizedBaseUrl, client.Url);
    }

    [Fact]
    public async Task Configure_snapshots_values_and_ignores_later_mutation_of_original_options()
    {
        var options = new SupabaseOptions
        {
            Url = "https://abc.supabase.co/",
            AnonKey = "initial-anon-key"
        };

        var configured = SupabaseClient.Configure(options);

        options.Url = "https://mutated.supabase.co";
        options.AnonKey = "mutated-anon-key";

        var hydrated = await configured.LoadPersistedSessionAsync();
        var client = await hydrated.InitializeAsync();

        Assert.Equal("https://abc.supabase.co", client.Url);
        Assert.Equal("initial-anon-key", client.AnonKey);
        Assert.Equal("https://abc.supabase.co", client.Urls.NormalizedBaseUrl);
    }

    [Fact]
    public async Task Initialize_async_honors_pre_canceled_token()
    {
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key"
        });

        var hydrated = await configured.LoadPersistedSessionAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hydrated.InitializeAsync(cts.Token));
    }

    [Fact]
    public async Task Initialize_async_cancellation_after_partial_wiring_cleans_up_partial_runtime_graph()
    {
        var observer = new AuthStateObserver();
        var runtimeContext = new SupabaseRuntimeContext(
            observer,
            NullLoggerFactory.Instance,
            MeterFactory: null,
            NoOpSupabaseSessionStore.Instance);
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key"
        }, runtimeContext);

        var hydrated = await configured.LoadPersistedSessionAsync();
        var reachedPreFinalizeWindow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SupabaseChildClients? capturedChildren = null;
        int authListenerCountDuringHook = 0;

        hydrated.BeforeFinalizeTestHookAsync = (children, _, _, _) =>
        {
            capturedChildren = children;
            authListenerCountDuringHook = ReadAuthListenerCount(children.Auth);
            reachedPreFinalizeWindow.TrySetResult();
            return resumeInitialization.Task;
        };

        using var cts = new CancellationTokenSource();
        var initializeTask = hydrated.InitializeAsync(cts.Token);

        await reachedPreFinalizeWindow.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(capturedChildren);

        cts.Cancel();
        resumeInitialization.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await initializeTask);

        Assert.NotNull(capturedChildren);
        Assert.Equal(authListenerCountDuringHook - 1, ReadAuthListenerCount(capturedChildren!.Auth));

        observer.Publish(new AuthState.Authenticated(
            1,
            "post-cancel-token",
            "refresh-token",
            DateTimeOffset.Parse("2026-04-08T10:00:00Z")));

        Assert.DoesNotContain("Authorization", capturedChildren.DynamicAuthHeaders.Build().Keys);
        Assert.Equal(string.Empty, ReadPrivateStringMember(capturedChildren.Realtime, "AccessToken"));
    }

    [Fact]
    public async Task Configure_creates_independent_lifecycle_pipelines()
    {
        var firstConfigured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://first.supabase.co",
            AnonKey = "first-key"
        });

        var secondConfigured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://second.supabase.co",
            AnonKey = "second-key"
        });

        var firstClient = await (await firstConfigured.LoadPersistedSessionAsync()).InitializeAsync();
        var secondClient = await (await secondConfigured.LoadPersistedSessionAsync()).InitializeAsync();

        Assert.Equal("https://first.supabase.co", firstClient.Url);
        Assert.Equal("first-key", firstClient.AnonKey);
        Assert.Equal("https://second.supabase.co", secondClient.Url);
        Assert.Equal("second-key", secondClient.AnonKey);
        Assert.NotSame(firstClient, secondClient);
    }

    [Fact]
    public async Task Disposed_configured_client_rejects_further_transitions()
    {
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key"
        });

        configured.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await configured.LoadPersistedSessionAsync());
    }

    [Fact]
    public async Task Disposed_hydrated_client_rejects_initialization()
    {
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key"
        });

        var hydrated = await configured.LoadPersistedSessionAsync();
        hydrated.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await hydrated.InitializeAsync());
    }

    [Fact]
    public async Task Disposed_client_rejects_public_operations()
    {
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key"
        });

        var client = await (await configured.LoadPersistedSessionAsync()).InitializeAsync();
        client.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = client.Auth);
        Assert.Throws<ObjectDisposedException>(() => _ = client.Postgrest);
        Assert.Throws<ObjectDisposedException>(() => _ = client.Realtime);
        Assert.Throws<ObjectDisposedException>(() => _ = client.Storage);
        Assert.Throws<ObjectDisposedException>(() => _ = client.Functions);
        Assert.Throws<ObjectDisposedException>(() => _ = client.Url);
        Assert.Throws<ObjectDisposedException>(() => _ = client.AnonKey);
        Assert.Throws<ObjectDisposedException>(() => _ = client.Urls);
        Assert.Throws<ObjectDisposedException>(() => client.Table<TestModel>());
    }

    private sealed class TestModel : global::Supabase.Postgrest.Models.BaseModel
    {
    }

    private static int ReadAuthListenerCount(global::Supabase.Gotrue.Client auth)
    {
        var field = typeof(global::Supabase.Gotrue.Client).GetField(
            "_authEventHandlers",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        var handlers = Assert.IsAssignableFrom<ICollection>(field!.GetValue(auth));
        return handlers.Count;
    }

    private static string? ReadPrivateStringMember(object instance, string memberName)
    {
        var property = instance.GetType().GetProperty(
            memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property is not null)
        {
            return property.GetValue(instance) as string;
        }

        var field = instance.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        return field!.GetValue(instance) as string;
    }
}
