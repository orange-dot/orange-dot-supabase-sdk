using System;
using OrangeDot.Supabase.Errors;
using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase.Internal;

internal static class SupabaseConfigurationSnapshotFactory
{
    internal static LifecycleSnapshot Create(SupabaseOptions options, string operation)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        return Create(options.Url, () => SupabaseKeyResolver.ResolveProjectKey(
            options.ConfiguredPublishableKey,
            options.ConfiguredAnonKey,
            operation), operation);
    }

    internal static LifecycleSnapshot Create(SupabaseServerOptions options, string operation)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        return Create(options.Url, () => SupabaseKeyResolver.ResolveProjectKey(
            options.ConfiguredPublishableKey,
            options.ConfiguredAnonKey,
            operation), operation);
    }

    private static LifecycleSnapshot Create(string? url, Func<string> resolveProjectKey, string operation)
    {
        ArgumentNullException.ThrowIfNull(resolveProjectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new SupabaseConfigurationException(
                SupabaseErrorCode.ConfigurationMissing,
                "Supabase URL is required.",
                operation: operation);
        }

        var projectKey = resolveProjectKey();

        try
        {
            var urls = SupabaseUrls.FromBaseUrl(url!);

            return new LifecycleSnapshot(
                urls.NormalizedBaseUrl,
                projectKey,
                urls);
        }
        catch (ArgumentException exception)
        {
            throw new SupabaseConfigurationException(
                SupabaseErrorCode.ConfigurationInvalid,
                "Supabase URL is invalid.",
                operation: operation,
                innerException: exception);
        }
    }
}
