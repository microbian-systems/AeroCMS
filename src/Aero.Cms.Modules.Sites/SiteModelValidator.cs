using Aero.Cms.Core.Entities;
using Aero.Validators.Extensions;
using FluentValidation;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Validates the identifiers and required display name used when creating or updating a site.
/// </summary>
public sealed class SiteModelValidator : AbstractValidator<SitesModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SiteModelValidator"/> class.
    /// </summary>
    /// <remarks>
    /// A site and tenant identifier must both be positive, and the site name must contain a value.
    /// Host, culture, and tenant-existence checks are outside this validator.
    /// </remarks>
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
    }
}