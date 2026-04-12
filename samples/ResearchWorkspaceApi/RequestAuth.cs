namespace ResearchWorkspaceApi;

internal static class RequestAuth
{
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
}
