using System;
using OrangeDot.Supabase.Auth;

namespace OrangeDot.Supabase.Internal;

internal sealed record CanonicalAuthSnapshot(
    string AuthState,
    long CanonicalVersion,
    long PendingRefreshVersion);

internal sealed class CanonicalAuthStateMachine
{
    private readonly object _gate = new();
    private AuthState _currentState;
    private AuthState.Authenticated? _lastAuthenticatedState;
    private long _pendingRefreshVersion;

    internal CanonicalAuthStateMachine()
        : this(new AuthState.Anonymous())
    {
    }

    internal CanonicalAuthStateMachine(
        AuthState currentState,
        long pendingRefreshVersion = 0,
        AuthState.Authenticated? lastAuthenticatedState = null)
    {
        ArgumentNullException.ThrowIfNull(currentState);

        _currentState = currentState;
        _pendingRefreshVersion = ResolvePendingRefreshVersion(currentState, pendingRefreshVersion);
        _lastAuthenticatedState = lastAuthenticatedState ?? currentState as AuthState.Authenticated;
    }

    internal AuthState CurrentState
    {
        get
        {
            lock (_gate)
            {
                return _currentState;
            }
        }
    }

    internal long PendingRefreshVersion
    {
        get
        {
            lock (_gate)
            {
                return _pendingRefreshVersion;
            }
        }
    }

    internal long CurrentProjectionVersion
    {
        get
        {
            lock (_gate)
            {
                return GetProjectionVersion(_currentState);
            }
        }
    }

    internal bool IsSignedOut
    {
        get
        {
            lock (_gate)
            {
                return _currentState is AuthState.SignedOut;
            }
        }
    }

    internal CanonicalAuthSnapshot CaptureSnapshot()
    {
        lock (_gate)
        {
            return new CanonicalAuthSnapshot(
                ToStateName(_currentState),
                _currentState.CanonicalVersion,
                _pendingRefreshVersion);
        }
    }

    internal bool IsRedundantAuthenticatedProjection(SessionSnapshot snapshot)
    {
        lock (_gate)
        {
            return _currentState is AuthState.Authenticated authenticated &&
                   string.Equals(authenticated.AccessToken, snapshot.AccessToken, StringComparison.Ordinal) &&
                   string.Equals(authenticated.RefreshToken, snapshot.RefreshToken, StringComparison.Ordinal);
        }
    }

    internal bool TryAdvanceAuthenticated(SessionSnapshot snapshot, out AuthState.Authenticated state)
    {
        lock (_gate)
        {
            if (_currentState is AuthState.Authenticated authenticated &&
                string.Equals(authenticated.AccessToken, snapshot.AccessToken, StringComparison.Ordinal) &&
                string.Equals(authenticated.RefreshToken, snapshot.RefreshToken, StringComparison.Ordinal))
            {
                state = authenticated;
                return false;
            }

            state = AdvanceAuthenticatedCore(snapshot, advanceVersion: true);
            return true;
        }
    }

    internal AuthState.Authenticated AdvanceAuthenticated(SessionSnapshot snapshot)
    {
        lock (_gate)
        {
            return AdvanceAuthenticatedCore(snapshot, advanceVersion: true);
        }
    }

    internal AuthState.Refreshing BeginRefresh(SessionSnapshot snapshot)
    {
        lock (_gate)
        {
            _pendingRefreshVersion = _currentState.CanonicalVersion + 1;
            var source = _lastAuthenticatedState;

            var state = new AuthState.Refreshing(
                _currentState.CanonicalVersion,
                _pendingRefreshVersion,
                source?.AccessToken ?? snapshot.AccessToken,
                source?.RefreshToken ?? snapshot.RefreshToken,
                source?.ExpiresAt ?? snapshot.ExpiresAt);
            _currentState = state;
            return state;
        }
    }

    internal AuthState.Authenticated CompleteRefresh(SessionSnapshot snapshot)
    {
        lock (_gate)
        {
            return AdvanceAuthenticatedCore(snapshot, advanceVersion: false);
        }
    }

    internal AuthState.Faulted Fault(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        lock (_gate)
        {
            var state = new AuthState.Faulted(_currentState.CanonicalVersion, _pendingRefreshVersion, reason);
            _pendingRefreshVersion = 0;
            _lastAuthenticatedState = null;
            _currentState = state;
            return state;
        }
    }

    internal bool TrySignOut(out AuthState.SignedOut state)
    {
        lock (_gate)
        {
            if (_currentState is AuthState.SignedOut signedOut)
            {
                state = signedOut;
                return false;
            }

            _pendingRefreshVersion = 0;
            _lastAuthenticatedState = null;
            state = new AuthState.SignedOut(_currentState.CanonicalVersion);
            _currentState = state;
            return true;
        }
    }

    internal bool TryIgnoreStaleRefreshResultAfterSignOut()
    {
        lock (_gate)
        {
            if (_currentState is not AuthState.SignedOut)
            {
                return false;
            }

            _pendingRefreshVersion = 0;
            return true;
        }
    }

    internal static string ToStateName(AuthState state)
    {
        return state switch
        {
            AuthState.Authenticated => "Authenticated",
            AuthState.Refreshing => "Refreshing",
            AuthState.SignedOut => "SignedOut",
            AuthState.Anonymous => "Anonymous",
            AuthState.Faulted => "Faulted",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown auth state.")
        };
    }

    internal static long GetPendingRefreshVersion(AuthState state)
    {
        return state switch
        {
            AuthState.Refreshing refreshing => refreshing.PendingRefreshVersion,
            AuthState.Faulted faulted => faulted.PendingRefreshVersion,
            _ => 0
        };
    }

    private AuthState.Authenticated AdvanceAuthenticatedCore(SessionSnapshot snapshot, bool advanceVersion)
    {
        var canonicalVersion = advanceVersion
            ? _currentState.CanonicalVersion + 1
            : _pendingRefreshVersion > _currentState.CanonicalVersion
                ? _pendingRefreshVersion
                : _currentState.CanonicalVersion + 1;

        _pendingRefreshVersion = 0;

        var state = new AuthState.Authenticated(
            canonicalVersion,
            snapshot.AccessToken,
            snapshot.RefreshToken,
            snapshot.ExpiresAt);
        _currentState = state;
        _lastAuthenticatedState = state;
        return state;
    }

    private static long ResolvePendingRefreshVersion(AuthState currentState, long pendingRefreshVersion)
    {
        return currentState switch
        {
            AuthState.Refreshing refreshing => refreshing.PendingRefreshVersion,
            AuthState.Faulted faulted => faulted.PendingRefreshVersion,
            _ => pendingRefreshVersion
        };
    }

    internal static long GetProjectionVersion(AuthState state)
    {
        return state switch
        {
            AuthState.Authenticated => state.CanonicalVersion,
            AuthState.Refreshing => state.CanonicalVersion,
            _ => 0
        };
    }
}
