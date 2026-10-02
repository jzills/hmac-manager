using HmacManager.Caching;
using HmacManager.Caching.Distributed;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Time.Testing;

namespace Unit.Tests.Caching.Distributed;

/// <summary>
/// The read before the write is a network round trip, so a request inside its window when the claim
/// starts can be outside it by the time the entry is written. The TTL is fixed before the read, so
/// the store never sees an expiry in the past, and a refusal only ever means the nonce was held.
/// </summary>
public class Test_NonceCache_Distributed_TryAddAsync_Expiry
{
    /// <summary>
    /// Validates expiries the way <c>RedisCache</c> does, and advances the clock during every read.
    /// </summary>
    private class RoundTripDistributedCache : IDistributedCache
    {
        private readonly FakeTimeProvider Clock;
        private readonly TimeSpan RoundTrip;

        public readonly List<DistributedCacheEntryOptions> Writes = new();

        public RoundTripDistributedCache(FakeTimeProvider clock, TimeSpan roundTrip)
        {
            Clock = clock;
            RoundTrip = roundTrip;
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            Clock.Advance(RoundTrip);
            return Task.FromResult<byte[]?>(null);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            if (options.AbsoluteExpiration <= Clock.GetUtcNow())
            {
                throw new ArgumentOutOfRangeException(nameof(options.AbsoluteExpiration), "The absolute expiration value must be in the future.");
            }

            if (options.AbsoluteExpirationRelativeToNow <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(options.AbsoluteExpirationRelativeToNow), "The relative expiration value must be positive.");
            }

            Writes.Add(options);
            return Task.CompletedTask;
        }

        public byte[]? Get(string key) => throw new NotSupportedException();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();
        public void Refresh(string key) => throw new NotSupportedException();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
        public void Remove(string key) => throw new NotSupportedException();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
    }

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);

    [Test]
    public async Task Test_TryAddAsync_WindowClosesDuringRead_ClaimsWithoutThrowing()
    {
        var clock = new FakeTimeProvider(Now);
        var store = new RoundTripDistributedCache(clock, TimeSpan.FromMilliseconds(5));
        var cache = new NonceDistributedCache(store, new NonceCacheOptions(), clock);

        // 2 ms left when the claim starts, 5 ms spent reading.
        var dateRequested = Now - MaxAge + TimeSpan.FromMilliseconds(2);

        Assert.IsTrue(await cache.TryAddAsync(Guid.NewGuid(), dateRequested, MaxAge));
        Assert.That(store.Writes.Single().AbsoluteExpirationRelativeToNow, Is.EqualTo(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task Test_TryAddAsync_WritesTtlRelativeToNow_FromBeforeTheRead()
    {
        var clock = new FakeTimeProvider(Now);
        var store = new RoundTripDistributedCache(clock, TimeSpan.FromMilliseconds(5));
        var cache = new NonceDistributedCache(store, new NonceCacheOptions(), clock);

        Assert.IsTrue(await cache.TryAddAsync(Guid.NewGuid(), Now, MaxAge));

        var write = store.Writes.Single();
        Assert.That(write.AbsoluteExpiration, Is.Null);
        Assert.That(write.AbsoluteExpirationRelativeToNow, Is.EqualTo(MaxAge));
    }
}
