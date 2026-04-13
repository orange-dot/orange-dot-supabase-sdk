namespace OrangeDot.Supabase.Internal;

internal sealed record SupabaseChildClients(
    DynamicAuthHeaders DynamicAuthHeaders,
    global::Supabase.Gotrue.Client Auth,
    global::Supabase.Postgrest.Client Postgrest,
    global::Supabase.Realtime.Client Realtime,
    global::Supabase.Storage.Client Storage,
    global::Supabase.Functions.Client Functions);
