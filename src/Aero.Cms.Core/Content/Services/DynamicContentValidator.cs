using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using FluentValidation;

namespace Aero.Cms.Core.Content.Services;

public sealed class DynamicContentValidator : AbstractValidator<ContentItem>
{
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
