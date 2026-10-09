using Microsoft.AspNetCore.Http;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// Provides extension methods for <see cref="HttpRequest"/> to facilitate content-related checks.
/// </summary>
internal static class HttpRequestExtensions
{
    /// <summary>
    /// Checks whether the <see cref="HttpRequest"/> has content.
    /// </summary>
    /// <param name="request">The <see cref="HttpRequest"/> to check for content.</param>
    /// <returns><c>true</c> if the request has content; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method checks if the request has a non-null body and if the content length is greater than zero.
    /// If the body can be sought, it checks the length directly. Otherwise, it reads the content into memory
    /// to determine the length of the body.
    /// </remarks>
    internal static bool HasContent(this HttpRequest request)
    {
        if (request.Body is not null)
        {
            if (request.ContentLength.HasValue && request.ContentLength > 0)
            {
                return true;
            }
            
            if (request.Body.CanSeek)
            {
                request.Body.Seek(0, SeekOrigin.Begin);
                return request.Body.Length > 0;
            }
            else
            {
                using var stream = new MemoryStream();
                request.Body.CopyToAsync(stream).Wait();
                return stream.Length > 0;
            }
        }
        else
        {
            return false;
        }
    }
}