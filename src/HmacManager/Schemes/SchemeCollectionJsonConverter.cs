using System.Text.Json;
using System.Text.Json.Serialization;
using HmacManager.Extensions;
using HmacManager.Headers;

namespace HmacManager.Schemes;

/// <summary>
/// A custom JSON converter for serializing and deserializing <see cref="SchemeCollection"/>.
/// </summary>
public class SchemeCollectionJsonConverter : JsonConverter<SchemeCollection>
{
    /// <summary>
    /// Reads and deserializes a <see cref="SchemeCollection"/> from the provided JSON.
    /// </summary>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="typeToConvert">The target type to convert to.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> used during deserialization.</param>
    /// <returns>The deserialized <see cref="SchemeCollection"/>.</returns>
    public override SchemeCollection? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var schemeCollection = new SchemeCollection();
        if (reader.TokenType is JsonTokenType.StartArray)
        {
            string schemeName = default!;
            List<Header> headers = new();
            while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
            {
                if (reader.TokenType is JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();
                    
                    if (propertyName == nameof(Scheme.Name) && 
                        !reader.TryGetString(out schemeName))
                    {
                        // Error
                    } 
                    else if (propertyName == nameof(Scheme.Headers) &&
                        !reader.TryGetHeaders(out headers))
                    {
                        // Error
                    }
                }

                if (reader.TokenType is JsonTokenType.EndObject)
                {
                    var builder = new SchemeBuilder(schemeName);
                    foreach (var header in headers)
                    {
                        builder.AddHeader(header.Name!, header.ClaimType!);
                    }
                    
                    schemeCollection.Add(builder.Build());
                }
            }
        }
        else
        {
            // Error
        }

        return schemeCollection;
    }

    /// <summary>
    /// Writes a <see cref="SchemeCollection"/> to JSON.
    /// </summary>
    /// <param name="writer">The <see cref="Utf8JsonWriter"/> to write the JSON to.</param>
    /// <param name="value">The <see cref="SchemeCollection"/> to serialize.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> used during serialization.</param>
    public override void Write(Utf8JsonWriter writer, SchemeCollection value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var scheme in value.GetAll())
        {
            writer.WriteStartObject();
            writer.WriteString("Name", scheme.Name);
            writer.WritePropertyName("Headers");
            writer.WriteStartArray();
            foreach (var header in scheme.Headers.GetAll())
            {
                writer.WriteStartObject();
                writer.WriteString("Name", header.Name);
                writer.WriteString("ClaimType", header.ClaimType);
                writer.WriteEndObject();
            }
            
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}