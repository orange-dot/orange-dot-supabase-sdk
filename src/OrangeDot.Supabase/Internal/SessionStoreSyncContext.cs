using System;
using System.Threading;

namespace OrangeDot.Supabase.Internal;

internal static class SessionStoreSyncContext
{
    private static readonly AsyncLocal<int> SuppressionDepth = new();

    internal static bool IsBridgePersistenceSuppressed => SuppressionDepth.Value > 0;

    internal static IDisposable SuppressBridgePersistence()
    {
        SuppressionDepth.Value++;
        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (SuppressionDepth.Value > 0)
            {
                SuppressionDepth.Value--;
            }
        }
    }
}
