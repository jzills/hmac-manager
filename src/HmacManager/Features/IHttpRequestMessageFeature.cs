namespace HmacManager.Features;

/// <summary>
/// Defines a feature for accessing the <see cref="HttpRequestMessage"/> in the context of an HTTP request.
/// </summary>
public interface IHttpRequestMessageFeature
{
    /// <summary>
    /// Gets or sets the <see cref="HttpRequestMessage"/> associated with the HTTP request.
    /// </summary>
    HttpRequestMessage HttpRequestMessage { get; set; }
}