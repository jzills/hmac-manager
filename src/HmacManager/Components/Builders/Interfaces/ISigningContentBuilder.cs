namespace HmacManager.Components;

/// <summary>
/// Defines methods for creating and building signing content.
/// </summary>
public interface ISigningContentBuilder
{
    /// <summary>
    /// Creates a new instance of a signing content builder.
    /// </summary>
    /// <returns>A new <see cref="SigningContentBuilder"/> instance.</returns>
    SigningContentBuilder CreateBuilder();

    /// <summary>
    /// Builds and returns the signing content as a string.
    /// </summary>
    /// <returns>The signing content as a string.</returns>
    string Build();
}