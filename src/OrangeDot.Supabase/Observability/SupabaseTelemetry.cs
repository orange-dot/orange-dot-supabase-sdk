using System.Diagnostics;

namespace OrangeDot.Supabase.Observability;

internal static class SupabaseTelemetry
{
    internal static readonly ActivitySource Source = new("Supabase.Client", "0.1.0");
}
