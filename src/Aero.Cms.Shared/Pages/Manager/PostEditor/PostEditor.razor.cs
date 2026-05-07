using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
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

    // Toasts
    protected List<ToastMessage> Toasts { get; set; } = [];

    // AI enhancement state
    protected bool IsEnhancePanelOpen { get; set; }
    protected bool IsEnhancing { get; set; }
    protected string EnhanceTargetField { get; set; } = "body";
    protected string EnhancePrompt { get; set; } = string.Empty;
    protected string? EnhanceSuggestion { get; set; }
    protected string? EnhanceRationale { get; set; }
    protected IReadOnlyList<string> EnhanceWarnings { get; set; } = [];

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

    // ──────────────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
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
        }
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
    protected void OnContentChanged(string content) { Content = content; MarkDirty(); }
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
            Metadata: BuildEnhanceMetadata());

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

    protected sealed record EnhanceTargetOption(string Value, string Label);
}
