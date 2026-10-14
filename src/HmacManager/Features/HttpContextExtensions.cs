using Microsoft.AspNetCore.Http;

namespace HmacManager.Features;

/// <summary>
/// Provides extension methods for <see cref="HttpContext"/> to simplify retrieving HTTP request-related features.
/// </summary>
internal static class HttpContextExtensions
{
    /// <summary>
    /// Retrieves the <see cref="HttpRequestMessage"/> associated with the current <see cref="HttpContext"/>.
    /// If it doesn't exist, a new instance is created and added to the context's features.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> from which to retrieve the <see cref="HttpRequestMessage"/>.</param>
    /// <returns>The <see cref="HttpRequestMessage"/> associated with the <see cref="HttpContext"/>.</returns>
    internal static HttpRequestMessage GetHttpRequestMessage(this HttpContext httpContext)
    {
        var feature = httpContext.Features.Get<IHttpRequestMessageFeature>();
        if (feature == null)
        {
            feature = new HttpRequestMessageFeature(httpContext);
            httpContext.Features.Set(feature);
        }

        return feature.HttpRequestMessage;
    }
}