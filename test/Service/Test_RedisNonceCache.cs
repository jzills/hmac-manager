using HmacManager.Caching;
using HmacManager.Common;
using HmacManager.Kubernetes.Caching;
using HmacManager.Mvc.Extensions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace HmacManager.Service.Tests;

/// <summary>
/// The claim itself, against a real Redis on the default port.
/// </summary>
public class Test_RedisNonceCache
{
    private const string Configuration = "localhost:6379";

    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);

    private IConnectionMultiplexer Redis = null!;

    [OneTimeSetUp]
    public async Task Connect() => Redis = await ConnectionMultiplexer.ConnectAsync(Configuration);

    [OneTimeTearDown]
    public void Disconnect() => Redis.Dispose();

    [Test]
    public async Task Test_ConcurrentClaims_ExactlyOneWins()
    {
        var cache = new RedisNonceCache(Redis);

        for (var run = 0; run < 20; run++)
        {
            var nonce = Guid.NewGuid();
            var dateRequested = DateTimeOffset.UtcNow;

            var results = await Task.WhenAll(Enumerable.Range(0, 16)
                .Select(_ => cache.TryAddAsync(nonce, dateRequested, MaxAge)));

            Assert.That(results.Count(claimed => claimed), Is.EqualTo(1));
        }
    }

    [TestCase(-10_000, 19, 20, Description = "20s left")]
    [TestCase(-29_200, 0.5, 1, Description = "0.8s left: still held, not dropped at once")]
    public async Task Test_Claim_IsHeldForTheRestOfTheWindow(int signedMsAgo, double minSeconds, double maxSeconds)
    {
        var cache = new RedisNonceCache(Redis);
        var nonce = Guid.NewGuid();

        Assert.IsTrue(await cache.TryAddAsync(nonce, DateTimeOffset.UtcNow.AddMilliseconds(signedMsAgo), MaxAge));

        var ttl = await Redis.GetDatabase().KeyTimeToLiveAsync(RedisNonceCache.CreateKey(nonce));
        Assert.That(ttl, Is.GreaterThan(TimeSpan.FromSeconds(minSeconds)).And.LessThanOrEqualTo(TimeSpan.FromSeconds(maxSeconds)));
    }

    [Test]
    public async Task Test_ClosedWindow_IsRefusedWithoutWriting()
    {
        var cache = new RedisNonceCache(Redis);
        var nonce = Guid.NewGuid();

        Assert.IsFalse(await cache.TryAddAsync(nonce, DateTimeOffset.UtcNow - MaxAge, MaxAge));
        Assert.IsFalse(await Redis.GetDatabase().KeyExistsAsync(RedisNonceCache.CreateKey(nonce)));
    }

    [Test]
    public async Task Test_NonceRecordedByTheLibraryDistributedCache_IsRefused()
    {
        // A rolling upgrade from a version that used the library's Distributed cache over RedisCache:
        // a nonce an old pod recorded must not be claimable on a new one.
        var services = new ServiceCollection()
            .AddStackExchangeRedisCache(options => options.Configuration = Configuration)
            .AddHmacManager(_ => { });

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var oldPod = scope.ServiceProvider
            .GetRequiredService<IComponentCollection<INonceCache>>()
            .Get(nameof(NonceCacheType.Distributed))!;

        var nonce = Guid.NewGuid();
        var dateRequested = DateTimeOffset.UtcNow;
        Assert.IsTrue(await oldPod.TryAddAsync(nonce, dateRequested, MaxAge));

        Assert.IsFalse(await new RedisNonceCache(Redis).TryAddAsync(nonce, dateRequested, MaxAge));
    }
}
