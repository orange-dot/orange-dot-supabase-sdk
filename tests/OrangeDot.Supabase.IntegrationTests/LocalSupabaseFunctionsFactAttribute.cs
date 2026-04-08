using System;
using Xunit;

namespace OrangeDot.Supabase.IntegrationTests;

public sealed class LocalSupabaseFunctionsFactAttribute : FactAttribute
{
    public LocalSupabaseFunctionsFactAttribute()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();

        if (!settings.IsEnabled)
        {
            Skip = $"Local Supabase integration tests are disabled. Set {IntegrationTestEnvironment.RunIntegrationVariableName}=1, run `supabase start`, then rerun `dotnet test`.";
            return;
        }

        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");

        if (!string.IsNullOrWhiteSpace(dockerHost) &&
            dockerHost.Contains("podman", StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Local Supabase edge-function smoke tests are skipped under Podman because the Supabase edge runtime cannot resolve mounted function entrypoints in that environment. Run the integration suite against a Docker-backed Supabase stack to execute this test.";
        }
    }
}
