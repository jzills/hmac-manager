using System.Net.Http.Headers;
using HmacManager.Caching;
using HmacManager.Caching.Memory;
using HmacManager.Components;
using HmacManager.Exceptions;
using HmacManager.Mvc;
using HmacManager.Mvc.Extensions;
using HmacManager.Policies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Metrics = HmacManager.Diagnostics.HmacMetrics;

namespace Unit.Tests.Diagnostics;

/// <summary>
/// Asserts the metrics contract: every way a request is refused is counted under its own outcome,
/// once, and no tag ever carries a value the caller chose.
/// </summary>
[TestFixture]
public class Test_HmacMetrics : TestBase
{
    private const string Verifications = "hmac_manager.verifications";
    private const string VerificationDuration = "hmac_manager.verification.duration";
    private const string Signings = "hmac_manager.signings";

    private MetricsRecorder Recorder = null!;

    // Unique per test: the meter is process-wide, and this keeps one test's measurements its own.
    private string Policy = null!;

    [SetUp]
    public void SetUp()
    {
        Recorder = new MetricsRecorder();
        Policy = $"Policy-{Guid.NewGuid():N}";
    }

    [TearDown]
    public void TearDown() => Recorder.Dispose();

    private IEnumerable<RecordedMeasurement> Ours(string instrument) =>
        Recorder.For(instrument).Where(m => Equals(m.Tag(Metrics.Tags.Policy), Policy));

    [Test]
    public async Task SignAsync_CountsTheSigning()
    {
        await CreateHmacManager().SignAsync(CreateRequest());

        var measurement = Ours(Signings).Single();
        Assert.That(measurement.Value, Is.EqualTo(1));
        Assert.That(measurement.Tag(Metrics.Tags.Outcome), Is.EqualTo(Metrics.SigningOutcome.Signed));
    }

    [Test]
    public async Task VerifyAsync_Verified_CountsOnceAndRecordsADuration()
    {
        var hmacManager = CreateHmacManager();
        var request = CreateRequest();
        await hmacManager.SignAsync(request);

        await hmacManager.VerifyAsync(request);

        var count = Ours(Verifications).Single();
        Assert.That(count.Tag(Metrics.Tags.Outcome), Is.EqualTo(Metrics.VerificationOutcome.Verified));

        var duration = Ours(VerificationDuration).Single();
        Assert.That(duration.Tag(Metrics.Tags.Outcome), Is.EqualTo(Metrics.VerificationOutcome.Verified));
        Assert.That(duration.Value, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task VerifyAsync_Replayed_IsCountedAsAReplay()
    {
        var hmacManager = CreateHmacManager();
        var request = CreateRequest();
        await hmacManager.SignAsync(request);

        await hmacManager.VerifyAsync(request);
        await hmacManager.VerifyAsync(request);

        Assert.That(
            Ours(Verifications).Select(m => m.Tag(Metrics.Tags.Outcome)),
            Is.EqualTo(new[] { Metrics.VerificationOutcome.Verified, Metrics.VerificationOutcome.Replayed }));
    }

    [Test]
    public async Task VerifyAsync_Tampered_IsCountedAsASignatureMismatch()
    {
        var hmacManager = CreateHmacManager();
        var request = CreateRequest();
        await hmacManager.SignAsync(request);

        var authorization = request.Headers.Authorization!;
        request.Headers.Authorization = new AuthenticationHeaderValue(authorization.Scheme, "tampered");
        await hmacManager.VerifyAsync(request);

        Assert.That(
            Ours(Verifications).Single().Tag(Metrics.Tags.Outcome),
            Is.EqualTo(Metrics.VerificationOutcome.SignatureMismatch));
    }

    [Test]
    public async Task VerifyAsync_Expired_IsCountedAsExpired()
    {
        // A zero-second window expires every request the instant it is signed.
        var hmacManager = CreateHmacManager(maxAgeInSeconds: 0);
        var request = CreateRequest();
        await hmacManager.SignAsync(request);

        await hmacManager.VerifyAsync(request);

        Assert.That(
            Ours(Verifications).Single().Tag(Metrics.Tags.Outcome),
            Is.EqualTo(Metrics.VerificationOutcome.Expired));
    }

    [Test]
    public void VerifyAsync_WithoutHmacHeaders_IsCountedAsInvalidHeadersNotAsAnError()
    {
        var hmacManager = CreateHmacManager();

        Assert.ThrowsAsync<MissingHeaderException>(() => hmacManager.VerifyAsync(CreateRequest()));

        Assert.That(
            Ours(Verifications).Single().Tag(Metrics.Tags.Outcome),
            Is.EqualTo(Metrics.VerificationOutcome.InvalidHeaders),
            "Bad input from a caller is not a server fault.");
    }

    [Test]
    public void VerifyAsync_WhenTheNonceCacheThrows_IsCountedAsAnErrorAndStillThrows()
    {
        var hmacManager = CreateHmacManager(cache: new ThrowingNonceCache());
        var request = CreateRequest();
        hmacManager.SignAsync(request).GetAwaiter().GetResult();

        Assert.ThrowsAsync<InvalidOperationException>(() => hmacManager.VerifyAsync(request));

        Assert.That(
            Ours(Verifications).Single().Tag(Metrics.Tags.Outcome),
            Is.EqualTo(Metrics.VerificationOutcome.Error));
    }

    [Test]
    public void ContextProvider_UnknownPolicy_IsCountedWithoutTheCallersPolicyName()
    {
        var (provider, headers) = SignedHeaders();
        var callersName = $"Attacker-{Guid.NewGuid():N}";
        headers[HmacAuthenticationDefaults.Headers.Policy] = callersName;

        Assert.Throws<HmacPolicyNotFoundException>(() => provider.TryGetAuthenticationContext(headers, out _));

        var measurement = Recorder.For(Verifications)
            .Single(m => Equals(m.Tag(Metrics.Tags.Outcome), Metrics.VerificationOutcome.UnknownPolicy));
        Assert.That(measurement.Tags.ContainsKey(Metrics.Tags.Policy), Is.False,
            "An unresolved policy name is the caller's; tagging it lets a caller mint series.");
        Assert.That(
            Recorder.Measurements.SelectMany(m => m.Tags.Values),
            Has.None.EqualTo(callersName));
    }

    [Test]
    public void ContextProvider_UnknownScheme_IsCountedUnderThePolicyButNotTheScheme()
    {
        var (provider, headers) = SignedHeaders();
        var callersScheme = $"Scheme-{Guid.NewGuid():N}";
        headers[HmacAuthenticationDefaults.Headers.Scheme] = callersScheme;

        provider.TryGetAuthenticationContext(headers, out _);

        var measurement = Ours(Verifications).Single();
        Assert.That(measurement.Tag(Metrics.Tags.Outcome), Is.EqualTo(Metrics.VerificationOutcome.UnknownScheme));
        Assert.That(measurement.Tags.ContainsKey(Metrics.Tags.Scheme), Is.False);
        Assert.That(
            Recorder.Measurements.SelectMany(m => m.Tags.Values),
            Has.None.EqualTo(callersScheme));
    }

    [Test]
    public void ContextProvider_UnparseableHeaders_IsCountedAsInvalidHeaders()
    {
        var (provider, headers) = SignedHeaders();
        headers.Remove(HmacAuthenticationDefaults.Headers.Nonce);

        Assert.That(
            () => provider.TryGetAuthenticationContext(headers, out _),
            Throws.InstanceOf<MissingHeaderException>().Or.InstanceOf<BadHeaderFormatException>());

        var measurement = Recorder.For(Verifications)
            .Single(m => Equals(m.Tag(Metrics.Tags.Outcome), Metrics.VerificationOutcome.InvalidHeaders));
        Assert.That(measurement.Tags.Keys, Is.EqualTo(new[] { Metrics.Tags.Outcome }));
    }

    [Test]
    public void ContextProvider_KnownPolicy_CountsNothingItself()
    {
        // The manager it hands back counts the verification; the provider counting too would
        // double every request on a dashboard.
        var (provider, headers) = SignedHeaders();

        provider.TryGetAuthenticationContext(headers, out _);

        Assert.That(Recorder.For(Verifications), Is.Empty);
    }

    private (IHmacAuthenticationContextProvider Provider, Dictionary<string, string> Headers) SignedHeaders()
    {
        var services = new ServiceCollection()
            .AddOptions()
            .AddLogging()
            .AddHmacManager(options => options.AddPolicy(Policy, policy =>
            {
                policy.UsePublicKey(PublicKey);
                policy.UsePrivateKey(PrivateKey);
                policy.UseMemoryCache(60);
            }))
            .BuildServiceProvider();

        var request = CreateRequest();
        services.GetRequiredService<IHmacManagerFactory>().Create(Policy)!.SignAsync(request).GetAwaiter().GetResult();

        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value));
        var provider = services.CreateScope().ServiceProvider.GetRequiredService<IHmacAuthenticationContextProvider>();

        // Only what the provider does from here on.
        Recorder.Dispose();
        Recorder = new MetricsRecorder();

        return (provider, headers);
    }

