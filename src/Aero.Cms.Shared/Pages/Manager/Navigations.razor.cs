using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Shared.Pages.Manager;

/// <summary>
/// Represents a class for Navigations.
/// </summary>
public partial class Navigations
{
    [Inject] private INavigationsHttpClient NavigationsClient { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<Navigations> Logger { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private RadzenDataGrid<NavigationSummary>? _menuGrid;
    private IReadOnlyList<NavigationSummary> _menus = Array.Empty<NavigationSummary>();
    private bool _isLoading;
    private bool _isSaving;

        /// <summary>
    /// OnInitializedAsync method.
    /// </summary>
protected override async Task OnInitializedAsync()
    {
        await LoadMenusAsync();
    }

    private async Task LoadMenusAsync()
    {
        _isLoading = true;
        try
        {
            var result = await NavigationsClient.GetAllAsync();
            if (result is Result<IReadOnlyList<NavigationSummary>, AeroError>.Ok ok)
            {
                _menus = ok.Value.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Name).ToList();
            }
            else if (result is Result<IReadOnlyList<NavigationSummary>, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, L["Header menus failed to load"], fail.Error.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load header menus");
            Notify(NotificationSeverity.Error, L["Header menus failed to load"], ex.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task CreateMenuAsync()
    {
        var dialogResult = await DialogService.OpenAsync<CreateNavMenuDialog>(
            L["New Header Menu"],
            null,
            new DialogOptions { Width = "460px", Resizable = false, Draggable = false });

        if (dialogResult is not CreateNavMenuDialogResult request)
        {
            return;
        }

        _isSaving = true;
        try
        {
            var result = await NavigationsClient.CreateAsync(new CreateNavigationRequest(
                request.Name.Trim(),
                request.Description?.Trim(),
                []));

            if (result is Result<NavigationDetail, AeroError>.Ok ok)
            {
                Notify(NotificationSeverity.Success, L["Header menu created"]);
                Navigation.NavigateTo(EditorUrl(ok.Value.Id));
            }
            else if (result is Result<NavigationDetail, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, L["Header menu was not created"], fail.Error.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create header menu");
            Notify(NotificationSeverity.Error, L["Header menu was not created"], ex.Message);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void OpenEditor(NavigationSummary? menu)
    {
        if (menu is null)
        {
            return;
        }

        Navigation.NavigateTo(EditorUrl(menu.Id));
    }

    private async Task ReloadGridAsync()
    {
        await LoadMenusAsync();
        if (_menuGrid is not null)
        {
            await _menuGrid.Reload();
        }
    }

    private static string EditorUrl(long id) => $"/manager/nav-menu/editor/{id}";

    private static string DisplayState(string? state)
        => string.IsNullOrWhiteSpace(state)
            ? "Draft"
            : state.Replace("PublishedWithDraft", "Published + Draft", StringComparison.Ordinal);

    private static BadgeStyle BadgeStyleFor(string? state)
        => state switch
        {
            "Published" => BadgeStyle.Success,
            "PublishedWithDraft" => BadgeStyle.Warning,
            "Archived" => BadgeStyle.Danger,
            _ => BadgeStyle.Info
        };

    private void Notify(NotificationSeverity severity, string summary, string? detail = null)
    {
        NotificationService.Notify(new NotificationMessage
        {
            Severity = severity,
            Summary = summary,
            Detail = detail ?? string.Empty,
            Duration = severity == NotificationSeverity.Error ? 6000 : 3500
        });
    }
}
