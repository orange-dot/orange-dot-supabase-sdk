using System;
using System.Threading.Tasks;
using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase;

public sealed class ConfiguredClient : IDisposable
{
    private readonly LifecycleSnapshot _snapshot;
    private readonly SupabaseRuntimeContext _runtimeContext;
    private bool _disposed;

    internal ConfiguredClient(LifecycleSnapshot snapshot, SupabaseRuntimeContext runtimeContext)
    {
        _snapshot = snapshot;
        _runtimeContext = runtimeContext;
    }

    public Task<HydratedClient> LoadPersistedSessionAsync()
    {
        ThrowIfDisposed();
        return Task.FromResult(new HydratedClient(_snapshot, _runtimeContext));
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
