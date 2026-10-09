using System.Text.Json;
using HmacManager.Headers;

namespace HmacManager.Extensions;

/// <summary>
/// Provides extension methods for <see cref="Utf8JsonReader"/> to simplify working with JSON data.
/// </summary>
internal static class Utf8JsonReaderExtensions
{
    /// <summary>
    /// Attempts to get a string value from the current position of the <see cref="Utf8JsonReader"/>.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="value">The string value extracted from the JSON.</param>
    /// <returns><c>true</c> if a valid string was extracted; otherwise, <c>false</c>.</returns>
    public static bool TryGetString(this ref Utf8JsonReader reader, out string value)
    {
        value = reader.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Attempts to parse and retrieve a list of headers from the JSON.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="values">The list of parsed headers.</param>
    /// <returns><c>true</c> if the headers were successfully parsed; otherwise, <c>false</c>.</returns>
    public static bool TryGetHeaders(this ref Utf8JsonReader reader, out List<Header> values)
    {
        values = new List<Header>();

        if (reader.TokenType is JsonTokenType.StartArray)
        {
            while (reader.Read())
            {
                if (TryGetHeader(ref reader, out var value))
                {
                    values.Add(value);
                }

                if (reader.TokenType is JsonTokenType.EndArray)
                {
                    reader.Read();
                    break;
                }
            }

            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to parse a single header from the current JSON token.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="value">The parsed <see cref="Header"/>.</param>
    /// <returns><c>true</c> if the header was successfully parsed; otherwise, <c>false</c>.</returns>
    public static bool TryGetHeader(this ref Utf8JsonReader reader, out Header value)
    {
        string? name = null;
        string? claimType = null;

        if (reader.TokenType is JsonTokenType.StartObject)
        {
            while (reader.Read())
            {
                if (reader.TokenType is JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    if (propertyName == nameof(Header.Name))
                    {
                        name = reader.GetString() ?? string.Empty;
                    }
                    else if (propertyName == nameof(Header.ClaimType))
                    {
                        claimType = reader.GetString() ?? string.Empty;
                    }
                }
                else if (reader.TokenType is not JsonTokenType.EndObject)
                {
                    break;
                }
            }

            value = new Header(name, claimType);
            return true;
        }
        else
        {
            value = new Header(name, claimType);
            return false;
        }
    }
}