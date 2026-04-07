using System;

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

    internal global::Supabase.Postgrest.Client CreatePostgrest(LifecycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new global::Supabase.Postgrest.Client(
            snapshot.Urls.RestUrl,
            new global::Supabase.Postgrest.ClientOptions
            {
                Headers = SupabaseChildClientFactory.CreateStaticHeaders(snapshot.AnonKey)
            });
    }

    internal global::Supabase.Storage.Client CreateStorage(LifecycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new global::Supabase.Storage.Client(
            snapshot.Urls.StorageUrl,
            headers: SupabaseChildClientFactory.CreateStaticHeaders(snapshot.AnonKey));
    }

    internal global::Supabase.Functions.Client CreateFunctions(LifecycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new global::Supabase.Functions.Client(snapshot.Urls.FunctionsUrl)
        {
            GetHeaders = () => SupabaseChildClientFactory.CreateStaticHeaders(snapshot.AnonKey)
        };
    }
}
