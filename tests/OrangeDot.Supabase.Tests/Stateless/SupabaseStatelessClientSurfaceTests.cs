using System.Linq;
using System.Reflection;
using Xunit;

namespace OrangeDot.Supabase.Tests.Stateless;

public sealed class SupabaseStatelessClientSurfaceTests
{
    [Fact]
    public void Supabase_stateless_client_has_no_public_constructor()
    {
        Assert.Empty(typeof(SupabaseStatelessClient)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .ToArray());
    }

    [Fact]
    public void Supabase_stateless_client_exposes_expected_public_surface_without_realtime()
    {
        Assert.Equal(
            typeof(global::Supabase.Gotrue.Interfaces.IGotrueStatelessClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session>),
            typeof(SupabaseStatelessClient).GetProperty(nameof(SupabaseStatelessClient.Auth))!.PropertyType);
        Assert.Equal(
            typeof(global::Supabase.Gotrue.StatelessClient.StatelessClientOptions),
            typeof(SupabaseStatelessClient).GetProperty(nameof(SupabaseStatelessClient.AuthOptions))!.PropertyType);
        Assert.Equal(
            typeof(global::Supabase.Postgrest.Interfaces.IPostgrestClient),
            typeof(SupabaseStatelessClient).GetProperty(nameof(SupabaseStatelessClient.Postgrest))!.PropertyType);
        Assert.Equal(
            typeof(global::Supabase.Storage.Interfaces.IStorageClient<global::Supabase.Storage.Bucket, global::Supabase.Storage.FileObject>),
            typeof(SupabaseStatelessClient).GetProperty(nameof(SupabaseStatelessClient.Storage))!.PropertyType);
        Assert.Equal(
            typeof(global::Supabase.Functions.Interfaces.IFunctionsClient),
            typeof(SupabaseStatelessClient).GetProperty(nameof(SupabaseStatelessClient.Functions))!.PropertyType);
        Assert.Null(typeof(SupabaseStatelessClient).GetProperty("Realtime"));
    }
}
