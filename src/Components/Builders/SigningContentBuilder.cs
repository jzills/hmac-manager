using System.Text;
using HmacManager.Exceptions;
using HmacManager.Headers;

namespace HmacManager.Components;

/// <summary>
/// Provides methods to build and sign content for a request using specific parameters.
/// </summary>
public class SigningContentBuilder : ISigningContentBuilder
{
    /// <summary>
    /// Gets the <see cref="StringBuilder"/> used to construct the signing content.
    /// </summary>
    protected readonly StringBuilder Builder;

    /// <summary>
    /// Gets the context containing request-related information used in building the signing content.
    /// </summary>
    protected readonly SigningContentContext Context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SigningContentBuilder"/> class.
    /// </summary>
    internal SigningContentBuilder()
    {
        Context = new();
        Builder = new StringBuilder();
    }

    /// <summary>
    /// Sets the HTTP request to be used in the signing content.
    /// </summary>
    /// <param name="request">The HTTP request message.</param>
    /// <returns>The <see cref="SigningContentBuilder"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <c>null</c>.</exception>
    internal SigningContentBuilder WithRequest(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        Context.Request = request;
        return this;
    }

    /// <summary>
    /// Sets the public key to be used in the signing content.
    /// </summary>
    /// <param name="publicKey">The public key as a <see cref="Guid"/>.</param>
    /// <returns>The <see cref="SigningContentBuilder"/> instance.</returns>
    internal SigningContentBuilder WithPublicKey(Guid publicKey)
    {
        Context.PublicKey = publicKey;
        return this;
    }

    /// <summary>
    /// Sets the date requested to be used in the signing content.
    /// </summary>
    /// <param name="dateRequested">The date the request was made as a <see cref="DateTimeOffset"/>.</param>
    /// <returns>The <see cref="SigningContentBuilder"/> instance.</returns>
    internal SigningContentBuilder WithDateRequested(DateTimeOffset dateRequested)
    {
        Context.DateRequested = dateRequested;
        return this;
    }

    /// <summary>
    /// Sets the nonce value to be used in the signing content.
    /// </summary>
    /// <param name="nonce">The nonce as a <see cref="Guid"/>.</param>
    /// <returns>The <see cref="SigningContentBuilder"/> instance.</returns>
    internal SigningContentBuilder WithNonce(Guid nonce)
    {
        Context.Nonce = nonce;
        return this;
    }

    /// <summary>
    /// Sets the header values to be included in the signing content.
    /// </summary>
    /// <param name="headerValues">The header values as a collection of <see cref="HeaderValue"/>.</param>
    /// <returns>The <see cref="SigningContentBuilder"/> instance.</returns>
    internal SigningContentBuilder WithHeaderValues(IReadOnlyCollection<HeaderValue> headerValues)
    {
        ArgumentNullException.ThrowIfNull(headerValues, nameof(headerValues));
        
        Context.HeaderValues = headerValues;
        return this;
    }

    /// <summary>
    /// Sets the content hash to be used in the signing content.
    /// </summary>
    /// <param name="contentHash">The content hash as a string.</param>
    /// <returns>The <see cref="SigningContentBuilder"/> instance.</returns>
    internal SigningContentBuilder WithContentHash(string? contentHash)
    {
        Context.ContentHash = contentHash;
        return this;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="SigningContentBuilder"/>.
    /// </summary>
    /// <returns>A new <see cref="SigningContentBuilder"/> instance.</returns>
    public virtual SigningContentBuilder CreateBuilder() => new SigningContentBuilder();

    /// <summary>
    /// Builds and returns the signing content as a string.
    /// </summary>
    /// <returns>The signing content as a string.</returns>
    public virtual string Build()
    {
        ArgumentNullException.ThrowIfNull(Context.Request, nameof(Context.Request));

        Builder.Append($"{Context.Request.Method}");

        if (Context.Request.RequestUri is not null)
        {
            // The problem above can be replicated when signing with HmacManager directly,
            // then proceeding to call Send/SendAsync on an HttpClient. When signing manually, without
            // using the HmacDelegatingHandler, if a relative URI is specified on the HttpRequestMessage,
            // then this builder only has access to that relative URI. However, on the recieving end,
            // the authentication handler will rebuild the signing content with the absolute URI because
            // it has been sent with the BaseAddress specified on the HttpClient. This causes a 401 response
            // because the signatures won't match.

            // Method #1 - Fallback to looking at the host header, if that header isn't present,
            // throw an exception forcing users to manually add the host.

            // Method #2 - Throw an exception immediately if the RequestUri.IsAbsoluteUri is false

            // This can be configurable during Program.cs, something like RequireAbsoluteUri().
            if (Context.Request.RequestUri.IsAbsoluteUri)
            {
                Builder.Append($":{Context.Request.RequestUri.PathAndQuery}");

                // IdnHost rather than Authority: Authority renders an internationalized
                // host as the Unicode it was given, while IdnHost is the punycode form --
                // the one that actually travels in the Host header. The WHATWG URL parser
                // the TypeScript client uses always applies IDNA, so URL.host is punycode;
                // signing Authority here disagreed with every request to an IDN host.
                var requestUri = Context.Request.RequestUri;
                var authority = requestUri.IsDefaultPort
                    ? requestUri.IdnHost
                    : $"{requestUri.IdnHost}:{requestUri.Port}";
                Builder.Append($":{authority}");
            }
            else
            {
                throw new AbsoluteUriException(
                    "The RequestUri must be absolute when signing requests. " +
                    "Please ensure the HttpRequestMessage has an absolute URI before signing.");
            }
        }

        Builder.Append($":{Context.DateRequested?.ToUnixTimeMilliseconds()}");
        Builder.Append($":{Context.PublicKey}");

        if (Context.ContentHash is not null)
        {
            Builder.Append($":{Context.ContentHash}");
        }

        if (Context.HeaderValues.Any())
        {
            Builder.Append(":");
            Builder.AppendJoin(":", Context.HeaderValues.Select(element => element.Value));
        }

        Builder.Append($":{Context.Nonce}");
        return Builder.ToString();
    }
}