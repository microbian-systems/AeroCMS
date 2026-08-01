using Aero.Cms.Abstractions.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

/// <summary>
/// Represents a class for CreatePostRequestValidators.
/// </summary>
public class CreatePostRequestValidators : AbstractValidator<CreatePostRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="CreatePostRequestValidators"/> class.
    /// </summary>
public CreatePostRequestValidators()
    {
        RuleFor(x => x.Title).NotNull().NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotNull().NotEmpty().MaximumLength(200).Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(300);
    }
}

/// <summary>
/// Represents a class for UpdatePostRequestValidator.
/// </summary>
public class UpdatePostRequestValidator : AbstractValidator<UpdatePostRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePostRequestValidator"/> class.
    /// </summary>
public UpdatePostRequestValidator()
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
/// Represents a class for DeletePostRequestValidator.
/// </summary>
public class DeletePostRequestValidator : AbstractValidator<DeletePostRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="DeletePostRequestValidator"/> class.
    /// </summary>
public DeletePostRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}