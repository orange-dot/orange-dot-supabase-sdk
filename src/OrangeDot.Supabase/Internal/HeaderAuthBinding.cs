using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using OrangeDot.Supabase.Auth;

namespace OrangeDot.Supabase.Internal;

internal sealed class HeaderAuthBinding : IDisposable
{
    private readonly DynamicAuthHeaders _dynamicAuthHeaders;
    private readonly ILogger<HeaderAuthBinding> _logger;
    private readonly IRuntimeTraceSink _traceSink;
    private readonly IDisposable _subscription;
    private int _disposed;

    internal HeaderAuthBinding(
        IAuthStateObserver authStateObserver,
        DynamicAuthHeaders dynamicAuthHeaders,
        ILogger<HeaderAuthBinding> logger,
        IRuntimeTraceSink? traceSink = null)
    {
        ArgumentNullException.ThrowIfNull(authStateObserver);
        ArgumentNullException.ThrowIfNull(dynamicAuthHeaders);
        ArgumentNullException.ThrowIfNull(logger);

        _dynamicAuthHeaders = dynamicAuthHeaders;
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
        _dynamicAuthHeaders.ClearAccessToken();
    }

    private void Apply(AuthState state)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        switch (state)
        {
            case AuthState.Refreshing { AccessToken: var accessToken }:
                _dynamicAuthHeaders.SetAccessToken(accessToken);
                RecordTrace(state, BindingProjectionAction.Applied);
                _logger.LogInformation("Applied authenticated headers for child HTTP clients.");
                break;
            case AuthState.Authenticated authenticated:
                _dynamicAuthHeaders.SetAccessToken(authenticated.AccessToken);
                RecordTrace(state, BindingProjectionAction.Applied);
                _logger.LogInformation("Applied authenticated headers for child HTTP clients.");
                break;
            case AuthState.Anonymous:
            case AuthState.SignedOut:
            case AuthState.Faulted:
                _dynamicAuthHeaders.ClearAccessToken();
                RecordTrace(state, BindingProjectionAction.Cleared);
                _logger.LogInformation("Cleared authenticated headers for child HTTP clients.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown auth state.");
        }
    }

    private void RecordTrace(AuthState state, BindingProjectionAction action)
    {
        _traceSink.Record(new BindingProjectionTraceEvent(
            BindingTarget.Header,
            action,
            CanonicalAuthStateMachine.ToStateName(state),
            state.CanonicalVersion,
            CanonicalAuthStateMachine.GetPendingRefreshVersion(state),
            CanonicalAuthStateMachine.GetProjectionVersion(state)));
    }
}
