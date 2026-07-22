using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Aero.Cms.Abstractions.Authentication;

namespace Aero.Cms.Shared.Pages.Manager;

/// <summary>
/// Represents a class for LoginBase.
/// </summary>
public abstract class LoginBase : ComponentBase
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>
    [Inject]
    private IManagerAuthenticationModeResolver ModeResolver { get; set; } = default!;

    /// <summary>
    /// Gets or sets the L.
    /// </summary>
    [Inject]
    protected IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    /// <summary>
    /// Gets or sets the Return Url.
    /// </summary>
    [SupplyParameterFromQuery(Name = "returnUrl")]
    protected string? ReturnUrl { get; set; }

    [SupplyParameterFromQuery(Name = "error")]
    protected string? Error { get; set; }

    /// <summary>
    /// Model.
    /// </summary>
    protected readonly LoginModel Model = new();
    /// <summary>
    /// ErrorMessage.
    /// </summary>
    protected string? ErrorMessage;
    protected string SafeReturnPath { get; private set; } = "/manager";

    protected bool IsAuthenticationModeUnavailable { get; private set; }
    protected bool IsRemoteAuthenticationActive { get; private set; }
    protected bool IsRemoteAuthenticationPending { get; private set; }
    protected string RemoteProviderLabel { get; private set; } = "External provider";
    protected string RemoteLoginHref { get; private set; } = "/api/v1/admin/auth/federation/login?returnUrl=%2Fmanager";
    protected bool IsDevelopment { get; private set; }

    /// <summary>
    /// OnInitialized method.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        SafeReturnPath = IsSafeLocalReturnPath(ReturnUrl) ? ReturnUrl! : "/manager";
        RemoteLoginHref = $"/api/v1/admin/auth/federation/login?returnUrl={Uri.EscapeDataString(SafeReturnPath)}";
        if (string.Equals(Error, "1", StringComparison.Ordinal))
            ErrorMessage = "Login failed. Check your credentials and try again.";
        var result = await ModeResolver.ResolveAsync();
        if (result is not Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var mode))
        {
            IsAuthenticationModeUnavailable = true;
            return;
        }

        IsRemoteAuthenticationActive = string.Equals(mode.Status,
            ManagerAuthenticationModeStatuses.Remote, StringComparison.Ordinal);
        IsRemoteAuthenticationPending = string.Equals(mode.Status,
            ManagerAuthenticationModeStatuses.Pending, StringComparison.Ordinal);
        RemoteProviderLabel = mode.RequestedProvider == AuthenticationProviderSelections.Manager.EntraWorkforce
            ? "Microsoft Entra"
            : mode.RequestedProvider == AuthenticationProviderSelections.Manager.WorkOs ? "WorkOS" : "External provider";

        IsDevelopment = Navigation.BaseUri.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
            Navigation.BaseUri.Contains("127.0.0.1", StringComparison.Ordinal);
        if (IsDevelopment)
        {
            Model.EmailOrUserName = "admin";
            Model.Password = "*strongPassword1";
        }
    }

    private static bool IsSafeLocalReturnPath(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith("/", StringComparison.Ordinal) &&
        value.Length <= 2048 && !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\') &&
        !value.Any(char.IsControl);

    /// <summary>
    /// Represents a class for LoginModel.
    /// </summary>
    protected sealed class LoginModel
    {
        /// <summary>
        /// Gets or sets the Email Or User Name.
        /// </summary>
        public string EmailOrUserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Password.
        /// </summary>
        public string Password { get; set; } = string.Empty;

    }
}
