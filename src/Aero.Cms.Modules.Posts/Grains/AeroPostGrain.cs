using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Core.Http;
using Marten.Pagination;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Posts.Grains;

/// <summary>
/// Orleans grain for blog post management — wraps Marten persistence behind
/// the <see cref="IAeroPostActor"/> interface.
///
/// Uses manual-construction delegation: opens sessions from <see cref="IDocumentStore"/>,
/// builds <see cref="PostContentService"/> inline with a <see cref="FixedSiteContext"/>,
/// and delegates each operation to the service.
/// </summary>
public sealed class AeroPostGrain : AeroActor, IAeroPostActor
{
    private readonly IDocumentStore _store;
    private readonly IServiceProvider _services;
    private PostViewModel _state = new();

    public AeroPostGrain(
        ILogger<AeroActor> log,
        IDocumentStore store,
        IServiceProvider services)
        : base(log)
    {
        _store = store;
        _services = services;
    }

    // ── IHaveState<PostViewModel> ────────────────────────────────────

    public Task<PostViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    public Task UpdateStateAsync(PostViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── Helper: manual construction of MartenBlogPostContentService ──

    private PostContentService CreatePostService(IDocumentSession session, long siteId)
    {
        var bus = _services.GetService<IMessageBus>();
        var cache = _services.GetService<IFusionCache>();
        return new PostContentService(session, new FixedSiteContext(siteId), bus, null, cache);
    }

    // ── Blog-specific methods (delegated to MartenBlogPostContentService) ────

    /// <summary>Get all posts with paging and optional search.</summary>
    public async Task<(List<PostViewModel> Items, long TotalCount)> GetAllPostsAsync(
        long siteId, int skip, int take, string? search, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var postService = CreatePostService(session, siteId);
        var result = await postService.GetAllPostsAsync(skip, take, search, ct);
        if (result is Result<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>.Ok ok)
            return (ok.Value.Items.Select(MapToViewModel).ToList(), ok.Value.TotalCount);
        return ([], 0);
    }

    /// <summary>Load a post by ID within a site (returns null if not found or wrong site).</summary>
    public async Task<PostViewModel?> LoadAsync(long id, long siteId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var postService = CreatePostService(session, siteId);
        var result = await postService.LoadAsync(id, ct);
        if (result is Result<PostDocument?, AeroError>.Ok { Value: not null } ok)
            return MapToViewModel(ok.Value);
        return null;
    }

    /// <summary>Find a published post by slug within a site.</summary>
    public async Task<PostViewModel?> FindBySlugAsync(string slug, long siteId, CancellationToken ct)
        => await FindBySlugAsync(slug, siteId, culture: null, ct);

    public async Task<PostViewModel?> FindBySlugAsync(string slug, long siteId, string? culture, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var postService = CreatePostService(session, siteId);
        var result = await postService.FindBySlugAsync(slug, culture, ct);
        if (result is Result<PostDocument?, AeroError>.Ok { Value: not null } ok)
            return MapToViewModel(ok.Value);
        return null;
    }

    public async Task<List<PostViewModel>> ListCultureVariantsAsync(long id, CancellationToken ct)
    {
        await using var loadSession = _store.QuerySession();
        var source = await loadSession.LoadAsync<PostDocument>(id, ct);
        if (source is null)
            return [];

        var TranslationGroupId = source.TranslationGroupId ?? source.Id;

        await using var session = _store.LightweightSession();
        var postService = CreatePostService(session, source.SiteId);
        var result = await postService.ListCultureVariantsAsync(TranslationGroupId, ct);
        return result is Result<IReadOnlyList<PostDocument>, AeroError>.Ok ok
            ? ok.Value.Select(MapToViewModel).ToList()
            : [];
    }

    public async Task<AeroRequestResponse<PostViewModel>> ForkPostForCultureAsync(long id, string culture, string slug, CancellationToken ct)
    {
        await using var loadSession = _store.QuerySession();
        var source = await loadSession.LoadAsync<PostDocument>(id, ct);
        if (source is null)
            return NotFound($"Post {id} not found");

        await using var session = _store.LightweightSession();
        var postService = CreatePostService(session, source.SiteId);
        var result = await postService.ForkPostForCultureAsync(id, culture, slug, ct);
        if (result is Result<PostDocument, AeroError>.Ok ok)
            return Ok(MapToViewModel(ok.Value));
        if (result is Result<PostDocument, AeroError>.Failure fail)
            return Fail(GetErrorMessage(fail.Error));
        return Fail("Failed to create post translation");
    }

    /// <summary>Save (create or update) a blog post, handling slug reservation + cache eviction.</summary>
    public async Task<AeroRequestResponse<PostViewModel>> SavePostAsync(PostViewModel vm, long siteId, CancellationToken ct)
    {
        var post = MapToDocument(vm);
        post.SiteId = siteId;

        // Preserve existing content when incoming Content is empty
        // (Content is stripped from PostViewModel to avoid Orleans BlockBase serialization errors)
        if (post.Content.Count == 0)
        {
            await using var loadSession = _store.LightweightSession();
            var loadService = CreatePostService(loadSession, siteId);
            var loadResult = await loadService.LoadAsync(post.Id, ct);
            if (loadResult is Result<PostDocument?, AeroError>.Ok { Value: not null } existing)
                post.Content = existing.Value.Content;
        }

        await using var session = _store.LightweightSession();
        var postService = CreatePostService(session, siteId);
        var result = await postService.SaveAsync(post, ct);
        if (result is Result<PostDocument, AeroError>.Ok ok)
            return Ok(MapToViewModel(ok.Value));
        if (result is Result<PostDocument, AeroError>.Failure fail)
            return Fail(GetErrorMessage(fail.Error));
        return Fail("Failed to save post");
    }

    /// <summary>Delete a blog post by ID, handling slug cleanup + cache eviction.</summary>
    public async Task<AeroRequestResponse<PostViewModel>> DeletePostAsync(long id, long siteId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var postService = CreatePostService(session, siteId);
        var loadResult = await postService.LoadAsync(id, ct);
        if (loadResult is not Result<PostDocument?, AeroError>.Ok { Value: not null } found)
            return Fail($"Blog post with id '{id}' not found or access denied");

        var deleteResult = await postService.DeleteAsync(id, ct);
        if (deleteResult is Result<bool, AeroError>.Ok)
            return Ok(MapToViewModel(found.Value));
        if (deleteResult is Result<bool, AeroError>.Failure fail)
            return Fail(GetErrorMessage(fail.Error));
        return Fail("Failed to delete post");
    }

    /// <summary>Publish a blog post by ID.</summary>
    public async Task<AeroRequestResponse<PostViewModel>> PublishPostAsync(long id, long siteId, CancellationToken ct)
    {
        var vm = await LoadAsync(id, siteId, ct);
        if (vm is null)
            return Fail($"Blog post with id '{id}' not found or access denied");

        vm.PublicationState = ContentPublicationState.Published;
        vm.PublishedOn = DateTimeOffset.UtcNow;

        return await SavePostAsync(vm, siteId, ct);
    }

    /// <summary>Unpublish a blog post by ID (set to Draft).</summary>
    public async Task<AeroRequestResponse<PostViewModel>> UnpublishPostAsync(long id, long siteId, CancellationToken ct)
    {
        var vm = await LoadAsync(id, siteId, ct);
        if (vm is null)
            return Fail($"Blog post with id '{id}' not found or access denied");

        vm.PublicationState = ContentPublicationState.Draft;
        vm.PublishedOn = null;

        return await SavePostAsync(vm, siteId, ct);
    }

    // ── ICruddable<PostViewModel, long> (direct IDocumentStore access) ──────

    public async Task<AeroRequestResponse<PostViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = _store.QuerySession();
        var doc = await session.LoadAsync<PostDocument>(id, ct);
        return doc is not null
            ? Ok(MapToViewModel(doc))
            : NotFound($"Post {id} not found");
    }

