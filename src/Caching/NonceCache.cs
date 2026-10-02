namespace HmacManager.Caching;

/// <summary>
/// The base class for an <see cref="INonceCache"/>. Derive from it to back replay protection with
/// a store of your own.
/// </summary>
/// <remarks>
/// <see cref="TryAddAsync"/> works out how long the entry must be kept and refuses one whose window
/// has already closed, so a derived class implements only <see cref="TryAddCoreAsync"/>: the atomic
/// claim against its store, for a time-to-live that is always positive.
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
    public Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge) =>
        TryGetTimeToLive(dateRequested + maxAge, out var timeToLive)
            ? TryAddCoreAsync(nonce, timeToLive)
            : Task.FromResult(false);

    /// <summary>
    /// Records <paramref name="nonce"/> for <paramref name="timeToLive"/> if it is not already
    /// recorded, as one atomic step.
    /// </summary>
    /// <param name="nonce">The nonce to claim.</param>
    /// <param name="timeToLive">How long to keep the entry, relative to now. Always positive, and a
    /// whole number of seconds no shorter than what is left of the request's window.</param>
    /// <returns><c>true</c> if the nonce was unclaimed and is now recorded; <c>false</c> if it was
    /// already claimed.</returns>
    protected abstract Task<bool> TryAddCoreAsync(Guid nonce, TimeSpan timeToLive);

    /// <summary>
    /// The time left until <paramref name="expiresAt"/>, rounded up to a whole second, or
    /// <c>false</c> if none is left.
    /// </summary>
    /// <remarks>
    /// A relative TTL rather than an absolute expiry, because a store reached over the network can
    /// see an absolute expiry pass on the way and some (<c>RedisCache</c>) throw on one in the past.
    /// Rounded up, because <c>RedisCache</c> truncates to whole seconds: an entry with 0.8s left would
    /// be written with no TTL at all and dropped at once, and one with 29.7s left would be dropped
    /// 0.7s before its signature stops verifying, leaving a replay window either way. Keeping an
    /// entry up to a second past its window costs nothing; a copy of the request arriving then is
    /// rejected as expired.
    /// </remarks>
    private protected bool TryGetTimeToLive(DateTimeOffset expiresAt, out TimeSpan timeToLive)
    {
        var remaining = expiresAt - Clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            timeToLive = default;
            return false;
        }

        var seconds = (remaining.Ticks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond;
        timeToLive = TimeSpan.FromSeconds(seconds);
        return true;
    }
}
