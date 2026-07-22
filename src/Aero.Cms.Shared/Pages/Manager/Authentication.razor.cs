using Aero.Cms.Abstractions.Authentication;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager;

/// <summary>Code-behind for installation-wide CMS manager authentication configuration.</summary>
public abstract class AuthenticationBase : ComponentBase
{
    [Inject] private IManagerAuthenticationModeResolver ModeResolver { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "configured")]
    protected string? Configured { get; set; }

    [SupplyParameterFromQuery(Name = "error")]
    protected string? Error { get; set; }

    protected bool IsLoading { get; private set; } = true;
    protected string? RequestedProvider { get; private set; }
    protected string? EffectiveProvider { get; private set; }
    protected string? Status { get; private set; }
    protected string? LoadErrorMessage { get; private set; }

    protected string? SuccessMessage => string.Equals(Configured, "1", StringComparison.Ordinal)
        ? "Manager identity authority configuration was saved. Verify it with the recovery administrator to activate remote sign-in."
        : null;

    protected string? QueryErrorMessage => string.Equals(Error, "1", StringComparison.Ordinal)
        ? "Manager authentication configuration or verification failed. Check the canonical authority, Vault reference, provider credentials, and recovery-administrator account."
        : null;

    protected bool IsPendingRemote =>
        string.Equals(Status, ManagerAuthenticationModeStatuses.Pending, StringComparison.Ordinal) &&
        ManagerIdentityProviders.IsSupported(RequestedProvider);

    protected bool IsRemoteActive =>
        string.Equals(Status, ManagerAuthenticationModeStatuses.Remote, StringComparison.Ordinal) &&
        ManagerIdentityProviders.IsSupported(EffectiveProvider);

    protected bool IsWorkOs =>
        string.Equals(RequestedProvider, ManagerIdentityProviders.WorkOs, StringComparison.Ordinal);

    protected string RequestedProviderLabel => ProviderLabel(RequestedProvider);
    protected string EffectiveProviderLabel => ProviderLabel(EffectiveProvider);
    protected string StatusLabel => Status switch
    {
        ManagerAuthenticationModeStatuses.Local => "Local",
        ManagerAuthenticationModeStatuses.Pending => "Pending configuration or verification",
        ManagerAuthenticationModeStatuses.Remote => "Remote provider active",
        _ => "Unavailable"
    };

    protected string OrganizationPlaceholder => IsWorkOs
        ? "org_01H..."
        : "00000000-0000-0000-0000-000000000000";

    protected string OrganizationHint => IsWorkOs
        ? "Enter the exact WorkOS organization ID assigned to this AeroCMS installation."
        : "Enter the Microsoft Entra Workforce tenant ID as a lowercase canonical GUID. Do not use common, organizations, or consumers.";

    protected string AuthorityValue => IsWorkOs ? "https://api.workos.com" : string.Empty;

    protected string AuthorityPlaceholder => IsWorkOs
        ? "https://api.workos.com"
        : "https://login.microsoftonline.com/{tenant-id}/v2.0";

    protected string AuthorityHint => IsWorkOs
        ? "WorkOS uses the fixed canonical authority https://api.workos.com."
        : "Replace {tenant-id} with the same lowercase tenant GUID. The URL must match exactly and must not use a tenant alias.";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var result = await ModeResolver.ResolveAsync();
            if (result is Result<ManagerAuthenticationModeResolution, AeroError>.Ok(var resolution) &&
                AuthenticationProviderSelections.Manager.IsCanonical(resolution.RequestedProvider) &&
                AuthenticationProviderSelections.Manager.IsCanonical(resolution.EffectiveProvider) &&
                resolution.Status is ManagerAuthenticationModeStatuses.Local or
                    ManagerAuthenticationModeStatuses.Pending or ManagerAuthenticationModeStatuses.Remote)
            {
                RequestedProvider = resolution.RequestedProvider;
                EffectiveProvider = resolution.EffectiveProvider;
                Status = resolution.Status;
                return;
            }

            LoadErrorMessage = "Manager authentication status is unavailable. Configuration and activation controls have been disabled for safety.";
        }
        catch
        {
            LoadErrorMessage = "Manager authentication status is unavailable. Configuration and activation controls have been disabled for safety.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string ProviderLabel(string? provider) => provider switch
    {
        AuthenticationProviderSelections.Manager.Local => "Local ASP.NET Core Identity",
        AuthenticationProviderSelections.Manager.EntraWorkforce => "Microsoft Entra Workforce",
        AuthenticationProviderSelections.Manager.WorkOs => "WorkOS",
        _ => "Unavailable"
    };
}
