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
    private readonly global::Supabase.Gotrue.Session? _restoredSession;
    private bool _disposed;

    // Test-only seam for deterministic cancellation cleanup coverage.
    internal Func<SupabaseChildClients, GotrueAuthStateBridge, HeaderAuthBinding, RealtimeTokenBinding, Task>? BeforeFinalizeTestHookAsync { private get; set; }

    internal HydratedClient(
        LifecycleSnapshot snapshot,
        SupabaseRuntimeContext runtimeContext,
        global::Supabase.Gotrue.Session? restoredSession = null)
    {
        _snapshot = snapshot;
        _runtimeContext = runtimeContext;
        _restoredSession = restoredSession;
    }

    public async Task<SupabaseClient> InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        cancellationToken.ThrowIfCancellationRequested();

        var childFactory = new SupabaseChildClientFactory();
        SupabaseChildClients? children = null;
        GotrueAuthStateBridge? authBridge = null;
        HeaderAuthBinding? headerBinding = null;
        RealtimeTokenBinding? realtimeBinding = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            children = childFactory.Create(_snapshot, _runtimeContext.SessionStore, cancellationToken);
            if (_restoredSession is not null)
            {
                GotrueSessionAccessor.SetCurrentSession(children.Auth, _restoredSession);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var metrics = SupabaseMetrics.TryCreate(_runtimeContext.MeterFactory);
            var loggerFactory = _runtimeContext.LoggerFactory;
            authBridge = new GotrueAuthStateBridge(
                children.Auth,
                _runtimeContext.AuthStateObserver,
                loggerFactory.CreateLogger<GotrueAuthStateBridge>(),
                metrics,
                _runtimeContext.SessionStore);
            cancellationToken.ThrowIfCancellationRequested();
            headerBinding = new HeaderAuthBinding(
                _runtimeContext.AuthStateObserver,
                children.DynamicAuthHeaders,
                loggerFactory.CreateLogger<HeaderAuthBinding>());
            cancellationToken.ThrowIfCancellationRequested();
            realtimeBinding = new RealtimeTokenBinding(
                _runtimeContext.AuthStateObserver,
                children.Realtime,
                loggerFactory.CreateLogger<RealtimeTokenBinding>());

            var beforeFinalizeTestHookAsync = BeforeFinalizeTestHookAsync;

            if (beforeFinalizeTestHookAsync is not null)
            {
                await beforeFinalizeTestHookAsync(children, authBridge, headerBinding, realtimeBinding).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return new SupabaseClient(
                _snapshot,
                children,
                authBridge,
                headerBinding,
                realtimeBinding);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            CleanupPartialInitialization(children, authBridge, headerBinding, realtimeBinding);
            throw;
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static void CleanupPartialInitialization(
        SupabaseChildClients? children,
        GotrueAuthStateBridge? authBridge,
        HeaderAuthBinding? headerBinding,
        RealtimeTokenBinding? realtimeBinding)
    {
        TryCleanup(() => headerBinding?.Dispose());
        TryCleanup(() => realtimeBinding?.Dispose());
        TryCleanup(() => authBridge?.Dispose());
        TryCleanup(() => children?.Auth.Shutdown());
        TryCleanup(() => children?.Realtime.Disconnect());
    }

    private static void TryCleanup(Action? cleanup)
    {
        if (cleanup is null)
        {
            return;
        }

        try
        {
            cleanup();
        }
        catch
        {
            // Preserve the original cancellation path if teardown is only partial.
        }
    }
}
