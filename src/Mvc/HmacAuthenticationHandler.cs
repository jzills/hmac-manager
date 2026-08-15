using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using HmacManager.Components;
using HmacManager.Diagnostics;
using HmacManager.Exceptions;
using HmacManager.Extensions;
using HmacManager.Mvc.Extensions.Internal;
using HmacManager.Features;

namespace HmacManager.Mvc;

/// <summary>
/// Handles HMAC-based authentication by verifying requests and generating authentication results.
/// </summary>
internal class HmacAuthenticationHandler : AuthenticationHandler<HmacAuthenticationOptions>
{
    /// <summary>
    /// Provides access to the HMAC authentication context for request verification.
    /// </summary>
    protected readonly IHmacAuthenticationContextProvider ContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacAuthenticationHandler"/> class with the specified context provider, options, logger, and URL encoder.
    /// </summary>
    /// <param name="contextProvider">The provider for HMAC authentication context information.</param>
    /// <param name="options">The options monitor for <see cref="HmacAuthenticationOptions"/>.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    public HmacAuthenticationHandler(
        IHmacAuthenticationContextProvider contextProvider,
        IOptionsMonitor<HmacAuthenticationOptions> options, 
        ILoggerFactory logger, 
        UrlEncoder encoder
    ) : base(options, logger, encoder)
    {
        ContextProvider = contextProvider;
    }

    /// <summary>
    /// Attempts to authenticate the request using HMAC authentication. 
    /// Returns a success result if authentication succeeds, or a failure result otherwise.
    /// </summary>
    /// <returns>A <see cref="Task{AuthenticateResult}"/> representing the authentication outcome.</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!ContextProvider.TryGetAuthenticationContext(Request.Headers, out var hmacAuthenticationContext))
        {
            HmacLog.AuthenticationSkipped(Logger, Request.Method, Request.Path.ToString());
            return AuthenticateResult.NoResult();
        }

        var policy = hmacAuthenticationContext.Policy!;

        // A request can name a policy that exists and a scheme that policy does not declare, in
        // which case the factory has no manager to give and GetResultAsync below would dereference
        // null. Fail the request instead: nothing can be verified without a manager, and the caller
        // supplied the name, so this is a rejected request rather than a server fault.
        //
        // Not logged here, and deliberately not at Warning: the scheme name comes off a request
        // header, so an edge deployment must not be drivable to unbounded Warning volume by it. The
        // factory already recorded which scheme failed to resolve, at Debug (event 1202).
        if (hmacAuthenticationContext.HmacManager is null)
        {
            return AuthenticateResult.Fail(new HmacAuthenticationException());
        }

        var hasValidKeys = await Options.Events.OnValidateKeysAsync(Context, policy.Keys);
        if (!hasValidKeys)
        {
            HmacLog.KeyValidationRejected(Logger, policy.Name!);
            return AuthenticateResult.Fail(new HmacAuthenticationException());
        }

        EnableBufferingIfContentExists();

        var result = await GetResultAsync(hmacAuthenticationContext);

        RewindIfContentExists();

        if (!result.IsSuccess)
        {
            // Why it failed was already recorded against the HmacManager category by VerifyAsync;
            // this pairs that with the policy as the pipeline saw it.
            HmacLog.AuthenticationFailed(Logger, policy.Name!);

            var failure = await Options.Events.OnAuthenticationFailureAsync(Request.HttpContext, result);
            return AuthenticateResult.Fail(failure);
        }

        var claims = await CreateClaimsAsync(result);

        HmacLog.AuthenticationSucceeded(Logger, result.Hmac!.Policy, result.Hmac.Scheme, claims.Count());

        return AuthenticateResult.Success(CreateSuccessTicket(claims, result));
    }

    /// <summary>
    /// Creates a collection of claims based on the HMAC result and any additional claims from success events.
    /// </summary>
    /// <param name="hmacResult">The result of the HMAC verification.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the collection of claims.</returns>
    private async Task<IEnumerable<Claim>> CreateClaimsAsync(HmacResult hmacResult)
    {
        var claims = new List<Claim>();
        if (hmacResult.Hmac?.HeaderValues?.Any() ?? false)
        {
            foreach (var headerValue in hmacResult.Hmac.HeaderValues)
            {
                claims.Add(new Claim(headerValue.ClaimType ?? headerValue.Name ?? string.Empty, headerValue.Value ?? string.Empty));
            }
        }

        var successHandlerClaims = await Options.Events.OnAuthenticationSuccessAsync(Request.HttpContext, hmacResult);
        if (successHandlerClaims.Any())
        {
            foreach (var claim in successHandlerClaims)
            {
                claims.Add(claim);
            }
        }

        // Adds policy and header claims to support dynamic authorization policies.
        claims.Add(new Claim(HmacAuthenticationDefaults.Properties.PolicyProperty, hmacResult.Hmac!.Policy));
        claims.Add(new Claim(HmacAuthenticationDefaults.Properties.SchemeProperty, hmacResult.Hmac.Scheme ?? string.Empty));

        return claims;
    }

    /// <summary>
    /// Creates an <see cref="AuthenticationTicket"/> representing a successful authentication.
    /// </summary>
    /// <param name="claims">The collection of claims generated for the user.</param>
    /// <param name="result">The HMAC result containing policy and scheme information.</param>
    /// <returns>The authentication ticket for the authenticated user.</returns>
    private AuthenticationTicket CreateSuccessTicket( 
        IEnumerable<Claim> claims, 
        HmacResult result
    ) => new AuthenticationTicket(
            new ClaimsPrincipal(
                new ClaimsIdentity(claims,
                    HmacAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                { HmacAuthenticationDefaults.Properties.PolicyProperty, result.Hmac!.Policy },
                { HmacAuthenticationDefaults.Properties.SchemeProperty, result.Hmac.Scheme }
            }),
            HmacAuthenticationDefaults.AuthenticationScheme
        );

    /// <summary>
    /// Enables buffering for the request body if content exists, allowing it to be read multiple times.
    /// </summary>
    private void EnableBufferingIfContentExists()
    {
        if (Request.HasContent())
        {
            Request.EnableBuffering();
        }
    }

    /// <summary>
    /// Rewinds the request body stream if content exists, resetting the position to the beginning.
    /// </summary>
    private void RewindIfContentExists()
    {
        if (Request.HasContent())
        {
            Request.Body.Rewind();
        }
    }

    /// <summary>
    /// Verifies the HMAC authentication result for the given context.
    /// </summary>
    /// <param name="context">The HMAC authentication context containing request information.</param>
    /// <returns>A task that represents the asynchronous operation, containing the HMAC verification result.</returns>
    private Task<HmacResult> GetResultAsync(HmacAuthenticationContext context) =>
        context.HmacManager!.VerifyAsync(
            Request.HttpContext.GetHttpRequestMessage()
        );
}