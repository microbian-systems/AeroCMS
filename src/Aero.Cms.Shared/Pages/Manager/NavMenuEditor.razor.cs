using System.Globalization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
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
    [Inject] private ISitesHttpClient SitesClient { get; set; } = default!;
    [Inject] private ICurrentSiteAccessor CurrentSiteAccessor { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<NavMenuEditor> Logger { get; set; } = default!;

    private NavigationDetail? _selected;
    private SiteViewModel? _currentSite;
    private IReadOnlyList<NavigationDetail> _cultureVariants = [];
    private RadzenDataGrid<NavItemEditorModel>? _itemsGrid;
    private List<NavItemEditorModel> _items = [];
    private bool _isLoading;
    private bool _isSaving;
    private bool _isLoadingTranslations;
    private bool _isCreatingTranslation;
    private string _selectedTranslationCulture = string.Empty;
    private string _editName = string.Empty;
    private string? _editDescription;
    private string? _editSiteLogoUrl;
    private IReadOnlyList<string> SupportedCultures =>
        _currentSite?.SupportedCultures is { Count: > 0 } cultures
            ? cultures
            : [_selected?.Culture ?? _currentSite?.DefaultCulture ?? "en-US"];

    private IEnumerable<string> AvailableTranslationCultures =>
        SupportedCultures
            .Select(NormalizeCultureName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(culture => !_cultureVariants.Any(variant =>
                string.Equals(variant.Culture, culture, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    private static readonly IReadOnlyList<LinkTargetOption> TargetOptions =
    [
        new("_self", "Same tab"),
        new("_blank", "New tab"),
        new("_parent", "Parent frame"),
        new("_top", "Top frame")
    ];

    protected override async Task OnParametersSetAsync()
    {
        await LoadMenuAsync();
    }

    private async Task LoadMenuAsync()
    {
        _isLoading = true;
        try
        {
            _currentSite ??= await ResolveCurrentSiteAsync();
            var result = await NavigationsClient.GetByIdAsync(Id);
            if (result is Result<NavigationDetail, AeroError>.Ok ok)
            {
                SetSelected(ok.Value);
                await LoadTranslationsAsync();
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

    private async Task LoadTranslationsAsync()
    {
        if (_selected is null)
        {
            _cultureVariants = [];
            ResetTranslationDraft();
            return;
        }

        _isLoadingTranslations = true;
        try
        {
            var result = await NavigationsClient.ListCultureVariantsAsync(_selected.Id);
            _cultureVariants = result is Result<IReadOnlyList<NavigationDetail>, AeroError>.Ok ok
                ? ok.Value.OrderBy(menu => menu.Culture, StringComparer.OrdinalIgnoreCase).ToList()
                : [];

            ResetTranslationDraft();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load header menu translations for {MenuId}", _selected.Id);
            _cultureVariants = [];
            ResetTranslationDraft();
        }
        finally
        {
            _isLoadingTranslations = false;
        }
    }

    private async Task CreateTranslationAsync()
    {
        if (_selected is null || _isCreatingTranslation)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedTranslationCulture))
        {
            Notify(NotificationSeverity.Warning, "Choose a target culture");
            return;
        }

        _isCreatingTranslation = true;
        try
        {
            var request = new ForkNavigationCultureRequest(_selectedTranslationCulture);
            var result = await NavigationsClient.ForkToCultureAsync(_selected.Id, request);
            if (result is Result<NavigationDetail, AeroError>.Ok ok)
            {
                Notify(NotificationSeverity.Success, $"Created {FormatCulture(ok.Value.Culture)} translation");
                Navigation.NavigateTo($"/manager/navigations/editor/{ok.Value.Id}");
            }
            else if (result is Result<NavigationDetail, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, "Translation was not created", fail.Error.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create header menu translation {MenuId}", _selected.Id);
            Notify(NotificationSeverity.Error, "Translation was not created", ex.Message);
        }
        finally
        {
            _isCreatingTranslation = false;
        }
    }

    private void OpenTranslation(long menuId)
        => Navigation.NavigateTo($"/manager/navigations/editor/{menuId}");

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
            IsExternal = link.IsExternal,
            Target = link.Target,
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
                    .Select(x =>
                    {
                        var isExternal = x.IsExternal;
                        return new UpdateNavigationItemRequest(
                            x.Id,
                            x.Label.Trim(),
                            NormalizeUrl(x.Url, isExternal),
                            isExternal ? null : x.PageId,
                            x.Order,
                            x.AltText?.Trim(),
                            isExternal,
                            NormalizeTarget(x.Target, isExternal));
                    })
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
                IsExternal = x.IsExternal || IsHttpUrl(x.Url),
                Target = NormalizeTarget(x.Target, x.IsExternal || IsHttpUrl(x.Url)),
                Order = x.Order
            })
            .ToList();
        NormalizeOrders();
    }

    private void ClearSelection()
    {
        _selected = null;
        _cultureVariants = [];
        _editName = string.Empty;
        _editDescription = null;
        _editSiteLogoUrl = null;
        _items = [];
        ResetTranslationDraft();
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
        if (invalid is not null)
        {
            return "Every link needs a label and URL.";
        }

        var invalidExternal = _items.FirstOrDefault(x => x.IsExternal && !IsHttpUrl(NormalizeUrl(x.Url, true)));
        if (invalidExternal is not null)
        {
            return $"External link '{invalidExternal.Label}' must start with http:// or https://.";
        }

        var invalidInternal = _items.FirstOrDefault(x => !x.IsExternal && x.PageId is null && !IsRelativeUrl(x.Url));
        if (invalidInternal is not null)
        {
            return $"Internal link '{invalidInternal.Label}' must use a site-relative URL that starts with '/'.";
        }

        var invalidTarget = _items.FirstOrDefault(x => !IsValidTarget(x.Target));
        return invalidTarget is null ? null : $"Link '{invalidTarget.Label}' has an invalid target.";
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

    private void OnItemExternalChanged(NavItemEditorModel item, ChangeEventArgs args)
    {
        item.IsExternal = args.Value is bool value ? value : bool.TryParse(args.Value?.ToString(), out var parsed) && parsed;
        if (item.IsExternal)
        {
            item.PageId = null;
            item.Target = "_blank";
            return;
        }

        item.Target = "_self";
    }

    private static string? NormalizeUrl(string? value, bool isExternal)
    {
        var url = value?.Trim();
        if (!isExternal || string.IsNullOrWhiteSpace(url) || url.Contains("://", StringComparison.Ordinal))
        {
            return url;
        }

        return $"https://{url}";
    }

    private static string NormalizeTarget(string? target, bool isExternal)
    {
        var normalized = string.IsNullOrWhiteSpace(target) ? "_self" : target.Trim();
        return normalized switch
        {
            "_self" or "_blank" or "_parent" or "_top" => normalized,
            _ => isExternal ? "_blank" : "_self"
        };
    }

    private static bool IsHttpUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool IsRelativeUrl(string? value)
    {
        var url = value?.Trim();
        return !string.IsNullOrWhiteSpace(url)
            && url.StartsWith("/", StringComparison.Ordinal)
            && !url.StartsWith("//", StringComparison.Ordinal);
    }

    private static bool IsValidTarget(string? target)
    {
        return string.IsNullOrWhiteSpace(target)
            || target is "_self" or "_blank" or "_parent" or "_top";
    }

    private async Task<SiteViewModel?> ResolveCurrentSiteAsync()
    {
        var selectedSite = await CurrentSiteAccessor.GetCurrentSiteAsync();
        if (selectedSite is not null)
        {
            return selectedSite;
        }

        var defaultResult = await SitesClient.GetDefaultAsync();
        return defaultResult is Result<SiteViewModel, AeroError>.Ok ok ? ok.Value : null;
    }

    private void ResetTranslationDraft()
        => _selectedTranslationCulture = AvailableTranslationCultures.FirstOrDefault() ?? string.Empty;

    private static string FormatCulture(string? culture)
    {
        var normalized = NormalizeCultureName(culture);
        try
        {
            var info = CultureInfo.GetCultureInfo(normalized);
            return $"{info.DisplayName} ({info.Name})";
        }
        catch (CultureNotFoundException)
        {
            return normalized;
        }
    }

    private static string NormalizeCultureName(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return "en-US";
        }

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return culture.Trim();
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
        public bool IsExternal { get; set; }
        public string Target { get; set; } = "_self";
        public int Order { get; set; }
    }

    private sealed record LinkTargetOption(string Value, string Text);
}
