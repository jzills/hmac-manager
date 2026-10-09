using HmacManager.Headers;

namespace HmacManager.Components;

/// <summary>
/// Defines the methods for building HMAC headers for authorization requests.
/// </summary>
public interface IHmacHeaderBuilder
{
    /// <summary>
    /// Adds an authorization signature to the HMAC headers.
    /// </summary>
    /// <param name="signature">The signature for authorization.</param>
    /// <returns>The <see cref="IHmacHeaderBuilder"/> instance with the authorization signature added.</returns>
    IHmacHeaderBuilder WithAuthorization(string signature);

    /// <summary>
    /// Adds a policy to the HMAC headers.
    /// </summary>
    /// <param name="policy">The policy to include in the headers.</param>
    /// <returns>The <see cref="IHmacHeaderBuilder"/> instance with the policy added.</returns>
    IHmacHeaderBuilder WithPolicy(string policy);

    /// <summary>
    /// Adds a scheme to the HMAC headers.
    /// </summary>
    /// <param name="scheme">The scheme to include in the headers.</param>
    /// <returns>The <see cref="IHmacHeaderBuilder"/> instance with the scheme added.</returns>
    IHmacHeaderBuilder WithScheme(string scheme);

    /// <summary>
    /// Adds a unique nonce to the HMAC headers for preventing replay attacks.
    /// </summary>
    /// <param name="nonce">A unique identifier for the request.</param>
    /// <returns>The <see cref="IHmacHeaderBuilder"/> instance with the nonce added.</returns>
    IHmacHeaderBuilder WithNonce(Guid nonce);

    /// <summary>
    /// Adds the date and time the request was made to the HMAC headers.
    /// </summary>
    /// <param name="dateRequested">The date and time of the request.</param>
    /// <returns>The <see cref="IHmacHeaderBuilder"/> instance with the date requested added.</returns>
    IHmacHeaderBuilder WithDateRequested(DateTimeOffset dateRequested);

    /// <summary>
    /// Creates and returns a new instance of an <see cref="IHmacHeaderBuilder"/> configured with the specified options and HMAC instance.
    /// </summary>
    /// <param name="options">The options for configuring the HMAC header builder.</param>
    /// <param name="hmac">The HMAC instance containing the cryptographic information for generating the headers.</param>
    /// <returns>An <see cref="IHmacHeaderBuilder"/> configured with the specified options and HMAC instance.</returns>
    IHmacHeaderBuilder CreateBuilder(HmacManagerOptions options, Hmac hmac);

    /// <summary>
    /// Builds and returns a read-only collection of header values representing the configured HMAC headers.
    /// </summary>
    /// <returns>A read-only collection of <see cref="HeaderValue"/> representing the HMAC headers.</returns>
    IReadOnlyCollection<HeaderValue> Build();
}