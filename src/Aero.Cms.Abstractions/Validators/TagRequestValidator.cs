using Aero.Cms.Abstractions.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

/// <summary>
/// Represents a class for TagRequestValidator.
/// </summary>
public class TagRequestValidator : AbstractValidator<CreateTagRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="TagRequestValidator"/> class.
    /// </summary>
public TagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");
        RuleFor(x => x.siteId)
            .GreaterThan(0).WithMessage("SiteId must be a positive integer.");
    }
}

/// <summary>
/// Represents a class for UpdateTagRequestValidator.
/// </summary>
public class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTagRequestValidator"/> class.
    /// </summary>
public UpdateTagRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");
    }
}
