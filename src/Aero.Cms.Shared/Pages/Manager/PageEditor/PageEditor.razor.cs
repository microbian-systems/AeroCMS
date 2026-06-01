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
using PageEditorCatalog = Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

public partial class PageEditor : ComponentBase, IDisposable, IBlockEditorCallbacks
{
    // ──────────────────────────────────────────────────────────
    // Parameters
    // ──────────────────────────────────────────────────────────

    /// <summary>Optional ID of an existing page to edit.</summary>
    [Parameter] public long? Id { get; set; }

    [Inject] protected IDocsHttpClient DocsClient { get; set; } = default!;
    [Inject] protected IPagesHttpClient PagesClient { get; set; } = default!;
    [Inject] protected IMediaHttpClient MediaClient { get; set; } = default!;
    [Inject] protected IBlogHttpClient BlogClient { get; set; } = default!;
    [Inject] protected ICategoriesHttpClient CategoriesClient { get; set; } = default!;
    [Inject] protected ITagsHttpClient TagsClient { get; set; } = default!;
    [Inject] protected IUsersHttpClient UsersClient { get; set; } = default!;
    [Inject] protected IPreviewHttpClient PreviewClient { get; set; } = default!;
    [Inject] protected ISitesHttpClient SitesClient { get; set; } = default!;
    [Inject] protected ICurrentSiteAccessor CurrentSiteAccessor { get; set; } = default!;
    [Inject] protected AdminStateContainer AdminState { get; set; } = default!;
    [Inject] protected NavigationManager NavManager { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected IHtmlSanitizer HtmlSanitizer { get; set; } = default!;
    [Inject] protected Catalog.INeoEditorCatalogProvider Catalog { get; set; } = default!;
    [Inject] protected IEnumerable<IPageEditorBlockProvider> PageEditorBlockProviders { get; set; } = [];
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    // ──────────────────────────────────────────────────────────
    // State  (mirrors Alpine.js cmsEditor() properties)
    // ──────────────────────────────────────────────────────────

    protected string PageTitle    { get; set; } = "Homepage";
    protected string LastSaved    { get; set; } = "Never";
    protected string Author       { get; set; } = "Admin";

    // Block list
    protected List<EditorBlock> Blocks { get; set; } = [];

    // Selection / drag state
    protected string? SelectedBlockId  { get; set; }
    protected string? DraggedType      { get; set; }
    protected int?    DraggedIndex     { get; set; }

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
    protected bool CategoryAeroUi    { get; set; } = true;
    protected bool CategoryLegacyUi  { get; set; } = true;
    protected bool CategoryAeroUx    { get; set; } = true;
    protected bool CategoryMedia     { get; set; } = true;
    protected bool CategoryReferences { get; set; }
    protected bool CategorySettings   { get; set; } = true;
    protected bool CategoryHyper      { get; set; } = true;
    protected bool CategoryNeo        { get; set; } = true;

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

    /// <summary>Read-only parent path prefix shown as a pill before the slug input.</summary>
    protected string ParentSlugPrefix { get; set; } = "";

    protected CmsPageDetail? LoadedPage { get; set; }
    protected SiteViewModel? CurrentSite { get; set; }
    protected IReadOnlyList<CmsPageDetail> PageCultureVariants { get; set; } = [];
    protected string SelectedTranslationCulture { get; set; } = string.Empty;
    protected string TranslationSlug { get; set; } = string.Empty;
    protected bool IsLoadingTranslations { get; set; }
    protected bool IsCreatingTranslation { get; set; }
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

    // Block edit modal
    protected bool BlockEditorModalOpen { get; set; }
    protected string? EditingBlockId { get; set; }
    protected EditorBlock? CurrentEditBlock =>
        string.IsNullOrEmpty(EditingBlockId)
            ? null
            : Blocks.FirstOrDefault(block => block.EditorId == EditingBlockId);

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

    /// <summary>Tracks whether unsaved changes exist. Auto-save only fires when Dirty.</summary>
    private enum PageState { Clean, Dirty }
    private PageState _pageState = PageState.Dirty;  // new pages start dirty

    // ──────────────────────────────────────────────────────────
    // Lifecycle  (mirrors Alpine.js init())
    // ──────────────────────────────────────────────────────────

    private long? _previousParentId;

    protected override async Task OnParametersSetAsync()
    {
        if (_previousParentId != ParentId)
        {
            _previousParentId = ParentId;
            await RefreshParentSlugPrefixAsync();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        PageEditorBlockRegistry.RegisterProviders(PageEditorBlockProviders);

        await ResolvePreviewBaseUriAsync();
        CurrentSite = await ResolveCurrentSiteAsync();

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
            
            // Load blocks if available in API
            if (page.Blocks != null)
            {
                Blocks = page.Blocks.ToList();
            }

            // Check for a newer draft — if one exists, use it as the in-progress state
            var draftResult = await PagesClient.GetDraftAsync(id);
            if (draftResult is Result<PageDraftSummary?, AeroError>.Ok { Value: not null } draftOk)
            {
                var draft = draftOk.Value;
                PageTitle = draft.Title;
                PageSlug = draft.Slug;
                Summary = draft.Summary ?? string.Empty;
                if (draft.Blocks is not null)
                    Blocks = draft.Blocks.ToList();
            }

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

    public void Dispose()
    {
        _autoSaveTimer?.Dispose();
        _previewDebounceCts?.Cancel();
        _previewDebounceCts?.Dispose();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JSRuntime.InvokeVoidAsync("PeNavTooltip.refresh");
    }

    // ──────────────────────────────────────────────────────────
    // Category toggle  (mirrors toggleCategory())
    // ──────────────────────────────────────────────────────────

    protected void ToggleCategory(string category)
    {
        switch (category)
        {
            case "aeroui":    CategoryAeroUi    = !CategoryAeroUi;    break;
            case "legacyui":  CategoryLegacyUi  = !CategoryLegacyUi;  break;
            case "aeroux":    CategoryAeroUx    = !CategoryAeroUx;    break;
            case "media":     CategoryMedia     = !CategoryMedia;     break;
            case "references": CategoryReferences = !CategoryReferences; break;
            case "settings":   CategorySettings   = !CategorySettings;   break;
            case "hyper":      CategoryHyper      = !CategoryHyper;      break;
            case "neo":        CategoryNeo        = !CategoryNeo;        break;
        }
    }

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> NeoAeroCatalogItems =>
        Catalog.GetCatalogItems()
            .Where(i => i.Section == PageEditorCatalog.NeoEditorCatalogSection.AeroUi)
            .OrderBy(i => i.SortOrder)
            .ToList();

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> LegacyUiCatalogItems { get; } =
    [
        CatalogItem("boring_hero", "Boring Hero", 10),
        CatalogItem("hero", "Hero", 20),
        CatalogItem("text", "Text", 30),
        CatalogItem("columns", "Columns", 40),
        CatalogItem("content", "Rich Text", 50),
        CatalogItem("markdown", "Markdown", 60),
        CatalogItem("raw_html", "Raw HTML", 70),
        CatalogItem("dynamic_template", "Scriban", 80),
        CatalogItem("quote", "Quote", 90),
        CatalogItem("separator", "Separator", 100)
    ];

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> AeroUxCatalogItems { get; } =
    [
        CatalogItem("aero_hero", "Aero Hero", 10),
        CatalogItem("aero_features", "Features", 20),
        CatalogItem("aero_cta", "CTA", 30),
        CatalogItem("aero_blog", "Blog", 40),
        CatalogItem("aero_pricing", "Pricing", 50),
        CatalogItem("aero_teams", "Teams", 60),
        CatalogItem("aero_testimonials", "Testimonials", 70),
        CatalogItem("aero_faq", "FAQ", 80),
        CatalogItem("aero_portfolio", "Portfolio", 90),
        CatalogItem("aero_contact", "Contact", 100),
        CatalogItem("aero_table", "Table", 110),
        CatalogItem("aero_auth", "Auth", 120)
    ];

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> MediaCatalogItems { get; } =
    [
        CatalogItem("image", "Image", 10),
        CatalogItem("video", "Video", 20),
        CatalogItem("gallery", "Gallery", 30),
        CatalogItem("carousel", "Carousel", 40),
        CatalogItem("audio", "Audio", 50)
    ];

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
            .Concat(PageEditorBlockRegistry.All.Select(ToCatalogItem))
            .Where(i => i.Section == PageEditorCatalog.NeoEditorCatalogSection.Hyper)
            .GroupBy(i => i.CatalogId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(i => i.SortOrder)
            .ToList();

    protected IReadOnlyList<PageEditorCatalog.NeoEditorCatalogItem> NeoNeoCatalogItems =>
        PageEditorBlockRegistry.All
            .Select(ToCatalogItem)
            .Where(i => i.Section == PageEditorCatalog.NeoEditorCatalogSection.Neo)
            .OrderBy(i => i.SortOrder)
            .ToList();

    private static PageEditorCatalog.NeoEditorCatalogItem ToCatalogItem(IPageEditorBlockDefinition definition) =>
        new()
        {
            CatalogId = definition.CatalogId,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            Section = ToCatalogSection(definition.Category),
            Kind = ToCatalogKind(definition.Kind),
            SortOrder = definition.SortOrder,
            IconName = definition.IconName,
            PublicStaticSsrSafe = definition.PublicStaticSsrSafe,
            EditorPreviewComponentType = definition.PreviewComponentType,
            PropertyEditorComponentType = definition.PropertyEditorComponentType
        };

    private static PageEditorCatalog.NeoEditorCatalogSection ToCatalogSection(string? category) =>
        category?.Trim().ToLowerInvariant() switch
        {
            "aero ui" or "aeroui" or "aero" => PageEditorCatalog.NeoEditorCatalogSection.AeroUi,
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
    // Block management  (mirrors addBlock / deleteBlock / etc.)
    // ──────────────────────────────────────────────────────────

    protected void AddBlock(string type)
    {
        var block = CreateBlock(type);
        Blocks.Add(block);
        SelectBlock(block.EditorId);
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
        BlockEditorModalOpen = true;
    }

    private void OpenBlockEditor(EditorBlock block)
    {
        OpenBlockEditor(block.EditorId);
    }

    protected void CloseBlockEditor()
    {
        BlockEditorModalOpen = false;
        EditingBlockId = null;
    }

    protected string GetBlockDisplayName(EditorBlock block)
    {
        var allItems = NeoAeroCatalogItems
            .Concat(NeoHyperCatalogItems)
            .Concat(LegacyUiCatalogItems)
            .Concat(AeroUxCatalogItems)
            .Concat(MediaCatalogItems)
            .Concat(ReferenceCatalogItems);

        return allItems.FirstOrDefault(item => item.CatalogId == block.Type)?.DisplayName
            ?? block.Type;
    }

    private EditorBlock CreateBlock(string type)
    {
        if (PageEditorBlockRegistry.TryGet(type, out var definition))
        {
            return definition.CreateDefaultEditorBlock();
        }

        var block = new EditorBlock { Type = type };

        switch (type)
        {
            // Neo catalog blocks
            case "aero.hero.01":
                block.Eyebrow         = "Introducing NeoUI v3";
                block.MainText        = "Build beautiful Blazor apps";
                block.Highlight       = "faster than ever";
                block.SubText         = "100+ production-ready components for .NET Blazor. Accessible, customizable, and built for speed.";
                block.CtaText         = "Get started for free";
                block.CtaUrl          = "#";
                block.CtaText2        = "View on GitHub";
                block.CtaUrl2         = "#";
                block.TrustMarkers    =
                [
                    "Free & open source",
                    ".NET 8+ compatible",
                    "Dark mode included",
                    "100+ components"
                ];
                block.BackgroundImage = string.Empty;
                break;
            case "aero.hero.basic":
                block.MainText        = "Welcome";
                block.SubText         = "Your message goes here.";
                block.CtaText         = "";
                block.CtaUrl          = "";
                block.BackgroundImage = string.Empty;
                block.FullWidth       = true;
                break;
            case "boring_hero":
                block.MainText = "Welcome";
                block.SubText = "Your message goes here.";
                block.FullWidth = true;
                break;
            case "hero":
                block.MainText = "Main headline";
                block.SubText = "Sub headline or description";
                block.CtaText = "Learn more";
                block.CtaUrl = "#";
                block.Height = 512;
                break;
            case "text":
                block.Content = "Enter your text here...";
                break;
            case "content":
                block.Content = "<p>Start writing...</p>";
                break;
            case "markdown":
                block.Content = "# Markdown content";
                block.MarkdownView = "edit";
                break;
            case "raw_html":
            case "ui.raw-html":
                block.Content = "<p>Custom HTML</p>";
                break;
            case "dynamic_template":
            case "neo.template.scriban":
                block.ScribanTemplate = "{{ title }}";
                block.ScribanDataJson = "{ \"title\": \"Hello\" }";
                break;
            case "quote":
                block.Content = "Enter quote text...";
                block.Author = "Author name";
                break;
            case "columns":
            case "neo.layout.columns":
                block.ColumnCount = 2;
                block.Gap = 16;
                block.EditorColumns =
                [
                    new EditorColumn(),
                    new EditorColumn()
                ];
                break;
            case "aero_hero":
                block.MainText = "Aero Hero";
                block.SubText = "Build a polished page section.";
                block.CtaText = "Get started";
                block.CtaUrl = "#";
                break;
            case "aero_features":
                block.MainText = "Features";
                block.SubText = "Everything you need.";
                block.FeatureItems =
                [
                    new AeroFeatureItem { Title = "Fast", Description = "Ship quickly.", Icon = "zap" },
                    new AeroFeatureItem { Title = "Flexible", Description = "Compose freely.", Icon = "layout" }
                ];
                break;
            case "aero_cta":
                block.MainText = "Ready to start?";
                block.SubText = "Take the next step.";
                block.CtaText = "Contact us";
                block.CtaUrl = "#";
                break;
            case "aero_blog":
                block.MainText = "Latest posts";
                block.Description = "Recent articles and updates.";
                break;
            case "aero_pricing":
                block.MainText = "Pricing";
                block.Description = "Simple plans for every team.";
                break;
            case "aero_teams":
                block.MainText = "Our team";
                block.Description = "Meet the people behind the work.";
                break;
            case "aero_testimonials":
                block.MainText = "Testimonials";
                block.Description = "What customers are saying.";
                break;
            case "aero_faq":
                block.MainText = "FAQ";
                block.Description = "Common questions.";
                block.FaqItems = [new AeroFaqItem { Question = "Question?", Answer = "Answer." }];
                break;
            case "aero_portfolio":
                block.MainText = "Portfolio";
                block.Description = "Selected work.";
                break;
            case "aero_contact":
                block.MainText = "Contact";
                block.Description = "Get in touch.";
                break;
            case "aero_table":
                block.MainText = "Table";
                block.Description = "Structured information.";
                break;
            case "aero_auth":
                block.MainText = "Sign in";
                block.Description = "Access your account.";
                block.CtaText = "Continue";
                break;
            default:
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

        if (PageEditorBlockRegistry.TryGet(block.Type, out var definition))
        {
            return definition.ToBlockBase(block);
        }

        var node = MapEditorBlockToNeoNode(block);
        return block.Type switch
        {
            "aero.hero.basic" => BasicHeroBlockMapper.FromNode(node),
            "media.image" => ImageBlockMapper.FromNode(node),
            "media.video" => VideoBlockMapper.FromNode(node),
            "media.audio" => AudioBlockMapper.FromNode(node),
            "media.gallery" => GalleryBlockMapper.FromNode(node),
            "ui.raw-html" => NeoRawHtmlBlockMapper.FromNode(node),
            "ui.separator" => SeparatorBlockMapper.FromNode(node),
            "neo.layout.columns" => NeoColumnsBlockMapper.FromNode(node),
            _ => null
        };
    }

    private static NeoPageNode MapEditorBlockToNeoNode(EditorBlock block)
    {
        if (PageEditorBlockRegistry.TryGet(block.Type, out var definition))
        {
            return definition.ToNeoPageNode(block);
        }

        return block.Type switch
        {
        "aero.hero.01" => new NeoPageNode
        {
            CatalogId = "aero.hero.01", Kind = NeoPageNodeKind.Block,
            Properties = new Dictionary<string, JsonElement>
            {
                ["eyebrow"] = System.Text.Json.JsonSerializer.SerializeToElement(block.Eyebrow),
                ["title"] = System.Text.Json.JsonSerializer.SerializeToElement(block.MainText),
                ["highlight"] = System.Text.Json.JsonSerializer.SerializeToElement(block.Highlight),
                ["description"] = System.Text.Json.JsonSerializer.SerializeToElement(block.SubText),
                ["primaryText"] = System.Text.Json.JsonSerializer.SerializeToElement(block.CtaText),
                ["primaryUrl"] = System.Text.Json.JsonSerializer.SerializeToElement(block.CtaUrl),
                ["secondaryText"] = System.Text.Json.JsonSerializer.SerializeToElement(block.CtaText2),
                ["secondaryUrl"] = System.Text.Json.JsonSerializer.SerializeToElement(block.CtaUrl2),
                ["trustMarkers"] = System.Text.Json.JsonSerializer.SerializeToElement(block.TrustMarkers)
            }
        },
        "aero.hero.basic" => new NeoPageNode
        {
            CatalogId = "aero.hero.basic", Kind = NeoPageNodeKind.Block,
            Properties = new Dictionary<string, JsonElement>
            {
                ["title"] = System.Text.Json.JsonSerializer.SerializeToElement(block.MainText),
                ["subtitle"] = System.Text.Json.JsonSerializer.SerializeToElement(block.SubText),
                ["backgroundImageUrl"] = System.Text.Json.JsonSerializer.SerializeToElement(block.BackgroundImage),
                ["ctaText"] = System.Text.Json.JsonSerializer.SerializeToElement(block.CtaText),
                ["ctaUrl"] = System.Text.Json.JsonSerializer.SerializeToElement(block.CtaUrl)
            }
        },
        "media.image" => new NeoPageNode
        {
            CatalogId = "media.image", Kind = NeoPageNodeKind.Block,
            Properties = new Dictionary<string, JsonElement>
            {
                ["src"] = System.Text.Json.JsonSerializer.SerializeToElement(block.Src),
                ["alt"] = System.Text.Json.JsonSerializer.SerializeToElement(block.Alt ?? string.Empty),
                ["caption"] = System.Text.Json.JsonSerializer.SerializeToElement(block.Caption ?? string.Empty)
            }
        },
        "media.video" => new NeoPageNode
        {
            CatalogId = "media.video", Kind = NeoPageNodeKind.Block,
            Properties = new Dictionary<string, JsonElement>
            {
                ["src"] = System.Text.Json.JsonSerializer.SerializeToElement(block.Src),
                ["autoplay"] = System.Text.Json.JsonSerializer.SerializeToElement(block.AutoPlay),
                ["controls"] = System.Text.Json.JsonSerializer.SerializeToElement(true)
            }
        },
        "media.audio" => new NeoPageNode
        {
            CatalogId = "media.audio", Kind = NeoPageNodeKind.Block,
            Properties = new Dictionary<string, JsonElement>
            {
                ["src"] = System.Text.Json.JsonSerializer.SerializeToElement(block.Src),
                ["controls"] = System.Text.Json.JsonSerializer.SerializeToElement(true)
            }
        },
        "media.gallery" => new NeoPageNode
        {
            CatalogId = "media.gallery", Kind = NeoPageNodeKind.Block,
            Properties = new Dictionary<string, JsonElement>
            {
                ["images"] = System.Text.Json.JsonSerializer.SerializeToElement(block.GalleryImages.Select(g => g.Src).ToList()),
                ["columns"] = System.Text.Json.JsonSerializer.SerializeToElement(3)
            }
        },
        "ui.raw-html" => new NeoPageNode
        {
            CatalogId = "ui.raw-html", Kind = NeoPageNodeKind.Block,
            Properties = new Dictionary<string, JsonElement>
            {
                ["html"] = System.Text.Json.JsonSerializer.SerializeToElement(block.Content)
            }
        },
        "ui.separator" => new NeoPageNode
        {
            CatalogId = "ui.separator", Kind = NeoPageNodeKind.Block,
            Properties = []
        },
        _ => new NeoPageNode { CatalogId = block.Type, Kind = NeoPageNodeKind.Block, Properties = [] }
        };
    }

    protected void SelectBlock(string id) => SelectedBlockId = id;

    protected void DeleteBlock(int index)
    {
        Blocks.RemoveAt(index);
        SelectedBlockId = null;
        MarkDirty();
        ShowToast(L["Block deleted"]);
        QueuePreviewRefresh();
    }

    protected void DuplicateBlock(int index)
    {
        var original = Blocks[index];
        var copy     = original.DeepClone();
        copy.EditorId = Guid.NewGuid().ToString();

        // Regenerate column IDs
        foreach (var col in copy.EditorColumns)
            col.ColId = Guid.NewGuid().ToString();

        Blocks.Insert(index + 1, copy);
        MarkDirty();
        ShowToast(L["Block duplicated"], "success");
        QueuePreviewRefresh();
    }

    protected void MoveBlockUp(int index)
    {
        if (index <= 0) return;
        (Blocks[index], Blocks[index - 1]) = (Blocks[index - 1], Blocks[index]);
        MarkDirty();
        QueuePreviewRefresh();
    }

    protected void MoveBlockDown(int index)
    {
        if (index >= Blocks.Count - 1) return;
        (Blocks[index], Blocks[index + 1]) = (Blocks[index + 1], Blocks[index]);
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
    }

    /// <summary>Handle canvas reorder from Sortable.</summary>
    protected void OnCanvasReordered(IList<EditorBlock> reordered)
    {
        Blocks = reordered.ToList();
        MarkDirty();
        QueuePreviewRefresh();
    }

    /// <summary>Handle catalog item dropped onto canvas from Sortable palette.</summary>
    protected void OnCatalogItemTransferred(SortableTransferArgs args)
    {
        var catalogId = args.ActiveId;
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

        if (MediaContext == "background" && CurrentMediaBlock != null)
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
            var result = await PreviewClient.RenderPageFragmentAsync(blocks: Blocks, ct: cancellationToken);
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

    protected void OpenTranslation(long pageId)
        => NavManager.NavigateTo($"/manager/page/editor/{pageId}");

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
            if (Blocks.Count == 0 && string.IsNullOrWhiteSpace(PageTitle))
                return;

            await SavePage();  // creates the page via API, sets Id
            return;
        }

        // Existing page: upsert draft (not PageDocument — that's for manual save/publish)
        try
        {
            var request = new PageDraftRequest(
                PageTitle,
                PageSlug,
                Summary,
                Blocks.ToList()
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
                var request = new UpdatePageRequest(
                    PageTitle,
                    PageSlug,
                    Summary,
                    SeoTitle,
                    SeoDescription,
                    PublicationState,
                    ParentId,
                    null, // LayoutRegions are mapped on backend from EditorBlocks
                    ShowInNavMenu,
                    ShowHeaderNavigation,
                    HideFooter,
                    ShowChatAgent,
                    Blocks
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
                    Blocks
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

    void IBlockEditorCallbacks.OpenBlockEditor(EditorBlock block) => OpenBlockEditor(block);

    void IBlockEditorCallbacks.OpenMediaSelector(EditorBlock block, bool multiSelect, string field)
        => OpenMediaSelector(block, multiSelect, field);
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

