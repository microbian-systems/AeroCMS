using Aero.Cms.Abstractions.Http.Clients;
using FluentValidation;

namespace Aero.Cms.Modules.Navigation.Validators;

/// <summary>
/// Represents a class for CreateNavigationRequestValidator.
/// </summary>
public sealed class CreateNavigationRequestValidator : AbstractValidator<CreateNavigationRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="CreateNavigationRequestValidator"/> class.
    /// </summary>
public CreateNavigationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.SiteLogoUrl)
            .MaximumLength(2048);

        RuleFor(x => x.Items)
            .NotNull()
            .Must(items => items.Count <= 100)
            .WithMessage("Navigation menu cannot contain more than 100 top-level items.");

        RuleForEach(x => x.Items)
            .SetValidator(new CreateNavigationItemRequestValidator());
    }
}

/// <summary>
/// Represents a class for UpdateNavigationRequestValidator.
/// </summary>
public sealed class UpdateNavigationRequestValidator : AbstractValidator<UpdateNavigationRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNavigationRequestValidator"/> class.
    /// </summary>
public UpdateNavigationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.SiteLogoUrl)
            .MaximumLength(2048);

        RuleFor(x => x.Items)
            .NotNull()
            .Must(items => items.Count <= 100)
            .WithMessage("Navigation menu cannot contain more than 100 top-level items.");

        RuleForEach(x => x.Items)
            .SetValidator(new UpdateNavigationItemRequestValidator());
    }
}

/// <summary>
/// Represents a class for CreateNavigationItemRequestValidator.
/// </summary>
public sealed class CreateNavigationItemRequestValidator : AbstractValidator<CreateNavigationItemRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="CreateNavigationItemRequestValidator"/> class.
    /// </summary>
public CreateNavigationItemRequestValidator()
    {
        RuleFor(x => x.Label)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x)
            .Must(x => x.PageId is > 0 || !string.IsNullOrWhiteSpace(x.Url))
            .WithMessage("Navigation links require either PageId or Url.");

        RuleFor(x => x)
            .Must(NavigationUrlRules.IsValid)
            .WithMessage("External links require an absolute http/https URL. Internal links require a relative URL or selected page.");

        RuleFor(x => x.Target)
            .Must(NavigationUrlRules.IsValidTarget)
            .WithMessage("Navigation link target must be _self, _blank, _parent, or _top.");
    }
}

/// <summary>
/// Represents a class for UpdateNavigationItemRequestValidator.
/// </summary>
public sealed class UpdateNavigationItemRequestValidator : AbstractValidator<UpdateNavigationItemRequest>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNavigationItemRequestValidator"/> class.
    /// </summary>
public UpdateNavigationItemRequestValidator()
    {
        RuleFor(x => x.Label)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x)
            .Must(x => x.PageId is > 0 || !string.IsNullOrWhiteSpace(x.Url))
            .WithMessage("Navigation links require either PageId or Url.");

        RuleFor(x => x)
            .Must(NavigationUrlRules.IsValid)
            .WithMessage("External links require an absolute http/https URL. Internal links require a relative URL or selected page.");

        RuleFor(x => x.Target)
            .Must(NavigationUrlRules.IsValidTarget)
            .WithMessage("Navigation link target must be _self, _blank, _parent, or _top.");
    }
}

internal static class NavigationUrlRules
{
        /// <summary>
    /// IsValid method.
    /// </summary>
public static bool IsValid(CreateNavigationItemRequest request)
        => IsValid(request.Url, request.PageId, request.IsExternal);

        /// <summary>
    /// IsValid method.
    /// </summary>
public static bool IsValid(UpdateNavigationItemRequest request)
        => IsValid(request.Url, request.PageId, request.IsExternal);

        /// <summary>
    /// IsValidTarget method.
    /// </summary>
public static bool IsValidTarget(string? target)
        => string.IsNullOrWhiteSpace(target) || target is "_self" or "_blank" or "_parent" or "_top";

    private static bool IsValid(string? url, long? pageId, bool isExternal)
    {
        if (isExternal)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        if (pageId is > 0)
        {
            return true;
        }

        var trimmed = url?.Trim();
        return !string.IsNullOrWhiteSpace(trimmed)
            && trimmed.StartsWith('/')
            && !trimmed.StartsWith("//", StringComparison.Ordinal);
    }
}
