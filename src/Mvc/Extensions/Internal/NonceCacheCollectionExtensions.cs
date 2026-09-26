using HmacManager.Caching;
using HmacManager.Caching.Distributed;
using HmacManager.Caching.Memory;
using HmacManager.Caching.Redis;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="NonceCacheCollection"/> to add default caches (memory and distributed) to the collection.
/// </summary>
internal static class NonceCacheCollectionExtensions
{
    /// <summary>
    /// A function that returns default options for a given <see cref="NonceCacheType"/>.
    /// </summary>
    /// <remarks>
    /// By default, the max age for a nonce cache is set to 30 seconds.
    /// </remarks>
    public static Func<NonceCacheType, NonceCacheOptions> DefaultOptionsAccessor = 
        cacheType => new NonceCacheOptions
        {
            CacheType = cacheType,
            MaxAgeInSeconds = 30
        };

    /// <summary>
    /// Adds the default memory and distributed caches to the <see cref="NonceCacheCollection"/>.
    /// </summary>
    /// <param name="source">The <see cref="NonceCacheCollection"/> to add caches to.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to retrieve cache-related services.</param>
    /// <returns>The <see cref="NonceCacheCollection"/> with the added caches.</returns>
    /// <remarks>
    /// This method adds both in-memory and distributed caches for storing nonces.
    /// </remarks>
    public static NonceCacheCollection AddDefaultCaches(
        this NonceCacheCollection source, 
        IServiceProvider serviceProvider
    ) => source
            .AddDefaultMemoryCache(serviceProvider)
            .AddDefaultDistributedCache(serviceProvider)
            .AddDefaultRedisCache(serviceProvider);

    /// <summary>
    /// Adds a default memory cache to the <see cref="NonceCacheCollection"/>.
    /// </summary>
    /// <param name="source">The <see cref="NonceCacheCollection"/> to add the memory cache to.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to retrieve the <see cref="IMemoryCache"/> service.</param>
    /// <returns>The <see cref="NonceCacheCollection"/> with the added memory cache.</returns>
    private static NonceCacheCollection AddDefaultMemoryCache(
        this NonceCacheCollection source, 
        IServiceProvider serviceProvider
    )
    {
        var cache = new NonceMemoryCache(
            serviceProvider.GetRequiredService<IMemoryCache>(), 
            DefaultOptionsAccessor(NonceCacheType.Memory)
        );

        source.Add(NonceCacheType.Memory, cache);
        return source;
    }

    /// <summary>
    /// Adds a default distributed cache to the <see cref="NonceCacheCollection"/>.
    /// </summary>
    /// <param name="source">The <see cref="NonceCacheCollection"/> to add the distributed cache to.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to retrieve the <see cref="IDistributedCache"/> service.</param>
    /// <returns>The <see cref="NonceCacheCollection"/> with the added distributed cache.</returns>
    private static NonceCacheCollection AddDefaultDistributedCache(
        this NonceCacheCollection source, 
        IServiceProvider serviceProvider
    )
    {
        var cache = new NonceDistributedCache(
            serviceProvider.GetRequiredService<IDistributedCache>(), 
            DefaultOptionsAccessor(NonceCacheType.Distributed)
        );

        source.Add(NonceCacheType.Distributed, cache);
        return source;
    }

    /// <summary>
    /// Adds a default Redis cache to the <see cref="NonceCacheCollection"/> when an <see cref="IConnectionMultiplexer"/> is registered.
    /// </summary>
    /// <param name="source">The <see cref="NonceCacheCollection"/> to add the Redis cache to.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to retrieve the <see cref="IConnectionMultiplexer"/> service.</param>
    /// <returns>The <see cref="NonceCacheCollection"/> with the added Redis cache, if a connection is registered.</returns>
    private static NonceCacheCollection AddDefaultRedisCache(
        this NonceCacheCollection source, 
        IServiceProvider serviceProvider
    )
    {
        var connection = serviceProvider.GetService<IConnectionMultiplexer>();
        if (connection is not null)
        {
            source.Add(NonceCacheType.Redis, new NonceRedisCache(
                connection, 
                DefaultOptionsAccessor(NonceCacheType.Redis)
            ));
        }

        return source;
    }
}