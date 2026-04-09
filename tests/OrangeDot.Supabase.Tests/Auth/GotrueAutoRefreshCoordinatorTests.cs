using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Internal;

namespace OrangeDot.Supabase.Tests.Auth;

public sealed class GotrueAutoRefreshCoordinatorTests
{
    [Fact]
    public async Task Coordinator_replays_authenticated_state_and_invokes_refresh_delegate_when_due()
    {
        var observer = new AuthStateObserver();
        var session = CreateSession(
            accessToken: "access-token",
            refreshToken: "refresh-token",
            expiresIn: 1,
            createdAtUtc: DateTime.UtcNow.AddSeconds(-1));
        var refreshCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        observer.Publish(new AuthState.Authenticated(
            1,
            session.AccessToken!,
            session.RefreshToken!,
            DateTimeOffset.UtcNow.AddSeconds(1)));

        using var coordinator = new GotrueAutoRefreshCoordinator(
            observer,
            () => session,
            maximumRefreshWaitTimeSeconds: 60,
            refreshTokenAsync: () =>
            {
                refreshCalled.TrySetResult();
                observer.Publish(new AuthState.SignedOut(1));
                return Task.CompletedTask;
            },
            NullLogger<GotrueAutoRefreshCoordinator>.Instance);

        await refreshCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Coordinator_reschedules_failed_refresh_while_state_stays_authenticated()
    {
        var observer = new AuthStateObserver();
        var session = CreateSession(
            accessToken: "access-token",
            refreshToken: "refresh-token",
            expiresIn: 1,
            createdAtUtc: DateTime.UtcNow.AddSeconds(-1));
        var refreshCallCount = 0;
        var secondCallObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        observer.Publish(new AuthState.Authenticated(
            1,
            session.AccessToken!,
            session.RefreshToken!,
            DateTimeOffset.UtcNow.AddSeconds(1)));

        using var coordinator = new GotrueAutoRefreshCoordinator(
            observer,
            () => session,
            maximumRefreshWaitTimeSeconds: 60,
            refreshTokenAsync: () =>
            {
                refreshCallCount++;
                if (refreshCallCount >= 2)
                {
                    observer.Publish(new AuthState.SignedOut(1));
                    secondCallObserved.TrySetResult();
                }

                throw new InvalidOperationException("boom");
            },
            NullLogger<GotrueAutoRefreshCoordinator>.Instance);

        await secondCallObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Try_create_refresh_schedule_clamps_to_maximum_wait_time()
    {
        var session = CreateSession(
            accessToken: "access-token",
            refreshToken: "refresh-token",
            expiresIn: 60 * 60,
            createdAtUtc: DateTime.UtcNow);

        var created = GotrueAutoRefreshCoordinator.TryCreateRefreshSchedule(
            session,
            maximumRefreshWaitTimeSeconds: 5,
            now: DateTimeOffset.UtcNow,
            out var dueTime);

        Assert.True(created);
        Assert.Equal(TimeSpan.FromSeconds(5), dueTime);
    }

    private static global::Supabase.Gotrue.Session CreateSession(
        string accessToken,
        string refreshToken,
        long expiresIn,
        DateTime createdAtUtc)
    {
        return new global::Supabase.Gotrue.Session
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiresIn,
            CreatedAt = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
        };
    }
}
