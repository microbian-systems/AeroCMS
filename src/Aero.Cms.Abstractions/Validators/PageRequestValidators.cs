using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Abstractions.Pages.Rendering;
using FluentValidation;
using System.Text;

namespace Aero.Cms.Abstractions.Validators;

/// <summary>
/// Represents a class for PageRequestValidators.
/// </summary>
public class PageRequestValidators : AbstractValidator<CreatePageRequest>
{
    private const int MaximumDraftSourceLengthBytes = 50_000;

        /// <summary>
    /// Initializes a new instance of the <see cref="PageRequestValidators"/> class.
    /// </summary>
public PageRequestValidators()
    {
        RuleFor(x => x.Title).NotNull().NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotNull().NotEmpty().MaximumLength(200).Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(300);
        RuleFor(x => x.RendererId)
            .Must(rendererId => PageRendererIds.IsValid(PageRendererIds.NormalizeOrDefault(rendererId)))
            .WithMessage("The page renderer identifier is invalid.");
        RuleFor(x => x.DraftSource)
            .Null()
            .When(x => PageRendererIds.NormalizeOrDefault(x.RendererId) == PageRendererIds.AeroComposition)
            .WithMessage("Aero composition pages cannot include draft source.");
        RuleFor(x => x.DraftSource)
            .Must(source => !string.IsNullOrWhiteSpace(source))
            .When(x => IsBuiltInSourceRenderer(
                PageRendererIds.NormalizeOrDefault(x.RendererId)))
            .WithMessage("Source-rendered pages require non-blank draft source.");
        RuleFor(x => x.DraftSource)
            .Must(WithinDraftSourceLimit)
            .When(x => x.DraftSource is not null)
            .WithMessage($"Page draft source cannot exceed {MaximumDraftSourceLengthBytes} UTF-8 bytes.");
    }

    private static bool WithinDraftSourceLimit(string? source)
        => source is null || Encoding.UTF8.GetByteCount(source) <= MaximumDraftSourceLengthBytes;

    private static bool IsBuiltInSourceRenderer(string rendererId) =>
        rendererId is PageRendererIds.Scriban or PageRendererIds.SharpTs or PageRendererIds.Htmx;
}

/// <summary>
/// Represents a class for UpdatePageRequestValidator.
/// </summary>
public class UpdatePageRequestValidator : AbstractValidator<UpdatePageRequest>
{
    private const int MaximumDraftSourceLengthBytes = 50_000;

        /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePageRequestValidator"/> class.
    /// </summary>
public UpdatePageRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotNull().NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotNull().NotEmpty().MaximumLength(200).Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(300);
        RuleFor(x => x.RendererId)
            .Must(rendererId => PageRendererIds.IsValid(PageRendererIds.NormalizeOrDefault(rendererId)))
            .WithMessage("The page renderer identifier is invalid.");
        RuleFor(x => x.DraftSource)
            .Null()
            .When(x => PageRendererIds.NormalizeOrDefault(x.RendererId) == PageRendererIds.AeroComposition)
            .WithMessage("Aero composition pages cannot include draft source.");
        RuleFor(x => x.DraftSource)
            .Must(source => !string.IsNullOrWhiteSpace(source))
            .When(x => IsBuiltInSourceRenderer(
                    PageRendererIds.NormalizeOrDefault(x.RendererId))
                && x.DraftSource is not null)
            .WithMessage("Page draft source cannot be blank.");
        RuleFor(x => x.DraftSource)
            .Must(WithinDraftSourceLimit)
            .When(x => x.DraftSource is not null)
            .WithMessage($"Page draft source cannot exceed {MaximumDraftSourceLengthBytes} UTF-8 bytes.");
    }

    private static bool WithinDraftSourceLimit(string? source)
        => source is null || Encoding.UTF8.GetByteCount(source) <= MaximumDraftSourceLengthBytes;

    private static bool IsBuiltInSourceRenderer(string rendererId) =>
        rendererId is PageRendererIds.Scriban or PageRendererIds.SharpTs or PageRendererIds.Htmx;
}

/// <summary>
/// Represents a class for DeletePageRequestValidator.
/// </summary>
public class DeletePageRequestValidator : AbstractValidator<DeletePageRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="DeletePageRequestValidator"/> class.
    /// </summary>
public DeletePageRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
