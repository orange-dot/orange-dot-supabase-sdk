using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OrangeDot.Supabase.Internal;

internal sealed class SupabaseTableRealtimeClient : ISupabaseTableRealtimeClient
{
    private readonly global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> _realtime;
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly Dictionary<long, Action<global::Supabase.Realtime.Constants.SocketState>> _stateListeners = new();
    private long _nextSubscriptionId = 1;
    private global::Supabase.Realtime.Constants.SocketState? _lastSocketState;
    private int _disposed;

    internal SupabaseTableRealtimeClient(
        global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> realtime)
    {
        _realtime = realtime;
        _realtime.AddStateChangedHandler(HandleSocketStateChanged);
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

    public IDisposable SubscribeToSocketState(Action<global::Supabase.Realtime.Constants.SocketState> listener)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        ArgumentNullException.ThrowIfNull(listener);

        long subscriptionId = 0;
        global::Supabase.Realtime.Constants.SocketState? replayState;

        try
        {
            lock (_stateGate)
            {
                subscriptionId = _nextSubscriptionId++;
                _stateListeners.Add(subscriptionId, listener);
                replayState = _lastSocketState;
            }

            if (replayState is { } state)
            {
                listener(state);
            }
        }
        catch
        {
            lock (_stateGate)
            {
                _stateListeners.Remove(subscriptionId);
            }

            throw;
        }

        return new SocketStateSubscription(this, subscriptionId);
    }

    public async Task ForceReconnectAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        var socket = _realtime.Socket
            ?? throw new InvalidOperationException("Cannot force reconnect because the realtime socket is not connected.");

        await InvokeReconnectAsync(socket).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _realtime.RemoveStateChangedHandler(HandleSocketStateChanged);
        lock (_stateGate)
        {
            _stateListeners.Clear();
        }

        _connectGate.Dispose();
    }

    private void HandleSocketStateChanged(
        global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> _,
        global::Supabase.Realtime.Constants.SocketState state)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        Action<global::Supabase.Realtime.Constants.SocketState>[] listeners;

        lock (_stateGate)
        {
            _lastSocketState = state;
            listeners = [.. _stateListeners.Values];
        }

        foreach (var listener in listeners)
        {
            try
            {
                listener(state);
            }
            catch
            {
                // Test-only listeners must not break the production realtime state path.
            }
        }
    }

    internal static async Task InvokeReconnectAsync(object socket)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var socketType = socket.GetType();
        var reconnectMethod = FindReconnectMethod(socketType);

        if (reconnectMethod is not null)
        {
            var reconnectResult = reconnectMethod.Invoke(socket, null);

            if (reconnectResult is Task reconnectTask)
            {
                await reconnectTask.ConfigureAwait(false);
            }

            return;
        }

        var connectionField = socketType.GetField("_connection", BindingFlags.Instance | BindingFlags.NonPublic);
        var connection = connectionField?.GetValue(socket)
            ?? throw new InvalidOperationException(
                $"Cannot force reconnect because {socketType.FullName} does not expose a reconnect-capable connection.");

        var connectionReconnectMethod = FindReconnectMethod(connection.GetType())
            ?? throw new InvalidOperationException(
                $"Cannot force reconnect because {connection.GetType().FullName} does not expose a reconnect method.");

        var connectionReconnectResult = connectionReconnectMethod.Invoke(connection, null);

        if (connectionReconnectResult is Task connectionReconnectTask)
        {
            await connectionReconnectTask.ConfigureAwait(false);
        }
    }

    private static MethodInfo? FindReconnectMethod(Type type)
    {
        return type.GetMethod("Reconnect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? type.GetMethod("ReconnectAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private void Unsubscribe(long subscriptionId)
    {
        lock (_stateGate)
        {
            _stateListeners.Remove(subscriptionId);
        }
    }

    private sealed class SocketStateSubscription : IDisposable
    {
        private readonly SupabaseTableRealtimeClient _owner;
        private readonly long _subscriptionId;
        private int _disposed;

        public SocketStateSubscription(SupabaseTableRealtimeClient owner, long subscriptionId)
        {
            _owner = owner;
            _subscriptionId = subscriptionId;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _owner.Unsubscribe(_subscriptionId);
        }
    }
}
