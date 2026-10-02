using HmacManager.Caching;
using HmacManager.Caching.Distributed;
using HmacManager.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Unit.Tests.Caching;

[TestFixture(NonceCacheType.Memory, 1)]
[TestFixture(NonceCacheType.Memory, 300)]
[TestFixture(NonceCacheType.Memory, int.MaxValue)]
[TestFixture(NonceCacheType.Distributed, 1)]
[TestFixture(NonceCacheType.Distributed, 300)]
[TestFixture(NonceCacheType.Distributed, int.MaxValue)]
public class Test_NonceCache_BuiltIn_TryAddAsync
{
    private readonly NonceCacheType CacheType;
    private readonly TimeSpan MaxAge;
    private INonceCache Cache = null!;

    public Test_NonceCache_BuiltIn_TryAddAsync(NonceCacheType cacheType, int maxAgeInSeconds)
    {
        CacheType = cacheType;
        MaxAge = TimeSpan.FromSeconds(maxAgeInSeconds);
    }

    [SetUp]
    public void Init()
    {
        // The cache-level default is deliberately shorter than every policy max age under test,
        // so a claim that fell back to it would show up as an early expiry.
        var options = new NonceCacheOptions { CacheType = CacheType, MaxAgeInSeconds = 0 };

        Cache = CacheType switch
        {
            NonceCacheType.Memory => new NonceMemoryCache(
                new MemoryCache(Options.Create(new MemoryCacheOptions())), options),
            _ => new NonceDistributedCache(
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), options)
        };
    }

    [Test]
    [TestCaseSource(typeof(TestCaseData), nameof(TestCaseData.GetNonces))]
    public async Task Test_TryAddAsync_FirstClaimSucceeds_SecondIsRefused(Guid nonce)
    {
        var dateRequested = DateTimeOffset.UtcNow;

        Assert.IsTrue(await Cache.TryAddAsync(nonce, dateRequested, MaxAge));
        Assert.IsFalse(await Cache.TryAddAsync(nonce, dateRequested, MaxAge));
    }

    [Test]
    [TestCaseSource(typeof(TestCaseData), nameof(TestCaseData.GetNonces))]
    public async Task Test_TryAddAsync_ExpiredRequest_IsRefusedAndNotStored(Guid nonce)
    {
        var expired = DateTimeOffset.UtcNow - MaxAge - TimeSpan.FromSeconds(1);

        Assert.IsFalse(await Cache.TryAddAsync(nonce, expired, MaxAge));

        // Nothing was stored, so a request in its window with the same nonce still claims it.
        Assert.IsTrue(await Cache.TryAddAsync(nonce, DateTimeOffset.UtcNow, MaxAge));
    }
}
