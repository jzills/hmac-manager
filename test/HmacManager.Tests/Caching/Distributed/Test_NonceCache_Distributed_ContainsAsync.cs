using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HmacManager.Caching;
using HmacManager.Caching.Distributed;

// The obsolete members are still supported on the built-in caches until they are removed.
#pragma warning disable CS0618

namespace Unit.Tests.Caching.Memory;

[TestFixture(1)]
[TestFixture(5)]
[TestFixture(10)]
[TestFixture(20)]
[TestFixture(300)]
[TestFixture(int.MaxValue)]
public class Test_NonceCache_Distributed_ContainsAsync : TestServiceCollection
{
    public INonceCache Cache;
    public readonly int MaxAgeInSeconds;

    public Test_NonceCache_Distributed_ContainsAsync(int maxAgeInSeconds) => MaxAgeInSeconds = maxAgeInSeconds;

    [SetUp]
    public void Init()
    {
        Cache = new NonceDistributedCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new NonceCacheOptions { MaxAgeInSeconds = MaxAgeInSeconds }
        );
    }

    [Test]
    [TestCaseSource(typeof(TestCaseData), nameof(TestCaseData.GetNonces))]
    public async Task Test_ContainsAsync_Exists(Guid nonce)
    {
        await Cache.SetAsync(nonce, DateTimeOffset.Now.AddSeconds(MaxAgeInSeconds));
        
        Assert.IsTrue(await Cache.ContainsAsync(nonce));
    }

    [Test]
    [TestCaseSource(typeof(TestCaseData), nameof(TestCaseData.GetNonces))]
    public async Task Test_ContainsAsync_DoesNotExist(Guid nonce)
    {
        Assert.IsFalse(await Cache.ContainsAsync(nonce));
    }
}