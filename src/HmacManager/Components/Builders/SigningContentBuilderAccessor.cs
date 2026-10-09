namespace HmacManager.Components;

/// <summary>
/// Provides an accessor for building signing content with a specified function.
/// </summary>
public class SigningContentBuilderAccessor : SigningContentBuilder
{
    /// <summary>
    /// Gets the function used to access the signing content from the context.
    /// </summary>
    protected readonly Func<SigningContentContext, string> SigningContentAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SigningContentBuilderAccessor"/> class with a specified signing content accessor function.
    /// </summary>
    /// <param name="signingContentAccessor">The function that generates signing content from a <see cref="SigningContentContext"/>.</param>
    public SigningContentBuilderAccessor(
        Func<SigningContentContext, string> signingContentAccessor
    ) => SigningContentAccessor = signingContentAccessor;

    /// <summary>
    /// Creates a new instance of <see cref="SigningContentBuilderAccessor"/> with the current signing content accessor function.
    /// </summary>
    /// <returns>A new <see cref="SigningContentBuilderAccessor"/> instance.</returns>
    public override SigningContentBuilder CreateBuilder() => new SigningContentBuilderAccessor(SigningContentAccessor);

    /// <summary>
    /// Builds and returns the signing content by using the specified signing content accessor function.
    /// </summary>
    /// <returns>The signing content as a string.</returns>
    /// <exception cref="ArgumentException">Thrown if the generated signing content is null or whitespace.</exception>
    public override string Build()
    {
        var signingContent = SigningContentAccessor(Context);

        ArgumentException.ThrowIfNullOrWhiteSpace(signingContent);

        return signingContent;
    }
}