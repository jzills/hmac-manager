using HmacManager.Headers;

namespace HmacManager.Schemes;

/// <summary>
/// Represents an authentication or authorization scheme, typically defined by a collection of headers and a name.
/// </summary>
public class Scheme
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Scheme"/> class with the specified name.
    /// </summary>
    /// <param name="name">The name of the scheme, which cannot be null or empty.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
    public Scheme(string? name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        Name = name;
    }

    /// <summary>
    /// Gets the name of the scheme.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets a collection of headers associated with this scheme.
    /// </summary>
    public HeaderCollection Headers = new();
}