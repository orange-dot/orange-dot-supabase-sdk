using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase;

public interface ISupabaseStatelessClient
{
    global::Supabase.Gotrue.Interfaces.IGotrueStatelessClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> Auth { get; }

    /// <summary>
    /// Gets the project-level GoTrue options used by the stateless auth client.
    /// Delegated user or service tokens are not stored here; they remain per-operation input.
    /// </summary>
    global::Supabase.Gotrue.StatelessClient.StatelessClientOptions AuthOptions { get; }

    global::Supabase.Postgrest.Interfaces.IPostgrestClient Postgrest { get; }

    global::Supabase.Storage.Interfaces.IStorageClient<global::Supabase.Storage.Bucket, global::Supabase.Storage.FileObject> Storage { get; }

    global::Supabase.Functions.Interfaces.IFunctionsClient Functions { get; }

    string Url { get; }

    string AnonKey { get; }

    SupabaseUrls Urls { get; }
}
