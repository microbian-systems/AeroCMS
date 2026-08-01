using System.Net;
using System.Text.Json;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Theming;
using Aero.Cms.Shared.Services;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.Theming;

public partial class ThemeStudio : ComponentBase, IAsyncDisposable
{
    private const long MaximumImportBytes = 262_144;

    [Parameter] public long SiteId { get; set; }
    [SupplyParameterFromQuery(Name = "draft")] public long? RequestedDraftId { get; set; }
    [Inject] private ISitesHttpClient SitesClient { get; set; } = default!;
    [Inject] private IThemesHttpClient ThemesClient { get; set; } = default!;
    [Inject] private ICurrentSiteAccessor CurrentSiteAccessor { get; set; } = default!;
    [Inject] private AdminStateContainer AdminState { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ILogger<ThemeStudio> Logger { get; set; } = default!;

    private readonly CancellationTokenSource _lifetime = new();
    private ThemeStudioInterop? _interop;
    private SiteViewModel? _site;
    private IReadOnlyList<ThemeDefinitionView> _drafts = [];
    private IReadOnlyList<ThemeVersionView> _versions = [];
    private IReadOnlyList<SiteThemePublicationView> _publications = [];
    private ThemeTokenSet _tokens = new();
    private long _draftId;
    private long _revision;
    private string _name = string.Empty;
    private string _slug = string.Empty;
    private string? _description;
    private bool _isLoading = true;
    private bool _isBusy;
    private bool _dirty;
    private string _busyLabel = "Working";
    private string? _loadError;
    private string? _operationMessage;
    private bool _operationIsError;
    private ThemeDefaultMode _editingMode = ThemeDefaultMode.Light;
    private ThemeStudioPanel _previewPanel;
    private ThemeStudioViewport _viewport = ThemeStudioViewport.Desktop;
    private long _loadedSiteId;

    private bool IsDirty => _dirty;
    private ThemeColorTokens EditingColors => _editingMode == ThemeDefaultMode.Light ? _tokens.Light : _tokens.Dark;
    private IReadOnlyList<ThemeContrastResult> ContrastResults => ThemeStudioTokens.Contrast(_tokens);
    private IReadOnlyList<string> ValidationProblems => ThemeStudioTokens.Validate(_tokens);
    private bool HasBlockingProblems => ValidationProblems.Count > 0 || string.IsNullOrWhiteSpace(_name) || string.IsNullOrWhiteSpace(_slug);
    private string PreviewCss => ThemeStudioTokens.PreviewCss(_tokens);
    private string PreviewDataTheme => _editingMode == ThemeDefaultMode.Light ? "theme-studio-light" : "theme-studio-dark";

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedSiteId == SiteId && !_isLoading) return;
        _loadedSiteId = SiteId;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadError = null;
        try
        {
            var siteResult = await SitesClient.GetByIdAsync(SiteId, _lifetime.Token);
            if (siteResult is not Result<SiteViewModel, AeroError>.Ok siteOk)
            {
                _site = null;
                _loadError = "The selected site is unavailable or you no longer have access.";
                return;
            }

            _site = siteOk.Value;
            await CurrentSiteAccessor.SetCurrentSiteAsync(SiteId);
            if (await CurrentSiteAccessor.GetCurrentSiteIdAsync() != SiteId)
            {
                _loadError = "The selected site context could not be established. No theme data was loaded.";
                return;
            }
            AdminState.SetSite(SiteId, _site.Name ?? "Site");

            var draftsTask = ThemesClient.ListDraftsAsync(_lifetime.Token);
            var historyTask = ThemesClient.GetPublicationHistoryAsync(_lifetime.Token);
            await Task.WhenAll(draftsTask, historyTask);

            if (draftsTask.Result is not Result<IReadOnlyList<ThemeDefinitionView>, AeroError>.Ok draftsOk)
            {
                _loadError = "Theme drafts could not be loaded. Check your theme design permission and try again.";
                return;
            }

            _drafts = draftsOk.Value.OrderBy(static draft => draft.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            _publications = historyTask.Result is Result<IReadOnlyList<SiteThemePublicationView>, AeroError>.Ok historyOk
                ? historyOk.Value.OrderByDescending(static item => item.PublishedOn).ToArray() : [];

            var initialDraft = RequestedDraftId.HasValue
                ? _drafts.FirstOrDefault(draft => draft.Id == RequestedDraftId.Value)
                : FindAssignedDraft() ?? _drafts.FirstOrDefault();
            LoadDraft(initialDraft);
            await LoadVersionsAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to load Theme Studio for site {SiteId}.", SiteId);
            _loadError = "Theme Studio could not load. Try again or return to the site editor.";
        }
        finally { _isLoading = false; }
    }

