using System;
using Microsoft.Extensions.Options;

namespace OrangeDot.Supabase.Internal;

internal sealed class SupabaseStatelessClientFactory : ISupabaseStatelessClientFactory
{
    private readonly IOptions<SupabaseServerOptions> _options;

    public SupabaseStatelessClientFactory(IOptions<SupabaseServerOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public ISupabaseStatelessClient CreateAnon()
    {
        return SupabaseStatelessClient.Create(CreateSnapshot(nameof(CreateAnon)));
    }

    public ISupabaseStatelessClient CreateForUser(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var snapshot = CreateSnapshot(nameof(CreateForUser));
        return SupabaseStatelessClient.Create(
            snapshot,
            StatelessChildAuthorization.ForDelegatedUser(snapshot.AnonKey, accessToken));
    }

    public ISupabaseStatelessClient CreateService()
    {
        var snapshot = CreateSnapshot(nameof(CreateService));
        var secretKey = SupabaseKeyResolver.ResolvePrivilegedKey(
            _options.Value.ConfiguredSecretKey,
            _options.Value.ConfiguredServiceRoleKey,
            nameof(CreateService));

        return SupabaseStatelessClient.Create(
            snapshot,
            StatelessChildAuthorization.ForPrivilegedKey(secretKey));
    }

    private LifecycleSnapshot CreateSnapshot(string operation)
    {
        return SupabaseConfigurationSnapshotFactory.Create(_options.Value, operation);
    }
}
