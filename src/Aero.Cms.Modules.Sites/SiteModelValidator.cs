using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Infrastructure;
using Aero.Validators.Extensions;
using FluentValidation;

namespace Aero.Cms.Modules.Sites;

public sealed class SiteModelValidator : AbstractValidator<SitesModel>
{
    public SiteModelValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage(x => $"site requires a valid id");

        RuleFor(x => x.TenantId)
            .GreaterThan(0)
            .WithMessage(x => $"site {x.Id} requires a valid tenant id");

        RuleFor(x => x.Name)
            .NotNullOrEmpty()
            .WithMessage("Site name must have a value");

        RuleFor(x => x.PrimaryHost)
            .NotNullOrEmpty()
            .WithMessage("Primary host name must have a value");

        RuleFor(x => x.Hosts)
            .NotEmpty()
            .WithMessage("At least one host must be configured");

        // Ensure PrimaryHost is in the Hosts list (normalized comparison)
        RuleFor(x => x)
            .Must(site =>
                site.Hosts.Any(h =>
                    string.Equals(HostNormalizer.Normalize(h), HostNormalizer.Normalize(site.PrimaryHost), StringComparison.Ordinal)))
            .WithMessage("PrimaryHost must be included in the Hosts list");
    }
}