using HmacManager.Caching;
using HmacManager.Caching.Distributed;
using HmacManager.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Unit.Tests.Caching;

/// <summary>
/// A cache that replaces a built-in one on the same store has to key its entries the same way, or
/// a nonce the built-in cache recorded is unclaimed again the moment the replacement takes over.
/// </summary>
public class Test_NonceCache_Base_CreateKey
{
    private class KeyedNonceCache : NonceCache
    {
        public static string KeyFor(NonceCacheType cacheType, Guid nonce) => CreateKey(cacheType, nonce);

        protected override Task<bool> TryAddCoreAsync(Guid nonce, TimeSpan timeToLive) => Task.FromResult(true);
    }

    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);

    [Test]
    public async Task Test_CreateKey_IsTheKeyTheBuiltInDistributedCacheWrites()
    {
        var store = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var builtIn = new NonceDistributedCache(store, new NonceCacheOptions { CacheType = NonceCacheType.Distributed });
        var nonce = Guid.NewGuid();

        Assert.IsTrue(await builtIn.TryAddAsync(nonce, DateTimeOffset.UtcNow, MaxAge));
        Assert.That(await store.GetAsync(KeyedNonceCache.KeyFor(NonceCacheType.Distributed, nonce)), Is.Not.Null);
    }

    [Test]
    public async Task Test_CreateKey_IsTheKeyTheBuiltInMemoryCacheWrites()
    {
        using var store = new MemoryCache(new MemoryCacheOptions());
        var builtIn = new NonceMemoryCache(store, new NonceCacheOptions { CacheType = NonceCacheType.Memory });
        var nonce = Guid.NewGuid();

        Assert.IsTrue(await builtIn.TryAddAsync(nonce, DateTimeOffset.UtcNow, MaxAge));
        Assert.IsTrue(store.TryGetValue(KeyedNonceCache.KeyFor(NonceCacheType.Memory, nonce), out _));
    }
}