    private static HttpRequestMessage CreateRequest() =>
        new(HttpMethod.Get, "https://localhost:5000/api/resource");

    private HmacManager.Components.HmacManager CreateHmacManager(int maxAgeInSeconds = 60, INonceCache? cache = null)
    {
        var privateKey = PrivateKey;
        var signatureProviderOptions = new HmacSignatureProviderOptions
        {
            Algorithms = new Algorithms
            {
                ContentHashAlgorithm = ContentHashAlgorithm.SHA256,
                SigningHashAlgorithm = SigningHashAlgorithm.HMACSHA256
            },
            Keys = new KeyCredentials { PublicKey = PublicKey, PrivateKey = privateKey },
            ContentHashGenerator = new ContentHashGenerator(ContentHashAlgorithm.SHA256),
            SignatureHashGenerator = new SignatureHashGenerator(privateKey, SigningHashAlgorithm.HMACSHA256)
        };

        var options = new HmacManager.Components.HmacManagerOptions(Policy)
        {
            MaxAgeInSeconds = maxAgeInSeconds,
            HeaderBuilder = new HmacHeaderBuilder(),
            HeaderParser = new HmacHeaderParser()
        };

        return new HmacManager.Components.HmacManager(
            options,
            new HmacFactory(new HmacSignatureProvider(signatureProviderOptions)),
            new HmacResultFactory(options.Policy),
            cache ?? new NonceMemoryCache(
                new MemoryCache(Options.Create(new MemoryCacheOptions())),
                new NonceCacheOptions()),
            NullLogger<HmacManager.Components.HmacManager>.Instance);
    }

    private sealed class ThrowingNonceCache : INonceCache
    {
        public Task<bool> TryAddAsync(Guid nonce, DateTimeOffset dateRequested, TimeSpan maxAge) =>
            throw new InvalidOperationException("The nonce store is unreachable.");
    }
}
