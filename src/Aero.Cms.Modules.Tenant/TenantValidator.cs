using Aero.Cms.Core.Entities;
using FluentValidation;

namespace Aero.Cms.Modules.Tenant;

public class TenantValidator : AbstractValidator<TenantModel>
{
    public TenantValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Hostname).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
