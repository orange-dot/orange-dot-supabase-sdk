using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace OrangeDot.Supabase.IntegrationTests;

internal static class IntegrationTestEnvironment
{
    internal const string RunIntegrationVariableName = "ORANGEDOT_SUPABASE_RUN_INTEGRATION";
    private const string SupabaseUrlVariableName = "SUPABASE_URL";
    private const string SupabaseAnonKeyVariableName = "SUPABASE_ANON_KEY";
    private const string DefaultSupabaseUrl = "http://127.0.0.1:54321";
    private const string DefaultAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6ImFub24iLCJleHAiOjE5ODM4MTI5OTZ9.CRXP1A7WOeoJeXxjNni43kdQwgnWNReilDMblYTn_I0";

    internal static IntegrationTestSettings LoadSettings()
    {
        var runIntegrationValue = Environment.GetEnvironmentVariable(RunIntegrationVariableName);
        var url = Environment.GetEnvironmentVariable(SupabaseUrlVariableName) ?? DefaultSupabaseUrl;
        var anonKey = Environment.GetEnvironmentVariable(SupabaseAnonKeyVariableName) ?? DefaultAnonKey;

        return new IntegrationTestSettings(
            url.TrimEnd('/'),
            anonKey,
            IsEnabled(runIntegrationValue));
    }

    internal static async Task EnsureOptInAndReachableAsync(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsEnabled)
        {
            return;
        }

        await EnsureReachableAsync(settings);
    }

    internal static async Task EnsureReachableAsync(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{settings.Url}/rest/v1/integration_todos?select=id&limit=1");
        request.Headers.Add("apikey", settings.AnonKey);

        using var response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        throw new InvalidOperationException(
            $"Local Supabase integration stack is not ready. Expected GET {request.RequestUri} to succeed after `supabase start`. Response: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}");
    }

    internal static string NewOwnerTag(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    internal static async Task CleanupByOwnerTagAsync(
        global::Supabase.Postgrest.Interfaces.IPostgrestClient postgrest,
        string ownerTag)
    {
        ArgumentNullException.ThrowIfNull(postgrest);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerTag);

        await postgrest.Table<IntegrationTodo>()
            .Filter("owner_tag", global::Supabase.Postgrest.Constants.Operator.Equals, ownerTag)
            .Delete();
    }

    private static bool IsEnabled(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.Equals(value, "1", StringComparison.Ordinal))
        {
            return true;
        }

        return bool.TryParse(value, out var enabled) && enabled;
    }
}

internal sealed record IntegrationTestSettings(
    string Url,
    string AnonKey,
    bool IsEnabled);
