using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Aero.Cms.Shared.Pages.Manager;

/// <summary>Code-behind for the explicit local manager-recovery page.</summary>
public abstract class RecoveryBase : ComponentBase
{
    /// <summary>Gets the manager localizer.</summary>
    [Inject] protected IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    /// <summary>Gets or sets the safe post-login return target.</summary>
    [SupplyParameterFromQuery(Name = "returnUrl")]
    protected string? ReturnUrl { get; set; }

    /// <summary>Gets or sets the uniform public failure flag.</summary>
    [SupplyParameterFromQuery(Name = "error")]
    protected string? Error { get; set; }

    /// <summary>Gets the uniform public failure message.</summary>
    protected string? ErrorMessage => string.Equals(Error, "1", StringComparison.Ordinal)
        ? "Recovery sign-in failed."
        : null;

    /// <summary>Gets the server-validated fallback target posted with the form.</summary>
    protected string SafeReturnUrl => IsLocalReturnUrl(ReturnUrl) ? ReturnUrl! : "/manager";

    private static bool IsLocalReturnUrl(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.Length <= 2048
            && returnUrl.StartsWith("/", StringComparison.Ordinal)
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            && returnUrl.All(character => !char.IsControl(character));
}
