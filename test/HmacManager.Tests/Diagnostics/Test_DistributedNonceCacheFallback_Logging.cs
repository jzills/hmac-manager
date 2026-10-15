using System.Text;
using HmacManager.Caching;
using HmacManager.Common;
using HmacManager.Mvc.Extensions;
using HmacManager.Mvc.Extensions.Internal;
using HmacManager.Policies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Unit.Tests.Diagnostics;

/// <summary>
/// With no <see cref="IDistributedCache"/> registered, <c>AddHmacManager</c> falls back to
/// <see cref="MemoryDistributedCache"/>, so a <c>Distributed</c> policy keeps its nonces in one
/// process while looking configured for many. Nothing fails, so the startup warning is the only
/// sign of it. These tests pin when it is given, and that it is not given when the cache is shared
/// or was never the built-in one.
/// </summary>
[TestFixture]
public class Test_DistributedNonceCacheFallback_Logging : TestBase
{
    private const int DistributedNonceCacheNotShared = 1203;

    private RecordingLogger<DistributedNonceCacheFallbackCheck> Logger = null!;

    [SetUp]
    public void SetUp() => Logger = new RecordingLogger<DistributedNonceCacheFallbackCheck>();

    private IServiceCollection Services(params (string Name, NonceCacheType CacheType)[] policies)
    {
        // AddOptions: every host registers it, and MemoryDistributedCache cannot be built without it.
        var services = new ServiceCollection()
            .AddOptions()
            .AddSingleton<ILogger<DistributedNonceCacheFallbackCheck>>(Logger)
            .AddHmacManager(options =>
            {
                foreach (var (name, cacheType) in policies)
                {
                    options.AddPolicy(name, policy =>
                    {
                        policy.UsePublicKey(PublicKey);
                        policy.UsePrivateKey(PrivateKey);
                        if (cacheType == NonceCacheType.Distributed)
                        {
                            policy.UseDistributedCache(60);
                        }
                        else
                        {
                            policy.UseMemoryCache(60);
                        }
                    });
                }
            });

        return services;
    }

    private static async Task StartAsync(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        foreach (var hostedService in provider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
    }

    [Test]
    public async Task Start_WithADistributedPolicyAndNoDistributedCache_WarnsNamingThePolicies()
    {
        await StartAsync(Services(
            ("PolicyA", NonceCacheType.Distributed),
            ("PolicyB", NonceCacheType.Memory),
            ("PolicyC", NonceCacheType.Distributed)));

        var entry = Logger.WithEventId(DistributedNonceCacheNotShared).Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(entry.Message, Does.Contain("PolicyA").And.Contain("PolicyC").And.Not.Contain("PolicyB"));
        Assert.That(entry.Message, Does.Not.Contain(PrivateKey));
    }

    [Test]
    public async Task Start_WithAnExplicitDistributedMemoryCache_StillWarns()
    {
        // AddDistributedMemoryCache is the same in-process store, registered on purpose or not.
        var services = Services(("PolicyA", NonceCacheType.Distributed));
        services.AddDistributedMemoryCache();

        await StartAsync(services);

        Assert.That(Logger.WithEventId(DistributedNonceCacheNotShared), Has.Exactly(1).Items);
    }

    [Test]
    public async Task Start_WithASharedDistributedCache_DoesNotWarn()
    {
        var services = Services(("PolicyA", NonceCacheType.Distributed));
        services.AddStackExchangeRedisCache(options => options.Configuration = "localhost:6379");

        await StartAsync(services);

        Assert.That(Logger.WithEventId(DistributedNonceCacheNotShared), Is.Empty);
    }

    [Test]
    public async Task Start_WithOnlyMemoryPolicies_DoesNotWarn()
    {
        await StartAsync(Services(("PolicyA", NonceCacheType.Memory)));

        Assert.That(Logger.WithEventId(DistributedNonceCacheNotShared), Is.Empty);
    }

    [Test]
    public async Task Start_WhenAddNonceCacheReplacesDistributed_DoesNotWarnOrResolveTheStore()
    {
        var resolved = false;
        var services = Services(("PolicyA", NonceCacheType.Distributed));
        services.AddSingleton<IDistributedCache>(_ =>
        {
            resolved = true;
            return new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        });
        services.AddNonceCache(NonceCacheType.Distributed, _ => new NonceMemoryCacheStub());

        await StartAsync(services);

        Assert.That(Logger.WithEventId(DistributedNonceCacheNotShared), Is.Empty);
        Assert.IsFalse(resolved, "a replaced built-in's store is never resolved");
    }

    [Test]
    public async Task Start_WithACacheCollectionOfItsOwn_DoesNotWarn()
    {
        var services = Services(("PolicyA", NonceCacheType.Distributed));
        services.AddScoped<IComponentCollection<INonceCache>>(_ =>
        {
            var caches = new NonceCacheCollection();
            caches.Add(NonceCacheType.Distributed, new NonceMemoryCacheStub());
            return caches;
        });

        await StartAsync(services);

        Assert.That(Logger.WithEventId(DistributedNonceCacheNotShared), Is.Empty);
    }

    [Test]
    public async Task Start_WithPoliciesFromConfiguration_Warns()
    {
        var json = $$"""
            { "HmacManager": [ {
                "Name": "FromConfig",
                "Keys": { "PublicKey": "{{PublicKey}}", "PrivateKey": "{{PrivateKey}}" },
                "Nonce": { "CacheType": "Distributed", "MaxAgeInSeconds": 60 }
            } ] }
            """;
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();

        // AddOptions: every host registers it, and MemoryDistributedCache cannot be built without it.
        var services = new ServiceCollection()
            .AddOptions()
            .AddSingleton<ILogger<DistributedNonceCacheFallbackCheck>>(Logger)
            .AddSingleton(typeof(ILogger<>), typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>))
            .AddHmacManager(configuration.GetSection("HmacManager"));

        await StartAsync(services);

        Assert.That(Logger.WithEventId(DistributedNonceCacheNotShared).Single().Message, Does.Contain("FromConfig"));
    }

    // Scoped policies are resolved per request, from wherever the application keeps them, so there is
    // no policy set to inspect at startup.
    [Test]
    public async Task Start_WithScopedPolicies_DoesNotCheck()
    {
        // AddOptions: every host registers it, and MemoryDistributedCache cannot be built without it.
        var services = new ServiceCollection()
            .AddOptions()
            .AddSingleton<ILogger<DistributedNonceCacheFallbackCheck>>(Logger)
            .AddHmacManager(options => options.EnableScopedPolicies(_ => new HmacPolicyCollection()));

        await StartAsync(services);

        Assert.That(Logger.Entries, Is.Empty);
    }

    private sealed class NonceMemoryCacheStub : NonceCache
    {
        protected override Task<bool> TryAddCoreAsync(Guid nonce, TimeSpan timeToLive) => Task.FromResult(true);
    }
}
