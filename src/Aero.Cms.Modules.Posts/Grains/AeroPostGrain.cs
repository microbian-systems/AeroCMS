using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using FlakeId;
using Marten;
using Microsoft.Extensions.Logging;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Posts.Grains;

/// <summary>
/// Orleans grain for blog post management — wraps Marten persistence behind
/// the <see cref="IAeroPostActor"/> interface.
///
/// Ported from <see cref="MartenBlogPostContentService"/>.
/// </summary>
public sealed class AeroPostGrain : AeroActor, IAeroPostActor
{
    private const string BlogCacheTag = "blog-index";

    private readonly IDocumentStore _store;
    private readonly IMessageBus? _bus;
    private readonly IFusionCache? _cache;
    private PostViewModel _state = new();

    public AeroPostGrain(
        ILogger<AeroActor> log,
        IDocumentStore store,
        IMessageBus? bus = null,
        IFusionCache? cache = null)
        : base(log)
    {
        _store = store;
        _bus = bus;
        _cache = cache;
    }

    // ── IHaveState<PostViewModel> ────────────────────────────────────

    public Task<PostViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    public Task UpdateStateAsync(PostViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── Blog-specific methods (ported from MartenBlogPostContentService) ──

    /// <summary>Get all posts with paging and optional search.</summary>
    public async Task<(List<PostViewModel> Items, long TotalCount)> GetAllPostsAsync(
        long siteId, int skip, int take, string? search, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        var cacheKey = BuildCacheKey(siteId, $"list:{skip}:{take}:{NormalizeCachePart(search)}");
        var cached = await TryGetCacheAsync<BlogPostListCacheEntry>(cacheKey, ct);
        if (cached is not null)
            return (cached.Items.Select(MapToViewModel).ToList(), cached.TotalCount);

        var query = session.Query<PostDocument>().Where(x => x.SiteId == siteId);

        IQueryable<PostDocument> filteredQuery = query;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            filteredQuery = query.Where(x => x.Title.ToLower().Contains(s) || x.Slug.ToLower().Contains(s));
        }

        var stats = new global::Marten.Linq.QueryStatistics();
        var posts = await ((global::Marten.Linq.IMartenQueryable<PostDocument>)filteredQuery)
            .OrderByDescending(x => x.CreatedOn)
            .Stats(out stats)
            .Skip(skip)
            .Take(take)
            .ToListAsync(token: ct);

        await SetCacheAsync(cacheKey, new BlogPostListCacheEntry(posts.ToList(), stats.TotalResults), ct);
        return (posts.Select(MapToViewModel).ToList(), stats.TotalResults);
    }

    /// <summary>Load a post by ID.</summary>
    public async Task<PostViewModel?> LoadAsync(long id, long siteId, CancellationToken ct)
    {
        await using var session = _store.QuerySession();

        var cacheKey = BuildCacheKey(siteId, $"id:{id}");
        var cached = await TryGetCacheAsync<PostDocument>(cacheKey, ct);
        if (cached is not null)
            return MapToViewModel(cached);

        var document = await session.LoadAsync<PostDocument>(id, ct);
        if (document is null || document.SiteId != siteId)
            return null;

        await SetCacheAsync(cacheKey, document, ct);
        return MapToViewModel(document);
    }

    /// <summary>Find a published post by slug.</summary>
    public async Task<PostViewModel?> FindBySlugAsync(string slug, long siteId, CancellationToken ct)
    {
        await using var session = _store.QuerySession();

        var cacheKey = BuildCacheKey(siteId, $"slug:{NormalizeCachePart(slug)}");
        var cached = await TryGetCacheAsync<PostDocument>(cacheKey, ct);
        if (cached is not null)
            return MapToViewModel(cached);

        var reservation = await session.Query<ContentSlugDocument>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == siteId &&
                string.Equals(slug, x.Slug, StringComparison.CurrentCultureIgnoreCase), token: ct);

        if (reservation is null || reservation.OwnerType != ContentSlugOwnerType.BlogPost)
            return null;

        var document = await session.LoadAsync<PostDocument>(reservation.OwnerId, ct);
        if (document is null)
            return null;

        if (document.PublicationState != ContentPublicationState.Published)
            return null;

