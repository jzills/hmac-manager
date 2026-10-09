using HmacManager.Caching;
using HmacManager.Caching.Distributed;
using HmacManager.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

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
    /// Adds the caches registered with <c>AddNonceCache</c> to the <see cref="NonceCacheCollection"/>,
    /// then the built-in memory and distributed caches for whichever types they leave uncovered.
    /// </summary>
    /// <param name="source">The <see cref="NonceCacheCollection"/> to add caches to.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to retrieve cache-related services.</param>
    /// <returns>The <see cref="NonceCacheCollection"/> with the added caches.</returns>
    public static NonceCacheCollection AddDefaultCaches(
        this NonceCacheCollection source, 
        IServiceProvider serviceProvider
    )
    {
        // Reversed because the collection keeps the first cache added for a type, and the last
        // registration should win, as it does for any other service.
        foreach (var registration in serviceProvider.GetServices<NonceCacheRegistration>().Reverse())
        {
            source.Add(registration.CacheType, registration.Cache);
        }

        // Only for the types left uncovered: a replaced cache's store need not exist at all.
        if (source.IsMissing(NonceCacheType.Memory))
        {
            source.AddDefaultMemoryCache(serviceProvider);
        }

        if (source.IsMissing(NonceCacheType.Distributed))
        {
            source.AddDefaultDistributedCache(serviceProvider);
        }

        return source;
    }

    private static bool IsMissing(this NonceCacheCollection source, NonceCacheType cacheType) =>
        source.Get(Enum.GetName(cacheType)!) is null;

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
}