using Aero.Cms.Abstractions.Http.Clients;
using FluentValidation;

namespace Aero.Cms.Modules.Navigation.Validators;

public sealed class CreateNavigationRequestValidator : AbstractValidator<CreateNavigationRequest>
{
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

public sealed class UpdateNavigationRequestValidator : AbstractValidator<UpdateNavigationRequest>
{
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

public sealed class CreateNavigationItemRequestValidator : AbstractValidator<CreateNavigationItemRequest>
{
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

public sealed class UpdateNavigationItemRequestValidator : AbstractValidator<UpdateNavigationItemRequest>
{
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
    public static bool IsValid(CreateNavigationItemRequest request)
        => IsValid(request.Url, request.PageId, request.IsExternal);

    public static bool IsValid(UpdateNavigationItemRequest request)
        => IsValid(request.Url, request.PageId, request.IsExternal);

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
