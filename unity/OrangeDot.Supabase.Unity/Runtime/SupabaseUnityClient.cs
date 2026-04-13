using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;
using static Supabase.Gotrue.Constants;

namespace OrangeDot.Supabase.Unity
{
public sealed class SupabaseUnityClient : IDisposable
{
    private readonly SupabaseUnityOptions _options;
    private readonly IGotrueSessionPersistence<Session>? _sessionPersistence;
    private int _disposed;
    private bool _initialized;

    public SupabaseUnityClient(SupabaseUnityOptions options, IGotrueSessionPersistence<Session>? sessionPersistence = null)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ProjectUrl))
        {
            throw new ArgumentException("Project URL is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.AnonKey))
        {
            throw new ArgumentException("Anon key is required.", nameof(options));
        }

        _options = options;
        _sessionPersistence = sessionPersistence;
        Urls = SupabaseUnityUrls.FromProjectUrl(options.ProjectUrl);

        Auth = CreateAuthClient(options, sessionPersistence);
        Postgrest = CreatePostgrestClient(options);
    }

    public SupabaseUnityUrls Urls { get; }

    public IGotrueClient<User, Session> Auth { get; }

    public IPostgrestClient Postgrest { get; }

    public Session? CurrentSession => Auth.CurrentSession;

    public User? CurrentUser => Auth.CurrentUser;

    public bool IsAuthenticated => !string.IsNullOrEmpty(CurrentSession != null ? CurrentSession.AccessToken : null);

    public async Task<Session?> InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_initialized)
        {
            return CurrentSession;
        }

        if (_sessionPersistence is not null)
        {
            Auth.LoadSession();
        }

        cancellationToken.ThrowIfCancellationRequested();

        _initialized = true;

        if (_options.RefreshSessionOnInitialize && CurrentSession != null && !string.IsNullOrEmpty(CurrentSession.RefreshToken))
        {
            return await Auth.RetrieveSessionAsync().ConfigureAwait(false);
        }

        return CurrentSession;
    }

    public Task<Session?> SignInWithPasswordAsync(string email, string password)
    {
        ThrowIfDisposed();
        return Auth.SignIn(email, password);
    }

    public Task SignOutAsync(SignOutScope scope = SignOutScope.Global)
    {
        ThrowIfDisposed();
        return Auth.SignOut(scope);
    }

    public IPostgrestTable<TModel> Table<TModel>()
        where TModel : BaseModel, new()
    {
        ThrowIfDisposed();
        return Postgrest.Table<TModel>();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Auth.Shutdown();
    }

    private IGotrueClient<User, Session> CreateAuthClient(
        SupabaseUnityOptions options,
        IGotrueSessionPersistence<Session>? sessionPersistence)
    {
        var authClient = new global::Supabase.Gotrue.Client(new global::Supabase.Gotrue.ClientOptions
        {
            Url = Urls.AuthUrl,
            AutoRefreshToken = options.AutoRefreshToken,
            Headers = CreateDefaultHeaders(options.AnonKey)
        });

        if (sessionPersistence is not null)
        {
            authClient.SetPersistence(sessionPersistence);
        }

        return authClient;
    }

    private IPostgrestClient CreatePostgrestClient(SupabaseUnityOptions options)
    {
        var postgrestClient = new global::Supabase.Postgrest.Client(Urls.RestUrl, new global::Supabase.Postgrest.ClientOptions
        {
            Schema = options.Schema,
            Headers = CreateDefaultHeaders(options.AnonKey)
        });

        postgrestClient.GetHeaders = CreateDynamicHeaders;
        return postgrestClient;
    }

    private Dictionary<string, string> CreateDynamicHeaders()
    {
        var headers = CreateDefaultHeaders(_options.AnonKey);

        if (CurrentSession != null && !string.IsNullOrEmpty(CurrentSession.AccessToken))
        {
            headers["Authorization"] = $"Bearer {CurrentSession.AccessToken}";
        }

        return headers;
    }

    private static Dictionary<string, string> CreateDefaultHeaders(string anonKey)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["apikey"] = anonKey
        };
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(SupabaseUnityClient));
        }
    }
}
}
