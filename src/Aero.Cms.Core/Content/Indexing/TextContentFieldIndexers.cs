using System.Text.Json;
using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Content.Indexing;

/// <summary>
/// Extracts plain text from a text field for search indexing.
/// </summary>
public sealed class TextFieldIndexer : IContentFieldIndexer
{
    /// <inheritdoc />
    public string FieldType => "text";

    /// <inheritdoc />
    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            yield return value.GetString() ?? "";
    }
}

/// <summary>
/// Extracts plain text from a richtext field for search indexing, stripping HTML tags.
/// </summary>
public sealed class RichTextFieldIndexer : IContentFieldIndexer
{
    /// <inheritdoc />
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
/// Extracts the raw ID value from a reference field for cross-reference indexing.
/// </summary>
public sealed class ReferenceFieldIndexer : IContentFieldIndexer
{
    /// <inheritdoc />
    public string FieldType => "reference";

    /// <inheritdoc />
    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            yield return value.GetString() ?? "";
    }
}

/// <summary>
/// Extracts a numeric value as text for search indexing.
/// </summary>
public sealed class NumberFieldIndexer : IContentFieldIndexer
{
    /// <inheritdoc />
    public string FieldType => "number";

    /// <inheritdoc />
    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var num))
            yield return num.ToString();
    }
}

/// <summary>
/// Extracts the boolean value as text for search indexing.
/// </summary>
public sealed class BooleanFieldIndexer : IContentFieldIndexer
{
    /// <inheritdoc />
    public string FieldType => "boolean";

    /// <inheritdoc />
    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.True) yield return "true";
        else if (value.ValueKind == JsonValueKind.False) yield return "false";
    }
}
