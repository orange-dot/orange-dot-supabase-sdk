using System;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Auth;

namespace OrangeDot.Supabase.Internal;

internal sealed record SupabaseRuntimeContext(
    AuthStateObserver AuthStateObserver,
    ILoggerFactory LoggerFactory,
    IMeterFactory? MeterFactory,
    ISupabaseSessionStore SessionStore)
{
    internal static SupabaseRuntimeContext CreateDefault(ISupabaseSessionStore? sessionStore = null)
    {
        return new SupabaseRuntimeContext(
            new AuthStateObserver(),
            NullLoggerFactory.Instance,
            MeterFactory: null,
            sessionStore ?? NoOpSupabaseSessionStore.Instance);
    }
}
