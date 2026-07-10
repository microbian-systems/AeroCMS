using Aero.Cms.Abstractions.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;


/// <summary>
/// Represents a class for CreateCategoryRequestValidator.
/// </summary>
public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="CreateCategoryRequestValidator"/> class.
    /// </summary>
public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(255).WithMessage("Name cannot exceed 255 characters.");
        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.");
    }
}

/// <summary>
/// Represents a class for UpdateCategoryRequestValidator.
/// </summary>
public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdateCategoryRequestValidator"/> class.
    /// </summary>
public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(255).WithMessage("Name cannot exceed 255 characters.");
        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.");
    }
}

/// <summary>
/// Represents a class for DeleteCategoryRequestValidator.
/// </summary>
public class DeleteCategoryRequestValidator : AbstractValidator<DeleteCategoryRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="DeleteCategoryRequestValidator"/> class.
    /// </summary>
public DeleteCategoryRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
    }
}