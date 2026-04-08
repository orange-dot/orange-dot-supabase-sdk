using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrangeDot.Supabase.Internal;

internal sealed class SupabaseTableRealtimeClient : ISupabaseTableRealtimeClient
{
    private readonly global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> _realtime;
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private int _disposed;

    internal SupabaseTableRealtimeClient(
        global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> realtime)
    {
        _realtime = realtime;
    }

    public bool HasSocket => _realtime.Socket is not null;

    public async Task ConnectAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        if (HasSocket)
        {
            return;
        }

        await _connectGate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (!HasSocket)
            {
                await _realtime.ConnectAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _connectGate.Release();
        }
    }

    public global::Supabase.Realtime.Interfaces.IRealtimeChannel Channel(string channelName)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

        return _realtime.Channel(channelName);
    }

    public void Remove(global::Supabase.Realtime.Interfaces.IRealtimeChannel channel)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        ArgumentNullException.ThrowIfNull(channel);

        if (channel is not global::Supabase.Realtime.RealtimeChannel realtimeChannel)
        {
            throw new ArgumentException("The channel must be a RealtimeChannel instance.", nameof(channel));
        }

        _realtime.Remove(realtimeChannel);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _connectGate.Dispose();
    }
}
