using Aero.Cms.Modules.Pages.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

public class PostRequestValidators : AbstractValidator<CreatePostRequest>
{
    public PostRequestValidators()
    {
        RuleFor(x => x.Title).NotNull().NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotNull().NotEmpty().MaximumLength(200).Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Summary).MaximumLength(500);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(300);
    }
}

public class UpdatePostRequestValidators : AbstractValidator<UpdatePostRequest>
{
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

public class DeletePostRequestValidators : AbstractValidator<DeletePostRequest>
{
    public DeletePostRequestValidators()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}   