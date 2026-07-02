using HmacManager.Components;
using HmacManager.Operator.Entities;
using k8s;
using NUnit.Framework;

namespace HmacManager.Operator.Tests;

/// <summary>
/// Guards the CR round-trip for the algorithm enums. KubeOps deserializes watched custom resources
/// with the Kubernetes client's System.Text.Json options (<see cref="KubernetesJson"/>). A CR stores
/// algorithm values as their string names (constrained by the CRD schema's <c>enum</c>), so those
/// names must survive the trip to and from the C# enum — which only holds when the property carries a
/// string-enum converter. Without one, System.Text.Json treats the enum as its integer ordinal and
/// throws on the stored string, breaking every reconcile.
/// </summary>
[TestFixture]
public class AlgorithmSerializationTests
{
    [Test]
    public void CR_algorithm_string_names_deserialize_to_enum_values()
    {
        const string json = """
        {
          "apiVersion": "hmac-manager.io/v1alpha1",
          "kind": "HmacPolicy",
          "metadata": { "name": "payments-api" },
          "spec": {
            "publicKey": "00000000-0000-0000-0000-000000000001",
            "algorithms": { "contentHash": "SHA256", "signingHash": "HMACSHA512" }
          }
        }
        """;

        var policy = KubernetesJson.Deserialize<V1HmacPolicy>(json);

        Assert.AreEqual(ContentHashAlgorithm.SHA256, policy.Spec.Algorithms.ContentHash);
        Assert.AreEqual(SigningHashAlgorithm.HMACSHA512, policy.Spec.Algorithms.SigningHash);
    }

    [Test]
    public void Enum_values_serialize_back_to_their_string_names()
    {
        var policy = new V1HmacPolicy
        {
            Spec = new V1HmacPolicy.PolicySpec
            {
                Algorithms = new V1HmacPolicy.AlgorithmsSpec
                {
                    ContentHash = ContentHashAlgorithm.SHA256,
                    SigningHash = SigningHashAlgorithm.HMACSHA512,
                },
            },
        };

        var json = KubernetesJson.Serialize(policy);

        StringAssert.Contains("\"SHA256\"", json);
        StringAssert.Contains("\"HMACSHA512\"", json);
    }
}
