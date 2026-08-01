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
    /// Returns dotted access for Scriban-safe identifiers and bracket access otherwise.
    /// </summary>
    /// <param name="fieldName">The content field name.</param>
    /// <returns>An accessor rooted at the <c>fields</c> scope.</returns>
    public static string Accessor(string fieldName)
        => SafeName.IsMatch(fieldName)
            ? "fields." + fieldName
            : "fields[\"" + fieldName + "\"]";
}

/// <summary>Generates a text-field wrapper containing a Scriban field expression.</summary>
internal sealed class TextFieldSnippet : IFieldTemplateSnippet
{
    /// <inheritdoc />
    public string FieldType => "text";
    /// <inheritdoc />
    public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "<div class=\"aero-field aero-field-text\">{{" + a + "}}</div>";
    }
}

/// <summary>Generates a conditional image element with an empty alternative-text attribute.</summary>
internal sealed class ImageFieldSnippet : IFieldTemplateSnippet
{
    /// <inheritdoc />
    public string FieldType => "image";
    /// <inheritdoc />
    public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + a + "}}<div class=\"aero-field aero-field-image\"><img src=\"{{" + a + "}}\" alt=\"\" /></div>{{end}}";
    }
}

/// <summary>Generates a rich-text wrapper containing a Scriban field expression.</summary>
internal sealed class RichtextFieldSnippet : IFieldTemplateSnippet
{
    /// <inheritdoc />
    public string FieldType => "richtext";
    /// <inheritdoc />
    public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "<div class=\"aero-field aero-field-richtext\">{{" + a + "}}</div>";
    }
}

/// <summary>Generates a conditional link whose target is the field value.</summary>
internal sealed class UrlFieldSnippet : IFieldTemplateSnippet
{
    /// <inheritdoc />
    public string FieldType => "url";
    /// <inheritdoc />
    public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + a + "}}<div class=\"aero-field aero-field-url\"><a href=\"{{" + a + "}}\">{{" + (field.Label ?? field.Name) + "}}</a></div>{{end}}";
    }
}

/// <summary>Generates a number-field wrapper containing a Scriban field expression.</summary>
internal sealed class NumberFieldSnippet : IFieldTemplateSnippet
{
    /// <inheritdoc />
    public string FieldType => "number";
    /// <inheritdoc />
    public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "<div class=\"aero-field aero-field-number\">{{" + a + "}}</div>";
    }
}

/// <summary>Generates a bounded integer-field wrapper.</summary>
internal sealed class RangeFieldSnippet : IFieldTemplateSnippet
{
    /// <inheritdoc />
    public string FieldType => ContentFieldTypes.Range;

    /// <inheritdoc />
    public string Render(ContentFieldDefinition field)
    {
        var accessor = ScribanFieldHelper.Accessor(field.Name);
        return "<div class=\"aero-field aero-field-range\">{{" + accessor + "}}</div>";
    }
}

/// <summary>Generates a hexadecimal color-field wrapper.</summary>
internal sealed class ColorFieldSnippet : IFieldTemplateSnippet
{
    /// <inheritdoc />
    public string FieldType => ContentFieldTypes.Color;

    /// <inheritdoc />
    public string Render(ContentFieldDefinition field)
    {
        var accessor = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + accessor + "}}<div class=\"aero-field aero-field-color\">{{" + accessor + "}}</div>{{end}}";
    }
}

/// <summary>Generates a labeled marker that is emitted only when the field value is truthy.</summary>
internal sealed class BooleanFieldSnippet : IFieldTemplateSnippet
{
    /// <inheritdoc />
    public string FieldType => "boolean";
    /// <inheritdoc />
    public string Render(ContentFieldDefinition field)
    {
        var a = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + a + "}}<div class=\"aero-field aero-field-boolean\">\u2713 " + (field.Label ?? field.Name) + "</div>{{end}}";
    }
}

/// <summary>Generates an unordered list for a bounded list field.</summary>
internal sealed class ListFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => ContentFieldTypes.List;

    public string Render(ContentFieldDefinition field)
    {
        var accessor = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + accessor + "}}<ul class=\"aero-field aero-field-list\">{{for value in " + accessor + "}}<li>{{value}}</li>{{end}}</ul>{{end}}";
    }
}

/// <summary>Generates an ordered group of images for a gallery field.</summary>
internal sealed class GalleryFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => ContentFieldTypes.Gallery;

    public string Render(ContentFieldDefinition field)
    {
        var accessor = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + accessor + "}}<div class=\"aero-field aero-field-gallery\">{{for image in " + accessor + "}}<img src=\"{{image}}\" alt=\"\" />{{end}}</div>{{end}}";
    }
}

/// <summary>Generates a definition list for a bounded key/value field.</summary>
internal sealed class DictionaryFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => ContentFieldTypes.Dictionary;

    public string Render(ContentFieldDefinition field)
    {
        var accessor = ScribanFieldHelper.Accessor(field.Name);
        return "{{if " + accessor + "}}<dl class=\"aero-field aero-field-dictionary\">{{for key in " + accessor + " | object.keys}}<dt>{{key}}</dt><dd>{{" + accessor + "[key]}}</dd>{{end}}</dl>{{end}}";
    }
}
