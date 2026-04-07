using System;
using System.Collections.Generic;

namespace OrangeDot.Supabase.Internal;

internal sealed class SupabaseChildClientFactory
{
    internal SupabaseChildClients Create(LifecycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var staticHeaders = CreateStaticHeaders(snapshot.AnonKey);
        var dynamicAuthHeaders = new DynamicAuthHeaders(snapshot.AnonKey);

        var auth = new global::Supabase.Gotrue.Client(new global::Supabase.Gotrue.ClientOptions
        {
            Url = snapshot.Urls.AuthUrl,
            Headers = new Dictionary<string, string>(staticHeaders)
        })
        {
            GetHeaders = dynamicAuthHeaders.Build
        };

        var postgrest = new global::Supabase.Postgrest.Client(
            snapshot.Urls.RestUrl,
            new global::Supabase.Postgrest.ClientOptions
            {
                Headers = new Dictionary<string, string>(staticHeaders)
            })
        {
            GetHeaders = dynamicAuthHeaders.Build
        };

        var realtimeOptions = new global::Supabase.Realtime.ClientOptions();
        realtimeOptions.Headers.Add("apikey", snapshot.AnonKey);

        var realtime = new global::Supabase.Realtime.Client(snapshot.Urls.RealtimeUrl, realtimeOptions)
        {
            GetHeaders = dynamicAuthHeaders.Build
        };

        var storage = new global::Supabase.Storage.Client(
            snapshot.Urls.StorageUrl,
            headers: new Dictionary<string, string>(staticHeaders))
        {
            GetHeaders = dynamicAuthHeaders.Build
        };

        var functions = new global::Supabase.Functions.Client(snapshot.Urls.FunctionsUrl)
        {
            GetHeaders = dynamicAuthHeaders.Build
        };

        return new SupabaseChildClients(dynamicAuthHeaders, auth, postgrest, realtime, storage, functions);
    }

    internal static Dictionary<string, string> CreateStaticHeaders(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return new Dictionary<string, string>
        {
            ["apikey"] = apiKey
        };
    }
}
