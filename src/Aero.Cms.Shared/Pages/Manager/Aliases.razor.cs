using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Shared.Services;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Text;

namespace Aero.Cms.Shared.Pages.Manager;

public partial class Aliases
{
    [Inject] public ILogger<Aliases> log { get; set; } = default!;
    [Inject] public AdminStateContainer AdminState { get; set; } = default!;
    //[Inject] public CurrentSiteAccessor ctx { get; }
    private RadzenDataGrid<AliasViewModel>? _grid;
    private IReadOnlyList<AliasViewModel>? _aliases;
    private int _count;
    private bool _isLoading;
    private bool _showCreateForm;

    private string _createOldPath = "";
    private string _createNewPath = "";

    private async Task LoadData(LoadDataArgs args)
    {
        _isLoading = true;
        try
        {
            var siteId = AdminState.CurrentSiteId;

            // Fallback: if state wasn't hydrated yet, call the default site API
            if (siteId is null)
            {
                var defaultResult = await SitesClient.GetDefaultAsync();
                if (defaultResult is Result<SiteViewModel, AeroError>.Ok defaultOk)
                {
                    siteId = defaultOk.Value.Id;
                    AdminState.SetSite(defaultOk.Value.Id, defaultOk.Value.Name ?? "Default Site");
                }
            }

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
            log.LogError(ex, "error occurred getting site aliases {SiteId}", AdminState.CurrentSiteId);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task HandleCreate()
    {
        var siteId = AdminState.CurrentSiteId;
        if (siteId is null) return;

        var request = new CreateAliasRequest(siteId.Value, _createOldPath, _createNewPath);
        var result = await AliasClient.CreateAsync(request);
        if (result is Result<AliasViewModel, AeroError>.Ok ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Alias created");
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
            $"Delete alias '{alias.OldPath}' \u2192 '{alias.NewPath}'?",
            "Delete Alias",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });
        if (confirmed != true) return;

        var result = await AliasClient.DeleteAsync(alias.Id);
        if (result is Result<bool, AeroError>.Ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Alias deleted");
            await _grid?.Reload();
        }
        else if (result is Result<bool, AeroError>.Failure fail)
        {
            NotificationService.Notify(NotificationSeverity.Error, fail.Error.ToString(), duration: 4000);
        }
    }
}
