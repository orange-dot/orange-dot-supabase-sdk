using System;
using System.Collections.Generic;

namespace OrangeDot.Supabase.Internal;

internal sealed class SupabaseStatelessChildClientFactory
{
    internal global::Supabase.Gotrue.StatelessClient CreateAuth()
    {
        return new global::Supabase.Gotrue.StatelessClient();
    }

    internal global::Supabase.Gotrue.StatelessClient.StatelessClientOptions CreateAuthOptions(LifecycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var options = new global::Supabase.Gotrue.StatelessClient.StatelessClientOptions
        {
            Url = snapshot.Urls.AuthUrl
        };

        foreach (var header in SupabaseChildClientFactory.CreateStaticHeaders(snapshot.AnonKey))
        {
            options.Headers.Add(header.Key, header.Value);
        }

        return options;
    }

    internal global::Supabase.Postgrest.Client CreatePostgrest(LifecycleSnapshot snapshot, string? bearerToken = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Keep ClientOptions.Headers empty in the stateless path so GetHeaders stays authoritative.
        return new global::Supabase.Postgrest.Client(
            snapshot.Urls.RestUrl,
            new global::Supabase.Postgrest.ClientOptions())
        {
            GetHeaders = () => CreateHeaders(snapshot.AnonKey, bearerToken)
        };
    }

    internal global::Supabase.Storage.Client CreateStorage(LifecycleSnapshot snapshot, string? bearerToken = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Storage merges runtime headers behind constructor headers, so delegated auth must be fixed at construction time.
        return new global::Supabase.Storage.Client(
            snapshot.Urls.StorageUrl,
            headers: CreateStorageHeaders(snapshot.AnonKey, bearerToken));
    }

    internal global::Supabase.Functions.Client CreateFunctions(LifecycleSnapshot snapshot, string? bearerToken = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // The stateless server path binds identity at client creation time, not through per-invoke token parameters.
        return new global::Supabase.Functions.Client(snapshot.Urls.FunctionsUrl)
        {
            GetHeaders = () => CreateHeaders(snapshot.AnonKey, bearerToken)
        };
    }

    private static Dictionary<string, string> CreateHeaders(string apiKey, string? bearerToken)
    {
        var headers = SupabaseChildClientFactory.CreateStaticHeaders(apiKey);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            headers["Authorization"] = $"Bearer {bearerToken}";
        }

        return headers;
    }

    private static Dictionary<string, string> CreateStorageHeaders(string apiKey, string? bearerToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return SupabaseChildClientFactory.CreateStorageHeaders(apiKey);
        }

        return SupabaseChildClientFactory.CreateStorageHeaders(
            apiKey,
            new Dictionary<string, string>
            {
                ["apikey"] = apiKey,
                ["Authorization"] = $"Bearer {bearerToken}"
            });
    }
}
