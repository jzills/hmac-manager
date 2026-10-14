using HmacManager.Caching;
using Microsoft.Extensions.Time.Testing;

namespace Unit.Tests.Caching;

public class Test_NonceCache_Base_TryAddAsync
{
    private class RecordingNonceCache : NonceCache
    {
        public readonly List<(Guid Nonce, TimeSpan TimeToLive)> Claims = new();

        public RecordingNonceCache(TimeProvider clock) : base(clock)
        {
        }

        protected override Task<bool> TryAddCoreAsync(Guid nonce, TimeSpan timeToLive)
        {
            Claims.Add((nonce, timeToLive));
            return Task.FromResult(true);
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);

    [Test]
    public async Task Test_TryAddAsync_InWindow_ClaimsForTheRestOfTheWindow()
    {
        var cache = new RecordingNonceCache(new FakeTimeProvider(Now));
        var nonce = Guid.NewGuid();

        Assert.IsTrue(await cache.TryAddAsync(nonce, Now.AddSeconds(-10), MaxAge));
        Assert.That(cache.Claims, Is.EqualTo(new[] { (nonce, TimeSpan.FromSeconds(20)) }));
    }

    // RedisCache truncates a TTL to whole seconds, so anything less than the remainder rounded up
    // drops the entry while its signature still verifies.
    [TestCase(1, 1, Description = "1 ms left")]
    [TestCase(800, 1, Description = "Under a second left: truncated, it would be dropped at once")]
    [TestCase(1000, 1, Description = "Exactly a second left")]
    [TestCase(1001, 2, Description = "Just over a second left")]
    [TestCase(29_700, 30, Description = "Truncated, it would be dropped 0.7s early")]
    public async Task Test_TryAddAsync_RoundsTheTimeToLiveUpToAWholeSecond(int remainingMilliseconds, int expectedSeconds)
    {
        var cache = new RecordingNonceCache(new FakeTimeProvider(Now));
        var dateRequested = Now - MaxAge + TimeSpan.FromMilliseconds(remainingMilliseconds);

        Assert.IsTrue(await cache.TryAddAsync(Guid.NewGuid(), dateRequested, MaxAge));
        Assert.That(cache.Claims.Single().TimeToLive, Is.EqualTo(TimeSpan.FromSeconds(expectedSeconds)));
    }

    [TestCase(0, Description = "Expires exactly now")]
    [TestCase(1, Description = "Expired a second ago")]
    [TestCase(3600, Description = "Expired an hour ago")]
    public async Task Test_TryAddAsync_ExpiryNotInFuture_RefusesWithoutReachingStore(int secondsPastExpiry)
    {
        var cache = new RecordingNonceCache(new FakeTimeProvider(Now));
        var dateRequested = Now - MaxAge - TimeSpan.FromSeconds(secondsPastExpiry);

        Assert.IsFalse(await cache.TryAddAsync(Guid.NewGuid(), dateRequested, MaxAge));
        Assert.That(cache.Claims, Is.Empty);
    }

    [Test]
    public async Task Test_TryAddAsync_DateRequestedInFuture_IsClaimed()
    {
        // Rejecting a request dated in the future is the manager's date check, not the cache's:
        // the cache only refuses an entry that would already be expired.
        var cache = new RecordingNonceCache(new FakeTimeProvider(Now));

        Assert.IsTrue(await cache.TryAddAsync(Guid.NewGuid(), Now.AddSeconds(5), MaxAge));
        Assert.That(cache.Claims.Single().TimeToLive, Is.EqualTo(TimeSpan.FromSeconds(35)));
    }
}
