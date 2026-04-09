using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using OrangeDot.Supabase.Auth;

namespace OrangeDot.Supabase.Internal;

internal sealed class RealtimeTokenBinding : IDisposable
{
    private readonly global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> _realtime;
    private readonly ILogger<RealtimeTokenBinding> _logger;
    private readonly IDisposable _subscription;
    private int _disposed;

    internal RealtimeTokenBinding(
        IAuthStateObserver authStateObserver,
        global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> realtime,
        ILogger<RealtimeTokenBinding> logger)
    {
        ArgumentNullException.ThrowIfNull(authStateObserver);
        ArgumentNullException.ThrowIfNull(realtime);
        ArgumentNullException.ThrowIfNull(logger);

        _realtime = realtime;
        _logger = logger;
        _subscription = authStateObserver.Subscribe(Apply);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _subscription.Dispose();
        ClearProjection();
    }

    private void Apply(AuthState state)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        switch (state)
        {
            case AuthState.Authenticated authenticated:
                _realtime.SetAuth(authenticated.AccessToken);
                _logger.LogInformation("Applied authenticated token to realtime client.");
                break;
            case AuthState.Refreshing refreshing:
                _realtime.SetAuth(refreshing.AccessToken);
                _logger.LogInformation("Applied refreshing token projection to realtime client.");
                break;
            case AuthState.SignedOut:
            case AuthState.Anonymous:
            case AuthState.Faulted:
            {
                ClearProjection();
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown auth state.");
        }
    }

    private void ClearProjection()
    {
        _realtime.SetAuth(string.Empty);

        var channels = _realtime.Subscriptions.Values.ToArray();

        foreach (var channel in channels)
        {
            _realtime.Remove(channel);
        }

        _logger.LogInformation(
            "Cleared realtime auth projection and removed {ChannelCount} realtime channels.",
            channels.Length);
    }
}
