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

public partial class FooterEditor
{
    [Parameter] public long Id { get; set; }

    [Inject] private IFootersHttpClient FootersClient { get; set; } = default!;
    [Inject] private ISitesHttpClient SitesClient { get; set; } = default!;
    [Inject] private ICurrentSiteAccessor CurrentSiteAccessor { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<FooterEditor> Logger { get; set; } = default!;

    private FooterDetail? _selected;
    private SiteViewModel? _currentSite;
    private IReadOnlyList<FooterDetail> _cultureVariants = [];
    private RadzenDataGrid<FooterGroupEditorModel>? _groupsGrid;
    private List<FooterGroupEditorModel> _groups = [];
    private bool _isLoading;
    private bool _isSaving;
    private bool _isLoadingTranslations;
    private bool _isCreatingTranslation;
    private string _selectedTranslationCulture = string.Empty;
    private string _editName = string.Empty;
    private string? _editDescription;
    private string _companyName = "Aero CMS";
    private string? _tagline;
    private string? _logoUrl;
    private string? _backgroundImageUrl;
    private decimal _overlayOpacity = 0.35m;
    private string? _copyrightText;
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

    protected override async Task OnParametersSetAsync()
    {
        await LoadFooterAsync();
    }

    private async Task LoadFooterAsync()
    {
        _isLoading = true;
        try
        {
            _currentSite ??= await ResolveCurrentSiteAsync();
            var result = await FootersClient.GetByIdAsync(Id);
            if (result is Result<FooterDetail, AeroError>.Ok ok)
            {
                SetSelected(ok.Value);
                await LoadTranslationsAsync();
            }
            else if (result is Result<FooterDetail, AeroError>.Failure fail)
            {
                ClearSelection();
                Notify(NotificationSeverity.Error, "Footer failed to load", fail.Error.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load footer {FooterId}", Id);
            ClearSelection();
            Notify(NotificationSeverity.Error, "Footer failed to load", ex.Message);
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
            var result = await FootersClient.ListCultureVariantsAsync(_selected.Id);
            _cultureVariants = result is Result<IReadOnlyList<FooterDetail>, AeroError>.Ok ok
                ? ok.Value.OrderBy(footer => footer.Culture, StringComparer.OrdinalIgnoreCase).ToList()
                : [];

            ResetTranslationDraft();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load footer translations for {FooterId}", _selected.Id);
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
            var request = new ForkFooterCultureRequest(_selectedTranslationCulture);
            var result = await FootersClient.ForkToCultureAsync(_selected.Id, request);
            if (result is Result<FooterDetail, AeroError>.Ok ok)
            {
                Notify(NotificationSeverity.Success, $"Created {FormatCulture(ok.Value.Culture)} translation");
                Navigation.NavigateTo($"/manager/footers/editor/{ok.Value.Id}");
            }
            else if (result is Result<FooterDetail, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, "Translation was not created", fail.Error.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create footer translation {FooterId}", _selected.Id);
            Notify(NotificationSeverity.Error, "Translation was not created", ex.Message);
        }
        finally
        {
            _isCreatingTranslation = false;
        }
    }

    private void OpenTranslation(long footerId)
        => Navigation.NavigateTo($"/manager/footers/editor/{footerId}");

    private async Task AddGroupAsync()
    {
        var nextOrder = _groups.Count == 0 ? 0 : _groups.Max(x => x.Order) + 1;
        _groups = _groups.Append(new FooterGroupEditorModel
        {
            Title = "New Group",
            Order = nextOrder,
            Links = [new FooterLinkEditorModel { Label = "New Link", Href = "/", Order = 0 }]
        }).ToList();
        NormalizeOrders();
        await RefreshGroupsGridAsync();
    }

    private async Task RemoveGroupAsync(FooterGroupEditorModel group)
    {
        _groups = _groups.Where(x => !ReferenceEquals(x, group)).ToList();
        NormalizeOrders();
        await RefreshGroupsGridAsync();
    }

    private async Task MoveGroupAsync(FooterGroupEditorModel group, int direction)
    {
        var current = _groups.IndexOf(group);
        if (current < 0)
        {
            return;
        }

        var target = current + direction;
        if (target < 0 || target >= _groups.Count)
        {
            return;
        }

        _groups.RemoveAt(current);
        _groups.Insert(target, group);
        NormalizeOrders();
        await RefreshGroupsGridAsync();
    }

    private async Task AddLinkAsync(FooterGroupEditorModel group)
    {
        var nextOrder = group.Links.Count == 0 ? 0 : group.Links.Max(x => x.Order) + 1;
        group.Links.Add(new FooterLinkEditorModel { Label = "New Link", Href = "/", Order = nextOrder });
        NormalizeOrders();
        await RefreshGroupsGridAsync();
    }

    private async Task RemoveLinkAsync(FooterGroupEditorModel group, FooterLinkEditorModel link)
    {
        group.Links = group.Links.Where(x => !ReferenceEquals(x, link)).ToList();
        NormalizeOrders();
        await RefreshGroupsGridAsync();
    }

    private async Task RefreshGroupsAsync()
    {
        await LoadFooterAsync();
        if (_selected is not null)
        {
            await RefreshGroupsGridAsync();
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
            var request = new UpdateFooterRequest(
                _editName.Trim(),
                _editDescription?.Trim(),
                _companyName.Trim(),
                _groups.OrderBy(x => x.Order)
                    .Select(x => new UpdateFooterLinkGroupRequest(
                        x.Id,
                        x.Title.Trim(),
                        x.Links.OrderBy(link => link.Order)
                            .Select(link => new UpdateFooterLinkRequest(link.Id, link.Label.Trim(), link.Href.Trim(), link.Order, link.OpenInNewTab))
                            .ToList(),
                        x.Order))
                    .ToList(),
                _tagline?.Trim(),
                _logoUrl?.Trim(),
                _backgroundImageUrl?.Trim(),
                _overlayOpacity,
                _copyrightText?.Trim());

            var result = await FootersClient.SaveDraftAsync(_selected.Id, request, _selected.Version);
            if (result is Result<FooterDetail, AeroError>.Ok ok)
            {
                SetSelected(ok.Value);
                Notify(NotificationSeverity.Success, "Draft saved");
            }
            else if (result is Result<FooterDetail, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, "Draft was not saved", fail.Error.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save footer draft {FooterId}", _selected.Id);
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
            var result = await FootersClient.PublishAsync(_selected.Id, _selected.Version);
            if (result is Result<FooterDetail, AeroError>.Ok ok)
            {
                SetSelected(ok.Value);
                Notify(NotificationSeverity.Success, "Footer published");
            }
            else if (result is Result<FooterDetail, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, "Footer was not published", fail.Error.ToString());
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
            var result = await FootersClient.SetDefaultAsync(_selected.Id);
            if (result is Result<bool, AeroError>.Ok)
            {
                Notify(NotificationSeverity.Success, "Default footer updated");
            }
            else if (result is Result<bool, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, "Default footer was not updated", fail.Error.ToString());
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
            $"Archive '{_selected.Name}'? Published pages will no longer resolve this footer.",
            "Archive Footer",
            new ConfirmOptions { OkButtonText = "Archive", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        _isSaving = true;
        try
        {
            var result = await FootersClient.DeleteAsync(_selected.Id);
            if (result is Result<bool, AeroError>.Ok)
            {
                Notify(NotificationSeverity.Success, "Footer archived");
                BackToList();
            }
            else if (result is Result<bool, AeroError>.Failure fail)
            {
                Notify(NotificationSeverity.Error, "Footer was not archived", fail.Error.ToString());
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void BackToList()
    {
        Navigation.NavigateTo("/manager/footers");
    }

    private void SetSelected(FooterDetail detail)
    {
        _selected = detail;
        _editName = detail.Name;
        _editDescription = detail.Description;
        _companyName = detail.CompanyName;
        _tagline = detail.Tagline;
        _logoUrl = detail.LogoUrl;
        _backgroundImageUrl = detail.BackgroundImageUrl;
        _overlayOpacity = detail.OverlayOpacity;
        _copyrightText = detail.CopyrightText;
        _groups = detail.LinkGroups.OrderBy(x => x.Order)
            .Select(x => new FooterGroupEditorModel
            {
                Id = x.Id,
                Title = x.Title,
                Order = x.Order,
                Links = x.Links.OrderBy(link => link.Order)
                    .Select(link => new FooterLinkEditorModel
                    {
                        Id = link.Id,
                        Label = link.Label,
                        Href = link.Href,
                        Order = link.Order,
                        OpenInNewTab = link.OpenInNewTab
                    })
                    .ToList()
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
        _companyName = "Aero CMS";
        _tagline = null;
        _logoUrl = null;
        _backgroundImageUrl = null;
        _overlayOpacity = 0.35m;
        _copyrightText = null;
        _groups = [];
        ResetTranslationDraft();
    }

    private string? ValidateEditor()
    {
        if (string.IsNullOrWhiteSpace(_editName))
        {
            return "Footer name is required.";
        }

        if (string.IsNullOrWhiteSpace(_companyName))
        {
            return "Company name is required.";
        }

        if (_logoUrl?.Length > 2048 || _backgroundImageUrl?.Length > 2048)
        {
            return "Image URLs cannot be longer than 2048 characters.";
        }

        if (_overlayOpacity is < 0 or > 1)
        {
            return "Overlay opacity must be between 0 and 1.";
        }

        var invalidGroup = _groups.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.Title));
        if (invalidGroup is not null)
        {
            return "Every link group needs a title.";
        }

        var invalidLink = _groups.SelectMany(x => x.Links).FirstOrDefault(x => string.IsNullOrWhiteSpace(x.Label) || string.IsNullOrWhiteSpace(x.Href));
        return invalidLink is null ? null : "Every link needs a label and URL.";
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

    private void NormalizeOrders()
    {
        for (var i = 0; i < _groups.Count; i++)
        {
            _groups[i].Order = i;
            for (var j = 0; j < _groups[i].Links.Count; j++)
            {
                _groups[i].Links[j].Order = j;
            }
        }
    }

    private async Task RefreshGroupsGridAsync()
    {
        _groups = _groups.OrderBy(x => x.Order).ToList();
        if (_groupsGrid is not null)
        {
            await _groupsGrid.Reload();
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

    protected sealed class FooterGroupEditorModel
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<FooterLinkEditorModel> Links { get; set; } = [];
    }

    protected sealed class FooterLinkEditorModel
    {
        public long Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Href { get; set; } = "/";
        public int Order { get; set; }
        public bool OpenInNewTab { get; set; }
    }
}
