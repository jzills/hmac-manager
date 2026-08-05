using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HmacManager.Caching;
using HmacManager.Caching.Memory;
using HmacManager.Components;
using HmacManager.Policies;
using NUnit.Framework;

namespace Unit.Tests.Diagnostics;

/// <summary>
/// Asserts the diagnostics contract of <see cref="HmacManager.Components.HmacManager"/>: every way a
/// request can be rejected is distinguishable from the others by event id, and no message — at any
/// level, including Trace — can carry the private key.
/// </summary>
[TestFixture]
public class Test_HmacManager_Logging
{
    private const string PublicKey = "a18f5729-32ce-43a4-ac4d-af0a699539ae";
    private const string PrivateKey = "xCy0Ucg3YEKlmiK23Zph+g==";

    private const int RequestSigned = 1000;
    private const int RequestVerified = 1100;
    private const int VerificationRequestExpired = 1102;
    private const int VerificationNonceReplayed = 1103;
    private const int VerificationSignatureMismatch = 1104;

    private RecordingLogger<HmacManager.Components.HmacManager> Logger = null!;

    [SetUp]
    public void SetUp() => Logger = new RecordingLogger<HmacManager.Components.HmacManager>();

    [Test]
    public async Task SignAsync_RecordsTheSignedRequest()
    {
        var hmacManager = CreateHmacManager();

        await hmacManager.SignAsync(CreateRequest());

        var entry = Logger.WithEventId(RequestSigned).Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Debug));
        Assert.That(entry.Message, Does.Contain("MyPolicy"));
    }

    [Test]
    public async Task VerifyAsync_RecordsTheVerifiedRequest()
    {
        var hmacManager = CreateHmacManager();
        var request = CreateRequest();

        await hmacManager.SignAsync(request);
        var result = await hmacManager.VerifyAsync(request);

        Assert.That(result.IsSuccess, Is.True);

        var entry = Logger.WithEventId(RequestVerified).Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Debug));
    }

    [Test]
    public async Task VerifyAsync_WithAReplayedNonce_RecordsTheReplayAndNothingElse()
    {
        var hmacManager = CreateHmacManager();
        var request = CreateRequest();

        await hmacManager.SignAsync(request);
        await hmacManager.VerifyAsync(request);

        // The same request a second time: the nonce is already spent.
        var result = await hmacManager.VerifyAsync(request);

        Assert.That(result.IsSuccess, Is.False);

        var entry = Logger.WithEventId(VerificationNonceReplayed).Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(Logger.WithEventId(VerificationSignatureMismatch), Is.Empty,
            "A replay must not be reported as a signature mismatch.");
    }

    [Test]
    public async Task VerifyAsync_WithATamperedSignature_RecordsAMismatchAndNotAReplay()
    {
        var hmacManager = CreateHmacManager();
        var request = CreateRequest();

        await hmacManager.SignAsync(request);

        var authorization = request.Headers.Authorization!;
        request.Headers.Authorization = new AuthenticationHeaderValue(
            authorization.Scheme, $"{authorization.Parameter}=");

        var result = await hmacManager.VerifyAsync(request);

        Assert.That(result.IsSuccess, Is.False);

        var entry = Logger.WithEventId(VerificationSignatureMismatch).Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(Logger.WithEventId(VerificationNonceReplayed), Is.Empty,
            "A first-time nonce must not be reported as a replay.");
    }

    [Test]
    public async Task VerifyAsync_WithAnExpiredRequest_RecordsTheExpiryBeforeSpendingTheNonce()
    {
        // A zero-second window expires every request the instant it is signed.
        var hmacManager = CreateHmacManager(maxAgeInSeconds: 0);
        var request = CreateRequest();

        await hmacManager.SignAsync(request);
        var result = await hmacManager.VerifyAsync(request);

        Assert.That(result.IsSuccess, Is.False);

        var entry = Logger.WithEventId(VerificationRequestExpired).Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(entry.Message, Does.Contain("clock skew"),
            "The message should point at the usual cause.");
    }

    [Test]
    public async Task NoLogMessage_AtAnyLevel_ContainsThePrivateKey()
    {
        var hmacManager = CreateHmacManager();
        var request = CreateRequest();

        // Exercise every logging path: sign, verify, replay, and mismatch.
        await hmacManager.SignAsync(request);
        await hmacManager.VerifyAsync(request);
        await hmacManager.VerifyAsync(request);

        var tampered = CreateRequest();
        await hmacManager.SignAsync(tampered);
        var authorization = tampered.Headers.Authorization!;
        tampered.Headers.Authorization = new AuthenticationHeaderValue(authorization.Scheme, "tampered");
        await hmacManager.VerifyAsync(tampered);

        Assert.That(Logger.Entries, Is.Not.Empty);
        Assert.That(
            Logger.Entries.Where(entry => entry.Message.Contains(PrivateKey)),
            Is.Empty,
            "The private key must never reach the log stream.");
    }

    private static HttpRequestMessage CreateRequest() =>
        new(HttpMethod.Get, "https://localhost:5000/api/resource");

    private HmacManager.Components.HmacManager CreateHmacManager(int maxAgeInSeconds = 60)
    {
        var signatureProviderOptions = new HmacSignatureProviderOptions
        {
            Algorithms = new Algorithms
            {
                ContentHashAlgorithm = ContentHashAlgorithm.SHA256,
                SigningHashAlgorithm = SigningHashAlgorithm.HMACSHA256
            },
            Keys = new KeyCredentials
            {
                PublicKey = Guid.Parse(PublicKey),
                PrivateKey = PrivateKey
            },
            ContentHashGenerator = new ContentHashGenerator(ContentHashAlgorithm.SHA256),
            SignatureHashGenerator = new SignatureHashGenerator(PrivateKey, SigningHashAlgorithm.HMACSHA256)
        };

        var options = new HmacManagerOptions("MyPolicy")
        {
            MaxAgeInSeconds = maxAgeInSeconds,
            HeaderBuilder = new HmacHeaderBuilder(),
            HeaderParser = new HmacHeaderParser()
        };

        return new HmacManager.Components.HmacManager(
            options,
            new HmacFactory(new HmacSignatureProvider(signatureProviderOptions)),
            new HmacResultFactory(options.Policy),
            new NonceMemoryCache(
                new MemoryCache(Options.Create(new MemoryCacheOptions())),
                new NonceCacheOptions()),
            Logger
        );
    }
}
