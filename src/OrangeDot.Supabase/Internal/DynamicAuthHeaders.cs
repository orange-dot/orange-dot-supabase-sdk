using System;
using System.Collections.Generic;

namespace OrangeDot.Supabase.Internal;

internal sealed class DynamicAuthHeaders
{
    private readonly string _apiKey;
    private volatile string? _accessToken;

    internal DynamicAuthHeaders(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _apiKey = apiKey;
    }

    internal void SetAccessToken(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        _accessToken = accessToken;
    }

    internal void ClearAccessToken()
    {
        _accessToken = null;
    }

    internal Dictionary<string, string> Build()
    {
        var headers = new Dictionary<string, string>
        {
            ["apikey"] = _apiKey
        };

        var accessToken = _accessToken;

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            headers["Authorization"] = $"Bearer {accessToken}";
        }

        return headers;
    }
}
