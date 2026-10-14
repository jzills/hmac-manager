namespace HmacManager.Components;

/// <summary>
/// Factory interface for creating instances of <see cref="IHmacHeaderBuilder"/>.
/// </summary>
public interface IHmacHeaderBuilderFactory
{
    /// <summary>
    /// Creates an <see cref="IHmacHeaderBuilder"/>.
    /// </summary>
    /// <returns>An <see cref="IHmacHeaderBuilder"/> instance.</returns>
    IHmacHeaderBuilder Create();
}