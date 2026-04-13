using System;
using System.Collections.Generic;
using System.Threading;

namespace OrangeDot.Supabase.Internal;

internal sealed class DynamicAuthHeaders
{
    private readonly string _apiKey;
    private volatile AuthHeaderSnapshot? _snapshot;

    internal DynamicAuthHeaders(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _apiKey = apiKey;
    }

    internal void SetAccessToken(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        _snapshot = new AuthHeaderSnapshot(accessToken, $"Bearer {accessToken}");
    }

    internal void ClearAccessToken()
    {
        _snapshot = null;
    }

    internal Dictionary<string, string> Build()
    {
        var snapshot = _snapshot;

        if (snapshot is not null)
        {
            return new Dictionary<string, string>(2)
            {
                ["apikey"] = _apiKey,
                ["Authorization"] = snapshot.AuthorizationValue
            };
        }

        return new Dictionary<string, string>(1)
        {
            ["apikey"] = _apiKey
        };
    }

    private sealed class AuthHeaderSnapshot
    {
        internal string AccessToken { get; }
        internal string AuthorizationValue { get; }

        internal AuthHeaderSnapshot(string accessToken, string authorizationValue)
        {
            AccessToken = accessToken;
            AuthorizationValue = authorizationValue;
        }
    }
}
