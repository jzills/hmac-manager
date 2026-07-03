using System.Text;
using HmacManager.Mvc.Extensions;
using HmacManager.Operator.Rendering;
using HmacManager.Policies;
using HmacManager.Policies.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace HmacManager.Operator.Tests;

[TestFixture]
public class PolicyConfigRendererTests
{
    private static string Key(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// The strongest guarantee we can make without a cluster: whatever the renderer emits must be
    /// consumable by the exact configuration pipeline the pods run — config.json plus the private
    /// key files (mounted via KeyPerFile, which flattens "__" to ":") bind back into real policies.
    /// </summary>
    [Test]
    public void Rendered_output_binds_back_through_the_library_pipeline()
    {
        var renderer = new PolicyConfigRenderer("Memory");
        var result = renderer.Render(new[]
        {
            new ResolvedPolicy(
                Name: "payments-api",
                PublicKey: "00000000-0000-0000-0000-000000000001",
                PrivateKey: Key("payments-secret"),
                ContentHashAlgorithm: "SHA256",
                SigningHashAlgorithm: "HMACSHA256",
                NonceMaxAgeInSeconds: 60,
                Schemes: new[] { new ResolvedScheme("UserContext", new[] { new ResolvedHeader("X-UserId", "userId") }) }),
        });

        var policies = BindThroughLibrary(result);

        Assert.IsTrue(policies.TryGetValue("payments-api", out var policy));
        Assert.AreEqual(Guid.Parse("00000000-0000-0000-0000-000000000001"), policy.Keys.PublicKey);
        Assert.AreEqual(Key("payments-secret"), policy.Keys.PrivateKey);
        Assert.AreEqual(60, policy.Nonce.MaxAgeInSeconds);

        // The scheme (and its header + claim type) must survive the render → bind round-trip, not
        // just parse without error: a renderer that dropped or mis-nested schemes would still yield
        // a config.json the pipeline accepts, so assert the scheme actually lands on the policy.
        var scheme = policy.Schemes.Get("UserContext");
        Assert.IsNotNull(scheme, "the UserContext scheme should survive rendering and binding");
        var header = scheme!.Headers.Get("X-UserId");
        Assert.IsNotNull(header, "the scheme's X-UserId header should survive rendering and binding");
        Assert.AreEqual("userId", header!.ClaimType);
    }

    [Test]
    public void Key_file_indices_align_with_config_array_order()
    {
        var renderer = new PolicyConfigRenderer("Memory");

        // Deliberately supplied out of order; the renderer must impose a stable order and keep the
        // key-file index aligned with each policy's position in the config array.
        var result = renderer.Render(new[]
        {
            new ResolvedPolicy("orders-api", "00000000-0000-0000-0000-000000000002", Key("orders-secret"),
                "SHA256", "HMACSHA256", 30, Array.Empty<ResolvedScheme>()),
            new ResolvedPolicy("payments-api", "00000000-0000-0000-0000-000000000001", Key("payments-secret"),
                "SHA256", "HMACSHA256", 60, Array.Empty<ResolvedScheme>()),
        });

        var policies = BindThroughLibrary(result);

        // Each policy must end up bound to the private key that belongs to it, regardless of input order.
        Assert.IsTrue(policies.TryGetValue("payments-api", out var payments));
        Assert.AreEqual(Key("payments-secret"), payments.Keys.PrivateKey);
        Assert.IsTrue(policies.TryGetValue("orders-api", out var orders));
        Assert.AreEqual(Key("orders-secret"), orders.Keys.PrivateKey);
    }

    private static IHmacPolicyCollection BindThroughLibrary(RenderResult result)
    {
        // Mount the private keys the way KeyPerFile does: file name "HmacManager__0__Keys__PrivateKey"
        // becomes config key "HmacManager:0:Keys:PrivateKey".
        var keyConfig = result.KeyFiles.ToDictionary(kv => kv.Key.Replace("__", ":"), kv => (string?)kv.Value);

        var config = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(result.ConfigJson)))
            .AddInMemoryCollection(keyConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddHmacManager(config.GetSection("HmacManager"));

        return services.BuildServiceProvider().GetRequiredService<IHmacPolicyCollection>();
    }
}
