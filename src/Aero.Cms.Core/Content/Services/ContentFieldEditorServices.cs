using Aero.Cms.Abstractions.Content;
using Aero.Modular;

namespace Aero.Cms.Core.Content.Services;

public sealed class TextFieldEditor : IContentFieldEditor, IFieldEditor
{
    public string FieldType => "text";
    public string EditorComponent => "aero-textbox";
    public object? Normalize(object? value) => value?.ToString();
}

public sealed class ImageFieldEditor : IContentFieldEditor, IFieldEditor
{
    public string FieldType => "image";
    public string EditorComponent => "aero-media-picker";
    public object? Normalize(object? value) => value?.ToString();
}

public sealed class RichtextFieldEditor : IContentFieldEditor, IFieldEditor
{
    public string FieldType => "richtext";
    public string EditorComponent => "aero-richtext-editor";
    public object? Normalize(object? value) => value?.ToString();
}

public sealed class NumberFieldEditor : IContentFieldEditor, IFieldEditor
{
    public string FieldType => "number";
    public string EditorComponent => "aero-numberbox";
    public object? Normalize(object? value)
    {
        if (value is null) return null;
        if (decimal.TryParse(value?.ToString(), out var d)) return d;
        return value;
    }
}

public sealed class BooleanFieldEditor : IContentFieldEditor, IFieldEditor
{
    public string FieldType => "boolean";
    public string EditorComponent => "aero-checkbox";
    public object? Normalize(object? value)
    {
        if (value is bool b) return b;
        if (bool.TryParse(value?.ToString(), out var parsed)) return parsed;
        return false;
    }
}

public sealed class UrlFieldEditor : IContentFieldEditor, IFieldEditor
{
    public string FieldType => "url";
    public string EditorComponent => "aero-urlbox";
    public object? Normalize(object? value) => value?.ToString();
}
