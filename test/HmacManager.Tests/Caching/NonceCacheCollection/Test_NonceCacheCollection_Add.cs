using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HmacManager.Caching;
using HmacManager.Caching.Distributed;
using HmacManager.Caching.Memory;

namespace Unit.Tests.Caching.Memory;

public class Test_NonceCacheCollection_Add : TestServiceCollection
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
    public void Test_NonceCacheCollection_Add_Exists()
    {
        CacheCollection.Add(NonceCacheType.Memory, MemoryCache);
        CacheCollection.Add(NonceCacheType.Distributed, DistributedCache);

        var memoryCache = CacheCollection.Get(Enum.GetName(NonceCacheType.Memory));
        var distributedCache = CacheCollection.Get(Enum.GetName(NonceCacheType.Distributed));

        Assert.IsNotNull(memoryCache);
        Assert.IsNotNull(distributedCache);
    }

    [Test]
    public void Test_NonceCacheCollection_Add_DoesNotExist()
    {
        var memoryCache = CacheCollection.Get(Enum.GetName(NonceCacheType.Memory));
        var distributedCache = CacheCollection.Get(Enum.GetName(NonceCacheType.Distributed));

        Assert.IsNull(memoryCache);
        Assert.IsNull(distributedCache);
    }
}