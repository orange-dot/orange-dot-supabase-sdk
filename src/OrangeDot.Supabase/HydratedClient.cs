using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrangeDot.Supabase.Internal;
using OrangeDot.Supabase.Observability;

namespace OrangeDot.Supabase;

public sealed class HydratedClient
{
    private readonly LifecycleSnapshot _snapshot;
    private readonly SupabaseRuntimeContext _runtimeContext;

    internal HydratedClient(LifecycleSnapshot snapshot, SupabaseRuntimeContext runtimeContext)
    {
        _snapshot = snapshot;
        _runtimeContext = runtimeContext;
    }

    public Task<SupabaseClient> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<SupabaseClient>(cancellationToken);
        }

        var childFactory = new SupabaseChildClientFactory();
        var children = childFactory.Create(_snapshot);
        var metrics = SupabaseMetrics.TryCreate(_runtimeContext.MeterFactory);
        var loggerFactory = _runtimeContext.LoggerFactory;
        var authBridge = new GotrueAuthStateBridge(
            children.Auth,
            _runtimeContext.AuthStateObserver,
            loggerFactory.CreateLogger<GotrueAuthStateBridge>(),
            metrics);
        var headerBinding = new HeaderAuthBinding(
            _runtimeContext.AuthStateObserver,
            children.DynamicAuthHeaders,
            loggerFactory.CreateLogger<HeaderAuthBinding>());
        var realtimeBinding = new RealtimeTokenBinding(
            _runtimeContext.AuthStateObserver,
            children.Realtime,
            loggerFactory.CreateLogger<RealtimeTokenBinding>());

        return Task.FromResult(new SupabaseClient(
            _snapshot,
            children,
            authBridge,
            headerBinding,
            realtimeBinding));
    }
}
