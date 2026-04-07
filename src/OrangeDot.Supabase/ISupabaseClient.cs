using System.Threading.Tasks;
using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase;

public interface ISupabaseClient
{
    global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> Auth { get; }

    global::Supabase.Postgrest.Interfaces.IPostgrestClient Postgrest { get; }

    global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> Realtime { get; }

    global::Supabase.Storage.Interfaces.IStorageClient<global::Supabase.Storage.Bucket, global::Supabase.Storage.FileObject> Storage { get; }

    global::Supabase.Functions.Interfaces.IFunctionsClient Functions { get; }

    string Url { get; }

    string AnonKey { get; }

    SupabaseUrls Urls { get; }

    Task Ready { get; }
}
