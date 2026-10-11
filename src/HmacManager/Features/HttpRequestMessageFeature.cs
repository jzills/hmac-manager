using Microsoft.AspNetCore.Http;
using HmacManager.Mvc.Extensions.Internal;

namespace HmacManager.Features;

/// <summary>
/// Implements the <see cref="IHttpRequestMessageFeature"/> interface, providing access to an <see cref="HttpRequestMessage"/> 
/// in the context of an HTTP request.
/// </summary>
internal class HttpRequestMessageFeature : IHttpRequestMessageFeature
{
    private readonly HttpContext _httpContext;
    private HttpRequestMessage? _httpRequestMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRequestMessageFeature"/> class.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> associated with the HTTP request.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="httpContext"/> is <c>null</c>.</exception>
    public HttpRequestMessageFeature(HttpContext httpContext)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }

        _httpContext = httpContext;
    }

    /// <summary>
    /// Gets or sets the <see cref="HttpRequestMessage"/> associated with the HTTP context.
    /// </summary>
    /// <remarks>
    /// If the <see cref="HttpRequestMessage"/> has not been created yet, it will be lazily created the first time this property is accessed.
    /// </remarks>
    public HttpRequestMessage HttpRequestMessage
    {
        get
        {
            if (_httpRequestMessage == null)
            {
                _httpRequestMessage = CreateHttpRequestMessage(_httpContext);
            }

            return _httpRequestMessage;
        }

        set => _httpRequestMessage = value;
    }

    /// <summary>
    /// Creates an <see cref="HttpRequestMessage"/> from the given <see cref="HttpContext"/>.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> to create the <see cref="HttpRequestMessage"/> from.</param>
    /// <returns>A new <see cref="HttpRequestMessage"/>.</returns>
    private static HttpRequestMessage CreateHttpRequestMessage(HttpContext httpContext)
    {
        var httpRequest = httpContext.Request;
        var uriString =
            httpRequest.Scheme + "://" +
            httpRequest.Host +
            httpRequest.PathBase +
            httpRequest.Path +
            httpRequest.QueryString;

        var message = new HttpRequestMessage(new HttpMethod(httpRequest.Method), uriString);
        message.Options.TryAdd(nameof(HttpContext), httpContext);
        
        if (httpRequest.HasContent())
        {
            message.Content = new StreamContent(httpRequest.Body);
        }

        foreach (var header in httpRequest.Headers)
        {
            if (!message.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value))
            {
                message.Content?.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value);
            }
        }

        return message;
    }
}