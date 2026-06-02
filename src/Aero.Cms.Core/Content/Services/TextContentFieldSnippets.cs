using System.Text;
using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Helper that emits a Scriban-safe field accessor.
/// Uses bracket notation for field names that contain hyphens, dots, or other
/// characters that are invalid in Scriban identifiers.
/// </summary>
internal static class ScribanFieldHelper
{
    private static readonly Regex SafeName = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    public static string Accessor(string fieldName)
        => SafeName.IsMatch(fieldName)
            ? "block." + fieldName
            : "block[\"" + fieldName + "\"]";
}

internal sealed class TextFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "text";
    public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "<div class=\"aero-field aero-field-text\">{{" + a + "}}</div>";
    }
}

internal sealed class ImageFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "image";
    public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + a + "}}<div class=\"aero-field aero-field-image\"><img src=\"{{" + a + "}}\" alt=\"\" /></div>{{end}}";
    }
}

internal sealed class RichtextFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "richtext";
    public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "<div class=\"aero-field aero-field-richtext\">{{" + a + "}}</div>";
    }
}

internal sealed class UrlFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "url";
    public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + a + "}}<div class=\"aero-field aero-field-url\"><a href=\"{{" + a + "}}\">{{" + (field.Label ?? field.Name) + "}}</a></div>{{end}}";
    }
}

internal sealed class NumberFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "number";
    public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "<div class=\"aero-field aero-field-number\">{{" + a + "}}</div>";
    }
}

internal sealed class BooleanFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "boolean";
    public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + a + "}}<div class=\"aero-field aero-field-boolean\">\u2713 " + (field.Label ?? field.Name) + "</div>{{end}}";
    }
}
