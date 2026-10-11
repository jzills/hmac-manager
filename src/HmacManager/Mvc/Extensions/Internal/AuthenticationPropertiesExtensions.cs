using Microsoft.AspNetCore.Authentication;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="AuthenticationProperties"/> to facilitate retrieval of HMAC-specific properties.
/// </summary>
internal static class AuthenticationPropertiesExtensions
{
    /// <summary>
    /// Tries to get the HMAC policy and scheme properties from the <see cref="AuthenticationProperties"/>.
    /// </summary>
    /// <param name="properties">The authentication properties to extract the HMAC properties from.</param>
    /// <param name="policy">The policy associated with the HMAC authentication, if found.</param>
    /// <param name="scheme">The authentication scheme associated with the HMAC, if found.</param>
    /// <returns><c>true</c> if both the policy and scheme are found; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method checks the <see cref="AuthenticationProperties.Items"/> dictionary for the presence of
    /// the properties defined in <see cref="HmacAuthenticationDefaults.Properties"/>.
    /// </remarks>
    internal static bool TryGetHmacProperties(this AuthenticationProperties properties, out string? policy, out string? scheme)
    {
        var hasPolicy = properties.Items.TryGetValue(HmacAuthenticationDefaults.Properties.PolicyProperty, out policy);
        var hasScheme = properties.Items.TryGetValue(HmacAuthenticationDefaults.Properties.SchemeProperty, out scheme);
        return hasPolicy && hasScheme;
    }
}