using HmacManager.Caching;
using HmacManager.Caching.Memory;
using HmacManager.Components;
using HmacManager.Policies;
using HmacManager.Schemes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Unit.Tests.Components;

/// <summary>
/// A request can expire while its signature is computed. The manager checks again before the cache,
/// so an expired request never reaches it, and whatever the cache refuses after that is a replay.
/// </summary>
public class Test_HmacManager_VerifyAsync_NonceExpiry
{
    private const int RequestVerified = 1100;
    private const int VerificationRequestExpired = 1102;
    private const int VerificationNonceReplayed = 1103;

    private class ClockAdvancingHmacFactory : IHmacFactory
    {
        private readonly IHmacFactory Inner;
        private readonly DateTimeOffset DateRequested;
        private readonly FakeTimeProvider Clock;
        public TimeSpan Elapsed;
        public int VerificationCalls;

        public ClockAdvancingHmacFactory(
            IHmacFactory inner, FakeTimeProvider clock, TimeSpan elapsed)
        {
            Inner = inner;
            DateRequested = clock.GetUtcNow();
            Clock = clock;
            Elapsed = elapsed;
        }

        public Task<Hmac> CreateAsync(HttpRequestMessage request, string policy, Scheme? scheme = null) =>
            Inner.CreateAsync(request, new HmacPartial { Policy = policy, DateRequested = DateRequested });

        public async Task<Hmac> CreateAsync(HttpRequestMessage request, HmacPartial? hmac)
        {
            VerificationCalls++;
            var result = await Inner.CreateAsync(request, hmac);
            Clock.Advance(Elapsed);
            return result;
        }
    }

    /// <summary>
    /// Implements <see cref="INonceCache.TryAddAsync"/> directly, as any cache could from v2.11.0, and
    /// writes an absolute expiry the way <c>RedisCache</c> validates one: it throws when it is not in
    /// the future.
    /// </summary>
    private class AbsoluteExpiryNonceCache(TimeProvider clock) : INonceCache
    {
        public int Calls;

        public Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge)
        {
            Calls++;
            if (dateRequested + maxAge <= clock.GetUtcNow())
            {
                throw new ArgumentOutOfRangeException("AbsoluteExpiration", "The absolute expiration value must be in the future.");
            }

            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// A store that answers after a delay, by which time the request's window may have closed.
    /// </summary>
    private class SlowNonceCache(INonceCache inner, FakeTimeProvider clock) : INonceCache
    {
        public TimeSpan Latency;

        public async Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge)
        {
            var claimed = await inner.TryAddAsync(nonce, dateRequested, maxAge);
            clock.Advance(Latency);
            return claimed;
        }
    }

    private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(5);

    private static (HmacManager.Components.HmacManager Manager, ClockAdvancingHmacFactory Factory, RecordingLogger<HmacManager.Components.HmacManager> Logger)
        Create(FakeTimeProvider clock, INonceCache cache, TimeSpan elapsed)
    {
        var privateKey = "Hii9mvaSlUm9RRLwsfuUcg==";
        var options = new HmacManagerOptions("Policy")
        {
            MaxAgeInSeconds = (int)MaxAge.TotalSeconds,
            HeaderBuilder = new HmacHeaderBuilder(),
            HeaderParser = new HmacHeaderParser()
        };

        var factory = new ClockAdvancingHmacFactory(
            new HmacFactory(new HmacSignatureProvider(new HmacSignatureProviderOptions
            {
                Algorithms = new Algorithms
                {
                    ContentHashAlgorithm = ContentHashAlgorithm.SHA256,
                    SigningHashAlgorithm = SigningHashAlgorithm.HMACSHA256
                },
                Keys = new KeyCredentials
                {
                    PublicKey = Guid.NewGuid(),
                    PrivateKey = privateKey
                },
                ContentHashGenerator = new ContentHashGenerator(ContentHashAlgorithm.SHA256),
                SignatureHashGenerator = new SignatureHashGenerator(privateKey, SigningHashAlgorithm.HMACSHA256)
            })),
            clock,
            elapsed
        );

        var logger = new RecordingLogger<HmacManager.Components.HmacManager>();
        var hmacManager = new HmacManager.Components.HmacManager(
            options, factory, new HmacResultFactory(options.Policy, null), cache, logger, clock);

        return (hmacManager, factory, logger);
    }

