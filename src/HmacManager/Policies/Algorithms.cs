using System.Text.Json.Serialization;
using HmacManager.Components;

namespace HmacManager.Policies;

/// <summary>
/// A class representing the <see cref="Algorithms"/> used for <see cref="Hmac"/> generation.
/// </summary>
public class Algorithms
{
    /// <summary>
    /// Represents the hash algorithm used when hashing request content, if present.
    /// </summary>
    /// <value>A ContentHashAlgorithm enum value. The default value is SHA256.</value>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContentHashAlgorithm ContentHashAlgorithm { get; set; } = ContentHashAlgorithm.SHA256;
    
    /// <summary>
    /// Represents the hash algorithm used when signing the constructed mac.
    /// </summary>
    /// <value>A SigningHashAlgorithm enum value. The default value is HAMCSHA256.</value>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SigningHashAlgorithm SigningHashAlgorithm { get; set; } = SigningHashAlgorithm.HMACSHA256;
}