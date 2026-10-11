using HmacManager.Headers;

namespace HmacManager.Schemes;

/// <summary>
/// Provides a builder for creating and configuring a <see cref="Scheme"/> with a specified name and required headers.
/// </summary>
public class SchemeBuilder
{
    /// <summary>
    /// Holds the collection of headers required for the scheme.
    /// </summary>
    protected HeaderCollection Headers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemeBuilder"/> class with a specified scheme name.
    /// </summary>
    /// <param name="name">The name of the scheme.</param>
    public SchemeBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        Name = name;
    }

    /// <summary>
    /// Gets or sets the name of the scheme being built.
    /// </summary>
    internal string Name { get; init; }

    /// <summary>
    /// Adds a specified header name to the scheme, making it a required header for successful authentication.
    /// The header value will be used directly as its own claim type.
    /// </summary>
    /// <param name="name">The name of the header on the HTTP request.</param>
    public void AddHeader(string name) => AddHeader(name, name);

    /// <summary>
    /// Adds a specified header to the scheme, associating it with a particular claim type.
    /// This header will be required for successful authentication.
    /// </summary>
    /// <param name="name">The name of the header on the HTTP request.</param>
    /// <param name="claimType">The claim type to which the header value should be mapped.</param>
    public void AddHeader(string name, string claimType) => Headers.Add(new Header(name, claimType));

    /// <summary>
    /// Builds and returns a new <see cref="Scheme"/> object based on the configured scheme name and headers.
    /// </summary>
    /// <returns>A configured <see cref="Scheme"/> instance.</returns>
    internal Scheme Build() => new Scheme(Name) { Headers = Headers };
}