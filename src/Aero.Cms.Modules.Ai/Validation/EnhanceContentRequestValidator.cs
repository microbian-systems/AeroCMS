using Aero.Cms.Abstractions.Ai;
using FluentValidation;

namespace Aero.Cms.Modules.Ai.Validation;

/// <summary>
/// Represents a class for EnhanceContentRequestValidator.
/// </summary>
public sealed class EnhanceContentRequestValidator : AbstractValidator<EnhanceContentRequest>
{
    private static readonly HashSet<string> ContentKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "post",
        "page",
        "doc"
    };

    private static readonly HashSet<string> TargetFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "body",
        "title",
        "summary",
        "seoTitle",
        "seoDescription"
    };

        /// <summary>
    /// Initializes a new instance of the <see cref="EnhanceContentRequestValidator"/> class.
    /// </summary>
public EnhanceContentRequestValidator()
    {
        // todo - get ai provider settings/options from the database config
        RuleFor(x => x.ContentKind)
            .NotEmpty()
            .Must(kind => ContentKinds.Contains(kind))
            .WithMessage("Content kind must be one of: post, page, doc.");

        RuleFor(x => x.TargetField)
            .NotEmpty()
            .Must(field => TargetFields.Contains(field))
            .WithMessage("Target field must be one of: body, title, summary, seoTitle, seoDescription.");

        RuleFor(x => x.CurrentText)
            .NotNull()
            .MaximumLength(30_000)
            .WithMessage("Current text must be 30,000 characters or fewer.");

        RuleFor(x => x.UserPrompt)
            .MaximumLength(20_000)
            .When(x => x.UserPrompt is not null);

        RuleFor(x => x.Title)
            .MaximumLength(300)
            .When(x => x.Title is not null);

        RuleFor(x => x.Summary)
            .MaximumLength(1_000)
            .When(x => x.Summary is not null);

        RuleFor(x => x.Slug)
            .MaximumLength(300)
            .When(x => x.Slug is not null);

        RuleFor(x => x.Tone)
            .MaximumLength(100)
            .When(x => x.Tone is not null);

        RuleFor(x => x.ProviderId)
            .MaximumLength(100)
            .When(x => x.ProviderId is not null);
    }
}
