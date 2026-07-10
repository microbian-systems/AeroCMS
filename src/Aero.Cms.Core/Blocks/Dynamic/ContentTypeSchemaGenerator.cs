using System.Text.Json;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Blocks.Dynamic;

/// <summary>
/// Represents a class for ContentTypeSchemaGenerator.
/// </summary>
public static class ContentTypeSchemaGenerator
{
        /// <summary>
    /// GenerateSchema method.
    /// </summary>
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
