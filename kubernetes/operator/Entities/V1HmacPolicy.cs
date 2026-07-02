using k8s.Models;
using KubeOps.Abstractions.Entities;

namespace HmacManager.Operator.Entities;

/// <summary>
/// The <c>HmacPolicy</c> custom resource. A policy's name is its <c>metadata.name</c>.
/// Public fields are reconciled into the aggregate ConfigMap; the private key is composed
/// into the aggregate Secret from the referenced Secret (see the controller).
/// </summary>
[KubernetesEntity(Group = "hmac-manager.io", ApiVersion = "v1alpha1", Kind = "HmacPolicy", PluralName = "hmacpolicies")]
public sealed class V1HmacPolicy : CustomKubernetesEntity<V1HmacPolicy.PolicySpec, V1HmacPolicy.PolicyStatus>
{
    public sealed class PolicySpec
    {
        /// <summary>The policy's public key (a GUID).</summary>
        public string PublicKey { get; set; } = string.Empty;

        /// <summary>
        /// Reference to the Secret holding this policy's private key. Optional in the schema so a
        /// future operator-generated-key path is purely additive; a missing ref is currently
        /// surfaced as an Invalid status.
        /// </summary>
        public SecretKeyReference? PrivateKeySecretRef { get; set; }

        public AlgorithmsSpec Algorithms { get; set; } = new();

        public NonceSpec Nonce { get; set; } = new();

        public List<SchemeSpec> Schemes { get; set; } = new();
    }

    public sealed class PolicyStatus
    {
        /// <summary>Ready | Invalid | Pending.</summary>
        public string Phase { get; set; } = "Pending";

        public string? Message { get; set; }

        public long ObservedGeneration { get; set; }
    }

    public sealed class SecretKeyReference
    {
        public string Name { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
    }

    public sealed class AlgorithmsSpec
    {
        // Strings validated by the CRD schema's enum, avoiding any ambiguity in how a CR's
        // string value would (de)serialize to a C# enum. Values flow straight into the rendered
        // config.json, where the library parses them.
        public string ContentHash { get; set; } = "SHA256";
        public string SigningHash { get; set; } = "HMACSHA256";
    }

    public sealed class NonceSpec
    {
        public int MaxAgeInSeconds { get; set; } = 60;
    }

    public sealed class SchemeSpec
    {
        public string Name { get; set; } = string.Empty;
        public List<HeaderSpec> Headers { get; set; } = new();
    }

    public sealed class HeaderSpec
    {
        public string Name { get; set; } = string.Empty;
        public string? ClaimType { get; set; }
    }
}
