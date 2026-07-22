using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Aero.Core;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;

using Aero.Core.Railway;
using CmsPageDetail = Aero.Cms.Abstractions.Http.Clients.PageDetail;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Shared.Services;
using Aero.Cms.Shared.Pages.Manager.PageTree;
using Radzen;
using Aero.Cms.Html;
using Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;
using Aero.Cms.Abstractions.Media;
using Aero.Cms.Abstractions.Pages.Composition;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>
/// Represents a class for PageEditor.
/// </summary>
public partial class PageEditor : ComponentBase, IAsyncDisposable
{
    // ──────────────────────────────────────────────────────────
    // Parameters
    // ──────────────────────────────────────────────────────────

    /// <summary>Optional ID of an existing page to edit.</summary>
    [Parameter] public long? Id { get; set; }

        /// <summary>
    /// Gets or sets the Pages Client.
    /// </summary>
[Inject] protected IPagesHttpClient PagesClient { get; set; } = default!;
    /// <summary>Gets or sets the server-side preview client.</summary>
    [Inject] protected IPreviewHttpClient PreviewClient { get; set; } = default!;
        /// <summary>
    /// Gets or sets the Sites Client.
    /// </summary>
[Inject] protected ISitesHttpClient SitesClient { get; set; } = default!;
        /// <summary>
    /// Gets or sets the Current Site Accessor.
    /// </summary>
[Inject] protected ICurrentSiteAccessor CurrentSiteAccessor { get; set; } = default!;
        /// <summary>
    /// Gets or sets the Admin State.
    /// </summary>
[Inject] protected AdminStateContainer AdminState { get; set; } = default!;
        /// <summary>
    /// Gets or sets the Admin Storage.
    /// </summary>
[Inject] protected Aero.Cms.Contracts.Abstractions.IAdminStorage AdminStorage { get; set; } = default!;
        /// <summary>
    /// Gets or sets the Nav Manager.
    /// </summary>
[Inject] protected NavigationManager NavManager { get; set; } = default!;
        /// <summary>
    /// Gets or sets the Dialog Service.
    /// </summary>
[Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    // ──────────────────────────────────────────────────────────
    // State  (mirrors Alpine.js cmsEditor() properties)
    // ──────────────────────────────────────────────────────────

        /// <summary>
    /// Gets or sets the Page Title.
    /// </summary>
protected string PageTitle    { get; set; } = "Homepage";
        /// <summary>
    /// Gets or sets the Last Saved.
    /// </summary>
protected string LastSaved    { get; set; } = "Never";
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
protected string Author       { get; set; } = "Admin";

    private static readonly HtmlElementCatalog HtmlCatalog = HtmlElementCatalog.CreateDefault();
    private static readonly IHtmlContentModelPolicy HtmlContentPolicy = new HtmlContentModelPolicy(HtmlCatalog);

    protected IReadOnlyList<HtmlElementDefinition> HtmlElementDefinitions { get; }
        = HtmlCatalog.Definitions
            .OrderBy(definition => definition.PaletteCategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    protected HtmlPageEditorSession HtmlEditor { get; private set; }
        = CreateHtmlEditorSession(new HtmlPageContent(), new NativeStyleProfile());

    protected HtmlElementDefinition? SelectedHtmlDefinition =>
        HtmlEditor.SelectedNode is { Kind: HtmlNodeKind.Element } selected
        && HtmlCatalog.TryGet(selected.TagName, out var definition)
            ? definition
            : null;

    protected string? HtmlPropertyError { get; private set; }

    protected bool HtmlElementEditorOpen { get; private set; }

    protected bool HtmlRichTextEditorOpen { get; private set; }

    protected string? HtmlRichTextError { get; private set; }

    protected bool HtmlMediaSelectorOpen { get; private set; }

    protected bool HtmlFragmentImportOpen { get; private set; }

    protected string? HtmlFragmentImportError { get; private set; }

    protected bool MarkdownInterchangeOpen { get; private set; }

    protected MarkdownInterchangeMode MarkdownMode { get; private set; }

    protected string MarkdownInterchangeText { get; private set; } = string.Empty;

    protected string? MarkdownInterchangeError { get; private set; }

    protected bool MarkdownFragmentEditorOpen { get; private set; }

    protected string MarkdownFragmentInitialHtml { get; private set; } = string.Empty;

    protected string? MarkdownFragmentError { get; private set; }

    protected bool CustomHtmlFragmentEditorOpen { get; private set; }

    protected string CustomHtmlFragmentInitialSource { get; private set; } = string.Empty;

    protected string? CustomHtmlFragmentError { get; private set; }

    protected bool ScribanFragmentEditorOpen { get; private set; }

    protected string ScribanFragmentInitialSource { get; private set; } = string.Empty;

    protected string? ScribanFragmentError { get; private set; }

    protected PageRenderedFragment? SelectedRenderedFragment =>
        HtmlEditor.SelectedNodeId is { } nodeId
            ? (HtmlEditor.Composition.RenderedFragments ?? [])
                .FirstOrDefault(fragment => fragment.NodeId == nodeId)
            : null;

    protected IReadOnlyList<PageRegisteredFragmentDescriptor> RegisteredFragmentDescriptors { get; private set; } = [];

    protected PageRegisteredFragment? SelectedRegisteredFragment =>
        HtmlEditor.SelectedNodeId is { } nodeId
            ? (HtmlEditor.Composition.RegisteredFragments ?? [])
                .FirstOrDefault(fragment => fragment.NodeId == nodeId)
            : null;

    protected PageRegisteredFragmentDescriptor? SelectedRegisteredFragmentDescriptor =>
        SelectedRegisteredFragment is { } fragment
            ? RegisteredFragmentDescriptors.FirstOrDefault(descriptor =>
                string.Equals(descriptor.Key, fragment.Key, StringComparison.Ordinal))
            : null;

    protected bool RegisteredFragmentEditorOpen { get; private set; }

    protected string? RegisteredFragmentError { get; private set; }

    private HtmlMediaTargetKind? _htmlMediaTarget;

    private IStyleProfile? _siteStyleProfile;
    private bool _styleProfileResolutionAttempted;

    private static HtmlPageEditorSession CreateHtmlEditorSession(
        HtmlPageContent content,
        IStyleProfile styleProfile,
        PageCompositionDocument? composition = null) => new(
        content,
        HtmlCatalog,
        HtmlContentPolicy,
        new HtmlContentValidator(
            HtmlCatalog,
            HtmlContentPolicy,
            new HtmlAttributePolicy()),
        new HtmlLayoutStarterFactory(HtmlCatalog),
        new HtmlComponentTemplateFactory(HtmlCatalog),
        new NativeCssStyleCompiler(),
        styleProfile,
        composition);

    private static IHtmlFragmentImporter CreateHtmlFragmentImporter() => new HtmlFragmentImporter(
        HtmlCatalog,
        new HtmlAttributePolicy(),
        HtmlContentPolicy,
        new HtmlContentValidator(HtmlCatalog, HtmlContentPolicy, new HtmlAttributePolicy()));

    private static IMarkdownInterchangeAdapter CreateMarkdownInterchangeAdapter() =>
        new MarkdownInterchangeAdapter(
            CreateHtmlFragmentImporter(),
            new HtmlContentValidator(HtmlCatalog, HtmlContentPolicy, new HtmlAttributePolicy()));

    private static HtmlStaticRenderer CreateHtmlStaticRenderer() => new(
        HtmlCatalog,
        HtmlContentPolicy,
        new HtmlAttributePolicy(),
        new HtmlContentValidator(HtmlCatalog, HtmlContentPolicy, new HtmlAttributePolicy()));

    // UI state
        /// <summary>
    /// Gets or sets the Preview Mode.
    /// </summary>
protected bool   PreviewMode      { get; set; }
        /// <summary>
    /// Gets or sets the Is Preview Rendering.
    /// </summary>
protected bool   IsPreviewRendering { get; set; }
        /// <summary>
    /// Gets or sets the Preview Html.
    /// </summary>
protected string? PreviewHtml { get; set; }
        /// <summary>
    /// Gets or sets the Preview Error.
    /// </summary>
protected string? PreviewError { get; set; }
        /// <summary>
    /// Gets or sets the Preview Fragment Url.
    /// </summary>
protected string PreviewFragmentUrl => BuildAbsoluteUrl("api/v1/admin/preview/pages/render-fragment");
        /// <summary>
    /// Gets or sets the Preview Frame Url.
    /// </summary>
protected string? PreviewFrameUrl => Id is { } id
        ? BuildAbsoluteUrl($"_cms/preview/pages/drafts/{id}?previewVersion={_previewRefreshVersion}", _previewBaseUri)
        : null;
        /// <summary>
    /// Gets or sets the Preview Frame Document.
    /// </summary>
protected string PreviewFrameDocument => BuildPreviewFrameDocument(PreviewHtml, NavManager.BaseUri, L);
        /// <summary>
    /// Gets or sets the Right Sidebar Collapsed.
    /// </summary>
protected bool   RightSidebarCollapsed { get; set; } = true;
        /// <summary>
    /// Gets or sets the Is Saving.
    /// </summary>
protected bool   IsSaving              { get; set; }
        /// <summary>
    /// Gets or sets the Active Tab.
    /// </summary>
protected string ActiveTab             { get; set; } = "editor";

    private const string SidebarStateStorageKey = "aero.page-editor.sidebar-state.v1";

    // Page Settings
        /// <summary>
    /// Gets or sets the Page Slug.
    /// </summary>
protected string PageSlug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Summary.
    /// </summary>
protected string Summary { get; set; } = string.Empty;

    /// <summary>Tracks whether the slug should continue following the title.</summary>
    private enum SlugState { Auto, Locked }
    private SlugState _slugState = SlugState.Auto;

    // Redundant ID removed to avoid ambiguity with ManagerComponent Base.Id
    // public string Id { get; set; } = string.Empty; 

    private string SeoTitle { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Seo Description.
    /// </summary>
protected string SeoDescription { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Show In Nav Menu.
    /// </summary>
protected bool   ShowInNavMenu { get; set; } = true;
        /// <summary>
    /// Gets or sets the Show Header Navigation.
    /// </summary>
protected bool   ShowHeaderNavigation { get; set; } = true;
        /// <summary>
    /// Gets or sets the Hide Footer.
    /// </summary>
protected bool   HideFooter { get; set; }
        /// <summary>
    /// Gets or sets the Show Chat Agent.
    /// </summary>
protected bool   ShowChatAgent { get; set; } = true;
        /// <summary>
    /// Gets or sets the Publication State.
    /// </summary>
protected ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    /// <summary>Optional parent page ID to pre-select when creating a new child page.</summary>
    [SupplyParameterFromQuery(Name = "parentId")]
    protected long? ParentId { get; set; }

        /// <summary>
    /// Gets or sets the Requested Tab.
    /// </summary>
[SupplyParameterFromQuery(Name = "tab")]
    protected string? RequestedTab { get; set; }

    /// <summary>Read-only parent path prefix shown as a pill before the slug input.</summary>
    protected string ParentSlugPrefix { get; set; } = "";

        /// <summary>
    /// Gets or sets the Loaded Page.
    /// </summary>
protected CmsPageDetail? LoadedPage { get; set; }
        /// <summary>
    /// Gets or sets the Current Site.
    /// </summary>
protected SiteViewModel? CurrentSite { get; set; }
        /// <summary>
    /// Gets or sets the Page Culture Variants.
    /// </summary>
protected IReadOnlyList<CmsPageDetail> PageCultureVariants { get; set; } = [];
        /// <summary>
    /// Gets or sets the Selected Translation Culture.
    /// </summary>
protected string SelectedTranslationCulture { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Translation Slug.
    /// </summary>
protected string TranslationSlug { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Is Loading Translations.
    /// </summary>
protected bool IsLoadingTranslations { get; set; }
        /// <summary>
    /// Gets or sets the Is Creating Translation.
    /// </summary>
protected bool IsCreatingTranslation { get; set; }
        /// <summary>
    /// Gets or sets the Is Bulk Publishing Translations.
    /// </summary>
protected bool IsBulkPublishingTranslations { get; set; }
        /// <summary>
    /// Gets or sets the Is Translating All.
    /// </summary>
protected bool IsTranslatingAll { get; set; }
        /// <summary>
    /// Gets or sets the Overwrite Existing Translations.
    /// </summary>
protected bool OverwriteExistingTranslations { get; set; }
        /// <summary>
    /// Gets or sets the Translating Cultures.
    /// </summary>
protected HashSet<string> TranslatingCultures { get; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
    /// Gets or sets the Supported Cultures.
    /// </summary>
protected IReadOnlyList<string> SupportedCultures =>
        CurrentSite?.SupportedCultures is { Count: > 0 } cultures
            ? cultures
            : [LoadedPage?.Culture ?? CurrentSite?.DefaultCulture ?? "en-US"];

        /// <summary>
    /// Gets or sets the Available Translation Cultures.
    /// </summary>
protected IEnumerable<string> AvailableTranslationCultures =>
        SupportedCultures
            .Select(NormalizeCultureName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(culture => !PageCultureVariants.Any(variant =>
                string.Equals(variant.Culture, culture, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    // Toasts
        /// <summary>
    /// Gets or sets the Toasts.
    /// </summary>
protected List<ToastMessage> Toasts { get; set; } = [];

    // Auto-save timer & dirty tracking
    private const int PreviewDebounceMilliseconds = 300;
    private System.Timers.Timer? _autoSaveTimer;
    private CancellationTokenSource? _previewDebounceCts;
    private string? _previewBaseUri;
    private long _previewRefreshVersion;	
    /// <summary>Tracks whether unsaved changes exist. Auto-save only fires when Dirty.</summary>
    private enum PageState { Clean, Dirty }
    private PageState _pageState = PageState.Dirty;  // new pages start dirty
    private bool _routeDecisionPending;

    // ──────────────────────────────────────────────────────────
    // Lifecycle  (mirrors Alpine.js init())
    // ──────────────────────────────────────────────────────────

    private long? _previousParentId;
    private long? _loadedPageId;
    private bool _isPersistedPageLoaded;

        /// <summary>
    /// OnParametersSetAsync method.
    /// </summary>
protected override async Task OnParametersSetAsync()
    {
        // Route parameters can change while this component remains alive (for
        // example, after creating a page and replacing /editor with
        // /editor/{id}). Loading in OnInitializedAsync misses that transition
        // and leaves an interactive WASM instance holding the new ID with the
        // default "Homepage" draft. Always synchronize routed resource state
        // from OnParametersSetAsync instead.
        if (Id is { } pageId && _loadedPageId != pageId)
        {
            _isPersistedPageLoaded = false;
            await LoadPageAsync(pageId);
        }

        if (IsKnownTab(RequestedTab))
        {
            ActiveTab = NormalizeTab(RequestedTab);
        }

        if (_previousParentId != ParentId)
        {
            _previousParentId = ParentId;
            await RefreshParentSlugPrefixAsync();
        }
    }

        /// <summary>
    /// OnInitializedAsync method.
    /// </summary>
protected override async Task OnInitializedAsync()
    {
        RestoreSidebarState();

        await ResolvePreviewBaseUriAsync();
        CurrentSite = await ResolveCurrentSiteAsync();
        await EnsureSiteStyleProfileAsync();
        await LoadRegisteredFragmentCatalogAsync();

        if (!Id.HasValue)
        {
            UpdateLastSaved();
        }

        await RefreshParentSlugPrefixAsync();

        _autoSaveTimer = new System.Timers.Timer(30_000);
        _autoSaveTimer.Elapsed += async (_, _) => await InvokeAsync(AutoSaveAsync);
        _autoSaveTimer.AutoReset = true;
        _autoSaveTimer.Start();
    }

    private async Task LoadRegisteredFragmentCatalogAsync()
    {
        var result = await PagesClient.GetRegisteredFragmentsAsync();
        switch (result)
        {
            case Result<IReadOnlyList<PageRegisteredFragmentDescriptor>, AeroError>.Ok ok:
                RegisteredFragmentDescriptors = ok.Value;
                break;
            case Result<IReadOnlyList<PageRegisteredFragmentDescriptor>, AeroError>.Failure failure:
                RegisteredFragmentDescriptors = [];
                ShowToast($"Registered fragments unavailable: {FormatError(failure.Error)}", "error");
                break;
        }
    }

    private async Task LoadPageAsync(long id)
    {
        var result = await PagesClient.GetByIdAsync(id);
        if (result is Result<CmsPageDetail, AeroError>.Ok ok)
        {
            var page = ok.Value;
            if (!await BindPageOwnerStyleProfileAsync(page.SiteId))
            {
                return;
            }

            LoadedPage = page;
            PageTitle = page.Title;
            PageSlug = page.Slug;
            // A loaded page follows its title until the author explicitly edits
            // the slug. This keeps the common title -> URL workflow predictable.
            _slugState = SlugState.Auto;
            Summary = page.Excerpt ?? string.Empty;
            SeoTitle = page.SeoTitle ?? string.Empty;
            SeoDescription = page.SeoDescription ?? string.Empty;
            PublicationState = page.PublicationState;
            ShowInNavMenu = page.ShowInNavMenu; 
            ShowHeaderNavigation = page.ShowHeaderNavigation;
            HideFooter = page.HideFooter;
            ShowChatAgent = page.ShowChatAgent;
            ParentId = page.ParentId;
            
            HtmlEditor = CreateHtmlEditorSession(
                page.DraftContent ?? new HtmlPageContent(),
                _siteStyleProfile!,
                page.DraftComposition);

            UpdateLastSaved();
            _pageState = PageState.Clean;
            _loadedPageId = id;
            _isPersistedPageLoaded = true;
            await LoadPageTranslationsAsync();
            await InvokeAsync(StateHasChanged);
        }
        else
        {
            ShowToast(L["Error loading page"], "error");
        }
    }

    /// <summary>
    /// Sets <see cref="ParentSlugPrefix"/> from the loaded page's Path
    /// or by loading the parent page via the API.
    /// </summary>
    private async Task RefreshParentSlugPrefixAsync()
    {
        if (ParentId is null or <= 0)
        {
            ParentSlugPrefix = "";
            return;
        }

        // Try to derive from the loaded page's Path first
        if (LoadedPage is { Path: { Length: > 1 } path })
        {
            var trimmed = path.TrimStart('/');
            var lastSlash = trimmed.LastIndexOf('/');
            ParentSlugPrefix = lastSlash > 0 ? trimmed[..lastSlash] : "";
            if (!string.IsNullOrEmpty(ParentSlugPrefix))
                return;
        }

        // Fallback: load the parent page to get its slug
        try
        {
            var result = await PagesClient.GetByIdAsync(ParentId.Value);
            if (result is Result<CmsPageDetail, AeroError>.Ok { Value: var parent })
            {
                var parentPath = !string.IsNullOrEmpty(parent.Path) ? parent.Path.TrimStart('/').TrimEnd('/') : parent.Slug;
                ParentSlugPrefix = parentPath;
            }
        }
        catch { ParentSlugPrefix = ""; }
    }

        /// <summary>
    /// DisposeAsync method.
    /// </summary>
public ValueTask DisposeAsync()
    {
        _autoSaveTimer?.Dispose();
        _previewDebounceCts?.Cancel();
        _previewDebounceCts?.Dispose();
        return ValueTask.CompletedTask;
    }

        /// <summary>
    /// OnRightSidebarCollapsedChanged method.
    /// </summary>
protected Task OnRightSidebarCollapsedChanged(bool isCollapsed)
    {
        RightSidebarCollapsed = isCollapsed;
        PersistSidebarState();
        return Task.CompletedTask;
    }

    private void RestoreSidebarState()
    {
        var state = AdminStorage.GetItem<SidebarState>(SidebarStateStorageKey);
        if (state is null)
        {
            return;
        }

        RightSidebarCollapsed = state.RightSidebarCollapsed;
    }

    private void PersistSidebarState() =>
        AdminStorage.SetItem(
            SidebarStateStorageKey,
            new SidebarState(RightSidebarCollapsed));

    private sealed record SidebarState(bool RightSidebarCollapsed);
    // ──────────────────────────────────────────────────────────
    // Preview  (mirrors togglePreview())
    // ──────────────────────────────────────────────────────────

        /// <summary>
    /// TogglePreview method.
    /// </summary>
protected async Task TogglePreview()
    {
        PreviewMode = !PreviewMode;
        if (PreviewMode)
        {
            HtmlEditor.Select(null);
            await RefreshPreviewAsync();
        }

        await InvokeAsync(StateHasChanged);
    }

    protected async Task SelectHtmlNodeAsync(long? nodeId)
    {
        HtmlEditor.Select(nodeId);
        HtmlPropertyError = null;
        ContentListSettingsError = null;
        if (nodeId is not null)
        {
            RightSidebarCollapsed = false;
            RightSidebarTab = HtmlPageEditorSidebarTab.Inspector;
            await EnsureSelectedContentScopeMetadataAsync();
        }
        else if (RightSidebarTab == HtmlPageEditorSidebarTab.Inspector)
        {
            RightSidebarTab = HtmlPageEditorSidebarTab.Elements;
        }

    }

    protected Task ClearHtmlSelectionAsync() => SelectHtmlNodeAsync(null);

    protected Task AddHtmlElementAsync(string tagName)
    {
        var result = HtmlEditor.AddElement(tagName);
        HandleHtmlEditorResult(result, $"Added <{tagName}>.");
        return Task.CompletedTask;
    }

    protected Task AddHtmlLayoutAsync(HtmlLayoutStarterKind kind)
    {
        var result = HtmlEditor.AddLayout(kind);
        HandleHtmlEditorResult(result, "Layout added.");
        return Task.CompletedTask;
    }

    protected Task AddHtmlComponentAsync(HtmlComponentTemplateKind kind)
    {
        var result = HtmlEditor.AddComponent(kind);
        HandleHtmlEditorResult(result, "Component added.");
        return Task.CompletedTask;
    }

    protected Task AddRenderedFragmentAsync(PageRenderedFragmentKind kind)
    {
        var result = HtmlEditor.AddRenderedFragment(kind, DefaultRenderedFragmentSource(kind));
        HandleHtmlEditorResult(result, $"{RenderedFragmentDisplayName(kind)} block added.");
        return result is Result<HtmlNode>.Ok ok
            ? OpenRenderedFragmentEditorForNodeAsync(ok.Value.NodeId)
            : Task.CompletedTask;
    }

    protected Task AddRegisteredFragmentAsync(string key)
    {
        var descriptor = RegisteredFragmentDescriptors.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, key, StringComparison.Ordinal));
        if (descriptor is null)
        {
            ShowToast("The selected registered fragment is no longer available.", "error");
            return Task.CompletedTask;
        }

        var result = HtmlEditor.AddRegisteredFragment(key, CreateDefaultParameters(descriptor));
        HandleHtmlEditorResult(result, $"{descriptor.DisplayName} added.");
        return result is Result<HtmlNode>.Ok ok
            ? OpenRegisteredFragmentEditorForNodeAsync(ok.Value.NodeId)
            : Task.CompletedTask;
    }

    protected Task OpenHtmlFragmentImportAsync()
    {
        HtmlFragmentImportError = null;
        HtmlFragmentImportOpen = true;
        return Task.CompletedTask;
    }

    protected Task CloseHtmlFragmentImportAsync()
    {
        HtmlFragmentImportOpen = false;
        HtmlFragmentImportError = null;
        return Task.CompletedTask;
    }

    protected Task ImportHtmlFragmentAsync(string fragment)
    {
        var imported = CreateHtmlFragmentImporter().Import(fragment);
        switch (imported)
        {
            case Result<HtmlPageContent>.Ok ok:
            {
                var inserted = HtmlEditor.InsertImportedFragment(ok.Value);
                if (inserted is Result<IReadOnlyList<HtmlNode>>.Failure failure)
                {
                    HtmlFragmentImportError = FormatError(failure.Error);
                    return Task.CompletedTask;
                }

                HtmlFragmentImportOpen = false;
                HtmlFragmentImportError = null;
                MarkDirty();
                ShowToast("HTML imported.", "success");
                break;
            }
            case Result<HtmlPageContent>.Failure failure:
                HtmlFragmentImportError = FormatError(failure.Error);
                break;
            default:
                HtmlFragmentImportError = "The HTML fragment could not be imported.";
                break;
        }

        return Task.CompletedTask;
    }

    protected Task OpenMarkdownImportAsync()
    {
        MarkdownMode = MarkdownInterchangeMode.Import;
        MarkdownInterchangeText = string.Empty;
        MarkdownInterchangeError = null;
        MarkdownInterchangeOpen = true;
        return Task.CompletedTask;
    }

    protected Task OpenMarkdownExportAsync()
    {
        MarkdownMode = MarkdownInterchangeMode.Export;
        MarkdownInterchangeOpen = true;

        var exported = CreateMarkdownInterchangeAdapter().Export(HtmlEditor.Content);
        switch (exported)
        {
            case Result<string>.Ok ok:
                MarkdownInterchangeText = ok.Value;
                MarkdownInterchangeError = null;
                break;
            case Result<string>.Failure failure:
                MarkdownInterchangeText = string.Empty;
                MarkdownInterchangeError = FormatError(failure.Error);
                break;
            default:
                MarkdownInterchangeText = string.Empty;
                MarkdownInterchangeError = "The page could not be exported to Markdown.";
                break;
        }

        return Task.CompletedTask;
    }

    protected Task CloseMarkdownInterchangeAsync()
    {
        MarkdownInterchangeOpen = false;
        MarkdownInterchangeText = string.Empty;
        MarkdownInterchangeError = null;
        return Task.CompletedTask;
    }

    protected Task ImportMarkdownAsync(string markdown)
    {
        var imported = CreateMarkdownInterchangeAdapter().Import(markdown);
        switch (imported)
        {
            case Result<HtmlPageContent>.Ok ok:
            {
                var inserted = HtmlEditor.InsertImportedFragment(ok.Value);
                if (inserted is Result<IReadOnlyList<HtmlNode>>.Failure failure)
                {
                    MarkdownInterchangeError = FormatError(failure.Error);
                    return Task.CompletedTask;
                }

                MarkdownInterchangeOpen = false;
                MarkdownInterchangeText = string.Empty;
                MarkdownInterchangeError = null;
                MarkDirty();
                ShowToast("Markdown imported.", "success");
                break;
            }
            case Result<HtmlPageContent>.Failure failure:
                MarkdownInterchangeError = FormatError(failure.Error);
                break;
            default:
                MarkdownInterchangeError = "The Markdown content could not be imported.";
                break;
        }

        return Task.CompletedTask;
    }

    protected Task MoveHtmlNodeAsync(HtmlSortMoveIntent intent)
    {
        var result = HtmlEditor.MoveRelative(intent.NodeId, intent.TargetNodeId, intent.Placement);
        HandleHtmlEditorResult(result, null);
        return Task.CompletedTask;
    }

    protected Task InsertHtmlPaletteItemAsync(HtmlPaletteInsertIntent intent)
    {
        Result<HtmlNode> result = intent.ItemKind switch
        {
            HtmlPaletteItemKind.Element => HtmlEditor.AddElementRelative(
                intent.ItemValue,
                intent.TargetNodeId,
                intent.Placement),
            HtmlPaletteItemKind.Layout when Enum.TryParse<HtmlLayoutStarterKind>(
                intent.ItemValue,
                true,
                out var layoutKind) => HtmlEditor.AddLayoutRelative(
                    layoutKind,
                    intent.TargetNodeId,
                    intent.Placement),
            HtmlPaletteItemKind.Component when Enum.TryParse<HtmlComponentTemplateKind>(
                intent.ItemValue,
                true,
                out var componentKind) => HtmlEditor.AddComponentRelative(
                    componentKind,
                    intent.TargetNodeId,
                    intent.Placement),
            HtmlPaletteItemKind.ContentList
                or HtmlPaletteItemKind.ContentItem
                or HtmlPaletteItemKind.ContentField
                when HtmlContentPaletteRequest.TryParse(
                    intent.ItemKind,
                    intent.ItemValue,
                    out var contentRequest) => InsertContentPaletteItemRelative(
                        contentRequest!,
                    intent.TargetNodeId,
                    intent.Placement),
            HtmlPaletteItemKind.RenderedFragment when Enum.TryParse<PageRenderedFragmentKind>(
                intent.ItemValue,
                true,
                out var fragmentKind) => HtmlEditor.AddRenderedFragmentRelative(
                    fragmentKind,
                    DefaultRenderedFragmentSource(fragmentKind),
                    intent.TargetNodeId,
                    intent.Placement),
            HtmlPaletteItemKind.RegisteredFragment
                when RegisteredFragmentDescriptors.FirstOrDefault(descriptor =>
                    string.Equals(descriptor.Key, intent.ItemValue, StringComparison.Ordinal)) is { } descriptor
                => HtmlEditor.AddRegisteredFragmentRelative(
                    descriptor.Key,
                    CreateDefaultParameters(descriptor),
                    intent.TargetNodeId,
                    intent.Placement),
            _ => AeroError.ValidationError(["The dragged palette item is not supported."])
        };

        var successMessage = intent.ItemKind switch
        {
            HtmlPaletteItemKind.Layout => "Layout added.",
            HtmlPaletteItemKind.Component => "Component added.",
            HtmlPaletteItemKind.ContentList
                or HtmlPaletteItemKind.ContentItem
                or HtmlPaletteItemKind.ContentField => ContentPaletteSuccessMessage(intent.ItemKind),
            HtmlPaletteItemKind.RenderedFragment when Enum.TryParse<PageRenderedFragmentKind>(
                intent.ItemValue,
                true,
                out var insertedFragmentKind) => $"{RenderedFragmentDisplayName(insertedFragmentKind)} block added.",
            HtmlPaletteItemKind.RegisteredFragment => $"{RegisteredFragmentDisplayName(intent.ItemValue)} added.",
            _ => $"Added <{intent.ItemValue}>."
        };
        HandleHtmlEditorResult(result, successMessage);
        return result is Result<HtmlNode>.Ok inserted
            ? intent.ItemKind switch
            {
                HtmlPaletteItemKind.RenderedFragment => OpenRenderedFragmentEditorForNodeAsync(inserted.Value.NodeId),
                HtmlPaletteItemKind.RegisteredFragment => OpenRegisteredFragmentEditorForNodeAsync(inserted.Value.NodeId),
                _ => Task.CompletedTask
            }
            : Task.CompletedTask;
    }

    protected Task RemoveSelectedHtmlNodeAsync()
    {
        var result = HtmlEditor.RemoveSelected();
        HandleHtmlEditorResult(result, "Element removed.");
        if (result is Result<HtmlNode>.Ok)
        {
            HtmlElementEditorOpen = false;
            if (RightSidebarTab == HtmlPageEditorSidebarTab.Inspector)
            {
                RightSidebarTab = HtmlPageEditorSidebarTab.Elements;
            }
        }
        return Task.CompletedTask;
    }

    protected Task DuplicateSelectedHtmlNodeAsync()
    {
        var result = HtmlEditor.DuplicateSelected();
        HandleHtmlEditorResult(result, "Element duplicated.");
        return Task.CompletedTask;
    }

    protected Task ApplyHtmlEditorCommandAsync(HtmlEditorCommandKind command) => command switch
    {
        HtmlEditorCommandKind.Undo when HtmlEditor.CanUndo => UndoHtmlChangeAsync(),
        HtmlEditorCommandKind.Redo when HtmlEditor.CanRedo => RedoHtmlChangeAsync(),
        HtmlEditorCommandKind.MoveUp when HtmlEditor.CanMoveSelectedUp => MoveSelectedHtmlNodeUpAsync(),
        HtmlEditorCommandKind.MoveDown when HtmlEditor.CanMoveSelectedDown => MoveSelectedHtmlNodeDownAsync(),
        HtmlEditorCommandKind.Duplicate => DuplicateSelectedHtmlNodeAsync(),
        HtmlEditorCommandKind.Edit => OpenSelectedHtmlElementEditorAsync(),
        HtmlEditorCommandKind.Delete => RemoveSelectedHtmlNodeAsync(),
        _ => Task.CompletedTask
    };

    protected Task MoveSelectedHtmlNodeUpAsync()
    {
        var result = HtmlEditor.MoveSelectedUp();
        HandleHtmlEditorResult(result, "Element moved up.");
        return Task.CompletedTask;
    }

    protected Task MoveSelectedHtmlNodeDownAsync()
    {
        var result = HtmlEditor.MoveSelectedDown();
        HandleHtmlEditorResult(result, "Element moved down.");
        return Task.CompletedTask;
    }

    protected Task UndoHtmlChangeAsync()
    {
        var result = HtmlEditor.Undo();
        HandleHtmlEditorResult(result, "Change undone.");
        return Task.CompletedTask;
    }

    protected Task RedoHtmlChangeAsync()
    {
        var result = HtmlEditor.Redo();
        HandleHtmlEditorResult(result, "Change redone.");
        return Task.CompletedTask;
    }

    protected Task ApplyHtmlPropertiesAsync(HtmlNodeProperties properties)
    {
        var result = HtmlEditor.UpdateSelectedProperties(properties);
        switch (result)
        {
            case Result<HtmlNode>.Ok:
                HtmlPropertyError = null;
                MarkDirty();
                ShowToast(L["Element updated."], "success");
                break;
            case Result<HtmlNode>.Failure failure:
                HtmlPropertyError = FormatError(failure.Error);
                ShowToast(HtmlPropertyError, "error");
                break;
        }

        return Task.CompletedTask;
    }

    protected Task OpenHtmlRichTextEditorAsync()
    {
        HtmlElementEditorOpen = false;
        HtmlRichTextError = null;
        HtmlRichTextEditorOpen = HtmlEditor.SelectedNode is not null;
        return Task.CompletedTask;
    }

    protected Task OpenHtmlRichTextForNodeAsync(long nodeId)
    {
        HtmlEditor.Select(nodeId);
        HtmlPropertyError = null;
        RightSidebarCollapsed = false;
        RightSidebarTab = HtmlPageEditorSidebarTab.Inspector;

        var node = HtmlEditor.SelectedNode;
        var converter = new TiptapInlineContentConverter();
        HtmlRichTextEditorOpen = node is not null
            && SelectedHtmlDefinition?.ChildModel is HtmlChildModel.Phrasing
            && converter.CanEdit(node);
        HtmlRichTextError = null;
        return Task.CompletedTask;
    }

    protected Task OpenHtmlElementEditorForNodeAsync(long nodeId)
    {
        HtmlEditor.Select(nodeId);
        if (SelectedRenderedFragment is not null)
        {
            return OpenRenderedFragmentEditorForNodeAsync(nodeId);
        }

        if (SelectedRegisteredFragment is not null)
        {
            return OpenRegisteredFragmentEditorForNodeAsync(nodeId);
        }

        HtmlPropertyError = null;
        HtmlRichTextEditorOpen = false;
        RightSidebarCollapsed = false;
        RightSidebarTab = HtmlPageEditorSidebarTab.Inspector;
        HtmlElementEditorOpen = HtmlEditor.SelectedNode is { Kind: HtmlNodeKind.Element }
            && SelectedHtmlDefinition is not null;
        return Task.CompletedTask;
    }

    protected Task OpenSelectedHtmlElementEditorAsync() => HtmlEditor.SelectedNodeId is { } nodeId
        ? OpenHtmlElementEditorForNodeAsync(nodeId)
        : Task.CompletedTask;

    protected Task OpenRegisteredFragmentEditorForNodeAsync(long nodeId)
    {
        HtmlEditor.Select(nodeId);
        HtmlElementEditorOpen = false;
        HtmlRichTextEditorOpen = false;
        MarkdownFragmentEditorOpen = false;
        CustomHtmlFragmentEditorOpen = false;
        ScribanFragmentEditorOpen = false;
        RegisteredFragmentEditorOpen = false;
        RegisteredFragmentError = null;

        if (SelectedRegisteredFragment is null || SelectedRegisteredFragmentDescriptor is null)
        {
            RegisteredFragmentError = "The selected registered fragment or its provider is unavailable.";
            RegisteredFragmentEditorOpen = false;
            return Task.CompletedTask;
        }

        RegisteredFragmentEditorOpen = true;
        RightSidebarCollapsed = false;
        RightSidebarTab = HtmlPageEditorSidebarTab.Inspector;
        return Task.CompletedTask;
    }

    protected Task CloseRegisteredFragmentEditorAsync()
    {
        RegisteredFragmentEditorOpen = false;
        RegisteredFragmentError = null;
        return Task.CompletedTask;
    }

    protected async Task ApplyRegisteredFragmentParametersAsync(
        IReadOnlyDictionary<string, JsonElement> parameters)
    {
        if (SelectedRegisteredFragment is not { } fragment)
        {
            RegisteredFragmentError = "The selected registered fragment no longer exists.";
            return;
        }

        var updated = fragment with
        {
            Parameters = parameters.ToDictionary(
                parameter => parameter.Key,
                parameter => parameter.Value.Clone(),
                StringComparer.Ordinal)
        };
        var fragments = (HtmlEditor.Composition.RegisteredFragments ?? [])
            .Select(candidate => candidate.NodeId == fragment.NodeId ? updated : candidate)
            .ToArray();
        var candidateComposition = HtmlEditor.Composition with { RegisteredFragments = fragments };
        var culture = LoadedPage?.Culture
            ?? CurrentSite?.DefaultCulture
            ?? CultureInfo.CurrentUICulture.Name;
        var preview = await PreviewClient.RenderPageFragmentAsync(
            HtmlEditor.Content,
            candidateComposition,
            culture);
        if (preview is Result<string, AeroError>.Failure previewFailure)
        {
            RegisteredFragmentError = FormatError(previewFailure.Error);
            return;
        }

        var result = HtmlEditor.UpdateRegisteredFragmentParameters(fragment.NodeId, parameters);
        switch (result)
        {
            case Result<PageRegisteredFragment>.Ok:
                RegisteredFragmentEditorOpen = false;
                RegisteredFragmentError = null;
                MarkDirty();
                ShowToast("Registered fragment updated.", "success");
                break;
            case Result<PageRegisteredFragment>.Failure failure:
                RegisteredFragmentError = FormatError(failure.Error);
                break;
        }
    }

    private static IReadOnlyDictionary<string, JsonElement> CreateDefaultParameters(
        PageRegisteredFragmentDescriptor descriptor) => descriptor.Parameters
        .Where(parameter => parameter.DefaultValue.HasValue)
        .ToDictionary(
            parameter => parameter.Name,
            parameter => parameter.DefaultValue!.Value.Clone(),
            StringComparer.Ordinal);

    private string RegisteredFragmentDisplayName(string key) =>
        RegisteredFragmentDescriptors.FirstOrDefault(descriptor =>
            string.Equals(descriptor.Key, key, StringComparison.Ordinal))?.DisplayName
        ?? key;

    protected Task CloseHtmlElementEditorAsync()
    {
        HtmlElementEditorOpen = false;
        HtmlPropertyError = null;
        return Task.CompletedTask;
    }

    protected Task CloseHtmlRichTextEditorAsync()
    {
        HtmlRichTextEditorOpen = false;
        HtmlRichTextError = null;
        return Task.CompletedTask;
    }

    protected Task OpenRenderedFragmentEditorForNodeAsync(long nodeId)
    {
        HtmlEditor.Select(nodeId);
        HtmlElementEditorOpen = false;
        HtmlRichTextEditorOpen = false;
        MarkdownFragmentEditorOpen = false;
        CustomHtmlFragmentEditorOpen = false;
        ScribanFragmentEditorOpen = false;
        RegisteredFragmentEditorOpen = false;
        MarkdownFragmentError = null;
        MarkdownFragmentInitialHtml = string.Empty;

        var fragment = SelectedRenderedFragment;
        if (fragment is null)
        {
            MarkdownFragmentError = "The selected rendered fragment does not have an available editor.";
            return Task.CompletedTask;
        }

        if (fragment.Kind == PageRenderedFragmentKind.CustomHtml)
        {
            CustomHtmlFragmentInitialSource = fragment.Source;
            CustomHtmlFragmentError = null;
            CustomHtmlFragmentEditorOpen = true;
            RightSidebarCollapsed = false;
            RightSidebarTab = HtmlPageEditorSidebarTab.Inspector;
            return Task.CompletedTask;
        }

        if (fragment.Kind == PageRenderedFragmentKind.Scriban)
        {
            ScribanFragmentInitialSource = fragment.Source;
            ScribanFragmentError = null;
            ScribanFragmentEditorOpen = true;
            RightSidebarCollapsed = false;
            RightSidebarTab = HtmlPageEditorSidebarTab.Inspector;
            return Task.CompletedTask;
        }

        if (fragment.Kind != PageRenderedFragmentKind.Markdown)
        {
            MarkdownFragmentError = "The selected rendered fragment does not have an available editor.";
            return Task.CompletedTask;
        }

        var imported = CreateMarkdownInterchangeAdapter().Import(fragment.Source);
        if (imported is Result<HtmlPageContent>.Failure importFailure)
        {
            MarkdownFragmentError = FormatError(importFailure.Error);
            return Task.CompletedTask;
        }

        var rendered = CreateHtmlStaticRenderer().Render(
            ((Result<HtmlPageContent>.Ok)imported).Value);
        if (rendered is Result<string>.Failure renderFailure)
        {
            MarkdownFragmentError = FormatError(renderFailure.Error);
            return Task.CompletedTask;
        }

        MarkdownFragmentInitialHtml = ((Result<string>.Ok)rendered).Value;
        MarkdownFragmentEditorOpen = true;
        RightSidebarCollapsed = false;
        RightSidebarTab = HtmlPageEditorSidebarTab.Inspector;
        return Task.CompletedTask;
    }

    protected Task CloseMarkdownFragmentEditorAsync()
    {
        MarkdownFragmentEditorOpen = false;
        MarkdownFragmentInitialHtml = string.Empty;
        MarkdownFragmentError = null;
        return Task.CompletedTask;
    }

    protected Task ApplyMarkdownFragmentHtmlAsync(string html)
    {
        if (SelectedRenderedFragment is not { Kind: PageRenderedFragmentKind.Markdown } fragment)
        {
            MarkdownFragmentError = "The selected Markdown fragment no longer exists.";
            return Task.CompletedTask;
        }

        var imported = CreateHtmlFragmentImporter().Import(html);
        if (imported is Result<HtmlPageContent>.Failure importFailure)
        {
            MarkdownFragmentError = FormatError(importFailure.Error);
            return Task.CompletedTask;
        }

        var exported = CreateMarkdownInterchangeAdapter().Export(
            ((Result<HtmlPageContent>.Ok)imported).Value);
        if (exported is Result<string>.Failure exportFailure)
        {
            MarkdownFragmentError = FormatError(exportFailure.Error);
            return Task.CompletedTask;
        }

        var updated = HtmlEditor.UpdateRenderedFragmentSource(
            fragment.NodeId,
            ((Result<string>.Ok)exported).Value);
        switch (updated)
        {
            case Result<PageRenderedFragment>.Ok:
                MarkdownFragmentEditorOpen = false;
                MarkdownFragmentInitialHtml = string.Empty;
                MarkdownFragmentError = null;
                MarkDirty();
                ShowToast("Markdown block updated.", "success");
                break;
            case Result<PageRenderedFragment>.Failure failure:
                MarkdownFragmentError = FormatError(failure.Error);
                break;
        }

        return Task.CompletedTask;
    }

    protected Task CloseCustomHtmlFragmentEditorAsync()
    {
        CustomHtmlFragmentEditorOpen = false;
        CustomHtmlFragmentInitialSource = string.Empty;
        CustomHtmlFragmentError = null;
        return Task.CompletedTask;
    }

    protected Task ApplyCustomHtmlFragmentSourceAsync(string source)
    {
        if (SelectedRenderedFragment is not { Kind: PageRenderedFragmentKind.CustomHtml } fragment)
        {
            CustomHtmlFragmentError = "The selected Custom HTML fragment no longer exists.";
            return Task.CompletedTask;
        }

        var imported = CreateHtmlFragmentImporter().Import(source);
        if (imported is Result<HtmlPageContent>.Failure importFailure)
        {
            CustomHtmlFragmentError = FormatError(importFailure.Error);
            return Task.CompletedTask;
        }

        var updated = HtmlEditor.UpdateRenderedFragmentSource(fragment.NodeId, source);
        switch (updated)
        {
            case Result<PageRenderedFragment>.Ok:
                CustomHtmlFragmentEditorOpen = false;
                CustomHtmlFragmentInitialSource = string.Empty;
                CustomHtmlFragmentError = null;
                MarkDirty();
                ShowToast("Custom HTML block updated.", "success");
                break;
            case Result<PageRenderedFragment>.Failure failure:
                CustomHtmlFragmentError = FormatError(failure.Error);
                break;
        }

        return Task.CompletedTask;
    }

    protected Task CloseScribanFragmentEditorAsync()
    {
        ScribanFragmentEditorOpen = false;
        ScribanFragmentInitialSource = string.Empty;
        ScribanFragmentError = null;
        return Task.CompletedTask;
    }

    protected async Task ApplyScribanFragmentSourceAsync(string source)
    {
        if (SelectedRenderedFragment is not { Kind: PageRenderedFragmentKind.Scriban } fragment)
        {
            ScribanFragmentError = "The selected Scriban fragment no longer exists.";
            return;
        }

        if (string.IsNullOrWhiteSpace(source)
            || source.Length > PageRenderedFragment.MaximumSourceLength)
        {
            ScribanFragmentError = string.IsNullOrWhiteSpace(source)
                ? "Scriban source cannot be empty."
                : $"Scriban source cannot exceed {PageRenderedFragment.MaximumSourceLength} characters.";
            return;
        }

        var fragments = (HtmlEditor.Composition.RenderedFragments ?? [])
            .Select(candidate => candidate.NodeId == fragment.NodeId
                ? candidate with { Source = source }
                : candidate)
            .ToArray();
        var candidateComposition = HtmlEditor.Composition with { RenderedFragments = fragments };
        var culture = LoadedPage?.Culture
            ?? CurrentSite?.DefaultCulture
            ?? CultureInfo.CurrentUICulture.Name;
        var preview = await PreviewClient.RenderPageFragmentAsync(
            HtmlEditor.Content,
            candidateComposition,
            culture);
        if (preview is Result<string, AeroError>.Failure previewFailure)
        {
            ScribanFragmentError = FormatError(previewFailure.Error);
            return;
        }

        var updated = HtmlEditor.UpdateRenderedFragmentSource(fragment.NodeId, source);
        switch (updated)
        {
            case Result<PageRenderedFragment>.Ok:
                ScribanFragmentEditorOpen = false;
                ScribanFragmentInitialSource = string.Empty;
                ScribanFragmentError = null;
                MarkDirty();
                ShowToast("Scriban block updated.", "success");
                break;
            case Result<PageRenderedFragment>.Failure failure:
                ScribanFragmentError = FormatError(failure.Error);
                break;
        }
    }

    private static string DefaultRenderedFragmentSource(PageRenderedFragmentKind kind) => kind switch
    {
        PageRenderedFragmentKind.Markdown => "## Markdown block\n\nStart writing here.",
        PageRenderedFragmentKind.CustomHtml => "<p>Custom HTML block</p>",
        PageRenderedFragmentKind.Scriban => "<p>Site {{ site.id }} · {{ page.culture }}</p>",
        _ => "Rendered block"
    };

    private static string RenderedFragmentDisplayName(PageRenderedFragmentKind kind) => kind switch
    {
        PageRenderedFragmentKind.CustomHtml => "Custom HTML",
        _ => kind.ToString()
    };

    protected Task ApplyHtmlRichTextAsync(IReadOnlyList<HtmlNode> children)
    {
        var result = HtmlEditor.UpdateSelectedChildren(children);
        switch (result)
        {
            case Result<HtmlNode>.Ok:
                HtmlRichTextEditorOpen = false;
                HtmlRichTextError = null;
                MarkDirty();
                ShowToast(L["Text updated."], "success");
                break;
            case Result<HtmlNode>.Failure failure:
                HtmlRichTextError = FormatError(failure.Error);
                ShowToast(HtmlRichTextError, "error");
                break;
        }

        return Task.CompletedTask;
    }

    protected Task ApplyHtmlCollectionActionAsync(HtmlCollectionActionKind action)
    {
        var result = HtmlEditor.ApplySelectedCollectionAction(action);
        var successMessage = action switch
        {
            HtmlCollectionActionKind.AddListItem => "List item added.",
            HtmlCollectionActionKind.AddTableRow => "Table row added.",
            HtmlCollectionActionKind.AddTableColumn => "Table column added.",
            HtmlCollectionActionKind.AddMediaSource => "Media source added.",
            HtmlCollectionActionKind.AddMediaTrack => "Caption track added.",
            HtmlCollectionActionKind.AddFormInput => "Text field added.",
            HtmlCollectionActionKind.AddFormTextArea => "Text area added.",
            HtmlCollectionActionKind.AddFormSelect => "Choice list added.",
            HtmlCollectionActionKind.AddSelectOption => "Choice added.",
            _ => "Structure updated."
        };
        HandleHtmlEditorResult(result, successMessage);
        return Task.CompletedTask;
    }

    protected Task OpenHtmlMediaSelectorAsync(HtmlMediaTargetKind target)
    {
        _htmlMediaTarget = target;
        HtmlMediaSelectorOpen = HtmlEditor.SelectedNode is not null;
        return Task.CompletedTask;
    }

    protected Task CloseHtmlMediaSelectorAsync()
    {
        HtmlMediaSelectorOpen = false;
        _htmlMediaTarget = null;
        return Task.CompletedTask;
    }

    protected Task ApplyHtmlMediaSelectionAsync(List<MediaItem> selectedItems)
    {
        var selectedMedia = selectedItems.FirstOrDefault();
        var selectedNode = HtmlEditor.SelectedNode;
        if (selectedMedia is null || selectedNode is null || _htmlMediaTarget is null)
        {
            return CloseHtmlMediaSelectorAsync();
        }

        var properties = HtmlMediaPropertyMapper.Map(
            selectedNode,
            _htmlMediaTarget.Value,
            selectedMedia.Src,
            selectedMedia.Alt);

        var result = HtmlEditor.UpdateSelectedProperties(properties);
        switch (result)
        {
            case Result<HtmlNode>.Ok:
                HtmlPropertyError = null;
                MarkDirty();
                ShowToast(L["Media selected."], "success");
                HtmlMediaSelectorOpen = false;
                _htmlMediaTarget = null;
                break;
            case Result<HtmlNode>.Failure failure:
                HtmlPropertyError = FormatError(failure.Error);
                ShowToast(HtmlPropertyError, "error");
                break;
        }

        return Task.CompletedTask;
    }

    private void HandleHtmlEditorResult<T>(Result<T> result, string? successMessage)
    {
        switch (result)
        {
            case Result<T>.Ok:
                MarkDirty();
                if (!string.IsNullOrWhiteSpace(successMessage))
                {
                    ShowToast(L[successMessage], "success");
                }
                break;
            case Result<T>.Failure failure:
                ShowToast(FormatError(failure.Error), "error");
                break;
        }
    }

    private async Task<bool> BindPageOwnerStyleProfileAsync(long siteId)
    {
        if (siteId <= 0)
        {
            return FailSiteStyleProfile("The page does not identify an owning site.");
        }

        if (CurrentSite?.Id != siteId)
        {
            var ownerSite = await LoadSiteByIdAsync(siteId);
            if (ownerSite is null)
            {
                return FailSiteStyleProfile(
                    $"The page's owning site ({siteId}) could not be loaded.");
            }

            CurrentSite = ownerSite;
            _siteStyleProfile = null;
            _styleProfileResolutionAttempted = false;
            _previewBaseUri = ResolvePreviewBaseUri(ownerSite) ?? NavManager.BaseUri;
        }

        return await EnsureSiteStyleProfileAsync();
    }

    private async Task<bool> EnsureSiteStyleProfileAsync()
    {
        if (_siteStyleProfile is not null)
        {
            return true;
        }

        if (CurrentSite is null)
        {
            CurrentSite = await ResolveCurrentSiteAsync();
        }

        if (CurrentSite is null)
        {
            return FailSiteStyleProfile("Select a site before editing page styles.");
        }

        if (_styleProfileResolutionAttempted)
        {
            return false;
        }

        _styleProfileResolutionAttempted = true;
        if (CurrentSite.StyleProfile is null)
        {
            return FailSiteStyleProfile(
                $"Site {CurrentSite.Id} does not have a style profile.");
        }

        var settings = new StyleProfileSettings
        {
            Revision = CurrentSite.StyleProfile.Revision,
            SmallScreenBreakpointRem = CurrentSite.StyleProfile.SmallScreenBreakpointRem,
            ColorTokens = (CurrentSite.StyleProfile.ColorTokens ?? [])
                .Select(static token => new StyleColorToken
                {
                    Name = token.Name,
                    HexValue = token.HexValue
                })
                .ToList()
        };

        var profileResult = NativeStyleProfileFactory.Create(CurrentSite.Id, settings);
        if (profileResult is Result<NativeStyleProfile, AeroError>.Failure failure)
        {
            return FailSiteStyleProfile(
                $"The selected site's style profile is invalid: {FormatError(failure.Error)}");
        }

        _siteStyleProfile = ((Result<NativeStyleProfile, AeroError>.Ok)profileResult).Value;
        HtmlEditor = CreateHtmlEditorSession(
            HtmlEditor.Content,
            _siteStyleProfile,
            HtmlEditor.Composition);
        HtmlPropertyError = null;
        return true;
    }

    private bool FailSiteStyleProfile(string message)
    {
        HtmlPropertyError = message;
        if (!Toasts.Any(toast => string.Equals(toast.Message, message, StringComparison.Ordinal)))
        {
            ShowToast(message, "error");
        }

        return false;
    }

    private static string FormatError(AeroError error) => error switch
    {
        AeroError.Validation validation => string.Join("; ", validation.Errors),
        AeroError.NotAllowed notAllowed => notAllowed.msg,
        AeroError.NotFound notFound => notFound.msg,
        AeroError.Conflict conflict => conflict.msg,
        AeroError.Timeout timeout => timeout.msg,
        AeroError.Error general => general.msg,
        _ => error.ToString()
    };

    private void QueuePreviewRefresh()
    {
        if (!PreviewMode)
        {
            return;
        }

        _previewRefreshVersion++;
        _previewDebounceCts?.Cancel();
        _previewDebounceCts?.Dispose();
        var cts = new CancellationTokenSource();
        _previewDebounceCts = cts;

        _ = InvokeAsync(async () =>
        {
            try
            {
                await Task.Delay(PreviewDebounceMilliseconds, cts.Token);
                await RefreshPreviewAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when another edit supersedes the pending preview render.
            }
        });
    }

    private async Task RefreshPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (!PreviewMode)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(PreviewFrameUrl))
        {
            PreviewHtml = null;
            PreviewError = null;
            IsPreviewRendering = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        IsPreviewRendering = true;
        PreviewError = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var culture = LoadedPage?.Culture
                ?? CurrentSite?.DefaultCulture
                ?? CultureInfo.CurrentUICulture.Name;
            var result = await PreviewClient.RenderPageFragmentAsync(
                HtmlEditor.Content,
                HtmlEditor.Composition,
                culture,
                cancellationToken);
            switch (result)
            {
                case Result<string, AeroError>.Ok ok:
                    PreviewHtml = ok.Value;
                    break;
                case Result<string, AeroError>.Failure failure:
                    PreviewHtml = null;
                    PreviewError = FormatError(failure.Error);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            PreviewHtml = null;
            PreviewError = L["Preview render failed: {0}", ex.Message];
        }
        finally
        {
            IsPreviewRendering = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ResolvePreviewBaseUriAsync()
    {
        SiteViewModel? selectedSite = null;

        if (AdminState.CurrentSiteId is { } selectedSiteId)
        {
            selectedSite = await LoadSiteByIdAsync(selectedSiteId);
        }

        selectedSite ??= await CurrentSiteAccessor.GetCurrentSiteAsync();

        if (selectedSite is null)
        {
            var defaultResult = await SitesClient.GetDefaultAsync();
            if (defaultResult is Result<SiteViewModel, AeroError>.Ok defaultOk)
            {
                selectedSite = defaultOk.Value;
            }
        }

        _previewBaseUri = ResolvePreviewBaseUri(selectedSite) ?? NavManager.BaseUri;
    }

    private async Task<SiteViewModel?> ResolveCurrentSiteAsync()
    {
        if (AdminState.CurrentSiteId is { } selectedSiteId)
            return await LoadSiteByIdAsync(selectedSiteId);

        var selectedSite = await CurrentSiteAccessor.GetCurrentSiteAsync();
        if (selectedSite is not null)
            return selectedSite;

        var defaultResult = await SitesClient.GetDefaultAsync();
        return defaultResult is Result<SiteViewModel, AeroError>.Ok ok ? ok.Value : null;
    }

    private string? ResolvePreviewBaseUri(SiteViewModel? site)
    {
        var baseUri = BuildSiteBaseUri(site);
        if (baseUri is null) return null;

        // The site's PrimaryHost might not include the port we're running on.
        var previewUri = new Uri(baseUri);
        var currentUri = new Uri(NavManager.BaseUri);

        if (previewUri.Port != currentUri.Port)
        {
            var builder = new UriBuilder(previewUri) { Port = currentUri.Port };
            return EnsureTrailingSlash(builder.Uri.ToString());
        }

        return baseUri;
    }

    private async Task<SiteViewModel?> LoadSiteByIdAsync(long siteId)
    {
        var result = await SitesClient.GetByIdAsync(siteId);
        return result is Result<SiteViewModel, AeroError>.Ok ok ? ok.Value : null;
    }

    private string BuildAbsoluteUrl(string relativeUrl, string? baseUri = null)
    {
        return new Uri(new Uri(baseUri ?? NavManager.BaseUri), relativeUrl.TrimStart('/')).ToString();
    }

    private string? BuildSiteBaseUri(SiteViewModel? site)
    {
        var host = site?.PrimaryHost;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = site?.Hosts.FirstOrDefault(static h => !string.IsNullOrWhiteSpace(h));
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        host = host.Trim().TrimEnd('/');

        if (Uri.TryCreate(host, UriKind.Absolute, out var absoluteUri))
        {
            return EnsureTrailingSlash(absoluteUri.ToString());
        }

        var current = new Uri(NavManager.BaseUri);
        var authority = host;
        if (!host.Contains(':', StringComparison.Ordinal))
        {
            authority = current.IsDefaultPort ? host : $"{host}:{current.Port}";
        }

        return EnsureTrailingSlash($"{current.Scheme}://{authority}");
    }

    private static string EnsureTrailingSlash(string uri)
    {
        return uri.EndsWith("/", StringComparison.Ordinal) ? uri : $"{uri}/";
    }

    private async Task LoadPageTranslationsAsync()
    {
        if (Id is null)
        {
            PageCultureVariants = [];
            ResetTranslationDraft();
            return;
        }

        IsLoadingTranslations = true;

        try
        {
            var result = await PagesClient.ListCultureVariantsAsync(Id.Value);
            PageCultureVariants = result is Result<IReadOnlyList<CmsPageDetail>, AeroError>.Ok ok
                ? ok.Value.OrderBy(page => page.Culture, StringComparer.OrdinalIgnoreCase).ToList()
                : [];

            ResetTranslationDraft();
        }
        catch
        {
            PageCultureVariants = [];
        }
        finally
        {
            IsLoadingTranslations = false;
        }
    }

        /// <summary>
    /// CreateTranslationAsync method.
    /// </summary>
protected async Task CreateTranslationAsync()
    {
        if (Id is null || IsCreatingTranslation)
            return;

        if (string.IsNullOrWhiteSpace(SelectedTranslationCulture))
        {
            ShowToast(L["Choose a target culture"], "error");
            return;
        }

        var slug = string.IsNullOrWhiteSpace(TranslationSlug)
            ? PageSlug.Trim()
            : TranslationSlug.Trim();

        if (string.IsNullOrWhiteSpace(slug))
        {
            ShowToast(L["Enter a translated slug"], "error");
            return;
        }

        if (_pageState == PageState.Dirty)
        {
            await SavePage();

            if (_pageState != PageState.Clean)
                return;
        }

        IsCreatingTranslation = true;

        try
        {
            var request = new ForkPageCultureRequest(SelectedTranslationCulture, slug);
            var result = await PagesClient.ForkToCultureAsync(Id.Value, request);

            if (result is Result<CmsPageDetail, AeroError>.Ok ok)
            {
                ShowToast(L["Created {0} translation", FormatCulture(ok.Value.Culture)], "success");
                NavManager.NavigateTo($"/manager/page/editor/{ok.Value.Id}");
                return;
            }

            if (result is Result<CmsPageDetail, AeroError>.Failure failure)
                ShowToast(L["Translation failed: {0}", failure.Error], "error");
        }
        catch (Exception ex)
        {
            ShowToast(L["Translation failed: {0}", ex.Message], "error");
        }
        finally
        {
            IsCreatingTranslation = false;
            await InvokeAsync(StateHasChanged);
        }
    }

        /// <summary>
    /// TranslateAllCulturesAsync method.
    /// </summary>
protected async Task TranslateAllCulturesAsync()
    {
        if (LoadedPage is null || Id is null || IsTranslatingAll)
            return;

        var existingCultures = PageCultureVariants
            .Select(x => NormalizeCultureName(x.Culture))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var targets = SupportedCultures
            .Select(NormalizeCultureName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(culture => !string.Equals(culture, LoadedPage.Culture, StringComparison.OrdinalIgnoreCase))
            .Where(culture => OverwriteExistingTranslations || !existingCultures.Contains(culture))
            .Select(culture =>
            {
                var existing = PageCultureVariants.FirstOrDefault(x => string.Equals(x.Culture, culture, StringComparison.OrdinalIgnoreCase));
                return new AiTranslatePageCultureRequest(
                    culture,
                    existing?.Slug ?? BuildDefaultTranslationSlug(PageSlug, culture));
            })
            .ToList();

        if (targets.Count == 0)
        {
            ShowToast(
                OverwriteExistingTranslations
                    ? L["There are no other site cultures to translate."]
                    : L["All enabled cultures already have translations. Enable overwrite to refresh existing translations."],
                "info");
            return;
        }

        var confirmed = await DialogService.Confirm(
            OverwriteExistingTranslations
                ? L["Translate all enabled cultures and overwrite existing localized page content? Existing variants will become drafts."]
                : L["Translate all missing enabled cultures for this page? New localized variants will be created as drafts."],
            L["AI Translate All"],
            new ConfirmOptions
            {
                OkButtonText = L["Translate"],
                CancelButtonText = L["Cancel"]
            });

        if (confirmed != true)
            return;

        await TranslateCulturesAsync(targets, OverwriteExistingTranslations, translateAll: true);
    }

        /// <summary>
    /// TranslateCultureAsync method.
    /// </summary>
protected Task TranslateCultureAsync(CmsPageDetail variant)
    {
        if (LoadedPage is null || Id is null)
            return Task.CompletedTask;

        if (string.Equals(variant.Culture, LoadedPage.Culture, StringComparison.OrdinalIgnoreCase))
        {
            ShowToast(L["Open another culture variant and translate from that source if needed."], "info");
            return Task.CompletedTask;
        }

        return TranslateCulturesAsync(
            [new AiTranslatePageCultureRequest(variant.Culture, variant.Slug)],
            overwriteExisting: true,
            translateAll: false);
    }

    private async Task TranslateCulturesAsync(
        IReadOnlyList<AiTranslatePageCultureRequest> targets,
        bool overwriteExisting,
        bool translateAll)
    {
        if (Id is null || targets.Count == 0)
            return;

        if (_pageState == PageState.Dirty)
        {
            await SavePage();

            if (_pageState != PageState.Clean)
                return;
        }

        if (translateAll)
        {
            IsTranslatingAll = true;
        }

        foreach (var target in targets)
        {
            TranslatingCultures.Add(target.Culture);
        }

        try
        {
            var request = new AiTranslatePageRequest(targets, ProviderId: null, overwriteExisting);
            var result = await PagesClient.TranslateWithAiAsync(Id.Value, request);

            if (result is Result<AiTranslatePageResult, AeroError>.Ok ok)
            {
                var succeeded = ok.Value.Results.Count(x => x.Succeeded);
                var failed = ok.Value.Results.Count - succeeded;

                if (succeeded > 0)
                {
                    ShowToast(
                        failed == 0
                            ? L["Translated {0} culture(s)", succeeded]
                            : L["Translated {0} culture(s); {1} failed", succeeded, failed],
                        failed == 0 ? "success" : "info");

                    await LoadPageTranslationsAsync();
                }

                foreach (var failure in ok.Value.Results.Where(x => !x.Succeeded))
                {
                    ShowToast(L["{0}: {1}", FormatCulture(failure.Culture), failure.Error ?? L["AI translation failed"]], "error");
                }

                return;
            }

            if (result is Result<AiTranslatePageResult, AeroError>.Failure apiFailure)
            {
                ShowToast(L["AI translation failed: {0}", apiFailure.Error], "error");
            }
        }
        catch (Exception ex)
        {
            ShowToast(L["AI translation failed: {0}", ex.Message], "error");
        }
        finally
        {
            if (translateAll)
            {
                IsTranslatingAll = false;
            }

            foreach (var target in targets)
            {
                TranslatingCultures.Remove(target.Culture);
            }

            await InvokeAsync(StateHasChanged);
        }
    }

        /// <summary>
    /// OpenTranslation method.
    /// </summary>
protected void OpenTranslation(long pageId)
        => NavManager.NavigateTo($"/manager/page/editor/{pageId}?tab=translations");

        /// <summary>
    /// OpenPublicTranslation method.
    /// </summary>
protected void OpenPublicTranslation(CmsPageDetail variant)
    {
        var baseUri = _previewBaseUri ?? NavManager.BaseUri.TrimEnd('/');
        NavManager.NavigateTo($"{baseUri.TrimEnd('/')}{variant.Path}");
    }

        /// <summary>
    /// DeleteTranslationAsync method.
    /// </summary>
protected async Task DeleteTranslationAsync(CmsPageDetail variant)
    {
        if (LoadedPage is null)
            return;

        var isDefault = string.Equals(CurrentSite?.DefaultCulture, variant.Culture, StringComparison.OrdinalIgnoreCase);
        if (isDefault)
        {
            ShowToast(L["Delete the default culture page from the Pages list so the full translation group warning is shown."], "error");
            return;
        }

        var confirmed = await DialogService.Confirm(
            L["Delete the {0} translation for '{1}'?", FormatCulture(variant.Culture), variant.Title],
            L["Delete Translation"],
            new ConfirmOptions { OkButtonText = L["Delete Translation"], CancelButtonText = L["Cancel"] });

        if (confirmed != true)
            return;

        var result = await PagesClient.DeleteAsync(variant.Id);
        if (result is Result<bool, AeroError>.Ok)
        {
            ShowToast(L["Deleted {0} translation", FormatCulture(variant.Culture)], "success");
            await LoadPageTranslationsAsync();
            return;
        }

        if (result is Result<bool, AeroError>.Failure failure)
            ShowToast(L["Delete failed: {0}", failure.Error], "error");
    }

        /// <summary>
    /// PublishAllTranslationsAsync method.
    /// </summary>
protected Task PublishAllTranslationsAsync()
        => SetAllTranslationsPublicationStateAsync(publish: true);

        /// <summary>
    /// UnpublishAllTranslationsAsync method.
    /// </summary>
protected Task UnpublishAllTranslationsAsync()
        => SetAllTranslationsPublicationStateAsync(publish: false);

    private async Task SetAllTranslationsPublicationStateAsync(bool publish)
    {
        if (LoadedPage is null || IsBulkPublishingTranslations)
            return;

        var translationGroupId = LoadedPage.TranslationGroupId ?? LoadedPage.Id;
        var action = publish ? L["publish"] : L["unpublish"];
        var confirmed = await DialogService.Confirm(
            L["This will {0} all existing localized versions for '{1}'. Continue?", action, LoadedPage.Title],
            publish ? L["Publish All Translations"] : L["Unpublish All Translations"],
            new ConfirmOptions
            {
                OkButtonText = publish ? L["Publish All"] : L["Unpublish All"],
                CancelButtonText = L["Cancel"]
            });

        if (confirmed != true)
            return;

        IsBulkPublishingTranslations = true;
        try
        {
            var result = publish
                ? await PagesClient.PublishTranslationGroupAsync(translationGroupId)
                : await PagesClient.UnpublishTranslationGroupAsync(translationGroupId);

            if (result is Result<PublicationBulkResult, AeroError>.Ok ok)
            {
                var current = ok.Value.Items.FirstOrDefault(x => x.Id == LoadedPage.Id);
                if (current is not null)
                {
                    PublicationState = current.Published
                        ? ContentPublicationState.Published
                        : ContentPublicationState.Draft;
                }

                ShowToast(
                    publish
                        ? L["Published {0} translation(s)", ok.Value.Updated]
                        : L["Unpublished {0} translation(s)", ok.Value.Updated],
                    "success");

                await LoadPageTranslationsAsync();
                return;
            }

            if (result is Result<PublicationBulkResult, AeroError>.Failure failure)
            {
                ShowToast(L["{0} all failed: {1}", publish ? L["Publish"] : L["Unpublish"], failure.Error], "error");
            }
        }
        finally
        {
            IsBulkPublishingTranslations = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void ResetTranslationDraft()
    {
        SelectedTranslationCulture = AvailableTranslationCultures.FirstOrDefault() ?? string.Empty;
        TranslationSlug = string.Empty;
    }

        /// <summary>
    /// FormatCulture method.
    /// </summary>
protected string FormatCulture(string? culture)
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
            return "en-US";

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return culture.Trim();
        }
    }

    private static string BuildDefaultTranslationSlug(string slug, string culture)
    {
        var normalized = TitleToSlug(slug.Trim().Trim('/'));
        return string.IsNullOrWhiteSpace(normalized)
            ? culture.ToLowerInvariant()
            : $"{normalized}-{culture.ToLowerInvariant()}";
    }

    private static string BuildPreviewFrameDocument(string? html, string baseUri, IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L)
    {
        var content = string.IsNullOrWhiteSpace(html)
            ? $"<main class=\"pe-empty-state\"><h3>{L["No preview content"]}</h3></main>"
            : html;
        var appCss = new Uri(new Uri(baseUri), "_content/Aero.Cms.Shared/app.css");
        var managerCss = new Uri(new Uri(baseUri), "_content/Aero.Cms.Shared/aero-manager.css");
        var radzenCss = new Uri(new Uri(baseUri), "_content/Radzen.Blazor/css/standard-base.css");
        var pagesCss = new Uri(new Uri(baseUri), "_content/Aero.Cms.Modules.Pages/css/pages.css");

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <base href="{{baseUri}}">
                <link rel="stylesheet" href="{{appCss}}">
                <link rel="stylesheet" href="{{managerCss}}">
                <link rel="stylesheet" href="{{radzenCss}}">
                <link rel="stylesheet" href="{{pagesCss}}">
                <style>
                    html, body { margin: 0; min-height: 100%; background: #fff; }
                    body { font-family: Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
                    .aero-preview-document { min-height: 100vh; overflow-x: hidden; }
                </style>
            </head>
            <body>
                <main class="aero-preview-document aero-page-content">
                    {{content}}
                </main>
            </body>
            </html>
            """;
    }

    // ──────────────────────────────────────────────────────────
    // Save / Publish  (mirrors savePage / publishPage)
    // ──────────────────────────────────────────────────────────

    private void MarkDirty() => _pageState = PageState.Dirty;

    private async Task AutoSaveAsync()
    {
        if (_pageState != PageState.Dirty || _routeDecisionPending) return;

        if (Id == 0 || Id is null)
        {
            // New page: only auto-create if there's actual content
            if (HtmlEditor.Content.Root.Children.Count == 0 && string.IsNullOrWhiteSpace(PageTitle))
                return;

            await SavePageCore(showSuccessToast: false);
            return;
        }

        await SavePageCore(showSuccessToast: false);
    }

    private Task SavePage() => SavePageCore(showSuccessToast: true);

    private async Task SavePageCore(bool showSuccessToast)
    {
        if (IsSaving) return;

        if (!await EnsureSiteStyleProfileAsync())
        {
            return;
        }

        if (Id is { } persistedId && (!_isPersistedPageLoaded || _loadedPageId != persistedId))
        {
            await LoadPageAsync(persistedId);
            if (!_isPersistedPageLoaded || _loadedPageId != persistedId)
            {
                ShowToast(L["The page is still loading. Please try again."], "info");
                return;
            }
        }

        // Normalize at the editor boundary so preview, preflight, and persistence
        // all operate on the same URL segment.
        PageSlug = TitleToSlug(
            string.IsNullOrWhiteSpace(PageSlug) ? PageTitle : PageSlug.Trim().Trim('/'));

        var routeDecision = await ResolvePreviousPathBehaviorAsync(showSuccessToast);
        if (!routeDecision.Continue)
            return;

        IsSaving = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            if (Id.HasValue)
            {
                var request = new UpdatePageRequest(
                    PageTitle,
                    PageSlug,
                    Summary,
                    SeoTitle,
                    SeoDescription,
                    PublicationState,
                    ParentId,
                    ShowInNavMenu,
                    ShowHeaderNavigation,
                    HideFooter,
                    ShowChatAgent,
                    DraftContent: HtmlEditor.Content,
                    PreviousPathBehavior: routeDecision.Behavior,
                    DraftComposition: DraftComposition
                );

                var result = await PagesClient.UpdateAsync(Id.Value, request);
                if (result is Result<CmsPageDetail, AeroError>.Ok ok)
                {
                    LoadedPage = ok.Value;
                    UpdateLastSaved();
                    _pageState = PageState.Clean;
                    await LoadPageTranslationsAsync();
                    if (showSuccessToast)
                    {
                        ShowToast(L["Page saved successfully"], "success");
                    }
                }
                else if (result is Result<CmsPageDetail, AeroError>.Failure err)
                {
                    ShowToast(L["Error saving: {0}", err.Error], "error");
                }
            }
            else
            {
                var request = new CreatePageRequest(
                    PageTitle,
                    PageSlug,
                    Summary,
                    SeoTitle,
                    SeoDescription,
                    PublicationState,
                    ParentId,
                    ShowInNavMenu,
                    ShowHeaderNavigation,
                    HideFooter,
                    ShowChatAgent,
                    DraftContent: HtmlEditor.Content,
                    DraftComposition: DraftComposition
                );

                var result = await PagesClient.CreateAsync(request);
                if (result is Result<CmsPageDetail, AeroError>.Ok createOk)
                {
                    Id = createOk.Value.Id;
                    LoadedPage = createOk.Value;
                    _loadedPageId = Id;
                    _isPersistedPageLoaded = true;
                    // Keep following the title unless the author explicitly
                    // edited the slug.
                    _pageState = PageState.Clean;
                    UpdateLastSaved();
                    await LoadPageTranslationsAsync();
                    if (showSuccessToast)
                    {
                        ShowToast(L["Page created successfully"], "success");
                    }

                    // Replace the transient create URL with the persisted resource URL.
                    // This keeps refresh/back behavior correct without reloading the WASM editor.
                    NavManager.NavigateTo(
                        $"/manager/page/editor/{Id}",
                        new NavigationOptions { ReplaceHistoryEntry = true });
                }
                else if (result is Result<CmsPageDetail, AeroError>.Failure err)
                {
                    ShowToast(L["Error creating: {0}", err.Error], "error");
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast(L["Save failed: {0}", ex.Message], "error");
        }
        finally
        {
            IsSaving = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task<(bool Continue, PreviousPathBehavior? Behavior)> ResolvePreviousPathBehaviorAsync(
        bool isManualSave)
    {
        if (Id is not { } id
            || LoadedPage is null
            || (string.Equals(LoadedPage.Slug, PageSlug, StringComparison.Ordinal)
                && LoadedPage.ParentId == ParentId))
        {
            _routeDecisionPending = false;
            return (true, null);
        }

        var impactResult = await PagesClient.GetRouteChangeImpactAsync(
            id,
            new PageRouteChangeRequest(PageSlug, ParentId));
        if (impactResult is Result<PageRouteChangeImpact, AeroError>.Failure failure)
        {
            ShowToast(L["Unable to check the URL change: {0}", failure.Error], "error");
            return (false, null);
        }

        var impact = ((Result<PageRouteChangeImpact, AeroError>.Ok)impactResult).Value;
        if (!impact.RequiresDecision)
        {
            _routeDecisionPending = false;
            return (true, null);
        }

        _routeDecisionPending = true;
        if (!isManualSave)
            return (false, null);

        var affectedCount = impact.PreviouslyPublishedRoutes.Count;
        var keepRedirects = await DialogService.Confirm(
            L[
                affectedCount == 1
                    ? "This page has already been published at '{0}'. Keep that URL working by creating a permanent redirect to '{1}'?"
                    : "This change affects {2} previously published URLs, beginning with '{0}'. Keep them working by creating permanent redirects to their new URLs?",
                impact.OldPath,
                impact.NewPath,
                affectedCount],
            L["Preserve Published URLs"],
            new ConfirmOptions
            {
                OkButtonText = L["Keep Old URLs"],
                CancelButtonText = L["Don't Keep"]
            });

        if (keepRedirects == true)
        {
            _routeDecisionPending = false;
            return (true, PreviousPathBehavior.CreatePermanentRedirect);
        }

        var discardConfirmed = await DialogService.Confirm(
            L[
                affectedCount == 1
                    ? "Continue without a redirect? Existing links to '{0}' will stop working."
                    : "Continue without redirects? Existing links to {1} published URLs will stop working.",
                impact.OldPath,
                affectedCount],
            L["Discard Published URLs"],
            new ConfirmOptions
            {
                OkButtonText = L["Continue Without Redirects"],
                CancelButtonText = L["Cancel Save"]
            });

        if (discardConfirmed != true)
            return (false, null);

        _routeDecisionPending = false;
        return (true, PreviousPathBehavior.Discard);
    }

        /// <summary>
    /// PublishPage method.
    /// </summary>
protected async Task PublishPage()
    {
        if (Id is { } persistedId && (!_isPersistedPageLoaded || _loadedPageId != persistedId))
        {
            await LoadPageAsync(persistedId);
            if (!_isPersistedPageLoaded || _loadedPageId != persistedId)
            {
                ShowToast(L["The page is still loading. Please try again."], "info");
                return;
            }
        }

        if (!Id.HasValue || _pageState == PageState.Dirty)
        {
            await SavePage();

            if (_pageState != PageState.Clean)
                return;
        }

        if (Id.HasValue)
        {
            var result = await PagesClient.PublishAsync(Id.Value);
            if (result is Result<CmsPageDetail, AeroError>.Ok ok)
            {
                PublicationState = ok.Value.PublicationState;
                _pageState = PageState.Clean;
                ShowToast(L["Page published!"], "success");
            }
            else
            {
                ShowToast(L["Failed to publish"], "error");
            }
        }
    }

        /// <summary>
    /// UnpublishPage method.
    /// </summary>
protected async Task UnpublishPage()
    {
        if (Id.HasValue)
        {
            var result = await PagesClient.UnpublishAsync(Id.Value);
            if (result is Result<CmsPageDetail, AeroError>.Ok ok)
            {
                PublicationState = ok.Value.PublicationState;
                _pageState = PageState.Clean;
                ShowToast(L["Page unpublished"], "success");
            }
            else
            {
                ShowToast(L["Failed to unpublish"], "error");
            }
        }
    }

        /// <summary>
    /// UpdateLastSaved method.
    /// </summary>
protected void UpdateLastSaved()
        => LastSaved = DateTime.Now.ToString("HH:mm");

    // ──────────────────────────────────────────────────────────
    // Slug auto-population  (mirrors common CMS behavior)
    // ──────────────────────────────────────────────────────────

    /// <summary>Called when the title changes in either the editor or metadata tab.</summary>
    protected void OnTitleChanged(string title)
    {
        PageTitle = title;
        MarkDirty();
        _routeDecisionPending = false;
        if (_slugState == SlugState.Auto)
            PageSlug = TitleToSlug(title);
    }

    /// <summary>Called when the user manually edits the slug. Locks it to prevent title overwrites.</summary>
    protected void OnSlugChanged(string slug)
    {
        PageSlug = slug;
        MarkDirty();
        _routeDecisionPending = false;
        _slugState = SlugState.Locked;
    }

    /// <summary>Returns the slug to automatic title synchronization.</summary>
    protected void UseTitleForSlug()
    {
        _slugState = SlugState.Auto;
        PageSlug = TitleToSlug(PageTitle);
        _routeDecisionPending = false;
        MarkDirty();
    }

    /// <summary>Normalizes a manually entered slug after editing.</summary>
    protected void NormalizePageSlug()
    {
        PageSlug = TitleToSlug(PageSlug.Trim().Trim('/'));
        _routeDecisionPending = false;
        MarkDirty();
    }

    /// <summary>Marks a parent change as a route-affecting edit.</summary>
    protected void MarkRouteDirty()
    {
        _routeDecisionPending = false;
        MarkDirty();
    }

    /// <summary>Converts a human-readable title to a URL-friendly slug.</summary>
    private static string TitleToSlug(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        // Decompose diacritics: "café" → "cafe" + combining accent
        var normalized = title.Normalize(NormalizationForm.FormD);
        var filtered = normalized.Where(c =>
            char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-');

        var slug = new string(filtered.ToArray())
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");  // strip remaining marks
        slug = Regex.Replace(slug, @"\s+", "-");           // spaces → hyphens
        slug = Regex.Replace(slug, @"-+", "-");            // collapse dashes
        return slug.Trim('-');
    }

    // ──────────────────────────────────────────────────────────
    // Toast  (mirrors showToast / removeToast)
    // ──────────────────────────────────────────────────────────

        /// <summary>
    /// ShowToast method.
    /// </summary>
protected void ShowToast(string message, string type = "info")
    {
        var toast = new ToastMessage { Message = message, Type = type };
        Toasts.Add(toast);

        // Auto-remove after 4 s
        _ = Task.Delay(4000).ContinueWith(_ => InvokeAsync(() =>
        {
            RemoveToast(toast.Id);
            StateHasChanged();
        }));
    }

        /// <summary>
    /// RemoveToast method.
    /// </summary>
protected void RemoveToast(string id)
        => Toasts.RemoveAll(t => t.Id == id);

    private string TabBtnClass(string tab) =>
        ActiveTab == tab ? "pe-tab-btn active" : "pe-tab-btn";

    private static bool IsKnownTab(string? tab)
        => string.Equals(tab, "editor", StringComparison.OrdinalIgnoreCase)
           || string.Equals(tab, "metadata", StringComparison.OrdinalIgnoreCase)
           || string.Equals(tab, "translations", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeTab(string? tab)
        => string.Equals(tab, "metadata", StringComparison.OrdinalIgnoreCase)
            ? "metadata"
            : string.Equals(tab, "translations", StringComparison.OrdinalIgnoreCase)
                ? "translations"
                : "editor";
}
