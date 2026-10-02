using HmacManager.Caching;
using HmacManager.Common;
using HmacManager.Mvc.Extensions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Integration.Caching;

/// <summary>
/// Nonce claims against a real Redis, which is what rejects an expiry in the past and what offers
/// the atomic claim <c>IDistributedCache</c> cannot.
/// </summary>
public class Test_NonceCache_Redis
{
    private const string Configuration = "localhost:6379";

    /// <summary>
    /// The example in <c>site/content/docs/dotnet/custom-nonce-cache.md</c>, verbatim.
    /// </summary>
    private class RedisNonceCache(IConnectionMultiplexer redis) : NonceCache
    {
        protected override Task<bool> TryAddCoreAsync(Guid nonce, TimeSpan timeToLive) =>
            redis.GetDatabase().StringSetAsync(
                $"hmac:nonce:{nonce}", 1, timeToLive, When.NotExists);
    }

    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);

    [Test]
    public async Task Test_DistributedCache_OverRedis_NeverThrows_AtTheEdgeOfTheWindow()
    {
        var services = new ServiceCollection()
            .AddStackExchangeRedisCache(options => options.Configuration = Configuration)
            .AddHmacManager(_ => { });

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var cache = scope.ServiceProvider
            .GetRequiredService<IComponentCollection<INonceCache>>()
            .Get(nameof(NonceCacheType.Distributed))!;

        // Every claim starts with 0–40 ms of its window left, less than a Redis round trip
        // for some of them. Each may succeed or be refused, but none may throw.
        for (var remainingMs = 0; remainingMs <= 40; remainingMs++)
        {
            var dateRequested = DateTimeOffset.UtcNow - MaxAge + TimeSpan.FromMilliseconds(remainingMs);
            Assert.DoesNotThrowAsync(() => cache.TryAddAsync(Guid.NewGuid(), dateRequested, MaxAge));
        }

        var nonce = Guid.NewGuid();
        Assert.IsTrue(await cache.TryAddAsync(nonce, DateTimeOffset.UtcNow, MaxAge));
        Assert.IsFalse(await cache.TryAddAsync(nonce, DateTimeOffset.UtcNow, MaxAge));
    }

    /// <summary>
    /// <c>RedisCache</c> truncates a TTL to whole seconds: written as-is, an entry with 0.8s left gets no
    /// TTL and is gone at once, and one with 1.6s left is gone after 1s. Either way a replay sent while
    /// the signature still verifies would be accepted.
    /// </summary>
    [TestCase(800, 0)]
    [TestCase(1600, 1100)]
    public async Task Test_DistributedCache_OverRedis_RefusesAReplayForTheWholeWindow(int remainingMs, int replayAfterMs)
    {
        var services = new ServiceCollection()
            .AddStackExchangeRedisCache(options => options.Configuration = Configuration)
            .AddHmacManager(_ => { });

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var cache = scope.ServiceProvider
            .GetRequiredService<IComponentCollection<INonceCache>>()
            .Get(nameof(NonceCacheType.Distributed))!;

        var nonce = Guid.NewGuid();
        var dateRequested = DateTimeOffset.UtcNow - MaxAge + TimeSpan.FromMilliseconds(remainingMs);

        Assert.IsTrue(await cache.TryAddAsync(nonce, dateRequested, MaxAge));
        await Task.Delay(replayAfterMs);

        Assume.That(dateRequested + MaxAge, Is.GreaterThan(DateTimeOffset.UtcNow), "the window must still be open");
        Assert.IsFalse(await cache.TryAddAsync(nonce, dateRequested, MaxAge));
    }

    [Test]
    public async Task Test_DocumentedRedisNonceCache_AcceptsExactlyOneOfConcurrentClaims()
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(Configuration);
        var cache = new RedisNonceCache(redis);

        for (var run = 0; run < 20; run++)
        {
            var nonce = Guid.NewGuid();
            var dateRequested = DateTimeOffset.UtcNow;

            var results = await Task.WhenAll(Enumerable.Range(0, 16)
                .Select(_ => cache.TryAddAsync(nonce, dateRequested, MaxAge)));

            Assert.That(results.Count(claimed => claimed), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_DocumentedRedisNonceCache_KeepsTheEntryForTheRestOfTheWindow()
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(Configuration);
        var cache = new RedisNonceCache(redis);
        var nonce = Guid.NewGuid();

        Assert.IsTrue(await cache.TryAddAsync(nonce, DateTimeOffset.UtcNow.AddSeconds(-10), MaxAge));

        var ttl = await redis.GetDatabase().KeyTimeToLiveAsync($"hmac:nonce:{nonce}");
        Assert.That(ttl, Is.GreaterThan(TimeSpan.FromSeconds(18)).And.LessThanOrEqualTo(TimeSpan.FromSeconds(20)));
    }
}
