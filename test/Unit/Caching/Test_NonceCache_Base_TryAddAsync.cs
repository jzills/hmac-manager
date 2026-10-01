using HmacManager.Caching;
using Microsoft.Extensions.Time.Testing;

namespace Unit.Tests.Caching;

public class Test_NonceCache_Base_TryAddAsync
{
    private class RecordingNonceCache : NonceCache
    {
        public readonly List<(Guid Nonce, DateTimeOffset ExpiresAt)> Claims = new();

        public RecordingNonceCache(TimeProvider clock) : base(clock)
        {
        }

        protected override Task<bool> TryAddCoreAsync(Guid nonce, DateTimeOffset expiresAt)
        {
            Claims.Add((nonce, expiresAt));
            return Task.FromResult(true);
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);

    [Test]
    public async Task Test_TryAddAsync_InWindow_ClaimsUntilDateRequestedPlusMaxAge()
    {
        var cache = new RecordingNonceCache(new FakeTimeProvider(Now));
        var nonce = Guid.NewGuid();

        Assert.IsTrue(await cache.TryAddAsync(nonce, Now.AddSeconds(-10), MaxAge));
        Assert.That(cache.Claims, Is.EqualTo(new[] { (nonce, Now.AddSeconds(20)) }));
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
        Assert.That(cache.Claims.Single().ExpiresAt, Is.EqualTo(Now.AddSeconds(35)));
    }
}
