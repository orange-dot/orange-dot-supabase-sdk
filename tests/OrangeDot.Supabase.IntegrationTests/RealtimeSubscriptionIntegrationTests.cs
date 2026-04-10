using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrangeDot.Supabase;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;

namespace OrangeDot.Supabase.IntegrationTests;

[Collection(LocalSupabaseStatefulCollection.Name)]
public sealed class RealtimeSubscriptionIntegrationTests
{
    private readonly LocalSupabaseStatefulFixture _fixture;

    public RealtimeSubscriptionIntegrationTests(LocalSupabaseStatefulFixture fixture)
    {
        _fixture = fixture;
    }

    [LocalSupabaseFact]
    public async Task Table_wrapper_receives_postgres_changes_for_matching_owner_tag()
    {
        var ownerTag = IntegrationTestEnvironment.NewOwnerTag("realtime");
        var details = $"details-{ownerTag}";
        var changeReceived = new TaskCompletionSource<IntegrationTodo>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        global::Supabase.Realtime.RealtimeChannel? channel = null;

        try
        {
            var subscribedChannel = await _fixture.Client.Table<IntegrationTodo>().On(
                PostgresChangesOptions.ListenType.Inserts,
                (_, change) =>
                {
                    var model = change.Model<IntegrationTodo>();

                    if (model?.OwnerTag == ownerTag)
                    {
                        changeReceived.TrySetResult(model);
                    }
                },
                filter: $"owner_tag=eq.{ownerTag}");

            channel = Assert.IsType<global::Supabase.Realtime.RealtimeChannel>(subscribedChannel);
            Assert.Equal(Constants.ChannelState.Joined, channel.State);

            await Task.Delay(TimeSpan.FromMilliseconds(500));

            await _fixture.Client.Table<IntegrationTodo>().Insert(new IntegrationTodo
            {
                Details = details,
                OwnerTag = ownerTag
            });

            var changed = await changeReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(ownerTag, changed.OwnerTag);
            Assert.Equal(details, changed.Details);
        }
        finally
        {
            if (channel is not null)
            {
                channel.Unsubscribe();
                _fixture.Client.Realtime.Remove(channel);
            }

            await IntegrationTestEnvironment.CleanupByOwnerTagAsync(_fixture.Client.Postgrest, ownerTag);
        }
    }

    [LocalSupabaseFact]
    public async Task Table_wrapper_recovers_existing_subscription_after_forced_reconnect()
    {
        var settings = _fixture.Settings;
        var ownerTag = IntegrationTestEnvironment.NewOwnerTag("realtime-reconnect");
        var details = $"details-{ownerTag}";
        var changeReceived = new TaskCompletionSource<IntegrationTodo>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var reconnectObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reopenedObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedStates = new List<global::Supabase.Realtime.Constants.SocketState>();
        var stateGate = new object();
        var reconnectSeen = false;
        SupabaseClient? client = null;
        IDisposable? socketStateSubscription = null;
        global::Supabase.Realtime.RealtimeChannel? channel = null;

        try
        {
            client = await CreateStatefulClientAsync(settings);
            socketStateSubscription = client.TableRealtime.SubscribeToSocketState(state =>
            {
                lock (stateGate)
                {
                    observedStates.Add(state);

                    if (state == global::Supabase.Realtime.Constants.SocketState.Reconnect)
                    {
                        reconnectSeen = true;
                        reconnectObserved.TrySetResult();
                    }
                    else if (reconnectSeen && state == global::Supabase.Realtime.Constants.SocketState.Open)
                    {
                        reopenedObserved.TrySetResult();
                    }
                }
            });

            var subscribedChannel = await client.Table<IntegrationTodo>().On(
                PostgresChangesOptions.ListenType.Inserts,
                (_, change) =>
                {
                    var model = change.Model<IntegrationTodo>();

                    if (model?.OwnerTag == ownerTag)
                    {
                        changeReceived.TrySetResult(model);
                    }
                },
                filter: $"owner_tag=eq.{ownerTag}");

            channel = Assert.IsType<global::Supabase.Realtime.RealtimeChannel>(subscribedChannel);
            var originalTopic = channel.Topic;
            Assert.Equal(Constants.ChannelState.Joined, channel.State);

            await Task.Delay(TimeSpan.FromMilliseconds(500));

            await client.TableRealtime.ForceReconnectAsync();
            await reconnectObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await reopenedObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(client.Realtime.Subscriptions.ContainsKey(originalTopic));
            Assert.Same(channel, client.Realtime.Subscriptions[originalTopic]);

            await Task.Delay(TimeSpan.FromMilliseconds(500));

            await client.Table<IntegrationTodo>().Insert(new IntegrationTodo
            {
                Details = details,
                OwnerTag = ownerTag
            });

            var changed = await changeReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(ownerTag, changed.OwnerTag);
            Assert.Equal(details, changed.Details);
            Assert.Contains(global::Supabase.Realtime.Constants.SocketState.Reconnect, observedStates);
        }
        finally
        {
            socketStateSubscription?.Dispose();

            if (channel is not null && client is not null)
            {
                channel.Unsubscribe();
                client.Realtime.Remove(channel);
            }

            if (client is not null)
            {
                try
                {
                    await IntegrationTestEnvironment.CleanupByOwnerTagAsync(client.Postgrest, ownerTag);
                }
                finally
                {
                    client.Dispose();
                }
            }
        }
    }

    private static async Task<SupabaseClient> CreateStatefulClientAsync(IntegrationTestSettings settings)
    {
        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = settings.Url,
            PublishableKey = settings.AnonKey
        });

        var hydrated = await configured.LoadPersistedSessionAsync();
        return await hydrated.InitializeAsync();
    }
}
