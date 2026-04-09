using System;
using System.Threading.Tasks;
using OrangeDot.Supabase.Errors;
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

    public async Task<HydratedClient> LoadPersistedSessionAsync()
    {
        ThrowIfDisposed();

        try
        {
            var restoredSession = await _runtimeContext.SessionStore.LoadAsync().ConfigureAwait(false);

            if (restoredSession is not null &&
                !GotrueAuthStateBridge.TryCreateSessionSnapshot(restoredSession, out _))
            {
                throw new SupabaseAuthException(
                    SupabaseErrorCode.AuthSessionLoadFailed,
                    "Persisted auth session is invalid.",
                    operation: nameof(LoadPersistedSessionAsync));
            }

            return new HydratedClient(_snapshot, _runtimeContext, restoredSession);
        }
        catch (SupabaseAuthException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SupabaseAuthException(
                SupabaseErrorCode.AuthSessionLoadFailed,
                "Failed to load persisted auth session.",
                operation: nameof(LoadPersistedSessionAsync),
                innerException: exception);
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
}
