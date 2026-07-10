using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Abstractions.Validators;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Shared.Pages.Manager;

/// <summary>
/// Represents a class for Aliases.
/// </summary>
public partial class Aliases
{
        /// <summary>
    /// Gets or sets the log.
    /// </summary>
[Inject] public ILogger<Aliases> log { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private RadzenDataGrid<AliasViewModel>? _grid;
    private IReadOnlyList<AliasViewModel>? _aliases;
    private int _count;
    private bool _isLoading;
    private bool _showCreateForm;

    private string _createOldPath = "";
    private string _createNewPath = "";

    /// <summary>
    /// Resolves the current SiteId from AdminState (singleton state container,
    /// hydrated from localStorage by ManagerShellLayout). Falls back to the
    /// default site API if state hasn't been hydrated yet.
    ///
    /// SiteId MUST be passed from client to server on every request (REST is stateless).
    /// </summary>
    private async Task<long?> ResolveSiteIdAsync()
    {
        var siteId = AdminState.CurrentSiteId;

        if (siteId is null)
        {
            var defaultResult = await SitesClient.GetDefaultAsync();
            if (defaultResult is Result<SiteViewModel, AeroError>.Ok defaultOk)
            {
                siteId = defaultOk.Value.Id;
                AdminState.SetSite(defaultOk.Value.Id, defaultOk.Value.Name ?? "Default Site");
            }
        }

        return siteId;
    }

    private async Task LoadData(LoadDataArgs args)
    {
        _isLoading = true;
        try
        {
            var siteId = await ResolveSiteIdAsync();
            if (siteId is null) return;

            var result = await AliasClient.GetAllBySiteAsync(siteId.Value);
            if (result is Result<IReadOnlyList<AliasViewModel>, AeroError>.Ok ok)
            {
                _count = ok.Value.Count;
                _aliases = ok.Value;
            }
            else if (result is Result<IReadOnlyList<AliasViewModel>, AeroError>.Failure fail)
            {
                NotificationService.Notify(NotificationSeverity.Error, fail.Error.ToString(), duration: 4000);
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "error occurred getting site aliases");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task HandleCreate()
    {
        var siteId = AdminState.CurrentSiteId;
        if (siteId is null)
        {
            // Fallback: try resolving from default site API
            siteId = await ResolveSiteIdAsync();
            if (siteId is null) return;
        }

        var oldPath = SanitizePath(_createOldPath);
        var newPath = SanitizePath(_createNewPath);

        if (string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath))
        {
            NotificationService.Notify(NotificationSeverity.Warning, L["Both Old URL and New URL are required."]);
            return;
        }

        var request = new CreateAliasRequest(siteId.Value, oldPath, newPath);

        // Client-side validation via FluentValidation
        var validator = new CreateAliasRequestValidator();
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage);
            NotificationService.Notify(NotificationSeverity.Error,
                string.Join("; ", errors), duration: 6000);
            return;
        }

        var result = await AliasClient.CreateAsync(request);
        if (result is Result<AliasViewModel, AeroError>.Ok ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, L["Alias created"]);
            _showCreateForm = false;
            _createOldPath = _createNewPath = "";
            await _grid?.Reload();
        }
        else if (result is Result<AliasViewModel, AeroError>.Failure fail)
        {
            NotificationService.Notify(NotificationSeverity.Error, fail.Error.ToString(), duration: 4000);
        }
    }

    private async Task DeleteAliasAsync(AliasViewModel alias)
    {
        var confirmed = await DialogService.Confirm(
            string.Format(L["Delete alias '{0}' \u2192 '{1}'?"], alias.OldPath, alias.NewPath),
            L["Delete Alias"],
            new ConfirmOptions { OkButtonText = L["Delete"], CancelButtonText = L["Cancel"] });
        if (confirmed != true) return;

        var result = await AliasClient.DeleteAsync(alias.Id);
        if (result is Result<bool, AeroError>.Ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, L["Alias deleted"]);
            await _grid?.Reload();
        }
        else if (result is Result<bool, AeroError>.Failure fail)
        {
            NotificationService.Notify(NotificationSeverity.Error, fail.Error.ToString(), duration: 4000);
        }
    }

    /// <summary>
    /// Normalizes a URL path by trimming whitespace, stripping leading/trailing slashes,
    /// then prepending a single leading slash. Accepts "old-page", "/old-page/", etc.
    /// </summary>
    private static string SanitizePath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        return "/" + raw.Trim().TrimStart('/').TrimEnd('/');
    }
}
