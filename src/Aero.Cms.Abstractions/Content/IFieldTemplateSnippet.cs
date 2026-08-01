namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Defines an interface for IFieldTemplateSnippet.
/// </summary>
public interface IFieldTemplateSnippet
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
string FieldType { get; }
        /// <summary>
    /// Render method.
    /// </summary>
string Render(ContentFieldDefinition field);
}
