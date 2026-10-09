namespace HmacManager.Components;

/// <summary>
/// Factory for creating <see cref="IHmacHeaderBuilder"/> instances based on the consolidated headers setting.
/// </summary>
public class HmacHeaderBuilderFactory : IHmacHeaderBuilderFactory
{
    /// <summary>
    /// Gets a value indicating whether consolidated headers are enabled.
    /// </summary>
    protected readonly bool IsConsolidatedHeadersEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacHeaderBuilderFactory"/> class.
    /// </summary>
    /// <param name="isConsolidatedHeadersEnabled">Indicates whether consolidated headers are enabled.</param>
    public HmacHeaderBuilderFactory(bool isConsolidatedHeadersEnabled) =>
        IsConsolidatedHeadersEnabled = isConsolidatedHeadersEnabled;

    /// <inheritdoc/>
    public IHmacHeaderBuilder Create() =>
        IsConsolidatedHeadersEnabled ? 
            new HmacOptionsHeaderBuilder() : 
            new HmacHeaderBuilder();
}