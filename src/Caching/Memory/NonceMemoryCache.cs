using Microsoft.Extensions.Caching.Memory;

namespace HmacManager.Caching.Memory;

/// <summary>
/// Provides an in-memory cache implementation of <see cref="NonceCache"/> for storing nonces.
/// </summary>
internal class NonceMemoryCache : NonceCache
{
    /// <summary>
    /// Gets the in-memory cache instance used to store nonces.
    /// </summary>
    protected readonly IMemoryCache Cache;

    /// <summary>
    /// Locks that make the check and set in <see cref="TryAddAsync"/> atomic. Static because a
    /// <see cref="NonceMemoryCache"/> is created per scope while the <see cref="IMemoryCache"/> behind it is shared.
    /// </summary>
    private static readonly object[] Locks = Enumerable.Range(0, 64).Select(_ => new object()).ToArray();

    /// <summary>
    /// Initializes a new instance of the <see cref="NonceMemoryCache"/> class with the specified memory cache and options.
    /// </summary>
    /// <param name="cache">The in-memory cache implementation.</param>
    /// <param name="options">The configuration options for nonce caching.</param>
    public NonceMemoryCache(IMemoryCache cache, NonceCacheOptions options) 
        : base(options) => Cache = cache;

    /// <summary>
    /// Sets a nonce in the cache with an expiration based on the specified <paramref name="dateRequested"/>.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce.</param>
    /// <param name="dateRequested">The date and time the nonce was requested, used to calculate expiration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task SetAsync(Guid nonce, DateTimeOffset dateRequested, int maxAgeInSeconds)
    {
        Cache.Set(
            GetKey(nonce),
            dateRequested, 
            new MemoryCacheEntryOptions
                { AbsoluteExpiration = GetAbsoluteExpiration(dateRequested, maxAgeInSeconds) }
        );

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge)
    {
        lock (Locks[(nonce.GetHashCode() & int.MaxValue) % Locks.Length])
        {
            if (Cache.Get(GetKey(nonce)) is not null)
            {
                return Task.FromResult(false);
            }

            SetAsync(nonce, dateRequested, (int)maxAge.TotalSeconds);
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Checks if a nonce exists in the cache.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains <c>true</c> if the nonce exists; otherwise, <c>false</c>.</returns>
    public override Task<bool> ContainsAsync(Guid nonce)
    {
        var value = Cache.Get(GetKey(nonce));
        return Task.FromResult(value is not null);
    }
}