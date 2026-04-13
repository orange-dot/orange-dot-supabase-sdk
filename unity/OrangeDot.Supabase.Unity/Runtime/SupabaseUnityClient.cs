using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Functions.Interfaces;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;
using Supabase.Storage.Interfaces;
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
        Functions = CreateFunctionsClient();
        Storage = CreateStorageClient(options);
    }

    public SupabaseUnityUrls Urls { get; }

    public IGotrueClient<User, Session> Auth { get; }

    public IPostgrestClient Postgrest { get; }

    public IFunctionsClient Functions { get; }

    public IStorageClient<global::Supabase.Storage.Bucket, global::Supabase.Storage.FileObject> Storage { get; }

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

    public Task<string> InvokeFunctionAsync(string functionName, Dictionary<string, object>? body = null)
    {
        ThrowIfDisposed();

        return Functions.Invoke(
            functionName,
            options: body is null
                ? null
                : new global::Supabase.Functions.Client.InvokeFunctionOptions
                {
                    Body = body
                });
    }

    public Task<string> UploadTextAsync(
        string bucket,
        string path,
        string text,
        string contentType = "text/plain;charset=UTF-8")
    {
        ThrowIfDisposed();

        return Storage
            .From(bucket)
            .Upload(
                Encoding.UTF8.GetBytes(text ?? string.Empty),
                path,
                new global::Supabase.Storage.FileOptions
                {
                    ContentType = contentType,
                    Upsert = true
                },
                inferContentType: false);
    }

    public async Task<IReadOnlyList<global::Supabase.Storage.FileObject>> ListFilesAsync(string bucket, string prefix)
    {
        ThrowIfDisposed();

        var files = await Storage
            .From(bucket)
            .List(
                prefix,
                new global::Supabase.Storage.SearchOptions
                {
                    Limit = 20
                })
            .ConfigureAwait(false);

        if (files is not null)
        {
            return files;
        }

        return Array.Empty<global::Supabase.Storage.FileObject>();
    }

    public Task<string> CreateSignedUrlAsync(string bucket, string path, int expiresInSeconds)
    {
        ThrowIfDisposed();
        return Storage.From(bucket).CreateSignedUrl(path, expiresInSeconds);
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

    private IFunctionsClient CreateFunctionsClient()
    {
        var functionsClient = new global::Supabase.Functions.Client(Urls.FunctionsUrl);
        functionsClient.GetHeaders = CreateDynamicHeaders;
        return functionsClient;
    }

    private IStorageClient<global::Supabase.Storage.Bucket, global::Supabase.Storage.FileObject> CreateStorageClient(
        SupabaseUnityOptions options)
    {
        var storageClient = new global::Supabase.Storage.Client(
            Urls.StorageUrl,
            CreateDefaultHeaders(options.AnonKey));
        storageClient.GetHeaders = CreateDynamicHeaders;
        return storageClient;
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
