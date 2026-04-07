using System.Collections.Generic;
using System.Threading.Tasks;

namespace OrangeDot.Supabase.IntegrationTests;

[Collection(LocalSupabaseStatefulCollection.Name)]
public sealed class StatefulAuthIntegrationTests
{
    private readonly LocalSupabaseStatefulFixture _fixture;

    public StatefulAuthIntegrationTests(LocalSupabaseStatefulFixture fixture)
    {
        _fixture = fixture;
    }

    [LocalSupabaseFact]
    public async Task Stateful_client_can_sign_in_refresh_and_sign_out_anonymously()
    {
        if (_fixture.Client.Auth.CurrentSession is not null)
        {
            await _fixture.Client.Auth.SignOut();
        }

        var session = await _fixture.Client.Auth.SignInAnonymously(new global::Supabase.Gotrue.SignInAnonymouslyOptions
        {
            Data = new Dictionary<string, object>
            {
                ["source"] = "integration-tests"
            }
        });

        Assert.NotNull(session);
        Assert.NotNull(_fixture.Client.Auth.CurrentSession);
        Assert.False(string.IsNullOrWhiteSpace(_fixture.Client.Auth.CurrentSession!.AccessToken));

        var originalAccessToken = _fixture.Client.Auth.CurrentSession.AccessToken;
        var refreshedSession = await _fixture.Client.Auth.RefreshSession();

        Assert.NotNull(refreshedSession);
        Assert.NotNull(_fixture.Client.Auth.CurrentSession);
        Assert.False(string.IsNullOrWhiteSpace(_fixture.Client.Auth.CurrentSession!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(originalAccessToken));

        await _fixture.Client.Auth.SignOut();

        Assert.Null(_fixture.Client.Auth.CurrentSession);
    }
}
