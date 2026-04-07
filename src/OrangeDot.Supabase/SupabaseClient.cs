using System;
using System.Threading.Tasks;
using OrangeDot.Supabase.Internal;
using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase;

public sealed class SupabaseClient : ISupabaseClient
{
    private readonly SupabaseChildClients _children;

    internal SupabaseClient(
        LifecycleSnapshot snapshot,
        SupabaseChildClients children,
        GotrueAuthStateBridge authStateBridge,
        HeaderAuthBinding headerAuthBinding,
        RealtimeTokenBinding realtimeTokenBinding)
    {
        _children = children;
        _ = authStateBridge;
        _ = headerAuthBinding;
        _ = realtimeTokenBinding;

        Url = snapshot.Url;
        AnonKey = snapshot.AnonKey;
        Urls = snapshot.Urls;
    }

    public Task Ready { get; } = Task.CompletedTask;

    public global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> Auth => _children.Auth;

    public global::Supabase.Postgrest.Interfaces.IPostgrestClient Postgrest => _children.Postgrest;

    public global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> Realtime => _children.Realtime;

    public global::Supabase.Storage.Interfaces.IStorageClient<global::Supabase.Storage.Bucket, global::Supabase.Storage.FileObject> Storage => _children.Storage;

    public global::Supabase.Functions.Interfaces.IFunctionsClient Functions => _children.Functions;

    public SupabaseTable<TModel> Table<TModel>()
        where TModel : global::Supabase.Postgrest.Models.BaseModel, new()
        => new(Postgrest.Table<TModel>(), new SupabaseTableRealtimeClient(Realtime));

    public string Url { get; }

    public string AnonKey { get; }

    public SupabaseUrls Urls { get; }

    public static ConfiguredClient Configure(SupabaseOptions options)
    {
        return Configure(options, SupabaseRuntimeContext.CreateDefault());
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
}
