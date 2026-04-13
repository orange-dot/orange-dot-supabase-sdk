using System;
using System.Threading.Tasks;

namespace OrangeDot.Supabase.Internal;

internal interface ISupabaseTableRealtimeClient : IDisposable
{
    bool HasSocket { get; }

    Task ConnectAsync();

    global::Supabase.Realtime.Interfaces.IRealtimeChannel Channel(string channelName);

    void Remove(global::Supabase.Realtime.Interfaces.IRealtimeChannel channel);

    IDisposable SubscribeToSocketState(Action<global::Supabase.Realtime.Constants.SocketState> listener);

    Task ForceReconnectAsync();
}
