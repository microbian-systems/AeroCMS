namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Defines the editor metadata for a content field type.
/// Provides the Blazor editor component name and value normalization
/// for a specific field type (e.g. "text", "image", "reference").
/// </summary>
public interface IContentFieldEditor
{
    /// <summary>The field type alias this editor handles (e.g. "text", "image", "reference").</summary>
    string FieldType { get; }

    /// <summary>The Blazor component name used in the admin UI (e.g. "aero-textbox").</summary>
    string EditorComponent { get; }

    /// <summary>Normalizes a raw editor value before storage.</summary>
    object? Normalize(object? value);
}
