using System;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OrangeDot.Supabase;
using OrangeDot.Supabase.Auth;
using OrangeDot.Supabase.Internal;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSupabase(
        this IServiceCollection services,
        Action<SupabaseOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddSupabase((_, options) => configure(options));
    }

    public static IServiceCollection AddSupabase(
        this IServiceCollection services,
        Action<IServiceProvider, SupabaseOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.TryAddSingleton<IOptions<SupabaseOptions>>(serviceProvider =>
        {
            var options = new SupabaseOptions();
            configure(serviceProvider, options);
            return Microsoft.Extensions.Options.Options.Create(options);
        });

        services.TryAddSingleton<IAuthStateObserver, AuthStateObserver>();
        services.TryAddSingleton<SupabaseClientShell>();
        services.TryAddSingleton<ISupabaseClient>(serviceProvider => serviceProvider.GetRequiredService<SupabaseClientShell>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, SupabaseStartupService>());

        return services;
    }
}
