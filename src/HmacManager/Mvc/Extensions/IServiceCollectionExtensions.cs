using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HmacManager.Caching;
using HmacManager.Mvc.Extensions.Internal;

namespace HmacManager.Mvc.Extensions;

/// <summary>
/// A class representing extension methods on an <see cref="IServiceCollection"/>.
/// </summary>
public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Registers the necessary dependencies to use <see cref="HmacManager.Components.IHmacManagerFactory"/>
    /// in the dependency injection container with the configured <see cref="HmacManagerOptions"/>.
    ///     <para>
    ///         See <see href="https://github.com/jzills/hmac-manager/tree/main/samples/">here</see> for examples.
    ///     </para>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configureOptions">The configuration action for <see cref="HmacManagerOptions"/>.</param>
    /// <returns>An <see cref="IServiceCollection"/> that can be used to further configure services.</returns>
    public static IServiceCollection AddHmacManager(
        this IServiceCollection services, 
        Action<HmacManagerOptions> configureOptions
    )
    {
        var options = new HmacManagerOptions();
        configureOptions(options);
        
        return services.AddHmacManager(options);
    }

    /// <summary>
    /// Registers the necessary dependencies to use <see cref="HmacManager.Components.IHmacManagerFactory"/>
    /// in the dependency injection container with the corresponding <see cref="IConfiguration"/> settings.
    ///     <para>
    ///         See <see href="https://github.com/jzills/hmac-manager/tree/main/samples/">here</see> for examples.
    ///     </para>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configurationSection">The <see cref="IConfigurationSection"/> for an array of JSON objects representing an <see cref="HmacManager.Policies.HmacPolicy"/>.</param>
    /// <returns>An <see cref="IServiceCollection"/> that can be used to further configure services.</returns>
    /// <remarks>
    /// If <paramref name="configurationSection"/> supports reload notifications (e.g. it was bound from a
    /// configuration source added with <c>reloadOnChange: true</c>), the resulting policy collection stays
    /// in sync with configuration changes for the lifetime of the process — see <see cref="HmacPolicyCollectionReloader"/>.
    /// </remarks>
    public static IServiceCollection AddHmacManager(
        this IServiceCollection services,
        IConfigurationSection configurationSection
    )
    {
        var policies = new ReloadableHmacPolicyCollection(configurationSection.GetPolicySection());
        var reloader = new HmacPolicyCollectionReloader(configurationSection, policies);

        services.AddHmacManager(new HmacManagerOptions(policies));

        // The reloader is already watching — see HmacPolicyCollectionReloader.UseLogger for why it
        // has to be. Registering it as a hosted service is how it gets a real logger, and how it
        // gets to report the live policy set once the host is up.
        services.AddHostedService(provider =>
            reloader.UseLogger(provider.GetRequiredService<ILogger<HmacPolicyCollectionReloader>>()));

        return services;
    }

    /// <summary>
    /// Serves every policy whose nonce cache type is <paramref name="cacheType"/> from
    /// <typeparamref name="TCache"/>. Policies on any other type keep the built-in cache.
    /// </summary>
    /// <typeparam name="TCache">The cache. Resolved from the container if it is registered there,
    /// otherwise constructed with its dependencies resolved from it.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="cacheType">The <see cref="NonceCacheType"/> whose policies the cache protects.</param>
    /// <returns>An <see cref="IServiceCollection"/> that can be used to further configure services.</returns>
    /// <inheritdoc cref="AddNonceCache(IServiceCollection, NonceCacheType, Func{IServiceProvider, INonceCache})" path="/remarks"/>
    public static IServiceCollection AddNonceCache<TCache>(
        this IServiceCollection services,
        NonceCacheType cacheType
    ) where TCache : class, INonceCache =>
        services.AddNonceCache(cacheType, ActivatorUtilities.GetServiceOrCreateInstance<TCache>);

    /// <summary>
    /// Serves every policy whose nonce cache type is <paramref name="cacheType"/> from the cache
    /// <paramref name="implementationFactory"/> creates. Policies on any other type keep the built-in cache.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="cacheType">The <see cref="NonceCacheType"/> whose policies the cache protects.</param>
    /// <param name="implementationFactory">Creates the cache. Called once.</param>
    /// <returns>An <see cref="IServiceCollection"/> that can be used to further configure services.</returns>
    /// <remarks>
    /// The cache is a singleton: created once, the first time a request needs a nonce cache, and
    /// shared by every request after it. It can be registered before or after <c>AddHmacManager</c>,
    /// and the last registration for a type wins. The built-in cache for that type is never
    /// constructed, so the store behind it is never resolved.
    /// </remarks>
    public static IServiceCollection AddNonceCache(
        this IServiceCollection services,
        NonceCacheType cacheType,
        Func<IServiceProvider, INonceCache> implementationFactory
    )
    {
        ArgumentNullException.ThrowIfNull(implementationFactory);

        return services.AddSingleton(provider =>
            new NonceCacheRegistration(cacheType, implementationFactory(provider)));
    }
}