    private static NonceMemoryCache CreateMemoryCache(TimeProvider clock) =>
        new(new MemoryCache(Options.Create(new MemoryCacheOptions())), new NonceCacheOptions(), clock);

    [TestCase(4999, true)]
    [TestCase(5000, false)]
    [TestCase(5001, false)]
    public async Task Test_VerifyAsync_WindowClosingDuringVerification_IsAnExpiry(int elapsedMilliseconds, bool expectedSuccess)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var (hmacManager, factory, logger) = Create(
            clock, CreateMemoryCache(clock), TimeSpan.FromMilliseconds(elapsedMilliseconds));

        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/endpoint");
        Assert.IsTrue((await hmacManager.SignAsync(request)).IsSuccess);

        var result = await hmacManager.VerifyAsync(request);

        Assert.That(factory.VerificationCalls, Is.EqualTo(1));
        Assert.That(result.IsSuccess, Is.EqualTo(expectedSuccess));
        Assert.That(logger.WithEventId(RequestVerified).Count(), Is.EqualTo(expectedSuccess ? 1 : 0));
        Assert.That(logger.WithEventId(VerificationRequestExpired).Count(), Is.EqualTo(expectedSuccess ? 0 : 1));
        Assert.That(logger.WithEventId(VerificationNonceReplayed), Is.Empty);
    }

    [TestCase(4999, true)]
    [TestCase(5000, false)]
    [TestCase(5001, false)]
    public async Task Test_VerifyAsync_WindowClosingDuringVerification_NeverReachesACacheThatImplementsTryAddAsync(
        int elapsedMilliseconds, bool expectedSuccess)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var cache = new AbsoluteExpiryNonceCache(clock);
        var (hmacManager, _, logger) = Create(clock, cache, TimeSpan.FromMilliseconds(elapsedMilliseconds));

        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/endpoint");
        Assert.IsTrue((await hmacManager.SignAsync(request)).IsSuccess);

        var result = await hmacManager.VerifyAsync(request);

        Assert.That(result.IsSuccess, Is.EqualTo(expectedSuccess));
        Assert.That(cache.Calls, Is.EqualTo(expectedSuccess ? 1 : 0));
        Assert.That(logger.WithEventId(VerificationRequestExpired).Count(), Is.EqualTo(expectedSuccess ? 0 : 1));
    }

    [Test]
    public async Task Test_VerifyAsync_ReplayWhoseCacheRoundTripCrossesTheWindow_IsAReplay()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(start);
        var cache = new SlowNonceCache(CreateMemoryCache(clock), clock);
        var (hmacManager, _, logger) = Create(clock, cache, TimeSpan.Zero);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/endpoint");
        Assert.IsTrue((await hmacManager.SignAsync(request)).IsSuccess);
        Assert.IsTrue((await hmacManager.VerifyAsync(request)).IsSuccess);

        // The replay arrives with 5 ms of its window left, and the store takes 10 ms to refuse it.
        clock.Advance(MaxAge - TimeSpan.FromMilliseconds(5));
        cache.Latency = TimeSpan.FromMilliseconds(10);

        var replay = await hmacManager.VerifyAsync(request);

        Assert.IsFalse(replay.IsSuccess);
        Assert.That(logger.WithEventId(VerificationNonceReplayed).Count(), Is.EqualTo(1));
        Assert.That(logger.WithEventId(VerificationRequestExpired), Is.Empty);
    }
}
