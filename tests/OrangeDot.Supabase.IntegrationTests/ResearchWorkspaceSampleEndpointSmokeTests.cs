using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace OrangeDot.Supabase.IntegrationTests;

public sealed class ResearchWorkspaceSampleEndpointSmokeTests
{
#if NET10_0
    private const string SampleTargetFramework = "net10.0";
#else
    private const string SampleTargetFramework = "net8.0";
#endif

    [LocalSupabaseFact]
    public async Task Protected_projects_endpoint_returns_json_401_without_bearer_token()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();
        await IntegrationTestEnvironment.EnsureOptInAndResearchWorkspaceReachableAsync(settings);

        var baseUrl = CreateBaseUrl();
        using var process = StartSampleProcess(settings, baseUrl);
        try
        {
            using var client = CreateHttpClient(baseUrl);

            await WaitForHealthyAsync(client, process);

            using var response = await client.GetAsync("/projects");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            using var json = JsonDocument.Parse(body);
            Assert.Equal("auth_required", GetRequiredString(json.RootElement, "error"));
            Assert.Equal(401, json.RootElement.GetProperty("status").GetInt32());
        }
        finally
        {
            KillProcess(process);
        }
    }

    [LocalSupabaseFact]
    public async Task Embedded_ui_and_openapi_routes_load()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();
        await IntegrationTestEnvironment.EnsureOptInAndResearchWorkspaceReachableAsync(settings);

        var baseUrl = CreateBaseUrl();
        using var process = StartSampleProcess(settings, baseUrl);
        try
        {
            using var client = CreateHttpClient(baseUrl);

            await WaitForHealthyAsync(client, process);

            using var uiResponse = await client.GetAsync("/");
            var uiBody = await uiResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, uiResponse.StatusCode);
            Assert.Contains("Research Workspace Cockpit", uiBody, StringComparison.Ordinal);

            using var swaggerResponse = await client.GetAsync("/swagger/index.html");
            var swaggerBody = await swaggerResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, swaggerResponse.StatusCode);
            Assert.Contains("swagger-ui", swaggerBody, StringComparison.OrdinalIgnoreCase);

            using var openApiResponse = await client.GetAsync("/openapi/v1.json");
            var openApiBody = await openApiResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);

            using var json = JsonDocument.Parse(openApiBody);
            Assert.True(json.RootElement.TryGetProperty("paths", out var paths));
            Assert.True(paths.TryGetProperty("/projects", out _));
            Assert.True(paths.TryGetProperty("/ui/bootstrap", out _));
        }
        finally
        {
            KillProcess(process);
        }
    }

    [LocalSupabaseFact]
    public async Task Ui_auth_signup_can_issue_a_session_for_sample_api_calls()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();
        await IntegrationTestEnvironment.EnsureOptInAndResearchWorkspaceReachableAsync(settings);

        var baseUrl = CreateBaseUrl();
        using var process = StartSampleProcess(settings, baseUrl);
        try
        {
            using var client = CreateHttpClient(baseUrl);

            await WaitForHealthyAsync(client, process);

            var email = IntegrationTestEnvironment.NewEmailAddress("ui-owner");
            using var session = await SendJsonAsync(
                client,
                HttpMethod.Post,
                "/ui/auth/signup",
                new
                {
                    email,
                    password = IntegrationTestEnvironment.DefaultPassword
                });

            var accessToken = GetRequiredString(session.RootElement, "accessToken");
            var userId = GetRequiredString(session.RootElement, "userId");
            Assert.Equal(email, GetRequiredString(session.RootElement, "email"));

            using var me = await SendJsonAsync(
                client,
                HttpMethod.Get,
                "/me",
                accessToken: accessToken);

            Assert.Equal(userId, GetRequiredString(me.RootElement, "userId"));
            Assert.Equal(email, GetRequiredString(me.RootElement, "email"));
        }
        finally
        {
            KillProcess(process);
        }
    }

    [LocalSupabaseFact]
    public async Task Owner_and_editor_can_complete_the_sample_http_flow_end_to_end()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();
        await IntegrationTestEnvironment.EnsureOptInAndResearchWorkspaceReachableAsync(settings);

        var baseUrl = CreateBaseUrl();
        using var process = StartSampleProcess(settings, baseUrl);
        try
        {
            using var sampleClient = CreateHttpClient(baseUrl);
            using var authClient = CreateHttpClient(settings.Url);

            await WaitForHealthyAsync(sampleClient, process);

            var owner = await CreateUserAsync(authClient, settings, "sample-owner");
            var editor = await CreateUserAsync(authClient, settings, "sample-editor");

            using var me = await SendJsonAsync(sampleClient, HttpMethod.Get, "/me", accessToken: owner.AccessToken);
            Assert.Equal(owner.UserId, GetRequiredString(me.RootElement, "userId"));

            using var organization = await SendJsonAsync(
                sampleClient,
                HttpMethod.Post,
                "/organizations",
                new { name = "Audio Research Guild" },
                owner.AccessToken,
                HttpStatusCode.Created);
            var organizationId = GetRequiredString(organization.RootElement, "id");

            using var membership = await SendJsonAsync(
                sampleClient,
                HttpMethod.Post,
                $"/organizations/{organizationId}/memberships",
                new { userId = editor.UserId, role = "editor" },
                owner.AccessToken,
                HttpStatusCode.Created);
            Assert.Equal(editor.UserId, GetRequiredString(membership.RootElement, "userId"));

            using var project = await SendJsonAsync(
                sampleClient,
                HttpMethod.Post,
                $"/organizations/{organizationId}/projects",
                new { name = "Cocek Tuning Lab" },
                owner.AccessToken,
                HttpStatusCode.Created);
            var projectId = GetRequiredString(project.RootElement, "id");

            using var experiment = await SendJsonAsync(
                sampleClient,
                HttpMethod.Post,
                $"/projects/{projectId}/experiments",
                new { name = "Spring Session", summary = "Watcher-enabled flow", status = "active" },
                owner.AccessToken,
                HttpStatusCode.Created);
            var experimentId = GetRequiredString(experiment.RootElement, "id");

            using var run = await SendJsonAsync(
                sampleClient,
                HttpMethod.Post,
                $"/experiments/{experimentId}/runs",
                new { displayName = "Run 01", notes = "first pass", status = "running" },
                editor.AccessToken,
                HttpStatusCode.Created);
            var runId = GetRequiredString(run.RootElement, "id");

            using var metric = await SendJsonAsync(
                sampleClient,
                HttpMethod.Post,
                $"/runs/{runId}/metrics",
                new { metricName = "tempoDrift", metricValue = 0.17, metricUnit = "percent" },
                editor.AccessToken,
                HttpStatusCode.Created);
            Assert.Equal("tempoDrift", GetRequiredString(metric.RootElement, "metricName"));

            using var artifact = await SendJsonAsync(
                sampleClient,
                HttpMethod.Post,
                $"/runs/{runId}/artifacts/text",
                new { kind = "log", fileName = "run.log", content = "render complete", contentType = "text/plain" },
                editor.AccessToken,
                HttpStatusCode.Created);
            Assert.Equal("log", GetRequiredString(artifact.RootElement, "kind"));

            using var watcher = await SendJsonAsync(
                sampleClient,
                HttpMethod.Post,
                $"/experiments/{experimentId}/watchers",
                accessToken: editor.AccessToken,
                expectedStatusCode: HttpStatusCode.Created);
            var watchId = GetRequiredString(watcher.RootElement, "watchId");

            using var forbiddenWatcherResponse = await sampleClient.SendAsync(CreateRequest(
                HttpMethod.Get,
                $"/watchers/{watchId}",
                accessToken: owner.AccessToken));
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenWatcherResponse.StatusCode);

            using var updatedRun = await SendJsonAsync(
                sampleClient,
                HttpMethod.Post,
                $"/runs/{runId}/status",
                new { status = "succeeded", notes = "watch should observe this" },
                editor.AccessToken);
            Assert.Equal("succeeded", GetRequiredString(updatedRun.RootElement, "status"));

            await WaitForWatchEventAsync(sampleClient, watchId, editor.AccessToken, runId, "succeeded");

            using var baseline = await SendJsonAsync(
                sampleClient,
                HttpMethod.Post,
                $"/experiments/{experimentId}/baseline",
                new { runId },
                editor.AccessToken);
            Assert.Equal(runId, GetRequiredString(baseline.RootElement, "promotedRunId"));

            using var artifacts = await SendJsonAsync(
                sampleClient,
                HttpMethod.Get,
                $"/runs/{runId}/artifacts",
                accessToken: editor.AccessToken);
            Assert.True(artifacts.RootElement.GetArrayLength() >= 1);
        }
        finally
        {
            KillProcess(process);
        }
    }

    private static async Task<SampleUser> CreateUserAsync(HttpClient authClient, IntegrationTestSettings settings, string prefix)
    {
        var email = IntegrationTestEnvironment.NewEmailAddress(prefix);

        using var signup = await SendJsonAsync(
            authClient,
            HttpMethod.Post,
            "/auth/v1/signup",
            new
            {
                email,
                password = IntegrationTestEnvironment.DefaultPassword
            },
            null,
            HttpStatusCode.OK,
            settings.AnonKey);

        var userId = GetRequiredString(signup.RootElement.GetProperty("user"), "id");

        using var token = await SendJsonAsync(
            authClient,
            HttpMethod.Post,
            "/auth/v1/token?grant_type=password",
            new
            {
                email,
                password = IntegrationTestEnvironment.DefaultPassword
            },
            null,
            HttpStatusCode.OK,
            settings.AnonKey);

        return new SampleUser(userId, GetRequiredString(token.RootElement, "access_token"));
    }

    private static async Task WaitForWatchEventAsync(
        HttpClient client,
        string watchId,
        string accessToken,
        string runId,
        string expectedStatus)
    {
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(15))
        {
            using var snapshot = await SendJsonAsync(
                client,
                HttpMethod.Get,
                $"/watchers/{watchId}",
                accessToken: accessToken);

            foreach (var item in snapshot.RootElement.GetProperty("events").EnumerateArray())
            {
                if (string.Equals(GetRequiredString(item, "runId"), runId, StringComparison.Ordinal) &&
                    string.Equals(GetRequiredString(item, "status"), expectedStatus, StringComparison.Ordinal))
                {
                    return;
                }
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Timed out waiting for watch '{watchId}' to observe run '{runId}' status '{expectedStatus}'.");
    }

    private static async Task<JsonDocument> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        object? payload = null,
        string? accessToken = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK,
        string? apiKey = null)
    {
        using var request = CreateRequest(method, path, payload, accessToken, apiKey);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatusCode, response.StatusCode);

        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        object? payload = null,
        string? accessToken = null,
        string? apiKey = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.TryAddWithoutValidation("apikey", apiKey);
        }

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return request;
    }

    private static HttpClient CreateHttpClient(string baseUrl)
    {
        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    private static Process StartSampleProcess(IntegrationTestSettings settings, string baseUrl)
    {
        var repoRoot = FindRepoRoot();
        var startInfo = new ProcessStartInfo(
            "dotnet",
            $"run --configuration Release --no-restore --project samples/ResearchWorkspaceApi/ResearchWorkspaceApi.csproj --framework {SampleTargetFramework} --urls {baseUrl}")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["Supabase__Url"] = settings.Url;
        startInfo.Environment["Supabase__PublishableKey"] = settings.AnonKey;
        startInfo.Environment["Supabase__SecretKey"] = settings.SecretKey;

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start ResearchWorkspaceApi sample process.");
    }

    private static async Task WaitForHealthyAsync(HttpClient client, Process process)
    {
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(30))
        {
            if (process.HasExited)
            {
                var stdout = await process.StandardOutput.ReadToEndAsync();
                var stderr = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException(
                    $"ResearchWorkspaceApi process exited before becoming healthy. Stdout: {stdout}\nStderr: {stderr}");
            }

            try
            {
                using var response = await client.GetAsync("/health");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // The sample may still be starting up.
            }

            await Task.Delay(500);
        }

        KillProcess(process);
        throw new TimeoutException("Timed out waiting for ResearchWorkspaceApi health endpoint.");
    }

    private static string CreateBaseUrl()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
    }

    private static void KillProcess(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Expected JSON string property '{propertyName}'.");
        }

        return property.GetString() ?? throw new InvalidOperationException($"JSON property '{propertyName}' was null.");
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "OrangeDot.Supabase.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the integration test output directory.");
    }

    private sealed record SampleUser(string UserId, string AccessToken);
}
