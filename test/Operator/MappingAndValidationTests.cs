using System.Text;
using HmacManager.Operator.Entities;
using HmacManager.Operator.Rendering;
using k8s.Models;
using NUnit.Framework;

namespace HmacManager.Operator.Tests;

[TestFixture]
public class MappingAndValidationTests
{
    private static string Key(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static V1HmacPolicy Policy(string name, Action<V1HmacPolicy.PolicySpec>? configure = null)
    {
        var spec = new V1HmacPolicy.PolicySpec
        {
            PublicKey = "00000000-0000-0000-0000-000000000001",
            Algorithms = new V1HmacPolicy.AlgorithmsSpec
            {
                ContentHash = "SHA512",
                SigningHash = "HMACSHA512",
            },
            Nonce = new V1HmacPolicy.NonceSpec { MaxAgeInSeconds = 45 },
            Schemes =
            {
                new V1HmacPolicy.SchemeSpec
                {
                    Name = "UserContext",
                    Headers = { new V1HmacPolicy.HeaderSpec { Name = "X-UserId", ClaimType = "userId" } },
                },
            },
        };
        configure?.Invoke(spec);
        return new V1HmacPolicy { Metadata = new V1ObjectMeta { Name = name }, Spec = spec };
    }

    [Test]
    public void ToResolvedPolicy_maps_spec_fields_including_enum_names_and_schemes()
    {
        var resolved = PolicyMapper.ToResolvedPolicy(Policy("payments-api"), Key("secret"));

        Assert.AreEqual("payments-api", resolved.Name);
        Assert.AreEqual("00000000-0000-0000-0000-000000000001", resolved.PublicKey);
        Assert.AreEqual(Key("secret"), resolved.PrivateKey);
        Assert.AreEqual("SHA512", resolved.ContentHashAlgorithm);
        Assert.AreEqual("HMACSHA512", resolved.SigningHashAlgorithm);
        Assert.AreEqual(45, resolved.NonceMaxAgeInSeconds);
        Assert.AreEqual(1, resolved.Schemes.Count);
        Assert.AreEqual("UserContext", resolved.Schemes[0].Name);
        Assert.AreEqual("X-UserId", resolved.Schemes[0].Headers[0].Name);
        Assert.AreEqual("userId", resolved.Schemes[0].Headers[0].ClaimType);
    }

    [Test]
    public void Validate_accepts_a_well_formed_policy()
    {
        var (isValid, message) = PolicyValidation.Validate("payments-api", "00000000-0000-0000-0000-000000000001", Key("secret"));

        Assert.IsTrue(isValid, message);
        Assert.IsNull(message);
    }

    [Test]
    public void Validate_rejects_a_missing_private_key()
    {
        var (isValid, message) = PolicyValidation.Validate("payments-api", "00000000-0000-0000-0000-000000000001", null);

        Assert.IsFalse(isValid);
        Assert.IsNotNull(message);
    }

    [Test]
    public void Validate_rejects_a_private_key_that_is_not_base64()
    {
        var (isValid, message) = PolicyValidation.Validate("payments-api", "00000000-0000-0000-0000-000000000001", "%%not-base64%%");

        Assert.IsFalse(isValid);
        Assert.IsNotNull(message);
    }

    [Test]
    public void Validate_rejects_a_public_key_that_is_not_a_guid()
    {
        var (isValid, message) = PolicyValidation.Validate("payments-api", "not-a-guid", Key("secret"));

        Assert.IsFalse(isValid);
        Assert.IsNotNull(message);
    }
}
