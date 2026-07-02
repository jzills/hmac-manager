using HmacManager.Operator.Entities;

namespace HmacManager.Operator.Rendering;

/// <summary>
/// Maps an <see cref="V1HmacPolicy"/> custom resource (plus its resolved private key) into the
/// Kubernetes-free <see cref="ResolvedPolicy"/> the renderer consumes.
/// </summary>
public static class PolicyMapper
{
    public static ResolvedPolicy ToResolvedPolicy(V1HmacPolicy cr, string privateKey) =>
        new(
            Name: cr.Metadata.Name,
            PublicKey: cr.Spec.PublicKey,
            PrivateKey: privateKey,
            ContentHashAlgorithm: cr.Spec.Algorithms.ContentHash.ToString(),
            SigningHashAlgorithm: cr.Spec.Algorithms.SigningHash.ToString(),
            NonceMaxAgeInSeconds: cr.Spec.Nonce.MaxAgeInSeconds,
            Schemes: cr.Spec.Schemes
                .Select(scheme => new ResolvedScheme(
                    scheme.Name,
                    scheme.Headers.Select(header => new ResolvedHeader(header.Name, header.ClaimType)).ToList()))
                .ToList());
}
