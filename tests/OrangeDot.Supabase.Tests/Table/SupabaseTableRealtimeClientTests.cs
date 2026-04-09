using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase.Tests.Table;

public sealed class SupabaseTableRealtimeClientTests
{
    [Fact]
    public void Subscribe_to_socket_state_replays_latest_state_and_stops_after_dispose()
    {
        var realtime = new global::Supabase.Realtime.Client("wss://abc.supabase.co/realtime/v1");
        using var wrapper = new SupabaseTableRealtimeClient(realtime);

        NotifySocketState(realtime, global::Supabase.Realtime.Constants.SocketState.Open);

        var received = new List<global::Supabase.Realtime.Constants.SocketState>();
        var subscription = wrapper.SubscribeToSocketState(received.Add);

        NotifySocketState(realtime, global::Supabase.Realtime.Constants.SocketState.Reconnect);
        subscription.Dispose();
        NotifySocketState(realtime, global::Supabase.Realtime.Constants.SocketState.Open);

        Assert.Equal(
            [
                global::Supabase.Realtime.Constants.SocketState.Open,
                global::Supabase.Realtime.Constants.SocketState.Reconnect
            ],
            received);
    }

    [Fact]
    public async Task Force_reconnect_async_throws_when_socket_is_missing()
    {
        var realtime = new global::Supabase.Realtime.Client("wss://abc.supabase.co/realtime/v1");
        using var wrapper = new SupabaseTableRealtimeClient(realtime);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => wrapper.ForceReconnectAsync());

        Assert.Equal("Cannot force reconnect because the realtime socket is not connected.", exception.Message);
    }

    [Fact]
    public async Task Invoke_reconnect_async_uses_socket_reconnect_method_when_available()
    {
        var socket = new FakeReconnectTarget();

        await SupabaseTableRealtimeClient.InvokeReconnectAsync(socket);

        Assert.Equal(1, socket.ReconnectCallCount);
    }

    [Fact]
    public async Task Invoke_reconnect_async_uses_private_connection_reconnect_method_when_socket_reconnect_is_missing()
    {
        var socket = new FakeSocketWithConnection();

        await SupabaseTableRealtimeClient.InvokeReconnectAsync(socket);

        Assert.Equal(1, socket.Connection.ReconnectCallCount);
    }

    private static void NotifySocketState(
        global::Supabase.Realtime.Client realtime,
        global::Supabase.Realtime.Constants.SocketState state)
    {
        var method = typeof(global::Supabase.Realtime.Client).GetMethod(
            "NotifySocketStateChange",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(realtime, [state]);
    }

    private sealed class FakeReconnectTarget
    {
        public int ReconnectCallCount { get; private set; }

        public Task Reconnect()
        {
            ReconnectCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSocketWithConnection
    {
        private readonly FakeConnection _connection = new();

        public FakeConnection Connection => _connection;
    }

    private sealed class FakeConnection
    {
        public int ReconnectCallCount { get; private set; }

        public Task Reconnect()
        {
            ReconnectCallCount++;
            return Task.CompletedTask;
        }
    }
}
