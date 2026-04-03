using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Blog.Validators;

internal class BlogPostValidator : AbstractValidator<BlogPostDocument>
{
    public BlogPostValidator()
    {
        RuleFor(x => x.Id > 0);
        RuleFor(x => x.Slug).NotNull().NotEmpty();
        RuleFor(x => x.Content).NotNull().NotEmpty();
        RuleFor(x => x.Title).NotNull().NotEmpty();
    }
}