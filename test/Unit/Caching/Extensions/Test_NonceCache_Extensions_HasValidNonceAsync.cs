using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HmacManager.Caching;
using HmacManager.Caching.Memory;
using HmacManager.Caching.Extensions;

namespace Unit.Tests.Caching.Memory;

[TestFixture(1)]
[TestFixture(5)]
[TestFixture(10)]
[TestFixture(20)]
[TestFixture(300)]
[TestFixture(int.MaxValue)]
public class Test_NonceCache_Extensions_HasValidNonceAsync : TestServiceCollection
{
    public INonceCache Cache;
    public readonly int MaxAgeInSeconds;

    public Test_NonceCache_Extensions_HasValidNonceAsync(int maxAgeInSeconds) => MaxAgeInSeconds = maxAgeInSeconds;

    [SetUp]
    public void Init()
    {
        Cache = new NonceMemoryCache(
            new MemoryCache(Options.Create(new MemoryCacheOptions())),
            new NonceCacheOptions { MaxAgeInSeconds = MaxAgeInSeconds }
        );
    }    

    [Test]
    [TestCaseSource(typeof(TestCaseData), nameof(TestCaseData.GetNonces))]
    public async Task Test_HasValidNonceAsync_IsValid(Guid nonce)
    {
        Assert.IsTrue(await Cache.IsValidNonceAsync(nonce, DateTimeOffset.Now, TimeSpan.FromSeconds(MaxAgeInSeconds)));
    }

    [Test]
    [TestCaseSource(typeof(TestCaseData), nameof(TestCaseData.GetNonces))]
    public async Task Test_HasValidNonceAsync_IsNotValid(Guid nonce)
    {
        await Cache.SetAsync(nonce, DateTimeOffset.Now);
   
        Assert.IsFalse(await Cache.IsValidNonceAsync(nonce, DateTimeOffset.Now, TimeSpan.FromSeconds(MaxAgeInSeconds)));
    }
}