using Aero.Cms.Core.Entities;
using FluentValidation;

namespace Aero.Cms.Modules.Tenant;

/// <summary>
/// Validates tenant naming, hostname, and notes length constraints.
/// </summary>
public class TenantValidator : AbstractValidator<TenantModel>
{
    /// <summary>
    /// Initializes rules requiring nonblank names and hostnames of at most 256 characters,
    /// with notes limited to 1,000 characters.
    /// </summary>
public TenantValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Hostname).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
