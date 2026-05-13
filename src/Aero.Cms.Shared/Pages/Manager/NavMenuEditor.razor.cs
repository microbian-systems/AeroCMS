using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Shared.Pages.Manager;

public partial class NavMenuEditor
{
    [Parameter] public long Id { get; set; }

    [Inject] private INavigationsHttpClient NavigationsClient { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<NavMenuEditor> Logger { get; set; } = default!;

    private NavigationDetail? _selected;
    private RadzenDataGrid<NavItemEditorModel>? _itemsGrid;
    private List<NavItemEditorModel> _items = [];
    private bool _isLoading;
    private bool _isSaving;
    private string _editName = string.Empty;
    private string? _editDescription;
    private string? _editSiteLogoUrl;

    protected override async Task OnParametersSetAsync()
    {
        await LoadMenuAsync();
    }

    private async Task LoadMenuAsync()
    {
        _isLoading = true;
        try
        {
            var result = await NavigationsClient.GetByIdAsync(Id);
            if (result is Result<NavigationDetail, AeroError>.Ok ok)
            {
                SetSelected(ok.Value);
            }
            else if (result is Result<NavigationDetail, AeroError>.Failure fail)
            {
                ClearSelection();
                Notify(NotificationSeverity.Error, "Header menu failed to load", fail.Error.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load header menu {MenuId}", Id);
            ClearSelection();
            Notify(NotificationSeverity.Error, "Header menu failed to load", ex.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task AddItem()
    {
        var dialogResult = await DialogService.OpenAsync<AddNavMenuLinkDialog>(
            "Add Header Link",
            null,
            new DialogOptions { Width = "720px", Resizable = false, Draggable = false });

        if (dialogResult is not AddNavMenuLinkDialogResult link)
        {
            return;
        }

        var nextOrder = _items.Count == 0 ? 0 : _items.Max(x => x.Order) + 1;
        var item = new NavItemEditorModel
        {
            Label = link.Label,
            Url = link.Url,
            PageId = link.PageId,
            AltText = link.AltText,
            Order = nextOrder
        };

        _items = _items.Append(item).ToList();
        NormalizeOrders();
        await RefreshItemsGridAsync();
    }

    private async Task RemoveItemAsync(NavItemEditorModel item)
    {
        if (!_items.Contains(item))
        {
            return;
        }

        _items = _items.Where(x => !ReferenceEquals(x, item)).ToList();
        NormalizeOrders();
        await RefreshItemsGridAsync();
    }

    private async Task MoveItemAsync(NavItemEditorModel item, int direction)
    {
        var current = _items.IndexOf(item);
        if (current < 0)
        {
            return;
        }

        var target = current + direction;
        if (target < 0 || target >= _items.Count)
        {
            return;
        }

        _items.RemoveAt(current);
        _items.Insert(target, item);
        NormalizeOrders();
        await RefreshItemsGridAsync();
    }

    private async Task RefreshLinksAsync()
    {
        await LoadMenuAsync();
        if (_selected is not null)
        {
            await RefreshItemsGridAsync();
        }
    }

    private async Task SaveDraftAsync()
    {
        if (_selected is null)
        {
            return;
        }

        var validation = ValidateEditor();
        if (validation is not null)
        {
            Notify(NotificationSeverity.Warning, "Draft was not saved", validation);
            return;
        }

        _isSaving = true;
        try
        {
            var request = new UpdateNavigationRequest(
                _editName.Trim(),
                _editDescription?.Trim(),
                _items
                    .OrderBy(x => x.Order)
                    .Select(x => new UpdateNavigationItemRequest(x.Id, x.Label.Trim(), x.Url?.Trim(), x.PageId, x.Order, x.AltText?.Trim()))
                    .ToList(),
                _editSiteLogoUrl?.Trim());

            var result = await NavigationsClient.SaveDraftAsync(_selected.Id, request, _selected.Version);
            if (result is Result<NavigationDetail, AeroError>.Ok ok)
            {
                SetSelected(ok.Value);
                Notify(NotificationSeverity.Success, "Draft saved");
            }
            else if (result is Result<NavigationDetail, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, "Draft was not saved", fail.Error.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save header menu draft {MenuId}", _selected.Id);
            Notify(NotificationSeverity.Error, "Draft was not saved", ex.Message);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task PublishAsync()
    {
        if (_selected is null)
        {
            return;
        }

        _isSaving = true;
        try
        {
            var result = await NavigationsClient.PublishAsync(_selected.Id, _selected.Version);
            if (result is Result<NavigationDetail, AeroError>.Ok ok)
            {
                SetSelected(ok.Value);
                Notify(NotificationSeverity.Success, "Header menu published");
            }
            else if (result is Result<NavigationDetail, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, "Header menu was not published", fail.Error.ToString());
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task SetDefaultAsync()
    {
        if (_selected is null)
        {
            return;
        }

        _isSaving = true;
        try
        {
            var result = await NavigationsClient.SetDefaultAsync(_selected.Id);
            if (result is Result<bool, AeroError>.Ok)
            {
                Notify(NotificationSeverity.Success, "Default header menu updated");
            }
            else if (result is Result<bool, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, "Default header menu was not updated", fail.Error.ToString());
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ArchiveAsync()
    {
        if (_selected is null)
        {
            return;
        }

        var confirmed = await DialogService.Confirm(
            $"Archive '{_selected.Name}'? Published pages will no longer resolve this menu.",
            "Archive Header Menu",
            new ConfirmOptions { OkButtonText = "Archive", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        _isSaving = true;
        try
        {
            var result = await NavigationsClient.DeleteAsync(_selected.Id);
            if (result is Result<bool, AeroError>.Ok)
            {
                Notify(NotificationSeverity.Success, "Header menu archived");
                BackToList();
            }
            else if (result is Result<bool, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, "Header menu was not archived", fail.Error.ToString());
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void BackToList()
    {
        Navigation.NavigateTo("/manager/navigations");
    }

    private void SetSelected(NavigationDetail detail)
    {
        _selected = detail;
        _editName = detail.Name;
        _editDescription = detail.Title;
        _editSiteLogoUrl = detail.SiteLogoUrl;
        _items = detail.Items
            .OrderBy(x => x.Order)
            .Select(x => new NavItemEditorModel
            {
                Id = x.Id,
                Label = x.Label,
                Url = x.Url,
                PageId = x.PageId,
                AltText = x.AltText,
                Order = x.Order
            })
            .ToList();
        NormalizeOrders();
    }

    private void ClearSelection()
    {
        _selected = null;
        _editName = string.Empty;
        _editDescription = null;
        _editSiteLogoUrl = null;
        _items = [];
    }

    private string? ValidateEditor()
    {
        if (string.IsNullOrWhiteSpace(_editName))
        {
            return "Menu name is required.";
        }

        if (_editSiteLogoUrl?.Length > 2048)
        {
            return "Site logo URL cannot be longer than 2048 characters.";
        }

        var invalid = _items.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.Label) || string.IsNullOrWhiteSpace(x.Url));
        return invalid is null ? null : "Every link needs a label and URL.";
    }

    private void NormalizeOrders()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].Order = i;
        }
    }

    private async Task RefreshItemsGridAsync()
    {
        _items = _items.OrderBy(x => x.Order).ToList();
        if (_itemsGrid is not null)
        {
            await _itemsGrid.Reload();
        }
        else
        {
            StateHasChanged();
        }
    }

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

    protected sealed class NavItemEditorModel
    {
        public long Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? Url { get; set; }
        public long? PageId { get; set; }
        public string? AltText { get; set; }
        public int Order { get; set; }
    }
}
