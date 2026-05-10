using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Shared.Services;
using Aero.Core;
using Aero.Core.Railway;
using BlazorMonaco;
using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PostEditor;

public partial class PostEditor : ComponentBase, IDisposable
{
    // ──────────────────────────────────────────────────────────
    // Parameters
    // ──────────────────────────────────────────────────────────

    /// <summary>Optional ID of an existing post to edit.</summary>
    [Parameter] public long? Id { get; set; }

    [Inject] protected IBlogHttpClient BlogApi { get; set; } = default!;
    [Inject] protected IAiHttpClient AiClient { get; set; } = default!;
    [Inject] protected ICategoriesHttpClient CategoriesClient { get; set; } = default!;
    [Inject] protected ITagsHttpClient TagsClient { get; set; } = default!;
    [Inject] protected NavigationManager NavManager { get; set; } = default!;
    [Inject] protected IPreviewHttpClient PreviewClient { get; set; } = default!;
    [Inject] protected ISitesHttpClient SitesClient { get; set; } = default!;
    [Inject] protected ICurrentSiteAccessor CurrentSiteAccessor { get; set; } = default!;
    [Inject] protected AdminStateContainer AdminState { get; set; } = default!;

    // ──────────────────────────────────────────────────────────
    // Editor state
    // ──────────────────────────────────────────────────────────

    protected string PostTitle { get; set; } = string.Empty;
    protected string PostSlug { get; set; } = string.Empty;
    protected string Content { get; set; } = string.Empty;
    protected string Excerpt { get; set; } = string.Empty;
    protected string SeoTitle { get; set; } = string.Empty;
    protected string SeoDescription { get; set; } = string.Empty;
    protected string FeaturedImageUrl { get; set; } = string.Empty;
    protected long CategoryId { get; set; }
    protected List<long> SelectedTagIds { get; set; } = [];
    protected DateTime? PublishedAt { get; set; }

    protected string LastSaved { get; set; } = "Never";
    protected bool IsSaving { get; set; }
    protected string ActiveTab { get; set; } = "editor";

    // Preview state
    protected bool FullPreviewMode { get; set; }
    protected string PreviewDevice { get; set; } = "desktop";
    protected bool   IsPreviewRendering { get; set; }
    protected string? PreviewHtml { get; set; }
    protected string? PreviewError { get; set; }
    protected string PreviewFragmentUrl => BuildAbsoluteUrl("api/v1/admin/preview/blog-posts/render-fragment");
    protected string PreviewFrameDocument => BuildPreviewFrameDocument(PreviewHtml, NavManager.BaseUri);
    protected string? PreviewFrameUrl => Id.HasValue
        ? BuildAbsoluteUrl($"_cms/preview/blog/drafts/{Id.Value}?previewVersion={_previewRefreshVersion}", _previewBaseUri)
        : null;

    // Loaded post data
    protected BlogDetail? LoadedPost { get; set; }

    // Reference data
    protected List<CategorySummary> Categories { get; set; } = [];
    protected List<TagSummary> AllTags { get; set; } = [];

    // BlazorMonaco editor reference
    protected StandaloneCodeEditor? _editor;
    private bool _editorReady;

    // Guards against RadzenTextArea @bind-Value firing ValueChanged("") 
    // during initialization and overwriting async-loaded content
    private bool _contentInitialized;

    // Media selector modal state
    protected bool MediaModalOpen { get; set; }

    // Toasts
    protected List<ToastMessage> Toasts { get; set; } = [];

    // AI enhancement state
    protected bool IsEnhancePanelOpen { get; set; }
    protected bool IsEnhancing { get; set; }
    protected bool IsLoadingAiProviders { get; set; }
    protected string EnhanceTargetField { get; set; } = "body";
    protected string EnhancePrompt { get; set; } = string.Empty;
    protected string? SelectedAiProviderId { get; set; }
    protected string? EnhanceSuggestion { get; set; }
    protected string? EnhanceRationale { get; set; }
    protected IReadOnlyList<string> EnhanceWarnings { get; set; } = [];
    protected IReadOnlyList<AiProviderOption> AiProviderOptions { get; set; } = [];

