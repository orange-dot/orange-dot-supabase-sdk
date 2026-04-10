using System;
using Microsoft.Extensions.DependencyInjection;
using OrangeDot.Supabase.Errors;

namespace OrangeDot.Supabase.Tests.Stateless;

public sealed class SupabaseStatelessClientFactoryTests
{
    [Fact]
    public void Create_anon_returns_fresh_instances()
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();

        var first = factory.CreateAnon();
        var second = factory.CreateAnon();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Create_for_user_returns_fresh_instances()
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();

        var first = factory.CreateForUser("user-token");
        var second = factory.CreateForUser("user-token");

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Create_service_returns_fresh_instances()
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();

        var first = factory.CreateService();
        var second = factory.CreateService();

        Assert.NotSame(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_for_user_throws_for_blank_access_token(string? accessToken)
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();

        Assert.ThrowsAny<ArgumentException>(() => factory.CreateForUser(accessToken!));
    }

    [Fact]
    public void Create_service_throws_configuration_missing_when_service_role_key_is_not_configured()
    {
        using var provider = CreateProvider(configure: options => options.SecretKey = null);
        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();

        var exception = Assert.Throws<SupabaseConfigurationException>(() => factory.CreateService());

        Assert.Equal(SupabaseErrorCode.ConfigurationMissing, exception.ErrorCode);
        Assert.Equal(nameof(ISupabaseStatelessClientFactory.CreateService), exception.Operation);
    }

    [Fact]
    public void Factory_variants_share_urls_and_project_level_auth_options()
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();

        var anon = factory.CreateAnon();
        var user = factory.CreateForUser("user-token");
        var service = factory.CreateService();

        Assert.IsType<SupabaseStatelessClient>(anon);
        Assert.IsType<SupabaseStatelessClient>(user);
        Assert.IsType<SupabaseStatelessClient>(service);

        Assert.Equal(anon.Url, user.Url);
        Assert.Equal(user.Url, service.Url);
        Assert.Equal(anon.Urls.NormalizedBaseUrl, user.Urls.NormalizedBaseUrl);
        Assert.Equal(user.Urls.NormalizedBaseUrl, service.Urls.NormalizedBaseUrl);
        Assert.Equal("publishable-key", anon.AuthOptions.Headers["apikey"]);
        Assert.Equal("publishable-key", user.AuthOptions.Headers["apikey"]);
        Assert.Equal("publishable-key", service.AuthOptions.Headers["apikey"]);
        Assert.DoesNotContain("Authorization", anon.AuthOptions.Headers.Keys);
        Assert.DoesNotContain("Authorization", user.AuthOptions.Headers.Keys);
        Assert.DoesNotContain("Authorization", service.AuthOptions.Headers.Keys);
    }

    [Fact]
    public void Create_for_user_injects_access_token_into_postgrest_functions_and_storage()
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();

        var client = factory.CreateForUser("user-token");
        var postgrest = Assert.IsType<global::Supabase.Postgrest.Client>(client.Postgrest);
        var functions = Assert.IsType<global::Supabase.Functions.Client>(client.Functions);
        var storage = Assert.IsType<global::Supabase.Storage.Client>(client.Storage);

        var postgrestHeaders = postgrest.GetHeaders!();
        var functionsHeaders = functions.GetHeaders!();

        Assert.Empty(postgrest.Options.Headers);
        Assert.Equal("publishable-key", postgrestHeaders["apikey"]);
        Assert.Equal("Bearer user-token", postgrestHeaders["Authorization"]);
        Assert.Equal("publishable-key", functionsHeaders["apikey"]);
        Assert.Equal("Bearer user-token", functionsHeaders["Authorization"]);
        Assert.Equal("publishable-key", storage.Headers["apikey"]);
        Assert.Equal("Bearer user-token", storage.Headers["Authorization"]);
    }

    [Fact]
    public void Create_service_injects_service_role_token_into_postgrest_functions_and_storage()
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();

        var client = factory.CreateService();
        var postgrest = Assert.IsType<global::Supabase.Postgrest.Client>(client.Postgrest);
        var functions = Assert.IsType<global::Supabase.Functions.Client>(client.Functions);
        var storage = Assert.IsType<global::Supabase.Storage.Client>(client.Storage);

        Assert.Equal("Bearer secret-key", postgrest.GetHeaders!()["Authorization"]);
        Assert.Equal("Bearer secret-key", functions.GetHeaders!()["Authorization"]);
        Assert.Equal("Bearer secret-key", storage.Headers["Authorization"]);
    }

    [Fact]
    public void Create_for_user_storage_headers_preserve_delegated_authorization()
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();

        var client = factory.CreateForUser("user-token");
        var storage = Assert.IsType<global::Supabase.Storage.Client>(client.Storage);

        Assert.Equal("Bearer user-token", storage.Headers["Authorization"]);
    }

    [Fact]
    public void Functions_headers_return_fresh_dictionaries_for_server_created_clients()
    {
        using var provider = CreateProvider();
        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();

        var client = factory.CreateForUser("user-token");
        var functions = Assert.IsType<global::Supabase.Functions.Client>(client.Functions);

        var first = functions.GetHeaders!();
        first["Authorization"] = "Bearer mutated";
        first["Mutated"] = "yes";

        var second = functions.GetHeaders!();

        Assert.Equal("publishable-key", second["apikey"]);
        Assert.Equal("Bearer user-token", second["Authorization"]);
        Assert.DoesNotContain("Mutated", second.Keys);
    }

    [Fact]
    public void Create_service_accepts_legacy_service_role_key_alias()
    {
        using var provider = CreateProvider(configure: options =>
        {
            options.SecretKey = null;
#pragma warning disable CS0618
            options.ServiceRoleKey = "legacy-service-role-key";
#pragma warning restore CS0618
        });
        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();

        var client = factory.CreateService();
        var functions = Assert.IsType<global::Supabase.Functions.Client>(client.Functions);

        Assert.Equal("Bearer legacy-service-role-key", functions.GetHeaders!()["Authorization"]);
    }

    private static ServiceProvider CreateProvider(Action<SupabaseServerOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSupabaseServer(options =>
        {
            options.Url = "https://abc.supabase.co";
            options.PublishableKey = "publishable-key";
            options.SecretKey = "secret-key";
            configure?.Invoke(options);
        });

        return services.BuildServiceProvider();
    }
}
