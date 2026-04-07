using System;
using System.Collections.Generic;

namespace OrangeDot.Supabase.Internal;

internal sealed class SupabaseChildClientFactory
{
    internal SupabaseChildClients Create(LifecycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var staticHeaders = CreateStaticHeaders(snapshot.AnonKey);
        var dynamicHeaders = CreateHeadersAccessor(snapshot.AnonKey, snapshot.AnonKey);

        var auth = new global::Supabase.Gotrue.Client(new global::Supabase.Gotrue.ClientOptions
        {
            Url = snapshot.Urls.AuthUrl,
            Headers = new Dictionary<string, string>(staticHeaders)
        })
        {
            GetHeaders = dynamicHeaders
        };

        var postgrest = new global::Supabase.Postgrest.Client(
            snapshot.Urls.RestUrl,
            new global::Supabase.Postgrest.ClientOptions
            {
                Headers = new Dictionary<string, string>(staticHeaders)
            })
        {
            GetHeaders = dynamicHeaders
        };

        var realtimeOptions = new global::Supabase.Realtime.ClientOptions();
        realtimeOptions.Headers.Add("apikey", snapshot.AnonKey);

        var realtime = new global::Supabase.Realtime.Client(snapshot.Urls.RealtimeUrl, realtimeOptions)
        {
            GetHeaders = dynamicHeaders
        };

        var storage = new global::Supabase.Storage.Client(
            snapshot.Urls.StorageUrl,
            headers: new Dictionary<string, string>(staticHeaders))
        {
            GetHeaders = dynamicHeaders
        };

        var functions = new global::Supabase.Functions.Client(snapshot.Urls.FunctionsUrl)
        {
            GetHeaders = dynamicHeaders
        };

        return new SupabaseChildClients(auth, postgrest, realtime, storage, functions);
    }

    internal static Dictionary<string, string> CreateStaticHeaders(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return new Dictionary<string, string>
        {
            ["apikey"] = apiKey
        };
    }

    internal static Func<Dictionary<string, string>> CreateHeadersAccessor(string apiKey, string bearerToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(bearerToken);

        return () => new Dictionary<string, string>
        {
            ["apikey"] = apiKey,
            ["Authorization"] = $"Bearer {bearerToken}"
        };
    }
}
