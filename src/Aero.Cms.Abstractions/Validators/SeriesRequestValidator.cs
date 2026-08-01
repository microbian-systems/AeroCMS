using Aero.Cms.Abstractions.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

/// <summary>
/// Represents a class for CreateSeriesRequestValidator.
/// </summary>
public sealed class CreateSeriesRequestValidator : AbstractValidator<CreateSeriesRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="CreateSeriesRequestValidator"/> class.
    /// </summary>
public CreateSeriesRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Slug).MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

/// <summary>
/// Represents a class for UpdateSeriesRequestValidator.
/// </summary>
public sealed class UpdateSeriesRequestValidator : AbstractValidator<UpdateSeriesRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdateSeriesRequestValidator"/> class.
    /// </summary>
public UpdateSeriesRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Slug).MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
