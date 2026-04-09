using System.Threading;
using System.Threading.Tasks;

namespace OrangeDot.Supabase.Internal;

internal sealed class NoOpSupabaseSessionStore : ISupabaseSessionStore
{
    internal static NoOpSupabaseSessionStore Instance { get; } = new();

    private NoOpSupabaseSessionStore()
    {
    }

    public ValueTask PersistAsync(global::Supabase.Gotrue.Session session, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask<global::Supabase.Gotrue.Session?> LoadAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<global::Supabase.Gotrue.Session?>(null);
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
