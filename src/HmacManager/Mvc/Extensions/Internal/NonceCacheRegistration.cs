using HmacManager.Caching;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// A cache registered with <c>AddNonceCache</c> for one <see cref="NonceCacheType"/>, which the
/// nonce cache collection uses in place of the built-in cache for that type.
/// </summary>
/// <remarks>
/// Registered as a singleton, so the cache is built once, from the root provider, and every scope's
/// collection shares it. A plain service rather than a keyed one: the collection is built on every
/// request whether or not anything was registered, and resolving a keyed service throws on a
/// container that does not support them.
/// </remarks>
internal sealed class NonceCacheRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NonceCacheRegistration"/> class.
    /// </summary>
    /// <param name="cacheType">The type of cache <paramref name="cache"/> stands in for.</param>
    /// <param name="cache">The cache.</param>
    public NonceCacheRegistration(NonceCacheType cacheType, INonceCache cache)
    {
        CacheType = cacheType;
        Cache = cache;
    }

    /// <summary>
    /// The type of cache <see cref="Cache"/> stands in for.
    /// </summary>
    public NonceCacheType CacheType { get; }

    /// <summary>
    /// The cache.
    /// </summary>
    public INonceCache Cache { get; }
}