    private ThemeDefinitionView? FindAssignedDraft()
    {
        if (_site is null || !_site.ThemeId.StartsWith("tenant-", StringComparison.Ordinal)) return null;
        return _drafts.FirstOrDefault(draft => _site.ThemeId.EndsWith($"-{draft.Slug}", StringComparison.Ordinal));
    }

    private void LoadDraft(ThemeDefinitionView? draft)
    {
        _draftId = draft?.Id ?? 0;
        _revision = draft?.Revision ?? 0;
        _name = draft?.Name ?? $"{_site?.Name ?? "Site"} theme";
        _slug = draft?.Slug ?? Slugify(_site?.Name ?? "site-theme");
        _description = draft?.Description;
        _tokens = ThemeStudioTokens.Clone(draft?.Tokens ?? new ThemeTokenSet());
        _editingMode = _tokens.DefaultMode;
        _dirty = false;
        _operationMessage = null;
    }

    private async Task SelectDraftAsync(long id)
    {
        if (id == _draftId) return;
        if (IsDirty && !await ConfirmDiscardAsync()) return;
        LoadDraft(id == 0 ? null : _drafts.FirstOrDefault(draft => draft.Id == id));
        await LoadVersionsAsync();
    }

    private async Task SaveAsync()
    {
        if (_isBusy) return;
        var documentProblems = ValidationProblems.Where(static problem => !problem.Contains("publishing requires", StringComparison.Ordinal)).ToArray();
        if (string.IsNullOrWhiteSpace(_name) || string.IsNullOrWhiteSpace(_slug) || documentProblems.Length > 0)
        {
            ShowMessage(documentProblems.FirstOrDefault() ?? "Name and slug are required before saving.", true);
            return;
        }

        await RunBusyAsync("Saving", async () =>
        {
            Result<ThemeDefinitionView, AeroError> result = _draftId == 0
                ? await ThemesClient.CreateDraftAsync(new(_name.Trim(), _slug.Trim(), CleanDescription(), ThemeStudioTokens.Clone(_tokens)), _lifetime.Token)
                : await ThemesClient.SaveDraftAsync(_draftId, new(_revision, _name.Trim(), _slug.Trim(), CleanDescription(), ThemeStudioTokens.Clone(_tokens)), _lifetime.Token);

            if (result is Result<ThemeDefinitionView, AeroError>.Ok ok)
            {
                UpsertDraft(ok.Value);
                LoadDraft(ok.Value);
                Navigation.NavigateTo($"/manager/sites/{SiteId}/theme-studio?draft={ok.Value.Id}", replace: true);
                ShowMessage("Draft saved. Local preview and server revision now match.");
                await LoadVersionsAsync();
                return;
            }

            if (result is Result<ThemeDefinitionView, AeroError>.Failure { Error: AeroError.Conflict }
                || result is Result<ThemeDefinitionView, AeroError>.Failure { Error: AeroError.HttpRequest { code: HttpStatusCode.Conflict } })
            {
                ShowMessage("This draft changed in another session. Your local edits are preserved; reload the draft before saving again.", true);
                return;
            }

            ShowMessage("The draft could not be saved. Review the fields and try again.", true);
        });
    }

    private async Task CreateServerPreviewAsync()
    {
        if (_draftId == 0 || IsDirty) return;
        await RunBusyAsync("Preparing preview", async () =>
        {
            var result = await ThemesClient.CreatePreviewAsync(_draftId, _lifetime.Token);
            if (result is Result<ThemePreviewView, AeroError>.Ok ok)
                ShowMessage($"Saved preview refreshed. It expires {ok.Value.ExpiresOn.ToLocalTime():t}; this canvas still shows your current local tokens.");
            else ShowMessage("The saved preview could not be prepared. Your local preview is still available.", true);
        });
    }

    private async Task PublishAndAssignAsync()
    {
        if (_draftId == 0 || HasBlockingProblems || _site is null) return;
        if (IsDirty) { await SaveAsync(); if (IsDirty) return; }
        await RunBusyAsync("Publishing", async () =>
        {
            var published = await ThemesClient.PublishAsync(_draftId, _lifetime.Token);
            if (published is not Result<ThemeVersionView, AeroError>.Ok publishOk)
            {
                ShowMessage("Publishing failed. Resolve every contrast warning and try again.", true);
                return;
            }
            await AssignCoreAsync(new(publishOk.Value.ThemeId, publishOk.Value.Version));
            await LoadVersionsAsync();
        });
    }

    private Task AssignAsync(ThemeAssignmentRequest request) => RunBusyAsync("Assigning", () => AssignCoreAsync(request));

