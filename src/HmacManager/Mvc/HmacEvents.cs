using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using HmacManager.Components;
using HmacManager.Policies;

namespace HmacManager.Mvc;

/// <summary>
/// A class representing <see cref="HmacEvents"/>.
/// </summary>
public class HmacEvents
{
    /// <summary>
    /// The event fired when authentication is successful.
    /// </summary>
    /// <remarks>The <see cref="HttpContext"/> and <see cref="HmacResult"/> are passed into the handler with
    /// the expectation that user claims will be returned and automatically added to the <see cref="ClaimsIdentity"/>
    /// for the authentication scheme found at <see cref="HmacAuthenticationDefaults.AuthenticationScheme"/>.
    /// </remarks>
    public Func<HttpContext, HmacResult, Task<Claim[]>> OnAuthenticationSuccessAsync { get; init; } = HmacEventsDefaults.OnAuthenticationSuccessAsync;

    /// <summary>
    /// The event fired when authentication is a failure.
    /// </summary>
    /// /// <remarks>The <see cref="HttpContext"/> and <see cref="HmacResult"/> are passed into the handler with
    /// the expectation that an exception will be returned indicating the error.
    /// </remarks>
    public Func<HttpContext, HmacResult, Task<Exception>> OnAuthenticationFailureAsync { get; init; } = HmacEventsDefaults.OnAuthenticationFailureAsync;

    /// <summary>
    /// The event fired prior to authenticating a request in order to prevalidate the keys.
    /// </summary>
    /// /// <remarks>The <see cref="HttpContext"/> and <see cref="KeyCredentials"/> are passed into the handler with
    /// the expectation that a <c>bool</c> will be returned indicating the validity of the keys.
    /// </remarks>
    public Func<HttpContext, KeyCredentials, Task<bool>> OnValidateKeysAsync { get; init;  } = HmacEventsDefaults.OnValidateKeysAsync;
}