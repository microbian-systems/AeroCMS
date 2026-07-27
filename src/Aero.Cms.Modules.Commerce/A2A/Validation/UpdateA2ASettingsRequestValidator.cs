using Aero.Cms.Modules.Commerce.A2A.Models;
using FluentValidation;

namespace Aero.Cms.Modules.Commerce.A2A.Validation;

/// <summary>Validates the manager-supplied A2A availability update.</summary>
public sealed class UpdateA2ASettingsRequestValidator : AbstractValidator<UpdateA2ASettingsRequest>
{
    /// <summary>Initializes the request validation rules.</summary>
    public UpdateA2ASettingsRequestValidator()
    {
        RuleFor(x => x.IsEnabled)
            .NotNull()
            .WithMessage("A2A enabled state is required.");
    }
}