    protected IReadOnlyList<EnhanceTargetOption> EnhanceTargetOptions { get; } =
    [
        new("body", "Body"),
        new("title", "Title"),
        new("summary", "Summary"),
        new("seoTitle", "SEO Title"),
        new("seoDescription", "SEO Description")
    ];

    // Auto-save timer & dirty tracking
    private System.Timers.Timer? _autoSaveTimer;
    private enum PostState { Clean, Dirty }
    private PostState _postState = PostState.Clean;  // new posts start clean — wait for user input

    // Preview debounce
    private const int PreviewDebounceMilliseconds = 300;
    private CancellationTokenSource? _previewDebounceCts;
    private long _previewRefreshVersion;
    private string? _previewBaseUri;

    // ──────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        await ResolvePreviewBaseUriAsync();
        await LoadReferenceDataAsync();

        if (Id.HasValue)
        {
            await LoadPostAsync(Id.Value);
        }
        else
        {
            PostSlug = string.Empty;
            UpdateLastSaved();
            _contentInitialized = true;
        }

        _autoSaveTimer = new System.Timers.Timer(15_000);
        _autoSaveTimer.AutoReset = false;
        _autoSaveTimer.Elapsed += async (_, _) =>
        {
            await InvokeAsync(AutoSaveAsync);
            _autoSaveTimer?.Start();
        };
        _autoSaveTimer.Start();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Once Monaco is initialized, sync any content loaded asynchronously
        if (_editor is not null && !_editorReady)
        {
            if (!string.IsNullOrEmpty(Content))
            {
                await _editor.SetValue(Content);
            }
            _editorReady = true;
        }
    }

    public void Dispose()
    {
        _autoSaveTimer?.Dispose();
        _previewDebounceCts?.Cancel();
        _previewDebounceCts?.Dispose();
    }

    private async Task LoadReferenceDataAsync()
    {
        var catsTask = CategoriesClient.GetAllAsync();
        var tagsTask = TagsClient.GetAllAsync();

        await catsTask;
        await tagsTask;

        if (catsTask.Result is Result<IReadOnlyList<CategorySummary>, AeroError>.Ok catsOk)
            Categories = catsOk.Value.ToList();

        if (tagsTask.Result is Result<IReadOnlyList<TagSummary>, AeroError>.Ok tagsOk)
            AllTags = tagsOk.Value.ToList();
    }

    private async Task LoadPostAsync(long id)
    {
        var result = await BlogApi.GetByIdAsync(id);
        if (result is Result<BlogDetail, AeroError>.Ok ok)
        {
            var post = ok.Value;
            LoadedPost = post;
            PostTitle = post.Title;
            PostSlug = post.Slug;
            Content = ExtractMarkdownContent(post.Content);
            _contentInitialized = true;
            Excerpt = post.Excerpt ?? string.Empty;
            SeoTitle = post.SeoTitle ?? string.Empty;
            SeoDescription = post.SeoDescription ?? string.Empty;
            FeaturedImageUrl = post.ImageUrl ?? string.Empty;
            CategoryId = post.CategoryIds?.FirstOrDefault() ?? 0;
            SelectedTagIds = post.TagIds?.ToList() ?? [];
            PublishedAt = post.PublishedOn?.DateTime;
            _postState = PostState.Clean;
            UpdateLastSaved();
            await InvokeAsync(StateHasChanged);
        }
        else
        {
            ShowToast("Error loading post", "error");
        }
    }

    private static string ExtractMarkdownContent(List<BlockBase>? blocks)
    {
        var markdownBlock = blocks?
            .OfType<MarkdownBlock>()
            .FirstOrDefault();
        return markdownBlock?.Content ?? string.Empty;
    }

    // ──────────────────────────────────────────────────────────
    // BlazorMonaco integration
    // ──────────────────────────────────────────────────────────

    protected StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            AutomaticLayout = true,
            Language = "markdown",
            Value = Content,
            Minimap = new EditorMinimapOptions { Enabled = false },
            ScrollBeyondLastLine = false,
            WordWrap = "on",
            LineNumbers = "on",
            TabSize = 2
        };
    }

    protected async Task OnEditorContentChanged()
    {
        if (_editor is not null)
        {
            Content = await _editor.GetValue();
        }
        MarkDirty();
        if (ActiveTab == "preview" || FullPreviewMode)
        {
            QueuePreviewRefresh();
        }
    }

    // ──────────────────────────────────────────────────────────
    // Tab switching
    // ──────────────────────────────────────────────────────────

    protected async Task SwitchToTab(string tab)
    {
        // When leaving the Code tab, sync Monaco value to Content
        if (ActiveTab == "code" && tab != "code" && _editor is not null)
        {
            Content = await _editor.GetValue();
        }

        // When entering the Code tab, push editor Content into Monaco
        if (tab == "code" && ActiveTab != "code" && _editor is not null && _editorReady)
        {
            await _editor.SetValue(Content);
        }

        ActiveTab = tab;

        // Refresh preview when switching to the preview tab
        if (tab == "preview")
        {
            FullPreviewMode = true;
            _ = RefreshPreviewAsync();
        }
        else
        {
            FullPreviewMode = false;
        }

        StateHasChanged();
    }

    // ──────────────────────────────────────────────────────────
    // Preview
    // ──────────────────────────────────────────────────────────

    protected async Task TogglePreview()
    {
        // Sync Monaco before entering preview mode
        if (!FullPreviewMode && ActiveTab == "code" && _editor is not null)
        {
            Content = await _editor.GetValue();
        }

        FullPreviewMode = !FullPreviewMode;
        if (FullPreviewMode)
        {
            ActiveTab = "preview";
            _previewRefreshVersion++;
            await RefreshPreviewAsync();
        }
        else
        {
            ActiveTab = "editor";
        }
    }

    private void QueuePreviewRefresh()
    {
        if (ActiveTab != "preview" && !FullPreviewMode)
        {
            return;
        }

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
        if (ActiveTab != "preview" && !FullPreviewMode)
        {
            return;
        }

        // When the post has an ID, the iframe loads the full site page
        // (nav, layout, footer) — no fragment render needed.
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
            var blocks = ContentToBlocks();
            var result = await PreviewClient.RenderBlogPostFragmentAsync(blocks, cancellationToken);
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

    private IReadOnlyList<BlockBase> ContentToBlocks()
    {
        var currentContent = Content;
        if (string.IsNullOrWhiteSpace(currentContent))
            return [];

        return [new MarkdownBlock { Content = currentContent }];
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

    private string BuildAbsoluteUrl(string relativeUrl, string? baseUri = null)
    {
        return new Uri(new Uri(baseUri ?? NavManager.BaseUri), relativeUrl.TrimStart('/')).ToString();
    }

    private async Task ResolvePreviewBaseUriAsync()
    {
        SiteViewModel? selectedSite = null;

        if (AdminState.CurrentSiteId is { } selectedSiteId)
        {
            selectedSite = await LoadSiteByIdAsync(selectedSiteId);
        }

        if (selectedSite is null)
        {
            selectedSite = await CurrentSiteAccessor.GetCurrentSiteAsync();
        }

        if (selectedSite is null)
        {
            // Fall back to the default site
            var allSites = await SitesClient.GetAllAsync();
            if (allSites is Result<IReadOnlyList<SiteViewModel>, AeroError>.Ok ok && ok.Value.Count > 0)
            {
                selectedSite = ok.Value.FirstOrDefault(s => s.Id == AdminState.CurrentSiteId) ?? ok.Value[0];
            }
        }

        _previewBaseUri = ResolvePreviewBaseUri(selectedSite) ?? NavManager.BaseUri;
    }

    private string? ResolvePreviewBaseUri(SiteViewModel? site)
    {
        var baseUri = BuildSiteBaseUri(site);
        if (baseUri is null) return null;

        // The site's PrimaryHost might not include the port we're running on.
        // Merge the port from the current request so preview URLs resolve correctly.
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

    private static string? BuildSiteBaseUri(SiteViewModel? site)
    {
        var host = site?.PrimaryHost;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = site?.Hosts?.FirstOrDefault(static h => !string.IsNullOrWhiteSpace(h));
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

        var current = new Uri("https://localhost");
        var authority = host;
        if (!host.Contains(':', StringComparison.Ordinal))
        {
            authority = current.IsDefaultPort ? host : $"{host}:{current.Port}";
        }

        return EnsureTrailingSlash($"https://{authority}");
    }

    private static string EnsureTrailingSlash(string uri)
    {
        return uri.EndsWith("/", StringComparison.Ordinal) ? uri : $"{uri}/";
    }

    // ──────────────────────────────────────────────────────────
    // Metadata helpers
    // ──────────────────────────────────────────────────────────

    protected void ToggleTag(long tagId)
    {
        if (SelectedTagIds.Contains(tagId))
            SelectedTagIds.Remove(tagId);
        else
            SelectedTagIds.Add(tagId);
        MarkDirty();
    }

    // ── Dirty tracking helpers for input handlers ────────

    protected void OnTitleChanged(string title) 
    { 
        PostTitle = title; 
        if (string.IsNullOrWhiteSpace(PostSlug))
            PostSlug = TitleToSlug(title);
        MarkDirty(); 
    }
    protected void OnSlugChanged(string slug) { PostSlug = slug; MarkDirty(); }
    protected void OnContentChanged(string content)
    {
        Content = content;
        MarkDirty();
        if (ActiveTab == "preview" || FullPreviewMode)
        {
            QueuePreviewRefresh();
        }
    }
    protected void OnExcerptChanged(string excerpt) { Excerpt = excerpt; MarkDirty(); }
    protected void OnSeoTitleChanged(string title) { SeoTitle = title; MarkDirty(); }
    protected void OnSeoDescriptionChanged(string description) { SeoDescription = description; MarkDirty(); }
    protected void OnFeaturedImageChanged(string url) { FeaturedImageUrl = url; MarkDirty(); }
    protected void OnCategoryChanged(string categoryId)
    {
        if (long.TryParse(categoryId, out var id)) CategoryId = id;
        MarkDirty();
    }

    // ──────────────────────────────────────────────────────────
    // Save / Publish / Unpublish
    // ──────────────────────────────────────────────────────────

    private async Task AutoSaveAsync()
    {
        if (_postState != PostState.Dirty) return;

        // Belt-and-suspenders: skip if slug is blank (no meaningful content yet)
        if (string.IsNullOrWhiteSpace(PostSlug))
            return;

        if (Id is null)
        {
            // New post: only auto-create if there's actual content
            if (string.IsNullOrWhiteSpace(PostTitle) && string.IsNullOrWhiteSpace(Content))
                return;
            await SavePost();
            return;
        }

        await SavePost();
    }

    private void MarkDirty()
    {
        _postState = PostState.Dirty;
        // Debounce: reset the 15s countdown on every change
        _autoSaveTimer?.Stop();
        _autoSaveTimer?.Start();
    }

    protected async Task SavePost()
    {
        if (IsSaving) return;

        // Sync Monaco value before saving (only if actively using Code tab)
        if (ActiveTab == "code" && _editor is not null)
        {
            Content = await _editor.GetValue();
        }

        IsSaving = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            if (Id.HasValue)
            {
                var request = new UpdateBlogRequest
                {
                    Id = Id.Value,
                    Title = PostTitle,
                    Slug = PostSlug,
                    Summary = Excerpt,
                    MarkdownContent = Content,
                    SeoTitle = string.IsNullOrWhiteSpace(SeoTitle) ? PostTitle : SeoTitle,
                    SeoDescription = string.IsNullOrWhiteSpace(SeoDescription) ? Excerpt : SeoDescription,
                    ImageUrl = FeaturedImageUrl,
                    PublicationState = PublishedAt.HasValue
                        ? (int)ContentPublicationState.Published
                        : (int)ContentPublicationState.Draft
                };

                var result = await BlogApi.UpdateAsync(Id.Value, request);
                if (result is Result<BlogDetail, AeroError>.Ok ok)
                {
                    PublishedAt = ok.Value.PublishedOn?.DateTime;
                    LoadedPost = ok.Value;
                    _postState = PostState.Clean;
                    UpdateLastSaved();
                    ShowToast("Post saved successfully", "success");
                }
                else if (result is Result<BlogDetail, AeroError>.Failure err)
                {
                    ShowToast($"Error saving: {err.Error}", "error");
                }
            }
            else
            {
                var request = new CreateBlogRequest
                {
                    Title = PostTitle,
                    Slug = PostSlug,
                    Summary = Excerpt,
                    MarkdownContent = Content,
                    SeoTitle = string.IsNullOrWhiteSpace(SeoTitle) ? PostTitle : SeoTitle,
                    SeoDescription = string.IsNullOrWhiteSpace(SeoDescription) ? Excerpt : SeoDescription,
                    ImageUrl = FeaturedImageUrl,
                    PublicationState = (int)ContentPublicationState.Draft
                };

                var result = await BlogApi.CreateAsync(request);
                if (result is Result<BlogDetail, AeroError>.Ok ok)
                {
                    Id = ok.Value.Id;
                    LoadedPost = ok.Value;
                    PublishedAt = ok.Value.PublishedOn?.DateTime;
                    _postState = PostState.Clean;
                    UpdateLastSaved();
                    ShowToast("Post created successfully", "success");

                    NavManager.NavigateTo($"/manager/post/editor/{Id}", false);
                }
                else if (result is Result<BlogDetail, AeroError>.Failure err)
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

    protected async Task PublishPost()
    {
        if (!Id.HasValue)
        {
            await SavePost();
        }

        if (Id.HasValue)
        {
            var result = await BlogApi.PublishAsync(Id.Value);
            if (result is Result<BlogDetail, AeroError>.Ok ok)
            {
                PublishedAt = ok.Value.PublishedOn?.DateTime;
                _postState = PostState.Clean;
                ShowToast("Post published!", "success");
            }
            else
            {
                ShowToast("Failed to publish", "error");
            }
        }
    }

    protected async Task UnpublishPost()
    {
        if (!Id.HasValue) return;

        var result = await BlogApi.UnpublishAsync(Id.Value);
        if (result is Result<BlogDetail, AeroError>.Ok ok)
        {
            PublishedAt = ok.Value.PublishedOn?.DateTime;
            ShowToast("Post unpublished", "success");
        }
        else
        {
            ShowToast("Failed to unpublish", "error");
        }
    }

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
        slug = Regex.Replace(slug, @"-{2,}", "-");         // collapse multiple hyphens
        slug = slug.Trim('-');                             // trim leading/trailing hyphens

        return slug;
    }

    protected void UpdateLastSaved()
        => LastSaved = $"Post saved at {DateTime.Now:HH:mm:ss}";

    // ──────────────────────────────────────────────────────────
    // Toast notifications
    // ──────────────────────────────────────────────────────────

    protected void ShowToast(string message, string type = "info")
    {
        var toast = new ToastMessage { Message = message, Type = type };
        Toasts.Add(toast);

        _ = Task.Delay(4000).ContinueWith(_ => InvokeAsync(() =>
        {
            RemoveToast(toast.Id);
            StateHasChanged();
        }));
    }

    protected void RemoveToast(string id)
        => Toasts.RemoveAll(t => t.Id == id);

    // ──────────────────────────────────────────────────────────
    // Media selector
    // ──────────────────────────────────────────────────────────

    protected void OpenMediaSelector()
    {
        MediaModalOpen = true;
    }

    protected void OnConfirmFeaturedImage(List<MediaItem> items)
    {
        if (items.Count > 0)
        {
            FeaturedImageUrl = items[0].Src;
            MarkDirty();
        }

        MediaModalOpen = false;
    }

    // ──────────────────────────────────────────────────────────
    // AI enhancement
    // ──────────────────────────────────────────────────────────

    protected async Task OpenEnhancePanel()
    {
        if (ActiveTab == "code" && _editor is not null)
        {
            Content = await _editor.GetValue();
        }

        EnhanceSuggestion = null;
        EnhanceRationale = null;
        EnhanceWarnings = [];
        IsEnhancePanelOpen = true;

        await LoadAiProviderOptionsAsync();
    }

    protected void CloseEnhancePanel()
    {
        IsEnhancePanelOpen = false;
        IsEnhancing = false;
    }

    protected void UseQuickPrompt(string prompt)
    {
        EnhancePrompt = prompt;
    }

    protected async Task RunEnhancementAsync()
    {
        if (IsEnhancing)
        {
            return;
        }

        if (ActiveTab == "code" && _editor is not null)
        {
            Content = await _editor.GetValue();
        }

        IsEnhancing = true;
        EnhanceSuggestion = null;
        EnhanceRationale = null;
        EnhanceWarnings = [];
        await InvokeAsync(StateHasChanged);

        var request = new EnhanceContentRequest(
            ContentKind: "post",
            TargetField: EnhanceTargetField,
            CurrentText: GetEnhanceFieldValue(EnhanceTargetField),
            UserPrompt: string.IsNullOrWhiteSpace(EnhancePrompt) ? null : EnhancePrompt,
            Title: PostTitle,
            Summary: Excerpt,
            Slug: PostSlug,
            Tone: null,
            Metadata: BuildEnhanceMetadata(),
            ProviderId: string.IsNullOrWhiteSpace(SelectedAiProviderId) ? null : SelectedAiProviderId);

        var result = await AiClient.EnhanceContentAsync(request);
        if (result is Result<EnhanceContentResponse, AeroError>.Ok ok)
        {
            EnhanceSuggestion = ok.Value.EnhancedText;
            EnhanceRationale = ok.Value.Rationale;
            EnhanceWarnings = ok.Value.Warnings;
        }
        else if (result is Result<EnhanceContentResponse, AeroError>.Failure failure)
        {
            ShowToast($"AI enhancement failed: {failure.Error}", "error");
        }

        IsEnhancing = false;
    }

    protected async Task ApplyEnhancementAsync()
    {
        if (string.IsNullOrWhiteSpace(EnhanceSuggestion))
        {
            return;
        }

        switch (EnhanceTargetField)
        {
            case "body":
                Content = EnhanceSuggestion;
                if (_editor is not null && _editorReady)
                {
                    await _editor.SetValue(Content);
                }
                break;
            case "title":
                PostTitle = EnhanceSuggestion;
                break;
            case "summary":
                Excerpt = EnhanceSuggestion;
                break;
            case "seoTitle":
                SeoTitle = EnhanceSuggestion;
                break;
            case "seoDescription":
                SeoDescription = EnhanceSuggestion;
                break;
        }

        MarkDirty();
        CloseEnhancePanel();
        ShowToast("AI suggestion applied locally", "success");
    }

    private string GetEnhanceFieldValue(string targetField)
        => targetField switch
        {
            "body" => Content,
            "title" => PostTitle,
            "summary" => Excerpt,
            "seoTitle" => SeoTitle,
            "seoDescription" => SeoDescription,
            _ => Content
        };

    private IReadOnlyDictionary<string, string> BuildEnhanceMetadata()
    {
        var metadata = new Dictionary<string, string>
        {
            ["publicationState"] = PublishedAt.HasValue ? "published" : "draft"
        };

        if (Id.HasValue)
        {
            metadata["postId"] = Id.Value.ToString();
        }

        return metadata;
    }

    private async Task LoadAiProviderOptionsAsync()
    {
        IsLoadingAiProviders = true;
        await InvokeAsync(StateHasChanged);

        var result = await AiClient.GetProviderOptionsAsync();
        if (result is Result<IReadOnlyList<AiProviderOption>, AeroError>.Ok ok)
        {
            AiProviderOptions = ok.Value;
            var selectedStillAvailable = AiProviderOptions.Any(provider =>
                provider.Id.Equals(SelectedAiProviderId, StringComparison.OrdinalIgnoreCase));

            if (!selectedStillAvailable)
            {
                SelectedAiProviderId = AiProviderOptions.FirstOrDefault(provider => provider.IsDefault)?.Id
                    ?? AiProviderOptions.FirstOrDefault()?.Id;
            }
        }
        else if (result is Result<IReadOnlyList<AiProviderOption>, AeroError>.Failure failure)
        {
            AiProviderOptions = [];
            SelectedAiProviderId = null;
            ShowToast($"AI providers failed to load: {failure.Error}", "error");
        }

        IsLoadingAiProviders = false;
    }

    private string TabBtnClass(string tab) =>
        ActiveTab == tab ? "pe-tab-btn active" : "pe-tab-btn";

    protected sealed record EnhanceTargetOption(string Value, string Label);
}
