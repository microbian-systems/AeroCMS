using Aero.Cms.Abstractions.Content;
using Aero.Modular;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Represents a class for TextFieldEditor.
/// </summary>
public sealed class TextFieldEditor : IContentFieldEditor, IFieldEditor
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "text";
        /// <summary>
    /// Gets or sets the Editor Component.
    /// </summary>
public string EditorComponent => "aero-textbox";
        /// <summary>
    /// Normalize method.
    /// </summary>
public object? Normalize(object? value) => value?.ToString();
}

/// <summary>
/// Represents a class for ImageFieldEditor.
/// </summary>
public sealed class ImageFieldEditor : IContentFieldEditor, IFieldEditor
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "image";
        /// <summary>
    /// Gets or sets the Editor Component.
    /// </summary>
public string EditorComponent => "aero-media-picker";
        /// <summary>
    /// Normalize method.
    /// </summary>
public object? Normalize(object? value) => value?.ToString();
}

/// <summary>
/// Represents a class for RichtextFieldEditor.
/// </summary>
public sealed class RichtextFieldEditor : IContentFieldEditor, IFieldEditor
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "richtext";
        /// <summary>
    /// Gets or sets the Editor Component.
    /// </summary>
public string EditorComponent => "aero-richtext-editor";
        /// <summary>
    /// Normalize method.
    /// </summary>
public object? Normalize(object? value) => value?.ToString();
}

/// <summary>
/// Represents a class for NumberFieldEditor.
/// </summary>
public sealed class NumberFieldEditor : IContentFieldEditor, IFieldEditor
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "number";
        /// <summary>
    /// Gets or sets the Editor Component.
    /// </summary>
public string EditorComponent => "aero-numberbox";
        /// <summary>
    /// Normalize method.
    /// </summary>
public object? Normalize(object? value)
    {
        if (value is null) return null;
        if (decimal.TryParse(value?.ToString(), out var d)) return d;
        return value;
    }
}

/// <summary>
/// Represents a class for BooleanFieldEditor.
/// </summary>
public sealed class BooleanFieldEditor : IContentFieldEditor, IFieldEditor
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "boolean";
        /// <summary>
    /// Gets or sets the Editor Component.
    /// </summary>
public string EditorComponent => "aero-checkbox";
        /// <summary>
    /// Normalize method.
    /// </summary>
public object? Normalize(object? value)
    {
        if (value is bool b) return b;
        if (bool.TryParse(value?.ToString(), out var parsed)) return parsed;
        return false;
    }
}

/// <summary>
/// Represents a class for UrlFieldEditor.
/// </summary>
public sealed class UrlFieldEditor : IContentFieldEditor, IFieldEditor
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
public string FieldType => "url";
        /// <summary>
    /// Gets or sets the Editor Component.
    /// </summary>
public string EditorComponent => "aero-urlbox";
        /// <summary>
    /// Normalize method.
    /// </summary>
public object? Normalize(object? value) => value?.ToString();
}
