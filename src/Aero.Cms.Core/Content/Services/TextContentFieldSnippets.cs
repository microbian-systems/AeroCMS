using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Core.Content.Services;

internal sealed class TextFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "text";
    public string Render(ContentFieldDefinition field)
    {
        return new StringBuilder()
            .Append("<div class=\"aero-field aero-field-text\">{{ block.").Append(field.Name).Append(" }}</div>")
            .ToString();
    }
}

internal sealed class ImageFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "image";
    public string Render(ContentFieldDefinition field)
    {
        return new StringBuilder()
            .Append("{{ if block.").Append(field.Name).Append(" }}")
            .Append("<div class=\"aero-field aero-field-image\"><img src=\"{{ block.").Append(field.Name).Append("\" alt=\"\" /></div>")
            .Append("{{ end }}")
            .ToString();
    }
}

internal sealed class RichtextFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "richtext";
    public string Render(ContentFieldDefinition field)
    {
        return new StringBuilder()
            .Append("<div class=\"aero-field aero-field-richtext\">{{ block.").Append(field.Name).Append(" }}</div>")
            .ToString();
    }
}

internal sealed class UrlFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "url";
    public string Render(ContentFieldDefinition field)
    {
        return new StringBuilder()
            .Append("{{ if block.").Append(field.Name).Append(" }}")
            .Append("<div class=\"aero-field aero-field-url\"><a href=\"{{ block.").Append(field.Name).Append("\">{{ ")
            .Append(field.Label ?? field.Name).Append(" }}</a></div>")
            .Append("{{ end }}")
            .ToString();
    }
}

internal sealed class NumberFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "number";
    public string Render(ContentFieldDefinition field)
    {
        return new StringBuilder()
            .Append("<div class=\"aero-field aero-field-number\">{{ block.").Append(field.Name).Append(" }}</div>")
            .ToString();
    }
}

internal sealed class BooleanFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "boolean";
    public string Render(ContentFieldDefinition field)
    {
        return new StringBuilder()
            .Append("{{ if block.").Append(field.Name).Append(" }}")
            .Append("<div class=\"aero-field aero-field-boolean\">✓ ")
            .Append(field.Label ?? field.Name).Append("</div>")
            .Append("{{ end }}")
            .ToString();
    }
}
