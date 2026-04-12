using System.Diagnostics;
using System.Security.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.OpenApi;
using ResearchWorkspaceApi;
using Supabase.Functions.Exceptions;
using Supabase.Postgrest.Exceptions;
using Supabase.Storage.Exceptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Research Workspace API",
        Version = "v1",
        Description = "Sample Supabase-backed research workspace with delegated bearer auth, RLS, storage, functions, and realtime watchers."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Paste a Supabase access token as `Bearer <token>`."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = new List<string>()
    });
    options.OperationFilter<ResearchWorkspaceBearerOperationFilter>();
});
builder.Services.AddHttpClient<ResearchWorkspaceUiAuthService>();
builder.Services.AddSupabaseServer(options =>
{
    options.Url = builder.Configuration["Supabase:Url"];
    options.PublishableKey = builder.Configuration["Supabase:PublishableKey"];
    options.SecretKey = builder.Configuration["Supabase:SecretKey"];
});
builder.Services.AddSingleton<ResearchWorkspaceIdentityResolver>();
builder.Services.AddSingleton<ResearchRunWatchRegistry>();
builder.Services.AddSingleton<ResearchWorkspaceService>();

var app = builder.Build();

app.UseExceptionHandler(handler =>
{
    handler.Run(context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var payload = CreateErrorResponse(exception, context.TraceIdentifier);

        context.Response.StatusCode = payload.Status;
        return context.Response.WriteAsJsonAsync(payload);
    });
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Research Workspace API";
    options.SwaggerEndpoint("/openapi/v1.json", "Research Workspace API v1");
});

DocumentPublic<HealthResponse>(
    app.MapGet("/health", (ResearchWorkspaceService service) => Results.Ok(service.GetHealth())),
    "Health and capability summary",
    "Returns the local sample health payload, including the storage bucket and edge function used by the research workflow.",
    "Sample");

DocumentPublic<UiBootstrapResponse>(
    app.MapGet("/ui/bootstrap", (ResearchWorkspaceService service) => Results.Ok(CreateUiBootstrap(service))),
    "Bootstrap the embedded cockpit UI",
    "Returns static metadata the browser cockpit uses to render option lists, links, and capability badges.",
    "Sample UI");

DocumentPublic<UiSessionResponse>(
    app.MapPost("/ui/auth/signup", async (
        UiPasswordAuthRequest body,
        ResearchWorkspaceUiAuthService authService,
        CancellationToken cancellationToken) =>
    {
        var session = await authService.SignUpAsync(body, cancellationToken);
        return Results.Ok(session);
    }),
    "Create a local user and start a browser session",
    "Creates a local Supabase Auth user through the publishable key flow, then exchanges email and password for an access token the UI stores locally.",
    "Sample UI",
    StatusCodes.Status200OK,
    StatusCodes.Status400BadRequest);

DocumentPublic<UiSessionResponse>(
    app.MapPost("/ui/auth/login", async (
        UiPasswordAuthRequest body,
        ResearchWorkspaceUiAuthService authService,
        CancellationToken cancellationToken) =>
    {
        var session = await authService.SignInAsync(body, cancellationToken);
        return Results.Ok(session);
    }),
    "Exchange email and password for a browser session",
    "Returns a Supabase access token without introducing server-side cookies or sample-specific session state.",
    "Sample UI",
    StatusCodes.Status200OK,
    StatusCodes.Status400BadRequest,
    StatusCodes.Status401Unauthorized);

DocumentProtected<MeResponse>(
    app.MapGet("/me", async (HttpRequest request, ResearchWorkspaceService service, ResearchWorkspaceIdentityResolver identities) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var identity = await identities.ResolveRequiredUserAsync(accessToken);
        return Results.Ok(service.GetMe(identity));
    }),
    "Inspect the delegated caller",
    "Returns the user id and email extracted from the delegated bearer token.");

DocumentProtected<IReadOnlyList<OrganizationSummary>>(
    app.MapGet("/me/organizations", async (HttpRequest request, ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var organizations = await service.GetOrganizationsAsync(accessToken);
        return Results.Ok(organizations);
    }),
    "List visible organizations",
    "Returns the organizations visible to the delegated caller, including the caller's effective membership role.");

