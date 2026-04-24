using Aero.Cms.Core.Entities;
using Aero.Validators.Extensions;
using FluentValidation;

namespace Aero.Cms.Modules.Sites;

public sealed class SiteModelValidator : AbstractValidator<SitesModel>
{
    public SiteModelValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(x => $"site requuires a valid id");

        RuleFor(x => x.TenantId)
            .GreaterThan(0)
            .WithMessage(x => $"site {x.Id} requires a valid tenant id" );

        RuleFor(x => x.Name)
            .NotNullOrEmpty()
            .WithMessage("Site name must have a value");

        RuleFor(x => x.Hostname)
            .NotNullOrEmpty()
            .WithMessage("Site host name must have a value");
    }
}