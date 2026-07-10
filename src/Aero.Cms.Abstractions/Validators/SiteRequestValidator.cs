using Aero.Cms.Abstractions.Requests;
using FluentValidation;
using System.Globalization;

namespace Aero.Cms.Abstractions.Validators;

/// <summary>
/// Represents a class for SiteRequestValidator.
/// </summary>
public class SiteRequestValidator : AbstractValidator<CreateSiteRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="SiteRequestValidator"/> class.
    /// </summary>
public SiteRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(255).WithMessage("Name cannot exceed 255 characters.");
        RuleFor(x => x.PrimaryHost)
            .NotEmpty().WithMessage("PrimaryHost is required.")
            .MaximumLength(255).WithMessage("PrimaryHost cannot exceed 255 characters.");
        RuleFor(x => x.DefaultCulture)
            .Must(SiteCultureValidation.BeKnownCultureWhenProvided)
            .WithMessage("DefaultCulture must be a valid .NET culture name.");
        RuleForEach(x => x.SupportedCultures)
            .Must(SiteCultureValidation.BeKnownCultureWhenProvided)
            .WithMessage("SupportedCultures must contain valid .NET culture names.");
        // Hosts is optional — PrimaryHost is always automatically registered as a host.
    }
}

/// <summary>
/// Represents a class for UpdateSiteRequestValidator.
/// </summary>
public class UpdateSiteRequestValidator : AbstractValidator<UpdateSiteRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdateSiteRequestValidator"/> class.
    /// </summary>
public UpdateSiteRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(255).WithMessage("Name cannot exceed 255 characters.");
        RuleFor(x => x.PrimaryHost)
            .NotEmpty().WithMessage("PrimaryHost is required.")
            .MaximumLength(255).WithMessage("PrimaryHost cannot exceed 255 characters.");
        RuleFor(x => x.DefaultCulture)
            .Must(SiteCultureValidation.BeKnownCultureWhenProvided)
            .WithMessage("DefaultCulture must be a valid .NET culture name.");
        RuleForEach(x => x.SupportedCultures)
            .Must(SiteCultureValidation.BeKnownCultureWhenProvided)
            .WithMessage("SupportedCultures must contain valid .NET culture names.");
        // Hosts is optional — PrimaryHost is always automatically registered as a host.
    }
}

/// <summary>
/// Represents a class for DeleteSiteRequestValidator.
/// </summary>
public class DeleteSiteRequestValidator : AbstractValidator<DeleteSiteRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="DeleteSiteRequestValidator"/> class.
    /// </summary>
public DeleteSiteRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Id must be a positive integer.");
    }
}

file static class SiteCultureValidation
{
        /// <summary>
    /// BeKnownCultureWhenProvided method.
    /// </summary>
public static bool BeKnownCultureWhenProvided(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return true;

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
