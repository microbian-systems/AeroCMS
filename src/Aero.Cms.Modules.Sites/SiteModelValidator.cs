using Aero.Cms.Core.Entities;
using Aero.Validators.Extensions;
using FluentValidation;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Represents a class for SiteModelValidator.
/// </summary>
public sealed class SiteModelValidator : AbstractValidator<SitesModel>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="SiteModelValidator"/> class.
    /// </summary>
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