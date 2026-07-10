using System.Globalization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Shared.Services;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Shared.Pages.Manager.Sites;

/// <summary>
/// Represents a class for Sites.
/// </summary>
public partial class Sites : ComponentBase
{
        /// <summary>
    /// Gets or sets the Sites Client.
    /// </summary>
[Inject] protected ISitesHttpClient SitesClient { get; set; } = null!;
        /// <summary>
    /// Gets or sets the Current Site Accessor.
    /// </summary>
[Inject] protected ICurrentSiteAccessor CurrentSiteAccessor { get; set; } = null!;
        /// <summary>
    /// Gets or sets the Admin State.
    /// </summary>
[Inject] protected AdminStateContainer AdminState { get; set; } = null!;
        /// <summary>
    /// Gets or sets the Dialog Service.
    /// </summary>
[Inject] protected DialogService DialogService { get; set; } = null!;
        /// <summary>
    /// Gets or sets the Navigation.
    /// </summary>
[Inject] protected NavigationManager Navigation { get; set; } = null!;
        /// <summary>
    /// Gets or sets the L.
    /// </summary>
[Inject] protected IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

        /// <summary>
    /// Grid.
    /// </summary>
protected RadzenDataGrid<SiteViewModel>? Grid;
        /// <summary>
    /// SiteRows.
    /// </summary>
protected IReadOnlyList<SiteViewModel>? SiteRows;
        /// <summary>
    /// Count.
    /// </summary>
protected int Count;
        /// <summary>
    /// IsLoading.
    /// </summary>
protected bool IsLoading;

        /// <summary>
    /// LoadData method.
    /// </summary>
protected async Task LoadData(LoadDataArgs args)
    {
        IsLoading = true;
        try
        {
            var result = await SitesClient.GetAllAsync();
            if (result is Result<IReadOnlyList<SiteViewModel>, AeroError>.Ok ok)
            {
                Count = ok.Value.Count;
                SiteRows = ok.Value.Skip(args.Skip ?? 0).Take(args.Top ?? 10).ToList();
            }
            else if (result is Result<IReadOnlyList<SiteViewModel>, AeroError>.Failure fail)
            {
                NotificationService.Notify(NotificationSeverity.Error, fail.Error.ToString(), duration: 4000);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

        /// <summary>
    /// DeleteSiteAsync method.
    /// </summary>
protected async Task DeleteSiteAsync(SiteViewModel site)
    {
        var confirmed = await DialogService.Confirm(
            $"Are you sure you want to delete '{site.Name}'? This action cannot be undone.",
            "Delete Site",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });
        if (confirmed != true) return;

        var result = await SitesClient.DeleteAsync(site.Id);
        if (result is Result<bool, AeroError>.Ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, $"Site '{site.Name}' deleted");
            if (Grid is not null)
                await Grid.Reload();
        }
        else if (result is Result<bool, AeroError>.Failure fail)
        {
            NotificationService.Notify(NotificationSeverity.Error, fail.Error.ToString(), duration: 4000);
        }
    }

        /// <summary>
    /// SelectSiteAsync method.
    /// </summary>
protected async Task SelectSiteAsync(long siteId, string? siteName)
    {
        await CurrentSiteAccessor.SetCurrentSiteAsync(siteId);
        AdminState.SetSite(siteId, siteName ?? "Site");
        NotificationService.Notify(NotificationSeverity.Success, "Site selected");
        Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
    }

        /// <summary>
    /// FormatCulture method.
    /// </summary>
protected static string FormatCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return "en-US";

        try
        {
            var info = CultureInfo.GetCultureInfo(culture);
            return info.Name;
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
    }
}
