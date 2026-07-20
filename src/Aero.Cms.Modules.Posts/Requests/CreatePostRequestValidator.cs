namespace Aero.Cms.Modules.Posts.Requests;

/// <summary>
/// Validates the required fields, length limits, and route-safe slug format for post creation.
/// </summary>
internal class CreatePostRequestValidator : AbstractValidator<CreatePostRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePostRequestValidator"/> class.
    /// </summary>
public CreatePostRequestValidator()
    {
        RuleFor(x => x.Title).NotNull().NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotNull().NotEmpty().MaximumLength(200).Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(300);
    }
}
