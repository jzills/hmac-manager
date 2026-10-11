using System.Collections.Concurrent;
using HmacManager.Caching;
using HmacManager.Caching.Memory;
using HmacManager.Common;
using HmacManager.Components;
using HmacManager.Mvc.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Unit.Tests.Mvc.Extensions;

/// <summary>
/// <c>AddNonceCache</c> puts a cache behind one <see cref="NonceCacheType"/> and leaves the built-in
/// cache behind every other, so an application can replace one store without rebuilding the
/// collection the library registers.
/// </summary>
public class Test_AddNonceCache : TestBase
{
    private class DictionaryNonceCache : NonceCache
    {
        private readonly ConcurrentDictionary<Guid, TimeSpan> Nonces = new();

        protected override Task<bool> TryAddCoreAsync(Guid nonce, TimeSpan timeToLive) =>
            Task.FromResult(Nonces.TryAdd(nonce, timeToLive));

        public int Count => Nonces.Count;
    }

    private class ClaimLog
    {
        public readonly ConcurrentQueue<Guid> Claims = new();
    }

    private class LoggingNonceCache(ClaimLog log) : NonceCache
    {
        protected override Task<bool> TryAddCoreAsync(Guid nonce, TimeSpan timeToLive)
        {
            log.Claims.Enqueue(nonce);
            return Task.FromResult(true);
        }
    }

    private const string DistributedPolicy = "DistributedPolicy";
    private const string MemoryPolicy = "MemoryPolicy";

    private static IServiceCollection AddPolicies(IServiceCollection services) =>
        services.AddMemoryCache().AddHmacManager(options =>
        {
            options.AddPolicy(DistributedPolicy, policy =>
            {
                policy.UsePublicKey(PublicKey);
                policy.UsePrivateKey(PrivateKey);
                policy.UseDistributedCache(60);
            });

            options.AddPolicy(MemoryPolicy, policy =>
            {
                policy.UsePublicKey(PublicKey);
                policy.UsePrivateKey(PrivateKey);
                policy.UseMemoryCache(60);
            });
        });

    /// <summary>
    /// Signs one request, then verifies it twice, each in its own scope as two requests would be.
    /// </summary>
    private static async Task<(bool First, bool Replay)> VerifyTwiceAsync(IServiceProvider provider, string policy)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/endpoint");

        using (var scope = provider.CreateScope())
        {
            var signer = scope.ServiceProvider.GetRequiredService<IHmacManagerFactory>().Create(policy)!;
            Assert.IsTrue((await signer.SignAsync(request)).IsSuccess);
        }

