using HmacManager.Caching;
using HmacManager.Components;
using HmacManager.Schemes;

namespace HmacManager.Policies;

/// <summary>
/// A class representing a <see cref="HmacPolicyBuilder"/> for creating a <see cref="HmacPolicy"/>.
/// </summary>
public class HmacPolicyBuilder
{
    /// <summary>
    /// Stores the credentials required for key-based authentication.
    /// </summary>
    internal readonly KeyCredentials Keys = new();

    /// <summary>
    /// Contains supported cryptographic algorithms for signing and verification.
    /// </summary>
    internal readonly Algorithms Algorithms = new();

    /// <summary>
    /// Manages unique nonce values to prevent replay attacks.
    /// </summary>
    internal readonly Nonce Nonce = new();

    /// <summary>
    /// Holds a collection of header schemes used in the HMAC authentication process.
    /// </summary>
    internal readonly SchemeCollection Schemes = new();

    /// <summary>
    /// Builds the content used for signing in HMAC authentication requests.
    /// </summary>
    internal SigningContentBuilder SigningContentBuilder = new();

    /// <summary>
    /// The name of the policy.
    /// </summary> 
    internal string? Name;

    /// <summary>
    /// Creates an instance of this builder.
    /// </summary>
    internal HmacPolicyBuilder()
    {   
    }

    /// <summary>
    /// Creates an instance of this builder using the specified name.
    /// </summary>
    public HmacPolicyBuilder(string name) => Name = name;

    /// <summary>
    /// Uses the specified <see cref="Guid"/> as the public key for this <see cref="HmacPolicy"/>.
    /// </summary>
    /// <param name="key">The public key.</param>
    /// <returns>A <see cref="HmacPolicyBuilder"/> that can be used to further configure a policy.</returns>
    public HmacPolicyBuilder UsePublicKey(Guid key)
    {
        Keys.PublicKey = key;
        return this;
    }

    /// <summary>
    /// Uses the specified base64 encoded <c>string</c> as the private key for this <see cref="HmacPolicy"/>.
    /// </summary>
    /// <param name="key">The private key.</param>
    /// <returns>A <see cref="HmacPolicyBuilder"/> that can be used to further configure a policy.</returns>
    public HmacPolicyBuilder UsePrivateKey(string key)
    {
        Keys.PrivateKey = key;
        return this;
    }

    /// <summary>
    /// Uses the specified <see cref="ContentHashAlgorithm"/> to hash the <see cref="HttpRequestMessage"/> content as part of the signature.
    /// </summary>
    /// <param name="hashAlgorithm">The <see cref="ContentHashAlgorithm"/>.</param>
    /// <returns>A <see cref="HmacPolicyBuilder"/> that can be used to further configure a policy.</returns>
    public HmacPolicyBuilder UseContentHashAlgorithm(ContentHashAlgorithm hashAlgorithm)
    {
        Algorithms.ContentHashAlgorithm = hashAlgorithm;
        return this;
    }

    /// <summary>
    /// Uses the specified <see cref="SigningHashAlgorithm"/> to sign the content for authentication.
    /// </summary>
    /// <param name="hashAlgorithm">The <see cref="SigningHashAlgorithm"/>.</param>
    /// <returns>A <see cref="HmacPolicyBuilder"/> that can be used to further configure a policy.</returns>
    public HmacPolicyBuilder UseSigningHashAlgorithm(SigningHashAlgorithm hashAlgorithm)
    {
        Algorithms.SigningHashAlgorithm = hashAlgorithm;
        return this;
    }

    /// <summary>
    /// Sets the maximum age on an <see cref="HttpRequestMessage"/> and the TTL for nonce cache entries.
    /// </summary>
    /// <param name="maxAgeInSeconds">The <see cref="TimeSpan"/> representing the max age of a request.</param>
    /// <returns>A <see cref="HmacPolicyBuilder"/> that can be used to further configure a policy.</returns>
    public HmacPolicyBuilder UseMemoryCache(int maxAgeInSeconds)
    {
        Nonce.CacheType = NonceCacheType.Memory;
        Nonce.MaxAgeInSeconds = maxAgeInSeconds;
        return this;
    }

    /// <summary>
    /// Sets the maximum age on an <see cref="HttpRequestMessage"/> and the TTL for nonce cache entries.
    /// </summary>
    /// <param name="maxAgeInSeconds">The <see cref="TimeSpan"/> representing the max age of a request.</param>
    /// <returns>A <see cref="HmacPolicyBuilder"/> that can be used to further configure a policy.</returns>
    public HmacPolicyBuilder UseDistributedCache(int maxAgeInSeconds)
    {
        Nonce.CacheType = NonceCacheType.Distributed;
        Nonce.MaxAgeInSeconds = maxAgeInSeconds;
        return this;
    }

    /// <summary>
    /// Defines the configuration for creating signing content for an <see cref="Hmac"/>.
    /// </summary>
    /// <param name="signingContentAccessor"></param>
    /// <returns>A <see cref="HmacPolicyBuilder"/> that can be used to further configure a policy.</returns>
    public HmacPolicyBuilder UseSigningContentBuilder(Func<SigningContentContext, string> signingContentAccessor)
    {
        SigningContentBuilder = new SigningContentBuilderAccessor(signingContentAccessor);
        return this;
    }

    /// <summary>
    /// Adds a specified scheme to the <see cref="SchemeCollection"/>. This can later be used
    /// to authenticate signatures.
    /// </summary>
    /// <param name="name">The name of the <see cref="Scheme"/>.</param>
    /// <param name="configureScheme">The configuration action for <see cref="Scheme"/>.</param>
    /// <returns>A <see cref="HmacPolicyBuilder"/> that can be used to further configure a policy.</returns>
    public HmacPolicyBuilder AddScheme(string name, Action<SchemeBuilder> configureScheme)
    {
        var builder = new SchemeBuilder(name);
        configureScheme.Invoke(builder);
        Schemes.Add(builder.Build());   
        return this;
    }

    /// <summary>
    /// Builds an instance of the configured <see cref="HmacPolicy"/>.
    /// </summary>
    /// <returns>The created policy.</returns>
    public HmacPolicy Build() => Build(Name);

    /// <summary>
    /// Builds an instance of the configured <see cref="HmacPolicy"/>.
    /// </summary>
    /// <returns>The created policy.</returns>
    internal HmacPolicy Build(string? name = null) => 
        new HmacPolicy(name)
        {
            Algorithms = Algorithms,
            Keys = Keys,
            Nonce = Nonce,
            Schemes = Schemes,
            SigningContentBuilder = SigningContentBuilder
        };
}