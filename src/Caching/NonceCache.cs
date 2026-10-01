namespace HmacManager.Caching;

/// <summary>
/// The base class for an <see cref="INonceCache"/>. Derive from it to back replay protection with
/// a store of your own.
/// </summary>
/// <remarks>
/// <see cref="TryAddAsync"/> computes when the entry expires and refuses one whose expiry has
/// already passed, so a derived class implements only <see cref="TryAddCoreAsync"/>: the atomic
/// claim against its store.
/// </remarks>
public abstract class NonceCache : INonceCache
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NonceCache"/> class.
    /// </summary>
    /// <param name="clock">The clock expiries are compared against. Defaults to <see cref="TimeProvider.System"/>.</param>
    protected NonceCache(TimeProvider? clock = null) => Clock = clock ?? TimeProvider.System;

    /// <summary>
    /// The clock expiries are compared against.
    /// </summary>
    protected TimeProvider Clock { get; }

    /// <inheritdoc/>
    public Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge)
    {
        var expiresAt = dateRequested + maxAge;
        if (expiresAt <= Clock.GetUtcNow())
        {
            return Task.FromResult(false);
        }

        return TryAddCoreAsync(nonce, expiresAt);
    }

    /// <summary>
    /// Records <paramref name="nonce"/> until <paramref name="expiresAt"/> if it is not already
    /// recorded, as one atomic step.
    /// </summary>
    /// <param name="nonce">The nonce to claim.</param>
    /// <param name="expiresAt">When the entry may be dropped. In the future by <see cref="Clock"/> when
    /// this is called; a store that converts it to a relative TTL should still treat a non-positive
    /// remainder as a lapsed claim and return <c>false</c>.</param>
    /// <returns><c>true</c> if the nonce was unclaimed and is now recorded; otherwise <c>false</c>.</returns>
    protected abstract Task<bool> TryAddCoreAsync(Guid nonce, DateTimeOffset expiresAt);
}
