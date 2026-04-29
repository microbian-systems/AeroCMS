using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    // Auto-save timer
    private System.Timers.Timer? _autoSaveTimer;

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

        _autoSaveTimer = new System.Timers.Timer(30_000);
        _autoSaveTimer.Elapsed += async (_, _) => await InvokeAsync(AutoSaveAsync);
        _autoSaveTimer.AutoReset = true;
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
            FeaturedImageUrl = post.ImageUrl ?? string.Empty;
            CategoryId = post.CategoryIds?.FirstOrDefault() ?? 0;
            SelectedTagIds = post.TagIds?.ToList() ?? [];
            PublishedAt = post.PublishedOn?.DateTime;
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
    }

    // ──────────────────────────────────────────────────────────
    // Save / Publish / Unpublish
    // ──────────────────────────────────────────────────────────

    private async Task AutoSaveAsync()
    {
        if (Id is null) return;
        await SavePost();
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
                    SeoTitle = PostTitle,
                    SeoDescription = Excerpt,
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
                    SeoTitle = PostTitle,
                    SeoDescription = Excerpt,
                    ImageUrl = FeaturedImageUrl,
                    PublicationState = (int)ContentPublicationState.Draft
                };

                var result = await BlogApi.CreateAsync(request);
                if (result is Result<BlogDetail, AeroError>.Ok ok)
                {
                    Id = ok.Value.Id;
                    LoadedPost = ok.Value;
                    PublishedAt = ok.Value.PublishedOn?.DateTime;
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

    protected void UpdateLastSaved()
        => LastSaved = DateTime.Now.ToString("HH:mm");

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
}
