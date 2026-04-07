using System;
using System.Threading.Tasks;
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
}
