using HmacManager.Caching;
using HmacManager.Caching.StackExchangeRedis;
using HmacManager.Common;
using HmacManager.Mvc.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HmacManager.StackExchangeRedis.Tests;

/// <summary>
/// <see cref="RedisNonceCache"/> against a real Redis on the default port.
/// </summary>
public class Test_RedisNonceCache
{
    private const string Configuration = "localhost:6379";

    // Nothing listens here. A claim waits in StackExchange.Redis's backlog for the connection to come
    // back, for syncTimeout (5 seconds by default), before it fails.
    private const string Unreachable = "localhost:1,connectTimeout=250,syncTimeout=250,asyncTimeout=250,connectRetry=0";

    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);

    private IConnectionMultiplexer? Redis;

    private IConnectionMultiplexer Admin => Redis!;

    [OneTimeSetUp]
    public async Task ConnectAsync() =>
        Redis = await ConnectionMultiplexer.ConnectAsync($"{Configuration},allowAdmin=true");

    [OneTimeTearDown]
    public async Task DisconnectAsync()
    {
        if (Redis is not null)
        {
            await Redis.DisposeAsync();
        }
    }

    private static RedisNonceCache CreateCache(
        Action<RedisNonceCacheOptions> configure,
        ILogger<RedisNonceCache>? logger = null)
    {
        var options = new RedisNonceCacheOptions();
        configure(options);
        return new RedisNonceCache(Options.Create(options), logger ?? NullLogger<RedisNonceCache>.Instance);
    }

    private static RedisNonceCache CreateCache(ILogger<RedisNonceCache>? logger = null) =>
        CreateCache(options => options.Configuration = Configuration, logger);

    private static string KeyFor(Guid nonce, string? instanceName = null) =>
        $"{instanceName}HmacManager:Distributed:{nonce}";

    private static async Task<bool> EventuallyAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(100);
        }

        return condition();
    }

    private async Task<ClientInfo[]> ClientsNamedAsync(string name) =>
        (await Admin.GetServers().Single().ClientListAsync()).Where(client => client.Name == name).ToArray();

    [Test]
    public async Task Test_TryAddAsync_AcceptsExactlyOneOfConcurrentClaims()
    {
        await using var cache = CreateCache();

        for (var run = 0; run < 20; run++)
        {
            var nonce = Guid.NewGuid();
            var dateRequested = DateTimeOffset.UtcNow;

            var results = await Task.WhenAll(Enumerable.Range(0, 16)
                .Select(_ => cache.TryAddAsync(nonce, dateRequested, MaxAge)));

            Assert.That(results.Count(claimed => claimed), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_TryAddAsync_KeepsTheEntryForTheRestOfTheWindow()
    {
        await using var cache = CreateCache();
        var nonce = Guid.NewGuid();

        Assert.IsTrue(await cache.TryAddAsync(nonce, DateTimeOffset.UtcNow.AddSeconds(-10), MaxAge));

        var ttl = await Admin.GetDatabase().KeyTimeToLiveAsync(KeyFor(nonce));
        Assert.That(ttl, Is.GreaterThan(TimeSpan.FromSeconds(18)).And.LessThanOrEqualTo(TimeSpan.FromSeconds(20)));
    }

    [Test]
    public async Task Test_TryAddAsync_PrefixesEveryKeyWithTheInstanceName()
    {
        var instanceName = $"test-{Guid.NewGuid():N}:";
        await using var cache = CreateCache(options =>
        {
            options.Configuration = Configuration;
            options.InstanceName = instanceName;
        });
        var nonce = Guid.NewGuid();

        Assert.IsTrue(await cache.TryAddAsync(nonce, DateTimeOffset.UtcNow, MaxAge));
        Assert.IsTrue(await Admin.GetDatabase().KeyExistsAsync(KeyFor(nonce, instanceName)));
    }

    /// <summary>
    /// An application switching to this cache from <c>UseDistributedCache</c> over
    /// <c>AddStackExchangeRedisCache</c>, with the same <c>InstanceName</c>. A nonce the built-in
    /// cache recorded before the switch must still be claimed after it.
    /// </summary>
    [Test]
    public async Task Test_TryAddAsync_RefusesANonceTheBuiltInCacheRecordedOverRedisCache()
    {
        var instanceName = $"test-{Guid.NewGuid():N}:";
        await using var provider = new ServiceCollection()
            .AddStackExchangeRedisCache(options =>
            {
                options.Configuration = Configuration;
                options.InstanceName = instanceName;
            })
            .AddHmacManager(_ => { })
            .BuildServiceProvider();
        using var scope = provider.CreateScope();
        var builtIn = scope.ServiceProvider
            .GetRequiredService<IComponentCollection<INonceCache>>()
            .Get(nameof(NonceCacheType.Distributed))!;
        await using var cache = CreateCache(options =>
        {
            options.Configuration = Configuration;
            options.InstanceName = instanceName;
        });
        var nonce = Guid.NewGuid();

        Assert.IsTrue(await builtIn.TryAddAsync(nonce, DateTimeOffset.UtcNow, MaxAge));
        Assert.IsFalse(await cache.TryAddAsync(nonce, DateTimeOffset.UtcNow, MaxAge));
    }

    /// <summary>
    /// The other direction, as seen by an instance still on the built-in cache during a rolling
    /// switch. <c>RedisCache</c> reads an entry as a hash, and this cache writes a string, so the
    /// read fails rather than answering. The copy is refused either way: never accepted.
    /// </summary>
    [Test]
    public async Task Test_BuiltInCacheOverRedisCache_FailsOnANonceThisCacheRecorded()
    {
        await using var provider = new ServiceCollection()
            .AddStackExchangeRedisCache(options => options.Configuration = Configuration)
            .AddHmacManager(_ => { })
            .BuildServiceProvider();
        using var scope = provider.CreateScope();
        var builtIn = scope.ServiceProvider
            .GetRequiredService<IComponentCollection<INonceCache>>()
            .Get(nameof(NonceCacheType.Distributed))!;
        await using var cache = CreateCache();
        var nonce = Guid.NewGuid();

        Assert.IsTrue(await cache.TryAddAsync(nonce, DateTimeOffset.UtcNow, MaxAge));
        Assert.That(async () => await builtIn.TryAddAsync(nonce, DateTimeOffset.UtcNow, MaxAge),
            Throws.InstanceOf<RedisServerException>().With.Message.Contains("WRONGTYPE"));
    }

    // Failing closed: the request fails rather than being accepted without replay protection, and
    // the fault is recorded as one rather than as a replay.
    [Test]
    public async Task Test_TryAddAsync_WhenRedisIsUnreachable_ThrowsAndLogsTheFault()
    {
        var logger = new RecordingLogger<RedisNonceCache>();
        await using var cache = CreateCache(options => options.Configuration = Unreachable, logger);

        Assert.That(async () => await cache.TryAddAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, MaxAge),
            Throws.InstanceOf<RedisException>().Or.InstanceOf<TimeoutException>());

        var fault = logger.WithEventId(1400).Single();
        Assert.That(fault.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(fault.Exception, Is.Not.Null);
    }

    [Test]
    public async Task Test_Logs_NeverIncludeThePassword()
    {
        const string password = "correct-horse-battery-staple";
        var logger = new RecordingLogger<RedisNonceCache>();
        await using var cache = CreateCache(options => options.Configuration = $"{Unreachable},password={password}", logger);

        Assert.That(async () => await cache.TryAddAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, MaxAge), Throws.Exception);

        Assert.That(logger.Entries, Is.Not.Empty);
        foreach (var entry in logger.Entries)
        {
            Assert.That(entry.Message, Does.Not.Contain(password));
            Assert.That(entry.Exception?.ToString() ?? string.Empty, Does.Not.Contain(password));
        }
    }

    // A failed connect is not remembered: the next claim tries again, and succeeds once Redis is back.
    [Test]
    public async Task Test_TryAddAsync_AfterAFailedConnect_ConnectsAgain()
    {
        var attempts = 0;
        await using var cache = CreateCache(options => options.ConnectionMultiplexerFactory = () =>
            ++attempts == 1
                ? throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is not up yet.")
                : Task.FromResult(Admin));

        Assert.That(async () => await cache.TryAddAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, MaxAge),
            Throws.InstanceOf<RedisConnectionException>());
        Assert.IsTrue(await cache.TryAddAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, MaxAge));
        Assert.That(attempts, Is.EqualTo(2));
    }

    [Test]
    public async Task Test_DisposeAsync_LeavesASuppliedConnectionOpen()
    {
        var cache = CreateCache(options => options.ConnectionMultiplexerFactory = () => Task.FromResult(Admin));
        Assert.IsTrue(await cache.TryAddAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, MaxAge));

        await cache.DisposeAsync();

        Assert.IsTrue(Admin.IsConnected);
        Assert.That(await Admin.GetDatabase().PingAsync(), Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public async Task Test_DisposeAsync_ClosesTheConnectionItOpened()
    {
        var name = $"nonce-cache-{Guid.NewGuid():N}";
        var cache = CreateCache(options => options.Configuration = $"{Configuration},name={name}");
        Assert.IsTrue(await cache.TryAddAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, MaxAge));
        Assume.That(await ClientsNamedAsync(name), Is.Not.Empty);

        await cache.DisposeAsync();

        Assert.IsTrue(await EventuallyAsync(() => ClientsNamedAsync(name).Result.Length == 0));
    }

    [Test]
    public async Task Test_TryAddAsync_AfterDispose_Throws()
    {
        var cache = CreateCache();
        await cache.DisposeAsync();

        Assert.That(async () => await cache.TryAddAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, MaxAge),
            Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public async Task Test_ALostConnection_IsLoggedWhenItDropsAndWhenItReturns()
    {
        var name = $"nonce-cache-{Guid.NewGuid():N}";
        var logger = new RecordingLogger<RedisNonceCache>();
        await using var cache = CreateCache(options => options.Configuration = $"{Configuration},name={name}", logger);
        Assert.IsTrue(await cache.TryAddAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, MaxAge));

        foreach (var client in await ClientsNamedAsync(name))
        {
            await Admin.GetServers().Single().ClientKillAsync(id: client.Id);
        }

        Assert.IsTrue(await EventuallyAsync(() => logger.WithEventId(1402).Any()), "the connection is restored");
        Assert.That(logger.WithEventId(1401).First().Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(logger.WithEventId(1402).First().Level, Is.EqualTo(LogLevel.Information));
        Assert.IsTrue(await cache.TryAddAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, MaxAge));
    }
}
