using System.Text.Json;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Generates the shallow JSON schema consumed by Scriban data validation.
/// </summary>
public static class ContentTypeSchemaGenerator
{
    /// <summary>
    /// Generates top-level property types and required-field names from a content type.
    /// </summary>
    /// <param name="definition">The content type definition to project.</param>
    /// <returns>A new <see cref="JsonDocument"/> owned by the caller.</returns>
    /// <remarks>
    /// Number and Boolean fields map to their corresponding JSON types; reference fields map
    /// to <c>integer</c>; every other field type maps to <c>string</c>. The current reference
    /// field validators store identifiers as JSON strings, so generated reference schemas do
    /// not match that storage shape. Field names and labels are written through
    /// <see cref="Utf8JsonWriter"/> and are JSON-escaped.
    /// </remarks>
    public static JsonDocument GenerateSchema(ContentTypeDefinition definition)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("type", "object");
        writer.WriteStartObject("properties");

        foreach (var field in definition.Fields)
        {
            writer.WriteStartObject(field.Name);
            writer.WriteString("type", MapFieldType(field.FieldType));
            writer.WriteString("title", field.Label ?? field.Name);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();

        var requiredFields = definition.Fields
            .Where(field => field.Required)
            .Select(field => field.Name)
            .ToList();
        if (requiredFields.Count > 0)
        {
            writer.WriteStartArray("required");
            foreach (var fieldName in requiredFields)
                writer.WriteStringValue(fieldName);
            writer.WriteEndArray();
        }

        writer.WriteBoolean("additionalProperties", false);
        writer.WriteEndObject();
        writer.Flush();

        stream.Position = 0;
        return JsonDocument.Parse(stream);
    }

    private static string MapFieldType(string ft) => ft switch
    {
        "number" => "number", "boolean" => "boolean",
        "reference" => "integer",
        _ => "string"
    };
}
