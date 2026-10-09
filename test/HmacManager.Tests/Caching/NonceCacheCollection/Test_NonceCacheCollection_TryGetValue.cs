using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HmacManager.Caching;
using HmacManager.Caching.Distributed;
using HmacManager.Caching.Memory;
using HmacManager.Common.Extensions;

namespace Unit.Tests.Caching.Memory;

public class Test_NonceCacheCollection_TryGetValue : TestServiceCollection
{
    public INonceCache MemoryCache;
    public INonceCache DistributedCache;
    NonceCacheCollection CacheCollection;

    [SetUp]
    public void Init()
    {
        MemoryCache = new NonceMemoryCache(
            new MemoryCache(Options.Create(new MemoryCacheOptions())),
            new NonceCacheOptions()
        );

        DistributedCache = new NonceDistributedCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new NonceCacheOptions()
        );
        
        CacheCollection = new NonceCacheCollection();
    }

    [Test]
    public void Test_NonceCacheCollection_TryGetValue_Exists()
    {
        CacheCollection.Add(NonceCacheType.Memory, MemoryCache);
        CacheCollection.Add(NonceCacheType.Distributed, DistributedCache);

        Assert.IsTrue(CacheCollection.TryGetValue(Enum.GetName(NonceCacheType.Memory), out var memoryCache) && memoryCache is not null);
        Assert.IsTrue(CacheCollection.TryGetValue(Enum.GetName(NonceCacheType.Distributed), out var distributedCache) && distributedCache is not null);
    }

    [Test]
    public void Test_NonceCacheCollection_TryGetValue_DoesNotExist()
    {
        Assert.IsFalse(CacheCollection.TryGetValue(Enum.GetName(NonceCacheType.Memory), out var memoryCache) && memoryCache is not null);
        Assert.IsFalse(CacheCollection.TryGetValue(Enum.GetName(NonceCacheType.Distributed), out var distributedCache) && distributedCache is not null);
    }
}