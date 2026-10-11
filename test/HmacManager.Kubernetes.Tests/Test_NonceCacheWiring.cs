using HmacManager.Caching;
using HmacManager.Caching.StackExchangeRedis;
using HmacManager.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HmacManager.Kubernetes.Tests;

/// <summary>
/// Which nonce cache the ext-authz service's own startup wires up. Resolving the cache does not
/// connect, so no Redis is needed: the claims themselves are covered by the package's suite.
/// </summary>
public class Test_NonceCacheWiring
{
    private static INonceCache? ResolveCache(string? redisConnectionString, NonceCacheType cacheType)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // As a pod runs: appsettings.Development.json carries a placeholder policy whose
            // empty public key fails validation.
            builder.UseEnvironment("Production");

            // What the chart mounts at /etc/hmac-manager, inline.
            builder.UseSetting("HmacManager:0:Name", "MyPolicy");
            builder.UseSetting("HmacManager:0:Keys:PublicKey", "eb8e9dae-08bd-4883-80fe-1d9a103b30b5");
            builder.UseSetting("HmacManager:0:Keys:PrivateKey", "Hii9mvaSlUm9RRLwsfuUcg==");
            builder.UseSetting("HmacManager:0:Nonce:CacheType", "Distributed");
            builder.UseSetting("HmacManager:0:Nonce:MaxAgeInSeconds", "30");

            if (redisConnectionString is not null)
            {
                builder.UseSetting("ConnectionStrings:Redis", redisConnectionString);
            }
        });

        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IComponentCollection<INonceCache>>().Get(Enum.GetName(cacheType)!);
    }

    [Test]
    public void Test_WithARedisConnectionString_DistributedPoliciesClaimInRedis()
    {
        Assert.That(ResolveCache("localhost:6379", NonceCacheType.Distributed), Is.TypeOf<RedisNonceCache>());
    }

    [Test]
    public void Test_WithARedisConnectionString_MemoryPoliciesKeepTheBuiltInCache()
    {
        var builtInMemoryCache = typeof(NonceCache).Assembly.GetType("HmacManager.Caching.Memory.NonceMemoryCache", throwOnError: true)!;

        Assert.That(ResolveCache("localhost:6379", NonceCacheType.Memory), Is.TypeOf(builtInMemoryCache));
    }

    [Test]
    public void Test_WithoutARedisConnectionString_NothingClaimsInRedis()
    {
        Assert.That(ResolveCache(null, NonceCacheType.Distributed), Is.Not.TypeOf<RedisNonceCache>());
    }
}
