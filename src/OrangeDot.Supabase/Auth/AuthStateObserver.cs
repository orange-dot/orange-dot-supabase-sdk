using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OrangeDot.Supabase.Auth;

public sealed class AuthStateObserver : IAuthStateObserver
{
    private readonly object _gate = new();
    private readonly Dictionary<long, Action<AuthState>> _listeners = new();
    private long _nextSubscriptionId = 1;
    private AuthState _current = new AuthState.Anonymous();

    public AuthState Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public IDisposable Subscribe(Action<AuthState> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        AuthState current;
        long subscriptionId;

        lock (_gate)
        {
            subscriptionId = _nextSubscriptionId++;
            _listeners.Add(subscriptionId, listener);
            current = _current;
        }

        try
        {
            listener(current);
        }
        catch
        {
            lock (_gate)
            {
                _listeners.Remove(subscriptionId);
            }

            throw;
        }

        return new Subscription(this, subscriptionId);
    }

    internal void Publish(AuthState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Action<AuthState>[] listeners;

        lock (_gate)
        {
            _current = state;
            listeners = _listeners.Values.ToArray();
        }

        List<Exception>? exceptions = null;

        foreach (var listener in listeners)
        {
            try
            {
                listener(state);
            }
            catch (Exception exception)
            {
                exceptions ??= [];
                exceptions.Add(exception);
            }
        }

        if (exceptions is not null)
        {
            throw new AggregateException(
                "One or more auth state listeners threw while handling a published state.",
                exceptions);
        }
    }

    private void Unsubscribe(long subscriptionId)
    {
        lock (_gate)
        {
            _listeners.Remove(subscriptionId);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly AuthStateObserver _owner;
        private readonly long _subscriptionId;
        private int _disposed;

        public Subscription(AuthStateObserver owner, long subscriptionId)
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
