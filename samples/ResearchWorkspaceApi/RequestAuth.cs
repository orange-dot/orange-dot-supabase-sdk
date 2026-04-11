using System.IdentityModel.Tokens.Jwt;
using System.Security.Authentication;

namespace ResearchWorkspaceApi;

internal static class RequestAuth
{
    private static readonly JwtSecurityTokenHandler TokenHandler = new();

    internal static bool TryGetBearerToken(HttpRequest request, out string accessToken)
    {
        accessToken = string.Empty;

        if (!request.Headers.TryGetValue("Authorization", out var authorizationValues))
        {
            return false;
        }

        var authorization = authorizationValues.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        accessToken = token;
        return true;
    }

    internal static string GetRequiredUserId(string accessToken)
    {
        var jwt = ReadBearerToken(accessToken);
        var userId = jwt.Subject?.Trim();

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new AuthenticationException("Bearer token did not contain a subject.");
        }

        return userId;
    }

    internal static string? TryGetEmail(string accessToken)
    {
        try
        {
            var jwt = ReadBearerToken(accessToken);
            return jwt.Claims.FirstOrDefault(claim => string.Equals(claim.Type, "email", StringComparison.Ordinal))?.Value;
        }
        catch (AuthenticationException)
        {
            return null;
        }
    }

    private static JwtSecurityToken ReadBearerToken(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || !TokenHandler.CanReadToken(accessToken))
        {
            throw new AuthenticationException("Bearer token was malformed.");
        }

        try
        {
            return TokenHandler.ReadJwtToken(accessToken);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            throw new AuthenticationException("Bearer token was malformed.", ex);
        }
    }
}
