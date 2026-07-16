namespace Aero.Cms.Modules.Posts.Validators;

internal class PostValidator : AbstractValidator<PostDocument>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="PostValidator"/> class.
    /// </summary>
public PostValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SiteId).GreaterThan(0);
        RuleFor(x => x.Slug).NotNull().NotEmpty();
        RuleFor(x => x.MarkdownContent).NotNull().NotEmpty();
        RuleFor(x => x.Title).NotNull().NotEmpty();
    }
}
