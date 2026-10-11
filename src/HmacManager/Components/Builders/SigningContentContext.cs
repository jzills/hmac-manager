using HmacManager.Headers;

namespace HmacManager.Components;

/// <summary>
/// Represents the context required to build signing content for a request.
/// </summary>
public class SigningContentContext
{
    /// <summary>
    /// Gets or sets the HTTP request message to be signed.
    /// </summary>
    public HttpRequestMessage? Request { get; set; }

    /// <summary>
    /// Gets or sets the public key used for signing.
    /// </summary>
    public Guid? PublicKey { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the request was made.
    /// </summary>
    public DateTimeOffset? DateRequested { get; set; }

    /// <summary>
    /// Gets or sets the unique nonce value for the signing operation.
    /// </summary>
    public Guid? Nonce { get; set; }

    /// <summary>
    /// Gets or sets the collection of header values to be included in the signing content.
    /// </summary>
    public IReadOnlyCollection<HeaderValue> HeaderValues { get; set; } = [];

    /// <summary>
    /// Gets or sets the hash of the content to be included in the signing content.
    /// </summary>
    public string? ContentHash { get; set; }
}