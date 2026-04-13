using OrangeDot.Supabase.Unity;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace OrangeDot.Supabase.Unity.Tests;

public sealed class SupabaseUnityClientTests
{
    [Fact]
    public async Task InitializeAsync_WithoutPersistedSession_UsesAnonHeadersOnly()
    {
        var client = new SupabaseUnityClient(new SupabaseUnityOptions
        {
            ProjectUrl = "https://project-ref.supabase.co",
            AnonKey = "anon-key",
            RefreshSessionOnInitialize = false
        });

        await client.InitializeAsync();

        var headers = client.Postgrest.GetHeaders!.Invoke();
        var functionHeaders = client.Functions.GetHeaders!.Invoke();
        var storageHeaders = client.Storage.GetHeaders!.Invoke();

        Assert.Equal("anon-key", headers["apikey"]);
        Assert.Equal("anon-key", functionHeaders["apikey"]);
        Assert.Equal("anon-key", storageHeaders["apikey"]);
        Assert.False(headers.ContainsKey("Authorization"));
        Assert.False(functionHeaders.ContainsKey("Authorization"));
        Assert.False(storageHeaders.ContainsKey("Authorization"));
    }

    [Fact]
    public async Task InitializeAsync_WithPersistedSession_ProjectsBearerToken()
    {
        var persistence = new StubSessionPersistence(new Session
        {
            AccessToken = "persisted-access-token",
            RefreshToken = "persisted-refresh-token",
            ExpiresIn = 3600,
            User = new User
            {
                Id = "user-123",
                Email = "persisted@example.com"
            }
        });

        var client = new SupabaseUnityClient(new SupabaseUnityOptions
        {
            ProjectUrl = "https://project-ref.supabase.co",
            AnonKey = "anon-key",
            RefreshSessionOnInitialize = false
        }, persistence);

        var session = await client.InitializeAsync();
        var headers = client.Postgrest.GetHeaders!.Invoke();
        var functionHeaders = client.Functions.GetHeaders!.Invoke();
        var storageHeaders = client.Storage.GetHeaders!.Invoke();

        Assert.NotNull(session);
        Assert.Equal("persisted-access-token", session!.AccessToken);
        Assert.Equal("Bearer persisted-access-token", headers["Authorization"]);
        Assert.Equal("Bearer persisted-access-token", functionHeaders["Authorization"]);
        Assert.Equal("Bearer persisted-access-token", storageHeaders["Authorization"]);
        Assert.Equal("persisted@example.com", client.CurrentUser!.Email);
    }

    private sealed class StubSessionPersistence : IGotrueSessionPersistence<Session>
    {
        private Session? _session;

        public StubSessionPersistence(Session? session)
        {
            _session = session;
        }

        public void SaveSession(Session session)
        {
            _session = session;
        }

        public void DestroySession()
        {
            _session = null;
        }

        public Session? LoadSession()
        {
            return _session;
        }
    }
}
