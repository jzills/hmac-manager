using Microsoft.Extensions.Caching.Distributed;

namespace HmacManager.Caching.Distributed;

/// <summary>
/// Provides a distributed cache implementation of <see cref="NonceCache"/> for storing nonces.
/// </summary>
internal class NonceDistributedCache : NonceCache
{
    /// <summary>
    /// Gets the distributed cache instance used to store nonces.
    /// </summary>
    protected readonly IDistributedCache Cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="NonceDistributedCache"/> class with the specified cache and options.
    /// </summary>
    /// <param name="cache">The distributed cache implementation.</param>
    /// <param name="options">The configuration options for nonce caching.</param>
    public NonceDistributedCache(IDistributedCache cache, NonceCacheOptions options)
        : base(options) => Cache = cache;

    /// <summary>
    /// Sets a nonce in the cache with an expiration based on the specified <paramref name="dateRequested"/>.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce.</param>
    /// <param name="dateRequested">The date and time the nonce was requested, used to calculate expiration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task SetAsync(Guid nonce, DateTimeOffset dateRequested, int maxAgeInSeconds) =>
        Cache.SetStringAsync(
            GetKey(nonce), 
            dateRequested.ToString(), 
            new DistributedCacheEntryOptions 
                { AbsoluteExpiration = GetAbsoluteExpiration(dateRequested, maxAgeInSeconds) }
        );

    /// <summary>
    /// Checks if a nonce exists in the cache.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains <c>true</c> if the nonce exists; otherwise, <c>false</c>.</returns>
    public override async Task<bool> ContainsAsync(Guid nonce)
    {
        var value = await Cache.GetAsync(GetKey(nonce));
        return value is not null;
    }
}