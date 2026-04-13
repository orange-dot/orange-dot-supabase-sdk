using System.Threading;
using System.Threading.Tasks;

namespace OrangeDot.Supabase;

public interface ISupabaseSessionStore
{
    ValueTask PersistAsync(global::Supabase.Gotrue.Session session, CancellationToken cancellationToken = default);

    ValueTask<global::Supabase.Gotrue.Session?> LoadAsync(CancellationToken cancellationToken = default);

    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}
