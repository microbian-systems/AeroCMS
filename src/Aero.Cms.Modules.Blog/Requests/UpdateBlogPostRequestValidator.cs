using FluentValidation;

namespace Aero.Cms.Modules.Blog.Requests;

internal class UpdateBlogPostRequestValidator : AbstractValidator<UpdateBlogPostRequest>
{
    public UpdateBlogPostRequestValidator()
    {
        RuleFor(x => x.Id).NotNull().GreaterThan(0);
        RuleFor(x => x.Title).NotNull().NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotNull().NotEmpty().MaximumLength(200).Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(300);
    }
}