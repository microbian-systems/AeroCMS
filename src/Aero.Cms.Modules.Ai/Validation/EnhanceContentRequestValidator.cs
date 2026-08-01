using Aero.Cms.Abstractions.Ai;
using FluentValidation;

namespace Aero.Cms.Modules.Ai.Validation;

/// <summary>
/// Validates content-enhancement requests before they are sent to an AI provider.
/// </summary>
/// <remarks>
/// The validator constrains known content kinds and target fields and limits the size of prompt
/// context. It does not sanitize content or establish whether the requested edit is safe or factual.
/// </remarks>
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
    /// Initializes validation rules for supported fields and request-size limits.
    /// </summary>
    /// <remarks>
    /// Content kinds are limited to <c>post</c>, <c>page</c>, and <c>doc</c>; target fields are
    /// limited to body, title, summary, SEO title, and SEO description. Provider identifiers are
    /// length-checked but are resolved separately by the settings provider.
    /// </remarks>
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
