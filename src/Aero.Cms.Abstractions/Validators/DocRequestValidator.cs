using Aero.Cms.Abstractions.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

/// <summary>
/// Represents a class for CreateDocRequestValidators.
/// </summary>
public class CreateDocRequestValidators : AbstractValidator<CreateDocRequest>
{
      /// <summary>
   /// Initializes a new instance of the <see cref="CreateDocRequestValidators"/> class.
   /// </summary>
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

/// <summary>
/// Represents a class for UpdateDocRequestValidators.
/// </summary>
public class UpdateDocRequestValidators : AbstractValidator<UpdateDocRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdateDocRequestValidators"/> class.
    /// </summary>
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


/// <summary>
/// Represents a class for DeleteDocRequestValidators.
/// </summary>
public class DeleteDocRequestValidators : AbstractValidator<DeleteDocRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="DeleteDocRequestValidators"/> class.
    /// </summary>
public DeleteDocRequestValidators()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
    }
}