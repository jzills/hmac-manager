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
/// A request can expire while its signature is computed. The cache refuses to claim a nonce whose
/// window has closed, and the manager reports that as an expiry rather than a replay.
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
        private readonly TimeSpan Elapsed;
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

    [TestCase(4999, true)]
    [TestCase(5000, false)]
    [TestCase(5001, false)]
    public async Task Test_VerifyAsync_WindowClosingDuringVerification_IsAnExpiry(int elapsedMilliseconds, bool expectedSuccess)
    {
        var maxAge = TimeSpan.FromSeconds(5);
        var privateKey = "Hii9mvaSlUm9RRLwsfuUcg==";
        var options = new HmacManagerOptions("Policy")
        {
            MaxAgeInSeconds = (int)maxAge.TotalSeconds,
            HeaderBuilder = new HmacHeaderBuilder(),
            HeaderParser = new HmacHeaderParser()
        };

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

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
            TimeSpan.FromMilliseconds(elapsedMilliseconds)
        );

        var cache = new NonceMemoryCache(
            new MemoryCache(Options.Create(new MemoryCacheOptions())), new NonceCacheOptions(), clock);
        var logger = new RecordingLogger<HmacManager.Components.HmacManager>();
        var hmacManager = new HmacManager.Components.HmacManager(
            options, factory, new HmacResultFactory(options.Policy, null), cache, logger, clock);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/endpoint");
        Assert.IsTrue((await hmacManager.SignAsync(request)).IsSuccess);

        var result = await hmacManager.VerifyAsync(request);

        Assert.That(factory.VerificationCalls, Is.EqualTo(1));
        Assert.That(result.IsSuccess, Is.EqualTo(expectedSuccess));
        Assert.That(logger.WithEventId(RequestVerified).Count(), Is.EqualTo(expectedSuccess ? 1 : 0));
        Assert.That(logger.WithEventId(VerificationRequestExpired).Count(), Is.EqualTo(expectedSuccess ? 0 : 1));
        Assert.That(logger.WithEventId(VerificationNonceReplayed), Is.Empty);
    }
}
