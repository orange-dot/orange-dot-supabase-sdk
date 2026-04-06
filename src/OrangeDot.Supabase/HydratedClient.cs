using System.Threading;
using System.Threading.Tasks;

namespace OrangeDot.Supabase;

public sealed class HydratedClient
{
    private readonly LifecycleSnapshot _snapshot;

    internal HydratedClient(LifecycleSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public Task<SupabaseClient> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<SupabaseClient>(cancellationToken);
        }

        return Task.FromResult(new SupabaseClient(_snapshot));
    }
}
