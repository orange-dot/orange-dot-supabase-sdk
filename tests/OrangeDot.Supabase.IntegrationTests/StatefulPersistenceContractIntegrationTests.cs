using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OrangeDot.Supabase;
using OrangeDot.Supabase.Errors;

namespace OrangeDot.Supabase.IntegrationTests;

public sealed class StatefulPersistenceContractIntegrationTests
{
    [LocalSupabaseFact]
    public async Task Stateful_client_restores_persisted_session_across_reconfiguration()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();
        await IntegrationTestEnvironment.EnsureOptInAndReachableAsync(settings);

        var store = new InMemorySessionStore();
        SupabaseClient? firstClient = null;
        SupabaseClient? secondClient = null;

        try
        {
            firstClient = await CreateStatefulClientAsync(settings, store);

            var session = await firstClient.Auth.SignInAnonymously(new global::Supabase.Gotrue.SignInAnonymouslyOptions
            {
                Data = new Dictionary<string, object>
                {
                    ["source"] = "persistence-contract"
                }
            });

            Assert.NotNull(session);
            Assert.NotNull(await store.LoadAsync());

            firstClient.Dispose();
            firstClient = null;

            secondClient = await CreateStatefulClientAsync(settings, store);
            var auth = Assert.IsAssignableFrom<global::Supabase.Gotrue.Client>(secondClient.Auth);
            var postgrest = Assert.IsType<global::Supabase.Postgrest.Client>(secondClient.Postgrest);
            var storage = Assert.IsType<global::Supabase.Storage.Client>(secondClient.Storage);
            var functions = Assert.IsType<global::Supabase.Functions.Client>(secondClient.Functions);

            Assert.NotNull(secondClient.Auth.CurrentSession);
            Assert.Equal(session.AccessToken, secondClient.Auth.CurrentSession!.AccessToken);
            Assert.Equal($"Bearer {session.AccessToken}", auth.GetHeaders!()["Authorization"]);
            Assert.Equal($"Bearer {session.AccessToken}", postgrest.GetHeaders!()["Authorization"]);
            Assert.Equal($"Bearer {session.AccessToken}", storage.Headers["Authorization"]);
            Assert.Equal($"Bearer {session.AccessToken}", functions.GetHeaders!()["Authorization"]);

            await secondClient.Auth.SignOut();

            Assert.Null(await store.LoadAsync());
        }
        finally
        {
            await BestEffortSignOutAndDisposeAsync(secondClient);
            await BestEffortSignOutAndDisposeAsync(firstClient);
        }
    }

    [LocalSupabaseFact]
    public async Task Stateful_auth_operations_surface_session_store_failures()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();
        await IntegrationTestEnvironment.EnsureOptInAndReachableAsync(settings);

        using var client = await CreateStatefulClientAsync(settings, new ThrowingPersistSessionStore());

        var exception = await Assert.ThrowsAsync<SupabaseAuthException>(() => client.Auth.SignInAnonymously(
            new global::Supabase.Gotrue.SignInAnonymouslyOptions
            {
                Data = new Dictionary<string, object>
                {
                    ["source"] = "persistence-failure"
                }
            }));

        Assert.Equal(SupabaseErrorCode.AuthFailed, exception.ErrorCode);
        Assert.Equal("SignIn", exception.Operation);

        if (client.Auth.CurrentSession is not null)
        {
            await client.Auth.SignOut();
        }
    }

    [LocalSupabaseFact]
    public async Task Stateful_and_stateless_clients_share_the_same_default_anon_contract()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();
        await IntegrationTestEnvironment.EnsureOptInAndReachableAsync(settings);

        using var stateful = await CreateStatefulClientAsync(settings);
        var stateless = SupabaseStatelessClient.Create(CreateOptions(settings));
        var statefulAuth = Assert.IsAssignableFrom<global::Supabase.Gotrue.Client>(stateful.Auth);
        var statefulPostgrest = Assert.IsType<global::Supabase.Postgrest.Client>(stateful.Postgrest);
        var statefulStorage = Assert.IsType<global::Supabase.Storage.Client>(stateful.Storage);
        var statefulFunctions = Assert.IsType<global::Supabase.Functions.Client>(stateful.Functions);
        var statelessPostgrest = Assert.IsType<global::Supabase.Postgrest.Client>(stateless.Postgrest);
        var statelessStorage = Assert.IsType<global::Supabase.Storage.Client>(stateless.Storage);
        var statelessFunctions = Assert.IsType<global::Supabase.Functions.Client>(stateless.Functions);

        Assert.Equal(stateless.Url, stateful.Url);
        Assert.Equal(stateless.AnonKey, stateful.AnonKey);
        Assert.Equal(stateless.Urls.AuthUrl, stateful.Urls.AuthUrl);
        Assert.Equal(stateless.Urls.RestUrl, stateful.Urls.RestUrl);
        Assert.Equal(stateless.Urls.StorageUrl, stateful.Urls.StorageUrl);
        Assert.Equal(stateless.Urls.FunctionsUrl, stateful.Urls.FunctionsUrl);

        Assert.Equal(stateless.AuthOptions.Url, statefulAuth.Options.Url);
        Assert.Equal(stateless.Urls.RestUrl, statefulPostgrest.BaseUrl);
        Assert.Equal(stateless.Urls.RestUrl, statelessPostgrest.BaseUrl);
        Assert.Equal(ReadPublicOrNonPublicStringProperty(statelessStorage, "Url"), ReadPublicOrNonPublicStringProperty(statefulStorage, "Url"));
        Assert.Equal(ReadPrivateStringField(statelessFunctions, "_baseUrl"), ReadPrivateStringField(statefulFunctions, "_baseUrl"));

        Assert.Equal(settings.AnonKey, statefulAuth.Options.Headers["apikey"]);
        Assert.Equal(settings.AnonKey, stateless.AuthOptions.Headers["apikey"]);
        Assert.DoesNotContain("Authorization", statefulAuth.GetHeaders!().Keys);
        Assert.DoesNotContain("Authorization", statelessPostgrest.GetHeaders!().Keys);
        Assert.DoesNotContain("Authorization", statefulPostgrest.GetHeaders!().Keys);
        Assert.Equal($"Bearer {settings.AnonKey}", statefulStorage.Headers["Authorization"]);
        Assert.Equal($"Bearer {settings.AnonKey}", statelessStorage.Headers["Authorization"]);
        Assert.DoesNotContain("Authorization", statefulFunctions.GetHeaders!().Keys);
        Assert.DoesNotContain("Authorization", statelessFunctions.GetHeaders!().Keys);

        var ownerTag = IntegrationTestEnvironment.NewOwnerTag("contract");
        var details = $"details-{ownerTag}";

        try
        {
            var insertResponse = await stateful.Postgrest.Table<IntegrationTodo>().Insert(new IntegrationTodo
            {
                Details = details,
                OwnerTag = ownerTag
            });

            var inserted = Assert.Single(insertResponse.Models);

            var readResponse = await stateless.Postgrest.Table<IntegrationTodo>()
                .Filter("owner_tag", global::Supabase.Postgrest.Constants.Operator.Equals, ownerTag)
                .Get();

            var readModel = Assert.Single(readResponse.Models);
            Assert.Equal(inserted.Id, readModel.Id);

            await stateless.Postgrest.Table<IntegrationTodo>().Delete(readModel);

            var afterDelete = await stateful.Postgrest.Table<IntegrationTodo>()
                .Filter("owner_tag", global::Supabase.Postgrest.Constants.Operator.Equals, ownerTag)
                .Get();

            Assert.Empty(afterDelete.Models);
        }
        finally
        {
            await IntegrationTestEnvironment.CleanupByOwnerTagAsync(stateful.Postgrest, ownerTag);
        }
    }

    private static SupabaseOptions CreateOptions(IntegrationTestSettings settings, ISupabaseSessionStore? store = null)
    {
        return new SupabaseOptions
        {
            Url = settings.Url,
            AnonKey = settings.AnonKey,
            SessionStore = store
        };
    }

    private static async Task<SupabaseClient> CreateStatefulClientAsync(
        IntegrationTestSettings settings,
        ISupabaseSessionStore? store = null)
    {
        var configured = SupabaseClient.Configure(CreateOptions(settings, store));
        var hydrated = await configured.LoadPersistedSessionAsync();
        return await hydrated.InitializeAsync();
    }

    private static async Task BestEffortSignOutAndDisposeAsync(SupabaseClient? client)
    {
        if (client is null)
        {
            return;
        }

        try
        {
            if (client.Auth.CurrentSession is not null)
            {
                await client.Auth.SignOut();
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            client.Dispose();
        }
    }

    private static string ReadPrivateStringField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        return Assert.IsType<string>(field!.GetValue(instance));
    }

    private static string ReadPublicOrNonPublicStringProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(property);
        return Assert.IsType<string>(property!.GetValue(instance));
    }

    private sealed class InMemorySessionStore : ISupabaseSessionStore
    {
        private global::Supabase.Gotrue.Session? _session;

        public ValueTask PersistAsync(global::Supabase.Gotrue.Session session, CancellationToken cancellationToken = default)
        {
            _session = session;
            return ValueTask.CompletedTask;
        }

        public ValueTask<global::Supabase.Gotrue.Session?> LoadAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_session);
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            _session = null;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingPersistSessionStore : ISupabaseSessionStore
    {
        public ValueTask PersistAsync(global::Supabase.Gotrue.Session session, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("persist failed");
        }

        public ValueTask<global::Supabase.Gotrue.Session?> LoadAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<global::Supabase.Gotrue.Session?>(null);
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }
}
