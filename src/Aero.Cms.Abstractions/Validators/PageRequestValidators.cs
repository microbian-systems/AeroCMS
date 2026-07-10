using Aero.Cms.Abstractions.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

/// <summary>
/// Represents a class for PageRequestValidators.
/// </summary>
public class PageRequestValidators : AbstractValidator<CreatePageRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="PageRequestValidators"/> class.
    /// </summary>
public PageRequestValidators()
    {
        RuleFor(x => x.Title).NotNull().NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotNull().NotEmpty().MaximumLength(200).Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(300);
    }
}

/// <summary>
/// Represents a class for UpdatePageRequestValidator.
/// </summary>
public class UpdatePageRequestValidator : AbstractValidator<UpdatePageRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePageRequestValidator"/> class.
    /// </summary>
public UpdatePageRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotNull().NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotNull().NotEmpty().MaximumLength(200).Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(300);
    }
}

/// <summary>
/// Represents a class for DeletePageRequestValidator.
/// </summary>
public class DeletePageRequestValidator : AbstractValidator<DeletePageRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="DeletePageRequestValidator"/> class.
    /// </summary>
public DeletePageRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}