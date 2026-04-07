using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrangeDot.Supabase.Observability;

namespace OrangeDot.Supabase.Tests.Observability;

public sealed class SupabaseObservabilityTests
{
    [Fact]
    public async Task Hosted_startup_emits_supabase_activity()
    {
        List<Activity> activities = [];

        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "Supabase.Client",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity)
        };

        ActivitySource.AddActivityListener(listener);

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSupabase(options =>
                {
                    options.Url = "https://abc.supabase.co";
                    options.AnonKey = "anon-key";
                });
            })
            .Build();

        await host.StartAsync();

        Assert.Contains(
            activities,
            candidate => candidate.Source.Name == "Supabase.Client" &&
                candidate.OperationName == "supabase.startup");
        var activity = activities.Last(candidate =>
            candidate.Source.Name == "Supabase.Client" &&
            candidate.OperationName == "supabase.startup");

        Assert.Equal("Supabase.Client", activity.Source.Name);
        Assert.Equal("supabase.startup", activity.OperationName);
    }

    [Fact]
    public void Supabase_metrics_can_be_created_from_meter_factory()
    {
        using var meterFactory = new TestMeterFactory();
        var metrics = new SupabaseMetrics(meterFactory);

        metrics.RecordStartup("success");
        metrics.RecordAuthStateChange("authenticated");
        metrics.RecordAuthTokenRefresh();
        metrics.RecordAuthBindingFailure("publish");

        var created = Assert.Single(meterFactory.CreatedMeters);
        Assert.Equal("Supabase.Client", created.Name);
        Assert.Equal("0.1.0", created.Version);
    }

    private sealed class TestMeterFactory : IMeterFactory, IDisposable
    {
        private readonly List<Meter> _meters = [];

        public IReadOnlyList<Meter> CreatedMeters => _meters;

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options.Name, options.Version);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in _meters)
            {
                meter.Dispose();
            }
        }
    }
}
