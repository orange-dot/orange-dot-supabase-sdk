using Microsoft.Extensions.Options;
using OrangeDot.Supabase;
using Supabase.Functions.Exceptions;
using Supabase.Postgrest.Exceptions;
using Supabase.Storage;
using Supabase.Storage.Exceptions;

namespace ResearchWorkspaceApi;

public sealed class ResearchWorkspaceService
{
    internal const string ResearchArtifactsBucketName = "research-artifacts";
    private static readonly string[] SupportedRoles = ["owner", "editor", "viewer"];
    private static readonly string[] SupportedExperimentStatuses = ["draft", "active", "archived"];
    private static readonly string[] SupportedRunStatuses = ["queued", "running", "succeeded", "failed", "canceled"];
    private static readonly string[] SupportedArtifactKinds = ["log", "report", "bundle"];
    private static readonly string[] SupportedDecisionStatuses = ["proposed", "accepted", "rejected"];

    private readonly ISupabaseStatelessClientFactory _clients;
    private readonly ResearchRunWatchRegistry _watchRegistry;
    private readonly ILogger<ResearchWorkspaceService> _logger;
    private readonly SupabaseServerOptions _options;

    public ResearchWorkspaceService(
        ISupabaseStatelessClientFactory clients,
        ResearchRunWatchRegistry watchRegistry,
        IOptions<SupabaseServerOptions> options,
        ILogger<ResearchWorkspaceService> logger)
    {
        _clients = clients;
        _watchRegistry = watchRegistry;
        _options = options.Value;
        _logger = logger;
    }

    public MeResponse GetMe(ResearchWorkspaceIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ValidateIdentifier(identity.UserId, nameof(identity.UserId));

        return new MeResponse(identity.UserId, identity.Email);
    }

    public static IReadOnlyList<string> GetSupportedRoles() => SupportedRoles;

    public static IReadOnlyList<string> GetSupportedExperimentStatuses() => SupportedExperimentStatuses;

    public static IReadOnlyList<string> GetSupportedRunStatuses() => SupportedRunStatuses;

    public static IReadOnlyList<string> GetSupportedArtifactKinds() => SupportedArtifactKinds;

    public static IReadOnlyList<string> GetSupportedDecisionStatuses() => SupportedDecisionStatuses;

    public async Task<OrganizationSummary> CreateOrganizationAsync(string accessToken, string userId, CreateOrganizationRequest request)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(userId, nameof(userId));
        var normalizedName = NormalizeRequiredText(request.Name, nameof(request.Name));

        var client = _clients.CreateForUser(accessToken);
        await client.Postgrest.Table<ResearchOrganizationRecord>().Insert(
            new ResearchOrganizationRecord
            {
                Name = normalizedName
            },
            new global::Supabase.Postgrest.QueryOptions
            {
                Returning = global::Supabase.Postgrest.QueryOptions.ReturnType.Minimal
            });

        var organization = await GetSingleAsync(
            client.Postgrest.Table<ResearchOrganizationRecord>()
                .Filter("name", global::Supabase.Postgrest.Constants.Operator.Equals, normalizedName)
                .Order(item => item.InsertedAt, global::Supabase.Postgrest.Constants.Ordering.Descending)
                .Limit(1),
            "organization");

