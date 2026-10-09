namespace HmacManager.Components;

/// <summary>
/// An <c>enum</c> representing a <see cref="SigningHashAlgorithm"/>.
/// </summary>
public enum SigningHashAlgorithm
{
    /// <summary>
    /// Represents the HMACSHA1 cryptographic hash function.
    /// </summary>
    HMACSHA1,

    /// <summary>
    /// Represents the HMACSHA256 cryptographic hash function.
    /// </summary>
    HMACSHA256,

    /// <summary>
    /// Represents the HMACSHA512 cryptographic hash function.
    /// </summary>
    HMACSHA512
}