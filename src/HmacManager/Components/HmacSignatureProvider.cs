using HmacManager.Extensions;
using HmacManager.Headers;
using HmacManager.Mvc.Extensions.Internal;

namespace HmacManager.Components;

/// <summary>
/// Provides functionality to compute HMAC signatures and signing content for HTTP requests.
/// </summary>
public class HmacSignatureProvider : IHmacSignatureProvider
{
    /// <summary>
    /// Options used to configure the HMAC signature provider.
    /// </summary>
    protected readonly HmacSignatureProviderOptions Options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacSignatureProvider"/> class with the specified options.
    /// </summary>
    /// <param name="options">The options for configuring the HMAC signature provider.</param>
    public HmacSignatureProvider(HmacSignatureProviderOptions options) => Options = options;

    /// <summary>
    /// Computes the HMAC signature for the provided signing content.
    /// </summary>
    /// <param name="signingContent">The content to sign.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the computed HMAC signature as a string.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="signingContent"/> is null or whitespace.</exception>
    public Task<string> ComputeSignatureAsync(string signingContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingContent);
        
        return Options.SignatureHashGenerator.HashAsync(signingContent);
    }

    /// <summary>
    /// Computes the signing content for an HTTP request, including headers and content hash if applicable.
    /// </summary>
    /// <param name="request">The HTTP request message.</param>
    /// <param name="dateRequested">The date and time the request was made.</param>
    /// <param name="nonce">A unique nonce to include in the signing content.</param>
    /// <param name="headerValues">Optional collection of header values to include in the signing content.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the computed signing content as a string.</returns>
    public async Task<string> ComputeSigningContentAsync(
        HttpRequestMessage request, 
        DateTimeOffset dateRequested, 
        Guid nonce,
        IReadOnlyCollection<HeaderValue>? headerValues = null
    )
    {
        var builder = Options.SigningContentBuilder.CreateBuilder()
            .WithRequest(request)
            .WithPublicKey(Options.Keys.PublicKey)
            .WithDateRequested(dateRequested)
            .WithNonce(nonce)
            .WithHeaderValues(headerValues ?? []);

        if (request.HasContent())
        {
            var contentString = await request.Content!.ReadAsStringAsync();
            var contentHash = await Options.ContentHashGenerator.HashAsync(contentString);
            builder.WithContentHash(contentHash);
        }

        return builder.Build();
    }
}