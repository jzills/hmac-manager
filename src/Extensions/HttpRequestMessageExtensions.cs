namespace HmacManager.Extensions;

/// <summary>
/// Provides extension methods for <see cref="HttpRequestMessage"/>.
/// </summary>
internal static class HttpRequestMessageExtensions
{
    /// <summary>
    /// Determines whether the <see cref="HttpRequestMessage"/> has content.
    /// </summary>
    /// <param name="request">The <see cref="HttpRequestMessage"/> to check.</param>
    /// <returns><c>true</c> if the request has content; otherwise, <c>false</c>.</returns>
    internal static bool HasContent(this HttpRequestMessage request)
    {
        if (request.Content is null) return false;

        // ContentLength is not the same as Headers.Contains("Content-Length"): for content
        // like StringContent, the length is known and computable via the property even
        // before the header collection has materialized the "Content-Length" entry. Reading
        // it here (rather than gating on Contains) is what makes an explicitly empty body
        // report false instead of falling through to the streamed-content default of true.
        return request.Content.Headers.ContentLength != 0;
    }
}