DocumentProtected<OrganizationSummary>(
    app.MapPost("/organizations", async (
        HttpRequest request,
        CreateOrganizationRequest body,
        ResearchWorkspaceService service,
        ResearchWorkspaceIdentityResolver identities) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var identity = await identities.ResolveRequiredUserAsync(accessToken);
        var organization = await service.CreateOrganizationAsync(accessToken, identity.UserId, body);
        return Results.Created($"/organizations/{organization.Id}", organization);
    }),
    "Create an organization",
    "Creates a new research organization and returns the newly visible organization summary for the caller.",
    successStatusCode: StatusCodes.Status201Created);

DocumentProtected<IReadOnlyList<MembershipSummary>>(
    app.MapGet("/organizations/{organizationId}/memberships", async (
        HttpRequest request,
        string organizationId,
        ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var memberships = await service.GetMembershipsAsync(accessToken, organizationId);
        return Results.Ok(memberships);
    }),
    "List organization memberships",
    "Returns visible memberships for the selected organization so the browser cockpit can inspect owner, editor, and viewer access.",
    StatusCodes.Status200OK,
    StatusCodes.Status404NotFound);

DocumentProtected<MembershipSummary>(
    app.MapPost("/organizations/{organizationId}/memberships", async (
        HttpRequest request,
        string organizationId,
        AddMembershipRequest body,
        ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var membership = await service.AddMembershipAsync(accessToken, organizationId, body);
        return Results.Created($"/organizations/{organizationId}/memberships/{membership.Id}", membership);
    }),
    "Add an organization membership",
    "Adds a user to an organization with an owner, editor, or viewer role.",
    StatusCodes.Status201Created,
    StatusCodes.Status404NotFound,
    StatusCodes.Status409Conflict);

DocumentProtected<IReadOnlyList<ProjectSummary>>(
    app.MapGet("/projects", async (HttpRequest request, ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var projects = await service.GetProjectsAsync(accessToken);
        return Results.Ok(projects);
    }),
    "List visible projects",
    "Returns all projects the delegated caller can read across their research organizations.");

DocumentProtected<ProjectSummary>(
    app.MapPost("/organizations/{organizationId}/projects", async (
        HttpRequest request,
        string organizationId,
        CreateProjectRequest body,
        ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var project = await service.CreateProjectAsync(accessToken, organizationId, body);
        return Results.Created($"/projects/{project.Id}", project);
    }),
    "Create a project inside an organization",
    "Creates a private project within the selected organization.",
    StatusCodes.Status201Created,
    StatusCodes.Status404NotFound);

DocumentProtected<IReadOnlyList<ExperimentSummary>>(
    app.MapGet("/projects/{projectId}/experiments", async (HttpRequest request, string projectId, ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var experiments = await service.GetExperimentsAsync(accessToken, projectId);
        return Results.Ok(experiments);
    }),
    "List experiments for a project",
    "Returns the experiments that belong to the selected project.",
    StatusCodes.Status200OK,
    StatusCodes.Status404NotFound);

DocumentProtected<ExperimentSummary>(
    app.MapPost("/projects/{projectId}/experiments", async (
        HttpRequest request,
        string projectId,
        CreateExperimentRequest body,
        ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var experiment = await service.CreateExperimentAsync(accessToken, projectId, body);
        return Results.Created($"/experiments/{experiment.Id}", experiment);
    }),
    "Create an experiment",
    "Creates an experiment for the selected project and records its lifecycle status.",
    StatusCodes.Status201Created,
    StatusCodes.Status404NotFound);

DocumentProtected<IReadOnlyList<RunSummary>>(
    app.MapGet("/experiments/{experimentId}/runs", async (HttpRequest request, string experimentId, ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var runs = await service.GetRunsAsync(accessToken, experimentId);
        return Results.Ok(runs);
    }),
    "List runs for an experiment",
    "Returns the research runs tracked under the selected experiment.",
    StatusCodes.Status200OK,
    StatusCodes.Status404NotFound);

DocumentProtected<RunSummary>(
    app.MapPost("/experiments/{experimentId}/runs", async (
        HttpRequest request,
        string experimentId,
        CreateRunRequest body,
        ResearchWorkspaceService service,
        ResearchWorkspaceIdentityResolver identities) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var identity = await identities.ResolveRequiredUserAsync(accessToken);
        var run = await service.CreateRunAsync(accessToken, identity.UserId, experimentId, body);
        return Results.Created($"/runs/{run.Id}", run);
    }),
    "Create a run",
    "Creates a run under the selected experiment and stamps it with the delegated caller identity.",
    StatusCodes.Status201Created,
    StatusCodes.Status404NotFound);

