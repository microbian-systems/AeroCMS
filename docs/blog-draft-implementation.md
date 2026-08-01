
> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). Marten
> was migrated out in [`surrealdb-marten-port.md`](surrealdb-marten-port.md).
> This document is a historical implementation record; its Marten/PostgreSQL
> persistence details do not reflect the current stack.

# Blog Draft Implementation

## Problem

The blog editor (`PostEditor`) currently auto-saves directly to `BlogPostDocument` via `PUT /api/v1/admin/blogs/{id}`. If the post is published, editing it overwrites the live content immediately. There is no draft isolation — unpublished edits bleed into the published version on every auto-save.

The PageEditor solved this with a dedicated draft system (`PageDraft` entity + draft endpoints). The blog editor needs the same treatment.

## Existing Draft Pattern (PageEditor — Reference)

### Entity: `PageDraft` (`src/Aero.Cms.Core.Entities/PageDraft.cs`)

```csharp
public sealed class PageDraft : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long PageId { get; set; }          // FK to the published PageDocument
    public string Title { get; set; }
    public string Slug { get; set; }
    public string? Summary { get; set; }
    public List<EditorBlock> Blocks { get; set; }
    public ContentPublicationState PublicationState { get; set; }
    public DateTimeOffset DraftedAt { get; set; }
}
```

### Endpoints: `PagesApi.cs` draft section

| Method | Route | Handler | Purpose |
|--------|-------|---------|---------|
| `GET` | `/api/v1/admin/pages/{id}/draft` | `GetPageDraft` | Load draft for a page (returns null if none) |
| `PUT` | `/api/v1/admin/pages/{id}/draft` | `SavePageDraft` | Upsert draft atomically |
| `DELETE` | `/api/v1/admin/pages/{id}/draft` | `DeletePageDraft` | Remove draft (on manual save or publish) |

### Editor behavior (PageEditor):

