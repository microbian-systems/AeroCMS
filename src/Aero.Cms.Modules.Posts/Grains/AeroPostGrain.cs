using Aero.Actors;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Core.Http;
using AeroDB.Sable.Pagination;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using IRequest = Aero.Core.Commands.IRequest;

namespace Aero.Cms.Modules.Posts.Grains;

/// <summary>
/// Implements the post actor contract by combining direct Sable queries with <see cref="PostContentService"/>.
/// </summary>
/// <remarks>
/// Each operation opens its own session. Service instances use a fixed site context, resolve the
/// message bus and cache optionally, and omit an HTTP context, so service audit stamping uses
/// <c>system</c>. Several convenience queries intentionally collapse service failures to empty or
/// <see langword="null"/> results; response-shaped mutation methods preserve an error message.
/// </remarks>
public sealed class AeroPostGrain : AeroActor, IAeroPostActor
{
    private readonly IDocumentStore _store;
    private readonly IServiceProvider _services;
    private PostViewModel _state = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AeroPostGrain"/> class.
    /// </summary>
    /// <param name="log">The actor logger.</param>
    /// <param name="store">The store used to open per-operation sessions.</param>
    /// <param name="services">The provider used to resolve optional bus and cache services.</param>
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

    /// <summary>
    /// Returns the current activation-local state without reading persistence.
    /// </summary>
    /// <param name="ct">A cancellation token that is not observed because the operation is synchronous.</param>
    /// <returns>The current state reference.</returns>
public Task<PostViewModel> GetStateAsync(CancellationToken ct)
        => Task.FromResult(_state);

    /// <summary>
    /// Replaces the activation-local state without persisting it.
    /// </summary>
    /// <param name="state">The state reference to retain.</param>
    /// <param name="ct">A cancellation token that is not observed because the operation is synchronous.</param>
    /// <returns>A completed task.</returns>
public Task UpdateStateAsync(PostViewModel state, CancellationToken ct)
    {
        _state = state;
        return Task.CompletedTask;
    }

    // ── Helper: manual construction of PostContentService ──

    /// <summary>
    /// Creates a site-scoped service over a caller-owned session.
    /// </summary>
    private PostContentService CreatePostService(IDocumentSession session, long siteId)
    {
        var bus = _services.GetService<IMessageBus>();
        var cache = _services.GetService<IFusionCache>();
        return new PostContentService(session, new FixedSiteContext(siteId), bus, null, cache);
    }

    // ── Blog-specific methods (delegated to PostContentService) ────

    /// <inheritdoc />
    /// <remarks>Service failures are returned as an empty page with a zero total.</remarks>
    public async Task<(List<PostViewModel> Items, long TotalCount)> GetAllPostsAsync(
        long siteId, int skip, int take, string? search, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var postService = CreatePostService(session, siteId);
        var result = await postService.GetAllPostsAsync(skip, take, search, ct);
        if (result is Result<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>.Ok ok)
            return (ok.Value.Items.Select(MapToViewModel).ToList(), ok.Value.TotalCount);
        return ([], 0);
    }

    /// <inheritdoc />
    /// <remarks>Not-found, wrong-site, and service failures are all collapsed to <see langword="null"/>.</remarks>
    public async Task<PostViewModel?> LoadAsync(long id, long siteId, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var postService = CreatePostService(session, siteId);
        var result = await postService.LoadAsync(id, ct);
        if (result is Result<PostDocument?, AeroError>.Ok { Value: not null } ok)
            return MapToViewModel(ok.Value);
        return null;
    }

    /// <inheritdoc />
    public async Task<PostViewModel?> FindBySlugAsync(string slug, long siteId, CancellationToken ct)
        => await FindBySlugAsync(slug, siteId, culture: null, ct);

    /// <inheritdoc />
    /// <remarks>Not-found, unpublished, and service failures are all collapsed to <see langword="null"/>.</remarks>
    public async Task<PostViewModel?> FindBySlugAsync(string slug, long siteId, string? culture, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var postService = CreatePostService(session, siteId);
        var result = await postService.FindBySlugAsync(slug, culture, ct);
        if (result is Result<PostDocument?, AeroError>.Ok { Value: not null } ok)
            return MapToViewModel(ok.Value);
        return null;
    }

