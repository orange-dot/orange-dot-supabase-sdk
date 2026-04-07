using System.Threading.Tasks;
using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase;

public sealed class ConfiguredClient
{
    private readonly LifecycleSnapshot _snapshot;
    private readonly SupabaseRuntimeContext _runtimeContext;

    internal ConfiguredClient(LifecycleSnapshot snapshot, SupabaseRuntimeContext runtimeContext)
    {
        _snapshot = snapshot;
        _runtimeContext = runtimeContext;
    }

    public Task<HydratedClient> LoadPersistedSessionAsync()
    {
        return Task.FromResult(new HydratedClient(_snapshot, _runtimeContext));
    }
}
