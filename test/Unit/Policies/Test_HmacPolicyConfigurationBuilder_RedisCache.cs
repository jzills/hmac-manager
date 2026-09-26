using HmacManager.Caching;
using HmacManager.Components;
using HmacManager.Mvc;
using HmacManager.Mvc.Configuration;
using HmacManager.Policies;

namespace Unit.Tests;

public class Test_HmacPolicyConfigurationBuilder_RedisCache
{
    [Test]
    public void Test_ConfigurationWithRedisCacheType_SetsCacheTypeAndMaxAge()
    {
        var section = new HmacPolicyConfigurationSection
        {
            Name = "MyPolicy",
            Keys = new KeyCredentials
            {
                PublicKey = Guid.NewGuid(),
                PrivateKey = "Hii9mvaSlUm9RRLwsfuUcg=="
            },
            Algorithms = new Algorithms
            {
                ContentHashAlgorithm = ContentHashAlgorithm.SHA256,
                SigningHashAlgorithm = SigningHashAlgorithm.HMACSHA256
            },
            Nonce = new Nonce { CacheType = NonceCacheType.Redis, MaxAgeInSeconds = 45 }
        };

        var policy = new HmacPolicyConfigurationBuilder(section).Build();

        Assert.That(policy.Nonce.CacheType, Is.EqualTo(NonceCacheType.Redis));
        Assert.That(policy.Nonce.MaxAgeInSeconds, Is.EqualTo(45));
    }
}
