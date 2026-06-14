using FluentValidation;
using Aero.Cms.Abstractions.Http.Clients;

namespace Aero.Cms.Modules.Pages.CustomComponents;

public sealed class SavePageCustomComponentRequestValidator
    : AbstractValidator<SavePageCustomComponentRequest>
{
    public SavePageCustomComponentRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(120);
        RuleFor(request => request.Description)
            .MaximumLength(500);
        RuleFor(request => request.Category)
            .NotEmpty()
            .MaximumLength(80);
        RuleFor(request => request.Root)
            .NotNull()
            .Must(root => !string.IsNullOrWhiteSpace(root.NodeId))
            .WithMessage("The component root must have a node ID.")
            .Must(root => !string.IsNullOrWhiteSpace(root.CatalogId))
            .WithMessage("The component root must have a catalog ID.");
        RuleForEach(request => request.Tags)
            .NotEmpty()
            .MaximumLength(60);
    }
}
