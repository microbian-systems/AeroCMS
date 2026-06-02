using Aero.Cms.Abstractions.Requests;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

public sealed class CreateSeriesRequestValidator : AbstractValidator<CreateSeriesRequest>
{
    public CreateSeriesRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Slug).MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class UpdateSeriesRequestValidator : AbstractValidator<UpdateSeriesRequest>
{
    public UpdateSeriesRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Slug).MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
