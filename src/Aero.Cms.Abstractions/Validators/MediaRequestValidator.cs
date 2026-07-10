using Aero.Cms.Abstractions.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

/// <summary>
/// Represents a class for PostRequestValidators.
/// </summary>
public class PostRequestValidators : AbstractValidator<CreatePostRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="PostRequestValidators"/> class.
    /// </summary>
public PostRequestValidators()
    {
        RuleFor(x => x.Title).NotNull().NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotNull().NotEmpty().MaximumLength(200).Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(300);
    }
}

/// <summary>
/// Represents a class for UpdatePostRequestValidators.
/// </summary>
public class UpdatePostRequestValidators : AbstractValidator<UpdatePostRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePostRequestValidators"/> class.
    /// </summary>
public UpdatePostRequestValidators()
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
/// Represents a class for DeletePostRequestValidators.
/// </summary>
public class DeletePostRequestValidators : AbstractValidator<DeletePostRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="DeletePostRequestValidators"/> class.
    /// </summary>
public DeletePostRequestValidators()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}   