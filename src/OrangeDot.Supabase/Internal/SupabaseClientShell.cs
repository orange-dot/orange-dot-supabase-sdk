using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase.Internal;

internal sealed class SupabaseClientShell : ISupabaseClient
{
    private readonly ILogger<SupabaseClientShell> _logger;
    private readonly IRuntimeTraceSink _traceSink;
    private readonly TaskCompletionSource<SupabaseClient> _readySource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;

    public SupabaseClientShell(ILogger<SupabaseClientShell> logger, IRuntimeTraceSink? traceSink = null)
    {
        _logger = logger;
        _traceSink = traceSink ?? NoOpRuntimeTraceSink.Instance;
    }

    public Task Ready => _readySource.Task;

    public global::Supabase.Gotrue.Interfaces.IGotrueClient<global::Supabase.Gotrue.User, global::Supabase.Gotrue.Session> Auth => GetReadyClient(nameof(Auth)).Auth;

    public global::Supabase.Postgrest.Interfaces.IPostgrestClient Postgrest => GetReadyClient(nameof(Postgrest)).Postgrest;

    public global::Supabase.Realtime.Interfaces.IRealtimeClient<global::Supabase.Realtime.RealtimeSocket, global::Supabase.Realtime.RealtimeChannel> Realtime => GetReadyClient(nameof(Realtime)).Realtime;

    public global::Supabase.Storage.Interfaces.IStorageClient<global::Supabase.Storage.Bucket, global::Supabase.Storage.FileObject> Storage => GetReadyClient(nameof(Storage)).Storage;

    public global::Supabase.Functions.Interfaces.IFunctionsClient Functions => GetReadyClient(nameof(Functions)).Functions;

    public ISupabaseTable<TModel> Table<TModel>()
        where TModel : global::Supabase.Postgrest.Models.BaseModel, new()
        => GetReadyClient("Table").Table<TModel>();

    public string Url => GetReadyClient(nameof(Url)).Url;

    public string AnonKey => GetReadyClient(nameof(AnonKey)).AnonKey;

    public SupabaseUrls Urls => GetReadyClient(nameof(Urls)).Urls;

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

        if (Volatile.Read(ref _disposed) != 0)
        {
            client.Dispose();
            return;
        }

        if (!_readySource.TrySetResult(client))
        {
            client.Dispose();
            return;
        }

        _traceSink.Record(new LifecycleTraceEvent(LifecycleTraceKind.ReadyCompleted));
        _logger.LogInformation("Supabase client readiness completed.");
    }

    internal void SetInitializationFailed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (_readySource.TrySetException(exception))
        {
            _traceSink.Record(new LifecycleTraceEvent(LifecycleTraceKind.ReadyFaulted));
            _logger.LogError(exception, "Supabase client readiness failed.");
        }
    }

    internal void SetInitializationCanceled(CancellationToken cancellationToken)
    {
        if (_readySource.TrySetCanceled(cancellationToken))
        {
            _traceSink.Record(new LifecycleTraceEvent(LifecycleTraceKind.ReadyCanceled));
            _logger.LogWarning("Supabase client readiness was canceled.");
        }
    }

    private SupabaseClient GetReadyClient(string memberName)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        if (!_readySource.Task.IsCompletedSuccessfully)
        {
            _traceSink.Record(new LifecycleTraceEvent(LifecycleTraceKind.PublicAccessDenied, memberName));
            throw new InvalidOperationException(
                "Supabase client is not ready. Await Ready before accessing client state.");
        }

        _traceSink.Record(new LifecycleTraceEvent(LifecycleTraceKind.PublicAccessAllowed, memberName));
        return _readySource.Task.GetAwaiter().GetResult();
    }
}
