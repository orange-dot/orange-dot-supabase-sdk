using System;
using System.Collections.Generic;
using System.Threading;

namespace OrangeDot.Supabase.Internal;

internal sealed class SupabaseChildClientFactory
{
    internal SupabaseChildClients Create(LifecycleSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        return Create(snapshot, NoOpSupabaseSessionStore.Instance, cancellationToken);
    }

    internal SupabaseChildClients Create(
        LifecycleSnapshot snapshot,
        ISupabaseSessionStore sessionStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(sessionStore);

        cancellationToken.ThrowIfCancellationRequested();
        var staticHeaders = CreateStaticHeaders(snapshot.AnonKey);
        var dynamicAuthHeaders = new DynamicAuthHeaders(snapshot.AnonKey);

        cancellationToken.ThrowIfCancellationRequested();
        var auth = new PersistedGotrueClient(new global::Supabase.Gotrue.ClientOptions
        {
            Url = snapshot.Urls.AuthUrl,
            Headers = new Dictionary<string, string>(staticHeaders)
        }, sessionStore)
        {
            GetHeaders = dynamicAuthHeaders.Build
        };

        cancellationToken.ThrowIfCancellationRequested();
        var postgrest = new global::Supabase.Postgrest.Client(
            snapshot.Urls.RestUrl,
            new global::Supabase.Postgrest.ClientOptions())
        {
            GetHeaders = dynamicAuthHeaders.Build
        };

        cancellationToken.ThrowIfCancellationRequested();
        var realtimeOptions = new global::Supabase.Realtime.ClientOptions();
        realtimeOptions.Headers.Add("apikey", snapshot.AnonKey);
        realtimeOptions.Parameters.ApiKey = snapshot.AnonKey;

        var realtime = new global::Supabase.Realtime.Client(snapshot.Urls.RealtimeUrl, realtimeOptions)
        {
            GetHeaders = dynamicAuthHeaders.Build
        };

        cancellationToken.ThrowIfCancellationRequested();
        var storage = new global::Supabase.Storage.Client(
            snapshot.Urls.StorageUrl,
            headers: new Dictionary<string, string>(staticHeaders))
        {
            GetHeaders = () => CreateStorageHeaders(snapshot.AnonKey, dynamicAuthHeaders.Build())
        };

        cancellationToken.ThrowIfCancellationRequested();
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

    internal static Dictionary<string, string> CreateStorageHeaders(
        string apiKey,
        Dictionary<string, string>? baseHeaders = null)
    {
        var headers = baseHeaders is null
            ? CreateStaticHeaders(apiKey)
            : new Dictionary<string, string>(baseHeaders);

        headers.TryAdd("Authorization", $"Bearer {apiKey}");

        return headers;
    }
}
