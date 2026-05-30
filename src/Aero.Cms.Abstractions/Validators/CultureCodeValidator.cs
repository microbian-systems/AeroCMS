using System.Globalization;
using Aero.Cms.Abstractions.Interfaces;
using FluentValidation;

namespace Aero.Cms.Abstractions.Validators;

public sealed class CultureCodeValidator : AbstractValidator<ICultureAware>
{
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
