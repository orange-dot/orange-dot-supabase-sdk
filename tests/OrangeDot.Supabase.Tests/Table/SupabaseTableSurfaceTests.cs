using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace OrangeDot.Supabase.Tests.Table;

public sealed class SupabaseTableSurfaceTests
{
    [Fact]
    public void Client_surface_exposes_table_wrapper_types()
    {
        var interfaceMethod = typeof(ISupabaseClient).GetMethod(nameof(ISupabaseClient.Table))!;
        var clientMethod = typeof(SupabaseClient).GetMethod(nameof(SupabaseClient.Table))!;

        Assert.Equal(typeof(ISupabaseTable<>), interfaceMethod.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(typeof(SupabaseTable<>), clientMethod.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void Table_method_carries_expected_generic_constraints()
    {
        var interfaceArg = typeof(ISupabaseClient).GetMethod(nameof(ISupabaseClient.Table))!.GetGenericArguments().Single();
        var clientArg = typeof(SupabaseClient).GetMethod(nameof(SupabaseClient.Table))!.GetGenericArguments().Single();

        Assert.True(interfaceArg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint));
        Assert.True(clientArg.GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint));
        Assert.Contains(typeof(global::Supabase.Postgrest.Models.BaseModel), interfaceArg.GetGenericParameterConstraints());
        Assert.Contains(typeof(global::Supabase.Postgrest.Models.BaseModel), clientArg.GetGenericParameterConstraints());
    }

    [Fact]
    public void Supabase_table_has_no_public_constructor()
    {
        Assert.Empty(typeof(SupabaseTable<>)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .ToArray());
    }

    [Fact]
    public void Supabase_table_interface_keeps_fluent_chaining_on_wrapper_interface()
    {
        var whereMethod = typeof(ISupabaseTable<SurfaceModel>).GetMethod(nameof(ISupabaseTable<SurfaceModel>.Where))!;
        var selectMethod = typeof(ISupabaseTable<SurfaceModel>).GetMethod(nameof(ISupabaseTable<SurfaceModel>.Select), new[] { typeof(string) })!;

        Assert.Equal(typeof(ISupabaseTable<SurfaceModel>), whereMethod.ReturnType);
        Assert.Equal(typeof(ISupabaseTable<SurfaceModel>), selectMethod.ReturnType);
    }

    [Fact]
    public void Stateless_client_exposes_neither_table_nor_from()
    {
        Assert.Null(typeof(SupabaseStatelessClient).GetMethod(nameof(ISupabaseClient.Table)));
        Assert.Null(typeof(SupabaseStatelessClient).GetMethod("From"));
    }

    private sealed class SurfaceModel : global::Supabase.Postgrest.Models.BaseModel
    {
    }
}
