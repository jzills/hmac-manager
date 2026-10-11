namespace HmacManager.Components;

/// <summary>
/// Provides a signing content builder that performs validation on required context properties before building the signing content.
/// </summary>
public class SigningContentBuilderValidated : SigningContentBuilder
{
    /// <summary>
    /// Creates a new instance of <see cref="SigningContentBuilderValidated"/>.
    /// </summary>
    /// <returns>A new <see cref="SigningContentBuilderValidated"/> instance.</returns>
    public override SigningContentBuilder CreateBuilder() => new SigningContentBuilderValidated();

    /// <summary>
    /// Builds and returns the signing content after validating that all required context properties are set.
    /// </summary>
    /// <returns>The signing content as a string.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if any of the required context properties (<see cref="SigningContentContext.Nonce"/>, <see cref="SigningContentContext.Request"/>,
    /// <see cref="SigningContentContext.PublicKey"/>, or <see cref="SigningContentContext.DateRequested"/>) is <c>null</c>.
    /// </exception>
    public override string Build()
    {
        ArgumentNullException.ThrowIfNull(Context.Nonce, nameof(Context.Nonce));
        ArgumentNullException.ThrowIfNull(Context.Request, nameof(Context.Request));
        ArgumentNullException.ThrowIfNull(Context.PublicKey, nameof(Context.PublicKey));
        ArgumentNullException.ThrowIfNull(Context.DateRequested, nameof(Context.DateRequested));

        return base.Build();
    }
}