    private async Task AssignCoreAsync(ThemeAssignmentRequest request)
    {
        if (_site is null) return;
        var assigned = await ThemesClient.AssignAsync(new(request.ThemeId, request.Version, _site.ThemeRevision), _lifetime.Token);
        if (assigned is Result<SiteThemePublicationView, AeroError>.Ok ok)
        {
            _site.ThemeId = ok.Value.ThemeId; _site.ThemeVersion = ok.Value.Version; _site.ThemeRevision = ok.Value.Revision;
            ShowMessage($"{ok.Value.ThemeId}@{ok.Value.Version} is now assigned to {_site.Name}.");
            await ReloadHistoryAsync();
            return;
        }
        if (assigned is Result<SiteThemePublicationView, AeroError>.Failure { Error: AeroError.Conflict }
            || assigned is Result<SiteThemePublicationView, AeroError>.Failure { Error: AeroError.HttpRequest { code: HttpStatusCode.Conflict } })
        {
            await ReloadSiteAsync();
            ShowMessage("The site theme changed elsewhere. The latest site revision was reloaded; review it before assigning again.", true);
            return;
        }
        ShowMessage("The theme version could not be assigned to this site.", true);
    }

    private async Task ExportAsync()
    {
        if (_draftId == 0) return;
        await RunBusyAsync("Exporting", async () =>
        {
            var result = await ThemesClient.ExportAsync(_draftId, _lifetime.Token);
            if (result is not Result<ThemeImportEnvelope, AeroError>.Ok ok) { ShowMessage("The saved draft could not be exported.", true); return; }
            var json = JsonSerializer.Serialize(ok.Value, ThemeStudioJsonContext.Default.ThemeImportEnvelope) + Environment.NewLine;
            _interop ??= new ThemeStudioInterop(JS);
            await _interop.DownloadAsync($"{Slugify(ok.Value.Theme.Slug)}-theme.json", json, "application/json", _lifetime.Token);
            ShowMessage("Deterministic theme JSON downloaded.");
        });
    }

    private async Task ImportAsync(InputFileChangeEventArgs args)
    {
        if (_isBusy) return;
        await RunBusyAsync("Importing", async () =>
        {
            try
            {
                await using var stream = args.File.OpenReadStream(MaximumImportBytes, _lifetime.Token);
                var envelope = await JsonSerializer.DeserializeAsync(stream, ThemeStudioJsonContext.Default.ThemeImportEnvelope, _lifetime.Token);
                if (envelope is null || envelope.SchemaVersion != 1 || envelope.Theme is null || envelope.Theme.Tokens is null) { ShowMessage("Import rejected: expected strict Theme Studio schema version 1.", true); return; }
                var problems = ThemeStudioTokens.Validate(envelope.Theme.Tokens).Where(static problem => !problem.Contains("publishing requires", StringComparison.Ordinal)).ToArray();
                if (string.IsNullOrWhiteSpace(envelope.Theme.Name) || string.IsNullOrWhiteSpace(envelope.Theme.Slug) || problems.Length > 0) { ShowMessage(problems.FirstOrDefault() ?? "Import rejected: name and slug are required.", true); return; }
                var imported = await ThemesClient.ImportAsync(envelope, _lifetime.Token);
                if (imported is not Result<ThemeDefinitionView, AeroError>.Ok ok) { ShowMessage("Import rejected by the server. No draft was changed.", true); return; }
                UpsertDraft(ok.Value); LoadDraft(ok.Value);
                Navigation.NavigateTo($"/manager/sites/{SiteId}/theme-studio?draft={ok.Value.Id}", replace: true);
                ShowMessage("Theme JSON imported as a new draft.");
                await LoadVersionsAsync();
            }
            catch (JsonException exception) { Logger.LogWarning(exception, "Rejected malformed Theme Studio JSON import."); ShowMessage("Import rejected: the JSON contains unknown, missing, or malformed values.", true); }
            catch (IOException exception) { Logger.LogWarning(exception, "Rejected Theme Studio JSON import because the file could not be read."); ShowMessage("Import rejected: choose a JSON file smaller than 256 KB.", true); }
        });
    }

    private async Task LoadVersionsAsync()
    {
        if (_draftId == 0) { _versions = []; return; }
        var result = await ThemesClient.ListVersionsAsync(_draftId, _lifetime.Token);
        _versions = result is Result<IReadOnlyList<ThemeVersionView>, AeroError>.Ok ok ? ok.Value.OrderByDescending(static item => item.PublishedOn).ToArray() : [];
    }

    private async Task ReloadHistoryAsync()
    {
        var result = await ThemesClient.GetPublicationHistoryAsync(_lifetime.Token);
        if (result is Result<IReadOnlyList<SiteThemePublicationView>, AeroError>.Ok ok) _publications = ok.Value.OrderByDescending(static item => item.PublishedOn).ToArray();
    }