DocumentProtected<RunSummary>(
    app.MapPost("/runs/{runId}/status", async (HttpRequest request, string runId, UpdateRunStatusRequest body, ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var run = await service.UpdateRunStatusAsync(accessToken, runId, body);
        return Results.Ok(run);
    }),
    "Update a run status",
    "Updates the run status and timestamps used by the realtime watcher flow.",
    StatusCodes.Status200OK,
    StatusCodes.Status404NotFound);

DocumentProtected<IReadOnlyList<MetricSummary>>(
    app.MapGet("/runs/{runId}/metrics", async (HttpRequest request, string runId, ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var metrics = await service.GetMetricsAsync(accessToken, runId);
        return Results.Ok(metrics);
    }),
    "List metrics for a run",
    "Returns the metrics that have been recorded for the selected run.",
    StatusCodes.Status200OK,
    StatusCodes.Status404NotFound);

DocumentProtected<MetricSummary>(
    app.MapPost("/runs/{runId}/metrics", async (HttpRequest request, string runId, AppendMetricRequest body, ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var metric = await service.AppendMetricAsync(accessToken, runId, body);
        return Results.Created($"/runs/{runId}/metrics/{metric.Id}", metric);
    }),
    "Append a metric to a run",
    "Records a numeric metric for the selected run.",
    StatusCodes.Status201Created,
    StatusCodes.Status404NotFound);

DocumentProtected<ArtifactUploadResponse>(
    app.MapPost("/runs/{runId}/artifacts/upload-url", async (
        HttpRequest request,
        string runId,
        CreateArtifactUploadRequest body,
        ResearchWorkspaceService service,
        ResearchWorkspaceIdentityResolver identities) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var identity = await identities.ResolveRequiredUserAsync(accessToken);
        var artifact = await service.CreateArtifactUploadAsync(accessToken, identity.UserId, runId, body);
        return Results.Created($"/runs/{runId}/artifacts/{artifact.ArtifactId}", artifact);
    }),
    "Create a signed upload URL for an artifact",
    "Creates a pending artifact record and returns a signed upload URL plus token for client-side upload flows.",
    StatusCodes.Status201Created,
    StatusCodes.Status404NotFound);

DocumentProtected<ArtifactSummary>(
    app.MapPost("/runs/{runId}/artifacts/text", async (
        HttpRequest request,
        string runId,
        UploadArtifactTextRequest body,
        ResearchWorkspaceService service,
        ResearchWorkspaceIdentityResolver identities) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var identity = await identities.ResolveRequiredUserAsync(accessToken);
        var artifact = await service.UploadTextArtifactAsync(accessToken, identity.UserId, runId, body);
        return Results.Created($"/runs/{runId}/artifacts/{artifact.Id}", artifact);
    }),
    "Upload a text artifact",
    "Stores a text artifact in the research bucket and returns a signed download URL when available.",
    StatusCodes.Status201Created,
    StatusCodes.Status404NotFound);

DocumentProtected<IReadOnlyList<ArtifactSummary>>(
    app.MapGet("/runs/{runId}/artifacts", async (HttpRequest request, string runId, ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var artifacts = await service.GetArtifactsAsync(accessToken, runId);
        return Results.Ok(artifacts);
    }),
    "List artifacts for a run",
    "Returns artifact metadata plus signed download URLs for objects the delegated caller is allowed to fetch.",
    StatusCodes.Status200OK,
    StatusCodes.Status404NotFound);

DocumentProtected<IReadOnlyList<DecisionSummary>>(
    app.MapGet("/projects/{projectId}/decisions", async (HttpRequest request, string projectId, ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var decisions = await service.GetDecisionsAsync(accessToken, projectId);
        return Results.Ok(decisions);
    }),
    "List project decisions",
    "Returns decision records tied to the selected project.",
    StatusCodes.Status200OK,
    StatusCodes.Status404NotFound);

