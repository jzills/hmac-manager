using Microsoft.AspNetCore.Http;

namespace HmacManager.Mvc;

/// <summary>
/// Defines methods for providing an HMAC authentication context based on HTTP headers.
/// </summary>
public interface IHmacAuthenticationContextProvider
{
    /// <summary>
    /// Attempts to retrieve an HMAC authentication context from the given dictionary of headers.
    /// </summary>
    /// <param name="headers">A dictionary of HTTP headers containing authentication data.</param>
    /// <param name="context">The retrieved <see cref="HmacAuthenticationContext"/> if successful, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the context was successfully retrieved; otherwise, <see langword="false"/>.</returns>
    bool TryGetAuthenticationContext(IDictionary<string, string> headers, out HmacAuthenticationContext context);
    
    /// <summary>
    /// Attempts to retrieve an HMAC authentication context from the given collection of headers.
    /// </summary>
    /// <param name="headers">A collection of HTTP headers containing authentication data.</param>
    /// <param name="context">The retrieved <see cref="HmacAuthenticationContext"/> if successful, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the context was successfully retrieved; otherwise, <see langword="false"/>.</returns>
    bool TryGetAuthenticationContext(IHeaderDictionary headers, out HmacAuthenticationContext context);
}