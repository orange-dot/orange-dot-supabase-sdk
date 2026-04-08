using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase.Internal;

internal sealed class SupabaseClientShell : ISupabaseClient
{
    private readonly ILogger<SupabaseClientShell> _logger;
    private readonly TaskCompletionSource<SupabaseClient> _readySource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;

    public SupabaseClientShell(ILogger<SupabaseClientShell> logger)
    {
        _logger = logger;
    }

    public Task Ready => _readySource.Task;

    public global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> Auth => GetReadyClient().Auth;

    public global::Supabase.Postgrest.Interfaces.IPostgrestClient Postgrest => GetReadyClient().Postgrest;

    public global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> Realtime => GetReadyClient().Realtime;

    public global::Supabase.Storage.Interfaces.IStorageClient<global::Supabase.Storage.Bucket, global::Supabase.Storage.FileObject> Storage => GetReadyClient().Storage;

    public global::Supabase.Functions.Interfaces.IFunctionsClient Functions => GetReadyClient().Functions;

    public ISupabaseTable<TModel> Table<TModel>()
        where TModel : global::Supabase.Postgrest.Models.BaseModel, new()
        => GetReadyClient().Table<TModel>();

    public string Url => GetReadyClient().Url;

    public string AnonKey => GetReadyClient().AnonKey;

    public SupabaseUrls Urls => GetReadyClient().Urls;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_readySource.Task.IsCompletedSuccessfully)
        {
            _readySource.Task.GetAwaiter().GetResult().Dispose();
            return;
        }

        _readySource.TrySetException(new ObjectDisposedException(nameof(SupabaseClientShell)));
    }

    internal void SetInitializedClient(SupabaseClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (_disposed != 0)
        {
            client.Dispose();
            return;
        }

        if (_readySource.TrySetResult(client))
        {
            _logger.LogInformation("Supabase client readiness completed.");
        }
    }

    internal void SetInitializationFailed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (_readySource.TrySetException(exception))
        {
            _logger.LogError(exception, "Supabase client readiness failed.");
        }
    }

    internal void SetInitializationCanceled(CancellationToken cancellationToken)
    {
        if (_readySource.TrySetCanceled(cancellationToken))
        {
            _logger.LogWarning("Supabase client readiness was canceled.");
        }
    }

    private SupabaseClient GetReadyClient()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        if (!_readySource.Task.IsCompletedSuccessfully)
        {
            throw new InvalidOperationException(
                "Supabase client is not ready. Await Ready before accessing client state.");
        }

        return _readySource.Task.GetAwaiter().GetResult();
    }
}
