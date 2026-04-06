using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace OrangeDot.Supabase.Observability;

internal sealed class SupabaseMetrics
{
    private readonly Counter<long> _startupCounter;

    internal SupabaseMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create(new MeterOptions("Supabase.Client")
        {
            Version = "0.1.0"
        });

        _startupCounter = meter.CreateCounter<long>("supabase.startup.total");
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
}
