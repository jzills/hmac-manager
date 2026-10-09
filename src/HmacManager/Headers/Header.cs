namespace HmacManager.Headers;

/// <summary>
/// Represents a header with a name and an optional claim type.
/// </summary>
public record Header
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Header"/> record.
    /// </summary>
    /// <param name="name">The name of the header.</param>
    /// <param name="claimType">The claim type associated with the header. If null or whitespace, this will default to the value of <paramref name="name"/>.</param>
    public Header(string? name, string? claimType = null)
    {
        Name = name;
        ClaimType = string.IsNullOrWhiteSpace(claimType) ? name : claimType;
    }

    /// <summary>
    /// Gets the name of the header.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the claim type associated with the header. Defaults to <see cref="Name"/> if not specified.
    /// </summary>
    public string? ClaimType { get; init; }
}