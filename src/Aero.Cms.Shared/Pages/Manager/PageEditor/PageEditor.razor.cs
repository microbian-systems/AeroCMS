using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using System;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Aero.Core;
using Aero.Cms.Core;
using Aero.Core.Security;
using Aero.Cms.Abstractions.Http.Clients;

using Aero.Core.Railway;
using CmsPageDetail = Aero.Cms.Abstractions.Http.Clients.PageDetail;
using Aero.Cms.Abstractions.Enums;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

public partial class PageEditor : ComponentBase, IDisposable
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
    [Inject] protected NavigationManager NavManager { get; set; } = default!;
    [Inject] protected IHtmlSanitizer HtmlSanitizer { get; set; } = default!;

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
    protected string? DraggedBlockId   { get; set; }
    protected string? DraggedType      { get; set; }
    protected int?    DraggedIndex     { get; set; }
    protected int     DragOverIndex    { get; set; } = -1;

    // UI state
    protected bool   SidebarCollapsed { get; set; }
    protected bool   PreviewMode      { get; set; }
    protected string PreviewDevice    { get; set; } = "desktop";
    protected bool   IsPreviewRendering { get; set; }
    protected string? PreviewHtml { get; set; }
    protected string? PreviewError { get; set; }
    protected string PreviewFragmentUrl => BuildAbsoluteUrl("api/v1/admin/preview/pages/render-fragment");
    protected string? PreviewFrameUrl => Id is { } id
        ? BuildAbsoluteUrl($"api/v1/admin/pages/drafts/{id}?previewVersion={_previewRefreshVersion}")
        : null;
    protected string PreviewFrameDocument => BuildPreviewFrameDocument(PreviewHtml, NavManager.BaseUri);
    protected bool   RightSidebarCollapsed { get; set; } = true;
    protected bool   IsSaving              { get; set; }
    protected string ActiveTab             { get; set; } = "editor";

    // Sidebar category toggles
    protected bool CategoryContent    { get; set; } = true;
    protected bool CategoryMedia      { get; set; } = true;
    protected bool CategoryReferences { get; set; } = true;
    protected bool CategorySettings   { get; set; } = true;
    protected bool CategoryAero       { get; set; } = true;

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

    protected CmsPageDetail? LoadedPage { get; set; }

    protected IReadOnlyList<DocsSummary>? DocsCategories { get; set; }

    // Media modal
    protected bool         MediaModalOpen   { get; set; }
    protected EditorBlock? CurrentMediaBlock { get; set; }
    protected bool         IsGalleryMode    { get; set; }
    protected string?      MediaContext     { get; set; }   // "background" | "nested"
    protected NestedBlock? NestedMediaTarget { get; set; }

    private Dictionary<string, List<ReferenceItem>> _referenceData = new();
    protected Dictionary<string, string> DynamicTemplatePreviewHtml { get; } = new();

    // Toasts
    protected List<ToastMessage> Toasts { get; set; } = [];

    // Auto-save timer & dirty tracking
    private const int PreviewDebounceMilliseconds = 300;
    private System.Timers.Timer? _autoSaveTimer;
    private CancellationTokenSource? _previewDebounceCts;
    private long _previewRefreshVersion;	

    /// <summary>Tracks whether unsaved changes exist. Auto-save only fires when Dirty.</summary>
    private enum PageState { Clean, Dirty }
    private PageState _pageState = PageState.Dirty;  // new pages start dirty

    // ──────────────────────────────────────────────────────────
    // Lifecycle  (mirrors Alpine.js init())
    // ──────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        if (Id.HasValue)
        {
            await LoadPageAsync(Id.Value);
        }
        else
        {
            UpdateLastSaved();
        }

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
            await InvokeAsync(StateHasChanged);
        }
        else
        {
            ShowToast("Error loading page", "error");
        }
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

    // ──────────────────────────────────────────────────────────
    // Category toggle  (mirrors toggleCategory())
    // ──────────────────────────────────────────────────────────

    protected void ToggleCategory(string category)
    {
        switch (category)
        {
            case "content":    CategoryContent    = !CategoryContent;    break;
            case "media":      CategoryMedia      = !CategoryMedia;      break;
            case "references": CategoryReferences = !CategoryReferences; break;
            case "settings":   CategorySettings   = !CategorySettings;   break;
            case "aero":       CategoryAero       = !CategoryAero;       break;
        }
    }

    // ──────────────────────────────────────────────────────────
    // Block management  (mirrors addBlock / deleteBlock / etc.)
    // ──────────────────────────────────────────────────────────

    protected void AddBlock(string type)
    {
        var block = CreateBlock(type);
        Blocks.Add(block);
        SelectBlock(block.EditorId);
        MarkDirty();
        ShowToast("Block added", "success");
        QueuePreviewRefresh();
    }

    private EditorBlock CreateBlock(string type)
    {
        var block = new EditorBlock { Type = type };

        switch (type)
        {
            case "boring_hero":
                block.MainText        = "Page Title";
                block.SubText         = "A simple full-width page intro.";
                block.BackgroundImage = string.Empty;
                block.FullWidth       = true;
                break;
            case "hero":
                block.MainText = string.Empty;
                block.SubText  = string.Empty;
                block.CtaText  = string.Empty;
                block.CtaUrl   = string.Empty;
                block.BackgroundImage = string.Empty;
                block.Height = 512;
                block.FullScreen = false;
                break;
            case "aero_hero":
                block.MainText        = "Building Your Next Idea";
                block.SubText         = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore.";
                block.CtaText         = "Get Started";
                block.CtaUrl          = "#";
                block.CtaText2        = "Learn More";
                block.CtaUrl2         = "#";
                block.AeroLayout      = "SideImage";
                block.Button1Style    = "Primary";
                block.Button2Style    = "Secondary";
                block.BackgroundImage = "https://images.unsplash.com/photo-1556761175-5973dc0f32e7?w=800";
                break;
            case "aero_features":
                block.MainText        = "Everything you need to build";
                block.SubText         = "Focus on your business and let us handle the technical complexities.";
                block.AeroLayout      = "Simple";
                block.FeatureItems    = new List<AeroFeatureItem>
                {
                    new() { Title = "Fast & Reliable", Description = "Built for performance.", Icon = "M13 10V3L4 14h7v7l9-11h-7z" },
                    new() { Title = "Modular Design", Description = "Customizable UI.", Icon = "M19 11H5m14 0V9a2-2 0 00-2-2M5 11V9a2 2 0 012-2" }
                };
                break;
            case "aero_cta":
                block.MainText    = "Build Your New Idea";
                block.Description = "Lorem, ipsum dolor sit amet consectetur adipisicing elit. Quidem modi reprehenderit vitae exercitationem aliquid dolores ullam temporibus enim expedita aperiam.";
                block.CtaText     = "Start Now";
                block.CtaUrl      = "#";
                block.AeroLayout  = "Card";
                break;
            case "aero_blog":
                block.SectionTitle = "From the blog";
                block.Description  = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Iure veritatis sint autem nesciunt.";
                block.BlogPosts    = new List<AeroBlogItem>
                {
                    new() { Title = "All the features you want to know", Description = "Lorem ipsum dolor sit amet...", PublishedAt = "21 Oct 2025", Category = "Product", ImageUrl = "https://images.unsplash.com/photo-1644018335954-ab54c83e007f?w=800" },
                    new() { Title = "Sticky note for problem solving", Description = "Lorem ipsum dolor sit amet...", PublishedAt = "20 Oct 2025", Category = "Design", ImageUrl = "https://images.unsplash.com/photo-1497032628192-86f99bcd76bc?w=800" }
                };
                break;
            case "aero_pricing":
                block.PageTitle       = "Pricing Plans";
                block.PageDescription = "Choose the plan that's right for you.";
                block.PricingPlans    = new List<AeroPricingPlan>
                {
                    new() { Name = "Free", Price = "$0", Period = "mo", Description = "Essential features", Features = ["Basic Analytics", "1 Project"], CtaText = "Free trial", CtaUrl = "#" },
                    new() { Name = "Pro", Price = "$29", Period = "mo", Description = "For growing teams", Features = ["Advanced Analytics", "10 Projects", "24/7 Support"], CtaText = "Get Pro", CtaUrl = "#", IsPopular = true }
                };
                break;
            case "aero_teams":
                block.SectionTitle = "Our Executive Team";
                block.Description  = "Lorem ipsum dolor sit amet consectetur adipisicing elit.";
                block.TeamMembers  = new List<AeroTeamMember>
                {
                    new() { Name = "Arthur Melo", Role = "Design Director", AvatarUrl = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=400" },
                    new() { Name = "Alice Williams", Role = "Senior Developer", AvatarUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=400" }
                };
                break;
            case "aero_testimonials":
                block.SectionTitle = "What our clients say";
                block.Description  = "Lorem ipsum dolor sit amet consectetur adipisicing elit.";
                block.Testimonials  = new List<AeroTestimonialItem>
                {
                    new() { AuthorName = "John Doe", AuthorRole = "CEO", CompanyName = "Tech Corp", Content = "Excellent service and results." },
                    new() { AuthorName = "Jane Smith", AuthorRole = "Product Manager", CompanyName = "Scale Up", Content = "Aero CMS transformed our workflow." }
                };
                break;
            case "aero_faq":
                block.Title = "Frequently Asked Questions";
                block.Description = "Everything you need to know about the product and billing.";
                block.FaqItems = new List<AeroFaqItem>
                {
                    new() { Question = "What is Aero CMS?", Answer = "Aero CMS is a modern, block-based content management system built with .NET." },
                    new() { Question = "How do I get started?", Answer = "Simply drag and drop blocks from the sidebar to compose your page." }
                };
                break;
            case "aero_portfolio":
                block.Title = "Our Recent Work";
                block.Description = "Explore some of the projects we've completed for our valued clients.";
                block.PortfolioItems = new List<AeroPortfolioItem>
                {
                    new() { ProjectTitle = "Project One", ProjectDescription = "A brief description of this amazing project.", ProjectImageUrl = "https://images.unsplash.com/photo-1498050108023-c5249f4df085?w=800", ProjectCategory = "Web Design" },
                    new() { ProjectTitle = "Project Two", ProjectDescription = "Another great project with a different focus.", ProjectImageUrl = "https://images.unsplash.com/photo-1461749280684-dccba630e2f6?w=800", ProjectCategory = "Development" }
                };
                break;
            case "aero_contact":
                block.Title = "Get in Touch";
                block.Description = "Our friendly team is always here to chat.";
                block.ContactDetails = new List<AeroContactDetail>
                {
                    new() { Label = "Email", Value = "hello@aerocms.com", Icon = "M22 6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2m20 0v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6m20 0l-10 7L2 6" },
                    new() { Label = "Phone", Value = "+1 (555) 000-0000", Icon = "M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z" }
                };
                break;
            case "aero_table":
                block.Title = "Resource List";
                block.Description = "A summary of available resources and their status.";
                block.TableHeaders = new List<AeroTableHeader> { new() { Label = "Name" }, new() { Label = "Status" }, new() { Label = "Date" } };
                block.TableRows = new List<AeroTableRow>
                {
                    new() { Cells = new List<string> { "Resource A", "Active", "2025-01-01" } },
                    new() { Cells = new List<string> { "Resource B", "Pending", "2025-01-15" } }
                };
                break;
            case "aero_auth":
                block.Title = "Sign in to your account";
                block.CtaText = "Sign in";
                break;
            case "raw_html":
                block.Content = "<!-- Custom HTML -->\n<div class=\"p-4 bg-gray-100\">Hello World</div>";
                block.MarkdownView = "edit";
                break;
            case "text":
                block.Content = string.Empty;
                break;

            case "content":
                block.Content = "<p>Start typing your content here...</p>";
                break;

            case "markdown":
                block.Content      = "# Heading\n\nYour markdown content here...";
                block.MarkdownView = "edit";
                break;

            case "dynamic_template":
                block.ScribanTemplate = "<section class=\"p-6 rounded-lg bg-slate-50\"><h2>{{ block.title }}</h2><p>{{ block.body }}</p></section>";
                block.ScribanDataJson = """
                    {
                      "title": "Dynamic Template",
                      "body": "Rendered with Scriban."
                    }
                    """;
                block.ScribanView = "code";
                break;

            case "quote":
                block.Content = string.Empty;
                block.Author  = string.Empty;
                break;

            case "separator":
                break;

            case "columns":
                block.ColumnCount   = 2;
                block.Gap           = 16;
                block.EditorColumns =
                [
                    new EditorColumn { Blocks = [] },
                    new EditorColumn { Blocks = [] },
                ];
                break;

            case "image":
                block.Src     = string.Empty;
                block.Alt     = string.Empty;
                block.Caption = string.Empty;
                break;

            case "video":
                block.Url = string.Empty;
                block.Src = string.Empty;
                block.AutoPlay = false;
                break;

            case "gallery":
                block.GalleryImages = [];
                break;

            case "audio":
                block.Src = string.Empty;
                break;

            // Reference types
            case "pages":
            case "posts":
            case "categories":
            case "tags":
            case "authors":
                block.SelectedReferenceId = string.Empty;
                break;
        }

        return block;
    }

    protected void SelectBlock(string id) => SelectedBlockId = id;

    protected void DeleteBlock(int index)
    {
        Blocks.RemoveAt(index);
        SelectedBlockId = null;
        MarkDirty();
        ShowToast("Block deleted");
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
        ShowToast("Block duplicated", "success");
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
        DraggedBlockId = null;
        DraggedIndex   = null;
    }

    protected void DragStartBlock(DragEventArgs e, string id, int index)
    {
        DraggedBlockId = id;
        DraggedIndex   = index;
        DraggedType    = null;
    }

    protected void DragOverBlock(DragEventArgs e, int index)
    {
        DragOverIndex = index;

        // Reorder while dragging (live preview – like the Alpine version)
        if (DraggedIndex is not null && DraggedIndex != index)
        {
            var block = Blocks[DraggedIndex.Value];
            Blocks.RemoveAt(DraggedIndex.Value);
            Blocks.Insert(index, block);
            DraggedIndex = index;
            QueuePreviewRefresh();
        }
    }

    protected void OnDropCanvas(DragEventArgs e)
    {
        if (DraggedType is not null)
        {
            AddBlock(DraggedType);
            DraggedType = null;
        }

        DraggedBlockId = null;
        DraggedIndex   = null;
        DragOverIndex  = -1;
        QueuePreviewRefresh();
    }

    protected void DropBlock(DragEventArgs e, int index)
    {
        DraggedBlockId = null;
        DraggedIndex   = null;
        DragOverIndex  = -1;
        QueuePreviewRefresh();
    }

    // ──────────────────────────────────────────────────────────
    // Column management  (mirrors updateColumnCount / addBlockToColumn / etc.)
    // ──────────────────────────────────────────────────────────

    protected void UpdateColumnCount(EditorBlock block, int newCount)
    {
        var current = block.EditorColumns.Count;

        if (newCount > current)
        {
            for (var i = current; i < newCount; i++)
                block.EditorColumns.Add(new EditorColumn { Blocks = [] });
        }
        else if (newCount < current)
        {
            // Check for content in columns to be removed
            var hasContent = block.EditorColumns.Skip(newCount).Any(c => c.Blocks.Count > 0);
            if (hasContent)
            {
                // In Blazor we can't show a JS confirm() — show a toast warning instead.
                // A future iteration can use RadzenDialogService.
                ShowToast("Some columns have content; reduce columns in the settings panel to confirm.");
                return;
            }

            block.EditorColumns.RemoveRange(newCount, current - newCount);
        }

        block.ColumnCount = newCount;
        QueuePreviewRefresh();
    }

    protected void AddBlockToColumn(EditorBlock block, int colIndex, string type)
    {
        var nb = CreateNestedBlock(type);
        block.EditorColumns[colIndex].Blocks.Add(nb);
        QueuePreviewRefresh();
    }

    private static NestedBlock CreateNestedBlock(string type) => type switch
    {
        "text"   => new NestedBlock { Type = "text",   Content = string.Empty },
        "image"  => new NestedBlock { Type = "image",  Src     = string.Empty, Alt = string.Empty },
        "video"  => new NestedBlock { Type = "video",  Url     = string.Empty, Src = string.Empty },
        "button" => new NestedBlock { Type = "button", Text    = "Click Me",   Url = "#", Style = "primary" },
        _        => new NestedBlock { Type = type },
    };

    protected void RemoveNestedBlock(EditorBlock block, int colIndex, int nestedIndex)
    {
        block.EditorColumns[colIndex].Blocks.RemoveAt(nestedIndex);
        QueuePreviewRefresh();
    }

    protected void DropOnColumn(DragEventArgs e, EditorBlock block, int colIndex)
    {
        if (DraggedType is null) return;

        var mapped = DraggedType switch
        {
            "text"  => "text",
            "image" => "image",
            "video" => "video",
            _       => null,
        };

        if (mapped is not null)
        {
            block.EditorColumns[colIndex].Blocks.Add(CreateNestedBlock(mapped));
            ShowToast($"{DraggedType} added to column", "success");
            QueuePreviewRefresh();
        }

        DraggedType = null;
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
            DynamicTemplatePreviewHtml[block.EditorId] = "<div class=\"text-sm text-red-600\">Template is required.</div>";
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
                _ => BuildPreviewError("Preview failed.")
            };
        }
        catch (JsonException ex)
        {
            DynamicTemplatePreviewHtml[block.EditorId] = BuildPreviewError($"Invalid JSON data: {ex.Message}");
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
    }

    protected void OpenMediaSelectorForNested(EditorBlock parent, int colIndex, NestedBlock nb)
    {
        CurrentMediaBlock = parent;
        IsGalleryMode     = false;
        MediaContext      = "nested";
        NestedMediaTarget = nb;
        MediaModalOpen    = true;
    }

    protected void OpenAudioSelector(EditorBlock block)
    {
        // Simulate audio selection with a placeholder URL
        block.Src = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-1.mp3";
        ShowToast("Audio added", "success");
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
        ShowToast("Media added", "success");
    }

    protected void RemoveImage(EditorBlock block)
    {
        block.Src     = string.Empty;
        block.Alt     = string.Empty;
        block.Caption = string.Empty;
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
            ShowToast("Video added", "success");
        }
        else
        {
            ShowToast("Invalid video URL", "error");
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
            PreviewError = $"Preview render failed: {ex.Message}";
        }
        finally
        {
            IsPreviewRendering = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private string BuildAbsoluteUrl(string relativeUrl)
    {
        return new Uri(new Uri(NavManager.BaseUri), relativeUrl.TrimStart('/')).ToString();
    }

    private static string BuildPreviewFrameDocument(string? html, string baseUri)
    {
        var content = string.IsNullOrWhiteSpace(html)
            ? "<main class=\"pe-empty-state\"><h3>No preview content</h3></main>"
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
                    null, // LayoutRegions are mapped on backend from EditorBlocks
                    ShowInNavMenu,
                    ShowHeaderNavigation,
                    HideFooter,
                    ShowChatAgent,
                    Blocks
                );

                var result = await PagesClient.UpdateAsync(Id.Value, request);
                if (result is Result<CmsPageDetail, AeroError>.Ok)
                {
                    UpdateLastSaved();
                    _pageState = PageState.Clean;
                    await PagesClient.DeleteDraftAsync(Id.Value);  // clean up draft
                    ShowToast("Page saved successfully", "success");
                }
                else if (result is Result<CmsPageDetail, AeroError>.Failure err)
                {
                    ShowToast($"Error saving: {err.Error}", "error");
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
                    _slugState = SlugState.Locked;  // preserve generated slug going forward
                    _pageState = PageState.Clean;
                    UpdateLastSaved();
                    ShowToast("Page created successfully", "success");
                    // Update URL without refreshing
                    // NavManager.NavigateTo($"/manager/page/editor/{Id}", false); 
                }
                else if (result is Result<CmsPageDetail, AeroError>.Failure err)
                {
                    ShowToast($"Error creating: {err.Error}", "error");
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Save failed: {ex.Message}", "error");
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
                ShowToast("Page published!", "success");
            }
            else
            {
                ShowToast("Failed to publish", "error");
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
}

