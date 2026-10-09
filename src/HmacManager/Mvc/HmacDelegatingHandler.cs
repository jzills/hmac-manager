using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HmacManager.Components;
using HmacManager.Diagnostics;
using HmacManager.Exceptions;

namespace HmacManager.Mvc;

/// <summary>
/// A delegating handler that adds HMAC authentication to outgoing HTTP requests.
/// </summary>
public class HmacDelegatingHandler : DelegatingHandler
{
    private readonly IHmacManager _hmacManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacDelegatingHandler"/> class that does not log.
    /// </summary>
    /// <param name="hmacManager">An instance of <see cref="IHmacManager"/> to sign the request.</param>
    public HmacDelegatingHandler(IHmacManager hmacManager)
        : this(hmacManager, NullLogger<HmacDelegatingHandler>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacDelegatingHandler"/> class.
    /// </summary>
    /// <param name="hmacManager">An instance of <see cref="IHmacManager"/> to sign the request.</param>
    /// <param name="logger">The <see cref="ILogger"/> to record unsent requests to.</param>
    public HmacDelegatingHandler(IHmacManager hmacManager, ILogger<HmacDelegatingHandler> logger)
    {
        _hmacManager = hmacManager;
        _logger = logger;
    }

    /// <summary>
    /// Sends the HTTP request synchronously, signing it before sending.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The HTTP response message returned by the base handler.</returns>
    /// <exception cref="HmacSigningException">Thrown when the HMAC signing fails.</exception>
    protected override HttpResponseMessage Send(
        HttpRequestMessage request, 
        CancellationToken cancellationToken
    ) => SendAsync(request, cancellationToken).Result;

    /// <summary>
    /// Asynchronously sends the HTTP request, signing it before sending.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the HTTP response message returned by the base handler.</returns>
    /// <exception cref="HmacSigningException">Thrown when the HMAC signing fails.</exception>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var signingResult = await _hmacManager.SignAsync(request);
        if (signingResult.IsSuccess)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // The exception carries the result, but it is thrown into the caller's HttpClient call —
        // which may be swallowed, retried by a resilience policy, or surfaced far from here. Record
        // the abandoned request against this handler's category so it is traceable either way.
        HmacLog.OutboundRequestNotSent(_logger, request.Method, request.RequestUri);

        throw new HmacSigningException(signingResult);
    }
}