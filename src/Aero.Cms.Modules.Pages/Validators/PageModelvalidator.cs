using Aero.Cms.Core.Entities;
using FluentValidation;

namespace Aero.Cms.Modules.Pages.Validators;

public class PageDocumentValidator : AbstractValidator<PageDocument>
{
    public PageDocumentValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Slug).NotNull().NotEmpty();
        RuleFor(x => x.Title).NotNull().NotEmpty();
    }
}

