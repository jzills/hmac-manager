using Microsoft.Extensions.Caching.Memory;

namespace HmacManager.Caching.Memory;

/// <summary>
/// Provides an in-memory cache implementation of <see cref="NonceCache"/> for storing nonces.
/// </summary>
// Re-declares INonceCache so the obsolete members below map to this class: NonceCache is where the
// interface is implemented, so without it calls through INonceCache would reach the throwing defaults.
internal class NonceMemoryCache : NonceCache, INonceCache
{
    /// <summary>
    /// Gets the in-memory cache instance used to store nonces.
    /// </summary>
    protected readonly IMemoryCache Cache;

    /// <summary>
    /// Gets the configuration options for the nonce cache.
    /// </summary>
    protected readonly NonceCacheOptions Options;

    /// <summary>
    /// Locks that make the check and set in <see cref="TryAddCoreAsync"/> atomic. Static because a
    /// <see cref="NonceMemoryCache"/> is created per scope while the <see cref="IMemoryCache"/> behind it is shared.
    /// </summary>
    private static readonly object[] Locks = Enumerable.Range(0, 64).Select(_ => new object()).ToArray();

    /// <summary>
    /// Initializes a new instance of the <see cref="NonceMemoryCache"/> class with the specified memory cache and options.
    /// </summary>
    /// <param name="cache">The in-memory cache implementation.</param>
    /// <param name="options">The configuration options for nonce caching.</param>
    /// <param name="clock">The clock expiries are compared against.</param>
    public NonceMemoryCache(IMemoryCache cache, NonceCacheOptions options, TimeProvider? clock = null)
        : base(clock)
    {
        Cache = cache;
        Options = options;
    }

    /// <inheritdoc/>
    protected override Task<bool> TryAddCoreAsync(Guid nonce, DateTimeOffset expiresAt)
    {
        var key = Options.CreateKey(nonce);
        lock (GetLock(nonce))
        {
            if (Contains(key))
            {
                return Task.FromResult(false);
            }

            Set(key, expiresAt);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    [Obsolete("Use TryAddAsync.")]
    public Task SetAsync(Guid nonce, DateTimeOffset dateRequested) =>
        SetAsync(nonce, dateRequested, TimeSpan.FromSeconds(Options.MaxAgeInSeconds));

    /// <inheritdoc/>
    [Obsolete("Use TryAddAsync.")]
    public Task SetAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge)
    {
        lock (GetLock(nonce))
        {
            Set(Options.CreateKey(nonce), dateRequested + maxAge);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    [Obsolete("Use TryAddAsync.")]
    public Task<bool> ContainsAsync(Guid nonce) => Task.FromResult(Contains(Options.CreateKey(nonce)));

    private bool Contains(string key) => Cache.TryGetValue(key, out _);

    private void Set(string key, DateTimeOffset expiresAt) =>
        Cache.Set(key, true, new MemoryCacheEntryOptions { AbsoluteExpiration = expiresAt });

    private static object GetLock(Guid nonce) => Locks[(nonce.GetHashCode() & int.MaxValue) % Locks.Length];
}
