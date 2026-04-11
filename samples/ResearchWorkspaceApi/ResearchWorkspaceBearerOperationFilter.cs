using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ResearchWorkspaceApi;

public sealed class ResearchWorkspaceBearerOperationFilter : IOperationFilter
{
    public static RequiresAccessTokenMetadata RequiresAccessToken { get; } = new();

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<RequiresAccessTokenMetadata>().Any())
        {
            return;
        }

        operation.Security = new List<OpenApiSecurityRequirement>();
    }
}

public sealed class RequiresAccessTokenMetadata;
