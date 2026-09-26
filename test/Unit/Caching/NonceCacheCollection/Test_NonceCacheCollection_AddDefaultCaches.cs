using Microsoft.Extensions.DependencyInjection;
using HmacManager.Caching;
using HmacManager.Common.Extensions;
using HmacManager.Mvc.Extensions.Internal;

namespace Unit.Tests.Caching.Memory;

public class Test_NonceCacheCollection_AddDefaultCaches
{
    [Test]
    public void Test_AddDefaultCaches_WithoutConnectionMultiplexer_HasNoRedisCache()
    {
        var serviceProvider = new ServiceCollection()
            .AddMemoryCache()
            .AddDistributedMemoryCache()
            .BuildServiceProvider();

        var cacheCollection = new NonceCacheCollection().AddDefaultCaches(serviceProvider);

        Assert.IsTrue(cacheCollection.TryGetValue(Enum.GetName(NonceCacheType.Memory), out _));
        Assert.IsTrue(cacheCollection.TryGetValue(Enum.GetName(NonceCacheType.Distributed), out _));
        Assert.IsFalse(cacheCollection.TryGetValue(Enum.GetName(NonceCacheType.Redis), out _));
    }
}
