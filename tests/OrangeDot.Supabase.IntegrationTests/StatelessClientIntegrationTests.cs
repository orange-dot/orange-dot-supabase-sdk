using System.Threading.Tasks;
using OrangeDot.Supabase;

namespace OrangeDot.Supabase.IntegrationTests;

public sealed class StatelessClientIntegrationTests
{
    [LocalSupabaseFact]
    public async Task Stateless_client_can_execute_postgrest_smoke_roundtrip()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();

        await IntegrationTestEnvironment.EnsureOptInAndReachableAsync(settings);

        var client = SupabaseStatelessClient.Create(new SupabaseOptions
        {
            Url = settings.Url,
            AnonKey = settings.AnonKey
        });

        var ownerTag = IntegrationTestEnvironment.NewOwnerTag("stateless");
        var details = $"details-{ownerTag}";

        try
        {
            var insertResponse = await client.Postgrest.Table<IntegrationTodo>().Insert(new IntegrationTodo
            {
                Details = details,
                OwnerTag = ownerTag
            });

            var inserted = Assert.Single(insertResponse.Models);

            var readResponse = await client.Postgrest.Table<IntegrationTodo>()
                .Filter("owner_tag", global::Supabase.Postgrest.Constants.Operator.Equals, ownerTag)
                .Get();

            var readModel = Assert.Single(readResponse.Models);
            Assert.Equal(inserted.Id, readModel.Id);

            await client.Postgrest.Table<IntegrationTodo>().Delete(inserted);

            var afterDelete = await client.Postgrest.Table<IntegrationTodo>()
                .Filter("owner_tag", global::Supabase.Postgrest.Constants.Operator.Equals, ownerTag)
                .Get();

            Assert.Empty(afterDelete.Models);
        }
        finally
        {
            await IntegrationTestEnvironment.CleanupByOwnerTagAsync(client.Postgrest, ownerTag);
        }
    }
}
