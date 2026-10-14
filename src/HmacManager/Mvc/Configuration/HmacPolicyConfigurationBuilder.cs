using HmacManager.Caching;
using HmacManager.Exceptions;
using HmacManager.Policies;

namespace HmacManager.Mvc.Configuration;

/// <summary>
/// Builds and configures an HMAC policy using the provided <see cref="HmacPolicyConfigurationSection"/>.
/// </summary>
internal class HmacPolicyConfigurationBuilder : HmacPolicyBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HmacPolicyConfigurationBuilder"/> class.
    /// </summary>
    /// <param name="policy">The <see cref="HmacPolicyConfigurationSection"/> containing the policy configuration.</param>
    /// <exception cref="ArgumentException">Thrown if the <paramref name="policy"/> contains invalid values such as null or empty strings.</exception>
    /// <exception cref="ArgumentNullException">Thrown if any required property in <paramref name="policy"/> is null.</exception>
    /// <exception cref="NonceCacheTypeNotSupportedException">Thrown if an unsupported <see cref="NonceCacheType"/> is specified.</exception>
    public HmacPolicyConfigurationBuilder(HmacPolicyConfigurationSection policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.Name, nameof(policy.Name));
        ArgumentNullException.ThrowIfNull(policy.Algorithms, nameof(policy.Algorithms));
        ArgumentNullException.ThrowIfNull(policy.Keys, nameof(policy.Keys));
        ArgumentNullException.ThrowIfNull(policy.Nonce, nameof(policy.Nonce));

        // Configure the public and private keys for HMAC policy
        UsePublicKey(policy.Keys.PublicKey);
        UsePrivateKey(policy.Keys.PrivateKey);
        
        // Set the content hash and signing hash algorithms for the policy
        UseContentHashAlgorithm(policy.Algorithms.ContentHashAlgorithm);
        UseSigningHashAlgorithm(policy.Algorithms.SigningHashAlgorithm);

        // Configure nonce caching based on the specified cache type
        switch (policy.Nonce.CacheType)
        {
            case NonceCacheType.Memory:
                UseMemoryCache(policy.Nonce.MaxAgeInSeconds);
                break;
            case NonceCacheType.Distributed:
                UseDistributedCache(policy.Nonce.MaxAgeInSeconds);
                break;
            default:
                throw new NonceCacheTypeNotSupportedException($"The specified \"CacheType\" of {Enum.GetName(policy.Nonce.CacheType)} is not supported.");
        }
 
        // Add header schemes if provided
        if (policy.Schemes?.Any() ?? false)
        {
            foreach (var scheme in policy.Schemes)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(scheme.Name, nameof(scheme.Name));
                
                if (scheme.Headers?.Count > 0)
                {
                    AddScheme(scheme.Name, builder =>
                    {
                        foreach (var header in scheme?.Headers ?? [])
                        {
                            builder.AddHeader(header.Name!, header.ClaimType!);
                        }
                    });
                }
            }
        }
    }
}