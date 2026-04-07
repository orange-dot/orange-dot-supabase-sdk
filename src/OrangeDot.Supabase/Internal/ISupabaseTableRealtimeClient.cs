using System.Threading.Tasks;

namespace OrangeDot.Supabase.Internal;

internal interface ISupabaseTableRealtimeClient
{
    bool HasSocket { get; }

    Task ConnectAsync();

    global::Supabase.Realtime.Interfaces.IRealtimeChannel Channel(string channelName);
}
