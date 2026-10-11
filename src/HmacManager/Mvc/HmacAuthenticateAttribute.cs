using HmacManager.Mvc.Extensions.Internal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HmacManager.Mvc;

/// <summary>
/// An attribute that enforces HMAC (Hash-based Message Authentication Code) authentication on a class or method.
/// Implements <see cref="IAuthorizationRequirement"/> and <see cref="IAsyncAuthorizationFilter"/> 
/// to provide asynchronous authorization handling based on a specified policy and optional scheme.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class HmacAuthenticateAttribute : Attribute, IAuthorizationRequirement, IAsyncAuthorizationFilter
{
    /// <summary>
    /// Gets or sets the policy required for authorization.
    /// </summary>
    public required string Policy { get; init; }

    /// <summary>
    /// Gets or sets the optional scheme used in HMAC authentication.
    /// </summary>
    public string? Scheme { get; init; }

    /// <summary>
    /// Asynchronously handles the authorization for the specified context.
    /// </summary>
    /// <param name="context">The authorization filter context containing HTTP context and request details.</param>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext is not null)
        {
            var authenticateResult = await context.HttpContext.AuthenticateAsync(HmacAuthenticationDefaults.AuthenticationScheme);
            if (authenticateResult is not null)
            {
                if (authenticateResult.Succeeded)
                {
                    if (authenticateResult.Properties.TryGetHmacProperties(out var policy, out var scheme))
                    {
                        if (!IsMatch(policy, scheme))
                        {
                            context.Result = new UnauthorizedResult();
                            return;
                        }
                    }
                }
                else
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Checks if the provided policy and scheme match the required values.
    /// </summary>
    /// <param name="policy">The policy retrieved from the authentication result.</param>
    /// <param name="scheme">The scheme retrieved from the authentication result.</param>
    /// <returns>
    /// <see langword="true"/> if the policy and scheme match the required values; otherwise, <see langword="false"/>.
    /// </returns>
    internal bool IsMatch(string? policy, string? scheme) =>
        (Policy, Scheme) switch
        {
            (null, null)    => false,
            (_, null)       => Policy == policy,
            (_, _)          => Policy == policy && Scheme == scheme
        };
}