    /// <summary>
    /// Resolves a source post, then returns the variants in its translation group.
    /// </summary>
    /// <param name="id">The identifier of any variant in the group.</param>
    /// <param name="siteId">The authorized site boundary for the source and returned variants.</param>
    /// <param name="ct">A token used to cancel persistence queries.</param>
    /// <returns>Mapped variants, or an empty list when the source or service result is unavailable.</returns>
    /// <remarks>The source must belong to <paramref name="siteId"/> before its translation group is queried.</remarks>
    public async Task<List<PostViewModel>> ListCultureVariantsAsync(long id, long siteId, CancellationToken ct)
    {
        await using var loadSession = await _store.QuerySessionAsync();
        var source = await loadSession.LoadAsync<PostDocument>(id, ct);
        if (source is null || source.SiteId != siteId)
            return [];

        var TranslationGroupId = source.TranslationGroupId ?? source.Id;

        await using var session = await _store.LightweightSessionAsync();
        var postService = CreatePostService(session, siteId);
        var result = await postService.ListCultureVariantsAsync(TranslationGroupId, ct);
        return result is Result<IReadOnlyList<PostDocument>, AeroError>.Ok ok
            ? ok.Value.Select(MapToViewModel).ToList()
            : [];
    }

    /// <summary>
    /// Creates and persists a draft culture variant of a source post.
    /// </summary>
    /// <param name="id">The source post identifier.</param>
    /// <param name="siteId">The authorized site boundary for the source and new variant.</param>
    /// <param name="culture">The target culture.</param>
    /// <param name="slug">The target slug.</param>
    /// <param name="ct">A token used to cancel persistence work.</param>
    /// <returns>The persisted variant or an error response.</returns>
    /// <remarks>The source must belong to <paramref name="siteId"/> before a variant can be created.</remarks>
    public async Task<AeroRequestResponse<PostViewModel>> ForkPostForCultureAsync(
        long id,
        long siteId,
        string culture,
        string slug,
        CancellationToken ct)
    {
        await using var loadSession = await _store.QuerySessionAsync();
        var source = await loadSession.LoadAsync<PostDocument>(id, ct);
        if (source is null || source.SiteId != siteId)
            return NotFound($"Post {id} not found");

        await using var session = await _store.LightweightSessionAsync();
        var postService = CreatePostService(session, siteId);
        var result = await postService.ForkPostForCultureAsync(id, culture, slug, ct);
        if (result is Result<PostDocument, AeroError>.Ok ok)
            return Ok(MapToViewModel(ok.Value));
        if (result is Result<PostDocument, AeroError>.Failure fail)
            return Fail(GetErrorMessage(fail.Error));
        return Fail("Failed to create post translation");
    }

    /// <inheritdoc />
    /// <remarks>
    /// A missing series is assigned by a separate session before the post session is opened. General
    /// series creation and post persistence therefore do not share a transaction.
    /// </remarks>
    public async Task<AeroRequestResponse<PostViewModel>> SavePostAsync(PostViewModel vm, long siteId, CancellationToken ct)
    {
        if (vm.SiteId != 0 && vm.SiteId != siteId)
            return NotFound($"Post {vm.Id} not found");

        await using (var query = await _store.QuerySessionAsync())
        {
            var existing = await query.LoadAsync<PostDocument>(vm.Id, ct);
            if (existing is not null && existing.SiteId != siteId)
                return NotFound($"Post {vm.Id} not found");
        }

        vm.SeriesId ??= await EnsureGeneralSeriesIdAsync(siteId, ct);

        var post = MapToDocument(vm);
        post.SiteId = siteId;

        await using var session = await _store.LightweightSessionAsync();
        var postService = CreatePostService(session, siteId);
        var result = await postService.SaveAsync(post, ct);
        if (result is Result<PostDocument, AeroError>.Ok ok)
            return Ok(MapToViewModel(ok.Value));
        if (result is Result<PostDocument, AeroError>.Failure fail)
            return Fail(GetErrorMessage(fail.Error));
        return Fail("Failed to save post");
    }

