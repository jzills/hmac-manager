using HmacManager.Caching;
using HmacManager.Caching.Redis;
using HmacManager.Components;
using HmacManager.Mvc.Extensions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

[TestFixture]
public class Test_NonceRedisCache
{
    ConnectionMultiplexer Connection;
    NonceRedisCache Cache;
    NonceCacheOptions Options;
    readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);

    [OneTimeSetUp]
    public void Connect() => Connection = ConnectionMultiplexer.Connect("localhost:6379");

    [OneTimeTearDown]
    public void Disconnect() => Connection.Dispose();

    [SetUp]
    public void Setup()
    {
        Options = new NonceCacheOptions { CacheType = NonceCacheType.Redis, MaxAgeInSeconds = 30 };
        Cache = new NonceRedisCache(Connection, Options);
    }

    [Test]
    public async Task Test_TryAddAsync_FirstAdd_IsTrue_Replay_IsFalse()
    {
        var nonce = Guid.NewGuid();
        var dateRequested = DateTimeOffset.UtcNow;

        Assert.IsTrue(await Cache.TryAddAsync(nonce, dateRequested, MaxAge));
        Assert.IsFalse(await Cache.TryAddAsync(nonce, dateRequested, MaxAge));
    }

    [Test]
    public async Task Test_TryAddAsync_Concurrent_SameNonce_ExactlyOneSucceeds()
    {
        var nonce = Guid.NewGuid();
        var dateRequested = DateTimeOffset.UtcNow;

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            Cache.TryAddAsync(nonce, dateRequested, MaxAge))));

        Assert.That(results.Count(result => result), Is.EqualTo(1));
    }

    [Test]
    public async Task Test_TryAddAsync_SetsTtl_WithinMaxAge()
    {
        var nonce = Guid.NewGuid();

        Assert.IsTrue(await Cache.TryAddAsync(nonce, DateTimeOffset.UtcNow, MaxAge));

        var ttl = await Connection.GetDatabase().KeyTimeToLiveAsync(Options.CreateKey(nonce));
        Assert.That(ttl, Is.Not.Null);
        Assert.That(ttl!.Value, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(ttl.Value, Is.LessThanOrEqualTo(MaxAge));
    }

    [Test]
    public async Task Test_TryAddAsync_Expired_IsFalse_WritesNoKey()
    {
        var nonce = Guid.NewGuid();

        Assert.IsFalse(await Cache.TryAddAsync(nonce, DateTimeOffset.UtcNow - MaxAge - TimeSpan.FromSeconds(1), MaxAge));
        Assert.IsFalse(await Connection.GetDatabase().KeyExistsAsync(Options.CreateKey(nonce)));
    }

    [Test]
    public async Task Test_SignAndVerify_WithRedisCache_ReplayFails()
    {
        var services = new ServiceCollection()
            .AddMemoryCache()
            .AddDistributedMemoryCache()
            .AddSingleton<IConnectionMultiplexer>(Connection);

        services.AddHmacManager(options =>
            options.AddPolicy("MyPolicy", policy =>
            {
                policy.UsePublicKey(Guid.NewGuid());
                policy.UsePrivateKey("Hii9mvaSlUm9RRLwsfuUcg==");
                policy.UseRedisCache(30);
            }));

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var hmacManager = scope.ServiceProvider
            .GetRequiredService<IHmacManagerFactory>()
            .Create("MyPolicy")!;

        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api");
        Assert.IsTrue((await hmacManager.SignAsync(request)).IsSuccess);

        Assert.IsTrue((await hmacManager.VerifyAsync(request)).IsSuccess);
        Assert.IsFalse((await hmacManager.VerifyAsync(request)).IsSuccess);
    }
}
