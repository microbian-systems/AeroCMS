using FluentValidation;

namespace Aero.Cms.Abstractions.Content;

public interface IContentFieldValidator
{
    string FieldType { get; }

    void ValidateElement(
        ContentFieldDefinition field,
        JsonElement element,
        ContentValidationMode mode,
        ValidationContext<ContentItem> context);
}
