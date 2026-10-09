using HmacManager.Schemes;

namespace HmacManager.Components;

/// <summary>
/// Configuration options for <see cref="HmacManager"/>.
/// </summary>
public class HmacManagerOptions
{
    /// <summary>
    /// Gets the policy.
    /// </summary>
    public readonly string Policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacManagerOptions"/> class with the specified policy.
    /// </summary>
    /// <param name="policy">The policy to associate with <see cref="HmacManager"/>.</param>
    public HmacManagerOptions(string policy) => Policy = policy;

    /// <summary>
    /// Gets or sets the header scheme for authentication.
    /// </summary>
    public Scheme? Scheme { get; init; }

    /// <summary>
    /// Gets or sets the maximum allowable age of the signature in seconds.
    /// Default value is 30 seconds.
    /// </summary>
    public int MaxAgeInSeconds { get; init; } = 30;

    /// <summary>
    /// Gets or sets the <see cref="IHmacHeaderBuilder"/> used to build headers.
    /// </summary>
    public required IHmacHeaderBuilder HeaderBuilder { get; init; }

    /// <summary>
    /// Gets or sets the <see cref="IHmacHeaderParser"/> used to build headers.
    /// </summary>
    public required IHmacHeaderParser HeaderParser { get; init; }
}