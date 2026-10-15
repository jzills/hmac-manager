using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="HttpRequest"/> to facilitate content-related checks.
/// </summary>
internal static class HttpRequestExtensions
{
    /// <summary>
    /// Whether the <see cref="HttpRequest"/> may carry a body, decided without reading it.
    /// </summary>
    /// <param name="request">The <see cref="HttpRequest"/> to check.</param>
    /// <returns><c>false</c> only when the request certainly has no body; otherwise, <c>true</c>.</returns>
    /// <remarks>
    /// A body without a Content-Length (chunked, HTTP/2 without the header, streamed through a
    /// proxy) cannot be measured without consuming it, and Kestrel's request body is never
    /// seekable, so measuring it here would drain the only copy before buffering is enabled. It
    /// is reported as possible content instead: the caller buffers it, and computing the signing
    /// content measures what it actually holds, so an empty one still contributes no segment.
    /// </remarks>
    internal static bool MayHaveContent(this HttpRequest request)
    {
        if (request.Body is null || request.Body == Stream.Null)
        {
            return false;
        }

        if (request.ContentLength.HasValue)
        {
            return request.ContentLength > 0;
        }

        // Kestrel knows from the framing whether a body can follow at all, which keeps a GET
        // without Content-Length or Transfer-Encoding off the buffering path.
        return request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody ?? true;
    }
}
