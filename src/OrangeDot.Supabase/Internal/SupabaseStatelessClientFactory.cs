using System;
using Microsoft.Extensions.Options;
using OrangeDot.Supabase.Errors;

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

        return SupabaseStatelessClient.Create(CreateSnapshot(nameof(CreateForUser)), accessToken);
    }

    public ISupabaseStatelessClient CreateService()
    {
        var serviceRoleKey = _options.Value.ServiceRoleKey;
        if (string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            throw new SupabaseConfigurationException(
                SupabaseErrorCode.ConfigurationMissing,
                "Supabase service role key is required.",
                operation: nameof(CreateService));
        }

        return SupabaseStatelessClient.Create(CreateSnapshot(nameof(CreateService)), serviceRoleKey);
    }

    private LifecycleSnapshot CreateSnapshot(string operation)
    {
        return SupabaseConfigurationSnapshotFactory.Create(
            new SupabaseOptions
            {
                Url = _options.Value.Url,
                AnonKey = _options.Value.AnonKey
            },
            operation);
    }
}
