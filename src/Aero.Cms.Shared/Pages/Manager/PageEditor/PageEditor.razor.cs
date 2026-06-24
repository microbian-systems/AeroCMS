using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using System.Text.Json;
using System.Globalization;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Aero.Core;
using Aero.Core.Security;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;

using Aero.Core.Railway;
using CmsPageDetail = Aero.Cms.Abstractions.Http.Clients.PageDetail;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Shared.Services;
using Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;
using Aero.Cms.Shared.Pages.Manager.PageTree;
using Radzen;
using NeoUI.Blazor.Primitives;
using Aero.Cms.Shared.Pages.Manager.PageEditor.Canvas;
using PageEditorCatalog = Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

public partial class PageEditor : ComponentBase, IAsyncDisposable, IBlockEditorCallbacks
{
    // ──────────────────────────────────────────────────────────
    // Parameters
    // ──────────────────────────────────────────────────────────

    /// <summary>Optional ID of an existing page to edit.</summary>
    [Parameter] public long? Id { get; set; }

    [Inject] protected IDocsHttpClient DocsClient { get; set; } = default!;
    [Inject] protected IPagesHttpClient PagesClient { get; set; } = default!;
    [Inject] protected IPageCustomComponentsHttpClient CustomComponentsClient { get; set; } = default!;
    [Inject] protected IMediaHttpClient MediaClient { get; set; } = default!;
    [Inject] protected IBlogHttpClient BlogClient { get; set; } = default!;
    [Inject] protected ICategoriesHttpClient CategoriesClient { get; set; } = default!;
    [Inject] protected ITagsHttpClient TagsClient { get; set; } = default!;
    [Inject] protected IUsersHttpClient UsersClient { get; set; } = default!;
    [Inject] protected IPreviewHttpClient PreviewClient { get; set; } = default!;
    [Inject] protected ISitesHttpClient SitesClient { get; set; } = default!;
    [Inject] protected ICurrentSiteAccessor CurrentSiteAccessor { get; set; } = default!;
    [Inject] protected AdminStateContainer AdminState { get; set; } = default!;
    [Inject] protected Aero.Cms.Contracts.Abstractions.IAdminStorage AdminStorage { get; set; } = default!;
    [Inject] protected NavigationManager NavManager { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected IHtmlSanitizer HtmlSanitizer { get; set; } = default!;
    [Inject] protected Catalog.INeoEditorCatalogProvider Catalog { get; set; } = default!;
    [Inject] protected IPageEditorDefinitionRegistry DefinitionRegistry { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    // ──────────────────────────────────────────────────────────
    // State  (mirrors Alpine.js cmsEditor() properties)
    // ──────────────────────────────────────────────────────────

    protected string PageTitle    { get; set; } = "Homepage";
    protected string LastSaved    { get; set; } = "Never";
    protected string Author       { get; set; } = "Admin";

    // NeoPageNode composition tree root
    protected NeoPageNode RootNode { get; set; } = CreateDefaultRootNode();
    private static NeoPageNode CreateDefaultRootNode() => new()
    {
        NodeId = "page-root",
        CatalogId = "page.root",
        Kind = NeoPageNodeKind.Page,
        Children = []
    };

    // Selection / drag state
    protected string? SelectedBlockId  { get; set; }
    protected string? DraggedType      { get; set; }
    protected int?    DraggedIndex     { get; set; }
    protected EditorPaletteDragState PaletteDragState { get; } = new();

    // UI state
    protected bool   SidebarCollapsed { get; set; }
    protected bool   PreviewMode      { get; set; }
    protected string PreviewDevice    { get; set; } = "desktop";
    protected bool   IsPreviewRendering { get; set; }
    protected string? PreviewHtml { get; set; }
    protected string? PreviewError { get; set; }
    protected string PreviewFragmentUrl => BuildAbsoluteUrl("api/v1/admin/preview/pages/render-fragment");
    protected string? PreviewFrameUrl => Id is { } id
        ? BuildAbsoluteUrl($"_cms/preview/pages/drafts/{id}?previewVersion={_previewRefreshVersion}", _previewBaseUri)
        : null;
    protected string PreviewFrameDocument => BuildPreviewFrameDocument(PreviewHtml, NavManager.BaseUri, L);
    protected bool   RightSidebarCollapsed { get; set; } = true;
    protected bool   IsSaving              { get; set; }
    protected string ActiveTab             { get; set; } = "editor";

    // Sidebar category toggles
    protected bool CategoryAeroUi      { get; set; } = true;
    protected bool CategoryComponents  { get; set; } = true;
    protected bool CategoryReferences  { get; set; }
    protected bool CategorySettings    { get; set; } = true;
    protected bool CategoryHyper       { get; set; } = true;
    protected bool CategoryNeo         { get; set; } = true;
    protected bool CategoryPrimitives  { get; set; } = true;
    protected bool CategoryCustom      { get; set; } = true;
    protected string PaletteSearch { get; set; } = string.Empty;
    protected string? CompositionDropError { get; set; }
    private const string PaletteStateStorageKey = "aero.page-editor.palette-state.v1";

    // Page Settings
    protected string PageSlug { get; set; } = string.Empty;
    protected string Summary { get; set; } = string.Empty;

    /// <summary>Tracks whether the slug should auto-populate from the title.</summary>
    private enum SlugState { Auto, Loaded, Locked }
    private SlugState _slugState = SlugState.Auto;

    // Redundant ID removed to avoid ambiguity with ManagerComponent Base.Id
    // public string Id { get; set; } = string.Empty; 

    private string SeoTitle { get; set; } = string.Empty;
    protected string SeoDescription { get; set; } = string.Empty;
    protected bool   ShowInNavMenu { get; set; } = true;
    protected bool   ShowHeaderNavigation { get; set; } = true;
    protected bool   HideFooter { get; set; }
    protected bool   ShowChatAgent { get; set; } = true;
    protected ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    /// <summary>Optional parent page ID to pre-select when creating a new child page.</summary>
    [SupplyParameterFromQuery(Name = "parentId")]
    protected long? ParentId { get; set; }

    [SupplyParameterFromQuery(Name = "tab")]
    protected string? RequestedTab { get; set; }

    /// <summary>Read-only parent path prefix shown as a pill before the slug input.</summary>
    protected string ParentSlugPrefix { get; set; } = "";

    protected CmsPageDetail? LoadedPage { get; set; }
    protected SiteViewModel? CurrentSite { get; set; }
    protected IReadOnlyList<CmsPageDetail> PageCultureVariants { get; set; } = [];
    protected string SelectedTranslationCulture { get; set; } = string.Empty;
    protected string TranslationSlug { get; set; } = string.Empty;
    protected bool IsLoadingTranslations { get; set; }
    protected bool IsCreatingTranslation { get; set; }
    protected bool IsBulkPublishingTranslations { get; set; }
    protected bool IsTranslatingAll { get; set; }
    protected bool OverwriteExistingTranslations { get; set; }
    protected HashSet<string> TranslatingCultures { get; } = new(StringComparer.OrdinalIgnoreCase);
    protected IReadOnlyList<string> SupportedCultures =>
        CurrentSite?.SupportedCultures is { Count: > 0 } cultures
            ? cultures
            : [LoadedPage?.Culture ?? CurrentSite?.DefaultCulture ?? "en-US"];

    protected IEnumerable<string> AvailableTranslationCultures =>
        SupportedCultures
            .Select(NormalizeCultureName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(culture => !PageCultureVariants.Any(variant =>
                string.Equals(variant.Culture, culture, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    protected IReadOnlyList<DocsSummary>? DocsCategories { get; set; }

    // Media modal
    protected bool         MediaModalOpen   { get; set; }
    protected EditorBlock? CurrentMediaBlock { get; set; }
    protected bool         IsGalleryMode    { get; set; }
    protected string?      MediaContext     { get; set; }   // "background" | "nested"
    protected NestedBlock? NestedMediaTarget { get; set; }
    private string? NativeMediaNodeId { get; set; }
    private string? NativeMediaField { get; set; }
    private EditorBreakpoint NativeMediaBreakpoint { get; set; }

    // Block edit modal
    protected bool BlockEditorModalOpen { get; set; }
    protected string? EditingBlockId { get; set; }
    protected string? EditingNodeId { get; set; }
    protected string BlockEditorTab { get; set; } = "design";
    protected NeoPageNode? CurrentEditNode =>
        string.IsNullOrEmpty(EditingBlockId)
            ? null
            : FindNodeInTree(EditingBlockId);

    protected IReadOnlyList<PageCustomComponentDetail> CustomComponents { get; set; } = [];
    protected bool SaveCustomComponentModalOpen { get; set; }
    protected bool IsSavingCustomComponent { get; set; }
    protected long? EditingCustomComponentId { get; set; }
    protected string CustomComponentName { get; set; } = string.Empty;
    protected string CustomComponentDescription { get; set; } = string.Empty;

    private Dictionary<string, List<ReferenceItem>> _referenceData = new();
    protected Dictionary<string, string> DynamicTemplatePreviewHtml { get; } = new();

    // Toasts
    protected List<ToastMessage> Toasts { get; set; } = [];

    // Auto-save timer & dirty tracking
    private const int PreviewDebounceMilliseconds = 300;
    private System.Timers.Timer? _autoSaveTimer;
    private CancellationTokenSource? _previewDebounceCts;
    private string? _previewBaseUri;
    private long _previewRefreshVersion;	
    private readonly Dictionary<string, CompositionHistory> _compositionHistory =
        new(StringComparer.Ordinal);
    private DotNetObjectReference<PageEditor>? _shortcutReference;
    private EditorBlockListMemento? _blockClipboard;
    private EditorNodeMemento? _nodeClipboard;
    private NeoPageNode? _pendingSaveNode;

    // Node-tree undo/redo stacks
    private readonly List<EditorNodeMemento> _treeUndoStack = [];
    private readonly List<EditorNodeMemento> _treeRedoStack = [];
    private const int MaxTreeHistory = 100;

    /// <summary>Tracks whether unsaved changes exist. Auto-save only fires when Dirty.</summary>
    private enum PageState { Clean, Dirty }
    private PageState _pageState = PageState.Dirty;  // new pages start dirty

    // ──────────────────────────────────────────────────────────
    // Lifecycle  (mirrors Alpine.js init())
    // ──────────────────────────────────────────────────────────

    private long? _previousParentId;

    protected override async Task OnParametersSetAsync()
    {
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

    protected override async Task OnInitializedAsync()
    {
        PaletteDragState.Cleared += ClearPaletteDragState;
        RestorePaletteState();

        await ResolvePreviewBaseUriAsync();
        CurrentSite = await ResolveCurrentSiteAsync();
        await LoadCustomComponentsAsync();

        if (Id.HasValue)
        {
            await LoadPageAsync(Id.Value);
        }
        else
        {
            UpdateLastSaved();
        }

        await RefreshParentSlugPrefixAsync();

        _autoSaveTimer = new System.Timers.Timer(30_000);
        _autoSaveTimer.Elapsed += async (_, _) => await InvokeAsync(AutoSaveAsync);
        _autoSaveTimer.AutoReset = true;
        _autoSaveTimer.Start();

        var result = await DocsClient.GetCategoriesAsync();

        if (result is Result<IReadOnlyList<DocsSummary>, AeroError>.Ok ok)
        {
            DocsCategories = ok.Value;
        }
    }

    private async Task LoadPageAsync(long id)
    {
        await LoadReferenceDataAsync();

        var result = await PagesClient.GetByIdAsync(id);
        if (result is Result<CmsPageDetail, AeroError>.Ok ok)
        {
            var page = ok.Value;
            LoadedPage = page;
            PageTitle = page.Title;
            PageSlug = page.Slug;
            _slugState = SlugState.Loaded;  // preserve DB slug — never auto-overwrite
            Summary = page.Excerpt ?? string.Empty;
            SeoTitle = page.SeoTitle ?? string.Empty;
            SeoDescription = page.SeoDescription ?? string.Empty;
            PublicationState = page.PublicationState;
            ShowInNavMenu = page.ShowInNavMenu; 
            ShowHeaderNavigation = page.ShowHeaderNavigation;
            HideFooter = page.HideFooter;
            ShowChatAgent = page.ShowChatAgent;
            ParentId = page.ParentId;
            
            // Load blocks from RootNodeJson
            if (page.RootNodeJson is { Length: > 0 } rootJson)
            {
                RootNode = JsonSerializer.Deserialize<NeoPageNode>(rootJson, BlockJsonContext.Default.Options)
                           ?? CreateDefaultRootNode();
            }

            // Check for a newer draft — if one exists, use it as the in-progress state
            var draftResult = await PagesClient.GetDraftAsync(id);
            if (draftResult is Result<PageDraftSummary?, AeroError>.Ok { Value: not null } draftOk)
            {
                var draft = draftOk.Value;
                PageTitle = draft.Title;
                PageSlug = draft.Slug;
                Summary = draft.Summary ?? string.Empty;
                if (draft.RootNodeJson is { Length: > 0 } draftRootJson)
                {
                    RootNode = JsonSerializer.Deserialize<NeoPageNode>(draftRootJson, BlockJsonContext.Default.Options)
                               ?? CreateDefaultRootNode();
                }
            }

            _compositionHistory.Clear();
            _treeUndoStack.Clear();
            _treeRedoStack.Clear();
            UpdateLastSaved();
            _pageState = PageState.Clean;
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

    private async Task LoadReferenceDataAsync()
    {
        // Reference Picker data
        var pagesTask = PagesClient.GetAllAsync(take: 50);
        var blogsTask = BlogClient.GetAllAsync(take: 50);
        var catsTask = CategoriesClient.GetAllAsync();
        var tagsTask = TagsClient.GetAllAsync();
        var usersTask = UsersClient.GetAllAsync(take: 50);

        await pagesTask;
        await blogsTask;
        await catsTask;
        await tagsTask;
        await usersTask;

        if (pagesTask.Result is Result<PagedResult<PageSummary>, AeroError>.Ok pagesOk)
            _referenceData["pages"] = pagesOk.Value.Items.Select(p => new ReferenceItem(p.Id.ToString(), p.Title)).ToList();
        
        if (blogsTask.Result is Result<PagedResult<BlogSummary>, AeroError>.Ok blogsOk)
            _referenceData["posts"] = blogsOk.Value.Items.Select(p => new ReferenceItem(p.Id.ToString(), p.Title)).ToList();
            
        if (catsTask.Result is Result<IReadOnlyList<CategorySummary>, AeroError>.Ok catsOk)
            _referenceData["categories"] = catsOk.Value.Select(c => new ReferenceItem(c.Id.ToString(), Name: c.Name)).ToList();
            
        if (tagsTask.Result is Result<IReadOnlyList<TagSummary>, AeroError>.Ok tagsOk)
            _referenceData["tags"] = tagsOk.Value.Select(t => new ReferenceItem(t.Id.ToString(), Name: t.Name)).ToList();
            
        if (usersTask.Result is Result<PagedResult<UserSummary>, AeroError>.Ok usersOk)
            _referenceData["authors"] = usersOk.Value.Items.Select(u => new ReferenceItem(u.Id.ToString(), Name: u.DisplayName)).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        PaletteDragState.Cleared -= ClearPaletteDragState;
        _autoSaveTimer?.Dispose();
        _previewDebounceCts?.Cancel();
        _previewDebounceCts?.Dispose();
        if (_shortcutReference is not null)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("PageEditorShortcuts.unregister");
            }
            catch (JSDisconnectedException)
            {
            }

            _shortcutReference.Dispose();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _shortcutReference = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync(
                "PageEditorShortcuts.register",
                _shortcutReference);
        }

        await JSRuntime.InvokeVoidAsync("PeNavTooltip.refresh");
    }

    // ──────────────────────────────────────────────────────────
    // Category toggle  (mirrors toggleCategory())
    // ──────────────────────────────────────────────────────────

    protected void ToggleCategory(string category)
    {
        switch (category)
        {
            case "aeroui":      CategoryAeroUi      = !CategoryAeroUi;      break;
            case "components":  CategoryComponents  = !CategoryComponents;  break;
            case "references":  CategoryReferences  = !CategoryReferences;  break;
            case "settings":    CategorySettings    = !CategorySettings;    break;
            case "hyper":       CategoryHyper       = !CategoryHyper;       break;
            case "neo":         CategoryNeo         = !CategoryNeo;         break;
            case "primitives":  CategoryPrimitives  = !CategoryPrimitives;  break;
            case "custom":      CategoryCustom      = !CategoryCustom;      break;
        }

        PersistPaletteState();
    }

    protected Task OnRightSidebarCollapsedChanged(bool isCollapsed)
    {
        RightSidebarCollapsed = isCollapsed;
        PersistPaletteState();
        return Task.CompletedTask;
    }

    private void RestorePaletteState()
    {
        var state = AdminStorage.GetItem<PaletteState>(PaletteStateStorageKey);
        if (state is null)
        {
            return;
        }

        RightSidebarCollapsed = state.RightSidebarCollapsed;
        CategoryAeroUi = state.CategoryAeroUi;
        CategoryComponents = state.CategoryComponents;
        CategoryReferences = state.CategoryReferences;
        CategorySettings = state.CategorySettings;
        CategoryHyper = state.CategoryHyper;
        CategoryNeo = state.CategoryNeo;
        CategoryPrimitives = state.CategoryPrimitives;
        CategoryCustom = state.CategoryCustom;
    }

    private void PersistPaletteState() =>
        AdminStorage.SetItem(
            PaletteStateStorageKey,
            new PaletteState(
                RightSidebarCollapsed,
                CategoryAeroUi,
                CategoryComponents,
                CategoryReferences,
                CategorySettings,
                CategoryHyper,
                CategoryNeo,
                CategoryPrimitives,
                CategoryCustom));

    private sealed record PaletteState(
        bool RightSidebarCollapsed,
        bool CategoryAeroUi,
        bool CategoryComponents,
        bool CategoryReferences,
        bool CategorySettings,
        bool CategoryHyper,
        bool CategoryNeo,
        bool CategoryPrimitives,
        bool CategoryCustom);

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> CustomCatalogItems =>
        CustomComponents
            .Select((component, index) => new PageEditorCatalog.NeoEditorCatalogItem
            {
                CatalogId = $"custom:{component.Id}",
                DisplayName = component.Name,
                Description = component.Description,
                Section = PageEditorCatalog.NeoEditorCatalogSection.Components,
                Kind = PageEditorCatalog.NeoEditorCatalogKind.Component,
                SortOrder = index,
                IconName = "box",
                AllowChildren = component.Root.Children.Count > 0,
                PublicStaticSsrSafe = true
            })
            .ToList();

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> NeoAeroCatalogItems =>
        Catalog.GetCatalogItems()
            .Where(i => i.Section == PageEditorCatalog.NeoEditorCatalogSection.AeroUi)
            .OrderBy(i => i.SortOrder)
            .ToList();

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> ReferenceCatalogItems { get; } =
    [
        CatalogItem("pages", "Pages", 10),
        CatalogItem("posts", "Posts", 20),
        CatalogItem("categories", "Categories", 30),
        CatalogItem("tags", "Tags", 40),
        CatalogItem("authors", "Authors", 50)
    ];

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> NeoHyperCatalogItems =>
        Catalog.GetCatalogItems()
            .Where(i => i.Section == PageEditorCatalog.NeoEditorCatalogSection.Hyper)
            .OrderBy(i => i.SortOrder)
            .ToList();

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> NeoNeoCatalogItems =>
        DefinitionRegistry.AllDescriptors
            .Select(ToCatalogItem)
            .Where(i => i.Section == PageEditorCatalog.NeoEditorCatalogSection.Neo)
            .OrderBy(i => i.SortOrder)
            .ToList();

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> PrimitiveCatalogItems =>
        DefinitionRegistry.AllDescriptors
            .Select(ToCatalogItem)
            .Where(i => i.Section == PageEditorCatalog.NeoEditorCatalogSection.Primitives)
            .OrderBy(i => i.SortOrder)
            .ToList();

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> ComponentsCatalogItems =>
        DefinitionRegistry.AllDescriptors
            .Select(ToCatalogItem)
            .Where(i => i.Section == PageEditorCatalog.NeoEditorCatalogSection.Components)
            .OrderBy(i => i.SortOrder)
            .ToList();

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> FilterPaletteItems(
        IEnumerable<PageEditorCatalog.NeoEditorCatalogItem> items)
    {
        return items
            .Where(item => PaletteSearchMatcher.Matches(
                CreatePaletteSearchDocument(item),
                PaletteSearch))
            .ToList();
    }

    protected bool HasPaletteSearchResults =>
        string.IsNullOrWhiteSpace(PaletteSearch) ||
        new IEnumerable<PageEditorCatalog.NeoEditorCatalogItem>[]
        {
            CustomCatalogItems,
            NeoAeroCatalogItems,
            PrimitiveCatalogItems,
            ComponentsCatalogItems,
            ReferenceCatalogItems,
            NeoHyperCatalogItems,
            NeoNeoCatalogItems
        }.Any(items => FilterPaletteItems(items).Count > 0);

    private PaletteSearchDocument CreatePaletteSearchDocument(
        PageEditorCatalog.NeoEditorCatalogItem item)
    {
        IReadOnlyCollection<string>? keywords = null;
        if (TryGetCustomComponentId(item.CatalogId, out var componentId))
        {
            var component = CustomComponents.FirstOrDefault(candidate =>
                candidate.Id == componentId);
            if (component is not null)
            {
                keywords = component.Tags
                    .Prepend(component.Category)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList();
            }
        }

        return new PaletteSearchDocument(
            item.DisplayName,
            item.Description,
            item.CatalogId,
            item.Kind.ToString(),
            item.Section.ToString(),
            keywords);
    }

    private static PageEditorCatalog.NeoEditorCatalogItem ToCatalogItem(
        PageEditorDefinitionDescriptor definition) =>
        new()
        {
            CatalogId = definition.CatalogId,
            DisplayName = definition.Catalog.DisplayName,
            Description = definition.Catalog.Description,
            Section = ToCatalogSection(definition.Catalog.Category),
            Kind = ToCatalogKind(definition.Catalog.Kind),
            SortOrder = definition.Catalog.SortOrder,
            IconName = definition.Catalog.IconName,
            AllowChildren = definition.Catalog.Composition.CanContainChildren,
            PublicStaticSsrSafe = definition.Catalog.PublicStaticSsrSafe,
            EditorPreviewComponentType = definition.Catalog.PreviewComponentType,
            PropertyEditorComponentType = definition.Catalog.PropertyEditorComponentType
        };

    private static PageEditorCatalog.NeoEditorCatalogSection ToCatalogSection(string? category) =>
        category?.Trim().ToLowerInvariant() switch
        {
            "aero ui" or "aeroui" or "aero" => PageEditorCatalog.NeoEditorCatalogSection.AeroUi,
            "aero ux" or "aeroux" => PageEditorCatalog.NeoEditorCatalogSection.AeroUi,
            "legacy ui" or "legacy" => PageEditorCatalog.NeoEditorCatalogSection.AeroUi,
            "media" => PageEditorCatalog.NeoEditorCatalogSection.AeroUi,
            "primitive" or "primitives" => PageEditorCatalog.NeoEditorCatalogSection.Primitives,
            "component" or "components" => PageEditorCatalog.NeoEditorCatalogSection.Components,
            "hyper" or "hyperui" or "hyper ui" => PageEditorCatalog.NeoEditorCatalogSection.Hyper,
            "neo" or "neoui" or "neo ui" => PageEditorCatalog.NeoEditorCatalogSection.Neo,
            _ => PageEditorCatalog.NeoEditorCatalogSection.AeroUi
        };

    private static PageEditorCatalog.NeoEditorCatalogKind ToCatalogKind(string? kind) =>
        kind?.Trim().ToLowerInvariant() switch
        {
            "primitive" => PageEditorCatalog.NeoEditorCatalogKind.Primitive,
            "component" => PageEditorCatalog.NeoEditorCatalogKind.Component,
            _ => PageEditorCatalog.NeoEditorCatalogKind.Block
        };

    private static PageEditorCatalog.NeoEditorCatalogKind ToCatalogKind(NeoPageNodeKind kind) =>
        kind switch
        {
            NeoPageNodeKind.Primitive => PageEditorCatalog.NeoEditorCatalogKind.Primitive,
            NeoPageNodeKind.Component or NeoPageNodeKind.Container or NeoPageNodeKind.Section =>
                PageEditorCatalog.NeoEditorCatalogKind.Component,
            _ => PageEditorCatalog.NeoEditorCatalogKind.Block
        };

    private static PageEditorCatalog.NeoEditorCatalogItem CatalogItem(string id, string name, int sortOrder) =>
        new()
        {
            CatalogId = id,
            DisplayName = name,
            Section = PageEditorCatalog.NeoEditorCatalogSection.AeroUi,
            Kind = PageEditorCatalog.NeoEditorCatalogKind.Block,
            SortOrder = sortOrder,
            IconName = "box",
            PublicStaticSsrSafe = true
        };

    // ──────────────────────────────────────────────────────────
    // Node tree helpers
    // ──────────────────────────────────────────────────────────

    /// <summary>Finds a node by ID in root's direct children.</summary>
    private NeoPageNode? FindRootChild(string nodeId) =>
        RootNode.Children.FirstOrDefault(n => n.NodeId == nodeId);

    /// <summary>Finds a node anywhere in the tree by ID.</summary>
    private NeoPageNode? FindNodeInTree(string nodeId) =>
        FindRootChild(nodeId) ?? FindNodeRecursive(RootNode.Children, nodeId);

    private static NeoPageNode? FindNodeRecursive(List<NeoPageNode> parents, string nodeId)
    {
        foreach (var parent in parents)
        {
            var found = parent.Children.FirstOrDefault(n => n.NodeId == nodeId);
            if (found is not null)
                return found;
            if (FindNodeRecursive(parent.Children, nodeId) is { } deep)
                return deep;
        }
        return null;
    }

    /// <summary>
    /// Finds the parent and index of the node with the given ID.
    /// Returns null for the root node itself (no parent).
    /// </summary>
    private (NeoPageNode? Parent, int Index) FindParentAndIndex(string nodeId)
    {
        for (var i = 0; i < RootNode.Children.Count; i++)
        {
            if (RootNode.Children[i].NodeId == nodeId)
                return (RootNode, i);
        }
        return FindParentRecursive(RootNode.Children, nodeId);
    }

    private static (NeoPageNode? Parent, int Index) FindParentRecursive(List<NeoPageNode> parents, string nodeId)
    {
        foreach (var parent in parents)
        {
            for (var i = 0; i < parent.Children.Count; i++)
            {
                if (parent.Children[i].NodeId == nodeId)
                    return (parent, i);
            }
            var (foundParent, foundIndex) = FindParentRecursive(parent.Children, nodeId);
            if (foundParent is not null)
                return (foundParent, foundIndex);
        }
        return (null, -1);
    }

    // ──────────────────────────────────────────────────────────
    // Block management  (mirrors addBlock / deleteBlock / etc.)
    // ──────────────────────────────────────────────────────────

    protected NeoPageNode CreateNode(string catalogId)
    {
        if (DefinitionRegistry.TryGetDescriptor(catalogId, out var descriptor) &&
            descriptor.NodeFactory is { } factory)
        {
            return factory.CreateDefaultNode();
        }

        // Legacy path: create from EditorBlock definition and extract node
        var block = CreateBlock(catalogId);
        var node = block.CompositionNodes is { Count: > 0 } nodes
            ? nodes[0]
            : new NeoPageNode
            {
                NodeId = block.EditorId ?? Guid.NewGuid().ToString("N"),
                CatalogId = "primitive.section",
                Kind = NeoPageNodeKind.Section,
                Style = block.Style?.DeepClone() ?? new ResponsiveNodeStyle(),
                Children = []
            };
        if (string.IsNullOrWhiteSpace(node.NodeId))
            node.NodeId = Guid.NewGuid().ToString("N");
        return node;
    }

    protected void AddBlock(string type)
    {
        EnsureCanvasHistory();
        var node = CreateNode(type);
        RootNode.Children.Add(node);
        RecordCanvasMutation();
        SelectBlock(node.NodeId);
        MarkDirty();
        ShowToast(L["Block added"], "success");
        QueuePreviewRefresh();
    }

    protected Task OnEditorBlockChanged(EditorBlock block)
    {
        MarkDirty();
        QueuePreviewRefresh();
        return Task.CompletedTask;
    }

    protected void OpenBlockEditor(string editorId)
    {
        SelectedBlockId = editorId;
        EditingBlockId = editorId;
        EditingNodeId = null;
        BlockEditorTab = "design";
        BlockEditorModalOpen = true;
    }

    protected void CloseBlockEditor()
    {
        BlockEditorModalOpen = false;
        EditingBlockId = null;
        EditingNodeId = null;
    }

    protected void OpenSaveCustomComponentModal()
    {
        if (CurrentEditNode is null && _pendingSaveNode is null)
        {
            return;
        }

        _pendingSaveNode = null;
        CustomComponentName = CurrentEditNode is not null
            ? GetBlockDisplayName(CurrentEditNode.CatalogId)
            : string.Empty;
        CustomComponentDescription = string.Empty;
        EditingCustomComponentId = null;
        SaveCustomComponentModalOpen = true;
    }

    protected void OpenEditCustomComponentModal(
        PageEditorCatalog.NeoEditorCatalogItem item)
    {
        if (!TryGetCustomComponentId(item.CatalogId, out var componentId))
        {
            return;
        }

        var component = CustomComponents.FirstOrDefault(candidate =>
            candidate.Id == componentId);
        if (component is null)
        {
            return;
        }

        EditingCustomComponentId = component.Id;
        CustomComponentName = component.Name;
        CustomComponentDescription = component.Description ?? string.Empty;
        SaveCustomComponentModalOpen = true;
    }

    protected void CloseSaveCustomComponentModal()
    {
        if (IsSavingCustomComponent)
        {
            return;
        }

        _pendingSaveNode = null;
        SaveCustomComponentModalOpen = false;
        EditingCustomComponentId = null;
    }

    protected async Task SaveCurrentBlockAsCustomAsync()
    {
        if ((!EditingCustomComponentId.HasValue && CurrentEditNode is null && _pendingSaveNode is null) ||
            string.IsNullOrWhiteSpace(CustomComponentName))
        {
            ShowToast(L["A component name is required."], "error");
            return;
        }

        IsSavingCustomComponent = true;
        try
        {
            var existing = EditingCustomComponentId.HasValue
                ? CustomComponents.FirstOrDefault(component =>
                    component.Id == EditingCustomComponentId.Value)
                : null;
            var root = _pendingSaveNode is not null
                ? CustomComponentTemplate.Capture(_pendingSaveNode)
                : existing is not null
                    ? CustomComponentTemplate.Capture(existing.Root)
                    : CustomComponentTemplate.Capture(CurrentEditNode!);
            var request = new SavePageCustomComponentRequest(
                CustomComponentName.Trim(),
                root,
                string.IsNullOrWhiteSpace(CustomComponentDescription)
                    ? null
                    : CustomComponentDescription.Trim());
            var result = EditingCustomComponentId.HasValue
                ? await CustomComponentsClient.UpdateAsync(
                    EditingCustomComponentId.Value,
                    request)
                : await CustomComponentsClient.CreateAsync(request);
            if (result is Result<PageCustomComponentDetail, AeroError>.Ok ok)
            {
                CustomComponents = CustomComponents
                    .Where(component => component.Id != ok.Value.Id)
                    .Append(ok.Value)
                    .OrderBy(component => component.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                SaveCustomComponentModalOpen = false;
                EditingCustomComponentId = null;
                CategoryCustom = true;
                ShowToast(L["Custom component saved"], "success");
            }
            else if (result is Result<PageCustomComponentDetail, AeroError>.Failure failure)
            {
                ShowToast(L["Could not save custom component: {0}", failure.Error], "error");
            }
        }
        finally
        {
            _pendingSaveNode = null;
            IsSavingCustomComponent = false;
        }
    }

    protected async Task DeleteCustomComponentAsync(
        PageEditorCatalog.NeoEditorCatalogItem item)
    {
        if (!TryGetCustomComponentId(item.CatalogId, out var componentId))
        {
            return;
        }

        var component = CustomComponents.FirstOrDefault(candidate =>
            candidate.Id == componentId);
        if (component is null)
        {
            return;
        }

        var confirmed = await DialogService.Confirm(
            L["Delete custom component '{0}'? Existing page instances will not be changed.", component.Name],
            L["Delete Custom Component"],
            new ConfirmOptions
            {
                OkButtonText = L["Delete"],
                CancelButtonText = L["Cancel"]
            });
        if (confirmed != true)
        {
            return;
        }

        var result = await CustomComponentsClient.DeleteAsync(component.Id);
        if (result is Result<bool, AeroError>.Ok)
        {
            CustomComponents = CustomComponents
                .Where(candidate => candidate.Id != component.Id)
                .ToList();
            ShowToast(L["Custom component deleted"], "success");
        }
        else if (result is Result<bool, AeroError>.Failure failure)
        {
            ShowToast(L["Could not delete custom component: {0}", failure.Error], "error");
        }
    }

    private static bool TryGetCustomComponentId(
        string catalogId,
        out long componentId)
    {
        componentId = default;
        return catalogId.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) &&
               long.TryParse(
                   catalogId.AsSpan("custom:".Length),
                   out componentId);
    }

    private async Task LoadCustomComponentsAsync()
    {
        var result = await CustomComponentsClient.GetAllAsync();
        if (result is Result<IReadOnlyList<PageCustomComponentDetail>, AeroError>.Ok ok)
        {
            CustomComponents = ok.Value
                .OrderBy(component => component.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private NeoPageNode CreateCustomComponentRoot(EditorBlock block)
    {
        if (block.CompositionNodes.Count == 1)
        {
            return CustomComponentTemplate.Capture(block.CompositionNodes[0]);
        }

        if (block.CompositionNodes.Count > 1)
        {
            return new NeoPageNode
            {
                NodeId = Guid.NewGuid().ToString("N"),
                CatalogId = "ui.container",
                Kind = NeoPageNodeKind.Container,
                Children = block.CompositionNodes
                    .Select(CustomComponentTemplate.Capture)
                    .ToList()
            };
        }

        var node = MapEditorBlockToNeoNode(block);
        if (string.IsNullOrWhiteSpace(node.NodeId))
        {
            node.NodeId = Guid.NewGuid().ToString("N");
        }

        return node;
    }

    protected string GetBlockDisplayName(string catalogId)
    {
        var allItems = NeoAeroCatalogItems
            .Concat(NeoHyperCatalogItems)
            .Concat(ComponentsCatalogItems)
            .Concat(PrimitiveCatalogItems)
            .Concat(ReferenceCatalogItems);

        return allItems.FirstOrDefault(item => item.CatalogId == catalogId)?.DisplayName
            ?? catalogId;
    }

    private EditorBlock CreateBlock(string type)
    {
        if (DefinitionRegistry.TryGetDescriptor(type, out var descriptor) &&
            descriptor.LegacyDefinition is { } definition)
        {
            var defaultBlock = definition.CreateDefaultEditorBlock();
            defaultBlock.Type = string.IsNullOrWhiteSpace(defaultBlock.Type)
                ? descriptor.CatalogId
                : defaultBlock.Type;
            return defaultBlock;
        }

        if (DefinitionRegistry.TryGetDescriptor(type, out descriptor))
        {
            return new EditorBlock
            {
                Type = descriptor.CatalogId,
                CompositionNodes = [descriptor.NodeFactory.CreateDefaultNode()]
            };
        }

        var block = new EditorBlock { Type = type };

        switch (type)
        {
            // All block types are now registered in the definition registry.
            // If a type is not found, fall through to unknown type handling.
            default:
                // fall through silently - unknown type
                break;
        }

        return block;
    }

    /// <summary>
    /// Converts an EditorBlock to its corresponding BlockBase for property editing.
    /// </summary>
    private BlockBase? GetBlockBaseForEditor(EditorBlock? block)
    {
        if (block == null) return null;

        if (TryMapWithDefinition(block, out var mappedBlock, out _))
        {
            return mappedBlock;
        }

        var node = MapEditorBlockToNeoNode(block);
        return block.Type switch
        {
            _ => null
        };
    }

    private NeoPageNode MapEditorBlockToNeoNode(EditorBlock block)
    {
        if (TryMapWithDefinition(block, out _, out var mappedNode))
        {
            return mappedNode;
        }

        // Native definitions (no LegacyDefinition): preserve CompositionNodes
        if (block.CompositionNodes is { Count: > 0 } compositionNodes)
        {
            var root = compositionNodes[0];
            // Reparent any additional root nodes as children of the first
            for (var i = 1; i < compositionNodes.Count; i++)
                root.Children.Add(compositionNodes[i]);
            return root;
        }

        return block.Type switch
        {
        _ => new NeoPageNode { CatalogId = "primitive.section", Kind = NeoPageNodeKind.Section, Properties = [] }
        };
    }

    private bool TryMapWithDefinition(
        EditorBlock block,
        out BlockBase? mappedBlock,
        out NeoPageNode mappedNode)
    {
        mappedBlock = null;
        mappedNode = new NeoPageNode { CatalogId = "primitive.section", Kind = NeoPageNodeKind.Section, Properties = [] };

        if (!DefinitionRegistry.TryGetDescriptor(block.Type, out var descriptor) ||
            descriptor.LegacyDefinition is not { } definition)
        {
            return false;
        }

        mappedBlock = definition.ToBlockBase(block);
        mappedNode = definition.ToNeoPageNode(block);
        if (string.IsNullOrWhiteSpace(mappedNode.CatalogId))
        {
            mappedNode.CatalogId = descriptor.CatalogId;
        }

        if (mappedNode.Kind == default)
        {
            mappedNode.Kind = NeoPageNodeKind.Section;
        }

        return true;
    }

    protected void SelectBlock(string id) => SelectedBlockId = id;

    protected void DeleteBlock(int index)
    {
        EnsureCanvasHistory();
        if (index >= 0 && index < RootNode.Children.Count)
        {
            var nodeId = RootNode.Children[index].NodeId;
            RootNode.Children.RemoveAt(index);
            RecordCanvasMutation();
            if (SelectedBlockId == nodeId)
                SelectedBlockId = null;
            MarkDirty();
            ShowToast(L["Block deleted"]);
            QueuePreviewRefresh();
        }
    }

    protected void DuplicateBlock(int index)
    {
        if (index < 0 || index >= RootNode.Children.Count) return;

        EnsureCanvasHistory();
        var original = RootNode.Children[index];
        var clone = EditorNodeMemento.Capture(original).Restore();
        clone.NodeId = Guid.NewGuid().ToString("N");
        RootNode.Children.Insert(index + 1, clone);
        RecordCanvasMutation();
        MarkDirty();
        ShowToast(L["Block duplicated"], "success");
        QueuePreviewRefresh();
    }

    protected void MoveBlockUp(int index)
    {
        if (index <= 0 || index >= RootNode.Children.Count) return;
        EnsureCanvasHistory();
        (RootNode.Children[index], RootNode.Children[index - 1]) =
            (RootNode.Children[index - 1], RootNode.Children[index]);
        RecordCanvasMutation();
        MarkDirty();
        QueuePreviewRefresh();
    }

    protected void MoveBlockDown(int index)
    {
        if (index < 0 || index >= RootNode.Children.Count - 1) return;
        EnsureCanvasHistory();
        (RootNode.Children[index], RootNode.Children[index + 1]) =
            (RootNode.Children[index + 1], RootNode.Children[index]);
        RecordCanvasMutation();
        MarkDirty();
        QueuePreviewRefresh();
    }

    // ──────────────────────────────────────────────────────────
    // Node operation handlers  (called from CanvasTree)
    // ──────────────────────────────────────────────────────────

    protected void OnNodeSelected(string nodeId)
    {
        SelectedBlockId = nodeId;
    }

    protected void OnNodeEditRequested(string nodeId)
    {
        var node = FindNodeInTree(nodeId);
        if (node is null) return;

        OpenBlockEditor(nodeId);
    }

    /// <summary>
    /// Applies a mutation emitted by a nested composition surface in the page tree.
    /// The child surface has already validated the mutation, so the editor owns
    /// history, dirty state, and preview refresh from this point forward.
    /// </summary>
    protected Task OnRootCompositionChanged(CompositionMutation mutation)
    {
        _treeUndoStack.Add(EditorNodeMemento.Capture(mutation.Before));
        if (_treeUndoStack.Count > MaxTreeHistory)
            _treeUndoStack.RemoveRange(0, _treeUndoStack.Count - MaxTreeHistory);

        _treeRedoStack.Clear();
        SyncRootNodeFrom(mutation.After);
        MarkDirty();
        QueuePreviewRefresh();
        return InvokeAsync(StateHasChanged);
    }

    protected void OnRootCompositionDropRejected(string message)
    {
        CompositionDropError = message;
        ShowToast(L["Cannot place item here: {0}", message], "error");
        _ = InvokeAsync(StateHasChanged);
    }

    /// <summary>Handle drop from palette or tree reorder.</summary>
    protected async Task OnCanvasDrop(CanvasDropArgs args)
    {
        // Palette drop: DraggedType is set by the palette DragStart
        // or passed via args.CatalogId (from future dataTransfer-based flows).
        var paletteCatalogId = args.CatalogId ?? PaletteDragState.CatalogId ?? DraggedType;
        if (!string.IsNullOrWhiteSpace(paletteCatalogId))
        {
            EnsureCanvasHistory();
            var catalogId = paletteCatalogId;
            PaletteDragState.Clear();

            if (TryGetCustomComponentId(catalogId, out var componentId))
            {
                var result = await CustomComponentsClient.CreateInstanceAsync(componentId);
                if (result is Result<NeoPageNode, AeroError>.Ok ok)
                {
                    var node = ok.Value;
                    var container = FindNodeInTree(args.ParentNodeId) ?? RootNode;
                    var insIdx = Math.Clamp(args.InsertAtIndex, 0, container.Children.Count);
                    container.Children.Insert(insIdx, node);
                    RecordCanvasMutation();
                    SelectBlock(node.NodeId);
                    MarkDirty();
                    QueuePreviewRefresh();
                    ShowToast(L["Custom component added"], "success");
                    return;
                }
                if (result is Result<NeoPageNode, AeroError>.Failure failure)
                {
                    ShowToast(L["Could not add custom component: {0}", failure.Error], "error");
                    return;
                }
            }

            AddChildNodeAt(catalogId, args.ParentNodeId, args.InsertAtIndex);
            return;
        }

        // Tree reorder: move the dropped node
        if (!string.IsNullOrWhiteSpace(args.TargetSiblingNodeId))
        {
            var (parent, index) = FindParentAndIndex(args.TargetSiblingNodeId);
            if (parent is null) return;

            EnsureCanvasHistory();
            var targetNode = parent.Children[index];
            parent.Children.RemoveAt(index);

            var newParent = FindNodeInTree(args.ParentNodeId) ?? RootNode;

            var insertAt = Math.Clamp(args.InsertAtIndex, 0, newParent.Children.Count);
            newParent.Children.Insert(insertAt, targetNode);
            RecordCanvasMutation();
            MarkDirty();
            QueuePreviewRefresh();
        }
    }

    /// <summary>Add a child node at a specific position in the tree.</summary>
    private void AddChildNodeAt(string catalogId, string parentNodeId, int insertAtIndex)
    {
        NeoPageNode node;
        if (DefinitionRegistry.TryGetDescriptor(catalogId, out var descriptor))
        {
            node = descriptor.NodeFactory.CreateDefaultNode();
        }
        else
        {
            node = new NeoPageNode
            {
                NodeId = Guid.NewGuid().ToString("N"),
                CatalogId = "primitive.section",
                Kind = NeoPageNodeKind.Section,
                Children = []
            };
        }

        var parent = FindNodeInTree(parentNodeId) ?? RootNode;
        var idx = Math.Clamp(insertAtIndex, 0, parent.Children.Count);
        parent.Children.Insert(idx, node);

        SelectBlock(node.NodeId);
        MarkDirty();
        ShowToast(L["Block added"], "success");
        QueuePreviewRefresh();
    }

    protected void OnNodeCopy(string nodeId)
    {
        var node = FindNodeInTree(nodeId);
        if (node is null) return;
        _nodeClipboard = EditorNodeMemento.Capture(node);
        _blockClipboard = null;  // clear legacy clipboard
        ShowToast(L["Node copied"], "success");
    }

    protected void OnNodePaste(string targetNodeId)
    {
        if (_nodeClipboard is null)
        {
            ShowToast(L["Nothing to paste"], "info");
            return;
        }

        EnsureCanvasHistory();
        var restored = _nodeClipboard.Restore();
        var (parent, index) = FindParentAndIndex(targetNodeId);
        var container = parent ?? RootNode;
        container.Children.Insert(Math.Clamp(index + 1, 0, container.Children.Count), restored);
        RecordCanvasMutation();
        SelectBlock(restored.NodeId);
        MarkDirty();
        QueuePreviewRefresh();
        ShowToast(L["Node pasted"], "success");
    }

    protected void OnNodeSaveAsCustom(string nodeId)
    {
        var node = FindNodeInTree(nodeId);
        if (node is null) return;

        _pendingSaveNode = node;
        EditingCustomComponentId = null;
        CustomComponentName = node.CatalogId;
        CustomComponentDescription = string.Empty;
        SaveCustomComponentModalOpen = true;
    }

    protected void OnNodeDuplicate(string nodeId)
    {
        var node = FindNodeInTree(nodeId);
        if (node is null) return;

        EnsureCanvasHistory();
        var (parent, index) = FindParentAndIndex(nodeId);
        if (parent is null) return;

        var clone = EditorNodeMemento.Capture(node).Restore();
        clone.NodeId = Guid.NewGuid().ToString("N");
        parent.Children.Insert(index + 1, clone);
        RecordCanvasMutation();
        SelectBlock(clone.NodeId);
        MarkDirty();
        ShowToast(L["Block duplicated"], "success");
        QueuePreviewRefresh();
    }

    protected void OnNodeDelete(string nodeId)
    {
        if (nodeId == RootNode.NodeId) return;  // can't delete root

        EnsureCanvasHistory();
        var (parent, index) = FindParentAndIndex(nodeId);
        if (parent is null) return;

        parent.Children.RemoveAt(index);
        RecordCanvasMutation();
        if (SelectedBlockId == nodeId)
            SelectedBlockId = null;
        MarkDirty();
        ShowToast(L["Node deleted"]);
        QueuePreviewRefresh();
    }

    protected void OnNodeMoveUp(string nodeId)
    {
        var (parent, index) = FindParentAndIndex(nodeId);
        if (parent is null || index <= 0) return;

        EnsureCanvasHistory();
        (parent.Children[index], parent.Children[index - 1]) = (parent.Children[index - 1], parent.Children[index]);
        RecordCanvasMutation();
        MarkDirty();
        QueuePreviewRefresh();
    }

    protected void OnNodeMoveDown(string nodeId)
    {
        var (parent, index) = FindParentAndIndex(nodeId);
        if (parent is null || index >= parent.Children.Count - 1) return;

        EnsureCanvasHistory();
        (parent.Children[index], parent.Children[index + 1]) = (parent.Children[index + 1], parent.Children[index]);
        RecordCanvasMutation();
        MarkDirty();
        QueuePreviewRefresh();
    }

    // ──────────────────────────────────────────────────────────
    // Drag & Drop  (mirrors dragStart / dragStartBlock / drop / etc.)
    // ──────────────────────────────────────────────────────────

    protected void DragStart(DragEventArgs e, string type)
    {
        DraggedType  = type;
        DraggedIndex   = null;
        PaletteDragState.Start(type);
    }

    private void ClearPaletteDragState()
    {
        DraggedType = null;
        DraggedIndex = null;
    }

    /// <summary>Handle canvas reorder (legacy — now handled by CanvasTree).</summary>
    protected void OnCanvasReordered(IList<EditorBlock> reordered)
    {
        // No-op: CanvasTree handles reorder via OnNodeMoveUp/OnNodeMoveDown
    }

    /// <summary>Handle catalog item dropped onto canvas from Sortable palette.</summary>
    protected async Task OnCatalogItemTransferred(SortableTransferArgs args)
    {
        var catalogId = args.ActiveId;
        if (TryGetCustomComponentId(catalogId, out var componentId))
        {
            var result = await CustomComponentsClient.CreateInstanceAsync(componentId);
            if (result is Result<NeoPageNode, AeroError>.Ok ok)
            {
                EnsureCanvasHistory();
                var node = ok.Value;
                RootNode.Children.Add(node);
                RecordCanvasMutation();
                SelectBlock(node.NodeId);
                MarkDirty();
                QueuePreviewRefresh();
                ShowToast(L["Custom component added"], "success");
            }
            else if (result is Result<NeoPageNode, AeroError>.Failure failure)
            {
                ShowToast(L["Could not add custom component: {0}", failure.Error], "error");
            }

            return;
        }

        if (!string.IsNullOrEmpty(catalogId))
        {
            AddBlock(catalogId);
        }
    }

    // ──────────────────────────────────────────────────────────
    // Markdown  (mirrors renderMarkdown())
    // ──────────────────────────────────────────────────────────

    protected static string RenderMarkdown(string? content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;

        var html = content
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")   // basic escape
            ;

        // headings
        html = Regex.Replace(html, @"^#### (.+)$", "<h4>$1</h4>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^### (.+)$",  "<h3>$1</h3>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^## (.+)$",   "<h2>$1</h2>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^# (.+)$",    "<h1>$1</h1>", RegexOptions.Multiline);

        // inline
        html = Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        html = Regex.Replace(html, @"\*(.+?)\*",      "<em>$1</em>");
        html = Regex.Replace(html, @"`(.+?)`",         "<code>$1</code>");
        html = Regex.Replace(html, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\">$1</a>");

        // lists
        html = Regex.Replace(html, @"^- (.+)$", "<li>$1</li>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"(<li>.+</li>)+", m => $"<ul>{m.Value}</ul>", RegexOptions.Singleline);

        // paragraphs
        var lines = html.Split('\n');
        html = string.Concat(lines.Select(l =>
            l.Trim().Length > 0 && !l.TrimStart().StartsWith('<') ? $"<p>{l}</p>" : l));

        return html;
    }

    protected void SanitizeHtmlPaste(HtmlEditorPasteEventArgs args)
    {
        args.Html = HtmlSanitizer.Sanitize(args.Html);
    }

    protected async Task RefreshDynamicTemplatePreviewAsync(EditorBlock block)
    {
        if (string.IsNullOrWhiteSpace(block.ScribanTemplate))
        {
            DynamicTemplatePreviewHtml[block.EditorId] = $"<div class=\"text-sm text-red-600\">{L["Template is required."]}</div>";
            return;
        }

        JsonDocument? data = null;
        try
        {
            data = string.IsNullOrWhiteSpace(block.ScribanDataJson)
                ? JsonDocument.Parse("{}")
                : JsonDocument.Parse(block.ScribanDataJson);

            var previewBlock = new DynamicTemplateBlock
            {
                DefinitionVersion = 1,
                InlineTemplate = block.ScribanTemplate,
                Data = data
            };

            var result = await PreviewClient.RenderBlockFragmentAsync(previewBlock);
            DynamicTemplatePreviewHtml[block.EditorId] = result switch
            {
                Result<string, AeroError>.Ok ok => ok.Value,
                Result<string, AeroError>.Failure failure => BuildPreviewError(failure.Error.ToString()),
                _ => BuildPreviewError(L["Preview failed."])
            };
        }
        catch (JsonException ex)
        {
            DynamicTemplatePreviewHtml[block.EditorId] = BuildPreviewError(L["Invalid JSON data: {0}", ex.Message]);
        }
        finally
        {
            data?.Dispose();
        }
    }

    private static string BuildPreviewError(string message)
    {
        return $"<div class=\"text-sm text-red-600\">{System.Net.WebUtility.HtmlEncode(message)}</div>";
    }

    // ──────────────────────────────────────────────────────────
    // Media selector  (mirrors openMediaSelector / confirmMediaSelection / etc.)
    // ──────────────────────────────────────────────────────────

    protected void OpenMediaSelector(EditorBlock block, bool isGallery = false, string? context = null)
    {
        CurrentMediaBlock = block;
        IsGalleryMode     = isGallery;
        MediaContext      = context;
        NestedMediaTarget = null;
        NativeMediaNodeId = null;
        NativeMediaField = null;
        MediaModalOpen    = true;
        InvokeAsync(StateHasChanged);
    }

    protected void OpenMediaSelectorForNested(EditorBlock parent, int colIndex, NestedBlock nb)
    {
        CurrentMediaBlock = parent;
        IsGalleryMode     = false;
        MediaContext      = "nested";
        NestedMediaTarget = nb;
        MediaModalOpen    = true;
        InvokeAsync(StateHasChanged);
    }

    protected void OpenNodeMediaSelector(
        EditorBlock block,
        string nodeId,
        string field,
        EditorBreakpoint breakpoint)
    {
        CurrentMediaBlock = block;
        IsGalleryMode = false;
        MediaContext = "native";
        NestedMediaTarget = null;
        NativeMediaNodeId = nodeId;
        NativeMediaField = field;
        NativeMediaBreakpoint = breakpoint;
        MediaModalOpen = true;
        InvokeAsync(StateHasChanged);
    }

    protected void OpenAudioSelector(EditorBlock block)
    {
        // Simulate audio selection with a placeholder URL
        block.Src = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-1.mp3";
        ShowToast(L["Audio added"], "success");
    }

    private async Task OnConfirmMediaSelection(List<MediaItem> items)
    {
        await AutoSaveAsync();
        if (!items.Any()) return;

        if (MediaContext == "native" &&
            CurrentMediaBlock is not null &&
            NativeMediaField == "background-image")
        {
            var selected = items.First();
            var image = new BackgroundImageStyle
            {
                MediaId = selected.Id,
                Url = selected.Src,
                Size = BackgroundImageSize.Cover
            };

            var style = FindCompositionNode(
                    CurrentMediaBlock.CompositionNodes,
                    NativeMediaNodeId)
                ?.Style;
            if (style is null &&
                string.Equals(
                    CurrentMediaBlock.EditorId,
                    NativeMediaNodeId,
                    StringComparison.Ordinal))
            {
                style = CurrentMediaBlock.Style;
            }

            if (style is null)
            {
                return;
            }

            switch (NativeMediaBreakpoint)
            {
                case EditorBreakpoint.Desktop:
                    style.Base = style.Base with { BackgroundImage = image };
                    break;
                case EditorBreakpoint.Tablet:
                    style.Tablet = (style.Tablet ?? new()) with
                    {
                        BackgroundImage = image
                    };
                    break;
                case EditorBreakpoint.Mobile:
                    style.Mobile = (style.Mobile ?? new()) with
                    {
                        BackgroundImage = image
                    };
                    break;
            }
        }
        else if (MediaContext == "native" &&
                 CurrentMediaBlock is not null &&
                 NativeMediaField?.StartsWith(
                     "property:",
                     StringComparison.Ordinal) == true &&
                 FindCompositionNode(
                     CurrentMediaBlock.CompositionNodes,
                     NativeMediaNodeId) is { } propertyNode)
        {
            var propertyName = NativeMediaField["property:".Length..];
            propertyNode.Properties[propertyName] =
                System.Text.Json.JsonSerializer.SerializeToElement(
                    items.First().Src);
        }
        else if (MediaContext == "background" && CurrentMediaBlock != null)
        {
            CurrentMediaBlock.BackgroundImage = items.First().Src;
        }
        else if (MediaContext == "video" && CurrentMediaBlock != null)
        {
            // Set the URL and auto-load the video (resolves YouTube/Vimeo embeds or direct URL)
            // LoadVideo handles its own toast — skip the generic one below.
            CurrentMediaBlock.Url = items.First().Src;
            LoadVideo(CurrentMediaBlock);
            MediaModalOpen = false;
            return;
        }
        else if (MediaContext == "nested" && NestedMediaTarget is not null)
        {
            NestedMediaTarget.Src = items.First().Src;
            NestedMediaTarget.Alt = items.First().Alt;
        }
        else if (IsGalleryMode && CurrentMediaBlock != null)
        {
            CurrentMediaBlock.GalleryImages.AddRange(
                items.Select(img => new GalleryImage { Src = img.Src, Alt = img.Alt }));
        }
        else if (CurrentMediaBlock != null)
        {
            CurrentMediaBlock.Src = items.First().Src;
            CurrentMediaBlock.Alt = items.First().Alt;
        }

        MediaModalOpen = false;
        MarkDirty();
        QueuePreviewRefresh();
        ShowToast(L["Media added"], "success");
    }

    private static NeoPageNode? FindCompositionNode(
        IEnumerable<NeoPageNode> nodes,
        string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        foreach (var node in nodes)
        {
            if (string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
            {
                return node;
            }

            if (FindCompositionNode(node.Children, nodeId) is { } child)
            {
                return child;
            }
        }

        return null;
    }

    protected void RemoveImage(EditorBlock block)
    {
        block.Src     = string.Empty;
        block.Alt     = string.Empty;
        block.Caption = string.Empty;
        MarkDirty();
        QueuePreviewRefresh();
    }

    // ──────────────────────────────────────────────────────────
    // Video  (mirrors loadVideo / removeVideo)
    // ──────────────────────────────────────────────────────────

    protected void LoadVideo(EditorBlock block)
    {
        var url      = block.Url;
        var embedUrl = ResolveVideoEmbed(url);

        if (!string.IsNullOrEmpty(embedUrl))
        {
            block.Src = embedUrl;
            ShowToast(L["Video added"], "success");
        }
        else
        {
            ShowToast(L["Invalid video URL"], "error");
        }
    }

    protected void LoadNestedVideo(NestedBlock nb)
    {
        var url      = nb.Url;
        var embedUrl = ResolveVideoEmbed(url);
        if (!string.IsNullOrEmpty(embedUrl))
            nb.Src = embedUrl;
    }

    protected void RemoveVideo(EditorBlock block)
    {
        block.Src = string.Empty;
        block.Url = string.Empty;
    }

    private static string ResolveVideoEmbed(string url)
    {
        // YouTube
        var yt = Regex.Match(url, @"(?:youtube\.com/watch\?v=|youtu\.be/)([^&\s]+)");
        if (yt.Success) return $"https://www.youtube.com/embed/{yt.Groups[1].Value}";

        // Vimeo
        var vm = Regex.Match(url, @"vimeo\.com/(\d+)");
        if (vm.Success) return $"https://player.vimeo.com/video/{vm.Groups[1].Value}";

        // Direct
        if (Regex.IsMatch(url, @"\.(mp4|webm|ogg)$", RegexOptions.IgnoreCase))
            return url;

        return string.Empty;
    }

    // ──────────────────────────────────────────────────────────
    // References  (mirrors getReferenceItems / renderReferencePreview)
    // ──────────────────────────────────────────────────────────

    protected List<ReferenceItem> GetReferenceItems(string type)
        => _referenceData.TryGetValue(type, out var items) ? items : [];

    // ──────────────────────────────────────────────────────────
    // Preview  (mirrors togglePreview())
    // ──────────────────────────────────────────────────────────

    protected async Task TogglePreview()
    {
        PreviewMode = !PreviewMode;
        if (PreviewMode)
        {
            SelectedBlockId = null;
            _previewRefreshVersion++;
            await RefreshPreviewAsync();
        }
    }

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
            var rootJson = JsonSerializer.Serialize(RootNode, BlockJsonContext.Default.Options);
            var result = await PreviewClient.RenderPageFragmentAsync(rootNodeJson: rootJson, ct: cancellationToken);
            switch (result)
            {
                case Result<string, AeroError>.Ok ok:
                    PreviewHtml = ok.Value;
                    break;
                case Result<string, AeroError>.Failure failure:
                    PreviewHtml = null;
                    PreviewError = failure.Error.ToString();
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

    protected void OpenTranslation(long pageId)
        => NavManager.NavigateTo($"/manager/page/editor/{pageId}?tab=translations");

    protected void OpenPublicTranslation(CmsPageDetail variant)
    {
        var baseUri = _previewBaseUri ?? NavManager.BaseUri.TrimEnd('/');
        NavManager.NavigateTo($"{baseUri.TrimEnd('/')}{variant.Path}");
    }

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

    protected Task PublishAllTranslationsAsync()
        => SetAllTranslationsPublicationStateAsync(publish: true);

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

    // ──────────────────────────────────────────────────────────
    // Save / Publish  (mirrors savePage / publishPage)
    // ──────────────────────────────────────────────────────────

    private void MarkDirty() => _pageState = PageState.Dirty;

    private async Task AutoSaveAsync()
    {
        if (_pageState != PageState.Dirty) return;

        if (Id == 0 || Id is null)
        {
            // New page: only auto-create if there's actual content
            if (RootNode.Children.Count == 0 && string.IsNullOrWhiteSpace(PageTitle))
                return;

            await SavePage();  // creates the page via API, sets Id
            return;
        }

        // Existing page: upsert draft (not PageDocument — that's for manual save/publish)
        try
        {
            var rootJson = JsonSerializer.Serialize(RootNode, BlockJsonContext.Default.Options);
            var request = new PageDraftRequest(
                PageTitle,
                PageSlug,
                Summary,
                rootJson
            );
            var result = await PagesClient.SaveDraftAsync(Id.Value, request);
            if (result is Result<bool, AeroError>.Ok)
                _pageState = PageState.Clean;
        }
        catch
        {
            // Auto-save failures are non-critical — will retry on next interval
        }
    }

    private async Task SavePage()
    {
        if (IsSaving) return;
        IsSaving = true;
        await InvokeAsync(StateHasChanged);

        // Ensure slug has a value before saving — derive from title if empty
        if (string.IsNullOrWhiteSpace(PageSlug))
        {
            PageSlug = TitleToSlug(PageTitle);
            _slugState = SlugState.Locked;
        }

        try
        {
            if (Id.HasValue)
            {
                var rootJson = JsonSerializer.Serialize(RootNode, BlockJsonContext.Default.Options);
                var request = new UpdatePageRequest(
                    PageTitle,
                    PageSlug,
                    Summary,
                    SeoTitle,
                    SeoDescription,
                    PublicationState,
                    ParentId,
                    null,
                    ShowInNavMenu,
                    ShowHeaderNavigation,
                    HideFooter,
                    ShowChatAgent,
                    rootJson
                );

                var result = await PagesClient.UpdateAsync(Id.Value, request);
                if (result is Result<CmsPageDetail, AeroError>.Ok ok)
                {
                    LoadedPage = ok.Value;
                    UpdateLastSaved();
                    _pageState = PageState.Clean;
                    await PagesClient.DeleteDraftAsync(Id.Value);  // clean up draft
                    await LoadPageTranslationsAsync();
                    ShowToast(L["Page saved successfully"], "success");
                }
                else if (result is Result<CmsPageDetail, AeroError>.Failure err)
                {
                    ShowToast(L["Error saving: {0}", err.Error], "error");
                }
            }
            else
            {
                var rootJson = JsonSerializer.Serialize(RootNode, BlockJsonContext.Default.Options);
                var request = new CreatePageRequest(
                    PageTitle,
                    PageSlug,
                    Summary,
                    SeoTitle,
                    SeoDescription,
                    PublicationState,
                    ParentId,
                    null,
                    ShowInNavMenu,
                    ShowHeaderNavigation,
                    HideFooter,
                    ShowChatAgent,
                    rootJson
                );

                var result = await PagesClient.CreateAsync(request);
                if (result is Result<CmsPageDetail, AeroError>.Ok createOk)
                {
                    Id = createOk.Value.Id;
                    LoadedPage = createOk.Value;
                    _slugState = SlugState.Locked;  // preserve generated slug going forward
                    _pageState = PageState.Clean;
                    UpdateLastSaved();
                    await LoadPageTranslationsAsync();
                    ShowToast(L["Page created successfully"], "success");
                    // Update URL without refreshing
                    // NavManager.NavigateTo($"/manager/page/editor/{Id}", false); 
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

    protected async Task PublishPage()
    {
        if (!Id.HasValue)
        {
            await SavePage();
        }

        if (Id.HasValue)
        {
            var result = await PagesClient.PublishAsync(Id.Value);
            if (result is Result<CmsPageDetail, AeroError>.Ok ok)
            {
                PublicationState = ok.Value.PublicationState;
                _pageState = PageState.Clean;
                await PagesClient.DeleteDraftAsync(Id.Value);  // clean up draft
                ShowToast(L["Page published!"], "success");
            }
            else
            {
                ShowToast(L["Failed to publish"], "error");
            }
        }
    }

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
        if (_slugState == SlugState.Auto)
            PageSlug = TitleToSlug(title);
    }

    /// <summary>Called when the user manually edits the slug. Locks it to prevent title overwrites.</summary>
    protected void OnSlugChanged(string slug)
    {
        PageSlug = slug;
        MarkDirty();
        _slugState = SlugState.Locked;
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

    // ──────────────────────────────────────────────────────────
    // IBlockEditorCallbacks explicit implementation
    // These forward to protected methods and properties used by
    // BlockEditorPreviewHost via the cascading IBlockEditorCallbacks.
    // ──────────────────────────────────────────────────────────

    bool IBlockEditorCallbacks.PreviewMode => PreviewMode;
    Dictionary<string, string> IBlockEditorCallbacks.DynamicTemplatePreviewHtml => DynamicTemplatePreviewHtml;

    void IBlockEditorCallbacks.SelectBlock(string editorId) => SelectBlock(editorId);
    void IBlockEditorCallbacks.BlockChanged(EditorBlock block)
    {
        MarkDirty();
        QueuePreviewRefresh();
    }

    protected async Task AddPaletteItemAsync(
        PageEditorCatalog.NeoEditorCatalogItem item)
    {
        if (!TryGetCustomComponentId(item.CatalogId, out var componentId))
        {
            AddBlock(item.CatalogId);
            return;
        }

        var result = await CustomComponentsClient.CreateInstanceAsync(componentId);
        if (result is Result<NeoPageNode, AeroError>.Ok ok)
        {
            EnsureCanvasHistory();
            var node = ok.Value;
            RootNode.Children.Add(node);
            RecordCanvasMutation();
            SelectBlock(node.NodeId);
            MarkDirty();
            QueuePreviewRefresh();
            ShowToast(L["Custom component added"], "success");
        }
        else if (result is Result<NeoPageNode, AeroError>.Failure failure)
        {
            ShowToast(L["Could not add custom component: {0}", failure.Error], "error");
        }
    }

    protected void CopySelectedBlock()
    {
        if (_nodeClipboard is not null)
        {
            // Already has a node clipboard — re-use
            ShowToast(L["Block copied"], "success");
            return;
        }

        var index = GetSelectedBlockIndex();
        if (index < 0 || index >= RootNode.Children.Count) return;

        var node = RootNode.Children[index];
        _nodeClipboard = EditorNodeMemento.Capture(node);
        ShowToast(L["Block copied"], "success");
    }

    protected void CutSelectedBlock()
    {
        var index = GetSelectedBlockIndex();
        if (index < 0 || index >= RootNode.Children.Count)
        {
            return;
        }

        var node = RootNode.Children[index];
        _nodeClipboard = EditorNodeMemento.Capture(node);
        RootNode.Children.RemoveAt(index);
        if (SelectedBlockId == node.NodeId)
            SelectedBlockId = null;
        MarkDirty();
        ShowToast(L["Block cut"], "success");
        QueuePreviewRefresh();
    }

    protected void PasteBlock()
    {
        if (_nodeClipboard is null)
        {
            ShowToast(L["Nothing to paste"], "info");
            return;
        }

        var pasteNode = _nodeClipboard.Restore();
        pasteNode.NodeId = Guid.NewGuid().ToString("N");
        var selectedIndex = GetSelectedBlockIndex();
        var insertIndex = selectedIndex >= 0 ? selectedIndex + 1 : RootNode.Children.Count;
        EnsureCanvasHistory();
        RootNode.Children.Insert(insertIndex, pasteNode);
        RecordCanvasMutation();
        SelectBlock(pasteNode.NodeId);
        MarkDirty();
        QueuePreviewRefresh();
        ShowToast(L["Block pasted"], "success");
    }

    [JSInvokable]
    public Task HandleEditorShortcut(string command)
    {
        switch (command)
        {
            case "undo":
                UndoComposition();
                break;
            case "redo":
                RedoComposition();
                break;
            case "copy":
                CopySelectedBlock();
                break;
            case "cut":
                CutSelectedBlock();
                break;
            case "paste":
                PasteBlock();
                break;
            case "duplicate":
                var duplicateIndex = GetSelectedBlockIndex();
                if (duplicateIndex >= 0)
                {
                    DuplicateBlock(duplicateIndex);
                }
                break;
            case "delete":
                var deleteIndex = GetSelectedBlockIndex();
                if (deleteIndex >= 0)
                {
                    DeleteBlock(deleteIndex);
                }
                break;
        }

        return InvokeAsync(StateHasChanged);
    }

    private NeoPageNode? GetSelectedBlock() =>
        string.IsNullOrWhiteSpace(SelectedBlockId)
            ? null
            : FindNodeInTree(SelectedBlockId);

    private int GetSelectedBlockIndex() =>
        string.IsNullOrWhiteSpace(SelectedBlockId)
            ? -1
            : RootNode.Children.FindIndex(node => node.NodeId == SelectedBlockId);

    void IBlockEditorCallbacks.CompositionChanged(
        EditorBlock block,
        CompositionMutation mutation)
    {
        if (!_compositionHistory.TryGetValue(block.EditorId, out var history))
        {
            history = new CompositionHistory(mutation.Before);
            _compositionHistory[block.EditorId] = history;
        }

        history.Record(mutation.After, mutation.CoalescingKey);
        block.CompositionNodes = [history.Current];
        MarkDirty();
        QueuePreviewRefresh();
    }

    void IBlockEditorCallbacks.CompositionDropRejected(string message)
    {
        CompositionDropError = message;
        ShowToast(L["Cannot place item here: {0}", message], "error");
        _ = InvokeAsync(StateHasChanged);
    }

    protected void ClearCompositionDropError()
    {
        CompositionDropError = null;
    }

    void IBlockEditorCallbacks.OpenNodeEditor(EditorBlock block, string nodeId)
    {
        SelectedBlockId = block.EditorId;
        EditingBlockId = block.EditorId;
        EditingNodeId = nodeId;
        BlockEditorTab = "content";
        BlockEditorModalOpen = true;
        _ = InvokeAsync(StateHasChanged);
    }

    protected bool CanUndoComposition =>
        CurrentCompositionHistory?.CanUndo == true ||
        _treeUndoStack.Count > 0;

    protected bool CanRedoComposition =>
        CurrentCompositionHistory?.CanRedo == true ||
        _treeRedoStack.Count > 0;

    private CompositionHistory? CurrentCompositionHistory =>
        !string.IsNullOrWhiteSpace(SelectedBlockId) &&
        _compositionHistory.TryGetValue(SelectedBlockId, out var history)
            ? history
            : null;

    protected void UndoComposition()
    {
        if (CurrentCompositionHistory?.CanUndo == true)
        {
            ApplyCompositionHistory(CurrentCompositionHistory.Undo());
            return;
        }

        if (_treeUndoStack.Count == 0) return;

        // Push current state to redo stack
        _treeRedoStack.Add(EditorNodeMemento.Capture(RootNode));

        // Restore previous state
        var previous = _treeUndoStack[^1];
        _treeUndoStack.RemoveAt(_treeUndoStack.Count - 1);
        var restored = previous.Restore();
        SyncRootNodeFrom(restored);

        _compositionHistory.Clear();
        MarkDirty();
        QueuePreviewRefresh();
    }

    protected void RedoComposition()
    {
        if (CurrentCompositionHistory?.CanRedo == true)
        {
            ApplyCompositionHistory(CurrentCompositionHistory.Redo());
            return;
        }

        if (_treeRedoStack.Count == 0) return;

        // Push current state to undo stack
        _treeUndoStack.Add(EditorNodeMemento.Capture(RootNode));
        if (_treeUndoStack.Count > MaxTreeHistory)
            _treeUndoStack.RemoveRange(0, _treeUndoStack.Count - MaxTreeHistory);

        // Restore next state
        var next = _treeRedoStack[^1];
        _treeRedoStack.RemoveAt(_treeRedoStack.Count - 1);
        var restored = next.Restore();
        SyncRootNodeFrom(restored);

        _compositionHistory.Clear();
        MarkDirty();
        QueuePreviewRefresh();
    }

    /// <summary>Replace RootNode's children with those from the restored tree.</summary>
    private void SyncRootNodeFrom(NeoPageNode restored)
    {
        RootNode.Children.Clear();
        foreach (var child in restored.Children)
            RootNode.Children.Add(child);
    }

    private void EnsureCanvasHistory()
    {
        if (_treeUndoStack.Count == 0 && _treeRedoStack.Count == 0)
        {
            _treeUndoStack.Add(EditorNodeMemento.Capture(RootNode));
        }
    }

    private void RecordCanvasMutation()
    {
        // Snapshot current state to undo stack
        _treeUndoStack.Add(EditorNodeMemento.Capture(RootNode));
        if (_treeUndoStack.Count > MaxTreeHistory)
            _treeUndoStack.RemoveRange(0, _treeUndoStack.Count - MaxTreeHistory);
        _treeRedoStack.Clear();
    }

    private void ApplyCanvasHistory(List<EditorBlock>? blocks)
    {
        // Legacy canvas history — no-op; tree history handles undo/redo
    }

    private void ApplyCompositionHistory(NeoPageNode? root)
    {
        if (root is null || string.IsNullOrWhiteSpace(SelectedBlockId))
        {
            return;
        }

        var node = FindNodeInTree(SelectedBlockId);
        if (node is null)
        {
            return;
        }

        // Replace the node's children with the root's children
        node.Children.Clear();
        foreach (var child in root.Children)
            node.Children.Add(child);
        node.Properties = root.Properties;
        node.Style = root.Style;
        MarkDirty();
        QueuePreviewRefresh();
    }

    void IBlockEditorCallbacks.OpenBlockEditor(EditorBlock block) => OpenBlockEditor(block.EditorId);

    void IBlockEditorCallbacks.OpenMediaSelector(EditorBlock block, bool multiSelect, string field)
        => OpenMediaSelector(block, multiSelect, field);

    void IBlockEditorCallbacks.OpenNodeMediaSelector(
        EditorBlock block,
        string nodeId,
        string field,
        EditorBreakpoint breakpoint) =>
        OpenNodeMediaSelector(block, nodeId, field, breakpoint);
    void IBlockEditorCallbacks.OpenAudioSelector(EditorBlock block) => OpenAudioSelector(block);
    void IBlockEditorCallbacks.RemoveImage(EditorBlock block) => RemoveImage(block);
    void IBlockEditorCallbacks.RemoveVideo(EditorBlock block) => RemoveVideo(block);
    void IBlockEditorCallbacks.LoadVideo(EditorBlock block) => LoadVideo(block);
    Task IBlockEditorCallbacks.RefreshDynamicTemplatePreviewAsync(EditorBlock block)
        => RefreshDynamicTemplatePreviewAsync(block);
    void IBlockEditorCallbacks.LoadNestedVideo(NestedBlock nb) => LoadNestedVideo(nb);
    void IBlockEditorCallbacks.OpenMediaSelectorForNested(EditorBlock parent, int colIndex, NestedBlock nb)
        => OpenMediaSelectorForNested(parent, colIndex, nb);
    List<ReferenceItem> IBlockEditorCallbacks.GetReferenceItems(string type) => GetReferenceItems(type);

    string IBlockEditorCallbacks.RenderDynamicTemplateIfCached(EditorBlock block)
    {
        return DynamicTemplatePreviewHtml.TryGetValue(block.EditorId, out var html)
            ? html
            : string.Empty;
    }

    /// <summary>Toggle sidebar panels (from empty-state click).</summary>
    protected void ToggleSidebarPanels() => RightSidebarCollapsed = false;

    // ──────────────────────────────────────────────────────────
    // Version History  (event sourcing — mt_events timeline)
    // ──────────────────────────────────────────────────────────

    private PageVersionHistory? _historyPanel;

    private async Task ShowHistoryAsync()
    {
        if (_historyPanel is not null && Id.HasValue)
        {
            await _historyPanel.OpenAsync();
        }
    }
}

