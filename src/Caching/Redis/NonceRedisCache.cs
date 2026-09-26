using StackExchange.Redis;

namespace HmacManager.Caching.Redis;

/// <summary>
/// Provides a Redis implementation of <see cref="NonceCache"/> that checks and stores a nonce in a single atomic operation.
/// </summary>
internal class NonceRedisCache : NonceCache
{
    /// <summary>
    /// Gets the Redis connection used to store nonces.
    /// </summary>
    protected readonly IConnectionMultiplexer Connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="NonceRedisCache"/> class with the specified connection and options.
    /// </summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="options">The configuration options for nonce caching.</param>
    public NonceRedisCache(IConnectionMultiplexer connection, NonceCacheOptions options)
        : base(options) => Connection = connection;

    /// <inheritdoc/>
    public override async Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge)
    {
        var ttl = dateRequested + maxAge - DateTimeOffset.UtcNow;
        if (ttl < TimeSpan.FromMilliseconds(1))
        {
            return false;
        }

        return await Connection.GetDatabase().StringSetAsync(
            GetKey(nonce), dateRequested.ToString("O"), ttl, When.NotExists);
    }

    /// <summary>
    /// Sets a nonce in the cache with an expiration based on the specified <paramref name="dateRequested"/>.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce.</param>
    /// <param name="dateRequested">The date and time the nonce was requested, used to calculate expiration.</param>
    /// <param name="maxAgeInSeconds">The number of seconds after <paramref name="dateRequested"/> the nonce is kept for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override Task SetAsync(Guid nonce, DateTimeOffset dateRequested, int maxAgeInSeconds) =>
        Connection.GetDatabase().StringSetAsync(
            GetKey(nonce),
            dateRequested.ToString("O"),
            GetAbsoluteExpiration(dateRequested, maxAgeInSeconds) - DateTimeOffset.UtcNow);

    /// <summary>
    /// Checks if a nonce exists in the cache.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains <c>true</c> if the nonce exists; otherwise, <c>false</c>.</returns>
    public override Task<bool> ContainsAsync(Guid nonce) =>
        Connection.GetDatabase().KeyExistsAsync(GetKey(nonce));
}
