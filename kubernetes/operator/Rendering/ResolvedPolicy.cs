namespace HmacManager.Operator.Rendering;

/// <summary>
/// A policy whose private key has been resolved from its referenced Secret and which is ready
/// to be rendered into the aggregate config. These records are deliberately free of any
/// Kubernetes-client or KubeOps types so the rendering logic can be unit-tested in isolation.
/// </summary>
public sealed record ResolvedPolicy(
    string Name,
    string PublicKey,
    string PrivateKey,
    string ContentHashAlgorithm,
    string SigningHashAlgorithm,
    int NonceMaxAgeInSeconds,
    IReadOnlyList<ResolvedScheme> Schemes);

public sealed record ResolvedScheme(string Name, IReadOnlyList<ResolvedHeader> Headers);

public sealed record ResolvedHeader(string Name, string? ClaimType);

/// <summary>
/// The rendered aggregate: the <c>config.json</c> string for the ConfigMap and the private-key
/// files for the Secret, keyed by their mount file name (<c>HmacManager__{i}__Keys__PrivateKey</c>).
/// </summary>
public sealed record RenderResult(string ConfigJson, IReadOnlyDictionary<string, string> KeyFiles);
