using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Internal;
using GotrueAuthState = global::Supabase.Gotrue.Constants.AuthState;

namespace OrangeDot.Supabase.Tests.Auth;

public sealed class GotrueAuthStateBridgeTests
{
    [Fact]
    public void Bridge_attach_with_existing_session_publishes_authenticated_state_immediately()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        var session = CreateSession("access-token", "refresh-token", 1800);

        SetCurrentSession(auth, session);
        _ = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, metrics: null);

        var authenticated = Assert.IsType<AuthState.Authenticated>(observer.Current);
        Assert.Equal("access-token", authenticated.AccessToken);
        Assert.Equal("refresh-token", authenticated.RefreshToken);
        Assert.Equal(new DateTimeOffset(session.CreatedAt).AddSeconds(session.ExpiresIn), authenticated.ExpiresAt);
    }

    [Fact]
    public void Bridge_attach_with_null_session_leaves_observer_anonymous()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();

        _ = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, metrics: null);

        Assert.IsType<AuthState.Anonymous>(observer.Current);
    }

    [Fact]
    public void Bridge_maps_gotrue_events_to_stable_auth_state()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        _ = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, metrics: null);

        var signedInSession = CreateSession("signed-in-token", "refresh-token", 1800);
        SetCurrentSession(auth, signedInSession);

        auth.NotifyAuthStateChange(GotrueAuthState.SignedIn);

        var authenticated = Assert.IsType<AuthState.Authenticated>(observer.Current);
        Assert.Equal("signed-in-token", authenticated.AccessToken);

        var refreshedSession = CreateSession("refreshed-token", "refresh-token-2", 3600);
        SetCurrentSession(auth, refreshedSession);
        auth.NotifyAuthStateChange(GotrueAuthState.TokenRefreshed);

        authenticated = Assert.IsType<AuthState.Authenticated>(observer.Current);
        Assert.Equal("refreshed-token", authenticated.AccessToken);

        auth.NotifyAuthStateChange(GotrueAuthState.SignedOut);

        Assert.IsType<AuthState.SignedOut>(observer.Current);
    }

    [Fact]
    public void Bridge_swallows_publish_failures_and_records_binding_failure_metric()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        using var meterFactory = new TestMeterFactory();
        using var collector = new LongMeasurementCollector("Supabase.Client");

        using var failingSubscription = observer.Subscribe(state =>
        {
            if (state is AuthState.Authenticated)
            {
                throw new InvalidOperationException("boom");
            }
        });
        _ = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, new OrangeDot.Supabase.Observability.SupabaseMetrics(meterFactory));

        SetCurrentSession(auth, CreateSession("access-token", "refresh-token", 1800));
        var exception = Record.Exception(() => auth.NotifyAuthStateChange(GotrueAuthState.SignedIn));

        Assert.Null(exception);
        Assert.Contains(collector.Measurements, measurement => measurement.Name == "supabase.auth.binding_failures.total");
    }

    [Fact]
    public void Bridge_emits_auth_activity_and_token_refresh_metric()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        using var meterFactory = new TestMeterFactory();
        using var collector = new LongMeasurementCollector("Supabase.Client");
        List<Activity> activities = [];

        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "Supabase.Client",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity)
        };

        ActivitySource.AddActivityListener(listener);

        _ = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, new OrangeDot.Supabase.Observability.SupabaseMetrics(meterFactory));

        SetCurrentSession(auth, CreateSession("refreshed-token", "refresh-token", 3600));
        auth.NotifyAuthStateChange(GotrueAuthState.TokenRefreshed);

        Assert.Contains(activities, activity => activity.OperationName == "supabase.auth.state_change");
        Assert.Contains(collector.Measurements, measurement => measurement.Name == "supabase.auth.token_refresh.total");
        Assert.Contains(collector.Measurements, measurement => measurement.Name == "supabase.auth.state_changes.total");
    }

    private static global::Supabase.Gotrue.Client CreateAuthClient()
    {
        return new global::Supabase.Gotrue.Client(new global::Supabase.Gotrue.ClientOptions
        {
            Url = "https://abc.supabase.co/auth/v1",
            Headers = new Dictionary<string, string>
            {
                ["apikey"] = "anon-key"
            }
        });
    }

    private static global::Supabase.Gotrue.Session CreateSession(string accessToken, string refreshToken, long expiresIn)
    {
        return new global::Supabase.Gotrue.Session
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            CreatedAt = new DateTime(2026, 4, 7, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static void SetCurrentSession(global::Supabase.Gotrue.Client auth, global::Supabase.Gotrue.Session? session)
    {
        var property = typeof(global::Supabase.Gotrue.Client).GetProperty(
            nameof(global::Supabase.Gotrue.Client.CurrentSession),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(property);
        property!.SetValue(auth, session);
    }

    private sealed class TestMeterFactory : IMeterFactory, IDisposable
    {
        private readonly List<Meter> _meters = [];

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

    private sealed class LongMeasurementCollector : IDisposable
    {
        private readonly MeterListener _listener = new();

        public LongMeasurementCollector(string meterName)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                var tagMap = new Dictionary<string, object?>();

                foreach (var tag in tags)
                {
                    tagMap[tag.Key] = tag.Value;
                }

                Measurements.Add(new Measurement(instrument.Name, measurement, tagMap));
            });
            _listener.Start();
        }

        public List<Measurement> Measurements { get; } = [];

        public void Dispose()
        {
            _listener.Dispose();
        }
    }

    private sealed record Measurement(string Name, long Value, IReadOnlyDictionary<string, object?> Tags);
}
