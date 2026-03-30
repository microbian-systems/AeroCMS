using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.IO;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Aero.Cms.Core;
using Aero.Cms.Core.Http.Clients;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Blocks.Common;
using Aero.Core.Railway;
using Aero.Cms.Core.Blocks.Layout;
using CmsPageDetail = Aero.Cms.Core.Http.Clients.PageDetail;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

public partial class PageEditor : ComponentBase, IDisposable
{
    // ──────────────────────────────────────────────────────────
    // Parameters
    // ──────────────────────────────────────────────────────────

    /// <summary>Optional ID of an existing page to edit.</summary>
    [Parameter] public long? Id { get; set; }

    [Inject] protected DocsClient DocsClient { get; set; } = default!;
    [Inject] protected IPagesHttpClient PagesClient { get; set; } = default!;
    [Inject] protected IMediaHttpClient MediaClient { get; set; } = default!;
    [Inject] protected IBlogHttpClient BlogClient { get; set; } = default!;
    [Inject] protected ICategoriesHttpClient CategoriesClient { get; set; } = default!;
    [Inject] protected ITagsHttpClient TagsClient { get; set; } = default!;
    [Inject] protected IUsersHttpClient UsersClient { get; set; } = default!;
    [Inject] protected NavigationManager NavManager { get; set; } = default!;

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

    // Redundant ID removed to avoid ambiguity with ManagerComponent Base.Id
    // public string Id { get; set; } = string.Empty; 

    private string SeoTitle { get; set; } = string.Empty;
    protected string SeoDescription { get; set; } = string.Empty;
    protected bool   ShowInNavMenu { get; set; } = true;
    protected ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;

    protected CmsPageDetail? LoadedPage { get; set; }

    protected IReadOnlyList<DocsSummary>? DocsCategories { get; set; }

    // Media modal
    protected bool         MediaModalOpen   { get; set; }
    protected EditorBlock? CurrentMediaBlock { get; set; }
    protected bool         IsGalleryMode    { get; set; }
    protected string?      MediaContext     { get; set; }   // "background" | "nested"
    protected NestedBlock? NestedMediaTarget { get; set; }

    protected List<MediaItem> MediaLibrary { get; set; } = [];
    private Dictionary<string, List<ReferenceItem>> _referenceData = new();

    // Toasts
    protected List<ToastMessage> Toasts { get; set; } = [];

    // Auto-save timer
    private System.Timers.Timer? _autoSaveTimer;

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

