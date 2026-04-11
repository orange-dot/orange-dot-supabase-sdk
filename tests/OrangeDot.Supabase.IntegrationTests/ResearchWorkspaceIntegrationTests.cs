using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrangeDot.Supabase;
using Supabase.Functions.Exceptions;
using Supabase.Postgrest.Exceptions;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using Supabase.Storage.Exceptions;

namespace OrangeDot.Supabase.IntegrationTests;

public sealed class ResearchWorkspaceIntegrationTests
{
    [LocalSupabaseFact]
    public async Task Authenticated_members_are_isolated_to_their_own_organization_projects()
    {
        await using var scenario = await ResearchWorkspaceScenario.CreateAsync("workspace-projects");

        var ownerProjects = await scenario.OwnerClient.Postgrest.Table<ResearchProjectTestRecord>().Get();
        var outsiderProjects = await scenario.OutsiderClient.Postgrest.Table<ResearchProjectTestRecord>().Get();

        Assert.Contains(ownerProjects.Models, project => string.Equals(project.Id, scenario.ProjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(ownerProjects.Models, project => string.Equals(project.Id, scenario.OutsiderProjectId, StringComparison.Ordinal));
        Assert.Contains(outsiderProjects.Models, project => string.Equals(project.Id, scenario.OutsiderProjectId, StringComparison.Ordinal));
        Assert.DoesNotContain(outsiderProjects.Models, project => string.Equals(project.Id, scenario.ProjectId, StringComparison.Ordinal));
    }

    [LocalSupabaseFact]
    public async Task Viewer_cannot_mutate_runs_metrics_artifacts_or_decisions()
    {
        await using var scenario = await ResearchWorkspaceScenario.CreateAsync("workspace-viewer");
        var run = await scenario.CreateRunAsEditorAsync("viewer-denied-run");

        var runInsertException = await Assert.ThrowsAsync<PostgrestException>(async () =>
            await scenario.ViewerClient.Postgrest.Table<ResearchRunTestRecord>().Insert(new ResearchRunTestRecord
            {
                ExperimentId = scenario.ExperimentId,
                DisplayName = "should-fail",
                Status = "queued"
            }));
        AssertDenied(runInsertException.StatusCode);

        var metricInsertException = await Assert.ThrowsAsync<PostgrestException>(async () =>
            await scenario.ViewerClient.Postgrest.Table<ResearchRunMetricTestRecord>().Insert(new ResearchRunMetricTestRecord
            {
                RunId = run.Id,
                MetricName = "viewer-metric",
                MetricValue = 1.0
            }));
        AssertDenied(metricInsertException.StatusCode);

        var decisionInsertException = await Assert.ThrowsAsync<PostgrestException>(async () =>
            await scenario.ViewerClient.Postgrest.Table<ResearchDecisionTestRecord>().Insert(new ResearchDecisionTestRecord
            {
                ProjectId = scenario.ProjectId,
                Title = "viewer-decision",
                Status = "proposed"
            }));
        AssertDenied(decisionInsertException.StatusCode);

        var path = BuildObjectPath(scenario.OrganizationId, scenario.ProjectId, run.Id!, "viewer-denied.txt");
        var artifactInsertException = await Assert.ThrowsAsync<PostgrestException>(async () =>
            await scenario.ViewerClient.Postgrest.Table<ResearchRunArtifactTestRecord>().Insert(new ResearchRunArtifactTestRecord
        {
            RunId = run.Id,
            StorageBucket = IntegrationTestEnvironment.ResearchArtifactsBucketName,
            ObjectPath = path,
            FileName = "viewer-denied.txt",
            Kind = "log"
        }));
        AssertDenied(artifactInsertException.StatusCode);

        var storageException = await Assert.ThrowsAsync<SupabaseStorageException>(async () =>
            await scenario.ViewerClient.Storage.From(IntegrationTestEnvironment.ResearchArtifactsBucketName)
                .CreateUploadSignedUrl(path));

        Assert.True(
            storageException.Reason is global::Supabase.Storage.Exceptions.FailureHint.Reason.NotFound
                or global::Supabase.Storage.Exceptions.FailureHint.Reason.NotAuthorized
                or global::Supabase.Storage.Exceptions.FailureHint.Reason.Unknown,
            $"Unexpected storage failure reason: {storageException.Reason}");
    }

    [LocalSupabaseFact]
    public async Task Editor_can_create_metrics_upload_artifacts_and_unauthorized_users_cannot_read_them()
    {
        await using var scenario = await ResearchWorkspaceScenario.CreateAsync("workspace-artifacts");
        var run = await scenario.CreateRunAsEditorAsync("artifact-run");

        var metricResponse = await scenario.EditorClient.Postgrest.Table<ResearchRunMetricTestRecord>().Insert(new ResearchRunMetricTestRecord
        {
            RunId = run.Id,
            MetricName = "tempo-drift",
            MetricValue = 0.17,
            MetricUnit = "percent"
        });

        var metric = Assert.Single(metricResponse.Models);
        Assert.Equal("tempo-drift", metric.MetricName);

        var objectPath = BuildObjectPath(scenario.OrganizationId, scenario.ProjectId, run.Id!, "artifact.log");
        await scenario.RegisterArtifactPathAsync(objectPath);

        var artifactResponse = await scenario.EditorClient.Postgrest.Table<ResearchRunArtifactTestRecord>().Insert(new ResearchRunArtifactTestRecord
        {
            RunId = run.Id,
            StorageBucket = IntegrationTestEnvironment.ResearchArtifactsBucketName,
            ObjectPath = objectPath,
            FileName = "artifact.log",
            Kind = "log",
            ContentType = "text/plain"
        });

        var artifact = Assert.Single(artifactResponse.Models);
        Assert.Equal(objectPath, artifact.ObjectPath);

        var bucket = scenario.EditorClient.Storage.From(IntegrationTestEnvironment.ResearchArtifactsBucketName);
        var payload = System.Text.Encoding.UTF8.GetBytes("artifact-payload");
        await bucket.Upload(payload, objectPath, new global::Supabase.Storage.FileOptions
        {
            ContentType = "text/plain",
            Upsert = true
        });

        var listedArtifacts = await scenario.EditorClient.Postgrest.Table<ResearchRunArtifactTestRecord>()
            .Filter("run_id", global::Supabase.Postgrest.Constants.Operator.Equals, run.Id!)
            .Get();

        Assert.Contains(listedArtifacts.Models, item => string.Equals(item.ObjectPath, objectPath, StringComparison.Ordinal));

        var downloaded = await bucket.Download(objectPath, transformOptions: null, onProgress: null);
        Assert.Equal(payload, downloaded);

        var outsiderArtifacts = await scenario.OutsiderClient.Postgrest.Table<ResearchRunArtifactTestRecord>()
            .Filter("run_id", global::Supabase.Postgrest.Constants.Operator.Equals, run.Id!)
            .Get();

        Assert.Empty(outsiderArtifacts.Models);

        var outsiderBucket = scenario.OutsiderClient.Storage.From(IntegrationTestEnvironment.ResearchArtifactsBucketName);
        var outsiderDownloadException = await Assert.ThrowsAsync<SupabaseStorageException>(async () =>
            await outsiderBucket.Download(objectPath, transformOptions: null, onProgress: null));

        Assert.True(
            outsiderDownloadException.Reason is global::Supabase.Storage.Exceptions.FailureHint.Reason.NotFound
                or global::Supabase.Storage.Exceptions.FailureHint.Reason.NotAuthorized
                or global::Supabase.Storage.Exceptions.FailureHint.Reason.Unknown,
            $"Unexpected outsider storage failure reason: {outsiderDownloadException.Reason}");
    }

    [LocalSupabaseFact]
    public async Task Baseline_promotion_function_succeeds_for_editor_and_fails_for_viewer()
    {
        await using var scenario = await ResearchWorkspaceScenario.CreateAsync("workspace-baseline");
        var run = await scenario.CreateRunAsEditorAsync("baseline-run", status: "succeeded");

        var promoted = await scenario.EditorClient.Functions.Invoke<PromoteBaselineFunctionResponse>(
            IntegrationTestEnvironment.ResearchPromoteBaselineFunctionName,
            options: new global::Supabase.Functions.Client.InvokeFunctionOptions
            {
                Body = new Dictionary<string, object>
                {
                    ["projectId"] = scenario.ProjectId,
                    ["experimentId"] = scenario.ExperimentId,
                    ["runId"] = run.Id!
                }
            });

        Assert.NotNull(promoted);
        Assert.True(promoted!.Ok);
        Assert.Equal(run.Id, promoted.PromotedRunId);

        var updatedExperiment = await scenario.OwnerClient.Postgrest.Table<ResearchExperimentTestRecord>()
            .Filter("id", global::Supabase.Postgrest.Constants.Operator.Equals, scenario.ExperimentId)
            .Get();
        Assert.Equal(run.Id, Assert.Single(updatedExperiment.Models).BaselineRunId);

        var viewerException = await Assert.ThrowsAsync<FunctionsException>(async () =>
            await scenario.ViewerClient.Functions.Invoke(
                IntegrationTestEnvironment.ResearchPromoteBaselineFunctionName,
                options: new global::Supabase.Functions.Client.InvokeFunctionOptions
                {
                    Body = new Dictionary<string, object>
                    {
                        ["projectId"] = scenario.ProjectId,
                        ["experimentId"] = scenario.ExperimentId,
                        ["runId"] = run.Id!
                    }
                }));

        Assert.Equal(403, viewerException.StatusCode);
    }

    [LocalSupabaseFact]
    public async Task Realtime_subscription_observes_run_status_updates_for_authorized_user()
    {
        await using var scenario = await ResearchWorkspaceScenario.CreateAsync("workspace-realtime");
        var run = await scenario.CreateRunAsEditorAsync("realtime-run", status: "running");
        var changeReceived = new TaskCompletionSource<ResearchRunTestRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = await scenario.CreateRealtimeClientForUserAsync(scenario.Editor);
        global::Supabase.Realtime.RealtimeChannel? channel = null;

        try
        {
            channel = client.Channel(Guid.NewGuid().ToString("N"));
            channel.Options.Parameters ??= new Dictionary<string, string>(StringComparer.Ordinal);
            channel.Options.Parameters["user_token"] = scenario.Editor.AccessToken;
            channel.Register(new PostgresChangesOptions(
                schema: "public",
                table: "research_runs",
                eventType: PostgresChangesOptions.ListenType.Updates,
                filter: $"experiment_id=eq.{scenario.ExperimentId}"));
            channel.AddPostgresChangeHandler(
                PostgresChangesOptions.ListenType.Updates,
                (_, change) =>
                {
                    var model = change.Model<ResearchRunTestRecord>();
                    if (model?.Id == run.Id && model?.Status == "succeeded")
                    {
                        changeReceived.TrySetResult(model);
                    }
                });

            await channel.Subscribe();
            Assert.Equal(Constants.ChannelState.Joined, channel.State);

            await Task.Delay(TimeSpan.FromSeconds(3));

            run.Status = "succeeded";
            run.CompletedAt = DateTime.UtcNow;
            await scenario.EditorClient.Postgrest.Table<ResearchRunTestRecord>().Update(run);

            var changed = await changeReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("succeeded", changed.Status);
        }
        finally
        {
            if (channel is not null)
            {
                channel.Unsubscribe();
                client.Remove(channel);
            }

            client.Disconnect();
        }
    }

    private static void AssertDenied(int statusCode)
    {
        Assert.True(statusCode is 401 or 403, $"Expected an authorization failure, but received status code {statusCode}.");
    }

    private static string BuildObjectPath(string organizationId, string projectId, string runId, string fileName)
    {
        return $"org/{organizationId}/project/{projectId}/run/{runId}/{Guid.NewGuid():N}-{fileName}";
    }

    private sealed class PromoteBaselineFunctionResponse
    {
        public required bool Ok { get; init; }

        public required string PromotedRunId { get; init; }
    }
}
