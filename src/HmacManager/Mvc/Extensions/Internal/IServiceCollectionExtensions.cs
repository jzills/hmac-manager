using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HmacManager.Caching;
using HmacManager.Common;
using HmacManager.Components;
using HmacManager.Policies;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to configure and add services related to HMAC management.
/// </summary>
internal static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds the necessary services to configure and manage HMAC-based authentication and headers.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="options">Options used to configure the HMAC manager and related services.</param>
    /// <returns>The <see cref="IServiceCollection"/> with the added services.</returns>
    /// <remarks>
    /// This method registers various services for HMAC header parsing, header building, authentication context, and authorization handling.
    /// It also adds memory and distributed cache services and configures scoped or singleton policy services based on options.
    /// </remarks>
    internal static IServiceCollection AddHmacManager(
        this IServiceCollection services,
        HmacManagerOptions options
    )
    {
        services.AddScoped<IHmacHeaderParserFactory, HmacHeaderParserFactory>(_ => 
            new HmacHeaderParserFactory(options.IsConsolidatedHeadersEnabled));

        services.AddScoped<IHmacHeaderBuilderFactory, HmacHeaderBuilderFactory>(_ => 
            new HmacHeaderBuilderFactory(options.IsConsolidatedHeadersEnabled));

        return services
            .AddHttpContextAccessor()
            .AddPolicyCollection(options.GetPolicyCollectionOptions())
            .AddCacheCollection()
            .AddFactory()
            .AddScoped<IHmacAuthenticationContextProvider, HmacAuthenticationContextProvider>()
            .AddSingleton<IAuthorizationHandler, HmacAuthorizationHandler>(); // Add AuthorizationHandler for dynamic policies
    }

    /// <summary>
    /// Adds the necessary services for managing HMAC policies to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="options">Options that configure the policy collection, including scoped or singleton behavior.</param>
    /// <returns>The <see cref="IServiceCollection"/> with the added services.</returns>
    internal static IServiceCollection AddPolicyCollection(
        this IServiceCollection services, 
        HmacPolicyCollectionOptions options
    )
    {
        if (options.IsScopedPoliciesEnabled)
        {
            services.AddScoped(options.PoliciesAccessor!);
        }
        else
        {
            services.AddSingleton(options.Policies);
        }

        return services;
    }

    /// <summary>
    /// Adds cache services (memory and distributed caches) to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> with the added cache services.</returns>
    internal static IServiceCollection AddCacheCollection(this IServiceCollection services)
    {
        services.TryAddSingleton<IMemoryCache, MemoryCache>();
        services.TryAddSingleton<IDistributedCache, MemoryDistributedCache>();

        return services.AddScoped<IComponentCollection<INonceCache>, NonceCacheCollection>(GetCacheCollection);
    }

    /// <summary>
    /// Adds the necessary services for creating an HMAC manager to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> with the added HMAC manager factory service.</returns>
    internal static IServiceCollection AddFactory(
        this IServiceCollection services
    ) => services.AddScoped<IHmacManagerFactory, HmacManagerFactory>();

    /// <summary>
    /// Creates a <see cref="NonceCacheCollection"/> with default cache services.
    /// </summary>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to retrieve required services.</param>
    /// <returns>A <see cref="NonceCacheCollection"/> with default caches.</returns>
    private static NonceCacheCollection GetCacheCollection(
        IServiceProvider serviceProvider
    ) => new NonceCacheCollection().AddDefaultCaches(serviceProvider);
}