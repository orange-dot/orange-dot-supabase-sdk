using System.Threading.Tasks;
using OrangeDot.Supabase;

namespace OrangeDot.Supabase.IntegrationTests;

public sealed class StorageIntegrationSmokeTests
{
    [LocalSupabaseFact]
    public async Task Stateless_client_can_list_objects_in_configured_integration_bucket()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();

        await IntegrationTestEnvironment.EnsureOptInAndStorageReachableAsync(settings);

        var client = SupabaseStatelessClient.Create(new SupabaseOptions
        {
            Url = settings.Url,
            AnonKey = settings.AnonKey
        });

        var files = await client.Storage.From(IntegrationTestEnvironment.IntegrationBucketName).List();

        Assert.NotNull(files);
        Assert.Empty(files);
    }
}
