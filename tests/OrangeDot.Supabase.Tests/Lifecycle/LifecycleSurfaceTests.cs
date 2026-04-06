using System.Linq;
using System.Reflection;
using Xunit;

namespace OrangeDot.Supabase.Tests.Lifecycle;

public sealed class LifecycleSurfaceTests
{
    [Fact]
    public void Configured_client_exposes_load_persisted_session_async_but_not_initialize_async()
    {
        Assert.NotNull(typeof(ConfiguredClient).GetMethod(nameof(ConfiguredClient.LoadPersistedSessionAsync)));
        Assert.Null(typeof(ConfiguredClient).GetMethod("InitializeAsync"));
    }

    [Fact]
    public void Hydrated_client_exposes_initialize_async_but_not_load_persisted_session_async()
    {
        Assert.NotNull(typeof(HydratedClient).GetMethod(nameof(HydratedClient.InitializeAsync)));
        Assert.Null(typeof(HydratedClient).GetMethod(nameof(ConfiguredClient.LoadPersistedSessionAsync)));
    }

    [Fact]
    public void Supabase_client_does_not_expose_load_persisted_session_async()
    {
        Assert.NotNull(typeof(SupabaseClient).GetMethod(nameof(SupabaseClient.Configure), BindingFlags.Public | BindingFlags.Static));
        Assert.Null(typeof(SupabaseClient).GetMethod(nameof(ConfiguredClient.LoadPersistedSessionAsync)));
    }

    [Fact]
    public void Supabase_client_has_no_public_constructor()
    {
        Assert.Empty(typeof(SupabaseClient)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .ToArray());
    }
}
