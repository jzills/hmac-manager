namespace HmacManager.Headers;

/// <summary>
/// Represents a value associated with a header, including an optional claim type and value.
/// Inherits from <see cref="Header"/>.
/// </summary>
public record HeaderValue(string Name, string? ClaimType, string? Value) : Header(Name, ClaimType)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderValue"/> record with the specified name and value,
    /// and a <see langword="null"/> claim type.
    /// </summary>
    /// <param name="name">The name of the header.</param>
    /// <param name="value">The value of the header.</param>
    public HeaderValue(string name, string? value) 
        : this(name, null, value)
    { 
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderValue"/> record from a <see cref="KeyValuePair{TKey, TValue}"/>,
    /// with the key representing the name and the value representing the value of the header. The claim type is set to <see langword="null"/>.
    /// </summary>
    /// <param name="keyValuePair">The key-value pair representing the header's name and value.</param>
    public HeaderValue(KeyValuePair<string, string?> keyValuePair) 
        : this(keyValuePair.Key, null, keyValuePair.Value)
    {
    }
}