        return MapOrganizationSummary(organization, "owner");
    }

    public async Task<IReadOnlyList<OrganizationSummary>> GetOrganizationsAsync(string accessToken)
    {
        ValidateAccessToken(accessToken);
        var client = _clients.CreateForUser(accessToken);
        var memberships = (await client.Postgrest.Table<ResearchMembershipRecord>().Get()).Models;
        var results = new List<OrganizationSummary>(memberships.Count);

        foreach (var membership in memberships)
        {
            var organization = await GetSingleAsync(
                client.Postgrest.Table<ResearchOrganizationRecord>()
                    .Filter("id", global::Supabase.Postgrest.Constants.Operator.Equals, membership.OrganizationId!),
                "organization");

            results.Add(MapOrganizationSummary(organization, membership.Role ?? "viewer"));
        }

        return results
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<MembershipSummary> AddMembershipAsync(string accessToken, string organizationId, AddMembershipRequest request)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(organizationId, nameof(organizationId));
        var userId = ValidateIdentifier(request.UserId, nameof(request.UserId));
        var role = ValidateRole(request.Role);

        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchMembershipRecord>().Insert(new ResearchMembershipRecord
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = role
        });

        return MapMembershipSummary(AssertSingle(response.Models));
    }

    public async Task<IReadOnlyList<MembershipSummary>> GetMembershipsAsync(string accessToken, string organizationId)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(organizationId, nameof(organizationId));
        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchMembershipRecord>()
            .Filter("organization_id", global::Supabase.Postgrest.Constants.Operator.Equals, organizationId)
            .Order(membership => membership.InsertedAt, global::Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(MapMembershipSummary).ToArray();
    }

    public async Task<ProjectSummary> CreateProjectAsync(string accessToken, string organizationId, CreateProjectRequest request)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(organizationId, nameof(organizationId));
        var normalizedName = NormalizeRequiredText(request.Name, nameof(request.Name));

        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchProjectRecord>().Insert(new ResearchProjectRecord
        {
            OrganizationId = organizationId,
            Name = normalizedName,
            Visibility = "private"
        });

        return MapProjectSummary(AssertSingle(response.Models));
    }

    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(string accessToken)
    {
        ValidateAccessToken(accessToken);
        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchProjectRecord>()
            .Order(project => project.InsertedAt, global::Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(MapProjectSummary).ToArray();
    }

    public async Task<ExperimentSummary> CreateExperimentAsync(string accessToken, string projectId, CreateExperimentRequest request)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(projectId, nameof(projectId));
        var normalizedName = NormalizeRequiredText(request.Name, nameof(request.Name));
        var summary = NormalizeOptionalText(request.Summary);
        var status = ValidateExperimentStatus(request.Status);

        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchExperimentRecord>().Insert(new ResearchExperimentRecord
        {
            ProjectId = projectId,
            Name = normalizedName,
            Summary = summary,
            Status = status
        });

        return MapExperimentSummary(AssertSingle(response.Models));
    }

    public async Task<IReadOnlyList<ExperimentSummary>> GetExperimentsAsync(string accessToken, string projectId)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(projectId, nameof(projectId));
        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchExperimentRecord>()
            .Filter("project_id", global::Supabase.Postgrest.Constants.Operator.Equals, projectId)
            .Order(experiment => experiment.InsertedAt, global::Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(MapExperimentSummary).ToArray();
    }

    public async Task<RunSummary> CreateRunAsync(string accessToken, string userId, string experimentId, CreateRunRequest request)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(userId, nameof(userId));
        ValidateIdentifier(experimentId, nameof(experimentId));
        var displayName = NormalizeRequiredText(request.DisplayName, nameof(request.DisplayName));
        var notes = NormalizeOptionalText(request.Notes);
        var status = ValidateRunStatus(request.Status);

        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchRunRecord>().Insert(new ResearchRunRecord
        {
            ExperimentId = experimentId,
            DisplayName = displayName,
            Notes = notes,
            Status = status,
            StartedAt = string.Equals(status, "running", StringComparison.Ordinal) ? DateTime.UtcNow : null,
            CompletedAt = IsTerminalRunStatus(status) ? DateTime.UtcNow : null
        });

        return MapRunSummary(AssertSingle(response.Models));
    }

    public async Task<IReadOnlyList<RunSummary>> GetRunsAsync(string accessToken, string experimentId)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(experimentId, nameof(experimentId));
        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchRunRecord>()
            .Filter("experiment_id", global::Supabase.Postgrest.Constants.Operator.Equals, experimentId)
            .Order(run => run.InsertedAt, global::Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(MapRunSummary).ToArray();
    }

    public async Task<RunSummary> UpdateRunStatusAsync(string accessToken, string runId, UpdateRunStatusRequest request)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(runId, nameof(runId));
        var status = ValidateRunStatus(request.Status);
        var notes = NormalizeOptionalText(request.Notes);

        var client = _clients.CreateForUser(accessToken);
        var run = await GetSingleAsync(
            client.Postgrest.Table<ResearchRunRecord>()
                .Filter("id", global::Supabase.Postgrest.Constants.Operator.Equals, runId),
            "run");

        run.Status = status;
        run.Notes = notes ?? run.Notes;
        run.StartedAt ??= string.Equals(status, "running", StringComparison.Ordinal) ? DateTime.UtcNow : null;
        run.CompletedAt = IsTerminalRunStatus(status) ? DateTime.UtcNow : null;

        var response = await client.Postgrest.Table<ResearchRunRecord>().Update(run);
        return MapRunSummary(AssertSingle(response.Models));
    }

    public async Task<MetricSummary> AppendMetricAsync(string accessToken, string runId, AppendMetricRequest request)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(runId, nameof(runId));
        var metricName = NormalizeRequiredText(request.MetricName, nameof(request.MetricName));
        var metricUnit = NormalizeOptionalText(request.MetricUnit);

        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchRunMetricRecord>().Insert(new ResearchRunMetricRecord
        {
            RunId = runId,
            MetricName = metricName,
            MetricValue = request.MetricValue,
            MetricUnit = metricUnit
        });

        return MapMetricSummary(AssertSingle(response.Models));
    }

    public async Task<IReadOnlyList<MetricSummary>> GetMetricsAsync(string accessToken, string runId)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(runId, nameof(runId));
        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchRunMetricRecord>()
            .Filter("run_id", global::Supabase.Postgrest.Constants.Operator.Equals, runId)
            .Order(metric => metric.InsertedAt, global::Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(MapMetricSummary).ToArray();
    }

    public async Task<ArtifactUploadResponse> CreateArtifactUploadAsync(string accessToken, string userId, string runId, CreateArtifactUploadRequest request)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(userId, nameof(userId));
        ValidateIdentifier(runId, nameof(runId));
        var kind = ValidateArtifactKind(request.Kind);
        var fileName = NormalizeRequiredText(request.FileName, nameof(request.FileName));
        var contentType = NormalizeOptionalText(request.ContentType);

        var client = _clients.CreateForUser(accessToken);
        var context = await ResolveRunContextAsync(client, runId);
        var objectPath = BuildObjectPath(context.OrganizationId, context.ProjectId, runId, fileName);

        var insertResponse = await client.Postgrest.Table<ResearchRunArtifactRecord>().Insert(new ResearchRunArtifactRecord
        {
            RunId = runId,
            StorageBucket = ResearchArtifactsBucketName,
            ObjectPath = objectPath,
            FileName = fileName,
            Kind = kind,
            ContentType = contentType
        });

        var artifact = AssertSingle(insertResponse.Models);
        var upload = await client.Storage.From(ResearchArtifactsBucketName).CreateUploadSignedUrl(objectPath);

        return new ArtifactUploadResponse(
            artifact.Id!,
            ResearchArtifactsBucketName,
            objectPath,
            upload.SignedUrl.ToString(),
            upload.Token,
            kind,
            fileName);
    }

    public async Task<ArtifactSummary> UploadTextArtifactAsync(string accessToken, string userId, string runId, UploadArtifactTextRequest request)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(userId, nameof(userId));
        ValidateIdentifier(runId, nameof(runId));
        var kind = ValidateArtifactKind(request.Kind);
        var fileName = NormalizeRequiredText(request.FileName, nameof(request.FileName));
        var content = NormalizeRequiredText(request.Content, nameof(request.Content));
        var contentType = NormalizeOptionalText(request.ContentType) ?? "text/plain";

        var client = _clients.CreateForUser(accessToken);
        var context = await ResolveRunContextAsync(client, runId);
        var objectPath = BuildObjectPath(context.OrganizationId, context.ProjectId, runId, fileName);

        var insertResponse = await client.Postgrest.Table<ResearchRunArtifactRecord>().Insert(new ResearchRunArtifactRecord
        {
            RunId = runId,
            StorageBucket = ResearchArtifactsBucketName,
            ObjectPath = objectPath,
            FileName = fileName,
            Kind = kind,
            ContentType = contentType
        });

        var artifact = AssertSingle(insertResponse.Models);
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        await client.Storage.From(ResearchArtifactsBucketName).Upload(
            bytes,
            objectPath,
            new global::Supabase.Storage.FileOptions
            {
                ContentType = contentType,
                Upsert = true
            });

        string? downloadUrl = null;

        try
        {
            downloadUrl = await client.Storage.From(ResearchArtifactsBucketName).CreateSignedUrl(objectPath, 3600);
        }
        catch (SupabaseStorageException ex)
        {
            _logger.LogWarning(ex, "Unable to create download URL for uploaded text artifact {ArtifactId}.", artifact.Id);
        }

        return MapArtifactSummary(artifact, downloadUrl);
    }

    public async Task<IReadOnlyList<ArtifactSummary>> GetArtifactsAsync(string accessToken, string runId)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(runId, nameof(runId));
        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchRunArtifactRecord>()
            .Filter("run_id", global::Supabase.Postgrest.Constants.Operator.Equals, runId)
            .Order(artifact => artifact.InsertedAt, global::Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        var bucket = client.Storage.From(ResearchArtifactsBucketName);
        var artifacts = new List<ArtifactSummary>(response.Models.Count);

        foreach (var artifact in response.Models)
        {
            string? downloadUrl = null;

            try
            {
                downloadUrl = await bucket.CreateSignedUrl(artifact.ObjectPath!, 3600);
            }
            catch (SupabaseStorageException)
            {
                // Pending uploads are allowed to exist before the object itself is present.
            }

            artifacts.Add(MapArtifactSummary(artifact, downloadUrl));
        }

        return artifacts;
    }

    public async Task<DecisionSummary> CreateDecisionAsync(string accessToken, string userId, string projectId, CreateDecisionRequest request)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(userId, nameof(userId));
        ValidateIdentifier(projectId, nameof(projectId));
        var title = NormalizeRequiredText(request.Title, nameof(request.Title));
        var summary = NormalizeOptionalText(request.Summary);
        var status = ValidateDecisionStatus(request.Status);
        var experimentId = ValidateOptionalIdentifier(request.ExperimentId, nameof(request.ExperimentId));
        var baselineRunId = ValidateOptionalIdentifier(request.BaselineRunId, nameof(request.BaselineRunId));

        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchDecisionRecord>().Insert(new ResearchDecisionRecord
        {
            ProjectId = projectId,
            ExperimentId = experimentId,
            BaselineRunId = baselineRunId,
            Title = title,
            Summary = summary,
            Status = status
        });

        return MapDecisionSummary(AssertSingle(response.Models));
    }

    public async Task<IReadOnlyList<DecisionSummary>> GetDecisionsAsync(string accessToken, string projectId)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(projectId, nameof(projectId));
        var client = _clients.CreateForUser(accessToken);
        var response = await client.Postgrest.Table<ResearchDecisionRecord>()
            .Filter("project_id", global::Supabase.Postgrest.Constants.Operator.Equals, projectId)
            .Order(decision => decision.InsertedAt, global::Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();

        return response.Models.Select(MapDecisionSummary).ToArray();
    }

    public async Task<PromoteBaselineResponse> PromoteBaselineAsync(string accessToken, string experimentId, PromoteBaselineRequest request)
    {
        ValidateAccessToken(accessToken);
        ValidateIdentifier(experimentId, nameof(experimentId));
        var runId = ValidateIdentifier(request.RunId, nameof(request.RunId));
        var client = _clients.CreateForUser(accessToken);
        var experiment = await GetSingleAsync(
            client.Postgrest.Table<ResearchExperimentRecord>()
                .Filter("id", global::Supabase.Postgrest.Constants.Operator.Equals, experimentId),
            "experiment");

        var result = await client.Functions.Invoke<PromoteBaselineResponse>(
            "research-promote-baseline",
            options: new global::Supabase.Functions.Client.InvokeFunctionOptions
            {
                Body = new Dictionary<string, object>
                {
                    ["projectId"] = experiment.ProjectId!,
                    ["experimentId"] = experimentId,
                    ["runId"] = runId
                }
            });

        if (result is null)
        {
            throw new InvalidOperationException("Function did not return a response body.");
        }

        return result;
    }

    public Task<RunWatchStartedResponse> StartRunWatchAsync(ResearchWorkspaceIdentity identity, string experimentId)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ValidateAccessToken(identity.AccessToken);
        ValidateIdentifier(identity.UserId, nameof(identity.UserId));
        ValidateIdentifier(experimentId, nameof(experimentId));
        return _watchRegistry.StartAsync(identity, experimentId);
    }

    public async Task<RunWatchSnapshot> GetRunWatchSnapshotAsync(string watchId, ResearchWorkspaceIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ValidateIdentifier(watchId, nameof(watchId));
        ValidateIdentifier(identity.UserId, nameof(identity.UserId));
        var snapshot = await _watchRegistry.GetSnapshotAsync(watchId, identity.UserId);
        if (snapshot is null)
        {
            throw new KeyNotFoundException($"Run watch '{watchId}' was not found.");
        }

        return snapshot;
    }

    public async Task DeleteRunWatchAsync(string watchId, ResearchWorkspaceIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ValidateIdentifier(watchId, nameof(watchId));
        ValidateIdentifier(identity.UserId, nameof(identity.UserId));

        var deleted = await _watchRegistry.DeleteAsync(watchId, identity.UserId);
        if (!deleted)
        {
            throw new KeyNotFoundException($"Run watch '{watchId}' was not found.");
        }
    }

    public HealthResponse GetHealth()
    {
        return new HealthResponse(
            true,
            "ResearchWorkspaceApi",
            new[]
            {
                "auth-delegation",
                "rls",
                "postgrest",
                "storage",
                "functions",
                "realtime"
            },
            ResearchArtifactsBucketName,
            "research-promote-baseline",
            _options.Url);
    }

    private static async Task<TRecord> GetSingleAsync<TRecord>(
        global::Supabase.Postgrest.Interfaces.IPostgrestTable<TRecord> table,
        string recordName)
        where TRecord : global::Supabase.Postgrest.Models.BaseModel, new()
    {
        var response = await table.Limit(1).Get();
        var record = response.Models.SingleOrDefault();

        return record ?? throw new KeyNotFoundException($"{recordName} was not found.");
    }

    private static TRecord AssertSingle<TRecord>(IReadOnlyList<TRecord> models)
    {
        if (models.Count != 1)
        {
            throw new InvalidOperationException($"Expected a single model, but received {models.Count}.");
        }

        return models[0];
    }

    private async Task<RunContext> ResolveRunContextAsync(ISupabaseStatelessClient client, string runId)
    {
        var run = await GetSingleAsync(
            client.Postgrest.Table<ResearchRunRecord>()
                .Filter("id", global::Supabase.Postgrest.Constants.Operator.Equals, runId),
            "run");
        var experiment = await GetSingleAsync(
            client.Postgrest.Table<ResearchExperimentRecord>()
                .Filter("id", global::Supabase.Postgrest.Constants.Operator.Equals, run.ExperimentId!),
            "experiment");
        var project = await GetSingleAsync(
            client.Postgrest.Table<ResearchProjectRecord>()
                .Filter("id", global::Supabase.Postgrest.Constants.Operator.Equals, experiment.ProjectId!),
            "project");

        return new RunContext(project.OrganizationId!, project.Id!, experiment.Id!, run.Id!);
    }

    private static string BuildObjectPath(string organizationId, string projectId, string runId, string fileName)
    {
        var sanitizedName = string.Concat(fileName.Trim().Select(ch =>
            char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '-'));
        var finalName = string.IsNullOrWhiteSpace(sanitizedName) ? "artifact.txt" : sanitizedName;

        return $"org/{organizationId}/project/{projectId}/run/{runId}/{Guid.NewGuid():N}-{finalName}";
    }

    private static void ValidateAccessToken(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("A bearer token is required.", nameof(accessToken));
        }
    }

    private static string ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out _))
        {
            throw new ArgumentException($"'{parameterName}' must be a valid UUID.", parameterName);
        }

        return value;
    }

    private static string? ValidateOptionalIdentifier(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ValidateIdentifier(value, parameterName);
    }

    private static string NormalizeRequiredText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{parameterName}' is required.", parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ValidateRole(string role)
    {
        var normalized = NormalizeRequiredText(role, nameof(role));
        if (!SupportedRoles.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported role '{normalized}'.", nameof(role));
        }

        return normalized;
    }

    private static string ValidateExperimentStatus(string status)
    {
        var normalized = NormalizeRequiredText(status, nameof(status));
        if (!SupportedExperimentStatuses.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported experiment status '{normalized}'.", nameof(status));
        }

        return normalized;
    }

    private static string ValidateRunStatus(string status)
    {
        var normalized = NormalizeRequiredText(status, nameof(status));
        if (!SupportedRunStatuses.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported run status '{normalized}'.", nameof(status));
        }

        return normalized;
    }

    private static string ValidateArtifactKind(string kind)
    {
        var normalized = NormalizeRequiredText(kind, nameof(kind));
        if (!SupportedArtifactKinds.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported artifact kind '{normalized}'.", nameof(kind));
        }

        return normalized;
    }

    private static string ValidateDecisionStatus(string status)
    {
        var normalized = NormalizeRequiredText(status, nameof(status));
        if (!SupportedDecisionStatuses.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported decision status '{normalized}'.", nameof(status));
        }

        return normalized;
    }

    private static bool IsTerminalRunStatus(string status)
    {
        return string.Equals(status, "succeeded", StringComparison.Ordinal)
            || string.Equals(status, "failed", StringComparison.Ordinal)
            || string.Equals(status, "canceled", StringComparison.Ordinal);
    }

    private static OrganizationSummary MapOrganizationSummary(ResearchOrganizationRecord organization, string role)
    {
        return new OrganizationSummary(
            organization.Id!,
            organization.Name ?? string.Empty,
            role,
            organization.InsertedAt);
    }

    private static MembershipSummary MapMembershipSummary(ResearchMembershipRecord membership)
    {
        return new MembershipSummary(
            membership.Id!,
            membership.OrganizationId!,
            membership.UserId!,
            membership.Role ?? "viewer",
            membership.InsertedAt);
    }

    private static ProjectSummary MapProjectSummary(ResearchProjectRecord project)
    {
        return new ProjectSummary(
            project.Id!,
            project.OrganizationId!,
            project.Name ?? string.Empty,
            project.Visibility ?? "private",
            project.InsertedAt,
            project.UpdatedAt);
    }

    private static ExperimentSummary MapExperimentSummary(ResearchExperimentRecord experiment)
    {
        return new ExperimentSummary(
            experiment.Id!,
            experiment.ProjectId!,
            experiment.Name ?? string.Empty,
            experiment.Summary,
            experiment.Status ?? "draft",
            experiment.BaselineRunId,
            experiment.InsertedAt,
            experiment.UpdatedAt);
    }

    private static RunSummary MapRunSummary(ResearchRunRecord run)
    {
        return new RunSummary(
            run.Id!,
            run.ExperimentId!,
            run.DisplayName ?? string.Empty,
            run.Notes,
            run.Status ?? "queued",
            run.CreatedBy ?? string.Empty,
            run.InsertedAt,
            run.UpdatedAt,
            run.StartedAt,
            run.CompletedAt);
    }

    private static MetricSummary MapMetricSummary(ResearchRunMetricRecord metric)
    {
        return new MetricSummary(
            metric.Id!,
            metric.RunId!,
            metric.MetricName ?? string.Empty,
            metric.MetricValue,
            metric.MetricUnit,
            metric.InsertedAt);
    }

    private static ArtifactSummary MapArtifactSummary(ResearchRunArtifactRecord artifact, string? downloadUrl)
    {
        return new ArtifactSummary(
            artifact.Id!,
            artifact.RunId!,
            artifact.StorageBucket ?? ResearchArtifactsBucketName,
            artifact.ObjectPath ?? string.Empty,
            artifact.Kind ?? "log",
            artifact.FileName ?? string.Empty,
            artifact.ContentType,
            artifact.UploadedBy ?? string.Empty,
            artifact.InsertedAt,
            downloadUrl);
    }

    private static DecisionSummary MapDecisionSummary(ResearchDecisionRecord decision)
    {
        return new DecisionSummary(
            decision.Id!,
            decision.ProjectId!,
            decision.ExperimentId,
            decision.BaselineRunId,
            decision.Title ?? string.Empty,
            decision.Summary,
            decision.Status ?? "proposed",
            decision.CreatedBy ?? string.Empty,
            decision.InsertedAt,
            decision.UpdatedAt);
    }

    private sealed record RunContext(
        string OrganizationId,
        string ProjectId,
        string ExperimentId,
        string RunId);
}
