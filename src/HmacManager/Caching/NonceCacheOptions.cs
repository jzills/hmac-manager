namespace HmacManager.Caching;

/// <summary>
/// Provides configuration options for the <see cref="NonceCache"/>, including expiration time and cache type.
/// </summary>
internal class NonceCacheOptions
{
    /// <summary>
    /// Gets or sets the maximum age, in seconds, for a nonce entry in the cache.
    /// </summary>
    /// <value>The default value is 30 seconds.</value>
    public int MaxAgeInSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the type of cache used for storing nonces.
    /// </summary>
    /// <value>The default value is <see cref="NonceCacheType.Memory"/>.</value>
    public NonceCacheType CacheType { get; set; } = NonceCacheType.Memory;

    /// <summary>
    /// Creates a unique cache key for a given nonce, incorporating the cache type and nonce value.
    /// </summary>
    /// <param name="nonce">The nonce for which to generate a cache key.</param>
    /// <returns>A string representing the cache key for the specified nonce.</returns>
    public string CreateKey(Guid nonce) => NonceCache.CreateKey(CacheType, nonce);
}