- **Editing a published page**: All auto-saves go to `PageDraft`, not `PageDocument`. Manual "Save" promotes draft to `PageDocument` and deletes the draft.
- **Editing a draft page**: Auto-saves go directly to `PageDocument` (since there's no published version to protect).
- **Publishing**: If a draft exists, publish copies draft content to `PageDocument` and deletes draft.

---

## Proposed Blog Draft Design

### 1. New Entity: `BlogPostDraft`

**File:** `src/Aero.Cms.Core.Entities/BlogPostDraft.cs`

```csharp
public sealed class BlogPostDraft : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long BlogPostId { get; set; }       // FK to the published BlogPostDocument (0 for new)
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? MarkdownContent { get; set; }
    public List<long> TagIds { get; set; } = [];
    public long? CategoryId { get; set; }
    public long? AuthorId { get; set; }
    public string? ImageUrl { get; set; }
    public DateTimeOffset DraftedAt { get; set; }
    public bool IsNew { get; set; }            // true if this is a draft for a not-yet-created post
}
```

**Marten config** — add to `BlogModule.cs` `Configure()`:

```csharp
opts.Schema.For<BlogPostDraft>()
    .DatabaseSchemaName(Schemas.Database)
    .DocumentAlias("blog_post_drafts");
opts.Schema.For<BlogPostDraft>().Index(x => x.BlogPostId);
opts.Schema.For<BlogPostDraft>().Index(x => x.SiteId);
```

### 2. New DTOs

**File:** `src/Aero.Cms.Abstractions/Http/Clients/BlogClient.cs` (add to existing file)

```csharp
public record BlogPostDraftResponse(
    long Id,
    long BlogPostId,
    string Title,
    string Slug,
    string? Excerpt,
    string? SeoTitle,
    string? SeoDescription,
    string? MarkdownContent,
    List<long> TagIds,
    long? CategoryId,
    string? ImageUrl,
    DateTimeOffset DraftedAt);

public record SaveBlogPostDraftRequest(
    string Title,
    string Slug,
    string? Excerpt,
    string? SeoTitle,
    string? SeoDescription,
    string? MarkdownContent,
    List<long>? TagIds,
    long? CategoryId,
    string? ImageUrl,
    long? AuthorId);
```

### 3. New Endpoints in `BlogApi.cs`

Add to the existing `MapBlogApi()` group:

```csharp
group.MapGet("/{id:long}/draft", GetDraft);
group.MapPut("/{id:long}/draft", SaveDraft);
group.MapDelete("/{id:long}/draft", DeleteDraft);
```

**Draft loading** — lightweight, returns null if no draft exists:

```csharp
private static async Task<IResult> GetDraft(
    long id,
    IQuerySession querySession)
{
    var draft = await querySession.Query<BlogPostDraft>()
        .FirstOrDefaultAsync(d => d.BlogPostId == id);
    return TypedResults.Ok(draft); // null means no draft
}
```

**Draft save** — upsert by `BlogPostId`:

```csharp
private static async Task<IResult> SaveDraft(
    long id,
    SaveBlogPostDraftRequest request,
    IDocumentSession session,
    IQuerySession querySession,
    [FromServices] ISiteContext siteContext)
{
    var existing = await querySession.Query<BlogPostDraft>()
        .FirstOrDefaultAsync(d => d.BlogPostId == id);

    if (existing is not null)
    {
        existing.Title = request.Title;
        existing.Slug = request.Slug;
        existing.Excerpt = request.Excerpt;
        existing.SeoTitle = request.SeoTitle;
        existing.SeoDescription = request.SeoDescription;
        existing.MarkdownContent = request.MarkdownContent;
        existing.TagIds = request.TagIds ?? [];
        existing.CategoryId = request.CategoryId;
        existing.ImageUrl = request.ImageUrl;
        existing.AuthorId = request.AuthorId;
        existing.DraftedAt = DateTimeOffset.UtcNow;
        session.Store(existing);
    }
    else
    {
        var draft = new BlogPostDraft
        {
            Id = Snowflake.NewId(),
            SiteId = siteContext.SiteId,
            BlogPostId = id,
            Title = request.Title,
            Slug = request.Slug,
            Excerpt = request.Excerpt,
            SeoTitle = request.SeoTitle,
            SeoDescription = request.SeoDescription,
            MarkdownContent = request.MarkdownContent,
            TagIds = request.TagIds ?? [],
            CategoryId = request.CategoryId,
            ImageUrl = request.ImageUrl,
            AuthorId = request.AuthorId,
            DraftedAt = DateTimeOffset.UtcNow,
            IsNew = false
        };
        session.Store(draft);
    }

    await session.SaveChangesAsync();
    return TypedResults.Ok();
}
```

**Draft delete** — cleanup on manual save or publish:

```csharp
private static async Task<IResult> DeleteDraft(
    long id,
    IDocumentSession session,
    IQuerySession querySession)
{
    var existing = await querySession.Query<BlogPostDraft>()
        .FirstOrDefaultAsync(d => d.BlogPostId == id);

    if (existing is not null)
    {
        session.Delete(existing);
        await session.SaveChangesAsync();
    }

    return TypedResults.NoContent();
}
```

### 4. HTTP Client Methods

Add to `IBlogHttpClient` / `BlogHttpClient` in `src/Aero.Cms.Abstractions/Http/Clients/BlogClient.cs`:

```csharp
public interface IBlogHttpClient
{
    // ... existing methods

    Task<BlogPostDraftResponse?> GetDraftAsync(long postId, CancellationToken ct = default);
    Task SaveDraftAsync(long postId, SaveBlogPostDraftRequest request, CancellationToken ct = default);
    Task DeleteDraftAsync(long postId, CancellationToken ct = default);
}
```

```csharp
public class BlogHttpClient : AeroCmsClientBase, IBlogHttpClient
{
    // ... existing methods

    public async Task<BlogPostDraftResponse?> GetDraftAsync(long postId, CancellationToken ct = default)
    {
        var result = await GetAsync<BlogPostDraftResponse?>($"{postId}/draft", ct);
        return result switch
        {
            Result<BlogPostDraftResponse?, AeroError>.Ok ok => ok.Value,
            _ => null
        };
    }

    public Task SaveDraftAsync(long postId, SaveBlogPostDraftRequest request, CancellationToken ct = default)
        => PutAsync($"{postId}/draft", request, ct);

    public Task DeleteDraftAsync(long postId, CancellationToken ct = default)
        => DeleteAsync($"{postId}/draft", ct);
}
```

Note: `PutAsync` and `DeleteAsync` with no response body need to be added to `AeroCmsClientBase` or handled differently since `BlogHttpClient` extends `AeroCmsClientBase`.

### 5. PostEditor Changes

#### 5a. Inject draft client and track published state

Add to `PostEditor.razor.cs`:

```csharp
[Inject] protected IBlogHttpClient BlogApi { get; set; } // already exists

// New: track post state for draft logic
private bool _isPublished;  // true when the loaded post is published
private bool _hasDraft;     // true when a draft exists for this post
```

When loading an existing post (`LoadPostAsync`):

```csharp
LoadedPost = post;
_isPublished = post.PublicationState == (int)ContentPublicationState.Published;

if (_isPublished)
{
    // Load draft overlay if it exists
    var draft = await BlogApi.GetDraftAsync(post.Id);
    if (draft is not null)
    {
        PostTitle = draft.Title;
        PostSlug = draft.Slug;
        Content = draft.MarkdownContent ?? string.Empty;
        Excerpt = draft.Excerpt ?? string.Empty;
        ...
        _hasDraft = true;
    }
}
```

#### 5b. Save logic: auto-save → draft (published) vs direct (draft/new)

In `SavePost()`, when `Id.HasValue` and `_isPublished`:

```csharp
if (_isPublished)
{
    // Save as draft — don't touch live document
    var draftRequest = new SaveBlogPostDraftRequest(
        Title: PostTitle,
        Slug: PostSlug,
        Excerpt: Excerpt,
        SeoTitle: PostTitle,
        SeoDescription: Excerpt,
        MarkdownContent: Content,
        TagIds: SelectedTagIds.Count > 0 ? SelectedTagIds : null,
        CategoryId: CategoryId > 0 ? CategoryId : null,
        ImageUrl: FeaturedImageUrl,
        AuthorId: null
    );

    try
    {
        await BlogApi.SaveDraftAsync(Id.Value, draftRequest);
        _hasDraft = true;
        _postState = PostState.Clean;
        UpdateLastSaved();
        ShowToast("Draft saved", "success");
    }
    catch (Exception ex)
    {
        ShowToast($"Error saving draft: {ex.Message}", "error");
    }
}
else
{
    // Existing direct save logic for unpublished posts
    // ...
}
```

#### 5c. Publish logic: promote draft to live

In `PublishPost()`, before publishing, check for a draft and promote:

```csharp
protected async Task PublishPost()
{
    if (!Id.HasValue)
    {
        await SavePost();  // creates the post first
    }

    if (Id.HasValue)
    {
        // If there's a draft, promote it to live first
        if (_hasDraft)
        {
            // Load draft content
            var draft = await BlogApi.GetDraftAsync(Id.Value);
            if (draft is not null)
            {
                // Update live document with draft content
                var updateRequest = new UpdateBlogRequest
                {
                    Id = Id.Value,
                    Title = draft.Title,
                    Slug = draft.Slug,
                    Summary = draft.Excerpt,
                    MarkdownContent = draft.MarkdownContent,
                    // ... map remaining fields
                    PublicationState = (int)ContentPublicationState.Draft
                };
                await BlogApi.UpdateAsync(Id.Value, updateRequest);
                // Delete draft
                await BlogApi.DeleteDraftAsync(Id.Value);
                _hasDraft = false;
            }
        }

        var result = await BlogApi.PublishAsync(Id.Value);
        // ... existing success/error handling
    }
}
```

#### 5d. New post flow (no changes needed)

For brand-new posts (`Id is null`), the editor already creates the post on first auto-save. After creation, it navigates to `/manager/post/editor/{Id}` and the post starts as `Draft`. Since it's not published, auto-saves go directly to `BlogPostDocument` — same as today. Draft isolation only kicks in when `_isPublished == true`.

### 6. Edge Cases

| Scenario | Behavior |
|----------|----------|
| New post → user types → auto-save creates post | Post created as Draft → no draft isolation needed |
| Draft post → user edits → auto-save | Direct save to `BlogPostDocument` (unchanged from today) |
| Published post → user edits → auto-save | Save to `BlogPostDraft`, live document untouched |
| Published post → user clicks Save | Promote draft → update live doc → delete draft |
| Published post → user clicks Publish | Promote draft → update live doc → publish → delete draft |
| Published post → user clicks Unpublish | Unpublish live doc, discard draft |
| User opens published post in editor | Load draft (if exists) or load live doc as base |
| Draft exists but user discards changes | Delete draft; next publish uses live doc content |
| Concurrent edit — two users | No special handling; last draft write wins (same as PageEditor) |

### 7. Files to Create/Modify

| File | Action |
|------|--------|
| `src/Aero.Cms.Core.Entities/BlogPostDraft.cs` | **Create** — new draft entity |
| `src/Aero.Cms.Modules.Blog/BlogModule.cs` | Modify — add Marten config for BlogPostDraft |
| `src/Aero.Cms.Modules.Headless/Areas/Api/v1/BlogApi.cs` | Modify — add 3 draft endpoints |
| `src/Aero.Cms.Abstractions/Http/Clients/BlogClient.cs` | Modify — add draft DTOs + client methods |
| `src/Aero.Cms.Shared/Pages/Manager/PostEditor/PostEditor.razor.cs` | Modify — draft-aware save/publish logic |
| `src/Aero.Cms.AeroCmsClientBase` (or base) | Modify — add PutAsync/DeleteAsync helpers if missing |
| `docs/blog-draft-implementation.md` | **This file** |

### 8. Implementation Order

1. Create `BlogPostDraft` entity + Marten config
2. Add draft DTOs to `BlogClient.cs`
3. Add draft endpoints to `BlogApi.cs`
4. Update `PostEditor.razor.cs` with draft-aware save/publish
5. Test: new post, draft post, published post, publish promotion, discard
