namespace HmacManager.Components;

/// <summary>
/// Factory for creating <see cref="IHmacHeaderParser"/> instances based on the consolidated headers setting.
/// </summary>
public class HmacHeaderParserFactory : IHmacHeaderParserFactory
{
    /// <summary>
    /// Gets a value indicating whether consolidated headers are enabled.
    /// </summary>
    protected readonly bool IsConsolidatedHeadersEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacHeaderParserFactory"/> class.
    /// </summary>
    /// <param name="isConsolidatedHeadersEnabled">Indicates whether consolidated headers are enabled.</param>
    public HmacHeaderParserFactory(bool isConsolidatedHeadersEnabled) =>
        IsConsolidatedHeadersEnabled = isConsolidatedHeadersEnabled;

    /// <inheritdoc/>
    public IHmacHeaderParser Create() =>
        IsConsolidatedHeadersEnabled
            ? new HmacOptionsHeaderParser()
            : new HmacHeaderParser();

    /// <inheritdoc/>
    public IHmacHeaderParser Create(IDictionary<string, string> headers) =>
        IsConsolidatedHeadersEnabled
            ? new HmacOptionsHeaderParser(headers)
            : new HmacHeaderParser(headers);
}