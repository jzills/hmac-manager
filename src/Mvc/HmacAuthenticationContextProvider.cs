using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HmacManager.Components;
using HmacManager.Diagnostics;
using HmacManager.Exceptions;
using HmacManager.Policies;
using HmacManager.Policies.Extensions;

namespace HmacManager.Mvc;

/// <summary>
/// Provides an implementation of <see cref="IHmacAuthenticationContextProvider"/> 
/// to retrieve the HMAC authentication context from the HTTP headers.
/// </summary>
public class HmacAuthenticationContextProvider : IHmacAuthenticationContextProvider
{
    /// <summary>
    /// The factory used to create instances of HMAC managers.
    /// </summary>
    protected readonly IHmacManagerFactory Factory;

    /// <summary>
    /// The collection of HMAC policies available for authentication.
    /// </summary>
    protected readonly IHmacPolicyCollection Policies;

    /// <summary>
    /// The factory used to create header parsers for processing authentication headers.
    /// </summary>
    protected readonly IHmacHeaderParserFactory HeaderParserFactory;

    /// <summary>
    /// The <see cref="ILogger"/> unresolvable policies are recorded to.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacAuthenticationContextProvider"/> class that does not log.
    /// </summary>
    /// <param name="factory">The factory used to create HMAC managers.</param>
    /// <param name="policies">The collection of HMAC policies.</param>
    /// <param name="headerParserFactory">The factory used to create header parsers.</param>
    public HmacAuthenticationContextProvider(
        IHmacManagerFactory factory,
        IHmacPolicyCollection policies,
        IHmacHeaderParserFactory headerParserFactory
    ) : this(factory, policies, headerParserFactory, NullLogger<HmacAuthenticationContextProvider>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacAuthenticationContextProvider"/> class.
    /// </summary>
    /// <param name="factory">The factory used to create HMAC managers.</param>
    /// <param name="policies">The collection of HMAC policies.</param>
    /// <param name="headerParserFactory">The factory used to create header parsers.</param>
    /// <param name="logger">The <see cref="ILogger"/> to record unresolvable policies to.</param>
    public HmacAuthenticationContextProvider(
        IHmacManagerFactory factory,
        IHmacPolicyCollection policies,
        IHmacHeaderParserFactory headerParserFactory,
        ILogger<HmacAuthenticationContextProvider> logger
    )
    {
        Factory = factory;
        Policies = policies;
        HeaderParserFactory = headerParserFactory;
        Logger = logger;
    }

    /// <summary>
    /// Tries to retrieve the HMAC authentication context from a dictionary of headers.
    /// </summary>
    /// <param name="headers">A dictionary of HTTP headers containing authentication data.</param>
    /// <param name="context">The retrieved <see cref="HmacAuthenticationContext"/> if successful, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the context was successfully retrieved; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="HmacPolicyNotFoundException">Thrown when the specified HMAC policy is not found.</exception>
    public bool TryGetAuthenticationContext(IDictionary<string, string> headers, out HmacAuthenticationContext context)
    {
        if (headers.TryGetValue(HmacAuthenticationDefaults.Headers.Authorization, out var authorizationHeader))
        {
            if (authorizationHeader.StartsWith(HmacAuthenticationDefaults.AuthenticationScheme))
            {
                var hmacPartial = HeaderParserFactory.Create(headers).Parse(out var signature);
                var hmacManager = Factory.Create(hmacPartial.Policy, hmacPartial.Scheme);

                if (Policies.TryGetValue(hmacPartial.Policy, out var hmacPolicy))
                {
                    context = new HmacAuthenticationContext
                    {
                        HmacManager = hmacManager,
                        Policy = hmacPolicy,
                        Signature = signature
                    };
                }
                else
                {
                    // Thrown, but log first: this exception is caught and flattened to a 403 by the
                    // ext-authz service, where the policy name would otherwise be lost.
                    HmacLog.RequestedPolicyNotRegistered(Logger, hmacPartial.Policy);

                    throw new HmacPolicyNotFoundException(hmacPartial.Policy);
                }

                return true;
            }
        }

        context = new HmacAuthenticationContext();
        return false;
    }

    /// <summary>
    /// Tries to retrieve the HMAC authentication context from an <see cref="IHeaderDictionary"/>.
    /// </summary>
    /// <param name="headers">The collection of HTTP headers containing authentication data.</param>
    /// <param name="context">The retrieved <see cref="HmacAuthenticationContext"/> if successful, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the context was successfully retrieved; otherwise, <see langword="false"/>.</returns>
    public bool TryGetAuthenticationContext(IHeaderDictionary headers, out HmacAuthenticationContext context) =>
        TryGetAuthenticationContext(
            headers.ToDictionary(
                header => header.Key,
                header => header.Value.FirstOrDefault() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase
        ), out context);
}