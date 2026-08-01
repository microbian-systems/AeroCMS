namespace Aero.Cms.Modules.Posts.Requests;

/// <summary>
/// Validates post updates, including a positive identifier and a route-safe slug.
/// </summary>
internal class UpdatePostRequestValidator : AbstractValidator<UpdatePostRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePostRequestValidator"/> class.
    /// </summary>
public UpdatePostRequestValidator()
    {
        RuleFor(x => x.Id).NotNull().GreaterThan(0);
        RuleFor(x => x.Title).NotNull().NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotNull().NotEmpty().MaximumLength(200).Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(300);
    }
}
