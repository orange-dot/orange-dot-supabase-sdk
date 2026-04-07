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
        Assert.Equal("Bearer anon-key", children.Auth.GetHeaders!()["Authorization"]);

        Assert.Equal(urls.RestUrl, children.Postgrest.BaseUrl);
        Assert.Equal("anon-key", children.Postgrest.Options.Headers["apikey"]);
        Assert.Equal("Bearer anon-key", children.Postgrest.GetHeaders!()["Authorization"]);

        Assert.Equal(urls.RealtimeUrl, ReadPrivateStringField(children.Realtime, "_realtimeUrl"));
        Assert.Equal("anon-key", children.Realtime.Options.Headers["apikey"]);
        Assert.Equal("Bearer anon-key", children.Realtime.GetHeaders!()["Authorization"]);

        Assert.Equal(urls.StorageUrl, ReadPublicOrNonPublicStringProperty(children.Storage, "Url"));
        Assert.Equal("anon-key", children.Storage.Headers["apikey"]);
        Assert.Equal("Bearer anon-key", children.Storage.Headers["Authorization"]);

        Assert.Equal(urls.FunctionsUrl, ReadPrivateStringField(children.Functions, "_baseUrl"));
        Assert.Equal("Bearer anon-key", children.Functions.GetHeaders!()["Authorization"]);
    }

    [Fact]
    public void Factory_uses_static_apikey_headers_without_freezing_future_authorization_override()
    {
        var staticHeaders = SupabaseChildClientFactory.CreateStaticHeaders("anon-key");
        var dynamicHeaders = SupabaseChildClientFactory.CreateHeadersAccessor("anon-key", "session-token")();

        Assert.Equal("anon-key", staticHeaders["apikey"]);
        Assert.DoesNotContain("Authorization", staticHeaders.Keys);
        Assert.Equal("anon-key", dynamicHeaders["apikey"]);
        Assert.Equal("Bearer session-token", dynamicHeaders["Authorization"]);
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
