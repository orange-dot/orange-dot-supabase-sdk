using System;
using System.Reflection;
using OrangeDot.Supabase.Internal;
using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase.Tests.Submodules;

public sealed class SupabaseChildClientFactoryTests
{
    [Fact]
    public void Factory_creates_real_child_clients_from_snapshotted_urls_and_anon_key()
    {
        var urls = SupabaseUrls.FromBaseUrl("https://abc.supabase.co");
        var snapshot = new LifecycleSnapshot(urls.NormalizedBaseUrl, "anon-key", urls);
        var factory = new SupabaseChildClientFactory();

        var children = factory.Create(snapshot);

        Assert.Equal(urls.AuthUrl, children.Auth.Options.Url);
        Assert.Equal("anon-key", children.Auth.Options.Headers["apikey"]);
        Assert.DoesNotContain("Authorization", children.Auth.GetHeaders!().Keys);

        Assert.Equal(urls.RestUrl, children.Postgrest.BaseUrl);
        Assert.Empty(children.Postgrest.Options.Headers);
        Assert.NotNull(children.Postgrest.GetHeaders);

        Assert.Equal(urls.RealtimeUrl, ReadPrivateStringField(children.Realtime, "_realtimeUrl"));
        Assert.Equal("anon-key", children.Realtime.Options.Headers["apikey"]);
        Assert.Equal("anon-key", children.Realtime.Options.Parameters.ApiKey);
        Assert.DoesNotContain("Authorization", children.Realtime.GetHeaders!().Keys);

        Assert.Equal(urls.StorageUrl, ReadPublicOrNonPublicStringProperty(children.Storage, "Url"));
        Assert.Equal("anon-key", children.Storage.Headers["apikey"]);
        Assert.DoesNotContain("Authorization", children.Storage.Headers.Keys);

        Assert.Equal(urls.FunctionsUrl, ReadPrivateStringField(children.Functions, "_baseUrl"));
        Assert.DoesNotContain("Authorization", children.Functions.GetHeaders!().Keys);

        children.DynamicAuthHeaders.SetAccessToken("session-token");

        Assert.Equal("Bearer session-token", children.Auth.GetHeaders!()["Authorization"]);
        Assert.Equal("Bearer session-token", children.Postgrest.GetHeaders!()["Authorization"]);
        Assert.Equal("Bearer session-token", children.Realtime.GetHeaders!()["Authorization"]);
        Assert.Equal("Bearer session-token", children.Storage.Headers["Authorization"]);
        Assert.Equal("Bearer session-token", children.Functions.GetHeaders!()["Authorization"]);
    }

    [Fact]
    public void Dynamic_auth_headers_return_fresh_dictionaries_and_can_clear_authorization()
    {
        var dynamicHeaders = new DynamicAuthHeaders("anon-key");
        var first = dynamicHeaders.Build();

        first["Authorization"] = "Bearer mutated";

        var second = dynamicHeaders.Build();
        dynamicHeaders.SetAccessToken("session-token");
        var authenticated = dynamicHeaders.Build();
        dynamicHeaders.ClearAccessToken();
        var cleared = dynamicHeaders.Build();

        Assert.Equal("anon-key", first["apikey"]);
        Assert.Equal("anon-key", second["apikey"]);
        Assert.DoesNotContain("Authorization", second.Keys);
        Assert.Equal("Bearer session-token", authenticated["Authorization"]);
        Assert.DoesNotContain("Authorization", cleared.Keys);
    }

    [Fact]
    public void Dynamic_auth_headers_snapshot_transitions_are_consistent()
    {
        var dynamicHeaders = new DynamicAuthHeaders("anon-key");

        var anonymous = dynamicHeaders.Build();
        Assert.Single(anonymous);
        Assert.Equal("anon-key", anonymous["apikey"]);

        dynamicHeaders.SetAccessToken("token-a");
        var firstAuth = dynamicHeaders.Build();
        Assert.Equal(2, firstAuth.Count);
        Assert.Equal("Bearer token-a", firstAuth["Authorization"]);

        dynamicHeaders.SetAccessToken("token-b");
        var secondAuth = dynamicHeaders.Build();
        Assert.Equal("Bearer token-b", secondAuth["Authorization"]);
        Assert.Equal("Bearer token-a", firstAuth["Authorization"]);

        dynamicHeaders.ClearAccessToken();
        var afterClear = dynamicHeaders.Build();
        Assert.Single(afterClear);
        Assert.DoesNotContain("Authorization", afterClear.Keys);
        Assert.Equal("Bearer token-b", secondAuth["Authorization"]);
    }

    private static string ReadPrivateStringField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);

        return Assert.IsType<string>(field!.GetValue(instance));
    }

    private static string ReadPublicOrNonPublicStringProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(property);

        return Assert.IsType<string>(property!.GetValue(instance));
    }
}
