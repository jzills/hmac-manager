using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HmacManager.Caching;
using HmacManager.Caching.Extensions;
using HmacManager.Caching.Memory;

namespace Unit.Tests;

public class Test_Memory_ReplayAttack_NonceLifetime
{
    [Test]
    public async Task Test_Nonce_IsKept_ForThePolicyMaxAge_NotTheCacheDefault()
    {
        var cache = new NonceMemoryCache(
            new MemoryCache(Options.Create(new MemoryCacheOptions())),
            new NonceCacheOptions { MaxAgeInSeconds = 30 }
        );

        var nonce = Guid.NewGuid();
        var dateRequested = DateTimeOffset.UtcNow.AddSeconds(-60);

        Assert.IsTrue(await cache.IsValidNonceAsync(nonce, dateRequested, TimeSpan.FromSeconds(120)));
        Assert.IsFalse(await cache.IsValidNonceAsync(nonce, dateRequested, TimeSpan.FromSeconds(120)));
    }
}
