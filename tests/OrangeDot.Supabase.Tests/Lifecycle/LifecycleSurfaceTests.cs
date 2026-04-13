using System;
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

    [Fact]
    public void Client_surface_exposes_child_clients_using_upstream_interfaces()
    {
        Assert.Equal(
            typeof(global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session>),
            typeof(ISupabaseClient).GetProperty(nameof(ISupabaseClient.Auth))!.PropertyType);
        Assert.Equal(
            typeof(global::Supabase.Postgrest.Interfaces.IPostgrestClient),
            typeof(ISupabaseClient).GetProperty(nameof(ISupabaseClient.Postgrest))!.PropertyType);
        Assert.Equal(
            typeof(global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel>),
            typeof(ISupabaseClient).GetProperty(nameof(ISupabaseClient.Realtime))!.PropertyType);
        Assert.Equal(
            typeof(global::Supabase.Storage.Interfaces.IStorageClient<global::Supabase.Storage.Bucket, global::Supabase.Storage.FileObject>),
            typeof(ISupabaseClient).GetProperty(nameof(ISupabaseClient.Storage))!.PropertyType);
        Assert.Equal(
            typeof(global::Supabase.Functions.Interfaces.IFunctionsClient),
            typeof(ISupabaseClient).GetProperty(nameof(ISupabaseClient.Functions))!.PropertyType);
        Assert.Equal(
            typeof(global::Supabase.Postgrest.Interfaces.IPostgrestClient),
            typeof(SupabaseClient).GetProperty(nameof(SupabaseClient.Postgrest))!.PropertyType);
    }

    [Fact]
    public void Lifecycle_surface_exposes_disposal_across_public_phases()
    {
        Assert.Contains(typeof(IDisposable), typeof(ConfiguredClient).GetInterfaces());
        Assert.Contains(typeof(IDisposable), typeof(HydratedClient).GetInterfaces());
        Assert.Contains(typeof(IDisposable), typeof(SupabaseClient).GetInterfaces());
        Assert.Contains(typeof(IDisposable), typeof(ISupabaseClient).GetInterfaces());
    }
}
