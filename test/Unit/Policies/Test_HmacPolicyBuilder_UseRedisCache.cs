using HmacManager.Caching;
using HmacManager.Policies;

namespace Unit.Tests;

public class Test_HmacPolicyBuilder_UseRedisCache
{
    [Test]
    public void Test_UseRedisCache_SetsCacheTypeAndMaxAge()
    {
        var policy = new HmacPolicyBuilder("MyPolicy")
            .UsePublicKey(Guid.NewGuid())
            .UsePrivateKey("Hii9mvaSlUm9RRLwsfuUcg==")
            .UseRedisCache(45)
            .Build();

        Assert.That(policy.Nonce.CacheType, Is.EqualTo(NonceCacheType.Redis));
        Assert.That(policy.Nonce.MaxAgeInSeconds, Is.EqualTo(45));
    }
}
