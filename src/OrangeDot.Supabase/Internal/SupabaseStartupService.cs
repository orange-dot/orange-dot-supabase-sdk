using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Observability;

namespace OrangeDot.Supabase.Internal;

internal sealed class SupabaseStartupService : IHostedService
{
    private readonly IOptions<SupabaseOptions> _options;
    private readonly SupabaseClientShell _shell;
    private readonly ILogger<SupabaseStartupService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly AuthStateObserver _authStateObserver;
    private readonly IMeterFactory? _meterFactory;
    private readonly IRuntimeTraceSink _traceSink;
    private readonly object _lifecycleGate = new();
    private SupabaseClient? _client;
    private bool _stopping;

    // Test-only seam for deterministic StartAsync/StopAsync overlap coverage.
    internal Func<Task>? BeforePublishTestHookAsync { private get; set; }

    public SupabaseStartupService(
        IOptions<SupabaseOptions> options,
        SupabaseClientShell shell,
        ILogger<SupabaseStartupService> logger,
        ILoggerFactory loggerFactory,
        AuthStateObserver authStateObserver,
        IMeterFactory? meterFactory = null,
        IRuntimeTraceSink? traceSink = null)
    {
        _options = options;
        _shell = shell;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _authStateObserver = authStateObserver;
        _meterFactory = meterFactory;
        _traceSink = traceSink ?? NoOpRuntimeTraceSink.Instance;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = SupabaseTelemetry.Source.StartActivity("supabase.startup");
        activity?.SetTag("supabase.path", "hosted");

        var metrics = SupabaseMetrics.TryCreate(_meterFactory);

        _logger.LogInformation("Starting Supabase hosted initialization.");

        try
        {
            _traceSink.Record(new StartupTraceEvent(StartupTraceKind.StartRequested));
            var runtimeContext = new SupabaseRuntimeContext(
                _authStateObserver,
                _loggerFactory,
                _meterFactory,
                _options.Value.SessionStore ?? NoOpSupabaseSessionStore.Instance,
                _traceSink);
            var configured = SupabaseClient.Configure(_options.Value, runtimeContext);
            var hydrated = await configured.LoadPersistedSessionAsync().ConfigureAwait(false);
            var client = await hydrated.InitializeAsync(cancellationToken).ConfigureAwait(false);
            _traceSink.Record(new StartupTraceEvent(StartupTraceKind.PrePublishWindowEntered));
            var beforePublishTestHookAsync = BeforePublishTestHookAsync;

            if (beforePublishTestHookAsync is not null)
            {
                await beforePublishTestHookAsync().ConfigureAwait(false);
            }

            lock (_lifecycleGate)
            {
                if (_stopping)
                {
                    _traceSink.Record(new StartupTraceEvent(StartupTraceKind.ReadyPublicationSkippedBecauseStopping));
                    client.Dispose();
                    return;
                }

                _client = client;
                _shell.SetInitializedClient(client);
            }

            metrics?.RecordStartup("success");
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Supabase hosted initialization completed.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _traceSink.Record(new StartupTraceEvent(StartupTraceKind.StartCanceled));
            _shell.SetInitializationCanceled(cancellationToken);
            metrics?.RecordStartup("canceled");
            activity?.SetStatus(ActivityStatusCode.Error, "canceled");
            _logger.LogWarning("Supabase hosted initialization canceled.");
            throw;
        }
        catch (Exception exception)
        {
            _traceSink.Record(new StartupTraceEvent(StartupTraceKind.StartFaulted));
            _shell.SetInitializationFailed(exception);
            metrics?.RecordStartup("failure");
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            _logger.LogError(exception, "Supabase hosted initialization failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _traceSink.Record(new StartupTraceEvent(StartupTraceKind.StopRequested));

        lock (_lifecycleGate)
        {
            _stopping = true;
            _client?.Dispose();
            _client = null;

            if (!_shell.Ready.IsCompleted)
            {
                _shell.SetInitializationCanceled(cancellationToken);
            }
        }

        return Task.CompletedTask;
    }
}
