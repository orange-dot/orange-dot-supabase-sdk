using System;
using System.Threading;
using System.Threading.Tasks;
using OrangeDot.Supabase.Errors;

namespace OrangeDot.Supabase.Tests.Auth;

public sealed class SupabaseSessionPersistenceTests
{
    [Fact]
    public async Task Load_persisted_session_restores_current_session_and_authenticated_bindings()
    {
        var store = new InMemorySessionStore(CreateSession("restored-access", "restored-refresh", 1800));
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key",
            SessionStore = store
        });

        using var client = await (await configured.LoadPersistedSessionAsync()).InitializeAsync();
        var auth = Assert.IsAssignableFrom<global::Supabase.Gotrue.Client>(client.Auth);
        var postgrest = Assert.IsType<global::Supabase.Postgrest.Client>(client.Postgrest);
        var storage = Assert.IsType<global::Supabase.Storage.Client>(client.Storage);
        var functions = Assert.IsType<global::Supabase.Functions.Client>(client.Functions);

        Assert.NotNull(client.Auth.CurrentSession);
        Assert.Equal("restored-access", client.Auth.CurrentSession!.AccessToken);
        Assert.Equal("restored-refresh", client.Auth.CurrentSession.RefreshToken);
        Assert.Equal("Bearer restored-access", auth.GetHeaders!()["Authorization"]);
        Assert.Equal("Bearer restored-access", postgrest.GetHeaders!()["Authorization"]);
        Assert.Equal("Bearer restored-access", storage.Headers["Authorization"]);
        Assert.Equal("Bearer restored-access", functions.GetHeaders!()["Authorization"]);
    }

    [Fact]
    public async Task Load_persisted_session_wraps_store_load_failures()
    {
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key",
            SessionStore = new ThrowingLoadSessionStore(new InvalidOperationException("boom"))
        });

        var exception = await Assert.ThrowsAsync<SupabaseAuthException>(() => configured.LoadPersistedSessionAsync());

        Assert.Equal(SupabaseErrorCode.AuthSessionLoadFailed, exception.ErrorCode);
        Assert.Equal(nameof(ConfiguredClient.LoadPersistedSessionAsync), exception.Operation);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task Load_persisted_session_rejects_invalid_stored_session()
    {
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key",
            SessionStore = new InMemorySessionStore(new global::Supabase.Gotrue.Session
            {
                AccessToken = "access-only"
            })
        });

        var exception = await Assert.ThrowsAsync<SupabaseAuthException>(() => configured.LoadPersistedSessionAsync());

        Assert.Equal(SupabaseErrorCode.AuthSessionLoadFailed, exception.ErrorCode);
        Assert.Equal(nameof(ConfiguredClient.LoadPersistedSessionAsync), exception.Operation);
        Assert.Equal("Persisted auth session is invalid.", exception.Message);
    }

    [Fact]
    public async Task Default_session_store_keeps_stateful_client_anonymous_on_load()
    {
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key"
        });

        using var client = await (await configured.LoadPersistedSessionAsync()).InitializeAsync();
        var auth = Assert.IsAssignableFrom<global::Supabase.Gotrue.Client>(client.Auth);
        var postgrest = Assert.IsType<global::Supabase.Postgrest.Client>(client.Postgrest);
        var functions = Assert.IsType<global::Supabase.Functions.Client>(client.Functions);

        Assert.Null(client.Auth.CurrentSession);
        Assert.DoesNotContain("Authorization", auth.GetHeaders!().Keys);
        Assert.DoesNotContain("Authorization", postgrest.GetHeaders!().Keys);
        Assert.DoesNotContain("Authorization", functions.GetHeaders!().Keys);
    }

    [Fact]
    public async Task Disposing_client_clears_auth_from_captured_http_children()
    {
        var store = new InMemorySessionStore(CreateSession("restored-access", "restored-refresh", 1800));
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key",
            SessionStore = store
        });

        using var client = await (await configured.LoadPersistedSessionAsync()).InitializeAsync();
        var auth = Assert.IsAssignableFrom<global::Supabase.Gotrue.Client>(client.Auth);
        var postgrest = Assert.IsType<global::Supabase.Postgrest.Client>(client.Postgrest);
        var storage = Assert.IsType<global::Supabase.Storage.Client>(client.Storage);
        var functions = Assert.IsType<global::Supabase.Functions.Client>(client.Functions);

        client.Dispose();

        Assert.DoesNotContain("Authorization", auth.GetHeaders!().Keys);
        Assert.DoesNotContain("Authorization", postgrest.GetHeaders!().Keys);
        Assert.Equal("Bearer anon-key", storage.Headers["Authorization"]);
        Assert.Equal("anon-key", storage.Headers["apikey"]);
        Assert.DoesNotContain("Authorization", functions.GetHeaders!().Keys);
    }

    private static global::Supabase.Gotrue.Session CreateSession(string accessToken, string refreshToken, long expiresIn)
    {
        return new global::Supabase.Gotrue.Session
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            CreatedAt = new DateTime(2026, 4, 8, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private sealed class InMemorySessionStore : ISupabaseSessionStore
    {
        private global::Supabase.Gotrue.Session? _session;

        public InMemorySessionStore(global::Supabase.Gotrue.Session? session = null)
        {
            _session = session;
        }

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

    private sealed class ThrowingLoadSessionStore : ISupabaseSessionStore
    {
        private readonly Exception _exception;

        public ThrowingLoadSessionStore(Exception exception)
        {
            _exception = exception;
        }

        public ValueTask PersistAsync(global::Supabase.Gotrue.Session session, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<global::Supabase.Gotrue.Session?> LoadAsync(CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }
}
