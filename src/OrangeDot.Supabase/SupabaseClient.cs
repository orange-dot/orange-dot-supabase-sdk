using System;
using System.Threading;
using System.Threading.Tasks;
using OrangeDot.Supabase.Internal;
using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase;

public sealed class SupabaseClient : ISupabaseClient
{
    private readonly SupabaseChildClients _children;
    private readonly GotrueAuthStateBridge _authStateBridge;
    private readonly HeaderAuthBinding _headerAuthBinding;
    private readonly RealtimeTokenBinding _realtimeTokenBinding;
    private readonly SupabaseTableRealtimeClient _tableRealtime;
    private readonly string _url;
    private readonly string _anonKey;
    private readonly SupabaseUrls _urls;
    private int _disposed;

    internal SupabaseClient(
        LifecycleSnapshot snapshot,
        SupabaseChildClients children,
        GotrueAuthStateBridge authStateBridge,
        HeaderAuthBinding headerAuthBinding,
        RealtimeTokenBinding realtimeTokenBinding)
    {
        _children = children;
        _authStateBridge = authStateBridge;
        _headerAuthBinding = headerAuthBinding;
        _realtimeTokenBinding = realtimeTokenBinding;
        _tableRealtime = new SupabaseTableRealtimeClient(children.Realtime);

        _url = snapshot.Url;
        _anonKey = snapshot.AnonKey;
        _urls = snapshot.Urls;
    }

    public Task Ready { get; } = Task.CompletedTask;

    public global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> Auth
    {
        get
        {
            ThrowIfDisposed();
            return _children.Auth;
        }
    }

    public global::Supabase.Postgrest.Interfaces.IPostgrestClient Postgrest
    {
        get
        {
            ThrowIfDisposed();
            return _children.Postgrest;
        }
    }

    public global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> Realtime
    {
        get
        {
            ThrowIfDisposed();
            return _children.Realtime;
        }
    }

    public global::Supabase.Storage.Interfaces.IStorageClient<global::Supabase.Storage.Bucket, global::Supabase.Storage.FileObject> Storage
    {
        get
        {
            ThrowIfDisposed();
            return _children.Storage;
        }
    }

    public global::Supabase.Functions.Interfaces.IFunctionsClient Functions
    {
        get
        {
            ThrowIfDisposed();
            return _children.Functions;
        }
    }

    public SupabaseTable<TModel> Table<TModel>()
        where TModel : global::Supabase.Postgrest.Models.BaseModel, new()
    {
        ThrowIfDisposed();
        return new SupabaseTable<TModel>(Postgrest.Table<TModel>(), _tableRealtime);
    }

    public string Url
    {
        get
        {
            ThrowIfDisposed();
            return _url;
        }
    }

    public string AnonKey
    {
        get
        {
            ThrowIfDisposed();
            return _anonKey;
        }
    }

    public SupabaseUrls Urls
    {
        get
        {
            ThrowIfDisposed();
            return _urls;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _headerAuthBinding.Dispose();
        _realtimeTokenBinding.Dispose();
        _authStateBridge.Dispose();
        _tableRealtime.Dispose();
        _children.Auth.Shutdown();
        _children.Realtime.Disconnect();
    }

    public static ConfiguredClient Configure(SupabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Configure(options, SupabaseRuntimeContext.CreateDefault(options.SessionStore));
    }

    internal static ConfiguredClient Configure(SupabaseOptions options, SupabaseRuntimeContext runtimeContext)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);
        var snapshot = SupabaseConfigurationSnapshotFactory.Create(options, nameof(Configure));

        return new ConfiguredClient(snapshot, runtimeContext);
    }

    ISupabaseTable<TModel> ISupabaseClient.Table<TModel>()
    {
        return Table<TModel>();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
    }
}
