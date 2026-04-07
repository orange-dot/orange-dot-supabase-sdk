using System.Threading.Tasks;

namespace OrangeDot.Supabase.IntegrationTests;

[Collection(LocalSupabaseStatefulCollection.Name)]
public sealed class PostgrestCrudIntegrationTests
{
    private readonly LocalSupabaseStatefulFixture _fixture;

    public PostgrestCrudIntegrationTests(LocalSupabaseStatefulFixture fixture)
    {
        _fixture = fixture;
    }

    [LocalSupabaseFact]
    public async Task Table_wrapper_can_insert_read_update_and_delete()
    {
        var ownerTag = IntegrationTestEnvironment.NewOwnerTag("crud");
        var createdDetails = $"created-{ownerTag}";
        var updatedDetails = $"updated-{ownerTag}";

        try
        {
            var insertedResponse = await _fixture.Client.Table<IntegrationTodo>().Insert(new IntegrationTodo
            {
                Details = createdDetails,
                OwnerTag = ownerTag
            });

            var inserted = Assert.Single(insertedResponse.Models);

            Assert.False(string.IsNullOrWhiteSpace(inserted.Id));
            Assert.Equal(createdDetails, inserted.Details);
            Assert.Equal(ownerTag, inserted.OwnerTag);

            var readResponse = await _fixture.Client.Table<IntegrationTodo>()
                .Filter("owner_tag", global::Supabase.Postgrest.Constants.Operator.Equals, ownerTag)
                .Get();

            var readModel = Assert.Single(readResponse.Models);
            Assert.Equal(inserted.Id, readModel.Id);

            var updatedResponse = await _fixture.Client.Table<IntegrationTodo>()
                .Set(todo => todo.Details!, updatedDetails)
                .Match(inserted)
                .Update();

            var updated = Assert.Single(updatedResponse.Models);
            Assert.Equal(updatedDetails, updated.Details);
            Assert.Equal(ownerTag, updated.OwnerTag);

            await _fixture.Client.Table<IntegrationTodo>().Delete(updated);

            var afterDelete = await _fixture.Client.Table<IntegrationTodo>()
                .Filter("owner_tag", global::Supabase.Postgrest.Constants.Operator.Equals, ownerTag)
                .Get();

            Assert.Empty(afterDelete.Models);
        }
        finally
        {
            await IntegrationTestEnvironment.CleanupByOwnerTagAsync(_fixture.Client.Postgrest, ownerTag);
        }
    }
}
