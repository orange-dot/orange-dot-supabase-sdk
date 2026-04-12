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

    internal global::Supabase.Postgrest.Client CreatePostgrest(
        LifecycleSnapshot snapshot,
        StatelessChildAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(authorization);

        // Keep ClientOptions.Headers empty in the stateless path so GetHeaders stays authoritative.
        return new global::Supabase.Postgrest.Client(
            snapshot.Urls.RestUrl,
            new global::Supabase.Postgrest.ClientOptions())
        {
            GetHeaders = () => CreateHeaders(authorization)
        };
    }

    internal global::Supabase.Storage.Client CreateStorage(
        LifecycleSnapshot snapshot,
        StatelessChildAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(authorization);

        // Storage merges runtime headers behind constructor headers, so delegated auth must be fixed at construction time.
        return new global::Supabase.Storage.Client(
            snapshot.Urls.StorageUrl,
            headers: CreateStorageHeaders(authorization));
    }

    internal global::Supabase.Functions.Client CreateFunctions(
        LifecycleSnapshot snapshot,
        StatelessChildAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(authorization);

        // The stateless server path binds identity at client creation time, not through per-invoke token parameters.
        return new global::Supabase.Functions.Client(snapshot.Urls.FunctionsUrl)
        {
            GetHeaders = () => CreateHeaders(authorization)
        };
    }

    private static Dictionary<string, string> CreateHeaders(StatelessChildAuthorization authorization)
    {
        var headers = SupabaseChildClientFactory.CreateStaticHeaders(authorization.ApiKey);
        if (!string.IsNullOrWhiteSpace(authorization.BearerToken))
        {
            headers["Authorization"] = $"Bearer {authorization.BearerToken}";
        }

        return headers;
    }

    private static Dictionary<string, string> CreateStorageHeaders(StatelessChildAuthorization authorization)
    {
        if (string.IsNullOrWhiteSpace(authorization.BearerToken))
        {
            return SupabaseChildClientFactory.CreateStorageHeaders(authorization.ApiKey);
        }

        return SupabaseChildClientFactory.CreateStorageHeaders(
            authorization.ApiKey,
            new Dictionary<string, string>
            {
                ["apikey"] = authorization.ApiKey,
                ["Authorization"] = $"Bearer {authorization.BearerToken}"
            });
    }
}
