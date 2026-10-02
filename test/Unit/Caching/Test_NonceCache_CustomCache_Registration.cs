using System.Collections.Concurrent;
using HmacManager.Caching;
using HmacManager.Common;
using HmacManager.Components;
using HmacManager.Mvc.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Unit.Tests.Caching;

/// <summary>
/// The registration documented in <c>site/content/docs/dotnet/custom-nonce-cache.md</c>: a cache
/// derived from <see cref="NonceCache"/>, registered after <c>AddHmacManager</c> in place of the
/// built-in cache for the type its policies select.
/// </summary>
public class Test_NonceCache_CustomCache_Registration : TestBase
{
    private class DictionaryNonceCache : NonceCache
    {
        private readonly ConcurrentDictionary<Guid, TimeSpan> Nonces = new();

        protected override Task<bool> TryAddCoreAsync(Guid nonce, TimeSpan timeToLive) =>
            Task.FromResult(Nonces.TryAdd(nonce, timeToLive));

        public int Count => Nonces.Count;
    }

    [Test]
    public async Task Test_CustomCache_RegisteredAfterAddHmacManager_ProtectsItsPolicies()
    {
        var cache = new DictionaryNonceCache();
        var services = new ServiceCollection()
            .AddHmacManager(options =>
            {
                options.AddPolicy("MyPolicy", policy =>
                {
                    policy.UsePublicKey(PublicKey);
                    policy.UsePrivateKey(PrivateKey);
                    policy.UseDistributedCache(60);
                });
            });

        services.AddScoped<IComponentCollection<INonceCache>>(_ =>
        {
            var caches = new NonceCacheCollection();
            caches.Add(NonceCacheType.Distributed, cache);
            return caches;
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var hmacManager = scope.ServiceProvider.GetRequiredService<IHmacManagerFactory>().Create("MyPolicy")!;

        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/endpoint");
        Assert.IsTrue((await hmacManager.SignAsync(request)).IsSuccess);

        Assert.IsTrue((await hmacManager.VerifyAsync(request)).IsSuccess);
        Assert.IsFalse((await hmacManager.VerifyAsync(request)).IsSuccess);
        Assert.That(cache.Count, Is.EqualTo(1));
    }
}
