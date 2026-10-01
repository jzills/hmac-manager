using HmacManager.Caching;
using HmacManager.Caching.Extensions;
using HmacManager.Components;
using HmacManager.Policies;

namespace Unit.Tests.Caching.Extensions;

public class Test_NonceCache_Extensions_CustomCache
{
    private class LegacyNonceCache : INonceCache
    {
        private readonly Dictionary<Guid, DateTimeOffset> Nonces = new();

        public Task SetAsync(Guid nonce, DateTimeOffset dateRequested)
        {
            Nonces[nonce] = dateRequested;
            return Task.CompletedTask;
        }

        public Task<bool> ContainsAsync(Guid nonce) => Task.FromResult(Nonces.ContainsKey(nonce));
    }

    private class MaxAgeRecordingNonceCache : INonceCache
    {
        public readonly List<TimeSpan> MaxAges = new();

        public Task SetAsync(Guid nonce, DateTimeOffset dateRequested) => Task.CompletedTask;

        public Task<bool> ContainsAsync(Guid nonce) => Task.FromResult(false);

        public Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge)
        {
            MaxAges.Add(maxAge);
            return Task.FromResult(true);
        }
    }

    private class MaxAgeStorageNonceCache : INonceCache
    {
        public readonly List<TimeSpan> MaxAges = new();
        private readonly HashSet<Guid> Nonces = new();

        public Task SetAsync(Guid nonce, DateTimeOffset dateRequested) =>
            throw new InvalidOperationException("The policy max age must reach the storage overload.");

        public Task SetAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge)
        {
            MaxAges.Add(maxAge);
            Nonces.Add(nonce);
            return Task.CompletedTask;
        }

        public Task<bool> ContainsAsync(Guid nonce) => Task.FromResult(Nonces.Contains(nonce));
    }

    [Test]
    public async Task Test_CustomCache_WithoutTryAddAsync_RejectsReplay()
    {
        INonceCache cache = new LegacyNonceCache();
        var nonce = Guid.NewGuid();
        var dateRequested = DateTimeOffset.UtcNow;

        Assert.IsTrue(await cache.IsValidNonceAsync(nonce, dateRequested, TimeSpan.FromSeconds(30)));
        Assert.IsFalse(await cache.IsValidNonceAsync(nonce, dateRequested, TimeSpan.FromSeconds(30)));
        Assert.IsTrue(await cache.ContainsAsync(nonce));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Test_CustomCache_ReceivesPolicyMaxAge(bool useStorageOverload)
    {
        var atomicCache = new MaxAgeRecordingNonceCache();
        var storageCache = new MaxAgeStorageNonceCache();
        INonceCache cache = useStorageOverload ? storageCache : atomicCache;
        var privateKey = "Hii9mvaSlUm9RRLwsfuUcg==";
        var options = new HmacManagerOptions("Policy")
        {
            MaxAgeInSeconds = 42,
            HeaderBuilder = new HmacHeaderBuilder(),
            HeaderParser = new HmacHeaderParser()
        };

        var hmacManager = new HmacManager.Components.HmacManager(
            options,
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
            new HmacResultFactory(options.Policy, null),
            cache
        );

        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/endpoint");
        await hmacManager.SignAsync(request);
        var result = await hmacManager.VerifyAsync(request);

        Assert.IsTrue(result.IsSuccess);
        var maxAges = useStorageOverload ? storageCache.MaxAges : atomicCache.MaxAges;
        Assert.That(maxAges, Is.EqualTo(new[] { TimeSpan.FromSeconds(42) }));

        if (useStorageOverload)
        {
            Assert.IsFalse((await hmacManager.VerifyAsync(request)).IsSuccess);
            Assert.That(maxAges.Count, Is.EqualTo(1));
        }
    }
}
