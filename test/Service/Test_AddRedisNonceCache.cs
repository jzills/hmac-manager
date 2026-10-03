using HmacManager.Caching;
using HmacManager.Common;
using HmacManager.Components;
using HmacManager.Kubernetes.Caching;
using HmacManager.Mvc.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace HmacManager.Service.Tests;

public class Test_AddRedisNonceCache
{
    private const string Configuration = "localhost:6379";
    private const string PublicKey = "eb8e9dae-08bd-4883-80fe-1d9a103b30b5";
    private const string PrivateKey = "Hii9mvaSlUm9RRLwsfuUcg==";

    private static ServiceProvider CreateReplica(string cacheType = "Distributed")
    {
        // AddOptions stands in for the web host, which always registers it; the library's memory
        // cache needs IOptions<MemoryCacheOptions>.
        var services = new ServiceCollection().AddOptions().AddHmacManager(options =>
        {
            options.AddPolicy("MyPolicy", policy =>
            {
                policy.UsePublicKey(Guid.Parse(PublicKey));
                policy.UsePrivateKey(PrivateKey);
                if (cacheType == "Distributed")
                {
                    policy.UseDistributedCache(30);
                }
                else
                {
                    policy.UseMemoryCache(30);
                }
            });
        });

        return services.AddRedisNonceCache(Configuration).BuildServiceProvider();
    }

    [Test]
    public void Test_Distributed_IsServedByRedis_AndMemory_KeepsTheLibraryCache()
    {
        using var provider = CreateReplica();
        using var scope = provider.CreateScope();
        var caches = scope.ServiceProvider.GetRequiredService<IComponentCollection<INonceCache>>();

        Assert.That(caches.Get(nameof(NonceCacheType.Distributed)), Is.InstanceOf<RedisNonceCache>());
        Assert.That(caches.Get(nameof(NonceCacheType.Memory)), Is.Not.Null.And.Not.InstanceOf<RedisNonceCache>());
    }

    [Test]
    public void Test_CalledBeforeAddHmacManager_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddRedisNonceCache(Configuration));
    }

    [Test]
    public async Task Test_MemoryPolicy_StillVerifiesAndRefusesAReplay()
    {
        using var provider = CreateReplica(cacheType: "Memory");
        var request = await SignAsync(provider);

        Assert.IsTrue(await VerifyAsync(provider, Copy(request)));
        Assert.IsFalse(await VerifyAsync(provider, Copy(request)));
    }

    /// <summary>
    /// The race this cache exists to close: copies of one signed request arriving at the same moment
    /// on two replicas that share Redis. Through <c>IDistributedCache</c>'s read-then-write, several
    /// can be accepted; with <c>SET NX</c>, exactly one is.
    /// </summary>
    [Test]
    public async Task Test_ConcurrentCopiesAcrossTwoReplicas_ExactlyOneIsAccepted()
    {
        using var replicaA = CreateReplica();
        using var replicaB = CreateReplica();

        for (var run = 0; run < 20; run++)
        {
            var request = await SignAsync(replicaA);

            var results = await Task.WhenAll(Enumerable.Range(0, 16)
                .Select(i => Task.Run(() => VerifyAsync(i % 2 == 0 ? replicaA : replicaB, Copy(request)))));

            Assert.That(results.Count(accepted => accepted), Is.EqualTo(1), $"run {run}");
        }
    }

    private static async Task<HttpRequestMessage> SignAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IHmacManagerFactory>().Create("MyPolicy")!;
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/endpoint");
        Assert.IsTrue((await manager.SignAsync(request)).IsSuccess);
        return request;
    }

    private static async Task<bool> VerifyAsync(IServiceProvider provider, HttpRequestMessage request)
    {
        using var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IHmacManagerFactory>().Create("MyPolicy")!;
        return (await manager.VerifyAsync(request)).IsSuccess;
    }

    private static HttpRequestMessage Copy(HttpRequestMessage signed)
    {
        var copy = new HttpRequestMessage(signed.Method, signed.RequestUri);
        foreach (var header in signed.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return copy;
    }
}
