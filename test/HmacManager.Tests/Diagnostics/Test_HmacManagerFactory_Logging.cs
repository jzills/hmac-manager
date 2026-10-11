using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HmacManager.Caching;
using HmacManager.Caching.Memory;
using HmacManager.Components;
using HmacManager.Headers;
using HmacManager.Policies;
using HmacManager.Schemes;
using NUnit.Framework;

namespace Unit.Tests.Diagnostics;

/// <summary>
/// Pins the log level of a caller-attributable policy-resolution failure. An unknown policy name is
/// supplied by the caller — in the ext-authz service it comes straight off a request header — so it
/// is routine, unbounded input, not a server-side fault. It must therefore be diagnostic (Debug),
/// never Warning, so an edge-facing deployment cannot be driven to emit unbounded Warning volume by
/// requests naming policies that do not exist.
/// </summary>
[TestFixture]
public class Test_HmacManagerFactory_Logging
{
    private const int PolicyNotFound = 1200;
    private const int SchemeNotFound = 1202;

    [Test]
    public void Create_WithAnUnregisteredPolicy_LogsPolicyNotFoundAtDebug()
    {
        var logger = new RecordingLogger<HmacManagerFactory>();
        var factory = new HmacManagerFactory(
            new HmacPolicyCollection(),
            new NonceCacheCollection(),
            new HmacHeaderParserFactory(false),
            new HmacHeaderBuilderFactory(false),
            new RecordingLoggerFactory(logger));

        var manager = factory.Create("does-not-exist");

        Assert.That(manager, Is.Null);

        var entry = logger.WithEventId(PolicyNotFound).Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Debug),
            "An unknown policy name is caller-controlled input and must not reach the Warning stream.");
    }

    /// <summary>
    /// A scheme name is caller-supplied in exactly the same way a policy name is, so it is pinned to
    /// the same level for the same reason.
    /// </summary>
    [Test]
    public void Create_WithAnUndeclaredScheme_LogsSchemeNotFoundAtDebug()
    {
        var logger = new RecordingLogger<HmacManagerFactory>();

        var policy = new HmacPolicy("SomePolicy")
        {
            Algorithms = new(),
            Keys = new KeyCredentials { PublicKey = Guid.NewGuid(), PrivateKey = "xCy0Ucg3YEKlmiK23Zph+g==" },
            Nonce = new Nonce { CacheType = NonceCacheType.Memory }
        };

        var scheme = new Scheme("SomeScheme");
        scheme.Headers.Add(new Header("X-UserId", "userId"));
        policy.Schemes.Add(scheme);

        var policies = new HmacPolicyCollection();
        policies.Add(policy);

        var caches = new NonceCacheCollection();
        caches.Add(NonceCacheType.Memory,
            new NonceMemoryCache(
                new MemoryCache(Options.Create(new MemoryCacheOptions())),
                new NonceCacheOptions()));

        var factory = new HmacManagerFactory(
            policies,
            caches,
            new HmacHeaderParserFactory(false),
            new HmacHeaderBuilderFactory(false),
            new RecordingLoggerFactory(logger));

        var manager = factory.Create("SomePolicy", "does-not-exist");

        Assert.That(manager, Is.Null);

        var entry = logger.WithEventId(SchemeNotFound).Single();
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Debug),
            "An unknown scheme name is caller-controlled input and must not reach the Warning stream.");
    }
}
