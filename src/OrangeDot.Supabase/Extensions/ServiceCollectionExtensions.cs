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
    public static IServiceCollection AddSupabaseHosted(
        this IServiceCollection services,
        Action<SupabaseOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddSupabaseHosted((_, options) => configure(options));
    }

    public static IServiceCollection AddSupabaseHosted(
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

        services.TryAddSingleton<IRuntimeTraceSink>(_ => NoOpRuntimeTraceSink.Instance);
        services.TryAddSingleton<AuthStateObserver>();
        services.TryAddSingleton<IAuthStateObserver>(serviceProvider => serviceProvider.GetRequiredService<AuthStateObserver>());
        services.TryAddSingleton<SupabaseClientShell>();
        services.TryAddSingleton<ISupabaseClient>(serviceProvider => serviceProvider.GetRequiredService<SupabaseClientShell>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, SupabaseStartupService>());

        return services;
    }

    public static IServiceCollection AddSupabaseServer(
        this IServiceCollection services,
        Action<SupabaseServerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddSupabaseServer((_, options) => configure(options));
    }

    public static IServiceCollection AddSupabaseServer(
        this IServiceCollection services,
        Action<IServiceProvider, SupabaseServerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.TryAddSingleton<IOptions<SupabaseServerOptions>>(serviceProvider =>
        {
            var options = new SupabaseServerOptions();
            configure(serviceProvider, options);
            return Microsoft.Extensions.Options.Options.Create(options);
        });

        services.TryAddSingleton<ISupabaseStatelessClientFactory, SupabaseStatelessClientFactory>();

        return services;
    }
}
