using HmacManager.Headers;

namespace HmacManager.Components;

/// <summary>
/// A record representing a partial construction of an hmac.
/// </summary>
public record HmacPartial
{
    /// <summary>
    /// Represents the policy used by HmacManager for signing and verification.
    /// </summary>
    /// <value>The name of the policy.</value>
    public required string Policy { get; init; }

    /// <summary>
    /// Represents the name of the <see cref="Scheme"/> used by <see cref="HmacManager"/> for signing and verification.
    /// </summary>
    /// <value>The name of the <see cref="Scheme"/>.</value>
    public string? Scheme { get; init; }
    
    /// <summary>
    /// Represents the date a request is made.
    /// </summary>
    /// <value>Defaults to the current utc time.</value>
    public DateTimeOffset DateRequested { get; init; } = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    /// <summary>
    /// Represents a nonce to prevent replay attacks.
    /// </summary>
    /// <value>Defaults to a new guid.</value>
    public Guid Nonce { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Represents the request content hash based on the configured HmacManager.
    /// </summary>
    /// <value>The hashed request content, if exists, otherwise null.</value>
    public string? SigningContent { get; init; }

    /// <summary>
    /// Represents custom header values configured on a per scheme basis. If defined, these become part of the signature.
    /// </summary>
    /// <value>An array of header values, empty if there is no scheme defined.</value>
    public IReadOnlyCollection<HeaderValue> HeaderValues { get; init; } = [];
}