using System.Security.Claims;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="ClaimsPrincipal"/> to facilitate retrieval of HMAC-specific claims.
/// </summary>
internal static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Tries to get the HMAC policy and scheme from the <see cref="ClaimsPrincipal"/>'s claims.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> from which to retrieve the policy and scheme.</param>
    /// <param name="policy">The policy associated with the HMAC authentication, if found.</param>
    /// <param name="scheme">The authentication scheme associated with the HMAC, if found.</param>
    /// <returns><c>true</c> if the policy is found and valid; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method checks the <see cref="ClaimsPrincipal.Identities"/> collection to find the identity
    /// with the appropriate <see cref="ClaimsIdentity.AuthenticationType"/> and then retrieves the HMAC
    /// properties defined in <see cref="HmacAuthenticationDefaults.Properties"/>.
    /// </remarks>
    internal static bool TryGetPolicyScheme(this ClaimsPrincipal user, out string? policy, out string? scheme)
    {
        var userIdentity = user.Identities.FirstOrDefault(userIdentity => userIdentity.AuthenticationType == HmacAuthenticationDefaults.AuthenticationScheme);
        policy = userIdentity?.FindFirst(HmacAuthenticationDefaults.Properties.PolicyProperty)?.Value;
        scheme = userIdentity?.FindFirst(HmacAuthenticationDefaults.Properties.SchemeProperty)?.Value;
        return !string.IsNullOrWhiteSpace(policy);
    }
}