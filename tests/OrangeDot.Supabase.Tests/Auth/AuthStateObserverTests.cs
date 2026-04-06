using System;
using System.Collections.Generic;
using System.Linq;
using OrangeDot.Supabase.Auth;
using Xunit;

namespace OrangeDot.Supabase.Tests.Auth;

public sealed class AuthStateObserverTests
{
    [Fact]
    public void Current_starts_as_anonymous()
    {
        var observer = new AuthStateObserver();

        Assert.IsType<AuthState.Anonymous>(observer.Current);
    }

    [Fact]
    public void Subscribe_immediately_replays_current_state()
    {
        var observer = new AuthStateObserver();
        AuthState? received = null;

        using var subscription = observer.Subscribe(state => received = state);

        Assert.Equal(new AuthState.Anonymous(), received);
    }

    [Fact]
    public void Late_subscriber_receives_latest_published_state_immediately()
    {
        var observer = new AuthStateObserver();
        var published = CreateAuthenticatedState(1);
        observer.Publish(published);

        AuthState? received = null;

        using var subscription = observer.Subscribe(state => received = state);

        Assert.Equal(published, received);
    }

    [Fact]
    public void Publish_updates_current_state()
    {
        var observer = new AuthStateObserver();
        var published = CreateAuthenticatedState(1);

        observer.Publish(published);

        Assert.Equal(published, observer.Current);
    }

    [Fact]
    public void Multiple_subscribers_receive_replay_and_future_publishes()
    {
        var observer = new AuthStateObserver();
        var firstReceived = new List<AuthState>();
        var secondReceived = new List<AuthState>();

        using var first = observer.Subscribe(firstReceived.Add);
        using var second = observer.Subscribe(secondReceived.Add);

        var published = CreateAuthenticatedState(1);
        observer.Publish(published);

        Assert.Equal(2, firstReceived.Count);
        Assert.Equal(2, secondReceived.Count);
        Assert.Equal(new AuthState.Anonymous(), firstReceived[0]);
        Assert.Equal(new AuthState.Anonymous(), secondReceived[0]);
        Assert.Equal(published, firstReceived[1]);
        Assert.Equal(published, secondReceived[1]);
    }

    [Fact]
    public void Unsubscribe_stops_future_notifications()
    {
        var observer = new AuthStateObserver();
        var received = new List<AuthState>();
        var subscription = observer.Subscribe(received.Add);

        subscription.Dispose();
        observer.Publish(CreateAuthenticatedState(1));

        Assert.Single(received);
        Assert.Equal(new AuthState.Anonymous(), received[0]);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var observer = new AuthStateObserver();
        var received = new List<AuthState>();
        var subscription = observer.Subscribe(received.Add);

        subscription.Dispose();
        subscription.Dispose();
        observer.Publish(CreateAuthenticatedState(1));

        Assert.Single(received);
    }

    [Fact]
    public void Unsubscribe_during_publish_does_not_corrupt_dispatch()
    {
        var observer = new AuthStateObserver();
        var firstReceived = new List<AuthState>();
        var secondReceived = new List<AuthState>();
        IDisposable? firstSubscription = null;

        firstSubscription = observer.Subscribe(state =>
        {
            firstReceived.Add(state);

            if (state is AuthState.Authenticated && firstSubscription is not null)
            {
                firstSubscription.Dispose();
            }
        });

        using var secondSubscription = observer.Subscribe(secondReceived.Add);

        observer.Publish(CreateAuthenticatedState(1));
        observer.Publish(CreateAuthenticatedState(2));

        Assert.Equal(2, firstReceived.Count);
        Assert.Equal(3, secondReceived.Count);
        Assert.Equal(new AuthState.Anonymous(), firstReceived[0]);
        Assert.Equal(CreateAuthenticatedState(1), firstReceived[1]);
        Assert.Equal(CreateAuthenticatedState(2), secondReceived[2]);
    }

