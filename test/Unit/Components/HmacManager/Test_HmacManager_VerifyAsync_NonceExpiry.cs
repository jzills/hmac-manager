using HmacManager.Caching;
using HmacManager.Components;
using HmacManager.Policies;
using HmacManager.Schemes;

namespace Unit.Tests.Components;

public class Test_HmacManager_VerifyAsync_NonceExpiry
{
    private class RecordingNonceCache : INonceCache
    {
        public int Calls;

        public Task SetAsync(Guid nonce, DateTimeOffset dateRequested)
        {
            Interlocked.Increment(ref Calls);
            return Task.CompletedTask;
        }

        public Task<bool> ContainsAsync(Guid nonce)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(false);
        }

        public Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(true);
        }
    }

    private class SlowVerifyingHmacFactory : IHmacFactory
    {
        private readonly IHmacFactory Inner;
        private readonly DateTimeOffset DateRequested;
        private readonly TimeSpan Delay;

        public SlowVerifyingHmacFactory(IHmacFactory inner, DateTimeOffset dateRequested, TimeSpan delay)
        {
            Inner = inner;
            DateRequested = dateRequested;
            Delay = delay;
        }

        public Task<Hmac> CreateAsync(HttpRequestMessage request, string policy, Scheme? scheme = null) =>
            Inner.CreateAsync(request, new HmacPartial { Policy = policy, DateRequested = DateRequested });

        public async Task<Hmac> CreateAsync(HttpRequestMessage request, HmacPartial? hmac)
        {
            await Task.Delay(Delay);
            return await Inner.CreateAsync(request, hmac);
        }
    }

    [Test]
    public async Task Test_VerifyAsync_NonceExpiredBeforeStorage_IsFailure_CacheNotCalled()
    {
        var maxAge = TimeSpan.FromSeconds(5);
        var privateKey = "Hii9mvaSlUm9RRLwsfuUcg==";
        var options = new HmacManagerOptions("Policy")
        {
            MaxAgeInSeconds = (int)maxAge.TotalSeconds,
            HeaderBuilder = new HmacHeaderBuilder(),
            HeaderParser = new HmacHeaderParser()
        };

        var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var dateRequested = now - maxAge + TimeSpan.FromMilliseconds(250);

        var factory = new SlowVerifyingHmacFactory(
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
            dateRequested,
            TimeSpan.FromMilliseconds(500)
        );

        var cache = new RecordingNonceCache();
        var hmacManager = new HmacManager.Components.HmacManager(
            options, factory, new HmacResultFactory(options.Policy, null), cache);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/endpoint");
        Assert.IsTrue((await hmacManager.SignAsync(request)).IsSuccess);

        var result = await hmacManager.VerifyAsync(request);

        Assert.IsFalse(result.IsSuccess);
        Assert.That(cache.Calls, Is.EqualTo(0));
    }
}
