namespace HmacManager.Components;

/// <summary>
/// Factory interface for creating instances of <see cref="IHmacHeaderParser"/>.
/// </summary>
public interface IHmacHeaderParserFactory
{
    /// <summary>
    /// Creates an <see cref="IHmacHeaderParser"/>.
    /// </summary>
    /// <returns>An <see cref="IHmacHeaderParser"/> instance.</returns>
    IHmacHeaderParser Create();

    /// <summary>
    /// Creates an <see cref="IHmacHeaderParser"/> instance using the specified headers.
    /// </summary>
    /// <param name="headers">A dictionary of headers to parse.</param>
    /// <returns>An <see cref="IHmacHeaderParser"/> instance.</returns>
    IHmacHeaderParser Create(IDictionary<string, string> headers);
}