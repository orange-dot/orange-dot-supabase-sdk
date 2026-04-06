using System.Threading.Tasks;

namespace OrangeDot.Supabase;

public sealed class ConfiguredClient
{
    private readonly LifecycleSnapshot _snapshot;

    internal ConfiguredClient(LifecycleSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public Task<HydratedClient> LoadPersistedSessionAsync()
    {
        return Task.FromResult(new HydratedClient(_snapshot));
    }
}