    private async Task ReloadSiteAsync()
    {
        var result = await SitesClient.GetByIdAsync(SiteId, _lifetime.Token);
        if (result is Result<SiteViewModel, AeroError>.Ok ok) _site = ok.Value;
    }

    private async Task RunBusyAsync(string label, Func<Task> action)
    {
        if (_isBusy) return;
        _isBusy = true; _busyLabel = label; _operationMessage = null;
        try { await action(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) { Logger.LogError(exception, "Theme Studio operation {Operation} failed for site {SiteId}.", label, SiteId); ShowMessage("The operation did not complete. Try again.", true); }
        finally { _isBusy = false; }
    }

    private void UpsertDraft(ThemeDefinitionView draft) => _drafts = _drafts.Where(item => item.Id != draft.Id).Append(draft).OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    private void SetName(string value) { _name = value; _dirty = true; }
    private void SetSlug(string value) { _slug = Slugify(value); _dirty = true; }
    private void SetDescription(string value) { _description = value; _dirty = true; }
    private void SetEditingMode(ThemeDefaultMode mode) => _editingMode = mode;
    private void SetDefaultMode(ThemeDefaultMode mode) { _tokens.DefaultMode = mode; _dirty = true; }
    private void SetPreviewPanel(ThemeStudioPanel panel) => _previewPanel = panel;
    private void SetViewport(ThemeStudioViewport viewport) => _viewport = viewport;

    private void ChangeColor(ThemeColorChange change)
    {
        var colors = change.Mode == ThemeDefaultMode.Light ? _tokens.Light : _tokens.Dark;
        var value = change.Value.Trim();
        switch (change.Token)
        {
            case "Base100": colors.Base100 = value; break; case "Base200": colors.Base200 = value; break; case "Base300": colors.Base300 = value; break; case "BaseContent": colors.BaseContent = value; break;
            case "Primary": colors.Primary = value; break; case "PrimaryContent": colors.PrimaryContent = value; break; case "Secondary": colors.Secondary = value; break; case "SecondaryContent": colors.SecondaryContent = value; break;
            case "Accent": colors.Accent = value; break; case "AccentContent": colors.AccentContent = value; break; case "Neutral": colors.Neutral = value; break; case "NeutralContent": colors.NeutralContent = value; break;
            case "Info": colors.Info = value; break; case "InfoContent": colors.InfoContent = value; break; case "Success": colors.Success = value; break; case "SuccessContent": colors.SuccessContent = value; break;
            case "Warning": colors.Warning = value; break; case "WarningContent": colors.WarningContent = value; break; case "Error": colors.Error = value; break; case "ErrorContent": colors.ErrorContent = value; break;
        }
        _dirty = true;
    }

    private void ChangeShape(ThemeShapeChange change)
    {
        switch (change.Token)
        {
            case "RadiusSelectorRem": _tokens.Shape.RadiusSelectorRem = change.Value; break; case "RadiusFieldRem": _tokens.Shape.RadiusFieldRem = change.Value; break; case "RadiusBoxRem": _tokens.Shape.RadiusBoxRem = change.Value; break;
            case "SizeSelectorRem": _tokens.Shape.SizeSelectorRem = change.Value; break; case "SizeFieldRem": _tokens.Shape.SizeFieldRem = change.Value; break; case "BorderRem": _tokens.Shape.BorderRem = change.Value; break;
            case "Depth": _tokens.Shape.Depth = (int)change.Value; break; case "Noise": _tokens.Shape.Noise = (int)change.Value; break;
        }
        _dirty = true;
    }

    private async Task ConfirmInternalNavigationAsync(LocationChangingContext context) { if (IsDirty && !await ConfirmDiscardAsync()) context.PreventNavigation(); }
    private async Task<bool> ConfirmDiscardAsync() { _interop ??= new ThemeStudioInterop(JS); return await _interop.ConfirmDiscardAsync(_lifetime.Token); }
    private string? CleanDescription() => string.IsNullOrWhiteSpace(_description) ? null : _description.Trim();
    private void ShowMessage(string message, bool error = false) { _operationMessage = message; _operationIsError = error; }
    private void DismissMessage() => _operationMessage = null;
    private void Back() => Navigation.NavigateTo($"/manager/sites/{SiteId}");
    private async Task BackAsync() { if (!IsDirty || await ConfirmDiscardAsync()) Back(); }
    private void OpenSites() => Navigation.NavigateTo("/manager/sites");
    private static string Slugify(string value) { var slug = new string(value.Trim().ToLowerInvariant().Select(static c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()); while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal); return slug.Trim('-'); }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel(); _lifetime.Dispose();
        if (_interop is not null) await _interop.DisposeAsync();
    }
}
