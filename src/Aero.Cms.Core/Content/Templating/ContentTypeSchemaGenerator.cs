using System.Text.Json;
using System.Globalization;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Services;

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
    /// Number, Range, and Boolean fields map to their corresponding JSON types; bounded
    /// collection fields map to arrays or objects; reference fields map to <c>integer</c>;
    /// every other field type maps to <c>string</c>. The current reference
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
            writer.WriteString("type", MapFieldType(field));
            writer.WriteString("title", field.Label ?? field.Name);
            WriteFieldConstraints(writer, field);
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

    private static string MapFieldType(ContentFieldDefinition field)
    {
        if (field.FieldType == ContentFieldTypes.Reference
            && (ReferenceFieldValidator.IsCmsDocumentReference(field)
                || ReferenceFieldValidator.IsContentEntryReference(field)))
        {
            return "object";
        }

        return field.FieldType switch
        {
            "number" => "number",
            "range" => "integer",
            "boolean" => "boolean",
            "list" or "gallery" => "array",
            "dictionary" => "object",
            _ => "string"
        };
    }

    private static void WriteFieldConstraints(Utf8JsonWriter writer, ContentFieldDefinition field)
    {
        if (field.FieldType == ContentFieldTypes.Range)
        {
            WriteIntegerSetting(
                writer,
                "minimum",
                field,
                RangeContentFieldSettings.Start);
            WriteIntegerSetting(
                writer,
                "maximum",
                field,
                RangeContentFieldSettings.End);
        }
        else if (field.FieldType == ContentFieldTypes.Color)
        {
            writer.WriteString(
                "pattern",
                "^#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?$");
        }
        else if (field.FieldType == ContentFieldTypes.List)
        {
            var itemType = GetStringSetting(field, CompositeContentFieldSettings.ItemType);
            writer.WriteStartObject("items");
            writer.WriteString("type", itemType == CompositeContentFieldSettings.Number ? "number" : "string");
            WriteAllowedValues(writer, field, itemType);
            writer.WriteEndObject();
            writer.WriteBoolean("uniqueItems", true);
            WriteEffectiveMinimum(
                writer,
                "minItems",
                "maxItems",
                field,
                CompositeContentFieldSettings.MinimumItems);
            WriteIntegerSetting(writer, "maxItems", field, CompositeContentFieldSettings.MaximumItems);
        }
        else if (field.FieldType == ContentFieldTypes.Gallery)
        {
            writer.WriteStartObject("items");
            writer.WriteString("type", "string");
            writer.WriteEndObject();
            WriteEffectiveMinimum(
                writer,
                "minItems",
                "maxItems",
                field,
                CompositeContentFieldSettings.MinimumItems);
            WriteIntegerSetting(writer, "maxItems", field, CompositeContentFieldSettings.MaximumItems);
        }
        else if (field.FieldType == ContentFieldTypes.Dictionary)
        {
            var valueType = GetStringSetting(field, CompositeContentFieldSettings.ValueType);
            writer.WriteStartObject("additionalProperties");
            writer.WriteString("type", valueType == CompositeContentFieldSettings.Number ? "number" : "string");
            writer.WriteEndObject();
            WriteEffectiveMinimum(
                writer,
                "minProperties",
                "maxProperties",
                field,
                CompositeContentFieldSettings.MinimumEntries);
            WriteIntegerSetting(writer, "maxProperties", field, CompositeContentFieldSettings.MaximumEntries);
        }
    }

    private static string? GetStringSetting(ContentFieldDefinition field, string key) =>
        field.Settings.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void WriteIntegerSetting(Utf8JsonWriter writer, string schemaName, ContentFieldDefinition field, string settingName)
    {
        if (field.Settings.TryGetValue(settingName, out var value) && value.TryGetInt32(out var number))
        {
            writer.WriteNumber(schemaName, number);
        }
    }

    private static void WriteEffectiveMinimum(
        Utf8JsonWriter writer,
        string schemaName,
        string emptyMaximumSchemaName,
        ContentFieldDefinition field,
        string settingName)
    {
        var minimum = field.Settings.TryGetValue(settingName, out var value)
            && value.TryGetInt32(out var configured)
                ? configured
                : 0;
        if (field.Required)
        {
            writer.WriteNumber(schemaName, Math.Max(1, minimum));
            return;
        }

        writer.WriteNumber(schemaName, 0);
        if (minimum <= 0)
        {
            return;
        }

        // Optional composites may be empty, but once used they must honor the
        // configured minimum. The alternatives preserve that runtime contract
        // in generated JSON Schema rather than weakening it to minItems: 0.
        writer.WriteStartArray("anyOf");
        writer.WriteStartObject();
        writer.WriteNumber(emptyMaximumSchemaName, 0);
        writer.WriteEndObject();
        writer.WriteStartObject();
        writer.WriteNumber(schemaName, minimum);
        writer.WriteEndObject();
        writer.WriteEndArray();
    }

    private static void WriteAllowedValues(Utf8JsonWriter writer, ContentFieldDefinition field, string? itemType)
    {
        if (!field.Settings.TryGetValue(CompositeContentFieldSettings.AllowedValues, out var allowed)
            || allowed.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        writer.WriteStartArray("enum");
        foreach (var value in allowed.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String))
        {
            var text = value.GetString() ?? string.Empty;
            if (itemType == CompositeContentFieldSettings.Number
                && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                writer.WriteNumberValue(number);
            else
                writer.WriteStringValue(text);
        }
        writer.WriteEndArray();
    }
}
