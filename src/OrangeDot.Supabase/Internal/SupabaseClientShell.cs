using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrangeDot.Supabase.Urls;

namespace OrangeDot.Supabase.Internal;

internal sealed class SupabaseClientShell : ISupabaseClient
{
    private readonly ILogger<SupabaseClientShell> _logger;
    private readonly TaskCompletionSource<SupabaseClient> _readySource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SupabaseClientShell(ILogger<SupabaseClientShell> logger)
    {
        _logger = logger;
    }

    public Task Ready => _readySource.Task;

    public string Url => GetReadyClient().Url;

    public string AnonKey => GetReadyClient().AnonKey;

    public SupabaseUrls Urls => GetReadyClient().Urls;

    internal void SetInitializedClient(SupabaseClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

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
        if (!_readySource.Task.IsCompletedSuccessfully)
        {
            throw new InvalidOperationException(
                "Supabase client is not ready. Await Ready before accessing client state.");
        }

        return _readySource.Task.GetAwaiter().GetResult();
    }
}
