namespace OrangeDot.Supabase;

public sealed class SupabaseOptions
{
    private string? _publishableKey;
    private string? _anonKey;

    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the preferred Supabase project publishable key used as the static API key.
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

    public ISupabaseSessionStore? SessionStore { get; set; }

    internal string? ConfiguredPublishableKey => _publishableKey;

    internal string? ConfiguredAnonKey => _anonKey;
}
