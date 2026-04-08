using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrangeDot.Supabase.Internal;
using OrangeDot.Supabase.Observability;

namespace OrangeDot.Supabase;

public sealed class HydratedClient : IDisposable
{
    private readonly LifecycleSnapshot _snapshot;
    private readonly SupabaseRuntimeContext _runtimeContext;
    private bool _disposed;

    internal HydratedClient(LifecycleSnapshot snapshot, SupabaseRuntimeContext runtimeContext)
    {
        _snapshot = snapshot;
        _runtimeContext = runtimeContext;
    }

    public Task<SupabaseClient> InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<SupabaseClient>(cancellationToken);
        }

        var childFactory = new SupabaseChildClientFactory();
        cancellationToken.ThrowIfCancellationRequested();
        var children = childFactory.Create(_snapshot, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var metrics = SupabaseMetrics.TryCreate(_runtimeContext.MeterFactory);
        var loggerFactory = _runtimeContext.LoggerFactory;
        var authBridge = new GotrueAuthStateBridge(
            children.Auth,
            _runtimeContext.AuthStateObserver,
            loggerFactory.CreateLogger<GotrueAuthStateBridge>(),
            metrics);
        cancellationToken.ThrowIfCancellationRequested();
        var headerBinding = new HeaderAuthBinding(
            _runtimeContext.AuthStateObserver,
            children.DynamicAuthHeaders,
            loggerFactory.CreateLogger<HeaderAuthBinding>());
        cancellationToken.ThrowIfCancellationRequested();
        var realtimeBinding = new RealtimeTokenBinding(
            _runtimeContext.AuthStateObserver,
            children.Realtime,
            loggerFactory.CreateLogger<RealtimeTokenBinding>());
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new SupabaseClient(
            _snapshot,
            children,
            authBridge,
            headerBinding,
            realtimeBinding));
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
