using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Posts.Validators;

internal class PostValidator : AbstractValidator<PostDocument>
{
    public PostValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.SiteId).GreaterThan(0);
        RuleFor(x => x.Slug).NotNull().NotEmpty();
        RuleFor(x => x.Content).NotNull().NotEmpty();
        RuleFor(x => x.Title).NotNull().NotEmpty();
    }
}