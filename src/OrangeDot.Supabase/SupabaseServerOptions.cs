namespace OrangeDot.Supabase;

public sealed class SupabaseServerOptions
{
    /// <summary>
    /// Gets or sets the Supabase base URL used to derive child-module endpoints.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the project anon key used as the static API key for all server-created clients.
    /// </summary>
    public string? AnonKey { get; set; }

    /// <summary>
    /// Gets or sets the optional service-role key used by <see cref="ISupabaseStatelessClientFactory.CreateService"/>.
    /// </summary>
    public string? ServiceRoleKey { get; set; }
}
