using System;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Auth;

namespace OrangeDot.Supabase.Internal;

internal sealed record SupabaseRuntimeContext
{
    internal SupabaseRuntimeContext(
        AuthStateObserver AuthStateObserver,
        ILoggerFactory LoggerFactory,
        IMeterFactory? MeterFactory,
        ISupabaseSessionStore SessionStore,
        IRuntimeTraceSink? TraceSink = null)
    {
        this.AuthStateObserver = AuthStateObserver;
        this.LoggerFactory = LoggerFactory;
        this.MeterFactory = MeterFactory;
        this.SessionStore = SessionStore;
        this.TraceSink = TraceSink ?? NoOpRuntimeTraceSink.Instance;
    }

    internal AuthStateObserver AuthStateObserver { get; }

    internal ILoggerFactory LoggerFactory { get; }

    internal IMeterFactory? MeterFactory { get; }

    internal ISupabaseSessionStore SessionStore { get; }

    internal IRuntimeTraceSink TraceSink { get; }

    internal static SupabaseRuntimeContext CreateDefault(ISupabaseSessionStore? sessionStore = null)
    {
        return new SupabaseRuntimeContext(
            new AuthStateObserver(),
            NullLoggerFactory.Instance,
            MeterFactory: null,
            sessionStore ?? NoOpSupabaseSessionStore.Instance,
            TraceSink: NoOpRuntimeTraceSink.Instance);
    }
}
