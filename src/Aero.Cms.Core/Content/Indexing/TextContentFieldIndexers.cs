using System.Text.Json;
using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Content.Indexing;

/// <summary>
/// Emits a text field's JSON string value as one search token.
/// </summary>
public sealed class TextFieldIndexer : IContentFieldIndexer
{
    /// <inheritdoc />
    public string FieldType => "text";

    /// <inheritdoc />
    /// <remarks>Non-string JSON values emit no tokens. A JSON empty string emits one empty token.</remarks>
    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            yield return value.GetString() ?? "";
    }
}

/// <summary>Emits a URL field's stored string value as one search token.</summary>
public sealed class UrlFieldIndexer : IContentFieldIndexer
{
    public string FieldType => ContentFieldTypes.Url;

    public IEnumerable<string> GetIndexTokens(
        ContentFieldDefinition field,
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            yield return value.GetString() ?? string.Empty;
    }
}

/// <summary>
/// Emits a rich-text JSON string after removing substrings that resemble HTML tags.
/// </summary>
/// <remarks>
/// Tag removal uses the regular expression <c>&lt;[^&gt;]*&gt;</c>. It is token extraction,
/// not HTML parsing or sanitization, and does not decode HTML entities.
/// </remarks>
public sealed class RichTextFieldIndexer : IContentFieldIndexer
{
    /// <inheritdoc />
    /// <remarks>Non-string JSON values emit no tokens; string values emit exactly one token.</remarks>
    public string FieldType => "richtext";

    /// <inheritdoc />
    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = Regex.Replace(value.GetString() ?? "", "<[^>]*>", "");
            yield return text;
        }
    }
}

/// <summary>
/// Emits scalar, array, and typed CMS reference values as search tokens.
/// </summary>
/// <remarks>
/// Typed CMS references use a source-qualified token such as
/// <c>pages:1530221140281556994</c> so identifiers from different source
/// collections cannot collide.
/// </remarks>
public sealed class ReferenceFieldIndexer : IContentFieldIndexer
{
    /// <inheritdoc />
    public string FieldType => "reference";

    /// <inheritdoc />
    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            yield return value.GetString() ?? "";
            yield break;
        }

        if (value.ValueKind == JsonValueKind.Object
            && IsCmsDocumentReference(field)
            && value.TryGetProperty("source", out var sourceElement)
            && sourceElement.ValueKind == JsonValueKind.String
            && value.TryGetProperty("id", out var idElement)
            && idElement.ValueKind == JsonValueKind.String
            && long.TryParse(
                idElement.GetString(),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var id)
            && id > 0)
        {
            var source = sourceElement.GetString()?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(source))
                yield return $"{source}:{id.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            yield break;
        }

        if (value.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var reference in value.EnumerateArray())
        {
            if (reference.ValueKind == JsonValueKind.String)
                yield return reference.GetString() ?? string.Empty;
        }
    }

    private static bool IsCmsDocumentReference(ContentFieldDefinition field) =>
        field.Settings.TryGetValue(
            ReferenceContentFieldSettings.TargetKind,
            out var targetKind)
        && targetKind.ValueKind == JsonValueKind.String
        && string.Equals(
            targetKind.GetString(),
            ReferenceContentFieldSettings.TargetKindCmsDocument,
            StringComparison.Ordinal);
}

/// <summary>
/// Emits a decimal JSON number formatted using the invariant culture.
/// </summary>
public sealed class NumberFieldIndexer : IContentFieldIndexer
{
    /// <inheritdoc />
    /// <remarks>Numbers not representable as <see cref="decimal"/> and non-number values emit no tokens.</remarks>
    public string FieldType => "number";

    /// <inheritdoc />
    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var num))
            yield return num.ToString(
                "G29",
                System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>Indexes a bounded integer range value.</summary>
public sealed class RangeFieldIndexer : IContentFieldIndexer
{
    /// <inheritdoc />
    public string FieldType => ContentFieldTypes.Range;

    /// <inheritdoc />
    public IEnumerable<string> GetIndexTokens(
        ContentFieldDefinition field,
        JsonElement value)
    {
        if (value.TryGetInt32(out var number))
        {
            yield return number.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}

/// <summary>Indexes a stored hexadecimal color value.</summary>
public sealed class ColorFieldIndexer : IContentFieldIndexer
{
    /// <inheritdoc />
    public string FieldType => ContentFieldTypes.Color;

    /// <inheritdoc />
    public IEnumerable<string> GetIndexTokens(
        ContentFieldDefinition field,
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            yield return value.GetString()!;
        }
    }
}

/// <summary>
/// Emits the lowercase invariant token <c>true</c> or <c>false</c> for a JSON Boolean.
/// </summary>
public sealed class BooleanFieldIndexer : IContentFieldIndexer
{
    /// <inheritdoc />
    /// <remarks>Non-Boolean JSON values emit no tokens.</remarks>
    public string FieldType => "boolean";

    /// <inheritdoc />
    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.True) yield return "true";
        else if (value.ValueKind == JsonValueKind.False) yield return "false";
    }
}

/// <summary>Indexes the scalar values in a bounded list field.</summary>
public sealed class ListFieldIndexer : IContentFieldIndexer
{
    public string FieldType => ContentFieldTypes.List;

    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) yield break;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
                yield return item.GetString() ?? string.Empty;
            else if (item.ValueKind == JsonValueKind.Number)
                yield return item.GetRawText();
        }
    }
}

/// <summary>Indexes scalar values, but not keys, in a bounded dictionary field.</summary>
public sealed class DictionaryFieldIndexer : IContentFieldIndexer
{
    public string FieldType => ContentFieldTypes.Dictionary;

    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) yield break;
        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
                yield return property.Value.GetString() ?? string.Empty;
            else if (property.Value.ValueKind == JsonValueKind.Number)
                yield return property.Value.GetRawText();
        }
    }
}
