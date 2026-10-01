namespace HmacManager.Caching;

/// <summary>
/// Records the nonces of verified requests, so a captured request cannot be replayed
/// within its validity window.
/// </summary>
/// <remarks>
/// <see cref="TryAddAsync"/> is the whole contract: it is the only member the library calls.
/// Derive from <see cref="NonceCache"/> rather than implementing this interface directly; it
/// computes each entry's expiry and refuses entries whose expiry has already passed, leaving
/// only the atomic claim to implement. <see cref="SetAsync(Guid, DateTimeOffset)"/> and
/// <see cref="ContainsAsync"/> are obsolete and will be removed in the next major version.
/// </remarks>
public interface INonceCache
{
    /// <summary>
    /// Stores a nonce along with the date and time it was requested.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce.</param>
    /// <param name="dateRequested">The date and time when the nonce was requested.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Obsolete("Implement TryAddAsync instead, or derive from NonceCache. This member will be removed in the next major version.")]
    Task SetAsync(Guid nonce, DateTimeOffset dateRequested) =>
        throw new NotSupportedException($"{GetType().Name} does not implement {nameof(SetAsync)}. Call {nameof(TryAddAsync)}.");

    /// <summary>
    /// Stores a nonce until the request's policy window expires.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce.</param>
    /// <param name="dateRequested">The date and time when the nonce was requested.</param>
    /// <param name="maxAge">The maximum age of a request under the policy.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Obsolete("Implement TryAddAsync instead, or derive from NonceCache. This member will be removed in the next major version.")]
    Task SetAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge) =>
        SetAsync(nonce, dateRequested);

    /// <summary>
    /// Checks if the specified nonce exists in the cache.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce to check.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating whether the nonce exists.</returns>
    [Obsolete("Implement TryAddAsync instead, or derive from NonceCache. This member will be removed in the next major version.")]
    Task<bool> ContainsAsync(Guid nonce) =>
        throw new NotSupportedException($"{GetType().Name} does not implement {nameof(ContainsAsync)}. Call {nameof(TryAddAsync)}.");

    /// <summary>
    /// Claims a nonce for the rest of its request's validity window, if it has not been claimed already.
    /// </summary>
    /// <param name="nonce">The unique identifier for the nonce.</param>
    /// <param name="dateRequested">The date and time when the nonce was requested.</param>
    /// <param name="maxAge">The maximum age of a request under the policy the nonce was verified for.</param>
    /// <returns>
    /// <c>true</c> if the nonce was unclaimed and is now recorded; <c>false</c> if it was already
    /// claimed, or if <paramref name="dateRequested"/> plus <paramref name="maxAge"/> is not in the future.
    /// </returns>
    /// <remarks>
    /// An implementation must:
    /// <list type="bullet">
    /// <item>check and record in one atomic step, so two concurrent copies of a request cannot both be accepted;</item>
    /// <item>keep the entry until at least <paramref name="dateRequested"/> plus <paramref name="maxAge"/>,
    /// because the request's signature stays valid until then;</item>
    /// <item>return <c>false</c> rather than store an entry whose expiry has already passed — some stores
    /// throw on an expiry in the past.</item>
    /// </list>
    /// The default exists only for caches written against the obsolete members. It honours the last rule
    /// but is check-then-set, so it is not atomic.
    /// </remarks>
    async Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge)
    {
        if (dateRequested + maxAge <= DateTimeOffset.UtcNow)
        {
            return false;
        }

#pragma warning disable CS0618 // The legacy path the obsolete members exist for.
        if (await ContainsAsync(nonce))
        {
            return false;
        }

        await SetAsync(nonce, dateRequested, maxAge);
#pragma warning restore CS0618
        return true;
    }
}