    [Fact]
    public void Publish_notifies_all_listeners_before_throwing_aggregate_for_single_failure()
    {
        var observer = new AuthStateObserver();
        var received = new List<AuthState>();

        using var good = observer.Subscribe(received.Add);
        using var bad = observer.Subscribe(state =>
        {
            if (state is AuthState.Authenticated)
            {
                throw new InvalidOperationException("boom");
            }
        });

        var published = CreateAuthenticatedState(1);
        var exception = Assert.Throws<AggregateException>(() => observer.Publish(published));

        Assert.Single(exception.InnerExceptions);
        Assert.IsType<InvalidOperationException>(exception.InnerExceptions[0]);
        Assert.Equal([new AuthState.Anonymous(), published], received);
        Assert.Equal(published, observer.Current);
    }

    [Fact]
    public void Publish_notifies_all_listeners_before_throwing_aggregate_for_multiple_failures()
    {
        var observer = new AuthStateObserver();
        var received = new List<AuthState>();

        using var good = observer.Subscribe(received.Add);
        using var badOne = observer.Subscribe(state =>
        {
            if (state is AuthState.Authenticated)
            {
                throw new InvalidOperationException("first");
            }
        });
        using var badTwo = observer.Subscribe(state =>
        {
            if (state is AuthState.Authenticated)
            {
                throw new ArgumentException("second");
            }
        });

        var published = CreateAuthenticatedState(1);
        var exception = Assert.Throws<AggregateException>(() => observer.Publish(published));

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Equal([new AuthState.Anonymous(), published], received);
        Assert.Equal(published, observer.Current);
    }

    [Fact]
    public void Authenticated_to_string_redacts_tokens()
    {
        var state = new AuthState.Authenticated("access-secret", "refresh-secret", DateTimeOffset.UnixEpoch);

        var text = state.ToString();

        Assert.DoesNotContain("access-secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-secret", text, StringComparison.Ordinal);
        Assert.Contains("[redacted]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Subscribe_replay_failure_does_not_leave_zombie_subscription()
    {
        var observer = new AuthStateObserver();
        var failedReplayCalls = 0;

        var replayException = Assert.Throws<InvalidOperationException>(() => observer.Subscribe(_ =>
        {
            failedReplayCalls++;
            throw new InvalidOperationException("replay failed");
        }));

        Assert.Equal("replay failed", replayException.Message);
        Assert.Equal(1, failedReplayCalls);

        var received = new List<AuthState>();
        using var subscription = observer.Subscribe(received.Add);

        observer.Publish(CreateAuthenticatedState(1));

        Assert.Equal(1, failedReplayCalls);
        Assert.Equal([new AuthState.Anonymous(), CreateAuthenticatedState(1)], received);
    }

    [Fact]
    public void Stress_test_preserves_order_and_delivery_counts()
    {
        var observer = new AuthStateObserver();
        var receivedBySubscriber = new List<AuthState>[100];
        var subscriptions = new List<IDisposable>(receivedBySubscriber.Length);

        try
        {
            for (var i = 0; i < receivedBySubscriber.Length; i++)
            {
                var received = new List<AuthState>();
                receivedBySubscriber[i] = received;
                subscriptions.Add(observer.Subscribe(received.Add));
            }

            var publishedStates = Enumerable.Range(1, 1000)
                .Select(CreateAuthenticatedState)
                .ToArray();

            foreach (var state in publishedStates)
            {
                observer.Publish(state);
            }

            Assert.Equal(publishedStates[^1], observer.Current);

            foreach (var received in receivedBySubscriber)
            {
                Assert.Equal(1001, received.Count);
                Assert.Equal(new AuthState.Anonymous(), received[0]);

                for (var i = 0; i < publishedStates.Length; i++)
                {
                    Assert.Equal(publishedStates[i], received[i + 1]);
                }
            }
        }
        finally
        {
            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }
        }
    }

    private static AuthState.Authenticated CreateAuthenticatedState(int sequence) =>
        new(
            $"access-{sequence}",
            $"refresh-{sequence}",
            DateTimeOffset.UnixEpoch.AddMinutes(sequence));
}
