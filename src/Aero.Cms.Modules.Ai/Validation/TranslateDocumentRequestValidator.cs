using Aero.Cms.Abstractions.Ai;
using FluentValidation;

namespace Aero.Cms.Modules.Ai.Validation;

public sealed class TranslateDocumentRequestValidator : AbstractValidator<TranslateDocumentRequest>
{
    public TranslateDocumentRequestValidator()
    {
        RuleFor(x => x.SourceCulture)
            .NotEmpty()
            .MaximumLength(35);

        RuleFor(x => x.TargetCulture)
            .NotEmpty()
            .MaximumLength(35)
            .NotEqual(x => x.SourceCulture, StringComparer.OrdinalIgnoreCase)
            .WithMessage("Target culture must be different from source culture.");

        RuleFor(x => x.ProviderId)
            .MaximumLength(100)
            .When(x => x.ProviderId is not null);

        RuleFor(x => x.Fields)
            .NotNull()
            .Must(fields => fields.Count > 0)
            .WithMessage("At least one field is required.")
            .Must(fields => fields.Select(field => field.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() == fields.Count)
            .WithMessage("Field keys must be unique.");

        RuleForEach(x => x.Fields).ChildRules(field =>
        {
            field.RuleFor(x => x.Key)
                .NotEmpty()
                .MaximumLength(300);

            field.RuleFor(x => x.SourceText)
                .NotNull()
                .MaximumLength(100_000);
        });
    }
}
