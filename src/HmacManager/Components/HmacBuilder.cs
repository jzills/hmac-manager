using HmacManager.Exceptions;
using HmacManager.Extensions;
using HmacManager.Headers;
using HmacManager.Schemes;

namespace HmacManager.Components;

/// <summary>
/// A class representing a builder for creating hmacs.
/// </summary>
public class HmacBuilder
{
    /// <summary>
    /// The hmac used for construction.
    /// </summary>
    protected readonly Hmac Hmac;

    /// <summary>
    /// The http request message used to derive the hmac.
    /// </summary>
    protected readonly HttpRequestMessage Request;

    /// <summary>
    /// Creates a builder based on an incoming request and scheme.
    /// </summary>
    /// <param name="request">The http request message for the basis of hmac computation.</param>
    /// <param name="policy">The policy to sign against.</param>
    /// <param name="scheme">The scheme to sign against.</param>
    public HmacBuilder(HttpRequestMessage request, string policy, Scheme? scheme = null)
    {
        Hmac = new Hmac { Policy = policy };

        if (request.Headers.TryParseHeaders(scheme, out var headerValues))
        {
            Hmac = Hmac with 
            { 
                Scheme = scheme?.Name,
                HeaderValues = headerValues 
            };
        }
        else
        {
            throw new MissingHeaderException();
        }

        Request = request;
    }

    /// <summary>
    /// Creates a builder based on an incoming request and a partial hmac.
    /// </summary>
    /// <param name="request">The http request message for the basis of hmac computation.</param>
    /// <param name="hmac">The partial hmac to sign against.</param>
    public HmacBuilder(HttpRequestMessage request, HmacPartial hmac)
    {
        Hmac = new Hmac 
        {
            Policy = hmac.Policy,
            Scheme = hmac.Scheme,
            DateRequested = hmac.DateRequested,
            Nonce = hmac.Nonce,
            HeaderValues = hmac.HeaderValues
        };

        Request = request;
    }

    /// <summary>
    /// Creates an hmac based on previous constructor configuration.
    /// </summary>
    /// <param name="provider">The signature provider responsible for computations on signing content and signatures.</param>
    /// <returns>A constructed hmac.</returns>
    public async Task<Hmac> BuildAsync(IHmacSignatureProvider provider)
    {
        var signingContent = await provider.ComputeSigningContentAsync(Request, 
            Hmac.DateRequested, 
            Hmac.Nonce,
            Hmac.HeaderValues
        );

        if (string.IsNullOrWhiteSpace(signingContent))
        {
            throw new HmacSignatureComputationException("The signing content could not be determined.");
        }

        var signature = await provider.ComputeSignatureAsync(signingContent);

        if (string.IsNullOrWhiteSpace(signature))
        {
            throw new HmacSignatureComputationException($"The signature could not be computed for \"{signingContent}\".");
        }

        return Hmac with 
        { 
            SigningContent = signingContent, 
            Signature = signature
        };
    }
}