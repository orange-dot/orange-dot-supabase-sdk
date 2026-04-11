using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OrangeDot.Supabase;
using OrangeDot.Supabase.Urls;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;

namespace OrangeDot.Supabase.IntegrationTests;

internal sealed class ResearchWorkspaceScenario : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly List<string> _organizationIds = new();
    private readonly List<string> _artifactPaths = new();

    private ResearchWorkspaceScenario(
        IntegrationTestSettings settings,
        ServiceProvider provider,
        IntegrationUser owner,
        IntegrationUser editor,
        IntegrationUser viewer,
        IntegrationUser outsider,
        string organizationId,
        string projectId,
        string experimentId,
        string outsiderOrganizationId,
        string outsiderProjectId)
    {
        Settings = settings;
        _provider = provider;
        Factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();
        Owner = owner;
        Editor = editor;
        Viewer = viewer;
        Outsider = outsider;
        OrganizationId = organizationId;
        ProjectId = projectId;
        ExperimentId = experimentId;
        OutsiderOrganizationId = outsiderOrganizationId;
        OutsiderProjectId = outsiderProjectId;

        _organizationIds.Add(organizationId);
        _organizationIds.Add(outsiderOrganizationId);
    }

    internal IntegrationTestSettings Settings { get; }

    internal ISupabaseStatelessClientFactory Factory { get; }

    internal IntegrationUser Owner { get; }

    internal IntegrationUser Editor { get; }

    internal IntegrationUser Viewer { get; }

    internal IntegrationUser Outsider { get; }

    internal string OrganizationId { get; }

    internal string ProjectId { get; }

    internal string ExperimentId { get; }

    internal string OutsiderOrganizationId { get; }

    internal string OutsiderProjectId { get; }

    internal ISupabaseStatelessClient OwnerClient => Factory.CreateForUser(Owner.AccessToken);

    internal ISupabaseStatelessClient EditorClient => Factory.CreateForUser(Editor.AccessToken);

    internal ISupabaseStatelessClient ViewerClient => Factory.CreateForUser(Viewer.AccessToken);

    internal ISupabaseStatelessClient OutsiderClient => Factory.CreateForUser(Outsider.AccessToken);

    internal ISupabaseStatelessClient ServiceClient => Factory.CreateService();

    internal static async Task<ResearchWorkspaceScenario> CreateAsync(string prefix)
    {
        var settings = IntegrationTestEnvironment.LoadSettings();
        await IntegrationTestEnvironment.EnsureOptInAndResearchWorkspaceReachableAsync(settings);

        var provider = IntegrationTestEnvironment.CreateServerProvider(settings);
        var factory = provider.GetRequiredService<ISupabaseStatelessClientFactory>();
        var anonClient = factory.CreateAnon();

        var owner = await CreateUserAsync(anonClient, settings, $"{prefix}-owner");
        var editor = await CreateUserAsync(anonClient, settings, $"{prefix}-editor");
        var viewer = await CreateUserAsync(anonClient, settings, $"{prefix}-viewer");
        var outsider = await CreateUserAsync(anonClient, settings, $"{prefix}-outsider");

        var ownerClient = factory.CreateForUser(owner.AccessToken);
        var outsiderClient = factory.CreateForUser(outsider.AccessToken);

        var organization = await InsertOrganizationAsync(ownerClient, $"{prefix}-org");
        var project = await InsertProjectAsync(ownerClient, organization.Id!, $"{prefix}-project");
        var experiment = await InsertExperimentAsync(ownerClient, project.Id!, $"{prefix}-experiment");

        await ownerClient.Postgrest.Table<ResearchMembershipTestRecord>().Insert(new ResearchMembershipTestRecord
        {
            OrganizationId = organization.Id,
            UserId = editor.UserId,
            Role = "editor"
        });

        await ownerClient.Postgrest.Table<ResearchMembershipTestRecord>().Insert(new ResearchMembershipTestRecord
        {
            OrganizationId = organization.Id,
            UserId = viewer.UserId,
            Role = "viewer"
        });

        var outsiderOrganization = await InsertOrganizationAsync(outsiderClient, $"{prefix}-outsider-org");
        var outsiderProject = await InsertProjectAsync(outsiderClient, outsiderOrganization.Id!, $"{prefix}-outsider-project");

        return new ResearchWorkspaceScenario(
            settings,
            provider,
            owner,
            editor,
            viewer,
            outsider,
            organization.Id!,
            project.Id!,
            experiment.Id!,
            outsiderOrganization.Id!,
            outsiderProject.Id!);
    }

    internal async Task<ResearchRunTestRecord> CreateRunAsEditorAsync(string displayName, string status = "running")
    {
        var response = await EditorClient.Postgrest.Table<ResearchRunTestRecord>().Insert(new ResearchRunTestRecord
        {
            ExperimentId = ExperimentId,
            DisplayName = displayName,
            Status = status,
            StartedAt = string.Equals(status, "running", StringComparison.Ordinal) ? DateTime.UtcNow : null,
            CompletedAt = string.Equals(status, "succeeded", StringComparison.Ordinal) ? DateTime.UtcNow : null
        });

        return response.Models.Single();
    }

    internal async Task<ResearchRunTestRecord> CreateRunAsOwnerAsync(string displayName, string status = "queued")
    {
        var response = await OwnerClient.Postgrest.Table<ResearchRunTestRecord>().Insert(new ResearchRunTestRecord
        {
            ExperimentId = ExperimentId,
            DisplayName = displayName,
            Status = status
        });

        return response.Models.Single();
    }

    internal async Task RegisterArtifactPathAsync(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            _artifactPaths.Add(path);
        }

        await Task.CompletedTask;
    }

    internal async Task<global::Supabase.Realtime.Client> CreateRealtimeClientForUserAsync(IntegrationUser user)
    {
        var realtimeUrl = SupabaseUrls.FromBaseUrl(Settings.Url).RealtimeUrl;
        var clientOptions = new global::Supabase.Realtime.ClientOptions();
        clientOptions.Headers.Add("apikey", Settings.AnonKey);
        clientOptions.Parameters.ApiKey = Settings.AnonKey;
        clientOptions.Parameters.Token = user.AccessToken;

        var client = new global::Supabase.Realtime.Client(realtimeUrl, clientOptions)
        {
            GetHeaders = () => new Dictionary<string, string>
            {
                ["apikey"] = Settings.AnonKey,
                ["Authorization"] = $"Bearer {user.AccessToken}"
            }
        };

        client.SetAuth(user.AccessToken);
        await client.ConnectAsync();
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            var serviceClient = ServiceClient;
            var bucket = serviceClient.Storage.From(IntegrationTestEnvironment.ResearchArtifactsBucketName);

            foreach (var objectPath in _artifactPaths.Distinct(StringComparer.Ordinal))
            {
                await IntegrationTestEnvironment.BestEffortRemoveStorageObjectAsync(bucket, objectPath);
            }

            foreach (var organizationId in _organizationIds.Distinct(StringComparer.Ordinal))
            {
                try
                {
                    await serviceClient.Postgrest.Table<ResearchOrganizationTestRecord>()
                        .Filter("id", global::Supabase.Postgrest.Constants.Operator.Equals, organizationId)
                        .Delete();
                }
                catch
                {
                    // Cleanup is best-effort for local integration scenarios.
                }
            }
        }
        finally
        {
            await _provider.DisposeAsync();
        }
    }

    private static async Task<IntegrationUser> CreateUserAsync(
        ISupabaseStatelessClient anonClient,
        IntegrationTestSettings settings,
        string prefix)
    {
        var email = IntegrationTestEnvironment.NewEmailAddress(prefix);
        var createdUser = await anonClient.Auth.CreateUser(
            settings.SecretKey,
            anonClient.AuthOptions,
            new AdminUserAttributes
            {
                Email = email,
                Password = IntegrationTestEnvironment.DefaultPassword,
                EmailConfirm = true
            });

        var session = await anonClient.Auth.SignIn(email, IntegrationTestEnvironment.DefaultPassword, anonClient.AuthOptions);

        if (createdUser?.Id is null || session?.AccessToken is null || session.RefreshToken is null)
        {
            throw new GotrueException("Unable to create a confirmed integration user.");
        }

        return new IntegrationUser(
            createdUser.Id,
            email,
            IntegrationTestEnvironment.DefaultPassword,
            session.AccessToken,
            session.RefreshToken);
    }

    private static async Task<ResearchOrganizationTestRecord> InsertOrganizationAsync(
        ISupabaseStatelessClient client,
        string name)
    {
        await client.Postgrest.Table<ResearchOrganizationTestRecord>().Insert(
            new ResearchOrganizationTestRecord
            {
                Name = name
            },
            new global::Supabase.Postgrest.QueryOptions
            {
                Returning = global::Supabase.Postgrest.QueryOptions.ReturnType.Minimal
            });

        var response = await client.Postgrest.Table<ResearchOrganizationTestRecord>()
            .Filter("name", global::Supabase.Postgrest.Constants.Operator.Equals, name)
            .Order(record => record.InsertedAt, global::Supabase.Postgrest.Constants.Ordering.Descending)
            .Limit(1)
            .Get();

        return response.Models.Single();
    }

    private static async Task<ResearchProjectTestRecord> InsertProjectAsync(
        ISupabaseStatelessClient client,
        string organizationId,
        string name)
    {
        var response = await client.Postgrest.Table<ResearchProjectTestRecord>().Insert(new ResearchProjectTestRecord
        {
            OrganizationId = organizationId,
            Name = name,
            Visibility = "private"
        });

        return response.Models.Single();
    }

    private static async Task<ResearchExperimentTestRecord> InsertExperimentAsync(
        ISupabaseStatelessClient client,
        string projectId,
        string name)
    {
        var response = await client.Postgrest.Table<ResearchExperimentTestRecord>().Insert(new ResearchExperimentTestRecord
        {
            ProjectId = projectId,
            Name = name,
            Status = "active"
        });

        return response.Models.Single();
    }

    internal sealed record IntegrationUser(
        string UserId,
        string Email,
        string Password,
        string AccessToken,
        string RefreshToken);
}