    /// <inheritdoc />
    /// <remarks>The method loads before deleting so a successful response can return the deleted snapshot.</remarks>
    public async Task<AeroRequestResponse<PostViewModel>> DeletePostAsync(long id, long siteId, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
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

    /// <inheritdoc />
    /// <remarks>Loading and saving use separate sessions; a missing or wrong-site post produces a failure response.</remarks>
    public async Task<AeroRequestResponse<PostViewModel>> PublishPostAsync(long id, long siteId, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var postService = CreatePostService(session, siteId);
        var result = await postService.SetPublicationStateAsync(id, ContentPublicationState.Published, ct);
        return result is Result<PostDocument, AeroError>.Ok ok
            ? Ok(MapToViewModel(ok.Value))
            : Fail($"Blog post with id '{id}' not found or access denied");
    }

    /// <inheritdoc />
    /// <remarks>Loading and saving use separate sessions; a missing or wrong-site post produces a failure response.</remarks>
    public async Task<AeroRequestResponse<PostViewModel>> UnpublishPostAsync(long id, long siteId, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var postService = CreatePostService(session, siteId);
        var result = await postService.SetPublicationStateAsync(id, ContentPublicationState.Draft, ct);
        return result is Result<PostDocument, AeroError>.Ok ok
            ? Ok(MapToViewModel(ok.Value))
            : Fail($"Blog post with id '{id}' not found or access denied");
    }

    // ── ICruddable<PostViewModel, long> (direct IDocumentStore access) ──────

    /// <summary>
    /// Loads a post by identifier without applying a site or publication-state filter.
    /// </summary>
    /// <param name="id">The persisted post identifier.</param>
    /// <param name="ct">A token used to cancel the query.</param>
    /// <returns>The mapped post or a not-found response.</returns>
public async Task<AeroRequestResponse<PostViewModel>> GetByIdAsync(long id, CancellationToken ct)
        => Fail("A site scope is required to load a post by identifier");

    /// <inheritdoc />
public async Task<AeroRequestResponse<PostViewModel>> GetByIdAsync(long id, long siteId, CancellationToken ct)
    {
        var post = await LoadAsync(id, siteId, ct);
        return post is not null
            ? Ok(post)
            : NotFound($"Post {id} not found");
    }

    /// <summary>
    /// Loads matching posts without a site filter and returns only the first mapped document.
    /// </summary>
    /// <param name="ids">The post identifiers to query.</param>
    /// <param name="ct">A token used to cancel the query.</param>
    /// <returns>A successful response containing the first match or an empty view model.</returns>
public async Task<AeroRequestResponse<PostViewModel>> GetByIdsAsync(long[] ids, CancellationToken ct)
        => Fail("A site scope is required to load posts by identifier");

    /// <summary>
    /// Adapts a recognized actor create request to a new post and delegates persistence to <see cref="SavePostAsync"/>.
    /// </summary>
    /// <param name="request">A post create request; other request types produce a failure response.</param>
    /// <param name="ct">A token used to cancel persistence work.</param>
    /// <returns>The saved post or an error response.</returns>
public async Task<AeroRequestResponse<PostViewModel>> CreateAsync(IRequest request, CancellationToken ct)
        => Fail("A site-scoped post save operation is required");

    /// <summary>
    /// Loads a post by identifier, applies actor-request fields, and delegates persistence.
    /// </summary>
    /// <param name="request">A post update request; other request types produce a failure response.</param>
    /// <param name="ct">A token used to cancel persistence work.</param>
    /// <returns>The saved post, a not-found response, or a request-type failure response.</returns>
public async Task<AeroRequestResponse<PostViewModel>> UpdateAsync(IRequest request, CancellationToken ct)
        => Fail("A site-scoped post save operation is required");

    /// <summary>
    /// Resolves the post's site from persistence and delegates its deletion.
    /// </summary>
    /// <param name="request">A post delete request; other request types produce a failure response.</param>
    /// <param name="ct">A token used to cancel persistence work.</param>
    /// <returns>The deleted post, a not-found response, or an error response.</returns>
public async Task<AeroRequestResponse<PostViewModel>> DeleteAsync(IRequest request, CancellationToken ct)
        => Fail("A site-scoped post delete operation is required");

    // ── ICanFindBySite<PostViewModel, long> ──────────────────────────

    /// <summary>
    /// Returns the first item from a requested site page.
    /// </summary>
    /// <param name="siteId">The owning site identifier.</param>
    /// <param name="page">The one-based page number.</param>
    /// <param name="rows">The maximum number of posts queried.</param>
    /// <param name="ct">A token used to cancel the query.</param>
    /// <returns>The first post in the page, or a not-found response when the page is empty or the query fails.</returns>
public async Task<AeroRequestResponse<PostViewModel>> GetBySiteIdAsync(
        long siteId, int page = 1, int rows = 10, CancellationToken ct = default)
    {
        var (items, _) = await GetAllPostsAsync(siteId, (page - 1) * rows, rows, search: null, ct);
        return items.Count > 0 ? Ok(items[0]) : NotFound("No posts found for site");
    }

    // ── ICanFindBySlug<PostViewModel, long> ──────────────────────────

    /// <summary>
    /// Finds a published post by slug within a site and returns an actor response.
    /// </summary>
    /// <param name="siteId">The owning site identifier.</param>
    /// <param name="slug">The route slug.</param>
    /// <param name="ct">A token used to cancel the query.</param>
    /// <returns>The post or a not-found response.</returns>
public async Task<AeroRequestResponse<PostViewModel>> GetBySlugAsync(long siteId, string slug, CancellationToken ct)
    {
        var vm = await FindBySlugAsync(slug, siteId, ct);
        return vm is not null ? Ok(vm) : NotFound($"Post with slug '{slug}' not found");
    }

    /// <summary>
    /// Adapts the string site-key contract to the numeric site identifier used by persistence.
    /// </summary>
    Task<AeroRequestResponse<PostViewModel>> ICanFindBySlug<PostViewModel, string>.GetBySlugAsync(string siteId, string slug, CancellationToken ct)
    {
        if (long.TryParse(siteId, out var id))
            return GetBySlugAsync(id, slug, ct);
        return Task.FromResult(Fail($"Invalid site ID: {siteId}"));
    }

    // ── Additional blog query methods ────────────────────────────────

    /// <inheritdoc />
    public async Task<(List<PostViewModel> Items, long TotalCount)> GetLatestPostsAsync(long siteId, int count, CancellationToken ct)
        => await GetLatestPostsAsync(siteId, count, culture: null, ct);

    /// <inheritdoc />
    /// <remarks>Service failures are returned as an empty list with a zero total.</remarks>
public async Task<(List<PostViewModel> Items, long TotalCount)> GetLatestPostsAsync(long siteId, int count, string? culture, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var postService = CreatePostService(session, siteId);
        var result = await postService.GetLatestPostsAsync(count, culture, ct);
        if (result is Result<IReadOnlyList<PostDocument>, AeroError>.Ok ok)
        {
            var items = ok.Value.Select(MapToViewModel).ToList();
            return (items, items.Count);
        }
        return ([], 0);
    }

    /// <inheritdoc />
    public async Task<(List<PostViewModel> Items, int TotalCount, int TotalPages, bool HasNext, bool HasPrev)> GetPagedPostsAsync(
        long siteId, int page, int pageSize, int skipFromLatest, CancellationToken ct)
        => await GetPagedPostsAsync(siteId, page, pageSize, skipFromLatest, culture: null, ct);

    /// <inheritdoc />
    /// <remarks>Service failures are returned as an empty page with all metadata set to zero or false.</remarks>
public async Task<(List<PostViewModel> Items, int TotalCount, int TotalPages, bool HasNext, bool HasPrev)> GetPagedPostsAsync(
        long siteId, int page, int pageSize, int skipFromLatest, string? culture, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
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

    /// <inheritdoc />
    /// <remarks>Service failures are collapsed to an empty dictionary.</remarks>
    public async Task<Dictionary<long, string>> GetTagNameMapAsync(long siteId, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var postService = CreatePostService(session, siteId);
        var result = await postService.GetAllTagsAsync(ct);
        if (result is Result<IReadOnlyList<Tag>, AeroError>.Ok ok)
            return ok.Value.ToDictionary(t => t.Id, t => t.Name);
        return [];
    }

    /// <inheritdoc />
    /// <remarks>
    /// The underlying author document has no site field, so <paramref name="siteId"/> only scopes the
    /// constructed service and does not constrain the author lookup.
    /// </remarks>
    public async Task<(string? Name, string? Bio, string? AvatarUrl)?> GetPostAuthorSummaryAsync(long siteId, long authorId, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var postService = CreatePostService(session, siteId);
        var result = await postService.GetAuthorAsync(authorId, ct);
        if (result is Result<PostAuthor?, AeroError>.Ok { Value: not null } ok)
            return (ok.Value.Name, ok.Value.Bio, ok.Value.AvatarUrl);
        return null;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Copies a persisted document into the actor model, substituting empty collections and system audit names.
    /// </summary>
    private static PostViewModel MapToViewModel(PostDocument d)
    {
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
            MarkdownContent = d.MarkdownContent,
            TagIds = d.TagIds ?? [],
            CategoryIds = d.CategoryIds ?? [],
            AuthorId = d.AuthorId,
            ImageUrl = d.ImageUrl,
            Likes = d.Likes,
            Culture = d.Culture,
            TranslationGroupId = d.TranslationGroupId,
            SeriesId = d.SeriesId,
            CreatedOn = d.CreatedOn,
            ModifiedOn = d.ModifiedOn,
            CreatedBy = d.CreatedBy ?? "system",
            ModifiedBy = d.ModifiedBy ?? "system"
        };
    }

    /// <summary>
    /// Copies actor state into a persistence document without normalizing culture or reserving the slug.
    /// </summary>
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
            SeriesId = vm.SeriesId,
            CreatedOn = vm.CreatedOn,
            ModifiedOn = vm.ModifiedOn,
            CreatedBy = vm.CreatedBy ?? "system",
            ModifiedBy = vm.ModifiedBy ?? "system"
        };

        doc.MarkdownContent = vm.MarkdownContent;

        return doc;
    }

