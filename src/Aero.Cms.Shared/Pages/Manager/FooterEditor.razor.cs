using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NeoUI.Blazor.Primitives;
using Radzen;

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
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private FooterDetail? _selected;
    private SiteViewModel? _currentSite;
    private IReadOnlyList<FooterDetail> _cultureVariants = [];
    private List<FooterComponentEditorModel> _components = [];
    private List<FooterCanvasRowEditorModel> _rows = [];
    private bool _isLoading;
    private bool _isSaving;
    private bool _isLoadingTranslations;
    private bool _isCreatingTranslation;
    private bool _isTranslatingAll;
    private bool _overwriteExistingTranslations;
    protected bool PreviewMode { get; set; }
    private bool RightSidebarCollapsed { get; set; }
    private bool CategoryFooter { get; set; } = true;
    private string SelectedBlockId { get; set; } = string.Empty;
    private HashSet<string> _translatingCultures = new(StringComparer.OrdinalIgnoreCase);
    private string _selectedTranslationCulture = string.Empty;
    private string _editName = string.Empty;
    private string? _editDescription;
    private string _companyName = "Aero CMS";
    private string? _tagline;
    private string? _logoUrl;
    private string? _backgroundImageUrl;
    private decimal _overlayOpacity = 0.35m;
    private string? _copyrightText;
    private List<FooterLinkEditorModel> _legalLinks = [];
    private IReadOnlyList<string> SupportedCultures =>
        _currentSite?.SupportedCultures is { Count: > 0 } cultures
            ? cultures
            : [_selected?.Culture ?? _currentSite?.DefaultCulture ?? "en-US"];
    protected string PreviewFrameDocument => BuildPreviewFrameDocument(BuildPreviewHtml(), Navigation.BaseUri, _editName);
    private static IReadOnlyList<PaletteBlock> FooterPalette { get; } =
    [
        new("linkGroup", "Link Group"),
        new("text", "Text"),
        new("social", "Social Links"),
        new("newsletter", "Newsletter"),
        new("search", "Search"),
        new("spacer", "Spacer")
    ];

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

    private async Task TranslateAllCulturesAsync()
    {
        if (_selected is null || _isTranslatingAll)
        {
            return;
        }

        var existingCultures = _cultureVariants
            .Select(x => NormalizeCultureName(x.Culture))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var targets = SupportedCultures
            .Select(NormalizeCultureName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(culture => !string.Equals(culture, _selected.Culture, StringComparison.OrdinalIgnoreCase))
            .Where(culture => _overwriteExistingTranslations || !existingCultures.Contains(culture))
            .Select(culture => new AiTranslateFooterCultureRequest(culture))
            .ToList();

        if (targets.Count == 0)
        {
            Notify(
                NotificationSeverity.Info,
                _overwriteExistingTranslations
                    ? "There are no other site cultures to translate."
                    : "All enabled cultures already have translations. Enable overwrite to refresh existing translations.");
            return;
        }

        var confirmed = await DialogService.Confirm(
            _overwriteExistingTranslations
                ? "Translate all enabled cultures and overwrite existing localized footer content? Existing variants will become drafts."
                : "Translate all missing enabled cultures for this footer? New variants will be created as drafts.",
            "AI Translate All",
            new ConfirmOptions { OkButtonText = "Translate", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        await SaveDraftAsync();
        await TranslateCulturesAsync(targets, _overwriteExistingTranslations, translateAll: true);
    }

    private Task TranslateCultureAsync(FooterDetail variant)
    {
        if (_selected is null)
        {
            return Task.CompletedTask;
        }

        if (string.Equals(variant.Culture, _selected.Culture, StringComparison.OrdinalIgnoreCase))
        {
            Notify(NotificationSeverity.Info, "Open another culture variant and translate from that source if needed.");
            return Task.CompletedTask;
        }

        return TranslateCulturesAsync(
            [new AiTranslateFooterCultureRequest(variant.Culture)],
            overwriteExisting: true,
            translateAll: false);
    }

    private async Task TranslateCulturesAsync(
        IReadOnlyList<AiTranslateFooterCultureRequest> targets,
        bool overwriteExisting,
        bool translateAll)
    {
        if (_selected is null || targets.Count == 0)
        {
            return;
        }

        if (translateAll)
        {
            _isTranslatingAll = true;
        }

        foreach (var target in targets)
        {
            _translatingCultures.Add(target.Culture);
        }

        try
        {
            var request = new AiTranslateFooterRequest(targets, ProviderId: null, overwriteExisting);
            var result = await FootersClient.TranslateWithAiAsync(_selected.Id, request);

            if (result is Result<AiTranslateFooterResult, AeroError>.Ok ok)
            {
                var succeeded = ok.Value.Results.Count(x => x.Succeeded);
                var failed = ok.Value.Results.Count - succeeded;

                if (succeeded > 0)
                {
                    Notify(
                        failed == 0 ? NotificationSeverity.Success : NotificationSeverity.Info,
                        failed == 0
                            ? $"Translated {succeeded} culture{(succeeded == 1 ? string.Empty : "s")}"
                            : $"Translated {succeeded} culture{(succeeded == 1 ? string.Empty : "s")}; {failed} failed");

                    await LoadTranslationsAsync();
                }

                foreach (var failure in ok.Value.Results.Where(x => !x.Succeeded))
                {
                    Notify(NotificationSeverity.Error, $"{FormatCulture(failure.Culture)} translation failed", failure.Error);
                }

                return;
            }

            if (result is Result<AiTranslateFooterResult, AeroError>.Failure apiFailure)
            {
                Notify(NotificationSeverity.Error, "AI translation failed", apiFailure.Error.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to AI translate footer {FooterId}", _selected.Id);
            Notify(NotificationSeverity.Error, "AI translation failed", ex.Message);
        }
        finally
        {
            if (translateAll)
            {
                _isTranslatingAll = false;
            }

            foreach (var target in targets)
            {
                _translatingCultures.Remove(target.Culture);
            }
        }
    }

    private void AddLegalLink()
    {
        var nextOrder = _legalLinks.Count == 0 ? 0 : _legalLinks.Max(x => x.Order) + 1;
        _legalLinks.Add(new FooterLinkEditorModel { Label = "New Link", Href = "/", Order = nextOrder });
        NormalizeLegalLinkOrders();
    }

    private void RemoveLegalLink(FooterLinkEditorModel link)
    {
        _legalLinks = _legalLinks.Where(x => !ReferenceEquals(x, link)).ToList();
        NormalizeLegalLinkOrders();
    }

    private void NormalizeLegalLinkOrders()
    {
        for (var i = 0; i < _legalLinks.Count; i++)
        {
            _legalLinks[i].Order = i;
        }
    }

    private async Task SaveDraftAsync()
    {
        await SaveDraftCoreAsync(notifySuccess: true);
    }

    private async Task<FooterDetail?> SaveDraftCoreAsync(bool notifySuccess)
    {
        if (_selected is null)
        {
            return null;
        }

        var validation = ValidateEditor();
        if (validation is not null)
        {
            Notify(NotificationSeverity.Warning, "Draft was not saved", validation);
            return null;
        }

        _isSaving = true;
        try
        {
            var request = BuildUpdateFooterRequest();

            var result = await FootersClient.SaveDraftAsync(_selected.Id, request, _selected.Version);
            if (result is Result<FooterDetail, AeroError>.Failure staleFailure && IsConflict(staleFailure.Error))
            {
                result = await FootersClient.SaveDraftAsync(_selected.Id, request, expectedVersion: 0);
            }

            if (result is Result<FooterDetail, AeroError>.Ok ok)
            {
                SetSelected(ok.Value);
                if (notifySuccess)
                {
                    Notify(NotificationSeverity.Success, "Draft saved");
                }

                return ok.Value;
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

        return null;
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
            var saved = await SaveDraftCoreAsync(notifySuccess: false);
            if (saved is null)
            {
                return;
            }

            var result = await FootersClient.PublishAsync(saved.Id, saved.Version);
            if (result is Result<FooterDetail, AeroError>.Failure staleFailure && IsConflict(staleFailure.Error))
            {
                result = await FootersClient.PublishAsync(saved.Id, expectedVersion: 0);
            }

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

    private void TogglePreview()
        => PreviewMode = !PreviewMode;

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
        _components = detail.Components
            .OrderBy(x => x.Order)
            .Select(MapComponent)
            .ToList();
        _rows = detail.Rows.Count > 0
            ? detail.Rows.OrderBy(x => x.Order).Select(MapRow).ToList()
            : BuildDefaultRows(_components);
        _legalLinks = detail.LegalLinks
            .Select(x => new FooterLinkEditorModel
            {
                Id = x.Id,
                Label = x.Label,
                Href = x.Href,
                Order = x.Order,
                OpenInNewTab = x.OpenInNewTab
            })
            .ToList();
        NormalizeLegalLinkOrders();
        NormalizeComponentOrders();
        NormalizeRowOrders();
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
        _components = [];
        _rows = [];
        _legalLinks = [];
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

        var invalidLegalLink = _legalLinks.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.Label) || string.IsNullOrWhiteSpace(x.Href));
        if (invalidLegalLink is not null)
        {
            return "Every legal link needs a label and URL.";
        }

        var invalidComponent = CanvasBlocks.FirstOrDefault(x => !IsValidComponent(x));
        return invalidComponent is null ? null : $"Canvas block '{invalidComponent.DisplayName}' is missing required content.";
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

    private void NormalizeComponentOrders()
    {
        foreach (var bucket in _components.GroupBy(x => x.Placement, StringComparer.OrdinalIgnoreCase))
        {
            var order = 0;
            foreach (var component in bucket.OrderBy(x => x.Order))
            {
                component.Order = order++;
            }
        }
    }

    private UpdateFooterRequest BuildUpdateFooterRequest()
        => new(
            _editName.Trim(),
            _editDescription?.Trim(),
            _companyName.Trim(),
            [],
            _tagline?.Trim(),
            _logoUrl?.Trim(),
            _backgroundImageUrl?.Trim(),
            _overlayOpacity,
            _copyrightText?.Trim(),
            _legalLinks.Select(x => new UpdateFooterLinkRequest(x.Id, x.Label.Trim(), x.Href.Trim(), x.Order, x.OpenInNewTab)).ToList(),
            BuildFooterComponents(),
            BuildFooterRows());

    private static bool IsConflict(AeroError error)
        => error is AeroError.Conflict;

    private void NormalizeRowOrders()
    {
        for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
        {
            var row = _rows[rowIndex];
            row.Order = rowIndex;
            for (var columnIndex = 0; columnIndex < row.Columns.Count; columnIndex++)
            {
                var column = row.Columns[columnIndex];
                column.Order = columnIndex;
                for (var blockIndex = 0; blockIndex < column.Blocks.Count; blockIndex++)
                {
                    column.Blocks[blockIndex].Order = blockIndex;
                }
            }
        }
    }

    private void AddRow()
    {
        _rows.Add(CreateRow(_rows.Count));
        NormalizeRowOrders();
    }

    private void RemoveRow(FooterCanvasRowEditorModel row)
    {
        _rows = _rows.Where(x => !ReferenceEquals(x, row)).ToList();
        NormalizeRowOrders();
    }

    private void MoveRow(FooterCanvasRowEditorModel row, int direction)
    {
        var ordered = _rows.OrderBy(x => x.Order).ToList();
        var index = ordered.FindIndex(x => ReferenceEquals(x, row));
        var target = index + direction;
        if (index < 0 || target < 0 || target >= ordered.Count)
        {
            return;
        }

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        _rows = ordered;
        NormalizeRowOrders();
    }

    private void DuplicateRow(FooterCanvasRowEditorModel row)
    {
        var ordered = _rows.OrderBy(x => x.Order).ToList();
        var index = ordered.FindIndex(x => ReferenceEquals(x, row));
        if (index < 0)
        {
            return;
        }

        ordered.Insert(index + 1, CloneRow(row));
        _rows = ordered;
        NormalizeRowOrders();
    }

    private void AddColumn(FooterCanvasRowEditorModel row)
    {
        var span = row.Columns.Count switch
        {
            0 => 12,
            1 => 6,
            2 => 4,
            _ => 3
        };

        row.Columns.Add(new FooterCanvasColumnEditorModel
        {
            Order = row.Columns.Count,
            DesktopSpan = span,
            TabletSpan = Math.Min(12, span * 2),
            MobileSpan = 12
        });
        NormalizeRowOrders();
    }

    private void RemoveColumn(FooterCanvasRowEditorModel row, FooterCanvasColumnEditorModel column)
    {
        row.Columns = row.Columns.Where(x => !ReferenceEquals(x, column)).ToList();
        if (row.Columns.Count == 0)
        {
            AddColumn(row);
        }

        NormalizeRowOrders();
    }

    private void RemoveBlockFromColumn(FooterCanvasColumnEditorModel column, FooterComponentEditorModel block)
    {
        column.Blocks = column.Blocks.Where(x => !ReferenceEquals(x, block)).ToList();
        if (SelectedBlockId == block.ClientId)
        {
            SelectedBlockId = string.Empty;
        }

        NormalizeRowOrders();
    }

    private void OnRowsReordered(IList<FooterCanvasRowEditorModel> rows)
    {
        _rows = rows.ToList();
        NormalizeRowOrders();
    }

    private void OnColumnBlocksReordered(FooterCanvasColumnEditorModel column, IList<FooterComponentEditorModel> blocks)
    {
        column.Blocks = blocks.ToList();
        NormalizeRowOrders();
    }

    private void OnPaletteTransferredToColumn(FooterCanvasColumnEditorModel column, SortableTransferArgs args)
    {
        var block = CreateComponent(args.ActiveId, "Main");
        var index = Math.Clamp(args.Index, 0, column.Blocks.Count);
        column.Blocks.Insert(index, block);
        SelectedBlockId = block.ClientId;
        NormalizeRowOrders();
    }

    private void ToggleCategory(string category)
    {
        if (string.Equals(category, "footer", StringComparison.OrdinalIgnoreCase))
        {
            CategoryFooter = !CategoryFooter;
        }
    }

    private void SelectBlock(string clientId)
        => SelectedBlockId = SelectedBlockId == clientId ? string.Empty : clientId;

    private void AddComponentLink(FooterComponentEditorModel component)
    {
        component.Links.Add(new FooterLinkEditorModel { Label = "Link", Href = "/", Order = component.Links.Count });
    }

    private void RemoveComponentLink(FooterComponentEditorModel component, FooterLinkEditorModel link)
    {
        component.Links = component.Links.Where(x => !ReferenceEquals(x, link)).ToList();
        for (var i = 0; i < component.Links.Count; i++)
        {
            component.Links[i].Order = i;
        }
    }

    private void AddSocialLink(FooterComponentEditorModel component)
    {
        component.SocialLinks.Add(new FooterSocialLinkEditorModel { Platform = "LinkedIn", Href = "https://" });
    }

    private void RemoveSocialLink(FooterComponentEditorModel component, FooterSocialLinkEditorModel link)
        => component.SocialLinks = component.SocialLinks.Where(x => !ReferenceEquals(x, link)).ToList();

    private static FooterComponentEditorModel CreateComponent(string kind, string placement)
    {
        var normalizedKind = kind.Trim().ToLowerInvariant();
        return new FooterComponentEditorModel
        {
            Kind = normalizedKind,
            Placement = placement,
            Title = normalizedKind == "linkgroup" ? "Links" : null,
            Text = normalizedKind == "text" ? "Footer text" : null,
            EndpointKey = normalizedKind == "newsletter" ? "default" : null,
            Placeholder = normalizedKind is "newsletter" ? "Email address" : normalizedKind == "search" ? "Search..." : null,
            ButtonLabel = normalizedKind == "newsletter" ? "Subscribe" : null,
            SearchAction = normalizedKind == "search" ? "/search" : null,
            SizeToken = normalizedKind == "spacer" ? "md" : null,
            Order = 0
        };
    }

    private IReadOnlyList<UpdateFooterComponentRequest> BuildFooterComponents()
        => CanvasBlocks
            .OrderBy(x => PlacementOrder(x.Placement))
            .ThenBy(x => x.Order)
            .Select(x => new UpdateFooterComponentRequest(
                x.Id,
                x.Kind,
                x.Order,
                x.Placement,
                x.Title?.Trim(),
                x.Text?.Trim(),
                x.Links.OrderBy(link => link.Order)
                    .Select(link => new UpdateFooterLinkRequest(link.Id, link.Label.Trim(), link.Href.Trim(), link.Order, link.OpenInNewTab))
                    .ToList(),
                x.SocialLinks.Select(link => new FooterSocialLinkDetail(link.Platform.Trim(), link.Href.Trim())).ToList(),
                x.EndpointKey?.Trim(),
                x.Placeholder?.Trim(),
                x.ButtonLabel?.Trim(),
                x.SearchAction?.Trim(),
                x.SizeToken?.Trim()))
            .ToList();

    private IReadOnlyList<UpdateFooterCanvasRowRequest> BuildFooterRows()
        => _rows
            .OrderBy(x => x.Order)
            .Select(row => new UpdateFooterCanvasRowRequest(
                row.Id,
                row.Order,
                row.Label?.Trim(),
                row.DesktopDisplay,
                row.TabletDisplay,
                row.MobileDisplay,
                row.Columns.OrderBy(column => column.Order)
                    .Select(column => new UpdateFooterCanvasColumnRequest(
                        column.Id,
                        column.Order,
                        Math.Clamp(column.DesktopSpan, 1, 12),
                        Math.Clamp(column.TabletSpan, 1, 12),
                        Math.Clamp(column.MobileSpan, 1, 12),
                        column.Blocks.OrderBy(block => block.Order)
                            .Select(block => new UpdateFooterComponentRequest(
                                block.Id,
                                block.Kind,
                                block.Order,
                                block.Placement,
                                block.Title?.Trim(),
                                block.Text?.Trim(),
                                block.Links.OrderBy(link => link.Order)
                                    .Select(link => new UpdateFooterLinkRequest(link.Id, link.Label.Trim(), link.Href.Trim(), link.Order, link.OpenInNewTab))
                                    .ToList(),
                                block.SocialLinks.Select(link => new FooterSocialLinkDetail(link.Platform.Trim(), link.Href.Trim())).ToList(),
                                block.EndpointKey?.Trim(),
                                block.Placeholder?.Trim(),
                                block.ButtonLabel?.Trim(),
                                block.SearchAction?.Trim(),
                                block.SizeToken?.Trim()))
                            .ToList()))
                    .ToList()))
            .ToList();

    private static FooterComponentEditorModel MapComponent(FooterComponentDetail component)
        => new()
        {
            Id = component.Id,
            Kind = component.Kind,
            Placement = component.Placement,
            Order = component.Order,
            Title = component.Title,
            Text = component.Text,
            Links = component.Links.OrderBy(x => x.Order).Select(x => new FooterLinkEditorModel
            {
                Id = x.Id,
                Label = x.Label,
                Href = x.Href,
                Order = x.Order,
                OpenInNewTab = x.OpenInNewTab
            }).ToList(),
            SocialLinks = component.SocialLinks.Select(x => new FooterSocialLinkEditorModel
            {
                Platform = x.Platform,
                Href = x.Href
            }).ToList(),
            EndpointKey = component.EndpointKey,
            Placeholder = component.Placeholder,
            ButtonLabel = component.ButtonLabel,
            SearchAction = component.SearchAction,
            SizeToken = component.SizeToken
        };

    private static FooterCanvasRowEditorModel MapRow(FooterCanvasRowDetail row)
        => new()
        {
            Id = row.Id,
            Order = row.Order,
            Label = row.Label,
            DesktopDisplay = row.DesktopDisplay,
            TabletDisplay = row.TabletDisplay,
            MobileDisplay = row.MobileDisplay,
            Columns = row.Columns.OrderBy(x => x.Order).Select(column => new FooterCanvasColumnEditorModel
            {
                Id = column.Id,
                Order = column.Order,
                DesktopSpan = column.DesktopSpan,
                TabletSpan = column.TabletSpan,
                MobileSpan = column.MobileSpan,
                Blocks = column.Blocks.OrderBy(block => block.Order).Select(MapComponent).ToList()
            }).ToList()
        };

    private static FooterCanvasRowEditorModel CloneRow(FooterCanvasRowEditorModel row)
        => new()
        {
            Label = string.IsNullOrWhiteSpace(row.Label) ? "Footer row" : $"{row.Label} copy",
            DesktopDisplay = row.DesktopDisplay,
            TabletDisplay = row.TabletDisplay,
            MobileDisplay = row.MobileDisplay,
            Columns = row.Columns.OrderBy(x => x.Order).Select(CloneColumn).ToList()
        };

    private static FooterCanvasColumnEditorModel CloneColumn(FooterCanvasColumnEditorModel column)
        => new()
        {
            DesktopSpan = column.DesktopSpan,
            TabletSpan = column.TabletSpan,
            MobileSpan = column.MobileSpan,
            Blocks = column.Blocks.OrderBy(x => x.Order).Select(CloneComponent).ToList()
        };

    private static FooterComponentEditorModel CloneComponent(FooterComponentEditorModel component)
        => new()
        {
            Kind = component.Kind,
            Placement = component.Placement,
            Order = component.Order,
            Title = component.Title,
            Text = component.Text,
            Links = component.Links.OrderBy(x => x.Order).Select(x => new FooterLinkEditorModel
            {
                Label = x.Label,
                Href = x.Href,
                Order = x.Order,
                OpenInNewTab = x.OpenInNewTab
            }).ToList(),
            SocialLinks = component.SocialLinks.Select(x => new FooterSocialLinkEditorModel
            {
                Platform = x.Platform,
                Href = x.Href
            }).ToList(),
            EndpointKey = component.EndpointKey,
            Placeholder = component.Placeholder,
            ButtonLabel = component.ButtonLabel,
            SearchAction = component.SearchAction,
            SizeToken = component.SizeToken
        };

    private static List<FooterCanvasRowEditorModel> BuildDefaultRows(IReadOnlyList<FooterComponentEditorModel> components)
    {
        var row = CreateRow(0);
        row.Columns[0].Blocks = components.Where(x => string.Equals(x.Placement, "Brand", StringComparison.OrdinalIgnoreCase)).ToList();
        row.Columns[1].Blocks = components.Where(x => string.Equals(x.Placement, "Main", StringComparison.OrdinalIgnoreCase)).ToList();
        row.Columns[2].Blocks = components.Where(x => string.Equals(x.Placement, "Utility", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Placement, "Bottom", StringComparison.OrdinalIgnoreCase)).ToList();
        return [row];
    }

    private static FooterCanvasRowEditorModel CreateRow(int order)
        => new()
        {
            Label = order == 0 ? "Footer row" : $"Footer row {order + 1}",
            Order = order,
            Columns =
            [
                new() { Order = 0, DesktopSpan = 4, TabletSpan = 6, MobileSpan = 12 },
                new() { Order = 1, DesktopSpan = 4, TabletSpan = 6, MobileSpan = 12 },
                new() { Order = 2, DesktopSpan = 4, TabletSpan = 12, MobileSpan = 12 }
            ]
        };

    private static bool IsValidComponent(FooterComponentEditorModel component)
        => component.Kind.Trim().ToLowerInvariant() switch
        {
            "text" => !string.IsNullOrWhiteSpace(component.Text),
            "social" or "sociallinks" => component.SocialLinks.All(x => !string.IsNullOrWhiteSpace(x.Platform) && !string.IsNullOrWhiteSpace(x.Href)),
            "newsletter" => !string.IsNullOrWhiteSpace(component.EndpointKey),
            "search" => !string.IsNullOrWhiteSpace(component.SearchAction),
            "spacer" => true,
            _ => !string.IsNullOrWhiteSpace(component.Title)
                 && component.Links.All(x => !string.IsNullOrWhiteSpace(x.Label) && !string.IsNullOrWhiteSpace(x.Href))
        };

    private static int PlacementOrder(string? placement)
        => placement?.ToLowerInvariant() switch
        {
            "brand" => 0,
            "utility" => 2,
            "bottom" => 3,
            _ => 1
        };

    private IReadOnlyList<FooterComponentEditorModel> CanvasBlocks
        => _rows.Count > 0
            ? _rows
                .OrderBy(row => row.Order)
                .SelectMany(row => row.Columns.OrderBy(column => column.Order))
                .SelectMany(column => column.Blocks.OrderBy(block => block.Order))
                .ToList()
            : _components;

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

    private string BuildPreviewHtml()
    {
        var builder = new StringBuilder();
        var background = string.IsNullOrWhiteSpace(_backgroundImageUrl)
            ? string.Empty
            : $" style=\"background-image:url('{Encode(_backgroundImageUrl)}');background-size:cover;background-position:center;\"";

        builder.Append(CultureInfo.InvariantCulture, $"<footer class=\"relative overflow-hidden bg-slate-950 text-slate-100\"{background}>");
        if (!string.IsNullOrWhiteSpace(_backgroundImageUrl))
        {
            builder.Append(CultureInfo.InvariantCulture, $"<div class=\"absolute inset-0 bg-slate-950\" style=\"opacity:{_overlayOpacity.ToString(CultureInfo.InvariantCulture)}\"></div>");
        }

        builder.Append("<div class=\"relative mx-auto max-w-7xl px-4 py-10 sm:px-6 lg:px-8\">");
        builder.Append("<div class=\"grid gap-8 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,2fr)]\">");
        builder.Append("<div class=\"space-y-4\">");
        builder.Append("<a href=\"/\" class=\"inline-flex items-center gap-3\">");
        if (!string.IsNullOrWhiteSpace(_logoUrl))
        {
            builder.Append(CultureInfo.InvariantCulture, $"<img src=\"{Encode(_logoUrl)}\" alt=\"{Encode(_companyName)} logo\" class=\"h-10 max-w-48 object-contain\" />");
        }
        else
        {
            builder.Append("<span class=\"flex h-10 w-10 items-center justify-center rounded bg-white text-sm font-black text-slate-950\">A</span>");
        }

        builder.Append(CultureInfo.InvariantCulture, $"<span class=\"text-lg font-bold tracking-tight text-white\">{Encode(_companyName)}</span>");
        builder.Append("</a>");
        if (!string.IsNullOrWhiteSpace(_tagline))
        {
            builder.Append(CultureInfo.InvariantCulture, $"<p class=\"max-w-sm text-sm leading-6 text-slate-300\">{Encode(_tagline)}</p>");
        }

        foreach (var component in CanvasBlocks.Where(x => string.Equals(x.Placement, "Brand", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Order))
        {
            AppendPreviewComponent(builder, component);
        }

        builder.Append("</div>");
        builder.Append("<div class=\"grid gap-6 sm:grid-cols-2 lg:grid-cols-3\">");
        foreach (var component in CanvasBlocks.Where(x => string.Equals(x.Placement, "Main", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Order))
        {
            AppendPreviewComponent(builder, component);
        }

        builder.Append("</div></div>");
        foreach (var component in CanvasBlocks.Where(x => string.Equals(x.Placement, "Utility", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Order))
        {
            AppendPreviewComponent(builder, component);
        }

        builder.Append("<div class=\"mt-8 flex flex-col gap-4 border-t border-white/10 pt-6 sm:flex-row sm:items-center sm:justify-between\">");
        builder.Append(CultureInfo.InvariantCulture, $"<p class=\"text-sm text-slate-400\">{Encode(string.IsNullOrWhiteSpace(_copyrightText) ? $"{_companyName}. All rights reserved." : _copyrightText)}</p>");
        if (_legalLinks.Count > 0)
        {
            builder.Append("<div class=\"flex flex-wrap items-center gap-4\">");
            foreach (var link in _legalLinks.OrderBy(x => x.Order))
            {
                AppendLink(builder, link.Label, link.Href, link.OpenInNewTab);
            }

            builder.Append("</div>");
        }

        builder.Append("</div></div></footer>");
        return builder.ToString();
    }

    private static string BuildPreviewFrameDocument(string? html, string baseUri, string title)
    {
        var content = string.IsNullOrWhiteSpace(html)
            ? "<main class=\"pe-empty-state\"><h3>No preview content</h3></main>"
            : html;
        var root = new Uri(baseUri);
        var appCss = new Uri(root, "_content/Aero.Cms.Shared/app.css");
        var managerCss = new Uri(root, "_content/Aero.Cms.Shared/aero-manager.css");
        var radzenCss = new Uri(root, "_content/Radzen.Blazor/css/standard-base.css");
        var aeroCss = new Uri(root, "css/aero.css");

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <base href="{{baseUri}}">
                <title>{{HtmlEncoder.Default.Encode(title)}}</title>
                <link rel="preconnect" href="https://fonts.googleapis.com">
                <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
                <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800;900&display=swap" rel="stylesheet">
                <link rel="stylesheet" href="{{appCss}}">
                <link rel="stylesheet" href="{{managerCss}}">
                <link rel="stylesheet" href="{{radzenCss}}">
                <script src="https://cdn.jsdelivr.net/npm/@tailwindcss/browser@4"></script>
                <style type="text/tailwindcss" src="{{aeroCss}}"></style>
                <style>
                    html, body { margin: 0; min-height: 100%; background: #fff; }
                    body { font-family: Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
                    .aero-preview-document { min-height: 100vh; overflow-x: hidden; }
                </style>
            </head>
            <body>
                <main class="aero-preview-document">
                    {{content}}
                </main>
            </body>
            </html>
            """;
    }

    private static void AppendPreviewComponent(StringBuilder builder, FooterComponentEditorModel component)
    {
        switch (component.Kind.Trim().ToLowerInvariant())
        {
            case "text":
                builder.Append(CultureInfo.InvariantCulture, $"<p class=\"text-sm leading-6 text-slate-300\">{Encode(component.Text)}</p>");
                break;
            case "social":
            case "sociallinks":
                builder.Append("<div class=\"flex flex-wrap gap-3\">");
                foreach (var social in component.SocialLinks)
                {
                    builder.Append(CultureInfo.InvariantCulture, $"<a href=\"{Encode(social.Href)}\" class=\"text-sm text-slate-300 hover:text-white\">{Encode(social.Platform)}</a>");
                }

                builder.Append("</div>");
                break;
            case "newsletter":
                builder.Append("<form class=\"mt-4 flex max-w-md gap-2\">");
                builder.Append(CultureInfo.InvariantCulture, $"<input class=\"min-w-0 flex-1 rounded border border-white/20 bg-white/10 px-3 py-2 text-sm text-white\" placeholder=\"{Encode(component.Placeholder ?? "Email address")}\" />");
                builder.Append(CultureInfo.InvariantCulture, $"<button class=\"rounded bg-white px-3 py-2 text-sm font-semibold text-slate-950\" type=\"button\">{Encode(component.ButtonLabel ?? "Subscribe")}</button>");
                builder.Append("</form>");
                break;
            case "search":
                builder.Append(CultureInfo.InvariantCulture, $"<form action=\"{Encode(component.SearchAction ?? "/search")}\" class=\"mt-4\"><input class=\"w-full max-w-md rounded border border-white/20 bg-white/10 px-3 py-2 text-sm text-white\" placeholder=\"{Encode(component.Placeholder ?? "Search...")}\" /></form>");
                break;
            case "spacer":
                builder.Append("<div class=\"h-6\"></div>");
                break;
            default:
                builder.Append("<nav class=\"space-y-3\">");
                builder.Append(CultureInfo.InvariantCulture, $"<h2 class=\"text-sm font-semibold uppercase tracking-wider text-white\">{Encode(component.Title)}</h2>");
                builder.Append("<ul class=\"space-y-2\">");
                foreach (var link in component.Links.OrderBy(x => x.Order))
                {
                    builder.Append("<li>");
                    AppendLink(builder, link.Label, link.Href, link.OpenInNewTab);
                    builder.Append("</li>");
                }

                builder.Append("</ul></nav>");
                break;
        }
    }

    private static void AppendLink(StringBuilder builder, string label, string href, bool openInNewTab)
    {
        var target = openInNewTab ? " target=\"_blank\" rel=\"noopener noreferrer\"" : string.Empty;
        builder.Append(CultureInfo.InvariantCulture, $"<a href=\"{Encode(href)}\" class=\"text-sm text-slate-300 hover:text-white\"{target}>{Encode(label)}</a>");
    }

    private static string Encode(string? value)
        => HtmlEncoder.Default.Encode(value ?? string.Empty);

    private string ColumnGridStyle(FooterCanvasColumnEditorModel column)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"--aero-mobile-span:{Math.Clamp(column.MobileSpan, 1, 12)};--aero-tablet-span:{Math.Clamp(column.TabletSpan, 1, 12)};--aero-desktop-span:{Math.Clamp(column.DesktopSpan, 1, 12)};");

    private RenderFragment RenderFooterBlockFields(FooterComponentEditorModel component) => builder =>
    {
        var sequence = 0;
        void Open(string element, string? css = null)
        {
            builder.OpenElement(sequence++, element);
            if (!string.IsNullOrWhiteSpace(css))
            {
                builder.AddAttribute(sequence++, "class", css);
            }
        }

        void Close() => builder.CloseElement();

        void TextInput(string? value, string placeholder, Action<string?> update)
        {
            Open("input", "w-full rounded px-3 py-2 text-sm");
            builder.AddAttribute(sequence++, "style", "border:1px solid var(--pe-border);background:var(--pe-bg-primary);color:var(--pe-text-primary);");
            builder.AddAttribute(sequence++, "value", value);
            builder.AddAttribute(sequence++, "placeholder", placeholder);
            builder.AddAttribute(sequence++, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, e => update(e.Value?.ToString())));
            Close();
        }

        void TextArea(string? value, string placeholder, Action<string?> update)
        {
            Open("textarea", "w-full rounded px-3 py-2 text-sm");
            builder.AddAttribute(sequence++, "style", "border:1px solid var(--pe-border);background:var(--pe-bg-primary);color:var(--pe-text-primary);");
            builder.AddAttribute(sequence++, "rows", 3);
            builder.AddAttribute(sequence++, "value", value);
            builder.AddAttribute(sequence++, "placeholder", placeholder);
            builder.AddAttribute(sequence++, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, e => update(e.Value?.ToString())));
            Close();
        }

        Open("div", "grid grid-cols-1 gap-2");
        switch (component.Kind.Trim().ToLowerInvariant())
        {
            case "text":
                TextArea(component.Text, L["Text"], value => component.Text = value);
                break;
            case "social":
            case "sociallinks":
                foreach (var social in component.SocialLinks)
                {
                    Open("div", "grid grid-cols-2 gap-2");
                    TextInput(social.Platform, L["Platform"], value => social.Platform = value ?? string.Empty);
                    TextInput(social.Href, "https://", value => social.Href = value ?? string.Empty);
                    Close();
                }

                Open("button", "pe-btn pe-btn-ghost pe-btn-sm");
                builder.AddAttribute(sequence++, "type", "button");
                builder.AddAttribute(sequence++, "onclick", EventCallback.Factory.Create(this, () => AddSocialLink(component)));
                builder.AddContent(sequence++, L["Add Social"]);
                Close();
                break;
            case "newsletter":
                TextInput(component.EndpointKey, L["Endpoint key"], value => component.EndpointKey = value);
                TextInput(component.Placeholder, L["Placeholder"], value => component.Placeholder = value);
                TextInput(component.ButtonLabel, L["Button label"], value => component.ButtonLabel = value);
                break;
            case "search":
                TextInput(component.Placeholder, L["Placeholder"], value => component.Placeholder = value);
                TextInput(component.SearchAction, "/search", value => component.SearchAction = value);
                break;
            case "spacer":
                TextInput(component.SizeToken, L["Size token"], value => component.SizeToken = value);
                break;
            default:
                TextInput(component.Title, L["Title"], value => component.Title = value);
                foreach (var link in component.Links.OrderBy(x => x.Order))
                {
                    Open("div", "grid grid-cols-2 gap-2");
                    TextInput(link.Label, L["Label"], value => link.Label = value ?? string.Empty);
                    TextInput(link.Href, "/path", value => link.Href = value ?? string.Empty);
                    Close();
                }

                Open("button", "pe-btn pe-btn-ghost pe-btn-sm");
                builder.AddAttribute(sequence++, "type", "button");
                builder.AddAttribute(sequence++, "onclick", EventCallback.Factory.Create(this, () => AddComponentLink(component)));
                builder.AddContent(sequence++, L["Add Link"]);
                Close();
                break;
        }

        Close();
    };

    protected sealed class FooterLinkEditorModel
    {
        public long Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Href { get; set; } = "/";
        public int Order { get; set; }
        public bool OpenInNewTab { get; set; }
    }

    protected sealed class FooterComponentEditorModel
    {
        public string ClientId { get; } = Guid.NewGuid().ToString("N");
        public long Id { get; set; }
        public string Kind { get; set; } = "linkGroup";
        public string Placement { get; set; } = "Main";
        public int Order { get; set; }
        public string? Title { get; set; }
        public string? Text { get; set; }
        public List<FooterLinkEditorModel> Links { get; set; } = [];
        public List<FooterSocialLinkEditorModel> SocialLinks { get; set; } = [];
        public string? EndpointKey { get; set; }
        public string? Placeholder { get; set; }
        public string? ButtonLabel { get; set; }
        public string? SearchAction { get; set; }
        public string? SizeToken { get; set; }
        public string DisplayName => Kind.Trim().ToLowerInvariant() switch
        {
            "text" => "Text",
            "social" or "sociallinks" => "Social links",
            "newsletter" => "Newsletter",
            "search" => "Search",
            "spacer" => "Spacer",
            _ => Title ?? "Link group"
        };
    }

    protected sealed class FooterSocialLinkEditorModel
    {
        public string Platform { get; set; } = string.Empty;
        public string Href { get; set; } = string.Empty;
    }

    protected sealed class FooterCanvasRowEditorModel
    {
        public string ClientId { get; } = Guid.NewGuid().ToString("N");
        public long Id { get; set; }
        public int Order { get; set; }
        public string? Label { get; set; }
        public string DesktopDisplay { get; set; } = "Grid";
        public string TabletDisplay { get; set; } = "Grid";
        public string MobileDisplay { get; set; } = "Stack";
        public List<FooterCanvasColumnEditorModel> Columns { get; set; } = [];
    }

    protected sealed class FooterCanvasColumnEditorModel
    {
        public string ClientId { get; } = Guid.NewGuid().ToString("N");
        public long Id { get; set; }
        public int Order { get; set; }
        public int DesktopSpan { get; set; } = 4;
        public int TabletSpan { get; set; } = 6;
        public int MobileSpan { get; set; } = 12;
        public List<FooterComponentEditorModel> Blocks { get; set; } = [];
    }

    private sealed record PaletteBlock(string Kind, string Label);
}
