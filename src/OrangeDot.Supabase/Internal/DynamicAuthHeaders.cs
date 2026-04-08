using System;
using System.Collections.Generic;

namespace OrangeDot.Supabase.Internal;

internal sealed class DynamicAuthHeaders
{
    private readonly string _apiKey;
    private volatile string? _accessToken;
    private volatile string? _authorizationHeaderValue;

    internal DynamicAuthHeaders(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _apiKey = apiKey;
    }

    internal void SetAccessToken(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        _accessToken = accessToken;
        _authorizationHeaderValue = $"Bearer {accessToken}";
    }

    internal void ClearAccessToken()
    {
        _accessToken = null;
        _authorizationHeaderValue = null;
    }

    internal Dictionary<string, string> Build()
    {
        var headers = new Dictionary<string, string>
        {
            ["apikey"] = _apiKey
        };

        var authorizationHeaderValue = _authorizationHeaderValue;

        if (!string.IsNullOrWhiteSpace(authorizationHeaderValue) && !string.IsNullOrWhiteSpace(_accessToken))
        {
            headers["Authorization"] = authorizationHeaderValue;
        }

        return headers;
    }
}
