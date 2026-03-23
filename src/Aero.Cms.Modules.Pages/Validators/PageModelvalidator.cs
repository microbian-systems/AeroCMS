using FluentValidation;

namespace Aero.Cms.Modules.Pages.Validators;

public class PageModelValidator : AbstractValidator<PageDocument>
{
    public PageModelValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Slug).NotNull().NotEmpty();
        RuleFor(x => x.Title).NotNull().NotEmpty();
    }
}