    public async Task<AeroRequestResponse<PostViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = _store.QuerySession();
        var docs = await session.Query<PostDocument>()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);
        var primary = docs.Count > 0 ? MapToViewModel(docs[0]) : new PostViewModel();
        return Ok(primary);
    }

    public async Task<AeroRequestResponse<PostViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not CreatePostRequest create)
            return Fail("Expected CreatePostRequest");

        var id = Snowflake.NewId();

        var vm = new PostViewModel
        {
            Id = id,
            SiteId = create.SiteId,
            Title = create.Title,
            Slug = create.Slug,
            Excerpt = create.Summary,
            SeoTitle = create.SeoTitle,
            SeoDescription = create.SeoDescription,
            PublicationState = create.PublicationState,
            CreatedOn = DateTimeOffset.UtcNow,
            CreatedBy = "system",
            ModifiedBy = "system"
        };

        return await SavePostAsync(vm, create.SiteId, ct);
    }

    public async Task<AeroRequestResponse<PostViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
    {
        if (request is not UpdatePostRequest update)
            return Fail("Expected UpdatePostRequest");

        await using var session = _store.QuerySession();
        var existing = await session.LoadAsync<PostDocument>(update.Id, ct);
        if (existing is null)
            return NotFound($"Post {update.Id} not found");

        existing.Title = update.Title;
        existing.Slug = update.Slug;
        existing.Excerpt = update.Summary;
        existing.SeoTitle = update.SeoTitle;
        existing.SeoDescription = update.SeoDescription;
        existing.PublicationState = update.PublicationState;

        var vm = MapToViewModel(existing);
        return await SavePostAsync(vm, existing.SiteId, ct);
    }

    public async Task<AeroRequestResponse<PostViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
    {
        if (request is not DeletePostRequest delete)
            return Fail("Expected DeletePostRequest");

        await using var session = _store.QuerySession();
        var existing = await session.LoadAsync<PostDocument>(delete.Id, ct);
        if (existing is null)
            return NotFound($"Post {delete.Id} not found");

        return await DeletePostAsync(delete.Id, existing.SiteId, ct);
    }

    // ── ICanFindBySite<PostViewModel, long> ──────────────────────────

    public async Task<AeroRequestResponse<PostViewModel>> GetBySiteIdAsync(
        long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        var (items, _) = await GetAllPostsAsync(siteId, (page - 1) * rows, rows, search: null, ct);
        return items.Count > 0 ? Ok(items[0]) : NotFound("No posts found for site");
    }

    // ── ICanFindBySlug<PostViewModel, long> ──────────────────────────

    public async Task<AeroRequestResponse<PostViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
    {
        var vm = await FindBySlugAsync(slug, siteId, ct);
        return vm is not null ? Ok(vm) : NotFound($"Post with slug '{slug}' not found");
    }

    Task<AeroRequestResponse<PostViewModel>> ICanFindBySlug<PostViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugAsync(id, slug, ct);
        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

    // ── Additional blog query methods ────────────────────────────────

    /// <summary>Get latest N published posts for a site.</summary>
    public async Task<(List<PostViewModel> Items, long TotalCount)> GetLatestPostsAsync(long siteId, int count, CancellationToken ct)
        => await GetLatestPostsAsync(siteId, count, culture: null, ct);

    public async Task<(List<PostViewModel> Items, long TotalCount)> GetLatestPostsAsync(long siteId, int count, string? culture, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var postService = CreatePostService(session, siteId);
        var result = await postService.GetLatestPostsAsync(count, culture, ct);
        if (result is Result<IReadOnlyList<PostDocument>, AeroError>.Ok ok)
        {
            var items = ok.Value.Select(MapToViewModel).ToList();
            return (items, items.Count);
        }
        return ([], 0);
    }

    /// <summary>Get paged published posts, skipping the first N latest posts.</summary>
    public async Task<(List<PostViewModel> Items, int TotalCount, int TotalPages, bool HasNext, bool HasPrev)> GetPagedPostsAsync(
        long siteId, int page, int pageSize, int skipFromLatest, CancellationToken ct)
        => await GetPagedPostsAsync(siteId, page, pageSize, skipFromLatest, culture: null, ct);

    public async Task<(List<PostViewModel> Items, int TotalCount, int TotalPages, bool HasNext, bool HasPrev)> GetPagedPostsAsync(
        long siteId, int page, int pageSize, int skipFromLatest, string? culture, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var postService = CreatePostService(session, siteId);
        var result = await postService.GetPagedPostsAsync(page, pageSize, skipFromLatest, culture, ct);
        if (result is Result<IPagedList<PostDocument>, AeroError>.Ok ok)
        {
            var pagedList = ok.Value;
            return (
                pagedList.Select(MapToViewModel).ToList(),
                (int)pagedList.TotalItemCount,
                (int)pagedList.PageCount,
                pagedList.HasNextPage,
                pagedList.HasPreviousPage
            );
        }
        return ([], 0, 0, false, false);
    }

    /// <summary>Get all tag IDs mapped to their display names for a site.</summary>
    public async Task<Dictionary<long, string>> GetTagNameMapAsync(long siteId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var postService = CreatePostService(session, siteId);
        var result = await postService.GetAllTagsAsync(ct);
        if (result is Result<IReadOnlyList<Tag>, AeroError>.Ok ok)
            return ok.Value.ToDictionary(t => t.Id, t => t.Name);
        return [];
    }

    /// <summary>Get a summary of a post author for a site.</summary>
    public async Task<(string? Name, string? Bio, string? AvatarUrl)?> GetPostAuthorSummaryAsync(long siteId, long authorId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var postService = CreatePostService(session, siteId);
        var result = await postService.GetAuthorAsync(authorId, ct);
        if (result is Result<PostAuthor?, AeroError>.Ok { Value: not null } ok)
            return (ok.Value.Name, ok.Value.Bio, ok.Value.AvatarUrl);
        return null;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static PostViewModel MapToViewModel(PostDocument d)
    {
        // Extract markdown content as strings — Orleans can't serialize BlockBase.
        // The PostEditor reads these strings back into its text editor.
        var content = new List<object>();
        foreach (var block in d.Content)
        {
            if (block is MarkdownBlock md && !string.IsNullOrWhiteSpace(md.Content))
                content.Add(md.Content);
        }

        return new()
        {
            Id = d.Id,
            SiteId = d.SiteId,
            Slug = d.Slug,
            Title = d.Title,
            Excerpt = d.Excerpt,
            SeoTitle = d.SeoTitle,
            SeoDescription = d.SeoDescription,
            PublishedOn = d.PublishedOn,
            PublicationState = d.PublicationState,
            Content = content,
            TagIds = d.TagIds ?? [],
            CategoryIds = d.CategoryIds ?? [],
            AuthorId = d.AuthorId,
            ImageUrl = d.ImageUrl,
            Likes = d.Likes,
            Culture = d.Culture,
            TranslationGroupId = d.TranslationGroupId,
            CreatedOn = d.CreatedOn,
            ModifiedOn = d.ModifiedOn,
            CreatedBy = d.CreatedBy ?? "system",
            ModifiedBy = d.ModifiedBy ?? "system"
        };
    }

    private static PostDocument MapToDocument(PostViewModel vm)
    {
        var doc = new PostDocument
        {
            Id = vm.Id,
            SiteId = vm.SiteId,
            Slug = vm.Slug ?? string.Empty,
            Title = vm.Title ?? string.Empty,
            Excerpt = vm.Excerpt,
            SeoTitle = vm.SeoTitle,
            SeoDescription = vm.SeoDescription,
            PublishedOn = vm.PublishedOn,
            PublicationState = vm.PublicationState,
            TagIds = vm.TagIds ?? [],
            CategoryIds = vm.CategoryIds ?? [],
            AuthorId = vm.AuthorId,
            ImageUrl = vm.ImageUrl,
            Likes = vm.Likes,
            Culture = vm.Culture,
            TranslationGroupId = vm.TranslationGroupId,
            CreatedOn = vm.CreatedOn,
            ModifiedOn = vm.ModifiedOn,
            CreatedBy = vm.CreatedBy ?? "system",
            ModifiedBy = vm.ModifiedBy ?? "system"
        };

        // Content: List<object> → List<BlockBase>
        // Strings in Content represent MarkdownContent carried across Orleans wire
        // (actual BlockBase instances can't be serialized by Orleans)
        doc.Content = [];
        if (vm.Content is { Count: > 0 })
        {
            foreach (var item in vm.Content)
            {
                if (item is BlockBase block)
                {
                    doc.Content.Add(block);
                }
                else if (item is string markdown && !string.IsNullOrWhiteSpace(markdown))
                {
                    doc.Content.Add(new MarkdownBlock
                    {
                        Id = Snowflake.NewId(),
                        Content = markdown,
                        Order = doc.Content.Count
                    });
                }
            }
        }

        return doc;
    }

    // ── AeroRequestResponse helpers ────────────────────────────────────

    private static AeroRequestResponse<PostViewModel> Ok(PostViewModel vm)
        => new(vm, new PostErrorViewModel());

    private static AeroRequestResponse<PostViewModel> NotFound(string msg)
        => new(new PostViewModel(), new PostErrorViewModel { Message = msg });

    private static AeroRequestResponse<PostViewModel> Fail(string msg)
        => new(new PostViewModel(), new PostErrorViewModel { Message = msg });

    /// <summary>Extract a human-readable message from any <see cref="AeroError"/> subtype.</summary>
    private static string GetErrorMessage(AeroError error) => error switch
    {
        AeroError.Error e => e.msg,
        AeroError.NotFound e => e.msg,
        AeroError.Conflict e => e.msg,
        AeroError.Database e => e.msg,
        AeroError.Unauthorized e => e.msg,
        AeroError.Forbidden e => e.msg,
        AeroError.Timeout e => e.msg,
        AeroError.InvalidRequest e => e.msg,
        AeroError.BadRequest e => e.msg,
        AeroError.Exists e => e.msg,
        AeroError.NullReferro e => e.msg,
        AeroError.Cancelled e => e.msg,
        AeroError.NotAllowed e => e.msg,
        AeroError.Configuration e => e.msg,
        AeroError.Validation e => string.Join("; ", e.Errors),
        AeroError.HttpRequest e => e.msg ?? "HTTP request error",
        _ => error.ToString()
    };

    // ── FixedSiteContext ─────────────────────────────────────────────

    private sealed class FixedSiteContext(long siteId) : ISiteContext
    {
        public long SiteId { get; } = siteId;
        public long TenantId { get; } = siteId;
    }
}
