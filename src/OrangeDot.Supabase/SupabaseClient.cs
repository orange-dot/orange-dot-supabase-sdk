using System;
using System.Threading.Tasks;
using OrangeDot.Supabase.Errors;
using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase;

public sealed class SupabaseClient : ISupabaseClient
{
    internal SupabaseClient(LifecycleSnapshot snapshot)
    {
        Url = snapshot.Url;
        AnonKey = snapshot.AnonKey;
        Urls = snapshot.Urls;
    }

    public Task Ready { get; } = Task.CompletedTask;

    public string Url { get; }

    public string AnonKey { get; }

    public SupabaseUrls Urls { get; }

    public static ConfiguredClient Configure(SupabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Url))
        {
            throw new SupabaseConfigurationException(
                SupabaseErrorCode.ConfigurationMissing,
                "Supabase URL is required.",
                operation: nameof(Configure));
        }

        if (string.IsNullOrWhiteSpace(options.AnonKey))
        {
            throw new SupabaseConfigurationException(
                SupabaseErrorCode.ConfigurationMissing,
                "Supabase anon key is required.",
                operation: nameof(Configure));
        }

        try
        {
            var urls = SupabaseUrls.FromBaseUrl(options.Url!);
            var snapshot = new LifecycleSnapshot(
                urls.NormalizedBaseUrl,
                options.AnonKey!,
                urls);

            return new ConfiguredClient(snapshot);
        }
        catch (ArgumentException exception)
        {
            throw new SupabaseConfigurationException(
                SupabaseErrorCode.ConfigurationInvalid,
                "Supabase URL is invalid.",
                operation: nameof(Configure),
                innerException: exception);
        }
    }
}