        if (result is Result<string, IReadOnlyList<DocsSummary>>.Ok ok)
        {
            DocsCategories = ok.Value;
        }
    }

    private async Task LoadPageAsync(long id)
    {
        await LoadReferenceDataAsync();

        var result = await PagesClient.GetByIdAsync(id);
        if (result is Result<string, CmsPageDetail>.Ok ok)
        {
            var page = ok.Value;
            LoadedPage = page;
            PageTitle = page.Title;
            PageSlug = page.Slug;
            SeoTitle = page.SeoTitle ?? string.Empty;
            SeoDescription = page.SeoDescription ?? string.Empty;
            PublicationState = page.PublicationState;
            ShowInNavMenu = page.ShowInNavMenu; 
            
            // Load blocks if available in API
            if (page.Blocks?.Any() == true)
            {
                Blocks = page.Blocks.ToList();
            }

            UpdateLastSaved();
        }
        else
        {
            ShowToast("Error loading page", "error");
        }
    }

    private async Task LoadReferenceDataAsync()
    {
        // Media Gallery
        var mediaResult = await MediaClient.GetAllAsync(take: 50);
        if (mediaResult is Result<string, PagedResult<MediaSummary>>.Ok mediaOk)
        {
            MediaLibrary = mediaOk.Value.Items
                .Select(m => new MediaItem(m.Id, m.Url, m.FileName))
                .ToList();
        }

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

        if (pagesTask.Result is Result<string, PagedResult<PageSummary>>.Ok pagesOk)
            _referenceData["pages"] = pagesOk.Value.Items.Select(p => new ReferenceItem(p.Id.ToString(), p.Title)).ToList();
        
        if (blogsTask.Result is Result<string, PagedResult<BlogSummary>>.Ok blogsOk)
            _referenceData["posts"] = blogsOk.Value.Items.Select(p => new ReferenceItem(p.Id.ToString(), p.Title)).ToList();
            
        if (catsTask.Result is Result<string, IReadOnlyList<CategorySummary>>.Ok catsOk)
            _referenceData["categories"] = catsOk.Value.Select(c => new ReferenceItem(c.Id.ToString(), Name: c.Name)).ToList();
            
        if (tagsTask.Result is Result<string, IReadOnlyList<TagSummary>>.Ok tagsOk)
            _referenceData["tags"] = tagsOk.Value.Select(t => new ReferenceItem(t.Id.ToString(), Name: t.Name)).ToList();
            
        if (usersTask.Result is Result<string, PagedResult<UserSummary>>.Ok usersOk)
            _referenceData["authors"] = usersOk.Value.Items.Select(u => new ReferenceItem(u.Id.ToString(), Name: u.DisplayName)).ToList();
    }

    public void Dispose()
    {
        _autoSaveTimer?.Dispose();
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
        ShowToast("Block added", "success");
    }

    private EditorBlock CreateBlock(string type)
    {
        var block = new EditorBlock { Type = type };

        switch (type)
        {
            case "hero":
                block.MainText = string.Empty;
                block.SubText  = string.Empty;
                block.CtaText  = string.Empty;
                block.CtaUrl   = string.Empty;
                block.BackgroundImage = string.Empty;
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
        ShowToast("Block deleted");
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
        ShowToast("Block duplicated", "success");
    }

    protected void MoveBlockUp(int index)
    {
        if (index <= 0) return;
        (Blocks[index], Blocks[index - 1]) = (Blocks[index - 1], Blocks[index]);
    }

    protected void MoveBlockDown(int index)
    {
        if (index >= Blocks.Count - 1) return;
        (Blocks[index], Blocks[index + 1]) = (Blocks[index + 1], Blocks[index]);
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
    }

    protected void DropBlock(DragEventArgs e, int index)
    {
        DraggedBlockId = null;
        DraggedIndex   = null;
        DragOverIndex  = -1;
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
    }

    protected void AddBlockToColumn(EditorBlock block, int colIndex, string type)
    {
        var nb = CreateNestedBlock(type);
        block.EditorColumns[colIndex].Blocks.Add(nb);
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
        => block.EditorColumns[colIndex].Blocks.RemoveAt(nestedIndex);

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

    protected async Task HandleFileSelect(InputFileChangeEventArgs e)
    {
        foreach (var file in e.GetMultipleFiles())
        {
            if (!file.ContentType.StartsWith("image/")) continue;

            // Simulate upload: read as base64 for preview
            using var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
            using var ms     = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var b64   = Convert.ToBase64String(bytes);
            var dataUrl = $"data:{file.ContentType};base64,{b64}";

            var newItem = new MediaItem(MediaLibrary.Any() ? MediaLibrary.Max(i => i.Id) + 1 : 1, dataUrl, file.Name);
            MediaLibrary.Insert(0, newItem);
        }

        ShowToast("Files uploaded", "success");
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

    protected void TogglePreview()
    {
        PreviewMode = !PreviewMode;
        if (PreviewMode) SelectedBlockId = null;
    }

    // ──────────────────────────────────────────────────────────
    // Save / Publish  (mirrors savePage / publishPage)
    // ──────────────────────────────────────────────────────────

    private async Task AutoSaveAsync()
    {
        // One-shot save implementation handles both create and update
        if (Id == 0 || Id is null) return;
        
        await SavePage();
    }

    private async Task SavePage()
    {
        if (IsSaving) return;
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
                    null, // LayoutRegions are mapped on backend from EditorBlocks
                    ShowInNavMenu,
                    Blocks
                );

                var result = await PagesClient.UpdateAsync(Id.Value, request);
                if (result is Result<string, CmsPageDetail>.Ok)
                {
                    UpdateLastSaved();
                    ShowToast("Page saved successfully", "success");
                }
                else if (result is Result<string, CmsPageDetail>.Failure err)
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
                    Blocks
                );

                var result = await PagesClient.CreateAsync(request);
                if (result is Result<string, CmsPageDetail>.Ok createOk)
                {
                    Id = createOk.Value.Id;
                    UpdateLastSaved();
                    ShowToast("Page created successfully", "success");
                    // Update URL without refreshing
                    // NavManager.NavigateTo($"/manager/page/editor/{Id}", false); 
                }
                else if (result is Result<string, CmsPageDetail>.Failure err)
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
            if (result is Result<string, CmsPageDetail>.Ok ok)
            {
                PublicationState = ok.Value.PublicationState;
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



// ──────────────────────────────────────────────────────────────
// Supporting types
// ──────────────────────────────────────────────────────────────