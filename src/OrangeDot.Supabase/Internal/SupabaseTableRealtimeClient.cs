using System;
using System.Threading.Tasks;

namespace OrangeDot.Supabase.Internal;

internal sealed class SupabaseTableRealtimeClient : ISupabaseTableRealtimeClient
{
    private readonly global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> _realtime;

    internal SupabaseTableRealtimeClient(
        global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> realtime)
    {
        _realtime = realtime;
    }

    public bool HasSocket => _realtime.Socket is not null;

    public async Task ConnectAsync()
    {
        await _realtime.ConnectAsync();
    }

    public global::Supabase.Realtime.Interfaces.IRealtimeChannel Channel(string channelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        return _realtime.Channel(channelName);
    }
}
