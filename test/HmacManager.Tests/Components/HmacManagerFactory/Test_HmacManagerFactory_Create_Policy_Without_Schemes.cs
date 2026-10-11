using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HmacManager.Caching;
using HmacManager.Caching.Memory;
using HmacManager.Components;
using HmacManager.Policies;

namespace Unit.Tests.Components;

/// <summary>
/// Schemes are optional. A policy that declares none must keep working through every overload —
/// the scheme guard added to Create(policy, scheme) must reject only a scheme that was *asked for*
/// and not found, never the absence of one.
///
/// The companion fixture, Test_HmacManagerFactory_Create_Scheme_Resolution, covers the same
/// overloads against a policy that does declare a scheme.
/// </summary>
[TestFixture]
public class Test_HmacManagerFactory_Create_Policy_Without_Schemes
{
    const string Policy = "PolicyWithoutSchemes";

    IHmacManagerFactory HmacManagerFactory = default!;

    [SetUp]
    public void Init()
    {
        // Deliberately no policy.Schemes.Add(...) anywhere in this fixture.
        var policy = new HmacPolicy(Policy)
        {
            Algorithms = new(),
            Keys = new KeyCredentials { PublicKey = Guid.NewGuid(), PrivateKey = "xCy0Ucg3YEKlmiK23Zph+g==" },
            Nonce = new Nonce { CacheType = NonceCacheType.Memory }
        };

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
    public void Test_Create_Policy_Only_Returns_NotNull()
    {
        Assert.That(HmacManagerFactory.Create(Policy), Is.Not.Null);
    }

    /// <summary>
    /// The two-argument overload with nothing in the scheme position. This is the case the guard
    /// could most easily have broken: the policy has an empty scheme collection, so any lookup
    /// against it misses.
    /// </summary>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Test_Create_Without_Scheme_Returns_NotNull(string? scheme)
    {
        Assert.That(HmacManagerFactory.Create(Policy, scheme), Is.Not.Null);
    }

    /// <summary>
    /// Asking a schemeless policy for a named scheme is still a mistake, and still refused — the
    /// policy cannot satisfy it.
    /// </summary>
    [Test]
    public void Test_Create_With_Named_Scheme_Returns_Null()
    {
        Assert.That(HmacManagerFactory.Create(Policy, "AnyScheme"), Is.Null);
    }

    /// <summary>
    /// End to end: a schemeless policy signs, and the result names no scheme.
    /// </summary>
    [Test]
    public async Task Test_Create_Without_Scheme_Signs_Successfully()
    {
        var hmacManager = HmacManagerFactory.Create(Policy);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/orders");
        var result = await hmacManager!.SignAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Hmac!.Scheme, Is.Null);
            Assert.That(request.Headers.Contains("Hmac-Scheme"), Is.False);
        });
    }
}
