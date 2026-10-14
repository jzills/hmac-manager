namespace HmacManager.Components;

/// <summary>
/// A class representing a single, generated <see cref="Hmac"/>.
/// </summary>
public record Hmac : HmacPartial
{
    /// <summary>
    /// Represents the Hmac signature generated from concatenating the following values together with a semicolon:<br/>
    /// <list>
    ///     <item>
    ///         <term>Method</term>
    ///         <description>The http method of the request.</description>
    ///     </item>
    ///     <item>
    ///         <term>Path, Query and Authority</term>
    ///         <description>If available, otherwise the original string for the uri is used.</description>
    ///     </item>
    ///     <item>
    ///         <term>Date Requested</term>
    ///         <description>The date the request was sent.</description>
    ///     </item>
    ///     <item>
    ///         <term>Public Key</term>
    ///         <description>The public key for the configured HmacManager.</description>
    ///     </item>
    ///     <item>
    ///         <term>Content Hash</term>
    ///         <description>If request content exists, the hash is generated as part of the signing content, otherwise this is skipped.</description>
    ///     </item>
    ///     <item>
    ///         <term>Header Values</term>
    ///         <description>These are the values configured for a specific scheme.</description>
    ///     </item>
    ///     <item>
    ///         <term>Nonce</term>
    ///         <description>Generated automatically by the configured HmacManager.</description>
    ///     </item>
    /// </list>
    /// </summary>
    /// <value>
    /// The complete, hashed signature added to the authorization header based on the above.
    /// </value>
    public string? Signature { get; init; }

    /// <summary>
    /// Checks that both hmac objects are equal.
    /// </summary>
    /// <param name="otherHmac">The other hmac to check equality against.</param>
    /// <returns>True if the two hmac objects are equal, otherwise false.</returns>
    internal bool IsVerified(Hmac? otherHmac) => Signature == otherHmac?.Signature;
}