using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using HmacManager.Components;
using HmacManager.Exceptions;
using HmacManager.Policies;

namespace HmacManager.Mvc;

/// <summary>
/// Provides default event handlers for HMAC authentication, including handlers for authentication success,
/// authentication failure, and key validation.
/// </summary>
internal static class HmacEventsDefaults
{
    /// <summary>
    /// Default handler invoked when HMAC authentication succeeds. 
    /// Returns an empty array of claims by default.
    /// </summary>
    internal static Func<HttpContext, HmacResult, Task<Claim[]>> OnAuthenticationSuccessAsync = 
        (_, _) => Task.FromResult<Claim[]>([]);

    /// <summary>
    /// Default handler invoked when HMAC authentication fails. 
    /// Returns a new <see cref="HmacAuthenticationException"/> by default.
    /// </summary>
    internal static Func<HttpContext, HmacResult, Task<Exception>> OnAuthenticationFailureAsync = 
        (_, _) => Task.FromResult<Exception>(new HmacAuthenticationException());

    /// <summary>
    /// Default handler for validating key credentials in the HMAC authentication process.
    /// Returns <c>true</c> by default, indicating that the key credentials are valid.
    /// </summary>
    internal static Func<HttpContext, KeyCredentials, Task<bool>> OnValidateKeysAsync = 
        (_, _) => Task.FromResult(true);
}