DocumentProtected<DecisionSummary>(
    app.MapPost("/projects/{projectId}/decisions", async (
        HttpRequest request,
        string projectId,
        CreateDecisionRequest body,
        ResearchWorkspaceService service,
        ResearchWorkspaceIdentityResolver identities) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var identity = await identities.ResolveRequiredUserAsync(accessToken);
        var decision = await service.CreateDecisionAsync(accessToken, identity.UserId, projectId, body);
        return Results.Created($"/projects/{projectId}/decisions/{decision.Id}", decision);
    }),
    "Create a decision",
    "Creates a decision record that can optionally reference an experiment or baseline run.",
    StatusCodes.Status201Created,
    StatusCodes.Status404NotFound);

DocumentProtected<PromoteBaselineResponse>(
    app.MapPost("/experiments/{experimentId}/baseline", async (
        HttpRequest request,
        string experimentId,
        PromoteBaselineRequest body,
        ResearchWorkspaceService service) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var response = await service.PromoteBaselineAsync(accessToken, experimentId, body);
        return Results.Ok(response);
    }),
    "Promote a run to experiment baseline",
    "Invokes the research baseline promotion edge function after validating experiment visibility through delegated access.",
    StatusCodes.Status200OK,
    StatusCodes.Status404NotFound);

DocumentProtected<RunWatchStartedResponse>(
    app.MapPost("/experiments/{experimentId}/watchers", async (
        HttpRequest request,
        string experimentId,
        ResearchWorkspaceService service,
        ResearchWorkspaceIdentityResolver identities) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var identity = await identities.ResolveRequiredUserAsync(accessToken);
        var response = await service.StartRunWatchAsync(identity, experimentId);
        return Results.Created($"/watchers/{response.WatchId}", response);
    }),
    "Start a realtime run watcher",
    "Starts a realtime watch for run status changes under the selected experiment and returns a caller-scoped watch id.",
    StatusCodes.Status201Created,
    StatusCodes.Status404NotFound);

DocumentProtected<RunWatchSnapshot>(
    app.MapGet("/watchers/{watchId}", async (
        HttpRequest request,
        string watchId,
        ResearchWorkspaceService service,
        ResearchWorkspaceIdentityResolver identities) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var identity = await identities.ResolveRequiredUserAsync(accessToken);
        return Results.Ok(await service.GetRunWatchSnapshotAsync(watchId, identity));
    }),
    "Fetch a watcher snapshot",
    "Returns the current event buffer for a caller-owned watcher.",
    StatusCodes.Status200OK,
    StatusCodes.Status404NotFound);

DocumentProtectedNoContent(
    app.MapDelete("/watchers/{watchId}", async (
        HttpRequest request,
        string watchId,
        ResearchWorkspaceService service,
        ResearchWorkspaceIdentityResolver identities) =>
    {
        var authError = RequireAccessToken(request, out var accessToken);
        if (authError is not null)
        {
            return authError;
        }

        var identity = await identities.ResolveRequiredUserAsync(accessToken);
        await service.DeleteRunWatchAsync(watchId, identity);
        return Results.NoContent();
    }),
    "Delete a watcher",
    "Stops a caller-owned watcher and releases its realtime resources.",
    StatusCodes.Status404NotFound);

app.Run();

static UiBootstrapResponse CreateUiBootstrap(ResearchWorkspaceService service)
{
    var health = service.GetHealth();

    return new UiBootstrapResponse(
        health.Sample,
        health.Url ?? string.Empty,
        "/swagger",
        "/openapi/v1.json",
        health.Bucket,
        health.Function,
        health.Capabilities,
        ResearchWorkspaceService.GetSupportedRoles(),
        ResearchWorkspaceService.GetSupportedExperimentStatuses(),
        ResearchWorkspaceService.GetSupportedRunStatuses(),
        ResearchWorkspaceService.GetSupportedArtifactKinds(),
        ResearchWorkspaceService.GetSupportedDecisionStatuses());
}

static IResult? RequireAccessToken(HttpRequest request, out string accessToken)
{
    if (RequestAuth.TryGetBearerToken(request, out accessToken))
    {
        return null;
    }

    return CreateErrorResult(StatusCodes.Status401Unauthorized, "auth_required", "A bearer token is required.");
}

