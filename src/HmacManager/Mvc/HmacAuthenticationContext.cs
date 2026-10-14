using HmacManager.Components;
using HmacManager.Policies;

namespace HmacManager.Mvc;

/// <summary>
/// Represents the context required for HMAC authentication, including the HMAC manager, authentication policy, and signature.
/// </summary>
public class HmacAuthenticationContext
{
    /// <summary>
    /// Gets or sets the HMAC manager used to handle HMAC-related operations.
    /// </summary>
    public IHmacManager? HmacManager { get; init; }

    /// <summary>
    /// Gets or sets the authentication policy associated with the HMAC authentication process.
    /// </summary>
    public HmacPolicy? Policy { get; init; }

    /// <summary>
    /// Gets or sets the HMAC signature used to validate the authenticity of a request.
    /// </summary>
    public string? Signature { get; init; }
}