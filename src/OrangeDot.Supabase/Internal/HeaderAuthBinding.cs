using System;
using Microsoft.Extensions.Logging;
using OrangeDot.Supabase.Auth;

namespace OrangeDot.Supabase.Internal;

internal sealed class HeaderAuthBinding
{
    private readonly DynamicAuthHeaders _dynamicAuthHeaders;
    private readonly ILogger<HeaderAuthBinding> _logger;

    internal HeaderAuthBinding(
        IAuthStateObserver authStateObserver,
        DynamicAuthHeaders dynamicAuthHeaders,
        ILogger<HeaderAuthBinding> logger)
    {
        ArgumentNullException.ThrowIfNull(authStateObserver);
        ArgumentNullException.ThrowIfNull(dynamicAuthHeaders);
        ArgumentNullException.ThrowIfNull(logger);

        _dynamicAuthHeaders = dynamicAuthHeaders;
        _logger = logger;
        authStateObserver.Subscribe(Apply);
    }

    private void Apply(AuthState state)
    {
        switch (state)
        {
            case AuthState.Authenticated authenticated:
                _dynamicAuthHeaders.SetAccessToken(authenticated.AccessToken);
                _logger.LogInformation("Applied authenticated headers for child HTTP clients.");
                break;
            case AuthState.Anonymous:
            case AuthState.SignedOut:
                _dynamicAuthHeaders.ClearAccessToken();
                _logger.LogInformation("Cleared authenticated headers for child HTTP clients.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown auth state.");
        }
    }
}
