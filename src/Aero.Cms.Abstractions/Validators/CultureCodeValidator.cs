using System.Globalization;
using Aero.Cms.Abstractions.Interfaces;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

/// <summary>
/// Represents a class for CultureCodeValidator.
/// </summary>
public sealed class CultureCodeValidator : AbstractValidator<ICultureAware>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="CultureCodeValidator"/> class.
    /// </summary>
public CultureCodeValidator()
    {
        RuleFor(x => x.Culture)
            .NotEmpty()
            .Must(BeKnownCulture)
            .WithMessage("Culture must be a valid .NET culture name.");
    }

    private static bool BeKnownCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return false;

        try
        {
            CultureInfo.GetCultureInfo(culture.Trim());
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
