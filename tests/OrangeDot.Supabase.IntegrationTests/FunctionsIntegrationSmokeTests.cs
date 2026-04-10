using System.Collections.Generic;
using System.Threading.Tasks;
using OrangeDot.Supabase;
using Supabase.Functions.Exceptions;

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
            PublishableKey = settings.AnonKey
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

    [LocalSupabaseFunctionsFact]
    public async Task Stateless_client_surfaces_controlled_failure_from_local_integration_function()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();

        await IntegrationTestEnvironment.EnsureOptInAndFailureFunctionReachableAsync(settings);

        var client = SupabaseStatelessClient.Create(new SupabaseOptions
        {
            Url = settings.Url,
            PublishableKey = settings.AnonKey
        });

        var ex = await Assert.ThrowsAsync<FunctionsException>(async () =>
            await client.Functions.Invoke(
                IntegrationTestEnvironment.IntegrationFailureFunctionName,
                options: new global::Supabase.Functions.Client.InvokeFunctionOptions
                {
                    Body = new Dictionary<string, object>
                    {
                        ["source"] = "integration-tests"
                    }
                }));

        Assert.Equal(500, ex.StatusCode);
        Assert.Equal(FailureHint.Reason.Internal, ex.Reason);
        Assert.NotNull(ex.Content);
        Assert.Contains("\"controlled_failure\"", ex.Content);
        Assert.Contains(IntegrationTestEnvironment.IntegrationFailureFunctionName, ex.Content);
    }

    public sealed class IntegrationFunctionSmokeResponse
    {
        public required bool Ok { get; init; }

        public required string Function { get; init; }

        public required string Source { get; init; }
    }
}
