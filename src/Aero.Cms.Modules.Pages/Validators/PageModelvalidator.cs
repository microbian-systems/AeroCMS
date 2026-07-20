using Aero.Cms.Abstractions.Enums;
using FluentValidation;

namespace Aero.Cms.Modules.Pages.Validators;

/// <summary>
/// Validates page identifiers, slugs, materialized paths, hierarchy values, and
/// publication-state enum values.
/// </summary>
public class PageDocumentValidator : AbstractValidator<PageDocument>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PageDocumentValidator"/> class.
    /// </summary>
    /// <remarks>
    /// Homepage slugs are exempt from the ordinary lower-case segment pattern. Parent
    /// validation rejects only non-positive identifiers and direct self-parenting; it
    /// does not query the store for parent existence or detect longer cycles.
    /// </remarks>
public PageDocumentValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SiteId).GreaterThan(0);
        RuleFor(x => x.Title).NotNull().NotEmpty();
        
        // Slug validation — homepage uses "/", exempted from the segment pattern
        RuleFor(x => x.Slug)
            .NotNull().NotEmpty();
        When(x => x.Kind != PageKind.Homepage, () =>
        {
            RuleFor(x => x.Slug)
                .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
                .WithMessage("Slug must contain only lowercase letters, numbers, and hyphens (no consecutive or trailing hyphens).");
        });

        // Path validation
        RuleFor(x => x.Path)
            .NotNull().NotEmpty()
            .Must(path => path.StartsWith("/"))
            .WithMessage("Path must start with '/'.")
            .Must(path => !path.Contains("//"))
            .WithMessage("Path must not contain double slashes.")
            .Must(path => !path.EndsWith("/") || path == "/")
            .WithMessage("Path must not end with '/' (except root).");

        // Hierarchy
        RuleFor(x => x.Depth)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0);

        // ParentId: when set, must be > 0 and cannot be its own ID
        When(x => x.ParentId.HasValue, () =>
        {
            RuleFor(x => x.ParentId!.Value)
                .GreaterThan(0)
                .Must((doc, parentId) => parentId != doc.Id)
                .WithMessage("A page cannot be its own parent.");
        });

        // Publication state
        RuleFor(x => x.PublicationState)
            .IsInEnum();
    }
}

