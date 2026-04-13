using System.Collections.Generic;
using System;
using Microsoft.Extensions.Logging.Abstractions;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Internal;
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

    [LocalSupabaseFact]
    public async Task Live_auth_trace_translates_to_model_actions_against_local_supabase()
    {
        var traceSink = new IntegrationRecordingRuntimeTraceSink();
        using var tracedClient = await CreateTracedClientAsync(traceSink);

        try
        {
            if (tracedClient.Auth.CurrentSession is not null)
            {
                await tracedClient.Auth.SignOut();
            }

            var session = await tracedClient.Auth.SignInAnonymously(new global::Supabase.Gotrue.SignInAnonymouslyOptions
            {
                Data = new Dictionary<string, object>
                {
                    ["source"] = "integration-live-trace"
                }
            });

            Assert.NotNull(session);

            var refreshedSession = await tracedClient.Auth.RefreshSession();

            Assert.NotNull(refreshedSession);

            await tracedClient.Auth.SignOut();

            var authTrace = FilterAuthTrace(traceSink.Snapshot());
            var actions = new AuthTraceToModelActionTranslator().Translate(authTrace);

            Assert.Equal(
                [
                    StartBinding("Postgrest"),
                    StartBinding("Storage"),
                    StartBinding("Functions"),
                    StartBinding("Realtime"),
                    Action(AuthModelActionKind.SignOut),
                    Action(AuthModelActionKind.SignIn),
                    Project("Postgrest"),
                    Project("Storage"),
                    Project("Functions"),
                    Project("Realtime"),
                    Action(AuthModelActionKind.BeginRefresh),
                    Action(AuthModelActionKind.CompleteRefresh),
                    Project("Postgrest"),
                    Project("Storage"),
                    Project("Functions"),
                    Project("Realtime"),
                    Action(AuthModelActionKind.SignOut)
                ],
                actions);
        }
        finally
        {
            if (tracedClient.Auth.CurrentSession is not null)
            {
                await tracedClient.Auth.SignOut();
            }
        }
    }

    private async Task<SupabaseClient> CreateTracedClientAsync(IntegrationRecordingRuntimeTraceSink traceSink)
    {
        var runtimeContext = new SupabaseRuntimeContext(
            new AuthStateObserver(),
            NullLoggerFactory.Instance,
            MeterFactory: null,
            NoOpSupabaseSessionStore.Instance,
            traceSink);

        var configured = SupabaseClient.Configure(new SupabaseOptions
        {
            Url = _fixture.Settings.Url,
            PublishableKey = _fixture.Settings.AnonKey
        }, runtimeContext);

        var hydrated = await configured.LoadPersistedSessionAsync();
        return await hydrated.InitializeAsync();
    }

    private static IReadOnlyList<RuntimeTraceEvent> FilterAuthTrace(IReadOnlyList<RuntimeTraceEvent> trace)
    {
        var filtered = new List<RuntimeTraceEvent>();

        foreach (var traceEvent in trace)
        {
            if (traceEvent is AuthTraceEvent or BindingProjectionTraceEvent)
            {
                filtered.Add(traceEvent);
            }
        }

        return filtered;
    }

    private static AuthModelAction Action(AuthModelActionKind kind)
    {
        return new AuthModelAction(kind);
    }

    private static AuthModelAction StartBinding(string bindingName)
    {
        return new AuthModelAction(AuthModelActionKind.StartBinding, bindingName);
    }

    private static AuthModelAction Project(string bindingName)
    {
        return new AuthModelAction(AuthModelActionKind.ProjectCurrentToBinding, bindingName);
    }
}
