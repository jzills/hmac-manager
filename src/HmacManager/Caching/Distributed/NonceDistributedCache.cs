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
    protected override async Task<bool> TryAddCoreAsync(Guid nonce, TimeSpan timeToLive)
    {
        var key = Options.CreateKey(nonce);
        if (await ExistsAsync(key))
        {
            return false;
        }

        await WriteAsync(key, timeToLive);
        return true;
    }

    /// <inheritdoc/>
    [Obsolete("Use TryAddAsync.")]
    public Task SetAsync(Guid nonce, DateTimeOffset dateRequested) =>
        SetAsync(nonce, dateRequested, TimeSpan.FromSeconds(Options.MaxAgeInSeconds));

    /// <inheritdoc/>
    [Obsolete("Use TryAddAsync.")]
    public Task SetAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge) =>
        TryGetTimeToLive(dateRequested + maxAge, out var timeToLive)
            ? WriteAsync(Options.CreateKey(nonce), timeToLive)
            : Task.CompletedTask;

    /// <inheritdoc/>
    [Obsolete("Use TryAddAsync.")]
    public Task<bool> ContainsAsync(Guid nonce) => ExistsAsync(Options.CreateKey(nonce));

    private async Task<bool> ExistsAsync(string key) => await Cache.GetAsync(key) is not null;

    /// <summary>
    /// Writes the entry with a TTL relative to now, measured before the read that precedes it. The
    /// entry outlives the window by that round trip at most, and the store never sees an expiry
    /// that passed on the way.
    /// </summary>
    private Task WriteAsync(string key, TimeSpan timeToLive) =>
        Cache.SetStringAsync(
            key,
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive });
}
