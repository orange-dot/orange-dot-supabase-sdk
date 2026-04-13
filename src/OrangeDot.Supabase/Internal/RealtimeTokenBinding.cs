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
    private readonly IRuntimeTraceSink _traceSink;
    private readonly IDisposable _subscription;
    private int _disposed;

    internal RealtimeTokenBinding(
        IAuthStateObserver authStateObserver,
        global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> realtime,
        ILogger<RealtimeTokenBinding> logger,
        IRuntimeTraceSink? traceSink = null)
    {
        ArgumentNullException.ThrowIfNull(authStateObserver);
        ArgumentNullException.ThrowIfNull(realtime);
        ArgumentNullException.ThrowIfNull(logger);

        _realtime = realtime;
        _logger = logger;
        _traceSink = traceSink ?? NoOpRuntimeTraceSink.Instance;
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
                RecordTrace(state, BindingProjectionAction.Applied);
                _logger.LogInformation("Applied authenticated token to realtime client.");
                break;
            case AuthState.Refreshing refreshing:
                _realtime.SetAuth(refreshing.AccessToken);
                RecordTrace(state, BindingProjectionAction.Applied);
                _logger.LogInformation("Applied refreshing token projection to realtime client.");
                break;
            case AuthState.SignedOut:
            case AuthState.Anonymous:
            case AuthState.Faulted:
            {
                ClearProjection(state);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown auth state.");
        }
    }

    private void ClearProjection(AuthState? state = null)
    {
        _realtime.SetAuth(string.Empty);

        var channels = _realtime.Subscriptions.Values.ToArray();

        foreach (var channel in channels)
        {
            _realtime.Remove(channel);
        }

        if (state is not null)
        {
            RecordTrace(state, BindingProjectionAction.Cleared);
        }

        _logger.LogInformation(
            "Cleared realtime auth projection and removed {ChannelCount} realtime channels.",
            channels.Length);
    }

    private void RecordTrace(AuthState state, BindingProjectionAction action)
    {
        _traceSink.Record(new BindingProjectionTraceEvent(
            BindingTarget.Realtime,
            action,
            CanonicalAuthStateMachine.ToStateName(state),
            state.CanonicalVersion,
            CanonicalAuthStateMachine.GetPendingRefreshVersion(state),
            CanonicalAuthStateMachine.GetProjectionVersion(state)));
    }
}
