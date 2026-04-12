using System;

namespace OrangeDot.Supabase.Internal;

internal sealed record StatelessChildAuthorization(
    string ApiKey,
    string? BearerToken = null)
{
    internal static StatelessChildAuthorization ForProjectKey(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return new StatelessChildAuthorization(apiKey);
    }

    internal static StatelessChildAuthorization ForDelegatedUser(string apiKey, string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        return new StatelessChildAuthorization(apiKey, accessToken);
    }

    internal static StatelessChildAuthorization ForPrivilegedKey(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return new StatelessChildAuthorization(
            apiKey,
            SupabaseKeyClassifier.ShouldSendBearerAuthorization(apiKey) ? apiKey : null);
    }
}
