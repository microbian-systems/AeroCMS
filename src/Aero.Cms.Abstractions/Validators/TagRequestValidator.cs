using Aero.Cms.Modules.Pages.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

public class TagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public TagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");
        RuleFor(x => x.siteId)
            .GreaterThan(0).WithMessage("SiteId must be a positive integer.");
    }
}

