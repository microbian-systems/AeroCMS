using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.IO;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Aero.Cms.Core.Http.Clients;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Blocks.Common;
using Aero.Core.Railway;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor;

/// <summary>
/// Code-behind for the Blazor port of the Alpine.js CMS Page Editor.
/// All state and logic is a close 1-to-1 translation of <c>aero-cms/app.js</c>.
/// </summary>
public partial class PageEditor : ComponentBase, IDisposable
{
    // ──────────────────────────────────────────────────────────
    // Parameters
    // ──────────────────────────────────────────────────────────

    /// <summary>Optional ID of an existing page to edit.</summary>
    [Parameter] public long? Id { get; set; }

    [Inject] protected DocsClient DocsClient { get; set; } = default!;

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

    // Sidebar category toggles
    protected bool CategoryContent    { get; set; } = true;
    protected bool CategoryMedia      { get; set; } = true;
    protected bool CategoryReferences { get; set; } = true;
    protected bool CategorySettings   { get; set; } = true;
    protected bool CategoryAero       { get; set; } = true;
    protected IReadOnlyList<DocsSummary>? DocsCategories { get; set; }

    // Media modal
    protected bool         MediaModalOpen   { get; set; }
    protected EditorBlock? CurrentMediaBlock { get; set; }
    protected bool         IsGalleryMode    { get; set; }
    protected string?      MediaContext     { get; set; }   // "background" | "nested"
    protected NestedBlock? NestedMediaTarget { get; set; }

    // Mocked media library (replace with real API call later)
    protected List<MediaItem> MediaLibrary { get; set; } =
    [
        new(1, "https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=400", "Mountain landscape"),
        new(2, "https://images.unsplash.com/photo-1469474968028-56623f02e42e?w=400", "Nature scene"),
        new(3, "https://images.unsplash.com/photo-1447752875215-b2761acb3c5d?w=400", "Forest path"),
        new(4, "https://images.unsplash.com/photo-1433086966358-54859d0ed716?w=400", "Waterfall"),
        new(5, "https://images.unsplash.com/photo-1501785888041-af3ef285b470?w=400", "Lake view"),
        new(6, "https://images.unsplash.com/photo-1470071459604-3b5ec3a7fe05?w=400", "Foggy mountains"),
    ];

    // Mocked reference data (replace with real API call later)
    private readonly Dictionary<string, List<ReferenceItem>> _referenceData = new()
    {
        ["pages"]      = [new("1", "About Us"), new("2", "Contact"), new("3", "Services"), new("4", "Portfolio")],
        ["posts"]      = [new("1", "Getting Started with Aero CMS"), new("2", "Best Practices"), new("3", "SEO Tips")],
        ["categories"] = [new("1", Name: "Technology"), new("2", Name: "Design"), new("3", Name: "Business"), new("4", Name: "Lifestyle")],
        ["tags"]       = [new("1", Name: "cms"), new("2", Name: "webdev"), new("3", Name: "design"), new("4", Name: "tutorial")],
        ["authors"]    = [new("1", Name: "John Doe"), new("2", Name: "Jane Smith"), new("3", Name: "Mike Johnson")],
    };

    // Toasts
    protected List<ToastMessage> Toasts { get; set; } = [];

    // Auto-save timer
    private System.Timers.Timer? _autoSaveTimer;

    // ──────────────────────────────────────────────────────────
    // Lifecycle  (mirrors Alpine.js init())
    // ──────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        UpdateLastSaved();

        _autoSaveTimer = new System.Timers.Timer(30_000);
        _autoSaveTimer.Elapsed += async (_, _) => await AutoSaveAsync();
        _autoSaveTimer.AutoReset = true;
        _autoSaveTimer.Start();

        var result = await DocsClient.GetCategoriesAsync();

        if (result is Result<string, IReadOnlyList<DocsSummary>>.Ok ok)
        {
            DocsCategories = ok.Value;
        }
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
        ShowToast("Block deleted", "info");
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
                ShowToast("Some columns have content; reduce columns in the settings panel to confirm.", "info");
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
            _       => (string?)null,
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

    protected void OnConfirmMediaSelection(List<MediaItem> selected)
    {
        if (MediaContext == "background" && selected.Count > 0)
        {
            CurrentMediaBlock!.BackgroundImage = selected[0].Src;
        }
        else if (MediaContext == "nested" && NestedMediaTarget is not null && selected.Count > 0)
        {
            NestedMediaTarget.Src = selected[0].Src;
            NestedMediaTarget.Alt = selected[0].Alt;
        }
        else if (IsGalleryMode)
        {
            CurrentMediaBlock!.GalleryImages.AddRange(
                selected.Select(img => new GalleryImage { Src = img.Src, Alt = img.Alt }));
        }
        else if (selected.Count > 0)
        {
            CurrentMediaBlock!.Src = selected[0].Src;
            CurrentMediaBlock.Alt  = selected[0].Alt;
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

            var newItem = new MediaItem(MediaLibrary.Max(i => i.Id) + 1, dataUrl, file.Name);
            MediaLibrary.Insert(0, newItem);
        }

        ShowToast("Files uploaded", "success");
    }

    // ──────────────────────────────────────────────────────────
    // Video  (mirrors loadVideo / removeVideo)
    // ──────────────────────────────────────────────────────────

    protected void LoadVideo(EditorBlock block)
    {
        var url      = block.Url ?? string.Empty;
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
        var url      = nb.Url ?? string.Empty;
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

    protected async Task SavePage()
    {
        if (IsSaving) return;
        IsSaving = true;
        StateHasChanged();

        try
        {
            // TODO: wire up to PageContentService / MediatR command
            await Task.Delay(800); // simulate network lag
            UpdateLastSaved();
            ShowToast("Page saved successfully", "success");
        }
        finally
        {
            IsSaving = false;
            StateHasChanged();
        }
    }

    protected async Task AutoSaveAsync()
    {
        if (Blocks.Count == 0) return;
        await InvokeAsync(SavePage);
    }

    protected async Task PublishPage()
    {
        await SavePage();
        ShowToast("Page published!", "success");
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

/// <summary>An item in the media library picker.</summary>
public record MediaItem(int Id, string Src, string Alt);

/// <summary>An item in the reference selector (pages, posts, etc.).</summary>
public record ReferenceItem(string Id, string? Title = null, string? Name = null);