using System.Collections;
using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Content.Views;

namespace Aero.Cms.Core.Content.Views;

/// <summary>Rejects executor output that does not conform to the registered code-owned shape.</summary>
public static class ContentShapeRowValidator
{
    public static bool TryValidateRows(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, ContentShapeDefinition shape, out string? error)
    {
        foreach (var row in rows)
            if (!TryValidateObject(row, shape.Fields, out error)) return false;
        error = null;
        return true;
    }

    private static bool TryValidateObject(IReadOnlyDictionary<string, object?> values, IReadOnlyList<ContentShapeField> fields, out string? error)
    {
        var allowed = fields.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        if (values.Keys.Any(key => !allowed.Contains(key)))
        {
            error = "The executor returned a field that is not declared by the content shape.";
            return false;
        }
        foreach (var field in fields)
        {
            if (!values.TryGetValue(field.Name, out var value) || value is null)
            {
                if (field.Required) { error = $"Required field '{field.Name}' is missing."; return false; }
                continue;
            }
            if (!TryValidateValue(value, field, out error)) return false;
        }
        error = null;
        return true;
    }

    private static bool TryValidateValue(object value, ContentShapeField field, out string? error)
    {
        error = null;
        bool valid;
        switch (field.Type)
        {
            case ContentShapeFieldType.String: valid = value is string or JsonElement { ValueKind: JsonValueKind.String }; break;
            case ContentShapeFieldType.Number: valid = IsNumber(value); break;
            case ContentShapeFieldType.Boolean: valid = value is bool || value is JsonElement { ValueKind: JsonValueKind.True or JsonValueKind.False }; break;
            case ContentShapeFieldType.DateTime: valid = value is DateTime or DateTimeOffset || value is JsonElement { ValueKind: JsonValueKind.String } element && element.TryGetDateTimeOffset(out _); break;
            case ContentShapeFieldType.Json: valid = true; break;
            case ContentShapeFieldType.ContentEntryKey: valid = IsEntryKey(value); break;
            case ContentShapeFieldType.ContentEntrySource: valid = value is ContentEntrySource { IsValid: true }; break;
            case ContentShapeFieldType.Reference: valid = IsEntryKey(value); break;
            case ContentShapeFieldType.Object: valid = TryAsObject(value, out var objectValue) && TryValidateObject(objectValue!, field.Fields!, out error); break;
            case ContentShapeFieldType.List: valid = TryValidateList(value, field.Item!, out error); break;
            default: valid = false; break;
        }
        if (!valid && error is null) error = $"Field '{field.Name}' does not match its declared {field.Type} shape.";
        return valid;
    }

    private static bool TryValidateList(object value, ContentShapeField item, out string? error)
    {
        if (value is JsonElement { ValueKind: JsonValueKind.Array } json)
        {
            foreach (var jsonItem in json.EnumerateArray())
            {
                if (!TryValidateValue(FromJson(jsonItem)!, item, out error)) return false;
            }
            error = null;
            return true;
        }
        if (value is string || value is not IEnumerable enumerable) { error = "Expected a list value."; return false; }
        foreach (var itemValue in enumerable)
        {
            if (itemValue is null) { error = "List values cannot contain null."; return false; }
            if (!TryValidateValue(itemValue, item, out error)) return false;
        }
        error = null;
        return true;
    }

    private static bool TryAsObject(object value, out IReadOnlyDictionary<string, object?>? objectValue)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnly) { objectValue = readOnly; return true; }
        if (value is IDictionary<string, object?> mutable) { objectValue = new Dictionary<string, object?>(mutable, StringComparer.Ordinal); return true; }
        if (value is JsonElement { ValueKind: JsonValueKind.Object } json)
        {
            objectValue = json.EnumerateObject().ToDictionary(property => property.Name, property => FromJson(property.Value), StringComparer.Ordinal);
            return true;
        }
        objectValue = null;
        return false;
    }

    private static object? FromJson(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(), JsonValueKind.True => true, JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
        JsonValueKind.Object or JsonValueKind.Array => value,
        JsonValueKind.Null => null, _ => value
    };

    private static bool IsEntryKey(object value)
    {
        if (value is ContentEntryKey { IsValid: true }) return true;
        if (value is not JsonElement { ValueKind: JsonValueKind.Object } json) return false;
        return json.TryGetProperty("provider", out var provider)
            && provider.ValueKind == JsonValueKind.String
            && json.TryGetProperty("stableId", out var stableId)
            && stableId.ValueKind == JsonValueKind.String
            && new ContentEntryKey(provider.GetString()!, stableId.GetString()!).IsValid;
    }

    private static bool IsNumber(object value) => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
        || value is JsonElement { ValueKind: JsonValueKind.Number };
}
