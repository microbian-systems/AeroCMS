using FluentValidation;

namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Defines an interface for IContentFieldValidator.
/// </summary>
public interface IContentFieldValidator
{
        /// <summary>
    /// Gets or sets the Field Type.
    /// </summary>
string FieldType { get; }

        /// <summary>
    /// ValidateElement method.
    /// </summary>
void ValidateElement(
        ContentFieldDefinition field,
        JsonElement element,
        ContentValidationMode mode,
        ValidationContext<ContentItem> context);
}
