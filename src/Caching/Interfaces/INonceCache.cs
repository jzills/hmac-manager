namespace HmacManager.Caching;

/// <summary>
/// Defines methods for managing nonces in a cache.
/// </summary>
public interface INonceCache
{
    /// <summary>
    /// Stores a nonce along with the date and time it was requested.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce.</param>
    /// <param name="dateRequested">The date and time when the nonce was requested.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetAsync(Guid nonce, DateTimeOffset dateRequested);

    /// <summary>
    /// Checks if the specified nonce exists in the cache.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce to check.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating whether the nonce exists.</returns>
    Task<bool> ContainsAsync(Guid nonce);

    /// <summary>
    /// Stores the specified nonce if it is not already in the cache.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce.</param>
    /// <param name="dateRequested">The date and time when the nonce was requested.</param>
    /// <param name="maxAge">The maximum age of a request under the policy the nonce was verified for.</param>
    /// <returns>A task that represents the asynchronous operation, containing <c>true</c> if the nonce was added; <c>false</c> if it was already present.</returns>
    async Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge)
    {
        if (await ContainsAsync(nonce))
        {
            return false;
        }

        await SetAsync(nonce, dateRequested);
        return true;
    }
}