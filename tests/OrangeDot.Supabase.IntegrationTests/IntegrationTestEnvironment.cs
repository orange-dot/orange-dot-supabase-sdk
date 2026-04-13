using System;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OrangeDot.Supabase;

namespace OrangeDot.Supabase.IntegrationTests;

internal static class IntegrationTestEnvironment
{
    internal const string RunIntegrationVariableName = "ORANGEDOT_SUPABASE_RUN_INTEGRATION";
    internal const string IntegrationBucketName = "integration-public";
    internal const string ResearchArtifactsBucketName = "research-artifacts";
    internal const string IntegrationSmokeFunctionName = "orangedot-integration-smoke";
    internal const string IntegrationFailureFunctionName = "orangedot-integration-failure";
    internal const string ResearchPromoteBaselineFunctionName = "research-promote-baseline";
    internal const string DefaultPassword = "password123";

    private const string SupabaseUrlVariableName = "SUPABASE_URL";
    private const string SupabaseAnonKeyVariableName = "SUPABASE_ANON_KEY";
    private const string SupabaseServiceKeyVariableName = "SUPABASE_SERVICE_ROLE_KEY";
    private const string DefaultSupabaseUrl = "http://127.0.0.1:54321";
    private const string DefaultAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6ImFub24iLCJleHAiOjE5ODM4MTI5OTZ9.CRXP1A7WOeoJeXxjNni43kdQwgnWNReilDMblYTn_I0";
    private const string DefaultServiceKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImV4cCI6MTk4MzgxMjk5Nn0.EGIM96RAZx35lJzdJsyH-qQwv8Hdp7fsn3W0YpN81IU";

    internal static IntegrationTestSettings LoadSettings()
    {
        var runIntegrationValue = Environment.GetEnvironmentVariable(RunIntegrationVariableName);
        var url = Environment.GetEnvironmentVariable(SupabaseUrlVariableName) ?? DefaultSupabaseUrl;
        var anonKey = Environment.GetEnvironmentVariable(SupabaseAnonKeyVariableName) ?? DefaultAnonKey;
        var secretKey = Environment.GetEnvironmentVariable(SupabaseServiceKeyVariableName) ?? DefaultServiceKey;

        return new IntegrationTestSettings(
            url.TrimEnd('/'),
            anonKey,
            secretKey,
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

    internal static async Task EnsureOptInAndStorageReachableAsync(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsEnabled)
        {
            return;
        }

        await EnsureReachableAsync(settings);
        await EnsureStorageReachableAsync(settings);
    }

    internal static async Task EnsureOptInAndFunctionsReachableAsync(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsEnabled)
        {
            return;
        }

        await EnsureReachableAsync(settings);
        await EnsureFunctionsReachableAsync(settings);
    }

    internal static async Task EnsureOptInAndFailureFunctionReachableAsync(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsEnabled)
        {
            return;
        }

