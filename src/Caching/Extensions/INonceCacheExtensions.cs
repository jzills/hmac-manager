namespace HmacManager.Caching.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="INonceCache"/> interface.
/// </summary>
internal static class INonceCacheExtensions
{
    /// <summary>
    /// Checks if the specified nonce is valid and stores it in the cache if valid.
    /// </summary>
    /// <param name="cache">The instance of <see cref="INonceCache"/> to operate on.</param>
    /// <param name="nonce">The unique identifier for the nonce.</param>
    /// <param name="dateRequested">The date and time when the nonce was requested.</param>
    /// <param name="maxAge">The maximum age of a request under the policy the nonce was verified for.</param>
    /// <returns>
    /// A task that represents the asynchronous operation, 
    /// containing a boolean indicating whether the nonce is valid (i.e., not already present in the cache).
    /// </returns>
    public static Task<bool> IsValidNonceAsync(
        this INonceCache cache, 
        Guid nonce, 
        DateTimeOffset dateRequested,
        TimeSpan maxAge
    ) => cache.TryAddAsync(nonce, dateRequested, maxAge);
}