namespace HmacManager.Caching;

/// <summary>
/// Provides an abstract base class for a cache that stores nonces with expiration settings to prevent replay attacks.
/// </summary>
internal abstract class NonceCache : INonceCache
{
    /// <summary>
    /// Gets the configuration options for the nonce cache.
    /// </summary>
    protected readonly NonceCacheOptions Options;

    /// <summary>
    /// Initializes a new instance of the <see cref="NonceCache"/> class with the specified options.
    /// </summary>
    /// <param name="options">The options to configure the nonce cache.</param>
    protected NonceCache(NonceCacheOptions options) => Options = options;

    /// <summary>
    /// Checks asynchronously whether the specified nonce exists in the cache.
    /// </summary>
    /// <param name="nonce">The nonce to check for in the cache.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains <c>true</c> if the nonce exists in the cache; otherwise, <c>false</c>.</returns>
    public abstract Task<bool> ContainsAsync(Guid nonce);

    /// <summary>
    /// Adds the specified nonce to the cache asynchronously with an expiration time based on the provided date.
    /// </summary>
    /// <param name="nonce">The nonce to store in the cache.</param>
    /// <param name="dateRequested">The date and time the nonce was requested, used to calculate expiration.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SetAsync(Guid nonce, DateTimeOffset dateRequested) =>
        SetAsync(nonce, dateRequested, Options.MaxAgeInSeconds);

    public abstract Task SetAsync(Guid nonce, DateTimeOffset dateRequested, int maxAgeInSeconds);

    /// <inheritdoc/>
    public virtual async Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge)
    {
        if (await ContainsAsync(nonce))
        {
            return false;
        }

        await SetAsync(nonce, dateRequested, (int)maxAge.TotalSeconds);
        return true;
    }

    /// <summary>
    /// Creates a cache key for the specified nonce.
    /// </summary>
    /// <param name="nonce">The nonce for which to create a cache key.</param>
    /// <returns>A string representing the cache key for the specified nonce.</returns>
    protected string GetKey(Guid nonce) => Options.CreateKey(nonce);

    /// <summary>
    /// Calculates the absolute expiration date and time for a nonce based on the specified date.
    /// </summary>
    /// <param name="dateRequested">The date and time the nonce was requested.</param>
    /// <returns>The absolute expiration date and time for the nonce.</returns>
    protected static DateTimeOffset GetAbsoluteExpiration(DateTimeOffset dateRequested, int maxAgeInSeconds) => dateRequested.AddSeconds(maxAgeInSeconds);
}