        await EnsureReachableAsync(settings);
        await EnsureFailureFunctionReachableAsync(settings);
    }

    internal static async Task EnsureOptInAndCapabilitiesReachableAsync(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsEnabled)
        {
            return;
        }

        await EnsureReachableAsync(settings);
        await EnsureStorageReachableAsync(settings);
        await EnsureFunctionsReachableAsync(settings);
        await EnsureFailureFunctionReachableAsync(settings);
    }

    internal static async Task EnsureOptInAndResearchWorkspaceReachableAsync(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsEnabled)
        {
            return;
        }

        await EnsureReachableAsync(settings);
        await EnsureResearchStorageReachableAsync(settings);
        await EnsureResearchFunctionReachableAsync(settings);
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

    internal static async Task EnsureStorageReachableAsync(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        using var httpClient = CreateHttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{settings.Url}/storage/v1/object/list/{IntegrationBucketName}");

        request.Headers.Add("apikey", settings.AnonKey);
        request.Headers.Add("Authorization", $"Bearer {settings.AnonKey}");
        request.Content = JsonContent.Create(new
        {
            prefix = string.Empty,
            limit = 1,
            offset = 0,
            search = string.Empty,
            sortBy = new
            {
                column = "name",
                order = "asc"
            }
        });

        using var response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        throw new InvalidOperationException(
            $"Local Supabase storage stack is not ready. Expected POST {request.RequestUri} to succeed for bucket '{IntegrationBucketName}'. Response: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}");
    }

    internal static async Task EnsureResearchStorageReachableAsync(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        using var httpClient = CreateHttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{settings.Url}/storage/v1/object/list/{ResearchArtifactsBucketName}");

        request.Headers.Add("apikey", settings.SecretKey);
        request.Headers.Add("Authorization", $"Bearer {settings.SecretKey}");
        request.Content = JsonContent.Create(new
        {
            prefix = string.Empty,
            limit = 1,
            offset = 0,
            search = string.Empty,
            sortBy = new
            {
                column = "name",
                order = "asc"
            }
        });

        using var response = await httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        throw new InvalidOperationException(
            $"Research workspace storage bucket is not ready. Expected POST {request.RequestUri} to succeed for bucket '{ResearchArtifactsBucketName}'. Response: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}");
    }

    internal static async Task EnsureFunctionsReachableAsync(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        using var httpClient = CreateHttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{settings.Url}/functions/v1/{IntegrationSmokeFunctionName}");

        request.Headers.Add("apikey", settings.AnonKey);
        request.Content = JsonContent.Create(new
        {
            source = "integration-readiness"
        });

        using var response = await httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Local Supabase functions stack is not ready. Expected POST {request.RequestUri} to succeed. Response: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}");
        }

        using var json = JsonDocument.Parse(responseBody);
        var root = json.RootElement;

        if (!root.TryGetProperty("ok", out var okProperty) || !okProperty.GetBoolean() ||
            !root.TryGetProperty("function", out var functionProperty) ||
            !string.Equals(functionProperty.GetString(), IntegrationSmokeFunctionName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Local Supabase functions stack returned an unexpected smoke response: {responseBody}");
        }
    }

    internal static async Task EnsureFailureFunctionReachableAsync(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        using var httpClient = CreateHttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{settings.Url}/functions/v1/{IntegrationFailureFunctionName}");

        request.Headers.Add("apikey", settings.AnonKey);
        request.Content = JsonContent.Create(new
        {
            source = "integration-readiness"
        });

        using var response = await httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if ((int)response.StatusCode != 500)
        {
            throw new InvalidOperationException(
                $"Local Supabase failure function is not ready. Expected POST {request.RequestUri} to return 500. Response: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}");
        }

        using var json = JsonDocument.Parse(responseBody);
        var root = json.RootElement;

        if (!root.TryGetProperty("ok", out var okProperty) || okProperty.GetBoolean() ||
            !root.TryGetProperty("function", out var functionProperty) ||
            !string.Equals(functionProperty.GetString(), IntegrationFailureFunctionName, StringComparison.Ordinal) ||
            !root.TryGetProperty("error", out var errorProperty) ||
            !string.Equals(errorProperty.GetString(), "controlled_failure", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Local Supabase failure function returned an unexpected response: {responseBody}");
        }
    }

    internal static async Task EnsureResearchFunctionReachableAsync(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        using var httpClient = CreateHttpClient();
        var startedAt = DateTime.UtcNow;
        Exception? lastException = null;

        while (DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(30))
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{settings.Url}/functions/v1/{ResearchPromoteBaselineFunctionName}");

                request.Headers.Add("apikey", settings.SecretKey);
                request.Headers.Add("Authorization", $"Bearer {settings.SecretKey}");
                request.Content = JsonContent.Create(new
                {
                    projectId = Guid.NewGuid(),
                    experimentId = Guid.NewGuid(),
                    runId = Guid.NewGuid()
                });

                using var response = await httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if ((int)response.StatusCode is 400 or 401 or 403 or 500)
                {
                    return;
                }

                if ((int)response.StatusCode == 404)
                {
                    throw new InvalidOperationException(
                        $"Research workspace function was not found at {request.RequestUri}. Response: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}");
                }

                if ((int)response.StatusCode == 503)
                {
                    lastException = new InvalidOperationException(
                        $"Research workspace function is not ready yet. Response: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}");
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Research workspace function returned an unexpected readiness response. Expected 400/401/403/500 but got {(int)response.StatusCode} {response.ReasonPhrase}. Body: {responseBody}");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                lastException = ex;
            }

            await Task.Delay(500);
        }

        throw new InvalidOperationException(
            $"Timed out waiting for research workspace function readiness after 30 seconds. Last error: {lastException?.Message}",
            lastException);
    }

    internal static string NewOwnerTag(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    internal static string NewEmailAddress(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}@example.com";
    }

    internal static string NewStorageObjectPath(string prefix, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var normalizedPrefix = prefix.Trim('/');
        var normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : extension.StartsWith(".", StringComparison.Ordinal)
                ? extension
                : $".{extension}";

        return $"{normalizedPrefix}/{Guid.NewGuid():N}{normalizedExtension}";
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

    internal static async Task BestEffortRemoveStorageObjectAsync(
        global::Supabase.Storage.Interfaces.IStorageFileApi<global::Supabase.Storage.FileObject> bucket,
        string path)
    {
        ArgumentNullException.ThrowIfNull(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await bucket.Remove(path);
        }
        catch (global::Supabase.Storage.Exceptions.SupabaseStorageException ex)
            when (ex.Reason == global::Supabase.Storage.Exceptions.FailureHint.Reason.NotFound)
        {
        }
    }

    internal static ServiceProvider CreateServerProvider(IntegrationTestSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSupabaseServer(options =>
        {
            options.Url = settings.Url;
            options.PublishableKey = settings.AnonKey;
            options.SecretKey = settings.SecretKey;
        });

        return services.BuildServiceProvider();
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

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }
}

internal sealed record IntegrationTestSettings(
    string Url,
    string AnonKey,
    string SecretKey,
    bool IsEnabled);
