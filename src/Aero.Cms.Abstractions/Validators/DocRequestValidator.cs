using Aero.Cms.Modules.Pages.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

public class CreateDocRequestValidators : AbstractValidator<CreateDocRequest>
{
   public CreateDocRequestValidators()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(255).WithMessage("Title cannot exceed 255 characters.");
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.");
        RuleFor(x => x.SiteId)
            .GreaterThan(0).WithMessage("SiteId must be a positive integer.");
    }
}

public class UpdateDocRequestValidators : AbstractValidator<UpdateDocRequest>
{
    public UpdateDocRequestValidators()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(255).WithMessage("Title cannot exceed 255 characters.");
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.");
    }
}


public class DeleteDocRequestValidators : AbstractValidator<DeleteDocRequest>
{
    public DeleteDocRequestValidators()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
    }
}