    /// <summary>
    /// Gets or persists the site's <c>general</c> series in an independent session.
    /// </summary>
    private async Task<long> EnsureGeneralSeriesIdAsync(long siteId, CancellationToken ct)
    {
        await using var session = await _store.LightweightSessionAsync();
        var general = await session.Query<Models.Series>()
            .Where(x => x.SiteId == siteId && x.Slug == "general")
            .FirstOrDefaultAsync(ct);

        if (general is not null)
            return general.Id;

        general = new Models.Series
        {
            Id = Snowflake.NewId(),
            SiteId = siteId,
            Name = "General",
            Slug = "general",
            Description = "Default blog series"
        };

        session.Store(general);
        await session.SaveChangesAsync(ct);
        return general.Id;
    }

    // ── AeroRequestResponse helpers ────────────────────────────────────

    /// <summary>
    /// Creates a successful actor response.
    /// </summary>
    private static AeroRequestResponse<PostViewModel> Ok(PostViewModel vm)
        => new(vm, new PostErrorViewModel());

    /// <summary>
    /// Creates a not-found response with an empty post payload.
    /// </summary>
    private static AeroRequestResponse<PostViewModel> NotFound(string msg)
        => new(new PostViewModel(), new PostErrorViewModel { Message = msg });

    /// <summary>
    /// Creates a failure response with an empty post payload.
    /// </summary>
    private static AeroRequestResponse<PostViewModel> Fail(string msg)
        => new(new PostViewModel(), new PostErrorViewModel { Message = msg });

    /// <summary>Extracts a human-readable message from any <see cref="AeroError"/> subtype.</summary>
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

    /// <summary>
    /// Supplies the actor-selected site as both the site and tenant boundary for a delegated service.
    /// </summary>
    /// <param name="siteId">The identifier exposed as both site and tenant.</param>
    private sealed class FixedSiteContext(long siteId) : ISiteContext
    {
        /// <summary>
        /// Gets the fixed site identifier.
        /// </summary>
public long SiteId { get; } = siteId;
        /// <summary>
        /// Gets the same identifier as <see cref="SiteId"/>.
        /// </summary>
public long TenantId { get; } = siteId;
    }
}
