using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace OrangeDot.Supabase.Observability;

internal sealed class SupabaseMetrics
{
    private readonly Counter<long> _startupCounter;
    private readonly Counter<long> _authStateChangesCounter;
    private readonly Counter<long> _authTokenRefreshCounter;
    private readonly Counter<long> _authBindingFailuresCounter;

    internal SupabaseMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(new MeterOptions("Supabase.Client")
        {
            Version = "0.1.0"
        });

        _startupCounter = meter.CreateCounter<long>("supabase.startup.total");
        _authStateChangesCounter = meter.CreateCounter<long>("supabase.auth.state_changes.total");
        _authTokenRefreshCounter = meter.CreateCounter<long>("supabase.auth.token_refresh.total");
        _authBindingFailuresCounter = meter.CreateCounter<long>("supabase.auth.binding_failures.total");
    }

    internal static SupabaseMetrics? TryCreate(IMeterFactory? meterFactory)
    {
        return meterFactory is null ? null : new SupabaseMetrics(meterFactory);
    }

    internal void RecordStartup(string outcome)
    {
        _startupCounter.Add(
            1,
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    internal void RecordAuthStateChange(string state)
    {
        _authStateChangesCounter.Add(
            1,
            new KeyValuePair<string, object?>("state", state));
    }

    internal void RecordAuthTokenRefresh()
    {
        _authTokenRefreshCounter.Add(1);
    }

    internal void RecordAuthBindingFailure(string stage)
    {
        _authBindingFailuresCounter.Add(
            1,
            new KeyValuePair<string, object?>("stage", stage));
    }
}
