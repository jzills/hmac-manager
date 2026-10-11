using HmacManager.Policies;

namespace HmacManager.Components;

/// <summary>
/// Options for configuring the HMAC signature provider.
/// </summary>
public class HmacSignatureProviderOptions
{
    /// <summary>
    /// Gets or sets the key credentials used for signing and verifying HMAC signatures.
    /// </summary>
    public required KeyCredentials Keys { get; set; }

    /// <summary>
    /// Gets or sets the algorithms used for HMAC signing and hashing.
    /// </summary>
    public required Algorithms Algorithms { get; set; }

    /// <summary>
    /// Gets or sets the generator responsible for creating the content hash.
    /// </summary>
    public required ContentHashGenerator ContentHashGenerator { get; set; }

    /// <summary>
    /// Gets or sets the generator responsible for creating the signature hash.
    /// </summary>
    public required SignatureHashGenerator SignatureHashGenerator { get; set; }

    /// <summary>
    /// Gets or sets the builder for creating the content to be signed. Defaults to <see cref="SigningContentBuilderValidated"/>.
    /// </summary>
    public SigningContentBuilder SigningContentBuilder { get; set; } = new SigningContentBuilderValidated();
}