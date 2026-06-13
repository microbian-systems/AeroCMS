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

public partial class NavMenuEditor
{
    [Parameter] public long Id { get; set; }

    [Inject] private INavigationsHttpClient NavigationsClient { get; set; } = default!;
    [Inject] private ISitesHttpClient SitesClient { get; set; } = default!;
    [Inject] private ICurrentSiteAccessor CurrentSiteAccessor { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<NavMenuEditor> Logger { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private NavigationDetail? _selected;
    private SiteViewModel? _currentSite;
    private IReadOnlyList<NavigationDetail> _cultureVariants = [];
    private List<NavComponentEditorModel> _components = [];
    private List<NavCanvasRowEditorModel> _rows = [];
    private bool _isLoading;
    private bool _isSaving;
    private bool _isLoadingTranslations;
    private bool _isCreatingTranslation;
    private bool _isTranslatingAll;
    private bool _overwriteExistingTranslations;
    protected bool PreviewMode { get; set; }
    private bool RightSidebarCollapsed { get; set; }
    private bool CategoryHeader { get; set; } = true;
    private string SelectedBlockId { get; set; } = string.Empty;
    private HashSet<string> _translatingCultures = new(StringComparer.OrdinalIgnoreCase);
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
    protected string PreviewFrameDocument => BuildPreviewFrameDocument(BuildPreviewHtml(), Navigation.BaseUri, _editName);
    private static IReadOnlyList<PaletteBlock> HeaderPalette { get; } =
    [
        new("link", "Link"),
        new("menu", "Menu"),
        new("language", "Language Select"),
        new("login", "Login Button"),
        new("register", "Register Button"),
        new("search", "Search Area"),
        new("html", "Rich Menu")
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
            .Select(culture => new AiTranslateNavigationCultureRequest(culture))
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
                ? "Translate all enabled cultures and overwrite existing localized header content? Existing variants will become drafts."
                : "Translate all missing enabled cultures for this header menu? New variants will be created as drafts.",
            "AI Translate All",
            new ConfirmOptions { OkButtonText = "Translate", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        await SaveDraftAsync();
        await TranslateCulturesAsync(targets, _overwriteExistingTranslations, translateAll: true);
    }

    private Task TranslateCultureAsync(NavigationDetail variant)
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
            [new AiTranslateNavigationCultureRequest(variant.Culture)],
            overwriteExisting: true,
            translateAll: false);
    }

    private async Task TranslateCulturesAsync(
        IReadOnlyList<AiTranslateNavigationCultureRequest> targets,
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
            var request = new AiTranslateNavigationRequest(targets, ProviderId: null, overwriteExisting);
            var result = await NavigationsClient.TranslateWithAiAsync(_selected.Id, request);

            if (result is Result<AiTranslateNavigationResult, AeroError>.Ok ok)
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

            if (result is Result<AiTranslateNavigationResult, AeroError>.Failure apiFailure)
            {
                Notify(NotificationSeverity.Error, "AI translation failed", apiFailure.Error.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to AI translate header menu {MenuId}", _selected.Id);
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
                [],
                _editSiteLogoUrl?.Trim(),
                BuildNavigationComponents(),
                BuildNavigationRows());

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

    private void TogglePreview()
        => PreviewMode = !PreviewMode;

    private void SetSelected(NavigationDetail detail)
    {
        _selected = detail;
        _editName = detail.Name;
        _editDescription = detail.Title;
        _editSiteLogoUrl = detail.SiteLogoUrl;
        _components = detail.Components
            .OrderBy(x => x.Order)
            .Select(MapComponent)
            .ToList();
        _rows = detail.Rows.Count > 0
            ? detail.Rows.OrderBy(x => x.Order).Select(MapRow).ToList()
            : BuildDefaultRows(_components);
        NormalizeComponentOrders();
        NormalizeRowOrders();
    }

    private void ClearSelection()
    {
        _selected = null;
        _cultureVariants = [];
        _editName = string.Empty;
        _editDescription = null;
        _editSiteLogoUrl = null;
        _components = [];
        _rows = [];
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

        var invalidComponent = CanvasBlocks.FirstOrDefault(x => !IsValidComponent(x));
        return invalidComponent is null ? null : $"Canvas block '{invalidComponent.DisplayName}' is missing required content.";
    }

    private void NormalizeComponentOrders()
    {
        var buckets = _components
            .GroupBy(x => x.Alignment, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var bucket in buckets)
        {
            var order = 0;
            foreach (var component in bucket.OrderBy(x => x.Order))
            {
                component.Order = order++;
            }
        }
    }

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
                    blockIndex = NormalizeBlockOrder(column, blockIndex);
                }
            }
        }
    }

    private static int NormalizeBlockOrder(NavCanvasColumnEditorModel column, int blockIndex)
    {
        column.Blocks[blockIndex].Order = blockIndex;
        return blockIndex;
    }

    private void AddRow()
    {
        _rows.Add(CreateRow(_rows.Count));
        NormalizeRowOrders();
    }

    private void RemoveRow(NavCanvasRowEditorModel row)
    {
        _rows = _rows.Where(x => x.ClientId != row.ClientId).ToList();
        NormalizeRowOrders();
    }

    private void MoveRow(NavCanvasRowEditorModel row, int direction)
    {
        var ordered = _rows.OrderBy(x => x.Order).ToList();
        var index = ordered.FindIndex(x => x.ClientId == row.ClientId);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= ordered.Count)
        {
            return;
        }

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        _rows = ordered;
        NormalizeRowOrders();
    }

    private void DuplicateRow(NavCanvasRowEditorModel row)
    {
        var ordered = _rows.OrderBy(x => x.Order).ToList();
        var index = ordered.FindIndex(x => x.ClientId == row.ClientId);
        if (index < 0)
        {
            return;
        }

        ordered.Insert(index + 1, CloneRow(row));
        _rows = ordered;
        NormalizeRowOrders();
    }

    private void AddColumn(NavCanvasRowEditorModel row)
    {
        var existing = row.Columns.Count;
        var span = existing switch
        {
            0 => 12,
            1 => 6,
            2 => 4,
            _ => 3
        };

        row.Columns.Add(new NavCanvasColumnEditorModel
        {
            DesktopSpan = span,
            TabletSpan = Math.Min(12, span * 2),
            MobileSpan = 12,
            Order = row.Columns.Count
        });
        NormalizeRowOrders();
    }

    private void RemoveColumn(NavCanvasRowEditorModel row, NavCanvasColumnEditorModel column)
    {
        row.Columns = row.Columns.Where(x => x.ClientId != column.ClientId).ToList();
        if (row.Columns.Count == 0)
        {
            AddColumn(row);
        }

        NormalizeRowOrders();
    }

    private void AddBlockToColumn(NavCanvasColumnEditorModel column, string kind)
    {
        column.Blocks.Add(CreateComponent(kind, "Left") with { Order = column.Blocks.Count });
        NormalizeRowOrders();
    }

    private void RemoveBlockFromColumn(NavCanvasColumnEditorModel column, NavComponentEditorModel block)
    {
        column.Blocks = column.Blocks.Where(x => x.ClientId != block.ClientId).ToList();
        if (SelectedBlockId == block.ClientId)
        {
            SelectedBlockId = string.Empty;
        }

        NormalizeRowOrders();
    }

    private void OnRowsReordered(IList<NavCanvasRowEditorModel> rows)
    {
        _rows = rows.ToList();
        NormalizeRowOrders();
    }

    private void OnColumnBlocksReordered(NavCanvasColumnEditorModel column, IList<NavComponentEditorModel> blocks)
    {
        column.Blocks = blocks.ToList();
        NormalizeRowOrders();
    }

    private void OnPaletteTransferredToColumn(NavCanvasColumnEditorModel column, SortableTransferArgs args)
    {
        var block = CreateComponent(args.ActiveId, "Left");
        var index = Math.Clamp(args.Index, 0, column.Blocks.Count);
        column.Blocks.Insert(index, block);
        SelectedBlockId = block.ClientId;
        NormalizeRowOrders();
    }

    private void ToggleCategory(string category)
    {
        if (string.Equals(category, "header", StringComparison.OrdinalIgnoreCase))
        {
            CategoryHeader = !CategoryHeader;
        }
    }

    private void SelectBlock(string clientId)
        => SelectedBlockId = SelectedBlockId == clientId ? string.Empty : clientId;

    private void AddChildLink(NavComponentEditorModel menu)
    {
        if (!string.Equals(menu.Kind, "menu", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        menu.Children.Add(CreateComponent("link", menu.Alignment) with { Order = menu.Children.Count });
    }

    private void RemoveChildLink(NavComponentEditorModel menu, NavComponentEditorModel child)
    {
        menu.Children = menu.Children.Where(x => !ReferenceEquals(x, child)).ToList();
        for (var i = 0; i < menu.Children.Count; i++)
        {
            menu.Children[i].Order = i;
        }
    }

    private static NavComponentEditorModel CreateComponent(string kind, string alignment)
    {
        var normalizedKind = kind.Trim().ToLowerInvariant();
        return new NavComponentEditorModel
        {
            Kind = normalizedKind,
            Alignment = alignment,
            Label = normalizedKind switch
            {
                "menu" => "Menu",
                "language" => "Language",
                "login" => "Login",
                "register" => "Register",
                "search" => "Search",
                "html" => "Rich menu",
                _ => "Link"
            },
            Url = normalizedKind switch
            {
                "link" => "/",
                "login" => "/login",
                "register" => "/register",
                _ => null
            },
            Html = normalizedKind == "html" ? "<div class=\"p-4\">Mega menu content</div>" : null,
            Placeholder = normalizedKind == "search" ? "Search..." : null,
            SearchAction = normalizedKind == "search" ? "/search" : null,
            ButtonLabel = normalizedKind == "search" ? "Search" : null,
            Visibility = normalizedKind switch
            {
                "login" or "register" => "AnonymousOnly",
                _ => "Always"
            },
            Target = "_self",
            Order = 0
        };
    }

    private IReadOnlyList<UpdateNavigationComponentRequest> BuildNavigationComponents()
        => CanvasBlocks
            .OrderBy(x => SlotOrder(x.Alignment))
            .ThenBy(x => x.Order)
            .Select(MapComponentRequest)
            .ToList();

    private IReadOnlyList<UpdateNavigationCanvasRowRequest> BuildNavigationRows()
        => _rows
            .OrderBy(x => x.Order)
            .Select(row => new UpdateNavigationCanvasRowRequest(
                row.Id,
                row.Order,
                row.Label?.Trim(),
                row.DesktopDisplay,
                row.TabletDisplay,
                row.MobileDisplay,
                row.Columns.OrderBy(column => column.Order)
                    .Select(column => new UpdateNavigationCanvasColumnRequest(
                        column.Id,
                        column.Order,
                        Math.Clamp(column.DesktopSpan, 1, 12),
                        Math.Clamp(column.TabletSpan, 1, 12),
                        Math.Clamp(column.MobileSpan, 1, 12),
                        column.Blocks.OrderBy(block => block.Order).Select(MapComponentRequest).ToList()))
                    .ToList()))
            .ToList();

    private static UpdateNavigationComponentRequest MapComponentRequest(NavComponentEditorModel component)
        => new(
            component.Id,
            component.Kind,
            component.Label?.Trim(),
            NormalizeUrl(component.Url, component.IsExternal),
            component.IsExternal ? null : component.PageId,
            component.Order,
            component.Alignment,
            component.AltText?.Trim(),
            component.IsExternal,
            NormalizeTarget(component.Target, component.IsExternal),
            component.Children.OrderBy(x => x.Order).Select(MapComponentRequest).ToList(),
            component.Html?.Trim(),
            component.Placeholder?.Trim(),
            component.SearchAction?.Trim(),
            component.ButtonLabel?.Trim(),
            component.Visibility);

    private static NavComponentEditorModel MapComponent(NavigationComponentDetail component)
        => new()
        {
            Id = component.Id,
            Kind = component.Kind,
            Label = component.Label,
            Url = component.Url,
            PageId = component.PageId,
            Order = component.Order,
            Alignment = component.Alignment,
            AltText = component.AltText,
            IsExternal = component.IsExternal,
            Target = NormalizeTarget(component.Target, component.IsExternal),
            Html = component.Html,
            Placeholder = component.Placeholder,
            SearchAction = component.SearchAction,
            ButtonLabel = component.ButtonLabel,
            Visibility = NormalizeVisibility(component.Visibility),
            Children = component.Children.OrderBy(x => x.Order).Select(MapComponent).ToList()
        };

    private static NavCanvasRowEditorModel MapRow(NavigationCanvasRowDetail row)
        => new()
        {
            Id = row.Id,
            Order = row.Order,
            Label = row.Label,
            DesktopDisplay = row.DesktopDisplay,
            TabletDisplay = row.TabletDisplay,
            MobileDisplay = row.MobileDisplay,
            Columns = row.Columns.OrderBy(x => x.Order).Select(column => new NavCanvasColumnEditorModel
            {
                Id = column.Id,
                Order = column.Order,
                DesktopSpan = column.DesktopSpan,
                TabletSpan = column.TabletSpan,
                MobileSpan = column.MobileSpan,
                Blocks = column.Blocks.OrderBy(block => block.Order).Select(MapComponent).ToList()
            }).ToList()
        };

    private static NavCanvasRowEditorModel CloneRow(NavCanvasRowEditorModel row)
        => new()
        {
            Label = string.IsNullOrWhiteSpace(row.Label) ? "Header row" : $"{row.Label} copy",
            DesktopDisplay = row.DesktopDisplay,
            TabletDisplay = row.TabletDisplay,
            MobileDisplay = row.MobileDisplay,
            Columns = row.Columns.OrderBy(x => x.Order).Select(CloneColumn).ToList()
        };

    private static NavCanvasColumnEditorModel CloneColumn(NavCanvasColumnEditorModel column)
        => new()
        {
            DesktopSpan = column.DesktopSpan,
            TabletSpan = column.TabletSpan,
            MobileSpan = column.MobileSpan,
            Blocks = column.Blocks.OrderBy(x => x.Order).Select(CloneComponent).ToList()
        };

    private static NavComponentEditorModel CloneComponent(NavComponentEditorModel component)
        => new()
        {
            Kind = component.Kind,
            Alignment = component.Alignment,
            Label = component.Label,
            Url = component.Url,
            PageId = component.PageId,
            AltText = component.AltText,
            IsExternal = component.IsExternal,
            Target = component.Target,
            Html = component.Html,
            Placeholder = component.Placeholder,
            SearchAction = component.SearchAction,
            ButtonLabel = component.ButtonLabel,
            Visibility = component.Visibility,
            Order = component.Order,
            Children = component.Children.OrderBy(x => x.Order).Select(CloneComponent).ToList()
        };

    private static List<NavCanvasRowEditorModel> BuildDefaultRows(IReadOnlyList<NavComponentEditorModel> components)
    {
        var row = CreateRow(0);
        row.Columns[0].Blocks = components.Where(x => string.Equals(x.Alignment, "Left", StringComparison.OrdinalIgnoreCase)).ToList();
        row.Columns[1].Blocks = components.Where(x => string.Equals(x.Alignment, "Center", StringComparison.OrdinalIgnoreCase)).ToList();
        row.Columns[2].Blocks = components.Where(x => string.Equals(x.Alignment, "Right", StringComparison.OrdinalIgnoreCase)).ToList();
        return [row];
    }

    private static NavCanvasRowEditorModel CreateRow(int order)
        => new()
        {
            Label = order == 0 ? "Header row" : $"Header row {order + 1}",
            Order = order,
            Columns =
            [
                new() { Order = 0, DesktopSpan = 4, TabletSpan = 6, MobileSpan = 12 },
                new() { Order = 1, DesktopSpan = 4, TabletSpan = 6, MobileSpan = 12 },
                new() { Order = 2, DesktopSpan = 4, TabletSpan = 12, MobileSpan = 12 }
            ]
        };

    private static bool IsValidComponent(NavComponentEditorModel component)
        => component.Kind.Trim().ToLowerInvariant() switch
        {
            "menu" => !string.IsNullOrWhiteSpace(component.Label) && component.Children.All(IsValidComponent),
            "html" => !string.IsNullOrWhiteSpace(component.Html),
            "search" => !string.IsNullOrWhiteSpace(component.SearchAction),
            "language" => true,
            "login" or "register" or "authbutton" => !string.IsNullOrWhiteSpace(component.Label)
                && IsRelativeUrl(component.Url),
            _ => !string.IsNullOrWhiteSpace(component.Label)
                 && !string.IsNullOrWhiteSpace(component.Url)
                 && (component.IsExternal
                     ? IsHttpUrl(NormalizeUrl(component.Url, true))
                     : component.PageId is not null || IsRelativeUrl(component.Url))
        };

    private static int SlotOrder(string? alignment)
        => alignment?.ToLowerInvariant() switch
        {
            "center" => 1,
            "right" => 2,
            _ => 0
        };

    private IReadOnlyList<NavComponentEditorModel> CanvasBlocks
        => _rows.Count > 0
            ? _rows
                .OrderBy(row => row.Order)
                .SelectMany(row => row.Columns.OrderBy(column => column.Order))
                .SelectMany(column => column.Blocks.OrderBy(block => block.Order))
                .ToList()
            : _components;

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

    private static string NormalizeVisibility(string? visibility)
        => visibility is "AnonymousOnly" or "AuthenticatedOnly" or "Always" ? visibility : "Always";

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

    private string BuildPreviewHtml()
    {
        var builder = new StringBuilder();
        builder.Append("<nav class=\"w-full border-b border-slate-200 bg-white shadow-sm\">");
        builder.Append("<div class=\"mx-auto flex h-16 max-w-7xl items-center justify-between gap-6 px-4 sm:px-6 lg:px-8\">");
        builder.Append("<a href=\"/\" class=\"flex items-center gap-3\">");
        if (!string.IsNullOrWhiteSpace(_editSiteLogoUrl))
        {
            builder.Append(CultureInfo.InvariantCulture, $"<img src=\"{Encode(_editSiteLogoUrl)}\" alt=\"{Encode(_editName)}\" class=\"h-10 max-w-48 object-contain\" />");
        }
        else
        {
            builder.Append("<span class=\"flex h-10 w-10 items-center justify-center rounded bg-indigo-600 text-sm font-black text-white\">A</span>");
            builder.Append(CultureInfo.InvariantCulture, $"<span class=\"font-semibold text-slate-900\">{Encode(_editName)}</span>");
        }

        builder.Append("</a>");
        builder.Append("<div class=\"hidden flex-1 items-center justify-between gap-4 md:flex\">");
        foreach (var alignment in new[] { "Left", "Center", "Right" })
        {
            builder.Append("<div class=\"flex items-center gap-1\">");
            foreach (var component in CanvasBlocks
                         .Where(x => string.Equals(x.Alignment, alignment, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(x => x.Order))
            {
                AppendPreviewComponent(builder, component);
            }

            builder.Append("</div>");
        }

        builder.Append("</div>");
        builder.Append("<button class=\"inline-flex h-10 w-10 items-center justify-center rounded-lg text-slate-600 md:hidden\" type=\"button\" aria-label=\"Menu\">");
        builder.Append("<span class=\"text-xl leading-none\">☰</span>");
        builder.Append("</button>");
        builder.Append("</div>");
        builder.Append("</nav>");
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

    private static void AppendPreviewComponent(StringBuilder builder, NavComponentEditorModel component)
    {
        switch (component.Kind.Trim().ToLowerInvariant())
        {
            case "menu":
                builder.Append("<div class=\"relative rounded-lg px-3 py-2 text-sm font-semibold text-slate-600\">");
                builder.Append(CultureInfo.InvariantCulture, $"{Encode(component.Label)} <span aria-hidden=\"true\">⌄</span>");
                builder.Append("</div>");
                break;
            case "html":
                builder.Append(CultureInfo.InvariantCulture, $"<div class=\"rounded-lg px-3 py-2 text-sm text-slate-600\">{Encode(component.Label ?? "Rich menu")}</div>");
                break;
            case "search":
                builder.Append(CultureInfo.InvariantCulture, $"<form action=\"{Encode(component.SearchAction ?? "/search")}\" class=\"flex items-center gap-2\"><input class=\"h-9 w-40 rounded border border-slate-200 px-3 text-sm\" placeholder=\"{Encode(component.Placeholder ?? "Search...")}\" /><button class=\"h-9 rounded bg-indigo-600 px-3 text-sm font-semibold text-white\" type=\"submit\">{Encode(component.ButtonLabel ?? "Search")}</button></form>");
                break;
            case "language":
                builder.Append("<details class=\"relative\"><summary class=\"flex h-10 w-10 cursor-pointer list-none items-center justify-center rounded-lg text-slate-600 hover:bg-slate-50 hover:text-indigo-600\" aria-label=\"Language\"><svg aria-hidden=\"true\" viewBox=\"0 0 24 24\" class=\"h-5 w-5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"><circle cx=\"12\" cy=\"12\" r=\"10\"></circle><path d=\"M2 12h20\"></path><path d=\"M12 2a15.3 15.3 0 0 1 0 20\"></path><path d=\"M12 2a15.3 15.3 0 0 0 0 20\"></path></svg></summary><div class=\"absolute right-0 top-full z-10 mt-2 min-w-36 rounded-lg border border-slate-200 bg-white p-2 shadow-xl\"><span class=\"block rounded px-3 py-2 text-sm font-semibold text-slate-700\">English</span><span class=\"block rounded px-3 py-2 text-sm font-semibold text-slate-700\">Spanish</span></div></details>");
                break;
            case "login" or "register" or "authbutton":
                var buttonCss = component.Kind.Equals("register", StringComparison.OrdinalIgnoreCase)
                    ? "rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white"
                    : "rounded-lg px-4 py-2 text-sm font-semibold text-slate-600 transition hover:bg-slate-50 hover:text-indigo-600";
                builder.Append(CultureInfo.InvariantCulture, $"<a href=\"{Encode(component.Url ?? "#")}\" class=\"{buttonCss}\">{Encode(component.Label)}</a>");
                break;
            default:
                var target = NormalizeTarget(component.Target, component.IsExternal);
                var targetAttribute = target == "_self" ? string.Empty : $" target=\"{Encode(target)}\"";
                var rel = target == "_blank" ? " rel=\"noopener noreferrer\"" : string.Empty;
                builder.Append(CultureInfo.InvariantCulture, $"<a href=\"{Encode(NormalizeUrl(component.Url, component.IsExternal) ?? "#")}\" class=\"rounded-lg px-3 py-2 text-sm font-semibold text-slate-600 transition hover:bg-slate-50 hover:text-indigo-600\"{targetAttribute}{rel}>{Encode(component.Label)}</a>");
                break;
        }
    }

    private string ColumnGridStyle(NavCanvasColumnEditorModel column)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"--aero-mobile-span:{Math.Clamp(column.MobileSpan, 1, 12)};--aero-tablet-span:{Math.Clamp(column.TabletSpan, 1, 12)};--aero-desktop-span:{Math.Clamp(column.DesktopSpan, 1, 12)};");

    private static string Encode(string? value)
        => HtmlEncoder.Default.Encode(value ?? string.Empty);

    private RenderFragment RenderHeaderBlockFields(NavComponentEditorModel component) => builder =>
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
            Open("textarea", "w-full rounded px-3 py-2 text-sm font-mono");
            builder.AddAttribute(sequence++, "style", "border:1px solid var(--pe-border);background:var(--pe-bg-primary);color:var(--pe-text-primary);");
            builder.AddAttribute(sequence++, "rows", 4);
            builder.AddAttribute(sequence++, "value", value);
            builder.AddAttribute(sequence++, "placeholder", placeholder);
            builder.AddAttribute(sequence++, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, e => update(e.Value?.ToString())));
            Close();
        }

        void SelectInput(string value, IReadOnlyList<(string Value, string Label)> options, Action<string> update)
        {
            Open("select", "w-full rounded px-3 py-2 text-sm");
            builder.AddAttribute(sequence++, "style", "border:1px solid var(--pe-border);background:var(--pe-bg-primary);color:var(--pe-text-primary);");
            builder.AddAttribute(sequence++, "value", value);
            builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e => update(e.Value?.ToString() ?? "Always")));
            foreach (var option in options)
            {
                Open("option");
                builder.AddAttribute(sequence++, "value", option.Value);
                builder.AddContent(sequence++, option.Label);
                Close();
            }

            Close();
        }

        Open("div", "grid grid-cols-1 gap-2");
        SelectInput(
            NormalizeVisibility(component.Visibility),
            [
                ("Always", L["Always"].Value),
                ("AnonymousOnly", L["Logged out only"].Value),
                ("AuthenticatedOnly", L["Logged in only"].Value)
            ],
            value => component.Visibility = NormalizeVisibility(value));

        switch (component.Kind.Trim().ToLowerInvariant())
        {
            case "search":
                TextInput(component.Placeholder, L["Placeholder"], value => component.Placeholder = value);
                TextInput(component.SearchAction, "/search", value => component.SearchAction = value);
                TextInput(component.ButtonLabel, L["Button label"], value => component.ButtonLabel = value);
                break;
            case "language":
                TextInput(component.Label, L["Accessible label"], value => component.Label = value);
                break;
            case "login":
            case "register":
            case "authbutton":
                TextInput(component.Label, L["Label"], value => component.Label = value);
                TextInput(component.Url, "/login", value => component.Url = value);
                break;
            case "html":
                TextInput(component.Label, L["Block label"], value => component.Label = value);
                TextArea(component.Html, L["Custom HTML"], value => component.Html = value);
                break;
            default:
                TextInput(component.Label, L["Label"], value => component.Label = value);
                TextInput(component.Url, "/path", value => component.Url = value);
                Open("label", "inline-flex items-center gap-2 text-sm");
                builder.AddAttribute(sequence++, "style", "color:var(--pe-text-secondary);");
                Open("input");
                builder.AddAttribute(sequence++, "type", "checkbox");
                builder.AddAttribute(sequence++, "checked", component.IsExternal);
                builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e => component.IsExternal = e.Value is true));
                Close();
                builder.AddContent(sequence++, L["External"]);
                Close();
                break;
        }

        Close();
    };

    private sealed record PaletteBlock(string Kind, string Label);

    protected sealed record NavComponentEditorModel
    {
        public string ClientId { get; } = Guid.NewGuid().ToString("N");
        public long Id { get; set; }
        public string Kind { get; set; } = "link";
        public string Alignment { get; set; } = "Left";
        public string? Label { get; set; }
        public string? Url { get; set; }
        public long? PageId { get; set; }
        public string? AltText { get; set; }
        public bool IsExternal { get; set; }
        public string Target { get; set; } = "_self";
        public string? Html { get; set; }
        public string? Placeholder { get; set; }
        public string? SearchAction { get; set; }
        public string? ButtonLabel { get; set; }
        public string Visibility { get; set; } = "Always";
        public int Order { get; set; }
        public List<NavComponentEditorModel> Children { get; set; } = [];
        public string DisplayName => Kind.Trim().ToLowerInvariant() switch
        {
            "menu" => Label ?? "Menu",
            "html" => Label ?? "Rich menu",
            "search" => "Search area",
            "language" => "Language select",
            "login" => Label ?? "Login",
            "register" => Label ?? "Register",
            "authbutton" => Label ?? "Auth button",
            _ => Label ?? "Link"
        };
    }

    protected sealed class NavCanvasRowEditorModel
    {
        public string ClientId { get; } = Guid.NewGuid().ToString("N");
        public long Id { get; set; }
        public int Order { get; set; }
        public string? Label { get; set; }
        public string DesktopDisplay { get; set; } = "Flex";
        public string TabletDisplay { get; set; } = "Flex";
        public string MobileDisplay { get; set; } = "Stack";
        public List<NavCanvasColumnEditorModel> Columns { get; set; } = [];
    }

    protected sealed class NavCanvasColumnEditorModel
    {
        public string ClientId { get; } = Guid.NewGuid().ToString("N");
        public long Id { get; set; }
        public int Order { get; set; }
        public int DesktopSpan { get; set; } = 4;
        public int TabletSpan { get; set; } = 6;
        public int MobileSpan { get; set; } = 12;
        public List<NavComponentEditorModel> Blocks { get; set; } = [];
    }
}
