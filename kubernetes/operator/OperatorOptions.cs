namespace HmacManager.Operator;

/// <summary>
/// Operator-level settings, bound from the "Operator" configuration section (env vars in-cluster).
/// </summary>
public sealed class OperatorOptions
{
    public const string SectionName = "Operator";

    /// <summary>
    /// Namespace to watch and to write the aggregate resources into. When empty, the reconciled
    /// resource's own namespace (falling back to the pod's current namespace) is used.
    /// </summary>
    public string? WatchNamespace { get; set; }

    /// <summary>Name of the aggregate ConfigMap the pods mount for public policy config.</summary>
    public string ConfigMapName { get; set; } = "hmac-manager-config";

    /// <summary>Name of the aggregate Secret the pods mount for private keys.</summary>
    public string SecretName { get; set; } = "hmac-manager-keys";

    /// <summary>Cluster-wide nonce cache type applied to every rendered policy ("Memory" or "Distributed").</summary>
    public string NonceCacheType { get; set; } = "Memory";
}
