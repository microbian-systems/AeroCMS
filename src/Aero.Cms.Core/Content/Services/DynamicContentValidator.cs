using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using FluentValidation;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Builds synchronous validation rules from a content type definition.
/// </summary>
public sealed class DynamicContentValidator : AbstractValidator<ContentItem>
{
    /// <summary>
    /// Initializes validation for alias, slug, required fields, and registered field-type rules.
    /// </summary>
    /// <param name="type">The authoritative content type definition.</param>
    /// <param name="mode">The validation mode; required fields are enforced only for publication.</param>
    /// <param name="fieldValidators">Validators keyed case-insensitively by field type.</param>
    /// <remarks>
    /// Rules validate the content type alias and nonempty slug before field values. A missing or
    /// JSON-null field is skipped unless it is required in publish mode. Fields whose type has no
    /// registered validator are not rejected by this validator.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// More than one supplied validator has the same field type, ignoring case.
    /// </exception>
    public DynamicContentValidator(ContentTypeDefinition type, ContentValidationMode mode, IEnumerable<IContentFieldValidator> fieldValidators)
    {
        RuleFor(x => x.ContentTypeAlias).Equal(type.Alias);
        RuleFor(x => x.Slug).NotEmpty();

        var lookup = fieldValidators.ToDictionary(v => v.FieldType, StringComparer.OrdinalIgnoreCase);

        RuleFor(x => x.Fields).Custom((fields, context) =>
        {
            foreach (var field in type.Fields)
            {
                var hasValue = fields.TryGetValue(field.Name, out var element) && element.ValueKind != JsonValueKind.Null;

                if (!hasValue)
                {
                    if (field.Required && mode == ContentValidationMode.Publish)
                        context.AddFailure(field.Name, $"{field.Label ?? field.Name} is required.");
                    continue;
                }

                if (lookup.TryGetValue(field.FieldType, out var fieldValidator))
                    fieldValidator.ValidateElement(field, element, mode, context);
            }
        });
    }
}
