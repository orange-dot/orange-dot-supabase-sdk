using System;
using OrangeDot.Supabase;
using System.Threading.Tasks;

namespace OrangeDot.Supabase.IntegrationTests;

public sealed class LocalSupabaseStatefulFixture : IAsyncLifetime
{
    internal IntegrationTestSettings Settings { get; } = IntegrationTestEnvironment.LoadSettings();

    public SupabaseClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await IntegrationTestEnvironment.EnsureOptInAndReachableAsync(Settings);

        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = Settings.Url,
            AnonKey = Settings.AnonKey
        });

        var hydrated = await configured.LoadPersistedSessionAsync();
        Client = await hydrated.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (Client is null)
        {
            return;
        }

        try
        {
            if (Client.Auth.CurrentSession is not null)
            {
                await Client.Auth.SignOut();
            }
        }
        catch (Exception)
        {
            // Sign-out is best-effort during teardown.
        }
        finally
        {
            Client.Dispose();
        }
    }
}
