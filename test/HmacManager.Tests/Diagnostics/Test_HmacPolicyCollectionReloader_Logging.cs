using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HmacManager.Mvc.Extensions.Internal;
using NUnit.Framework;

namespace Unit.Tests.Diagnostics;

/// <summary>
/// A configuration reload that fails is deliberately swallowed so a half-synced key rotation cannot
/// take a running process down — which makes the log the only evidence it happened. These tests
/// pin that evidence: a reload that succeeds says so, a reload that fails says so and says the
/// previous policies are still serving, and neither message carries a key.
/// </summary>
[TestFixture]
public class Test_HmacPolicyCollectionReloader_Logging
{
    private const string GuidA = "00000000-0000-0000-0000-000000000001";

    private const int PolicyReloadWatchStarted = 1210;
    private const int PolicyReloadSucceeded = 1211;
    private const int PolicyReloadFailed = 1212;

    private string Path = null!;
    private RecordingLogger<HmacPolicyCollectionReloader> Logger = null!;

    [SetUp]
    public void SetUp()
    {
        Path = System.IO.Path.GetTempFileName();
        Logger = new RecordingLogger<HmacPolicyCollectionReloader>();
    }

    [TearDown]
    public void TearDown() => File.Delete(Path);

    [Test]
    public async Task StartAsync_ReportsTheLivePolicySet()
    {
        var (reloader, _) = Build(BuildJson(("PolicyA", GuidA, "key-a-v1")));

        await reloader.StartAsync(CancellationToken.None);

        var entry = Single(PolicyReloadWatchStarted);
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Information));
        Assert.That(entry.Message, Does.Contain("PolicyA"));
    }

    [Test]
    public void Reload_WhenTheNewConfigurationIsValid_ReportsTheNewPolicySet()
    {
        var (_, root) = Build(BuildJson(("PolicyA", GuidA, "key-a-v1")));

        File.WriteAllText(Path, BuildJson(
            ("PolicyA", GuidA, "key-a-v2"),
            ("PolicyB", "00000000-0000-0000-0000-000000000002", "key-b-v1")));
        root.Reload();

        var entry = Single(PolicyReloadSucceeded);
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Information));
        Assert.That(entry.Message, Does.Contain("PolicyA").And.Contain("PolicyB"));
    }

    [Test]
    public void Reload_WhenTheNewConfigurationIsInvalid_ReportsTheFailureAndTheRetainedPolicies()
    {
        var (_, root) = Build(BuildJson(("PolicyA", GuidA, "key-a-v1")));

        // A partially-synced rotation: the policy is listed but its private key is not yet valid.
        File.WriteAllText(Path, BuildJsonRaw(("PolicyA", GuidA, "%%not-base64%%")));
        root.Reload();

        var entry = Single(PolicyReloadFailed);
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(entry.Exception, Is.Not.Null, "The binding failure is the whole diagnosis.");
        Assert.That(Logger.WithEventId(PolicyReloadSucceeded), Is.Empty);
    }

    [Test]
    public void NoReloadMessage_ContainsAPrivateKey()
    {
        var (_, root) = Build(BuildJson(("PolicyA", GuidA, "key-a-v1")));

        File.WriteAllText(Path, BuildJson(("PolicyA", GuidA, "key-a-v2")));
        root.Reload();

        Assert.That(Logger.Entries, Is.Not.Empty);
        Assert.That(
            Logger.Entries.Where(entry => entry.Message.Contains(Encode("key-a-v2"))),
            Is.Empty,
            "Policy names are logged; keys are not.");
    }

    /// <summary>
    /// Returns one entry for <paramref name="eventId"/>, asserting they are all identical first.
    /// A single <see cref="IConfigurationRoot.Reload"/> raises its change token more than once —
    /// once per provider load and once for the root — so the reloader legitimately runs, and logs,
    /// more than once per edit. What matters is that every occurrence says the same thing.
    /// </summary>
    private RecordedLog Single(int eventId)
    {
        var entries = Logger.WithEventId(eventId).ToArray();

        Assert.That(entries, Is.Not.Empty, $"Expected at least one log entry with event id {eventId}.");
        Assert.That(entries.Select(entry => entry.Message).Distinct().Count(), Is.EqualTo(1));

        return entries[0];
    }

    /// <summary>
    /// Wires the reloader directly rather than through <c>AddHmacManager</c>, because the logger is
    /// attached by the container at host start and these tests need it from the first reload.
    /// </summary>
    private (HmacPolicyCollectionReloader Reloader, IConfigurationRoot Root) Build(string json)
    {
        File.WriteAllText(Path, json);

        var root = new ConfigurationBuilder()
            .AddJsonFile(Path, optional: false, reloadOnChange: false)
            .Build();

        var section = root.GetSection("HmacManager");
        var policies = new ReloadableHmacPolicyCollection(section.GetPolicySection());

        return (new HmacPolicyCollectionReloader(section, policies).UseLogger(Logger), root);
    }

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string BuildJson(params (string Name, string PublicKey, string PrivateKey)[] policies) =>
        BuildJsonRaw(policies.Select(p => (p.Name, p.PublicKey, Encode(p.PrivateKey))).ToArray());

    private static string BuildJsonRaw(params (string Name, string PublicKey, string PrivateKey)[] policies)
    {
        var entries = policies.Select(policy => $$"""
            {
                "Name": "{{policy.Name}}",
                "Keys": {
                    "PublicKey": "{{policy.PublicKey}}",
                    "PrivateKey": "{{policy.PrivateKey}}"
                }
            }
            """);

        return $$"""{ "HmacManager": [{{string.Join(",", entries)}}] }""";
    }
}
