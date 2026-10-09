using Microsoft.AspNetCore.Authorization;
using HmacManager.Mvc.Extensions.Internal;

namespace HmacManager.Mvc;

/// <summary>
/// Handles HMAC-based authorization by validating the user’s authentication policy and scheme.
/// </summary>
public class HmacAuthorizationHandler : AuthorizationHandler<HmacAuthenticateAttribute>
{
    /// <summary>
    /// Evaluates the specified authorization requirement against the current authorization context.
    /// If the user's policy and scheme match the requirement, authorization succeeds; otherwise, it fails.
    /// </summary>
    /// <param name="context">The authorization context containing information about the user and resources.</param>
    /// <param name="requirement">The HMAC authentication requirement to validate against the user's policy and scheme.</param>
    /// <returns>A completed <see cref="Task"/> representing the asynchronous operation.</returns>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, 
        HmacAuthenticateAttribute requirement
    )
    {
        if (context.User.TryGetPolicyScheme(out var policy, out var scheme) && 
             requirement.IsMatch(policy, scheme))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }
}