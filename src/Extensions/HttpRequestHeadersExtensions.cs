using System.Net.Http.Headers;
using HmacManager.Headers;
using HmacManager.Schemes;

namespace HmacManager.Extensions;

/// <summary>
/// Provides extension methods for <see cref="HttpRequestHeaders"/>.
/// </summary>
internal static class HttpRequestHeadersExtensions
{
    /// <summary>
    /// Adds a collection of headers to the <see cref="HttpRequestHeaders"/>.
    /// </summary>
    /// <param name="source">The <see cref="HttpRequestHeaders"/> to which the headers will be added.</param>
    /// <param name="headers">The collection of <see cref="HeaderValue"/> to be added to the headers.</param>
    internal static void AddRange(this HttpRequestHeaders source, IReadOnlyCollection<HeaderValue> headers)
    {
        foreach (var header in headers)
        {
#pragma warning disable CS8604
            source.Add(header.Name, header.Value);
#pragma warning restore CS8604
        }
    }

    /// <summary>
    /// Tries to parse the headers based on the provided <see cref="Scheme"/>.
    /// </summary>
    /// <param name="headers">The <see cref="HttpRequestHeaders"/> to be parsed.</param>
    /// <param name="scheme">The <see cref="Scheme"/> used to define which headers to look for.</param>
    /// <param name="headerValues">The parsed <see cref="IReadOnlyCollection{HeaderValue}"/> containing the header values, if successful.</param>
    /// <returns><c>true</c> if the headers were successfully parsed according to the scheme; otherwise, <c>false</c>.</returns>
    internal static bool TryParseHeaders(
        this HttpRequestHeaders headers,
        Scheme? scheme,
        out IReadOnlyCollection<HeaderValue> headerValues
    )
    {
        if (scheme is null)
        {
            headerValues = new List<HeaderValue>().AsReadOnly();
            return true;
        }
        else
        {
            var schemeHeaders = scheme.Headers.GetAll();
            var schemeHeaderValues = new List<HeaderValue>(schemeHeaders.Count);
            foreach (var schemeHeader in schemeHeaders)
            {
#pragma warning disable CS8604
                if (headers.TryGetValues(schemeHeader.Name, out var values))
#pragma warning restore CS8604
                {
                    // Trimmed so this agrees with a Fetch Headers value, which the
                    // Headers class normalizes on the way in per specification. Without
                    // this, a value signed with surrounding whitespace here would not
                    // match what a compliant parser on the other end reconstructs.
                    var schemeHeaderValue = values.First().Trim();
                    schemeHeaderValues.Add(new HeaderValue(
                        schemeHeader.Name,
                        schemeHeader.ClaimType,
                        schemeHeaderValue
                    ));
                }
                else
                {
                    headerValues = new List<HeaderValue>().AsReadOnly();
                    return false;
                }
            }

            headerValues = schemeHeaderValues.AsReadOnly();
            return true;
        }
    }
}