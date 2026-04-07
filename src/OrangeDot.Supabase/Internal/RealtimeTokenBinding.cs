using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using OrangeDot.Supabase.Auth;

namespace OrangeDot.Supabase.Internal;

internal sealed class RealtimeTokenBinding
{
    private readonly global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> _realtime;
    private readonly ILogger<RealtimeTokenBinding> _logger;

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
        authStateObserver.Subscribe(Apply);
    }

    private void Apply(AuthState state)
    {
        switch (state)
        {
            case AuthState.Authenticated authenticated:
                _realtime.SetAuth(authenticated.AccessToken);
                _logger.LogInformation("Applied authenticated token to realtime client.");
                break;
            case AuthState.SignedOut:
            {
                var channels = _realtime.Subscriptions.Values.ToArray();

                foreach (var channel in channels)
                {
                    _realtime.Remove(channel);
                }

                _logger.LogInformation(
                    "Unsubscribed {ChannelCount} realtime channels after sign-out.",
                    channels.Length);
                break;
            }
            case AuthState.Anonymous:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown auth state.");
        }
    }
}
