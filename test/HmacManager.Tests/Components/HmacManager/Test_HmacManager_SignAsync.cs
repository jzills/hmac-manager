using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HmacManager.Caching.Memory;
using HmacManager.Components;
using HmacManager.Policies;
using HmacManager.Schemes;

namespace Unit.Tests.Components;

[TestFixture("a18f5729-32ce-43a4-ac4d-af0a699539ae", "xCy0Ucg3YEKlmiK23Zph+g==", ContentHashAlgorithm.SHA1, SigningHashAlgorithm.HMACSHA1)]
[TestFixture("84a778d9-b4cf-4971-ada5-0ce009d1bf86", "Hii9mvaSlUm9RRLwsfuUcg==", ContentHashAlgorithm.SHA256, SigningHashAlgorithm.HMACSHA256)]
[TestFixture("b6ce3749-a4e3-4391-93ca-eaa20dd56ea3", "zdYk8nnPdE2khpt2ROgadw==", ContentHashAlgorithm.SHA512, SigningHashAlgorithm.HMACSHA512)]
public class Test_HmacManager_SignAsync
{
    public readonly HmacManager.Components.HmacManager HmacManager;
    public readonly HmacManagerOptions HmacManagerOptions;
    public readonly HmacSignatureProviderOptions HmacSignatureProviderOptions;

    public Test_HmacManager_SignAsync(
        string publicKey, 
        string privateKey, 
        ContentHashAlgorithm contentHashAlgorithm,
        SigningHashAlgorithm signingHashAlgorithm
    )
    {
        HmacSignatureProviderOptions = new HmacSignatureProviderOptions
        {
            Algorithms = new Algorithms
            {
                ContentHashAlgorithm = contentHashAlgorithm,
                SigningHashAlgorithm = signingHashAlgorithm
            },
            Keys = new KeyCredentials
            {
                PublicKey = Guid.Parse(publicKey),
                PrivateKey = privateKey
            },
            ContentHashGenerator = new ContentHashGenerator(contentHashAlgorithm),
            SignatureHashGenerator = new SignatureHashGenerator(privateKey, signingHashAlgorithm)
        };

        HmacManagerOptions = new HmacManagerOptions("Policy")
        {
            MaxAgeInSeconds = 60,
            Scheme = new Scheme("Scheme"),
            HeaderBuilder = new HmacHeaderBuilder(),
            HeaderParser = new HmacHeaderParser()
        };

        HmacManager = new HmacManager.Components.HmacManager(
            HmacManagerOptions, 
            new HmacFactory(new HmacSignatureProvider(HmacSignatureProviderOptions)), 
            new HmacResultFactory(HmacManagerOptions.Policy, HmacManagerOptions.Scheme.Name),
            new NonceMemoryCache(
                new MemoryCache(Options.Create(new MemoryCacheOptions())), 
                new HmacManager.Caching.NonceCacheOptions()
            )
        );
    }

    [TestCaseSource(typeof(TestCaseSource), nameof(TestCaseSource.GetHttpRequestMessages))]
    public async Task Test_SignAsync_IsSuccess(HttpRequestMessage request)
    {
        var signingResult = await HmacManager.SignAsync(request);
        Assert.IsTrue(signingResult.IsSuccess);
    }

    [TestCaseSource(typeof(TestCaseSource), nameof(TestCaseSource.GetHttpRequestMessages))]
    public async Task Test_SignAsync_Hmac_ShouldMatch_Verification(HttpRequestMessage request)
    {
        var signingResult = await HmacManager.SignAsync(request);
        Assert.IsTrue(signingResult.IsSuccess);

        var verificationResult = await HmacManager.VerifyAsync(request);
        Assert.IsTrue(verificationResult.IsSuccess);

        Assert.That(signingResult.Hmac?.Policy, Is.EqualTo(verificationResult.Hmac?.Policy));
        Assert.That(signingResult.Hmac?.Scheme, Is.EqualTo(verificationResult.Hmac?.Scheme));

        Assert.That(signingResult.Hmac!.DateRequested, Is.EqualTo(verificationResult.Hmac!.DateRequested));
        
        foreach (var (signingHeaderValue, verificationHeaderValue) in 
            signingResult.Hmac.HeaderValues.Zip(verificationResult.Hmac.HeaderValues))
        {
            Assert.That(signingHeaderValue.Name, Is.EqualTo(verificationHeaderValue.Name));
            Assert.That(signingHeaderValue.ClaimType, Is.EqualTo(verificationHeaderValue.ClaimType));
            Assert.That(signingHeaderValue.Value, Is.EqualTo(verificationHeaderValue.Value));
        }

        Assert.That(signingResult.Hmac!.Nonce, Is.EqualTo(verificationResult.Hmac!.Nonce));
        Assert.That(signingResult.Hmac!.Signature, Is.EqualTo(verificationResult.Hmac!.Signature));
        Assert.That(signingResult.Hmac!.SigningContent, Is.EqualTo(verificationResult.Hmac!.SigningContent));
    }   
}