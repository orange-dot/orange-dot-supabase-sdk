using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase;

internal sealed record LifecycleSnapshot(
    string Url,
    string AnonKey,
    SupabaseUrls Urls);
