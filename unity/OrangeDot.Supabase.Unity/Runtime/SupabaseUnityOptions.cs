namespace OrangeDot.Supabase.Unity
{
public sealed class SupabaseUnityOptions
{
    public string ProjectUrl { get; set; } = string.Empty;

    public string AnonKey { get; set; } = string.Empty;

    public string Schema { get; set; } = "public";

    public bool AutoRefreshToken { get; set; } = true;

    public bool RefreshSessionOnInitialize { get; set; } = true;
}
}
