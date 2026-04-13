using System;
using System.Reflection;
using OrangeDot.Supabase.Errors;
using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase.Tests.Configuration;

public sealed class SupabaseKeyResolverTests
{
    [Fact]
    public void Resolve_project_key_prefers_publishable_key()
    {
        var resolved = SupabaseKeyResolver.ResolveProjectKey("publishable-key", null, "TestOperation");

        Assert.Equal("publishable-key", resolved);
    }

    [Fact]
    public void Resolve_project_key_accepts_legacy_alias()
    {
        var resolved = SupabaseKeyResolver.ResolveProjectKey(null, "legacy-anon-key", "TestOperation");

        Assert.Equal("legacy-anon-key", resolved);
    }

    [Fact]
    public void Resolve_project_key_accepts_matching_alias_pair()
    {
        var resolved = SupabaseKeyResolver.ResolveProjectKey("same-key", "same-key", "TestOperation");

        Assert.Equal("same-key", resolved);
    }

    [Fact]
    public void Resolve_project_key_rejects_conflicting_alias_pair()
    {
        var exception = Assert.Throws<SupabaseConfigurationException>(() => SupabaseKeyResolver.ResolveProjectKey(
            "publishable-key",
            "legacy-anon-key",
            "TestOperation"));

        Assert.Equal(SupabaseErrorCode.ConfigurationInvalid, exception.ErrorCode);
        Assert.Equal("TestOperation", exception.Operation);
    }

    [Fact]
    public void Resolve_project_key_rejects_missing_values()
    {
        var exception = Assert.Throws<SupabaseConfigurationException>(() => SupabaseKeyResolver.ResolveProjectKey(
            null,
            null,
            "TestOperation"));

        Assert.Equal(SupabaseErrorCode.ConfigurationMissing, exception.ErrorCode);
        Assert.Equal("TestOperation", exception.Operation);
    }

    [Fact]
    public void Resolve_privileged_key_prefers_secret_key()
    {
        var resolved = SupabaseKeyResolver.ResolvePrivilegedKey("secret-key", null, "TestOperation");

        Assert.Equal("secret-key", resolved);
    }

    [Fact]
    public void Resolve_privileged_key_accepts_matching_alias_pair()
    {
        var resolved = SupabaseKeyResolver.ResolvePrivilegedKey("same-key", "same-key", "TestOperation");

        Assert.Equal("same-key", resolved);
    }

    [Fact]
    public void Resolve_privileged_key_rejects_conflicting_alias_pair()
    {
        var exception = Assert.Throws<SupabaseConfigurationException>(() => SupabaseKeyResolver.ResolvePrivilegedKey(
            "secret-key",
            "legacy-service-role-key",
            "TestOperation"));

        Assert.Equal(SupabaseErrorCode.ConfigurationInvalid, exception.ErrorCode);
        Assert.Equal("TestOperation", exception.Operation);
    }

    [Fact]
    public void Legacy_key_properties_are_marked_obsolete_with_fixed_messages()
    {
        var optionsAnon = typeof(SupabaseOptions).GetProperty("AnonKey")!;
        var serverAnon = typeof(SupabaseServerOptions).GetProperty("AnonKey")!;
        var serviceRole = typeof(SupabaseServerOptions).GetProperty("ServiceRoleKey")!;

        Assert.Equal(
            "Use PublishableKey instead. AnonKey will be removed in a future major version.",
            optionsAnon.GetCustomAttribute<ObsoleteAttribute>()!.Message);
        Assert.Equal(
            "Use PublishableKey instead. AnonKey will be removed in a future major version.",
            serverAnon.GetCustomAttribute<ObsoleteAttribute>()!.Message);
        Assert.Equal(
            "Use SecretKey instead. ServiceRoleKey will be removed in a future major version.",
            serviceRole.GetCustomAttribute<ObsoleteAttribute>()!.Message);
    }
}
