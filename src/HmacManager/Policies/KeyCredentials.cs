namespace HmacManager.Policies;

/// <summary>
/// Represents a set of credentials for HMAC authentication, including a public key and a private key.
/// </summary>
public class KeyCredentials
{
    /// <summary>
    /// Gets or sets the public key, represented as a <see cref="Guid"/>. 
    /// This is typically used to identify the client making the request.
    /// </summary>
    public Guid PublicKey { get; set; } = Guid.Empty;

    /// <summary>
    /// Gets or sets the private key, represented as a <see cref="string"/>.
    /// This key is used in conjunction with the public key for signing and verifying requests.
    /// </summary>
    public string PrivateKey { get; set; } = string.Empty;
}