namespace OrangeDot.Supabase;

/// <summary>
/// Creates fresh stateless Supabase clients for server-side usage.
/// Each call returns a new client graph and does not cache underlying HTTP clients.
/// </summary>
public interface ISupabaseStatelessClientFactory
{
    /// <summary>
    /// Creates a fresh stateless client that uses anon-only child-client headers.
    /// </summary>
    ISupabaseStatelessClient CreateAnon();

    /// <summary>
    /// Creates a fresh stateless client whose PostgREST, Storage, and Functions children use the supplied bearer token.
    /// </summary>
    ISupabaseStatelessClient CreateForUser(string accessToken);

    /// <summary>
    /// Creates a fresh stateless client whose PostgREST, Storage, and Functions children use the configured service-role bearer token.
    /// </summary>
    ISupabaseStatelessClient CreateService();
}
