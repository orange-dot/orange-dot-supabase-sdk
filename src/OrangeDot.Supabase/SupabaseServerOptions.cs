namespace OrangeDot.Supabase;

public sealed class SupabaseServerOptions
{
    private string? _publishableKey;
    private string? _anonKey;
    private string? _secretKey;
    private string? _serviceRoleKey;

    /// <summary>
    /// Gets or sets the Supabase base URL used to derive child-module endpoints.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the preferred Supabase project publishable key used as the static API key for all server-created clients.
    /// </summary>
    public string? PublishableKey
    {
        get => _publishableKey;
        set => _publishableKey = value;
    }

    /// <summary>
    /// Gets or sets the legacy alias for <see cref="PublishableKey"/>.
    /// </summary>
    [Obsolete("Use PublishableKey instead. AnonKey will be removed in a future major version.")]
    public string? AnonKey
    {
        get => _anonKey;
        set => _anonKey = value;
    }

    /// <summary>
    /// Gets or sets the preferred privileged Supabase secret key used by <see cref="ISupabaseStatelessClientFactory.CreateService"/>.
    /// </summary>
    public string? SecretKey
    {
        get => _secretKey;
        set => _secretKey = value;
    }

    /// <summary>
    /// Gets or sets the legacy alias for <see cref="SecretKey"/>.
    /// </summary>
    [Obsolete("Use SecretKey instead. ServiceRoleKey will be removed in a future major version.")]
    public string? ServiceRoleKey
    {
        get => _serviceRoleKey;
        set => _serviceRoleKey = value;
    }

    internal string? ConfiguredPublishableKey => _publishableKey;

    internal string? ConfiguredAnonKey => _anonKey;

    internal string? ConfiguredSecretKey => _secretKey;

    internal string? ConfiguredServiceRoleKey => _serviceRoleKey;
}
