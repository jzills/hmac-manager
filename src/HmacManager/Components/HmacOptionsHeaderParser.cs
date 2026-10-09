using System.Net.Http.Headers;
using System.Text;
using HmacManager.Exceptions;
using HmacManager.Mvc;

namespace HmacManager.Components;

/// <inheritdoc/>
public class HmacOptionsHeaderParser : HmacHeaderParser
{
    /// <summary>
    /// A static dictionary mapping header names to their respective prefix values.
    /// Used for parsing and identifying headers within a collection of split header strings.
    /// </summary>
    protected static readonly IDictionary<string, string> HeaderMappings = new Dictionary<string, string>
    {
        { HmacAuthenticationDefaults.Headers.Authorization, $"{HmacAuthenticationDefaults.Headers.Authorization}=" },
        { HmacAuthenticationDefaults.Headers.Policy, $"{HmacAuthenticationDefaults.Headers.Policy}=" },
        { HmacAuthenticationDefaults.Headers.Scheme, $"{HmacAuthenticationDefaults.Headers.Scheme}=" },
        { HmacAuthenticationDefaults.Headers.Nonce, $"{HmacAuthenticationDefaults.Headers.Nonce}=" },
        { HmacAuthenticationDefaults.Headers.DateRequested, $"{HmacAuthenticationDefaults.Headers.DateRequested}=" }
    };

    /// <inheritdoc/>
    public HmacOptionsHeaderParser() : base()
    {
    }

    /// <inheritdoc/>
    public HmacOptionsHeaderParser(IDictionary<string, string> headers) : base(Process(headers))
    {
    }

    /// <inheritdoc/>
    public override IHmacHeaderParser CreateParser(IDictionary<string, string> headers) => new HmacOptionsHeaderParser(headers);

    /// <inheritdoc/>
    public override IHmacHeaderParser CreateParser(HttpRequestHeaders headers) => 
        new HmacOptionsHeaderParser(headers.ToDictionary(
            header => header.Key, 
            header => header.Value.FirstOrDefault() ?? string.Empty
        ));

    private static IDictionary<string, string> Process(IDictionary<string, string> headers)
    {
        if (headers.TryGetValue(HmacAuthenticationDefaults.Headers.Options, out var options))
        {
            if (!string.IsNullOrWhiteSpace(options))
            {
                var headerOptionsValueFormatted = DecodeBase64(options);
                var headerOptionsValues = ToDictionary(headerOptionsValueFormatted);
                return headerOptionsValues;
            }
            else
            {
                throw new BadHeaderFormatException();
            }
        }
        else
        {
            throw new MissingHeaderException();
        }
    }

    private static string DecodeBase64(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            throw new BadHeaderFormatException();
        }
    }

    private static IDictionary<string, string> ToDictionary(string value)
    {
        var splitValues = value.Split("&");
        if (splitValues.Length > 0)
        {
            var result = new Dictionary<string, string>();
            foreach (var splitValue in splitValues)
            {
                foreach (var header in HeaderMappings)
                {
                    if (splitValue.StartsWith(header.Value))
                    {
                        result[header.Key] = splitValue.Substring(header.Value.Length);
                        break;
                    }
                }
            }

            return result;
        }
        else
        {
            throw new BadHeaderFormatException();
        }
    }
}