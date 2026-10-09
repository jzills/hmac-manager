using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HmacManager.Caching;
using HmacManager.Caching.Memory;
using HmacManager.Components;
using HmacManager.Headers;
using HmacManager.Policies;
using HmacManager.Schemes;

namespace Unit.Tests.Components;

/// <summary>
/// Create(policy, scheme) used to check only the policy. An unresolved scheme name became a null
/// Scheme on the options, which HmacManager treats as "no scheme" — so the factory handed back a
/// working manager that signed without the scheme and the verifier rejected it as a signature
/// mismatch.
/// </summary>
[TestFixture]
public class Test_HmacManagerFactory_Create_Scheme_Resolution
{
    const string Policy = "SomePolicy";
    const string Scheme = "SomeScheme";

    IHmacManagerFactory HmacManagerFactory = default!;

    [SetUp]
    public void Init()
    {
        var scheme = new Scheme(Scheme);
        scheme.Headers.Add(new Header("X-UserId", "userId"));

        var policy = new HmacPolicy(Policy)
        {
            Algorithms = new(),
            Keys = new KeyCredentials { PublicKey = Guid.NewGuid(), PrivateKey = "xCy0Ucg3YEKlmiK23Zph+g==" },
            Nonce = new Nonce { CacheType = NonceCacheType.Memory }
        };
        policy.Schemes.Add(scheme);

        var policies = new HmacPolicyCollection();
        policies.Add(policy);

        var caches = new NonceCacheCollection();
        caches.Add(NonceCacheType.Memory,
            new NonceMemoryCache(
                new MemoryCache(Options.Create(new MemoryCacheOptions())),
                new NonceCacheOptions())
        );

        HmacManagerFactory = new HmacManagerFactory(
            policies, caches, new HmacHeaderParserFactory(false), new HmacHeaderBuilderFactory(false));
    }

    [Test]
    public void Test_Create_With_Declared_Scheme_Returns_NotNull()
    {
        Assert.That(HmacManagerFactory.Create(Policy, Scheme), Is.Not.Null);
    }

    [Test]
    public void Test_Create_With_Undeclared_Scheme_Returns_Null()
    {
        Assert.That(HmacManagerFactory.Create(Policy, "NotAScheme"), Is.Null);
    }

    [Test]
    public void Test_Create_With_Unknown_Policy_Returns_Null()
    {
        Assert.That(HmacManagerFactory.Create("NotAPolicy", Scheme), Is.Null);
    }

    /// <summary>
    /// Passing no scheme is not a failure — it signs without one, which is what a policy with no
    /// schemes wants. The guard must not catch this case.
    /// </summary>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Test_Create_Without_Scheme_Returns_NotNull(string? scheme)
    {
        Assert.That(HmacManagerFactory.Create(Policy, scheme), Is.Not.Null);
    }

    /// <summary>
    /// The positive case, end to end: a resolved scheme is actually applied, so the signed request
    /// names it. Without this, the guard above could be satisfied by a factory that resolved the
    /// scheme and then dropped it anyway.
    /// </summary>
    [Test]
    public async Task Test_Create_With_Declared_Scheme_Signs_With_It()
    {
        var hmacManager = HmacManagerFactory.Create(Policy, Scheme);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/orders");
        request.Headers.Add("X-UserId", "42");

        var result = await hmacManager!.SignAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Hmac!.Scheme, Is.EqualTo(Scheme));
            Assert.That(result.Hmac!.SigningContent, Does.Contain("42"));
        });
    }
}
