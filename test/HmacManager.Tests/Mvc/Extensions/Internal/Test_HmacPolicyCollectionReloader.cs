using System.Text;
using HmacManager.Mvc.Extensions;
using HmacManager.Policies;
using HmacManager.Policies.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Unit.Tests.Mvc.Extensions.Internal;

[TestFixture]
public class Test_HmacPolicyCollectionReloader
{
    private const string GuidA = "00000000-0000-0000-0000-000000000001";
    private const string GuidB = "00000000-0000-0000-0000-000000000002";

    private string Path = null!;

    [SetUp]
    public void SetUp() => Path = System.IO.Path.GetTempFileName();

    [TearDown]
    public void TearDown() => File.Delete(Path);

    /// <summary>
    /// Registers HmacManager over a file-backed configuration section and returns the
    /// live policy collection together with the configuration root used to trigger reloads.
    /// This exercises the same public seam the Kubernetes host uses, so the tests are
    /// agnostic to how the reloader is wired internally.
    /// </summary>
    private (IHmacPolicyCollection Policies, IConfigurationRoot Root) Build(string json)
    {
        File.WriteAllText(Path, json);

        var root = new ConfigurationBuilder()
            .AddJsonFile(Path, optional: false, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();
        services.AddHmacManager(root.GetSection("HmacManager"));

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IHmacPolicyCollection>(), root);
    }

    [Test]
    public void Reload_AddsNewAndReplacesChangedAndRemovesMissingPolicies()
    {
        var (policies, root) = Build(BuildJson((("PolicyA", GuidA, "key-a-v1"))));

        Assert.IsTrue(policies.TryGetValue("PolicyA", out var policyA));
        Assert.AreEqual(Encode("key-a-v1"), policyA.Keys.PrivateKey);

        // Rotate PolicyA's key and introduce PolicyB.
        File.WriteAllText(Path, BuildJson(
            ("PolicyA", GuidA, "key-a-v2"),
            ("PolicyB", GuidB, "key-b-v1")));
        root.Reload();

        Assert.IsTrue(policies.TryGetValue("PolicyA", out var rotatedPolicyA));
        Assert.AreEqual(Encode("key-a-v2"), rotatedPolicyA.Keys.PrivateKey);
        Assert.IsTrue(policies.TryGetValue("PolicyB", out var policyB));
        Assert.AreEqual(Encode("key-b-v1"), policyB.Keys.PrivateKey);
        CollectionAssert.AreEquivalent(new[] { "PolicyA", "PolicyB" }, policies.Values.Select(p => p.Name));

        // Remove PolicyA entirely.
        File.WriteAllText(Path, BuildJson(("PolicyB", GuidB, "key-b-v1")));
        root.Reload();

        Assert.IsFalse(policies.TryGetValue("PolicyA", out _));
        CollectionAssert.AreEquivalent(new[] { "PolicyB" }, policies.Values.Select(p => p.Name));
    }

    [Test]
    public void Reload_WithEmptySection_LeavesPreviousPoliciesUnchanged()
    {
        var (policies, root) = Build(BuildJson(("PolicyA", GuidA, "key-a-v1")));

        // An empty document binds zero policies.
        File.WriteAllText(Path, "{}");
        Assert.DoesNotThrow(() => root.Reload());

        Assert.IsTrue(policies.TryGetValue("PolicyA", out var policyA));
        Assert.AreEqual(Encode("key-a-v1"), policyA.Keys.PrivateKey);
    }

    [Test]
    public void Reload_WithTransientInvalidPrivateKey_LeavesPreviousPoliciesUnchanged()
    {
        var (policies, root) = Build(BuildJson(("PolicyA", GuidA, "key-a-v1")));

        // Simulate a partially-synced rotation: config.json already lists PolicyA, but the
        // Secret volume hasn't caught up, so the private key is not (yet) valid base64.
        // Building the policy set throws a validation error that is NOT a ConfigurationErrorsException.
        File.WriteAllText(Path, BuildJsonRaw(("PolicyA", GuidA, "%%not-base64%%")));
        Assert.DoesNotThrow(() => root.Reload());

        Assert.IsTrue(policies.TryGetValue("PolicyA", out var policyA));
        Assert.AreEqual(Encode("key-a-v1"), policyA.Keys.PrivateKey);
    }

    [Test]
    public void Reload_UnderConcurrentReads_NeverThrowsAndKeepsPolicyVisible()
    {
        var (policies, root) = Build(BuildJson(("PolicyA", GuidA, "key-a-v1")));

        Exception? failure = null;
        var stop = false;

        var reader = Task.Run(() =>
        {
            try
            {
                while (!Volatile.Read(ref stop))
                {
                    if (!policies.TryGetValue("PolicyA", out _))
                    {
                        throw new InvalidOperationException("PolicyA was momentarily missing during a reload.");
                    }

                    // Materializes a snapshot of the backing store; races with an in-place mutation.
                    foreach (var _ in policies.Values) { }
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        try
        {
            for (var i = 0; i < 3000 && failure is null; i++)
            {
                // Same policy name, alternating key value, so every reload is a replace.
                var version = i % 2 == 0 ? "key-a-v2" : "key-a-v1";
                File.WriteAllText(Path, BuildJson(("PolicyA", GuidA, version)));
                root.Reload();
            }
        }
        finally
        {
            Volatile.Write(ref stop, true);
            reader.Wait();
        }

        Assert.IsNull(failure, failure?.ToString());
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
