using Aero.Cms.Modules.Pages.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;


public class CreateAliasRequestValidator : AbstractValidator<CreateAliasRequest>
{
    public CreateAliasRequestValidator()
    {
        RuleFor(x => x.OldPath)
            .NotEmpty().WithMessage("Old path is required.")
            .MaximumLength(2000).WithMessage("Old path cannot exceed 2000 characters.");
        RuleFor(x => x.NewPath)
            .NotEmpty().WithMessage("New path is required.")
            .MaximumLength(2000).WithMessage("New path cannot exceed 2000 characters.");
        RuleFor(x => x.OldPath)
            .NotEqual(x => x.NewPath).WithMessage("Old path and new path cannot be the same.");
        RuleFor(x => x.SiteId)
            .GreaterThan(0).WithMessage("SiteId must be a positive integer.");
    }
}

public class UpdateAliasRequestValidator : AbstractValidator<UpdateAliasRequest>
{
    public UpdateAliasRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
        RuleFor(x => x.OldPath)
            .NotEmpty().WithMessage("Old path is required.")
            .MaximumLength(2000).WithMessage("Old path cannot exceed 2000 characters.");
        RuleFor(x => x.NewPath)
            .NotEmpty().WithMessage("New path is required.")
            .MaximumLength(2000).WithMessage("New path cannot exceed 2000 characters.");
        RuleFor(x => x.OldPath)
            .NotEqual(x => x.NewPath).WithMessage("Old path and new path cannot be the same.");
    }
}


public class DeleteAliasRequestValidator : AbstractValidator<DeleteAliasRequest>
{
    public DeleteAliasRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
    }
}
