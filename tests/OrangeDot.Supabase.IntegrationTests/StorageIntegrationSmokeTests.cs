using System;
using System.Text;
using System.Threading.Tasks;
using OrangeDot.Supabase;

namespace OrangeDot.Supabase.IntegrationTests;

public sealed class StorageIntegrationSmokeTests
{
    private const string LifecycleObjectPrefix = "integration/pr17-storage";

    [LocalSupabaseFact]
    public async Task Stateless_client_can_list_objects_in_configured_integration_bucket()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();

        await IntegrationTestEnvironment.EnsureOptInAndStorageReachableAsync(settings);

        var client = SupabaseStatelessClient.Create(new SupabaseOptions
        {
            Url = settings.Url,
            PublishableKey = settings.AnonKey
        });

        var files = await client.Storage.From(IntegrationTestEnvironment.IntegrationBucketName).List();

        Assert.NotNull(files);
    }

    [LocalSupabaseFact]
    public async Task Stateless_client_can_upload_list_download_and_delete_objects_in_configured_integration_bucket()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();

        await IntegrationTestEnvironment.EnsureOptInAndStorageReachableAsync(settings);

        var client = SupabaseStatelessClient.Create(new SupabaseOptions
        {
            Url = settings.Url,
            PublishableKey = settings.AnonKey
        });

        var bucket = client.Storage.From(IntegrationTestEnvironment.IntegrationBucketName);
        var objectPath = IntegrationTestEnvironment.NewStorageObjectPath(LifecycleObjectPrefix, ".txt");
        var objectName = objectPath[(LifecycleObjectPrefix.Length + 1)..];
        var payloadText = $"storage-lifecycle-{Guid.NewGuid():N}";
        var payload = Encoding.UTF8.GetBytes(payloadText);

        try
        {
            var uploadedPath = await bucket.Upload(payload, objectPath);
            Assert.Equal($"{IntegrationTestEnvironment.IntegrationBucketName}/{objectPath}", uploadedPath);

            var filesAfterUpload = await bucket.List(LifecycleObjectPrefix);
            Assert.NotNull(filesAfterUpload);
            Assert.Contains(filesAfterUpload, file => string.Equals(file.Name, objectName, StringComparison.Ordinal));

            var downloaded = await bucket.Download(objectPath, transformOptions: null, onProgress: null);
            Assert.Equal(payload, downloaded);
            Assert.Equal(payloadText, Encoding.UTF8.GetString(downloaded));

            await bucket.Remove(objectPath);

            var filesAfterDelete = await bucket.List(LifecycleObjectPrefix);
            Assert.NotNull(filesAfterDelete);
            Assert.DoesNotContain(filesAfterDelete, file => string.Equals(file.Name, objectName, StringComparison.Ordinal));
        }
        finally
        {
            await IntegrationTestEnvironment.BestEffortRemoveStorageObjectAsync(bucket, objectPath);
        }
    }
}
