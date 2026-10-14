using System.Net.Http.Headers;

namespace HmacManager.Components;

/// <summary>
/// Interface for parsing HMAC headers.
/// </summary>
public interface IHmacHeaderParser
{
    /// <summary>
    /// Retrieves the authorization header value.
    /// </summary>
    /// <returns>A string representing the authorization header.</returns>
    string GetAuthorization();

    /// <summary>
    /// Retrieves the policy associated with the HMAC.
    /// </summary>
    /// <returns>A string representing the policy.</returns>
    string GetPolicy();

    /// <summary>
    /// Retrieves the scheme associated with the HMAC.
    /// </summary>
    /// <returns>A string representing the scheme, or null if not specified.</returns>
    string? GetScheme();

    /// <summary>
    /// Retrieves the nonce used for HMAC validation.
    /// </summary>
    /// <returns>A <see cref="Guid"/> representing the nonce.</returns>
    Guid GetNonce();

    /// <summary>
    /// Retrieves the date and time the HMAC was requested.
    /// </summary>
    /// <returns>A <see cref="DateTimeOffset"/> representing the date requested.</returns>
    DateTimeOffset GetDateRequested();

    /// <summary>
    /// Creates and returns a new instance of an <see cref="IHmacHeaderParser"/> attached to the specified headers.
    /// </summary>
    /// <param name="headers">The incoming HTTP request headers.</param>
    /// <returns>An <see cref="IHmacHeaderParser"/> with the specified headers.</returns>
    IHmacHeaderParser CreateParser(IDictionary<string, string> headers);

    /// <summary>
    /// Creates and returns a new instance of an <see cref="IHmacHeaderParser"/> attached to the specified headers.
    /// </summary>
    /// <param name="headers">The incoming HTTP request headers.</param>
    /// <returns>An <see cref="IHmacHeaderParser"/> with the specified headers.</returns>
    IHmacHeaderParser CreateParser(HttpRequestHeaders headers);

    /// <summary>
    /// Parses the HMAC headers and returns an instance of <see cref="HmacPartial"/>.
    /// </summary>
    /// <param name="signature">Output parameter that will contain the signature value.</param>
    /// <returns>An instance of <see cref="HmacPartial"/> representing the parsed HMAC data.</returns>
    HmacPartial Parse(out string signature);
}