using Aero.Cms.Abstractions.Http.Clients;
using FluentValidation;

namespace Aero.Cms.Modules.Navigation.Validators;

public sealed class CreateNavigationRequestValidator : AbstractValidator<CreateNavigationRequest>
{
    public CreateNavigationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.SiteLogoUrl)
            .MaximumLength(2048);

        RuleFor(x => x.Items)
            .NotNull()
            .Must(items => items.Count <= 100)
            .WithMessage("Navigation menu cannot contain more than 100 top-level items.");

        RuleForEach(x => x.Items)
            .SetValidator(new CreateNavigationItemRequestValidator());
    }
}

public sealed class UpdateNavigationRequestValidator : AbstractValidator<UpdateNavigationRequest>
{
    public UpdateNavigationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.SiteLogoUrl)
            .MaximumLength(2048);

        RuleFor(x => x.Items)
            .NotNull()
            .Must(items => items.Count <= 100)
            .WithMessage("Navigation menu cannot contain more than 100 top-level items.");

        RuleForEach(x => x.Items)
            .SetValidator(new UpdateNavigationItemRequestValidator());
    }
}

public sealed class CreateNavigationItemRequestValidator : AbstractValidator<CreateNavigationItemRequest>
{
    public CreateNavigationItemRequestValidator()
    {
        RuleFor(x => x.Label)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x)
            .Must(x => x.PageId is > 0 || !string.IsNullOrWhiteSpace(x.Url))
            .WithMessage("Navigation links require either PageId or Url.");
    }
}

public sealed class UpdateNavigationItemRequestValidator : AbstractValidator<UpdateNavigationItemRequest>
{
    public UpdateNavigationItemRequestValidator()
    {
        RuleFor(x => x.Label)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x)
            .Must(x => x.PageId is > 0 || !string.IsNullOrWhiteSpace(x.Url))
            .WithMessage("Navigation links require either PageId or Url.");
    }
}
