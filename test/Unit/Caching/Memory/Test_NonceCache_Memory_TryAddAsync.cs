using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HmacManager.Caching;
using HmacManager.Caching.Memory;

namespace Unit.Tests.Caching.Memory;

public class Test_NonceCache_Memory_TryAddAsync
{
    [Test]
    public async Task Test_TryAddAsync_Concurrent_SameNonce_ExactlyOneSucceeds()
    {
        var memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        var maxAge = TimeSpan.FromSeconds(30);

        for (var run = 0; run < 50; run++)
        {
            var nonce = Guid.NewGuid();
            var dateRequested = DateTimeOffset.UtcNow;

            var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
                new NonceMemoryCache(memoryCache, new NonceCacheOptions { MaxAgeInSeconds = 30 })
                    .TryAddAsync(nonce, dateRequested, maxAge))));

            Assert.That(results.Count(result => result), Is.EqualTo(1));
        }
    }
}
