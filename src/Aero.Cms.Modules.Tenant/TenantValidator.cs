using Aero.Cms.Core.Entities;
using FluentValidation;

namespace Aero.Cms.Modules.Tenant;

/// <summary>
/// Represents a class for TenantValidator.
/// </summary>
public class TenantValidator : AbstractValidator<TenantModel>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="TenantValidator"/> class.
    /// </summary>
public TenantValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Hostname).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
