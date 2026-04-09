using OrangeDot.Supabase.Internal;
using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase;

public sealed class SupabaseStatelessClient : ISupabaseStatelessClient
{
    internal SupabaseStatelessClient(
        LifecycleSnapshot snapshot,
        global::Supabase.Gotrue.StatelessClient auth,
        global::Supabase.Gotrue.StatelessClient.StatelessClientOptions authOptions,
        global::Supabase.Postgrest.Client postgrest,
        global::Supabase.Storage.Client storage,
        global::Supabase.Functions.Client functions)
    {
        Auth = auth;
        AuthOptions = authOptions;
        Postgrest = postgrest;
        Storage = storage;
        Functions = functions;
        Url = snapshot.Url;
        AnonKey = snapshot.AnonKey;
        Urls = snapshot.Urls;
    }

    public global::Supabase.Gotrue.Interfaces.IGotrueStatelessClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> Auth { get; }

    public global::Supabase.Gotrue.StatelessClient.StatelessClientOptions AuthOptions { get; }

    public global::Supabase.Postgrest.Interfaces.IPostgrestClient Postgrest { get; }

    public global::Supabase.Storage.Interfaces.IStorageClient<global::Supabase.Storage.Bucket, global::Supabase.Storage.FileObject> Storage { get; }

    public global::Supabase.Functions.Interfaces.IFunctionsClient Functions { get; }

    public string Url { get; }

    public string AnonKey { get; }

    public SupabaseUrls Urls { get; }

    public static SupabaseStatelessClient Create(SupabaseOptions options)
    {
        var snapshot = SupabaseConfigurationSnapshotFactory.Create(options, nameof(Create));

        return Create(snapshot);
    }

    internal static SupabaseStatelessClient Create(LifecycleSnapshot snapshot, string? bearerToken = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var factory = new SupabaseStatelessChildClientFactory();

        return new SupabaseStatelessClient(
            snapshot,
            factory.CreateAuth(),
            factory.CreateAuthOptions(snapshot),
            factory.CreatePostgrest(snapshot, bearerToken),
            factory.CreateStorage(snapshot, bearerToken),
            factory.CreateFunctions(snapshot, bearerToken));
    }
}