        await SetCacheAsync(cacheKey, document, ct);
        return MapToViewModel(document);
    }

    /// <summary>Save (create or update) a blog post.</summary>
    public async Task<AeroRequestResponse<PostViewModel>> SavePostAsync(PostViewModel vm, long siteId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        var post = MapToDocument(vm);
        post.SiteId = siteId;

        var existingPost = await session.LoadAsync<PostDocument>(post.Id, ct);

        // Preserve existing content when incoming Content is empty
        // (Content is stripped from PostViewModel to avoid Orleans BlockBase serialization errors)
        if (existingPost is not null && post.Content.Count == 0)
            post.Content = existingPost.Content;

        await ContentSlugReservation.ReserveAsync(
            session,
            post.Id,
            ContentSlugOwnerType.BlogPost,
            post.Slug,
            post.SiteId,
            existingPost?.Slug,
            ct);

        var now = DateTimeOffset.UtcNow;
        var existingCreatedAtUtc = existingPost?.CreatedOn;
        post.CreatedOn = existingCreatedAtUtc is null || existingCreatedAtUtc == default ? now : existingCreatedAtUtc.Value;
        post.ModifiedOn = now;
        post.ModifiedBy = "system";
        post.PublishedOn = post.PublicationState == ContentPublicationState.Published
            ? existingPost?.PublishedOn ?? now
            : null;

        session.Store(post);
        await session.SaveChangesAsync(ct);
        await PublishContentUpdatedAsync(post, existingPost?.Slug, ct);

        return Ok(MapToViewModel(post));
    }

    /// <summary>Delete a blog post by ID.</summary>
    public async Task<AeroRequestResponse<PostViewModel>> DeletePostAsync(long id, long siteId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        var post = await session.LoadAsync<PostDocument>(id, ct);
        if (post is null || post.SiteId != siteId)
            return Fail($"Blog post with id '{id}' not found or access denied");

        var reservation = await session.Query<ContentSlugDocument>()
            .FirstOrDefaultAsync(x => x.OwnerId == id && x.OwnerType == ContentSlugOwnerType.BlogPost && x.SiteId == siteId, token: ct);

        if (reservation is not null)
            session.Delete(reservation);

        session.Delete<PostDocument>(id);
        await session.SaveChangesAsync(ct);
        await PublishContentUpdatedAsync(post, post.Slug, ct);

        return Ok(MapToViewModel(post));
    }

    /// <summary>Publish a blog post.</summary>
    public async Task<AeroRequestResponse<PostViewModel>> PublishPostAsync(long id, long siteId, CancellationToken ct)
    {
        var vm = await LoadAsync(id, siteId, ct);
        if (vm is null)
            return Fail($"Blog post with id '{id}' not found or access denied");

        vm.PublicationState = ContentPublicationState.Published;
        vm.PublishedOn = DateTimeOffset.UtcNow;

        return await SavePostAsync(vm, siteId, ct);
    }

    /// <summary>Unpublish a blog post (set to Draft).</summary>
    public async Task<AeroRequestResponse<PostViewModel>> UnpublishPostAsync(long id, long siteId, CancellationToken ct)
    {
        var vm = await LoadAsync(id, siteId, ct);
        if (vm is null)
            return Fail($"Blog post with id '{id}' not found or access denied");

        vm.PublicationState = ContentPublicationState.Draft;
        vm.PublishedOn = null;

        return await SavePostAsync(vm, siteId, ct);
    }

    // ── ICruddable<PostViewModel, long> ──────────────────────────────

    public async Task<AeroRequestResponse<PostViewModel>> GetByIdAsync(long id, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<PostDocument>(id, ct);

        return doc is not null
            ? Ok(MapToViewModel(doc))
            : NotFound($"Post {id} not found");
    }

    public async Task<AeroRequestResponse<PostViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
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

        await using var session = _store.LightweightSession();
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

        await using var session = _store.LightweightSession();
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

    // ── Cache helpers (ported from MartenBlogPostContentService) ─────

    private string BuildCacheKey(long siteId, string suffix)
        => $"cms:blog:{siteId}:{suffix}";

    private async Task<T?> TryGetCacheAsync<T>(string key, CancellationToken ct) where T : class
    {
        if (_cache is null) return null;
        var cached = await _cache.TryGetAsync<T>(key, token: ct);
        return cached.HasValue ? cached.Value : null;
    }

    private Task SetCacheAsync<T>(string key, T value, CancellationToken ct) where T : class
        => _cache is null
            ? Task.CompletedTask
            : _cache.SetAsync(key, value, tags: [BlogCacheTag], token: ct).AsTask();

    private Task PublishContentUpdatedAsync(PostDocument post, string? oldSlug, CancellationToken ct)
        => _bus is null
            ? Task.CompletedTask
            : _bus.PublishAsync(new BlogPostContentUpdatedEvent(post.Id, post.SiteId, post.Slug, oldSlug)).AsTask();

    private static string NormalizeCachePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "_" : value.Trim().Trim('/').ToLowerInvariant();

    private sealed record BlogPostListCacheEntry(List<PostDocument> Items, long TotalCount);

    // ── AeroRequestResponse helpers ────────────────────────────────────

    private static AeroRequestResponse<PostViewModel> Ok(PostViewModel vm)
        => new(vm, new PostErrorViewModel());

    private static AeroRequestResponse<PostViewModel> NotFound(string msg)
        => new(new PostViewModel(), new PostErrorViewModel { Message = msg });

    private static AeroRequestResponse<PostViewModel> Fail(string msg)
        => new(new PostViewModel(), new PostErrorViewModel { Message = msg });
}
