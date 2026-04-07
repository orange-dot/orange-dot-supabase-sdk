using System;
using System.Reflection;
using OrangeDot.Supabase.Errors;
using Xunit;

namespace OrangeDot.Supabase.Tests.Stateless;

public sealed class SupabaseStatelessClientTests
{
    [Fact]
    public void Create_throws_for_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => SupabaseStatelessClient.Create(null!));
    }

    [Fact]
    public void Create_throws_configuration_missing_for_missing_url()
    {
        var options = new SupabaseOptions
        {
            AnonKey = "anon-key"
        };

        var exception = Assert.Throws<SupabaseConfigurationException>(() => SupabaseStatelessClient.Create(options));

        Assert.Equal(SupabaseErrorCode.ConfigurationMissing, exception.ErrorCode);
        Assert.Equal("Create", exception.Operation);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Create_throws_configuration_missing_for_missing_anon_key()
    {
        var options = new SupabaseOptions
        {
            Url = "https://abc.supabase.co"
        };

        var exception = Assert.Throws<SupabaseConfigurationException>(() => SupabaseStatelessClient.Create(options));

        Assert.Equal(SupabaseErrorCode.ConfigurationMissing, exception.ErrorCode);
        Assert.Equal("Create", exception.Operation);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Create_throws_configuration_invalid_for_invalid_url()
    {
        var options = new SupabaseOptions
        {
            Url = "not a url",
            AnonKey = "anon-key"
        };

        var exception = Assert.Throws<SupabaseConfigurationException>(() => SupabaseStatelessClient.Create(options));

        Assert.Equal(SupabaseErrorCode.ConfigurationInvalid, exception.ErrorCode);
        Assert.Equal("Create", exception.Operation);
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void Create_throws_configuration_invalid_for_unsupported_url_scheme()
    {
        var options = new SupabaseOptions
        {
            Url = "ftp://example.com",
            AnonKey = "anon-key"
        };

        var exception = Assert.Throws<SupabaseConfigurationException>(() => SupabaseStatelessClient.Create(options));

        Assert.Equal(SupabaseErrorCode.ConfigurationInvalid, exception.ErrorCode);
        Assert.Equal("Create", exception.Operation);
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void Happy_path_preserves_snapshotted_values_and_derived_urls_for_hosted_url()
    {
        var client = SupabaseStatelessClient.Create(new SupabaseOptions
        {
            Url = "https://abc.supabase.co/",
            AnonKey = "anon-key"
        });

        Assert.Equal("https://abc.supabase.co", client.Url);
        Assert.Equal("anon-key", client.AnonKey);
        Assert.Equal("https://abc.supabase.co", client.Urls.NormalizedBaseUrl);
        Assert.Equal("https://abc.supabase.co/auth/v1", client.Urls.AuthUrl);
        Assert.Equal("https://abc.supabase.co/rest/v1", client.Urls.RestUrl);
        Assert.Equal("https://abc.supabase.co/storage/v1", client.Urls.StorageUrl);
        Assert.Equal("https://abc.supabase.co/functions/v1", client.Urls.FunctionsUrl);
    }

    [Fact]
    public void Create_uses_shared_url_derivation_for_self_hosted_url()
    {
        var client = SupabaseStatelessClient.Create(new SupabaseOptions
        {
            Url = "http://localhost:54321",
            AnonKey = "anon-key"
        });

        Assert.Equal("http://localhost:54321", client.Url);
        Assert.Equal("http://localhost:54321/auth/v1", client.Urls.AuthUrl);
        Assert.Equal("http://localhost:54321/rest/v1", client.Urls.RestUrl);
        Assert.Equal("ws://localhost:54321/realtime/v1", client.Urls.RealtimeUrl);
        Assert.Equal("http://localhost:54321/storage/v1", client.Urls.StorageUrl);
        Assert.Equal("http://localhost:54321/functions/v1", client.Urls.FunctionsUrl);
    }

    [Fact]
    public void Create_snapshots_values_and_ignores_later_mutation_of_original_options()
    {
        var options = new SupabaseOptions
        {
            Url = "https://abc.supabase.co/",
            AnonKey = "initial-anon-key"
        };

        var client = SupabaseStatelessClient.Create(options);

        options.Url = "https://mutated.supabase.co";
        options.AnonKey = "mutated-anon-key";

        Assert.Equal("https://abc.supabase.co", client.Url);
        Assert.Equal("initial-anon-key", client.AnonKey);
        Assert.Equal("https://abc.supabase.co", client.Urls.NormalizedBaseUrl);
    }

    [Fact]
    public void Auth_options_are_prefilled_with_auth_url_and_anon_headers()
    {
        var client = SupabaseStatelessClient.Create(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key"
        });

        Assert.Equal(client.Urls.AuthUrl, client.AuthOptions.Url);
        Assert.Equal("anon-key", client.AuthOptions.Headers["apikey"]);
        Assert.DoesNotContain("Authorization", client.AuthOptions.Headers.Keys);
    }

    [Fact]
    public void Stateless_children_are_created_against_derived_endpoints_and_default_to_anon_headers()
    {
        var client = SupabaseStatelessClient.Create(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key"
        });

        var postgrest = Assert.IsType<global::Supabase.Postgrest.Client>(client.Postgrest);
        var storage = Assert.IsType<global::Supabase.Storage.Client>(client.Storage);
        var functions = Assert.IsType<global::Supabase.Functions.Client>(client.Functions);

        Assert.IsType<global::Supabase.Gotrue.StatelessClient>(client.Auth);
        Assert.Equal(client.Urls.RestUrl, postgrest.BaseUrl);
        Assert.Equal("anon-key", postgrest.Options.Headers["apikey"]);
        Assert.DoesNotContain("Authorization", postgrest.Options.Headers.Keys);

        Assert.Equal(client.Urls.StorageUrl, ReadPublicOrNonPublicStringProperty(storage, "Url"));
        Assert.Equal("anon-key", storage.Headers["apikey"]);
        Assert.DoesNotContain("Authorization", storage.Headers.Keys);

        Assert.Equal(client.Urls.FunctionsUrl, ReadPrivateStringField(functions, "_baseUrl"));
        Assert.Equal("anon-key", functions.GetHeaders!()["apikey"]);
        Assert.DoesNotContain("Authorization", functions.GetHeaders!().Keys);
    }

    [Fact]
    public void Stateless_functions_get_headers_returns_fresh_dictionaries()
    {
        var client = SupabaseStatelessClient.Create(new SupabaseOptions
        {
            Url = "https://abc.supabase.co",
            AnonKey = "anon-key"
        });

        var first = client.Functions.GetHeaders!();
        first["Authorization"] = "Bearer mutated";
        first["Mutated"] = "yes";

        var second = client.Functions.GetHeaders!();

        Assert.Equal("anon-key", first["apikey"]);
        Assert.Equal("anon-key", second["apikey"]);
        Assert.DoesNotContain("Authorization", second.Keys);
        Assert.DoesNotContain("Mutated", second.Keys);
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
