using System.Text.Json.Serialization;
using HmacManager.Caching;
using HmacManager.Components;
using HmacManager.Schemes;

namespace HmacManager.Policies;

/// <summary>
/// A class representing a <see cref="HmacPolicy"/>.
/// </summary>
public class HmacPolicy
{
    /// <summary>
    /// The name of the policy.
    /// </summary>
    [JsonInclude]
    public string? Name { get; init; }

    /// <summary>
    /// Creates an instance of <see cref="HmacPolicy"/>.
    /// </summary> 
    internal HmacPolicy() { }

    /// <summary>
    /// Creates an <see cref="HmacPolicy"/> from a specified name.
    /// </summary>
    /// <param name="name">The name of the <see cref="HmacPolicy"/>.</param>
    public HmacPolicy(string? name) => Name = name;

    /// <summary>
    /// The <see cref="KeyCredentials"/> used to sign and verify authentication codes.
    /// This includes both <see cref="KeyCredentials.PublicKey"/> and <see cref="KeyCredentials.PrivateKey"/>.
    /// </summary>
    public KeyCredentials Keys { get; set; } = new();

    /// <summary>
    /// The <see cref="Algorithms"/> used to compute hash values.
    /// This includes both <see cref="ContentHashAlgorithm"/> and <see cref="SigningHashAlgorithm"/>.
    /// </summary>
    public Algorithms Algorithms { get; set; } = new();
    
    /// <summary>
    /// A <see cref="Nonce"/> object containing the cache type and max age.
    /// </summary>
    public Nonce Nonce { get; set; } = new();

    /// <summary>
    /// A collection of <see cref="Scheme"/> objects for this policy.
    /// </summary>
    public SchemeCollection Schemes { get; set; } = new();

    /// <summary>
    /// The <see cref="SigningContentBuilder"/> used to construct signing content.
    /// </summary>
    public SigningContentBuilder SigningContentBuilder { get; set; } = new SigningContentBuilderValidated();
}