using System.Security.Authentication;
using OrangeDot.Supabase;
using Supabase.Gotrue.Exceptions;

namespace ResearchWorkspaceApi;

public sealed class ResearchWorkspaceIdentityResolver
{
    private readonly ISupabaseStatelessClientFactory _clients;

    public ResearchWorkspaceIdentityResolver(ISupabaseStatelessClientFactory clients)
    {
        _clients = clients;
    }

    public async Task<ResearchWorkspaceIdentity> ResolveRequiredUserAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new AuthenticationException("A bearer token is required.");
        }

        try
        {
            var client = _clients.CreateAnon();
            var user = await client.Auth.GetUser(accessToken, client.AuthOptions);
            var userId = user?.Id?.Trim();

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new AuthenticationException("Bearer token did not resolve to a user.");
            }

            return new ResearchWorkspaceIdentity(
                userId,
                string.IsNullOrWhiteSpace(user.Email) ? null : user.Email.Trim(),
                accessToken);
        }
        catch (GotrueException ex) when (ex.StatusCode is 401 or 403 or 422)
        {
            throw new AuthenticationException("Bearer token is invalid or expired.", ex);
        }
        catch (ArgumentException ex)
        {
            throw new AuthenticationException("Bearer token was malformed.", ex);
        }
    }
}

public sealed record ResearchWorkspaceIdentity(
    string UserId,
    string? Email,
    string AccessToken);
