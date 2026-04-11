namespace ResearchWorkspaceApi;

public sealed record ApiErrorResponse(
    int Status,
    string Error,
    string Detail,
    string? TraceId = null);

public sealed record MeResponse(
    string UserId,
    string? Email);

public sealed record HealthResponse(
    bool Ok,
    string Sample,
    IReadOnlyList<string> Capabilities,
    string Bucket,
    string Function,
    string? Url);

public sealed record CreateOrganizationRequest(string Name);

public sealed record OrganizationSummary(
    string Id,
    string Name,
    string Role,
    DateTime InsertedAt);

public sealed record AddMembershipRequest(
    string UserId,
    string Role);

public sealed record MembershipSummary(
    string Id,
    string OrganizationId,
    string UserId,
    string Role,
    DateTime InsertedAt);

public sealed record UiBootstrapResponse(
    string Sample,
    string SupabaseUrl,
    string SwaggerUrl,
    string OpenApiUrl,
    string Bucket,
    string Function,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> ExperimentStatuses,
    IReadOnlyList<string> RunStatuses,
    IReadOnlyList<string> ArtifactKinds,
    IReadOnlyList<string> DecisionStatuses);

public sealed record UiPasswordAuthRequest(
    string Email,
    string Password);

public sealed record UiSessionResponse(
    string UserId,
    string Email,
    string AccessToken,
    string TokenType,
    int ExpiresIn);

public sealed record CreateProjectRequest(string Name);

public sealed record ProjectSummary(
    string Id,
    string OrganizationId,
    string Name,
    string Visibility,
    DateTime InsertedAt,
    DateTime UpdatedAt);

public sealed record CreateExperimentRequest(
    string Name,
    string? Summary = null,
    string Status = "active");

public sealed record ExperimentSummary(
    string Id,
    string ProjectId,
    string Name,
    string? Summary,
    string Status,
    string? BaselineRunId,
    DateTime InsertedAt,
    DateTime UpdatedAt);

public sealed record CreateRunRequest(
    string DisplayName,
    string? Notes = null,
    string Status = "queued");

public sealed record UpdateRunStatusRequest(
    string Status,
    string? Notes = null);

public sealed record RunSummary(
    string Id,
    string ExperimentId,
    string DisplayName,
    string? Notes,
    string Status,
    string CreatedBy,
    DateTime InsertedAt,
    DateTime UpdatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public sealed record AppendMetricRequest(
    string MetricName,
    double MetricValue,
    string? MetricUnit = null);

public sealed record MetricSummary(
    string Id,
    string RunId,
    string MetricName,
    double MetricValue,
    string? MetricUnit,
    DateTime InsertedAt);

public sealed record CreateArtifactUploadRequest(
    string Kind,
    string FileName,
    string? ContentType = null);

public sealed record UploadArtifactTextRequest(
    string Kind,
    string FileName,
    string Content,
    string ContentType = "text/plain");

public sealed record ArtifactUploadResponse(
    string ArtifactId,
    string Bucket,
    string ObjectPath,
    string UploadUrl,
    string UploadToken,
    string Kind,
    string FileName);

public sealed record ArtifactSummary(
    string Id,
    string RunId,
    string Bucket,
    string ObjectPath,
    string Kind,
    string FileName,
    string? ContentType,
    string UploadedBy,
    DateTime InsertedAt,
    string? DownloadUrl);

public sealed record PromoteBaselineRequest(string RunId);

public sealed record PromoteBaselineResponse(
    bool Ok,
    string Function,
    string ProjectId,
    string ExperimentId,
    string PromotedRunId);

public sealed record CreateDecisionRequest(
    string Title,
    string? Summary = null,
    string Status = "proposed",
    string? ExperimentId = null,
    string? BaselineRunId = null);

public sealed record DecisionSummary(
    string Id,
    string ProjectId,
    string? ExperimentId,
    string? BaselineRunId,
    string Title,
    string? Summary,
    string Status,
    string CreatedBy,
    DateTime InsertedAt,
    DateTime UpdatedAt);

public sealed record RunWatchStartedResponse(
    string WatchId,
    string ExperimentId,
    int EventCount);

public sealed record RunWatchSnapshot(
    string WatchId,
    string ExperimentId,
    bool Connected,
    IReadOnlyList<RunWatchEvent> Events);

public sealed record RunWatchEvent(
    string EventType,
    string RunId,
    string Status,
    DateTime ObservedAt);
