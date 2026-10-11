using HmacManager.Caching;
using HmacManager.Caching.StackExchangeRedis;
using HmacManager.Common;
using HmacManager.Components;
using HmacManager.Mvc.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HmacManager.StackExchangeRedis.Tests;

/// <summary>
/// The registration an application makes: policies on <c>Distributed</c> are protected by Redis,
/// policies on <c>Memory</c> keep the built-in cache.
/// </summary>
public class Test_AddStackExchangeRedisNonceCache
{
    private const string Configuration = "localhost:6379";

    private const string DistributedPolicy = "DistributedPolicy";
    private const string MemoryPolicy = "MemoryPolicy";

    private static readonly Guid PublicKey = Guid.NewGuid();
    private static readonly string PrivateKey = Convert.ToBase64String("thisIsMySuperCoolPrivateKey"u8.ToArray());

    private IConnectionMultiplexer? Redis;

    [OneTimeSetUp]
    public async Task ConnectAsync() => Redis = await ConnectionMultiplexer.ConnectAsync(Configuration);

    [OneTimeTearDown]
    public async Task DisconnectAsync()
    {
        if (Redis is not null)
        {
            await Redis.DisposeAsync();
        }
    }

    private static ServiceProvider BuildProvider(string instanceName) =>
        new ServiceCollection()
            .AddMemoryCache()
            .AddHmacManager(options =>
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
            })
            .AddStackExchangeRedisNonceCache(options =>
            {
                options.Configuration = Configuration;
                options.InstanceName = instanceName;
            })
            .BuildServiceProvider();

    private static async Task<HttpRequestMessage> SignAsync(IServiceProvider provider, string policy)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/endpoint");

        using var scope = provider.CreateScope();
        var signer = scope.ServiceProvider.GetRequiredService<IHmacManagerFactory>().Create(policy)!;
        Assert.IsTrue((await signer.SignAsync(request)).IsSuccess);

        return request;
    }

    /// <summary>
    /// A copy of <paramref name="request"/>, as a second delivery of it would arrive: its own
    /// message, so concurrent verifications never share one.
    /// </summary>
    private static HttpRequestMessage Copy(HttpRequestMessage request)
    {
        var copy = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return copy;
    }

    private static async Task<bool> VerifyInNewScopeAsync(IServiceProvider provider, string policy, HttpRequestMessage request)
    {
        using var scope = provider.CreateScope();
        var verifier = scope.ServiceProvider.GetRequiredService<IHmacManagerFactory>().Create(policy)!;
        return (await verifier.VerifyAsync(request)).IsSuccess;
    }

    private static INonceCache? ResolveCache(IServiceProvider provider, NonceCacheType cacheType)
    {
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IComponentCollection<INonceCache>>().Get(Enum.GetName(cacheType)!);
    }

    [Test]
    public async Task Test_DistributedPolicies_AreProtectedByRedis()
    {
        var instanceName = $"test-{Guid.NewGuid():N}:";
        await using var provider = BuildProvider(instanceName);
        var request = await SignAsync(provider, DistributedPolicy);

        Assert.IsTrue(await VerifyInNewScopeAsync(provider, DistributedPolicy, request));
        Assert.IsFalse(await VerifyInNewScopeAsync(provider, DistributedPolicy, request));

        // The library's fallback IDistributedCache is in-process, so a key in Redis is what shows
        // the claim was made there.
        var keys = Redis!.GetServers().Single().Keys(pattern: $"{instanceName}*").ToArray();
        Assert.That(keys, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task Test_MemoryPolicies_KeepTheBuiltInCache()
    {
        await using var provider = BuildProvider($"test-{Guid.NewGuid():N}:");
        var request = await SignAsync(provider, MemoryPolicy);

        Assert.IsTrue(await VerifyInNewScopeAsync(provider, MemoryPolicy, request));
        Assert.IsFalse(await VerifyInNewScopeAsync(provider, MemoryPolicy, request));

        var builtInMemoryCache = typeof(NonceCache).Assembly.GetType("HmacManager.Caching.Memory.NonceMemoryCache", throwOnError: true)!;
        Assert.That(ResolveCache(provider, NonceCacheType.Memory), Is.TypeOf(builtInMemoryCache));
        Assert.That(ResolveCache(provider, NonceCacheType.Distributed), Is.TypeOf<RedisNonceCache>());
    }

    // Two replicas behind a load balancer, each with its own container, sharing one Redis. Copies of
    // one signed request reaching both at the same moment: exactly one may be accepted.
    [Test]
    public async Task Test_TwoInstancesSharingRedis_AcceptExactlyOneOfConcurrentCopies()
    {
        var instanceName = $"test-{Guid.NewGuid():N}:";
        await using var first = BuildProvider(instanceName);
        await using var second = BuildProvider(instanceName);

        for (var run = 0; run < 10; run++)
        {
            var request = await SignAsync(first, DistributedPolicy);

            var results = await Task.WhenAll(Enumerable.Range(0, 16)
                .Select(copy => VerifyInNewScopeAsync(copy % 2 == 0 ? first : second, DistributedPolicy, Copy(request))));

            Assert.That(results.Count(accepted => accepted), Is.EqualTo(1));
        }
    }

    // The registration documented for sharing a connection the application already has.
    [Test]
    public async Task Test_ASharedConnection_IsUsedAndLeftOpen()
    {
        var instanceName = $"test-{Guid.NewGuid():N}:";
        var services = new ServiceCollection()
            .AddMemoryCache()
            .AddSingleton(Redis!)
            .AddHmacManager(options =>
            {
                options.AddPolicy(DistributedPolicy, policy =>
                {
                    policy.UsePublicKey(PublicKey);
                    policy.UsePrivateKey(PrivateKey);
                    policy.UseDistributedCache(60);
                });
            })
            .AddStackExchangeRedisNonceCache(options => options.InstanceName = instanceName);

        services.AddOptions<RedisNonceCacheOptions>()
            .Configure<IConnectionMultiplexer>((options, redis) =>
                options.ConnectionMultiplexerFactory = () => Task.FromResult(redis));

        await using (var provider = services.BuildServiceProvider())
        {
            var request = await SignAsync(provider, DistributedPolicy);
            Assert.IsTrue(await VerifyInNewScopeAsync(provider, DistributedPolicy, request));
            Assert.IsFalse(await VerifyInNewScopeAsync(provider, DistributedPolicy, request));
        }

        Assert.That(Redis!.GetServers().Single().Keys(pattern: $"{instanceName}*").ToArray(), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task Test_WithoutAConnection_FailsValidation()
    {
        await using var provider = new ServiceCollection()
            .AddMemoryCache()
            .AddHmacManager(_ => { })
            .AddStackExchangeRedisNonceCache(_ => { })
            .BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => ResolveCache(provider, NonceCacheType.Distributed));
    }

    [Test]
    public void Test_NullSetupAction_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddStackExchangeRedisNonceCache(null!));
    }
}
