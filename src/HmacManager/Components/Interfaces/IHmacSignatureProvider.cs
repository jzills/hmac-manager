using HmacManager.Headers;

namespace HmacManager.Components;

/// <summary>
/// An interface representing an <see cref="IHmacSignatureProvider"/>, which
/// handles the computation of hashes and signatures.
/// </summary>
public interface IHmacSignatureProvider
{
    /// <summary>
    /// Computes a signature against a specified signing content.
    /// </summary>
    /// <param name="signingContent">A string representing the constructed content to be signed.</param>
    /// <returns>A string representing the computed signature.</returns>
    Task<string> ComputeSignatureAsync(string signingContent);

    /// <summary>
    /// Computes signing content for the incoming request.
    /// </summary>
    /// <param name="request">The http request to use for the basis of computation.</param>
    /// <param name="dateRequested">The date requested.</param>
    /// <param name="nonce">The nonce tied to this request.</param>
    /// <param name="headerValues">The header values tied to this request.</param>
    /// <returns>A string representing the computed signing content.</returns>
    Task<string> ComputeSigningContentAsync(
        HttpRequestMessage request, 
        DateTimeOffset dateRequested, 
        Guid nonce,
        IReadOnlyCollection<HeaderValue>? headerValues = null
    );
}
