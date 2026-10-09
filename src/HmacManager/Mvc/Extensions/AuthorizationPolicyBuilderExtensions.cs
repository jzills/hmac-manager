using Microsoft.AspNetCore.Authorization;

namespace HmacManager.Mvc;

/// <summary>
/// Provides extension methods for the <see cref="AuthorizationPolicyBuilder"/> to enforce HMAC-based authentication policies and schemes.
/// </summary>
public static class AuthorizationPolicyBuilderExtensions
{
    /// <summary>
    /// Requires HMAC authentication for the policy, optionally specifying a scheme.
    /// </summary>
    /// <param name="builder">The <see cref="AuthorizationPolicyBuilder"/> instance.</param>
    /// <param name="policy">The policy name or names that must be used in the claim.</param>
    /// <param name="scheme">Optional authentication scheme to be applied. If not provided, the default scheme will be used.</param>
    /// <returns>The <see cref="AuthorizationPolicyBuilder"/> for further configuration.</returns>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="policy"/> is null or whitespace.</exception>
    /// <remarks>
    /// This method configures the authorization policy to require that the request includes a claim 
    /// for the specified HMAC policy and optionally a scheme.
    /// </remarks>
    public static AuthorizationPolicyBuilder RequireHmacAuthentication(
        this AuthorizationPolicyBuilder builder,
        string policy,
        string? scheme = null
    ) 
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy, nameof(policy));
        
        builder.RequireHmacPolicy(policy);

        if (!string.IsNullOrWhiteSpace(scheme))
        {
            builder.RequireHmacScheme(scheme);
        }

        return builder;
    }

    /// <summary>
    /// Requires that the claim for a given HMAC policy is present in the request.
    /// </summary>
    /// <param name="builder">The <see cref="AuthorizationPolicyBuilder"/> instance.</param>
    /// <param name="policies">One or more policy names that must be included in the claim.</param>
    /// <returns>The <see cref="AuthorizationPolicyBuilder"/> for further configuration.</returns>
    /// <exception cref="ArgumentException">Thrown when any of the <paramref name="policies"/> is null or whitespace.</exception>
    /// <remarks>
    /// This method configures the authorization policy to require that the request includes a claim
    /// for one or more HMAC policies.
    /// </remarks>
    public static AuthorizationPolicyBuilder RequireHmacPolicy(
        this AuthorizationPolicyBuilder builder, 
        params string[] policies
    ) 
    {
        ThrowIfAnyAreNullOrWhiteSpace(policies);

        return builder.RequireClaim(HmacAuthenticationDefaults.Properties.PolicyProperty, policies);
    }

    /// <summary>
    /// Requires that the claim for a given HMAC authentication scheme is present in the request.
    /// </summary>
    /// <param name="builder">The <see cref="AuthorizationPolicyBuilder"/> instance.</param>
    /// <param name="schemes">One or more scheme names that must be included in the claim.</param>
    /// <returns>The <see cref="AuthorizationPolicyBuilder"/> for further configuration.</returns>
    /// <exception cref="ArgumentException">Thrown when any of the <paramref name="schemes"/> is null or whitespace.</exception>
    /// <remarks>
    /// This method configures the authorization policy to require that the request includes a claim
    /// for one or more HMAC authentication schemes.
    /// </remarks>
    public static AuthorizationPolicyBuilder RequireHmacScheme(
        this AuthorizationPolicyBuilder builder, 
        params string[] schemes
    ) 
    {
        ThrowIfAnyAreNullOrWhiteSpace(schemes);

        return builder.RequireClaim(HmacAuthenticationDefaults.Properties.SchemeProperty, schemes);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> if any of the provided values are null or whitespace.
    /// </summary>
    /// <param name="values">The values to check.</param>
    /// <exception cref="ArgumentException">Thrown if any value is null or whitespace.</exception>
    private static void ThrowIfAnyAreNullOrWhiteSpace(params string[] values)
    {
        foreach (var value in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
        }
    }
}