        return (await VerifyInNewScopeAsync(provider, policy, request), await VerifyInNewScopeAsync(provider, policy, request));
    }

    private static async Task<bool> VerifyInNewScopeAsync(IServiceProvider provider, string policy, HttpRequestMessage request)
    {
        using var scope = provider.CreateScope();
        var verifier = scope.ServiceProvider.GetRequiredService<IHmacManagerFactory>().Create(policy)!;
        return (await verifier.VerifyAsync(request)).IsSuccess;
    }

    private static INonceCache? ResolveCache(IServiceScope scope, NonceCacheType cacheType) =>
        scope.ServiceProvider.GetRequiredService<IComponentCollection<INonceCache>>().Get(Enum.GetName(cacheType)!);

    [Test]
    public async Task Test_AddNonceCache_ProtectsThePoliciesOfItsType()
    {
        var cache = new DictionaryNonceCache();
        var services = AddPolicies(new ServiceCollection())
            .AddNonceCache(NonceCacheType.Distributed, _ => cache);

        using var provider = services.BuildServiceProvider();
        var (first, replay) = await VerifyTwiceAsync(provider, DistributedPolicy);

        Assert.IsTrue(first);
        Assert.IsFalse(replay);
        Assert.That(cache.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_AddNonceCache_KeepsTheBuiltInCacheForOtherTypes()
    {
        var cache = new DictionaryNonceCache();
        var services = AddPolicies(new ServiceCollection())
            .AddNonceCache(NonceCacheType.Distributed, _ => cache);

        using var provider = services.BuildServiceProvider();
        var (first, replay) = await VerifyTwiceAsync(provider, MemoryPolicy);

        Assert.IsTrue(first);
        Assert.IsFalse(replay);
        Assert.That(cache.Count, Is.Zero);

        using var scope = provider.CreateScope();
        Assert.That(ResolveCache(scope, NonceCacheType.Memory), Is.InstanceOf<NonceMemoryCache>());
    }

    [Test]
    public async Task Test_AddNonceCache_BeforeAddHmacManager_StillApplies()
    {
        var cache = new DictionaryNonceCache();
        var services = AddPolicies(new ServiceCollection()
            .AddNonceCache(NonceCacheType.Distributed, _ => cache));

        using var provider = services.BuildServiceProvider();
        var (first, replay) = await VerifyTwiceAsync(provider, DistributedPolicy);

        Assert.IsTrue(first);
        Assert.IsFalse(replay);
        Assert.That(cache.Count, Is.EqualTo(1));
    }

    // The collection is resolved per scope. A cache built per scope would start empty for every
    // request, so the replay below would be accepted.
    [Test]
    public async Task Test_AddNonceCache_SharesOneCacheAcrossScopes()
    {
        var services = AddPolicies(new ServiceCollection())
            .AddNonceCache<DictionaryNonceCache>(NonceCacheType.Distributed);

        using var provider = services.BuildServiceProvider();
        var (first, replay) = await VerifyTwiceAsync(provider, DistributedPolicy);

        Assert.IsTrue(first);
        Assert.IsFalse(replay);

        using var scope = provider.CreateScope();
        using var otherScope = provider.CreateScope();
        Assert.That(ResolveCache(scope, NonceCacheType.Distributed), Is.SameAs(ResolveCache(otherScope, NonceCacheType.Distributed)));
    }

    [Test]
    public async Task Test_AddNonceCache_ConstructsTheCacheThroughDependencyInjection()
    {
        var log = new ClaimLog();
        var services = AddPolicies(new ServiceCollection())
            .AddSingleton(log)
            .AddNonceCache<LoggingNonceCache>(NonceCacheType.Distributed);

        using var provider = services.BuildServiceProvider();
        var (first, _) = await VerifyTwiceAsync(provider, DistributedPolicy);

        Assert.IsTrue(first);
        Assert.That(log.Claims, Has.Count.EqualTo(2));
    }

    // The built-in distributed cache resolves IDistributedCache, and the library registers an
    // in-process one when the application has not. Neither should be built for a type whose cache
    // was replaced: the store may not exist in that deployment at all.
    [Test]
    public async Task Test_AddNonceCache_NeverBuildsTheCacheItReplaces()
    {
        var services = AddPolicies(new ServiceCollection()
            .AddSingleton<IDistributedCache>(_ => throw new InvalidOperationException("The replaced cache's store was resolved.")))
            .AddNonceCache<DictionaryNonceCache>(NonceCacheType.Distributed);

        using var provider = services.BuildServiceProvider();
        var (first, replay) = await VerifyTwiceAsync(provider, DistributedPolicy);

        Assert.IsTrue(first);
        Assert.IsFalse(replay);
    }

    [Test]
    public async Task Test_AddNonceCache_LastRegistrationForATypeWins()
    {
        var replaced = new DictionaryNonceCache();
        var cache = new DictionaryNonceCache();
        var services = AddPolicies(new ServiceCollection())
            .AddNonceCache(NonceCacheType.Distributed, _ => replaced)
            .AddNonceCache(NonceCacheType.Distributed, _ => cache);

        using var provider = services.BuildServiceProvider();
        var (first, _) = await VerifyTwiceAsync(provider, DistributedPolicy);

        Assert.IsTrue(first);
        Assert.That(cache.Count, Is.EqualTo(1));
        Assert.That(replaced.Count, Is.Zero);
    }

    [Test]
    public void Test_AddNonceCache_NullFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddNonceCache(NonceCacheType.Distributed, null!));
    }
}
