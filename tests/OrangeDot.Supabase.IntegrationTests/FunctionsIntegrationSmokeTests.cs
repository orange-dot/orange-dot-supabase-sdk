using System.Collections.Generic;
using System.Threading.Tasks;
using OrangeDot.Supabase;

namespace OrangeDot.Supabase.IntegrationTests;

public sealed class FunctionsIntegrationSmokeTests
{
    [LocalSupabaseFunctionsFact]
    public async Task Stateless_client_can_invoke_local_integration_smoke_function()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();

        await IntegrationTestEnvironment.EnsureOptInAndFunctionsReachableAsync(settings);

        var client = SupabaseStatelessClient.Create(new SupabaseOptions
        {
            Url = settings.Url,
            AnonKey = settings.AnonKey
        });

        var result = await client.Functions.Invoke<IntegrationFunctionSmokeResponse>(
            IntegrationTestEnvironment.IntegrationSmokeFunctionName,
            options: new global::Supabase.Functions.Client.InvokeFunctionOptions
            {
                Body = new Dictionary<string, object>
                {
                    ["source"] = "integration-tests"
                }
            });

        Assert.NotNull(result);
        Assert.True(result!.Ok);
        Assert.Equal(IntegrationTestEnvironment.IntegrationSmokeFunctionName, result.Function);
        Assert.Equal("integration-tests", result.Source);
    }

    public sealed class IntegrationFunctionSmokeResponse
    {
        public required bool Ok { get; init; }

        public required string Function { get; init; }

        public required string Source { get; init; }
    }
}