static RouteHandlerBuilder DocumentPublic<TResponse>(
    RouteHandlerBuilder builder,
    string summary,
    string description,
    string tag,
    int successStatusCode = StatusCodes.Status200OK,
    params int[] additionalErrorStatusCodes)
{
    builder
        .WithTags(tag)
        .WithSummary(summary)
        .WithDescription(description)
        .Produces<TResponse>(successStatusCode)
        .Produces<ApiErrorResponse>(StatusCodes.Status500InternalServerError);

    foreach (var statusCode in additionalErrorStatusCodes.Distinct())
    {
        builder.Produces<ApiErrorResponse>(statusCode);
    }

    return builder;
}

static RouteHandlerBuilder DocumentProtected<TResponse>(
    RouteHandlerBuilder builder,
    string summary,
    string description,
    int successStatusCode = StatusCodes.Status200OK,
    params int[] additionalErrorStatusCodes)
{
    var errorStatuses = new HashSet<int>(additionalErrorStatusCodes)
    {
        StatusCodes.Status400BadRequest,
        StatusCodes.Status401Unauthorized,
        StatusCodes.Status403Forbidden
    };

    builder.WithMetadata(ResearchWorkspaceBearerOperationFilter.RequiresAccessToken);
    return DocumentPublic<TResponse>(builder, summary, description, "Research Workspace", successStatusCode, errorStatuses.ToArray());
}

static RouteHandlerBuilder DocumentProtectedNoContent(
    RouteHandlerBuilder builder,
    string summary,
    string description,
    params int[] additionalErrorStatusCodes)
{
    var errorStatuses = new HashSet<int>(additionalErrorStatusCodes)
    {
        StatusCodes.Status400BadRequest,
        StatusCodes.Status401Unauthorized,
        StatusCodes.Status403Forbidden
    };

    builder
        .WithTags("Research Workspace")
        .WithSummary(summary)
        .WithDescription(description)
        .WithMetadata(ResearchWorkspaceBearerOperationFilter.RequiresAccessToken)
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ApiErrorResponse>(StatusCodes.Status500InternalServerError);

    foreach (var statusCode in errorStatuses.Distinct())
    {
        builder.Produces<ApiErrorResponse>(statusCode);
    }

    return builder;
}

static ApiErrorResponse CreateErrorResponse(Exception? exception, string traceId)
{
    var (status, error, detail) = exception switch
    {
        null => (500, "internal_error", "An unknown error occurred."),
        AuthenticationException ex => (401, "auth_invalid", ex.Message),
        UiAuthException ex => (ex.Status, ex.Error, ex.Detail),
        UnauthorizedAccessException ex => (403, "forbidden", ex.Message),
        ArgumentException ex => (400, "invalid_request", ex.Message),
        KeyNotFoundException ex => (404, "not_found", ex.Message),
        PostgrestException ex => MapError("postgrest_error", ex.StatusCode, ex.Content ?? ex.Message),
        FunctionsException ex => MapError("function_error", ex.StatusCode, ex.Content ?? ex.Message),
        SupabaseStorageException ex => MapError("storage_error", MapStorageStatusCode(ex), ex.Message),
        _ => (500, "internal_error", exception.Message)
    };

    return new ApiErrorResponse(status, error, detail, Activity.Current?.Id ?? traceId);
}

static IResult CreateErrorResult(int status, string error, string detail)
{
    return Results.Json(
        new ApiErrorResponse(status, error, detail, Activity.Current?.Id),
        statusCode: status);
}

static (int Status, string Error, string Detail) MapError(string fallbackError, int statusCode, string detail)
{
    var status = statusCode == 0 ? 500 : statusCode;

    return status switch
    {
        400 => (status, "invalid_request", detail),
        401 => (status, "auth_invalid", detail),
        403 => (status, "forbidden", detail),
        404 => (status, "not_found", detail),
        409 => (status, "conflict", detail),
        _ => (status, fallbackError, detail)
    };
}

static int MapStorageStatusCode(SupabaseStorageException ex)
{
    return ex.Reason switch
    {
        Supabase.Storage.Exceptions.FailureHint.Reason.NotFound => 404,
        Supabase.Storage.Exceptions.FailureHint.Reason.NotAuthorized => 403,
        Supabase.Storage.Exceptions.FailureHint.Reason.InvalidInput => 400,
        _ => 500
    };
}

public partial class Program;
