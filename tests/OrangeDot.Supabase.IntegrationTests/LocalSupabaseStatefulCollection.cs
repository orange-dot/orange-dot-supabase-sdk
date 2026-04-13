using Xunit;

namespace OrangeDot.Supabase.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class LocalSupabaseStatefulCollection : ICollectionFixture<LocalSupabaseStatefulFixture>
{
    public const string Name = "Local Supabase Stateful";
}
