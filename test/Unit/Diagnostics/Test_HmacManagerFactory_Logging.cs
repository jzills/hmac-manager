using Microsoft.Extensions.Logging;
using HmacManager.Caching;
using HmacManager.Components;
using HmacManager.Policies;
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
}
