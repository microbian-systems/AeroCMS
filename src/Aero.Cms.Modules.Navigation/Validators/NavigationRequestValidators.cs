using Aero.Cms.Abstractions.Http.Clients;
using FluentValidation;

namespace Aero.Cms.Modules.Navigation.Validators;

/// <summary>
/// Validates initial navigation names, logos, and legacy top-level links.
/// </summary>
public sealed class CreateNavigationRequestValidator : AbstractValidator<CreateNavigationRequest>
{
    /// <summary>
    /// Initializes rules requiring a name of at most 100 characters, a logo URL of at most
    /// 2,048 characters, and no more than 100 individually valid links.
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
/// Validates navigation draft metadata and its legacy top-level link collection.
/// </summary>
public sealed class UpdateNavigationRequestValidator : AbstractValidator<UpdateNavigationRequest>
{
    /// <summary>
    /// Initializes rules requiring a name of at most 100 characters, a logo URL of at most
    /// 2,048 characters, and no more than 100 individually valid legacy links.
    /// </summary>
    /// <remarks>
    /// Mapped component payloads are validated later by
    /// <see cref="Aero.Cms.Modules.Navigation.Domain.NavMenuSnapshot"/>. Row, column, and block
    /// structure bypasses these collection rules; spans are clamped during mapping rather than rejected.
    /// </remarks>
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
/// Validates a create-request link's label, order, destination, and target keyword.
/// </summary>
public sealed class CreateNavigationItemRequestValidator : AbstractValidator<CreateNavigationItemRequest>
{
    /// <summary>
    /// Initializes rules for a nonblank 120-character label, nonnegative order, and a
    /// safe internal or absolute HTTP(S) external destination.
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
/// Validates an update-request link's label, order, destination, and target keyword.
/// </summary>
public sealed class UpdateNavigationItemRequestValidator : AbstractValidator<UpdateNavigationItemRequest>
{
    /// <summary>
    /// Initializes rules for a nonblank 120-character label, nonnegative order, and a
    /// safe internal or absolute HTTP(S) external destination.
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

/// <summary>
/// Centralizes URL and browsing-target rules shared by create and update validators.
/// </summary>
internal static class NavigationUrlRules
{
    /// <summary>
    /// Validates the destination encoded by a create-item request.
    /// </summary>
    /// <param name="request">The create-item request.</param>
    /// <returns>Whether its URL/page combination matches its external flag.</returns>
public static bool IsValid(CreateNavigationItemRequest request)
        => IsValid(request.Url, request.PageId, request.IsExternal);

    /// <summary>
    /// Validates the destination encoded by an update-item request.
    /// </summary>
    /// <param name="request">The update-item request.</param>
    /// <returns>Whether its URL/page combination matches its external flag.</returns>
public static bool IsValid(UpdateNavigationItemRequest request)
        => IsValid(request.Url, request.PageId, request.IsExternal);

    /// <summary>
    /// Determines whether a link target is blank or a supported browsing-context keyword.
    /// </summary>
    /// <param name="target">The candidate target.</param>
    /// <returns>Whether the target is accepted.</returns>
public static bool IsValidTarget(string? target)
        => string.IsNullOrWhiteSpace(target) || target is "_self" or "_blank" or "_parent" or "_top";

    /// <summary>
    /// Applies external HTTP(S) and internal root-relative/page destination rules.
    /// </summary>
    /// <param name="url">The candidate URL.</param>
    /// <param name="pageId">The optional internal page identifier.</param>
    /// <param name="isExternal">Whether the URL must be absolute HTTP(S).</param>
    /// <returns>Whether the destination is valid.</returns>
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
