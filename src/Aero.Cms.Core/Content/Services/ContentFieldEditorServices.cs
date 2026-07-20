using Aero.Cms.Abstractions.Content;
using Aero.Modular;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Supplies the built-in text editor and string normalization.
/// </summary>
public sealed class TextFieldEditor : IContentFieldEditor, IFieldEditor
{
    /// <inheritdoc />
    public string FieldType => "text";
    /// <inheritdoc />
    public string EditorComponent => "aero-textbox";
    /// <summary>Converts a non-null value to its string representation.</summary>
    /// <param name="value">The raw editor value.</param>
    /// <returns><see langword="null"/> for null input; otherwise <see cref="object.ToString"/> output.</returns>
    public object? Normalize(object? value) => value?.ToString();
}

/// <summary>
/// Supplies the built-in image picker and string normalization.
/// </summary>
public sealed class ImageFieldEditor : IContentFieldEditor, IFieldEditor
{
    /// <inheritdoc />
    public string FieldType => "image";
    /// <inheritdoc />
    public string EditorComponent => "aero-media-picker";
    /// <summary>Converts a non-null image value to its string representation.</summary>
    /// <param name="value">The raw editor value.</param>
    /// <returns><see langword="null"/> for null input; otherwise <see cref="object.ToString"/> output.</returns>
    public object? Normalize(object? value) => value?.ToString();
}

/// <summary>
/// Supplies the built-in rich-text editor and string normalization.
/// </summary>
public sealed class RichtextFieldEditor : IContentFieldEditor, IFieldEditor
{
    /// <inheritdoc />
    public string FieldType => "richtext";
    /// <inheritdoc />
    public string EditorComponent => "aero-richtext-editor";
    /// <summary>Converts a non-null rich-text value to its string representation.</summary>
    /// <param name="value">The raw editor value.</param>
    /// <returns><see langword="null"/> for null input; otherwise <see cref="object.ToString"/> output.</returns>
    public object? Normalize(object? value) => value?.ToString();
}

/// <summary>
/// Supplies the built-in number editor and decimal normalization.
/// </summary>
public sealed class NumberFieldEditor : IContentFieldEditor, IFieldEditor
{
    /// <inheritdoc />
    public string FieldType => "number";
    /// <inheritdoc />
    public string EditorComponent => "aero-numberbox";
    /// <summary>Normalizes a value to <see cref="decimal"/> using the current culture when possible.</summary>
    /// <param name="value">The raw editor value.</param>
    /// <returns>
    /// <see langword="null"/> for null input, the parsed decimal on success, or the original
    /// object unchanged when parsing fails.
    /// </returns>
    public object? Normalize(object? value)
    {
        if (value is null) return null;
        if (decimal.TryParse(value?.ToString(), out var d)) return d;
        return value;
    }
}

/// <summary>
/// Supplies the built-in Boolean editor and Boolean normalization.
/// </summary>
public sealed class BooleanFieldEditor : IContentFieldEditor, IFieldEditor
{
    /// <inheritdoc />
    public string FieldType => "boolean";
    /// <inheritdoc />
    public string EditorComponent => "aero-checkbox";
    /// <summary>Normalizes a Boolean value or a string accepted by <see cref="bool.TryParse(string?, out bool)"/>.</summary>
    /// <param name="value">The raw editor value.</param>
    /// <returns>The supplied Boolean or parsed value; <see langword="false"/> for null or unparseable input.</returns>
    public object? Normalize(object? value)
    {
        if (value is bool b) return b;
        if (bool.TryParse(value?.ToString(), out var parsed)) return parsed;
        return false;
    }
}

/// <summary>
/// Supplies the built-in URL editor and string normalization.
/// </summary>
public sealed class UrlFieldEditor : IContentFieldEditor, IFieldEditor
{
    /// <inheritdoc />
    public string FieldType => "url";
    /// <inheritdoc />
    public string EditorComponent => "aero-urlbox";
    /// <summary>Converts a non-null URL value to its string representation without validating it.</summary>
    /// <param name="value">The raw editor value.</param>
    /// <returns><see langword="null"/> for null input; otherwise <see cref="object.ToString"/> output.</returns>
    public object? Normalize(object? value) => value?.ToString();
}
