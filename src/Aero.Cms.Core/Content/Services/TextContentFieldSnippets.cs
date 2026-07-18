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

        /// <summary>
    /// Accessor method.
    /// </summary>
public static string Accessor(string fieldName)
        => SafeName.IsMatch(fieldName)
            ? "fields." + fieldName
            : "fields[\"" + fieldName + "\"]";
}

internal sealed class TextFieldSnippet : IFieldTemplateSnippet
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "text";
        /// <summary>
    /// Render method.
    /// </summary>
public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "<div class=\"aero-field aero-field-text\">{{" + a + "}}</div>";
    }
}

internal sealed class ImageFieldSnippet : IFieldTemplateSnippet
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "image";
        /// <summary>
    /// Render method.
    /// </summary>
public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + a + "}}<div class=\"aero-field aero-field-image\"><img src=\"{{" + a + "}}\" alt=\"\" /></div>{{end}}";
    }
}

internal sealed class RichtextFieldSnippet : IFieldTemplateSnippet
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "richtext";
        /// <summary>
    /// Render method.
    /// </summary>
public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "<div class=\"aero-field aero-field-richtext\">{{" + a + "}}</div>";
    }
}

internal sealed class UrlFieldSnippet : IFieldTemplateSnippet
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "url";
        /// <summary>
    /// Render method.
    /// </summary>
public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + a + "}}<div class=\"aero-field aero-field-url\"><a href=\"{{" + a + "}}\">{{" + (field.Label ?? field.Name) + "}}</a></div>{{end}}";
    }
}

internal sealed class NumberFieldSnippet : IFieldTemplateSnippet
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "number";
        /// <summary>
    /// Render method.
    /// </summary>
public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "<div class=\"aero-field aero-field-number\">{{" + a + "}}</div>";
    }
}

internal sealed class BooleanFieldSnippet : IFieldTemplateSnippet
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "boolean";
        /// <summary>
    /// Render method.
    /// </summary>
public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + a + "}}<div class=\"aero-field aero-field-boolean\">\u2713 " + (field.Label ?? field.Name) + "</div>{{end}}";
    }
}
