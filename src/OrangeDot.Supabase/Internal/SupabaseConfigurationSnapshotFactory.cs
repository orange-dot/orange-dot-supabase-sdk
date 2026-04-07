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

        if (string.IsNullOrWhiteSpace(options.Url))
        {
            throw new SupabaseConfigurationException(
                SupabaseErrorCode.ConfigurationMissing,
                "Supabase URL is required.",
                operation: operation);
        }

        if (string.IsNullOrWhiteSpace(options.AnonKey))
        {
            throw new SupabaseConfigurationException(
                SupabaseErrorCode.ConfigurationMissing,
                "Supabase anon key is required.",
                operation: operation);
        }

        try
        {
            var urls = SupabaseUrls.FromBaseUrl(options.Url!);

            return new LifecycleSnapshot(
                urls.NormalizedBaseUrl,
                options.AnonKey!,
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
