using Aero.Cms.Abstractions.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

public class SiteRequestValidator : AbstractValidator<CreateSiteRequest>
{
    public SiteRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(255).WithMessage("Name cannot exceed 255 characters.");
        RuleFor(x => x.PrimaryHost)
            .NotEmpty().WithMessage("PrimaryHost is required.")
            .MaximumLength(255).WithMessage("PrimaryHost cannot exceed 255 characters.");
        // Hosts is optional — PrimaryHost is always automatically registered as a host.
    }
}

public class UpdateSiteRequestValidator : AbstractValidator<UpdateSiteRequest>
{
    public UpdateSiteRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(255).WithMessage("Name cannot exceed 255 characters.");
        RuleFor(x => x.PrimaryHost)
            .NotEmpty().WithMessage("PrimaryHost is required.")
            .MaximumLength(255).WithMessage("PrimaryHost cannot exceed 255 characters.");
        // Hosts is optional — PrimaryHost is always automatically registered as a host.
    }
}

public class DeleteSiteRequestValidator : AbstractValidator<DeleteSiteRequest>
{
    public DeleteSiteRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
    }
}