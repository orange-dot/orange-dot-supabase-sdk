using System;
using System.Collections.Generic;
using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase.IntegrationTests;

internal sealed class IntegrationRecordingRuntimeTraceSink : IRuntimeTraceSink
{
    private readonly object _gate = new();
    private readonly List<RuntimeTraceEvent> _events = [];

    public void Record(RuntimeTraceEvent traceEvent)
    {
        ArgumentNullException.ThrowIfNull(traceEvent);

        lock (_gate)
        {
            _events.Add(traceEvent);
        }
    }

    internal IReadOnlyList<RuntimeTraceEvent> Snapshot()
    {
        lock (_gate)
        {
            return _events.ToArray();
        }
    }
}
