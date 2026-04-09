using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Errors;
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
        Assert.Equal(1, authenticated.CanonicalVersion);
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
        var received = new List<AuthState>();
        using var subscription = observer.Subscribe(received.Add);
        using var bridge = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, metrics: null);

        var signedInSession = CreateSession("signed-in-token", "refresh-token", 1800);
        SetCurrentSession(auth, signedInSession);

        auth.NotifyAuthStateChange(GotrueAuthState.SignedIn);

        var authenticated = Assert.IsType<AuthState.Authenticated>(observer.Current);
        Assert.Equal(1, authenticated.CanonicalVersion);
        Assert.Equal("signed-in-token", authenticated.AccessToken);

        var refreshedSession = CreateSession("refreshed-token", "refresh-token-2", 3600);
        SetCurrentSession(auth, refreshedSession);
        auth.NotifyAuthStateChange(GotrueAuthState.TokenRefreshed);

        authenticated = Assert.IsType<AuthState.Authenticated>(observer.Current);
        Assert.Equal(2, authenticated.CanonicalVersion);
        Assert.Equal("refreshed-token", authenticated.AccessToken);
        Assert.Contains(received, state => state is AuthState.Refreshing refreshing && refreshing.PendingRefreshVersion == 2);

        auth.NotifyAuthStateChange(GotrueAuthState.SignedOut);

        var signedOut = Assert.IsType<AuthState.SignedOut>(observer.Current);
        Assert.Equal(2, signedOut.CanonicalVersion);
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

    [Fact]
    public void Bridge_maps_user_updated_to_new_authenticated_version()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        using var bridge = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, metrics: null);

        SetCurrentSession(auth, CreateSession("signed-in-token", "refresh-token", 1800));
        auth.NotifyAuthStateChange(GotrueAuthState.SignedIn);
        SetCurrentSession(auth, CreateSession("updated-token", "refresh-token", 1800));
        auth.NotifyAuthStateChange(GotrueAuthState.UserUpdated);

        var authenticated = Assert.IsType<AuthState.Authenticated>(observer.Current);
        Assert.Equal(2, authenticated.CanonicalVersion);
        Assert.Equal("updated-token", authenticated.AccessToken);
    }

    [Fact]
    public void Bridge_deduplicates_signed_in_after_user_updated_for_same_session()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        var store = new RecordingSessionStore();
        var received = new List<AuthState>();
        using var subscription = observer.Subscribe(received.Add);
        using var bridge = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, metrics: null, store);

        SetCurrentSession(auth, CreateSession("signed-in-token", "refresh-token", 1800));
        auth.NotifyAuthStateChange(GotrueAuthState.UserUpdated);
        auth.NotifyAuthStateChange(GotrueAuthState.SignedIn);

        var authenticatedStates = received.OfType<AuthState.Authenticated>().ToArray();

        Assert.Single(authenticatedStates);
        Assert.Equal(1, authenticatedStates[0].CanonicalVersion);
        Assert.Equal(1, store.PersistCalls);
    }

    [Fact]
    public void Bridge_ignores_duplicate_signed_out_events()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        var store = new RecordingSessionStore();
        var received = new List<AuthState>();
        using var subscription = observer.Subscribe(received.Add);
        using var bridge = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, metrics: null, store);

        SetCurrentSession(auth, CreateSession("signed-in-token", "refresh-token", 1800));
        auth.NotifyAuthStateChange(GotrueAuthState.SignedIn);
        auth.NotifyAuthStateChange(GotrueAuthState.SignedOut);
        auth.NotifyAuthStateChange(GotrueAuthState.SignedOut);

        Assert.Single(received.OfType<AuthState.SignedOut>());
        Assert.Equal(1, store.ClearCalls);
    }

    [Fact]
    public void Bridge_publishes_faulted_state_when_refresh_event_has_no_valid_session()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        using var bridge = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, metrics: null);

        SetCurrentSession(auth, CreateSession("signed-in-token", "refresh-token", 1800));
        auth.NotifyAuthStateChange(GotrueAuthState.SignedIn);
        SetCurrentSession(auth, null);
        auth.NotifyAuthStateChange(GotrueAuthState.TokenRefreshed);

        var faulted = Assert.IsType<AuthState.Faulted>(observer.Current);
        Assert.Equal(1, faulted.CanonicalVersion);
        Assert.Equal(0, faulted.PendingRefreshVersion);
    }

    [Fact]
    public void Bridge_dispose_removes_registered_listener()
    {
        var auth = CreateAuthClient();
        var initialCount = GetAuthListenerCount(auth);
        var observer = new AuthStateObserver();
        var bridge = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, metrics: null);

        Assert.Equal(initialCount + 1, GetAuthListenerCount(auth));

        bridge.Dispose();

        Assert.Equal(initialCount, GetAuthListenerCount(auth));
    }

    [Fact]
    public void Bridge_persists_latest_session_and_clears_store_on_sign_out()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        var store = new RecordingSessionStore();
        using var bridge = new GotrueAuthStateBridge(auth, observer, NullLogger<GotrueAuthStateBridge>.Instance, metrics: null, store);

        SetCurrentSession(auth, CreateSession("signed-in-token", "refresh-token", 1800));
        auth.NotifyAuthStateChange(GotrueAuthState.SignedIn);

        Assert.NotNull(store.CurrentSession);
        Assert.Equal("signed-in-token", store.CurrentSession!.AccessToken);
        Assert.Equal(1, store.PersistCalls);
        Assert.Equal(0, store.ClearCalls);

        SetCurrentSession(auth, CreateSession("refreshed-token", "refresh-token-2", 3600));
        auth.NotifyAuthStateChange(GotrueAuthState.TokenRefreshed);

        Assert.NotNull(store.CurrentSession);
        Assert.Equal("refreshed-token", store.CurrentSession!.AccessToken);
        Assert.Equal(2, store.PersistCalls);
        Assert.Equal(0, store.ClearCalls);

        auth.NotifyAuthStateChange(GotrueAuthState.SignedOut);

        Assert.Null(store.CurrentSession);
        Assert.Equal(1, store.ClearCalls);
    }

    [Fact]
    public void Bridge_session_store_failure_does_not_publish_signed_in_state_when_gotrue_swallows_listener_exception()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        using var bridge = new GotrueAuthStateBridge(
            auth,
            observer,
            NullLogger<GotrueAuthStateBridge>.Instance,
            metrics: null,
            new ThrowingSessionStore(persistException: new InvalidOperationException("persist failed")));

        SetCurrentSession(auth, CreateSession("signed-in-token", "refresh-token", 1800));

        var exception = Record.Exception(() => auth.NotifyAuthStateChange(GotrueAuthState.SignedIn));

        Assert.Null(exception);
        Assert.IsType<AuthState.Anonymous>(observer.Current);
    }

    [Fact]
    public void Bridge_records_binding_failure_metric_when_session_store_persistence_fails()
    {
        var auth = CreateAuthClient();
        var observer = new AuthStateObserver();
        using var meterFactory = new TestMeterFactory();
        using var collector = new LongMeasurementCollector("Supabase.Client");
        using var bridge = new GotrueAuthStateBridge(
            auth,
            observer,
            NullLogger<GotrueAuthStateBridge>.Instance,
            new OrangeDot.Supabase.Observability.SupabaseMetrics(meterFactory),
            new ThrowingSessionStore(persistException: new InvalidOperationException("persist failed")));

        SetCurrentSession(auth, CreateSession("signed-in-token", "refresh-token", 1800));
        var exception = Record.Exception(() => auth.NotifyAuthStateChange(GotrueAuthState.SignedIn));

        Assert.Null(exception);
        Assert.Contains(
            collector.Measurements,
            measurement => measurement.Name == "supabase.auth.binding_failures.total" &&
                           Equals(measurement.Tags["stage"], "session_store"));
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

    private static int GetAuthListenerCount(global::Supabase.Gotrue.Client auth)
    {
        var field = typeof(global::Supabase.Gotrue.Client).GetField(
            "_authEventHandlers",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        return Assert.IsAssignableFrom<System.Collections.ICollection>(field!.GetValue(auth)).Count;
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

    private class RecordingSessionStore : ISupabaseSessionStore
    {
        public global::Supabase.Gotrue.Session? CurrentSession { get; private set; }

        public int PersistCalls { get; private set; }

        public int ClearCalls { get; private set; }

        public virtual ValueTask PersistAsync(global::Supabase.Gotrue.Session session, CancellationToken cancellationToken = default)
        {
            PersistCalls++;
            CurrentSession = session;
            return ValueTask.CompletedTask;
        }

        public ValueTask<global::Supabase.Gotrue.Session?> LoadAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(CurrentSession);
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            ClearCalls++;
            CurrentSession = null;
            return ValueTask.CompletedTask;
        }

        protected void SetCurrentSession(global::Supabase.Gotrue.Session? session)
        {
            CurrentSession = session;
        }
    }

    private sealed class ThrowingSessionStore : ISupabaseSessionStore
    {
        private readonly Exception? _persistException;
        private readonly Exception? _clearException;

        public ThrowingSessionStore(Exception? persistException = null, Exception? clearException = null)
        {
            _persistException = persistException;
            _clearException = clearException;
        }

        public ValueTask PersistAsync(global::Supabase.Gotrue.Session session, CancellationToken cancellationToken = default)
        {
            if (_persistException is not null)
            {
                throw _persistException;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask<global::Supabase.Gotrue.Session?> LoadAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<global::Supabase.Gotrue.Session?>(null);
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            if (_clearException is not null)
            {
                throw _clearException;
            }

            return ValueTask.CompletedTask;
        }
    }

}
