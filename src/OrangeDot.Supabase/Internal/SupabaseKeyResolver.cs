using System;
using OrangeDot.Supabase.Errors;

namespace OrangeDot.Supabase.Internal;

internal static class SupabaseKeyResolver
{
    internal static string ResolveProjectKey(string? publishableKey, string? anonKey, string operation)
    {
        return ResolveRequiredKey(
            publishableKey,
            anonKey,
            primaryName: "PublishableKey",
            legacyName: "AnonKey",
            displayName: "publishable key",
            operation: operation);
    }

    internal static string ResolvePrivilegedKey(string? secretKey, string? serviceRoleKey, string operation)
    {
        return ResolveRequiredKey(
            secretKey,
            serviceRoleKey,
            primaryName: "SecretKey",
            legacyName: "ServiceRoleKey",
            displayName: "secret key",
            operation: operation);
    }

    private static string ResolveRequiredKey(
        string? primaryValue,
        string? legacyValue,
        string primaryName,
        string legacyName,
        string displayName,
        string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var hasPrimary = !string.IsNullOrWhiteSpace(primaryValue);
        var hasLegacy = !string.IsNullOrWhiteSpace(legacyValue);

        if (hasPrimary && hasLegacy && !string.Equals(primaryValue, legacyValue, StringComparison.Ordinal))
        {
            throw new SupabaseConfigurationException(
                SupabaseErrorCode.ConfigurationInvalid,
                $"Supabase {displayName} is configured with both {primaryName} and {legacyName}, but the values differ.",
                operation: operation);
        }

        if (hasPrimary)
        {
            return primaryValue!;
        }

        if (hasLegacy)
        {
            return legacyValue!;
        }

        throw new SupabaseConfigurationException(
            SupabaseErrorCode.ConfigurationMissing,
            $"Supabase {displayName} is required.",
            operation: operation);
    }
}
