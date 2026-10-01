using Microsoft.Extensions.Caching.Distributed;

namespace HmacManager.Caching.Distributed;

/// <summary>
/// Provides a distributed cache implementation of <see cref="NonceCache"/> for storing nonces.
/// </summary>
/// <remarks>
/// <see cref="IDistributedCache"/> has no conditional write, so the claim is a read followed by a
/// write: two copies of a request reaching different instances at the same moment can both pass.
/// </remarks>
// Re-declares INonceCache so the obsolete members below map to this class: NonceCache is where the
// interface is implemented, so without it calls through INonceCache would reach the throwing defaults.
internal class NonceDistributedCache : NonceCache, INonceCache
{
    /// <summary>
    /// Gets the distributed cache instance used to store nonces.
    /// </summary>
    protected readonly IDistributedCache Cache;

    /// <summary>
    /// Gets the configuration options for the nonce cache.
    /// </summary>
    protected readonly NonceCacheOptions Options;

    /// <summary>
    /// Initializes a new instance of the <see cref="NonceDistributedCache"/> class with the specified cache and options.
    /// </summary>
    /// <param name="cache">The distributed cache implementation.</param>
    /// <param name="options">The configuration options for nonce caching.</param>
    /// <param name="clock">The clock expiries are compared against.</param>
    public NonceDistributedCache(IDistributedCache cache, NonceCacheOptions options, TimeProvider? clock = null)
        : base(clock)
    {
        Cache = cache;
        Options = options;
    }

    /// <inheritdoc/>
    protected override async Task<bool> TryAddCoreAsync(Guid nonce, DateTimeOffset expiresAt)
    {
        var key = Options.CreateKey(nonce);
        if (await ExistsAsync(key))
        {
            return false;
        }

        return await WriteAsync(key, expiresAt);
    }

    /// <inheritdoc/>
    [Obsolete("Use TryAddAsync.")]
    public Task SetAsync(Guid nonce, DateTimeOffset dateRequested) =>
        SetAsync(nonce, dateRequested, TimeSpan.FromSeconds(Options.MaxAgeInSeconds));

    /// <inheritdoc/>
    [Obsolete("Use TryAddAsync.")]
    public Task SetAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge) =>
        WriteAsync(Options.CreateKey(nonce), dateRequested + maxAge);

    /// <inheritdoc/>
    [Obsolete("Use TryAddAsync.")]
    public Task<bool> ContainsAsync(Guid nonce) => ExistsAsync(Options.CreateKey(nonce));

    private async Task<bool> ExistsAsync(string key) => await Cache.GetAsync(key) is not null;

    /// <summary>
    /// Writes the entry with a TTL relative to now rather than an absolute expiry. The read before
    /// the write is a network round trip, so an expiry checked before it can be in the past by the
    /// time the store sees it, and stores such as <c>RedisCache</c> throw on that. A relative TTL is
    /// in the future by the store's own clock; a remainder that has already run out means the
    /// request's window closed, so there is nothing left to guard and the claim fails.
    /// </summary>
    private async Task<bool> WriteAsync(string key, DateTimeOffset expiresAt)
    {
        var remaining = expiresAt - Clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return false;
        }

        await Cache.SetStringAsync(
            key,
            expiresAt.ToString("O"),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining });

        return true;
    }
}
