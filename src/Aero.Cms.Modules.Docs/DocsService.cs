using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Core.Http;
using Microsoft.Extensions.Logging;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Content service for docs — mirrors <see cref="Aero.Cms.Modules.Pages.MartenPageContentService"/>.
/// Constructed per operation by <see cref="Grains.AeroDocsGrain"/> with
/// an explicit <see cref="IDocumentSession"/> and <see cref="ISiteContext"/>.
/// Also usable as a scoped service for Razor pages within the HTTP request scope.
/// </summary>
public sealed class DocsContentService : IDocsService
{
    private readonly IDocumentSession _session;
    private readonly IBlockService _blockService;
    private readonly IMessageBus _bus;
    private readonly ISiteContext _siteContext;
    private readonly ILogger<DocsContentService> _logger;
    private readonly string? _actor;
    private readonly IFusionCache? _cache;
    private const string DocsCacheTag = "docs-index";

    public DocsContentService(
        IDocumentSession session,
        IBlockService blockService,
        IMessageBus bus,
        ISiteContext siteContext,
        ILogger<DocsContentService> logger,
        string? actor = null,
        IFusionCache? cache = null)
    {
        _session = session;
        _blockService = blockService;
        _bus = bus;
        _siteContext = siteContext;
        _logger = logger;
        _actor = actor;
        _cache = cache;
    }

    // ── CRUD ─────────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey("all");
            var cached = await TryGetCacheAsync<DocsPageCollectionCacheEntry>(cacheKey, ct);
            if (cached is not null)
                return Ok<IReadOnlyList<DocsPage>, AeroError>(cached.Items);

            var docs = await _session.Query<DocsPage>()
                .Where(x => x.SiteId == _siteContext.SiteId)
                .OrderBy(x => x.Order)
                .ToListAsync(ct);

            await SetCacheAsync(cacheKey, new DocsPageCollectionCacheEntry(docs.ToList()), ct);
            return Ok<IReadOnlyList<DocsPage>, AeroError>(docs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all docs for site {SiteId}", _siteContext.SiteId);
            return Fail<IReadOnlyList<DocsPage>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    // ─────────── Published (compiled queries) ───────────────────────────

    public async Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetPublishedAsync(CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey("published");
            var cached = await TryGetCacheAsync<DocsPageCollectionCacheEntry>(cacheKey, ct);
            if (cached is not null)
                return Ok<IReadOnlyList<DocsPage>, AeroError>(cached.Items);

            var docs = await _session.QueryAsync(
                new Queries.DocsPublishedBySiteIdQuery { SiteId = _siteContext.SiteId }, ct);
            var list = docs.ToList();

            await SetCacheAsync(cacheKey, new DocsPageCollectionCacheEntry(list), ct);
            return Ok<IReadOnlyList<DocsPage>, AeroError>(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get published docs for site {SiteId}", _siteContext.SiteId);
            return Fail<IReadOnlyList<DocsPage>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<(IReadOnlyList<DocsPage> Items, long TotalCount), AeroError>> GetPagedAsync(int skip, int take, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"paged:{skip}:{take}");
            var cached = await TryGetCacheAsync<DocsPagePagedCacheEntry>(cacheKey, ct);
            if (cached is not null)
                return Ok<(IReadOnlyList<DocsPage> Items, long TotalCount), AeroError>((cached.Items, cached.TotalCount));

            var siteId = _siteContext.SiteId;
            var items = await _session.QueryAsync(
                new Queries.DocsPublishedBySiteIdPagedQuery { SiteId = siteId, Skip = skip, Take = take }, ct);
            var totalCount = await _session.QueryAsync(
                new Queries.DocsPublishedCountBySiteIdQuery { SiteId = siteId }, ct);
            var itemsList = items.ToList();

            await SetCacheAsync(cacheKey, new DocsPagePagedCacheEntry(itemsList, totalCount), ct);
            return Ok<(IReadOnlyList<DocsPage> Items, long TotalCount), AeroError>((itemsList, totalCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get paged docs (skip={Skip}, take={Take})", skip, take);
            return Fail<(IReadOnlyList<DocsPage> Items, long TotalCount), AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    // ── Slug lookup ─────────────────────────────────────────────────────

    public async Task<Result<DocsPage?, AeroError>> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"slug:{NormalizeCachePart(slug)}");
            var cached = await TryGetCacheAsync<DocsPage>(cacheKey, ct);
            if (cached is not null)
                return Ok<DocsPage?, AeroError>(cached);

            var doc = await _session.Query<DocsPage>()
                .FirstOrDefaultAsync(x => x.SiteId == _siteContext.SiteId && x.Slug == slug, ct);

            if (doc is not null)
                await SetCacheAsync(cacheKey, doc, ct);

            return Ok<DocsPage?, AeroError>(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find doc by slug {Slug}", slug);
            return Fail<DocsPage?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<DocsPage?, AeroError>> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"slug-pub:{NormalizeCachePart(slug)}");
            var cached = await TryGetCacheAsync<DocsPage>(cacheKey, ct);
            if (cached is not null)
                return Ok<DocsPage?, AeroError>(cached);

            var doc = await _session.QueryAsync(
                new Queries.DocsPublishedBySlugQuery { SiteId = _siteContext.SiteId, Slug = slug }, ct);

            if (doc is not null)
                await SetCacheAsync(cacheKey, doc, ct);

            return Ok<DocsPage?, AeroError>(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find published doc by slug {Slug}", slug);
            return Fail<DocsPage?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<DocsPage?, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"id:{id}");
            var cached = await TryGetCacheAsync<DocsPage>(cacheKey, ct);
            if (cached is not null)
                return Ok<DocsPage?, AeroError>(cached);

            var doc = await _session.LoadAsync<DocsPage>(id, ct);
            if (doc is null || doc.SiteId != _siteContext.SiteId)
                return Ok<DocsPage?, AeroError>(null);

            await SetCacheAsync(cacheKey, doc, ct);
            return Ok<DocsPage?, AeroError>(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load doc {DocId}", id);
            return Fail<DocsPage?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <summary>
    /// Save (create or update) a docs page. Handles slug reservation, timestamp
    /// stamping, and Wolverine event publishing for cache eviction.
    /// </summary>
    public async Task<Result<DocsPage, AeroError>> SaveAsync(DocsPage page, CancellationToken ct = default)
    {
        try
        {
            var existing = await _session.LoadAsync<DocsPage>(page.Id, ct);
            var oldSlug = existing?.Slug;
            page.SiteId = _siteContext.SiteId;

            var now = DateTimeOffset.UtcNow;
            page.ModifiedOn = now;
            page.ModifiedBy = _actor ?? "system";

            _session.Store(page);
            await _session.SaveChangesAsync(ct);

            var isNew = existing is null;
            if (isNew)
                await _bus.PublishAsync(new DocViewModelCreated(MapToViewModel(page), $"Doc created: {page.Slug}"));
            else
                await _bus.PublishAsync(new DocViewModelUpdated(MapToViewModel(page), $"Doc updated: {page.Slug}"));

            await _bus.PublishAsync(new DocsPageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, oldSlug));

            _logger.LogInformation("Saved doc {DocId}: {Title} (slug={Slug})", page.Id, page.Title, page.Slug);
            return Ok<DocsPage, AeroError>(page);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save doc {DocId}", page.Id);
            return Fail<DocsPage, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var page = await _session.LoadAsync<DocsPage>(id, ct);
            if (page is null || page.SiteId != _siteContext.SiteId)
                return Fail<bool, AeroError>(AeroError.NotFoundError($"Doc with id '{id}' not found or access denied"));

            var slug = page.Slug;
            _session.Delete<DocsPage>(id);
            await _session.SaveChangesAsync(ct);

            await _bus.PublishAsync(new DocViewModelDeleted(MapToViewModel(page), $"Doc deleted: {page.Slug}"));
            await _bus.PublishAsync(new DocsPageContentUpdatedEvent(page.Id, page.SiteId, slug, slug));

            _logger.LogInformation("Deleted doc {DocId}: {Slug}", id, slug);
            return Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete doc {DocId}", id);
            return Fail<bool, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    // ── Batch load ─────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetByIdsAsync(long[] ids, CancellationToken ct = default)
    {
        try
        {
            var docs = await _session.Query<DocsPage>()
                .Where(x => ids.Contains(x.Id))
                .ToListAsync(ct);
            return Ok<IReadOnlyList<DocsPage>, AeroError>(docs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load docs by ids");
            return Fail<IReadOnlyList<DocsPage>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    // ── Request-based CRUD (thin mapping, delegates to SaveAsync) ──────────

    public async Task<Result<DocsPage, AeroError>> CreateAsync(CreateDocRequest request, CancellationToken ct = default)
    {
        try
        {
            var doc = new DocsPage
            {
                Id = Snowflake.NewId(),
                SiteId = request.SiteId,
                Title = request.Title,
                Slug = request.Slug,
                Summary = request.Summary,
                SeoTitle = request.SeoTitle,
                SeoDescription = request.SeoDescription,
                MarkdownContent = request.Markdown ?? request.Content,
                PublicationState = request.PublicationState
            };

            var validationResult = await ValidateAsync(doc, ct);
            if (validationResult is Result<bool, AeroError>.Failure vf)
                return Fail<DocsPage, AeroError>(vf.Error);

            return await SaveAsync(doc, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create doc '{Title}'", request.Title);
            return Fail<DocsPage, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<DocsPage, AeroError>> UpdateAsync(long id, UpdateDocRequest request, CancellationToken ct = default)
    {
        try
        {
            var doc = await _session.LoadAsync<DocsPage>(id, ct);
            if (doc is null || doc.SiteId != _siteContext.SiteId)
                return Fail<DocsPage, AeroError>(AeroError.NotFoundError($"Doc with id '{id}' not found or access denied"));

            doc.Title = request.Title;
            doc.Slug = request.Slug;
            doc.Summary = request.Summary;
            doc.SeoTitle = request.SeoTitle;
            doc.SeoDescription = request.SeoDescription;
            doc.MarkdownContent = request.Markdown ?? request.Content;
            doc.PublicationState = request.PublicationState;

            return await SaveAsync(doc, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update doc {DocId}", id);
            return Fail<DocsPage, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    // ── Tree ──────────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"children:{parentId}");
            var cached = await TryGetCacheAsync<DocsPageCollectionCacheEntry>(cacheKey, ct);
            if (cached is not null)
                return Ok<IReadOnlyList<DocsPage>, AeroError>(cached.Items);

            var children = await _session.Query<DocsPage>()
                .Where(x => x.SiteId == _siteContext.SiteId && x.ParentId == parentId)
                .OrderBy(x => x.Order)
                .ToListAsync(ct);

            await SetCacheAsync(cacheKey, new DocsPageCollectionCacheEntry(children.ToList()), ct);
            return Ok<IReadOnlyList<DocsPage>, AeroError>(children);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get children for parent {ParentId}", parentId);
            return Fail<IReadOnlyList<DocsPage>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <summary>
    /// Gets top-level spaces — children of the virtual root doc (Slug == "docs", ParentId == null).
    /// </summary>
    public async Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetTopLevelCategoriesAsync(CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey("top-level-categories");
            var cached = await TryGetCacheAsync<DocsPageCollectionCacheEntry>(cacheKey, ct);
            if (cached is not null)
                return Ok<IReadOnlyList<DocsPage>, AeroError>(cached.Items);

            var rootDoc = await _session.Query<DocsPage>()
                .FirstOrDefaultAsync(x => x.SiteId == _siteContext.SiteId && x.Slug == "docs", ct);

            if (rootDoc is null)
                return Ok<IReadOnlyList<DocsPage>, AeroError>([]);

            var children = await _session.Query<DocsPage>()
                .Where(x => x.SiteId == _siteContext.SiteId && x.ParentId == rootDoc.Id)
                .OrderBy(x => x.Order)
                .ToListAsync(ct);

            await SetCacheAsync(cacheKey, new DocsPageCollectionCacheEntry(children.ToList()), ct);
            return Ok<IReadOnlyList<DocsPage>, AeroError>(children);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get top-level docs categories for site {SiteId}", _siteContext.SiteId);
            return Fail<IReadOnlyList<DocsPage>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    // ── Publish workflow ───────────────────────────────────────────────────

    /// <summary>
    /// Publishes a docs page — sets <see cref="ContentPublicationState.Published"/>
    /// and bumps <see cref="DocsPage.PublishedVersion"/>.
    /// </summary>
    public async Task<Result<DocsPage, AeroError>> PublishAsync(long id, CancellationToken ct = default)
    {
        var page = await _session.LoadAsync<DocsPage>(id, ct);
        if (page is null || page.SiteId != _siteContext.SiteId)
            return Fail<DocsPage, AeroError>(AeroError.NotFoundError($"Doc with id '{id}' not found or access denied"));

        page.PublicationState = ContentPublicationState.Published;
        page.PublishedOn = DateTimeOffset.UtcNow;
        page.PublishedVersion++;

        return await SaveAsync(page, ct);
    }

    /// <summary>
    /// Unpublishes a docs page — sets <see cref="ContentPublicationState.Draft"/>.
    /// </summary>
    public async Task<Result<DocsPage, AeroError>> UnpublishAsync(long id, CancellationToken ct = default)
    {
        var page = await _session.LoadAsync<DocsPage>(id, ct);
        if (page is null || page.SiteId != _siteContext.SiteId)
            return Fail<DocsPage, AeroError>(AeroError.NotFoundError($"Doc with id '{id}' not found or access denied"));

        page.PublicationState = ContentPublicationState.Draft;
        page.PublishedOn = null;

        return await SaveAsync(page, ct);
    }

    // ── ViewModel Save ──────────────────────────────────────────────────────

    /// <summary>
    /// Save from a <see cref="DocViewModel"/> — loads the existing entity
    /// (if found) and applies ViewModel fields before delegating to
    /// <see cref="SaveAsync"/>.
    /// </summary>
    public async Task<Result<DocsPage, AeroError>> SaveFromViewModelAsync(DocViewModel vm, CancellationToken ct = default)
    {
        try
        {
            var existing = await _session.LoadAsync<DocsPage>(vm.Id, ct);
            var isNew = existing is null;

            var doc = isNew
                ? new DocsPage { Id = Snowflake.NewId() }
                : existing!;

            doc.SiteId = vm.SiteId;
            doc.Title = vm.Title ?? string.Empty;
            doc.Slug = vm.Slug ?? string.Empty;
            doc.Summary = vm.Summary;
            doc.MarkdownContent = vm.MarkdownContent;
            doc.SeoTitle = vm.SeoTitle;
            doc.SeoDescription = vm.SeoDescription;
            doc.PublicationState = vm.PublicationState;
            doc.PublishedOn = vm.PublishedOn;
            doc.ShowHeaderNavigation = vm.ShowHeaderNavigation;
            doc.HeaderImageUrl = vm.HeaderImageUrl;
            doc.ParentId = vm.ParentId;
            doc.Order = vm.Order;
            doc.BlockSchemaVersion = vm.BlockSchemaVersion;

            return await SaveAsync(doc, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save doc from view model {DocId}", vm.Id);
            return Fail<DocsPage, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    // ── Validation ─────────────────────────────────────────────────────────

    private async Task<Result<bool, AeroError>> ValidateAsync(DocsPage page, CancellationToken ct = default)
    {
        await Task.CompletedTask; // TODO: replace with DocsPageValidator when implemented

        if (string.IsNullOrWhiteSpace(page.Title))
            return Fail<bool, AeroError>(AeroError.ValidationError(["Title is required"]));

        if (string.IsNullOrWhiteSpace(page.Slug))
            return Fail<bool, AeroError>(AeroError.ValidationError(["Slug is required"]));

        return Ok<bool, AeroError>(true);
    }

    // ── Mapping ────────────────────────────────────────────────────────────

    private static DocViewModel MapToViewModel(DocsPage page) => new()
    {
        Id = page.Id,
        SiteId = page.SiteId,
        Slug = page.Slug,
        Title = page.Title,
        Summary = page.Summary,
        MarkdownContent = page.MarkdownContent,
        SeoTitle = page.SeoTitle,
        SeoDescription = page.SeoDescription,
        PublicationState = page.PublicationState,
        PublishedOn = page.PublishedOn,
        ShowHeaderNavigation = page.ShowHeaderNavigation,
        HeaderImageUrl = page.HeaderImageUrl,
        ParentId = page.ParentId,
        Order = page.Order,
        PublishedVersion = page.PublishedVersion,
        BlockSchemaVersion = page.BlockSchemaVersion,
        CreatedOn = page.CreatedOn,
        ModifiedOn = page.ModifiedOn,
        CreatedBy = page.CreatedBy,
        ModifiedBy = page.ModifiedBy
    };

    // ── Cache helpers ──────────────────────────────────────────────────────

    private string BuildCacheKey(string suffix)
        => $"cms:docs:{_siteContext.SiteId}:{suffix}";

    private async Task<T?> TryGetCacheAsync<T>(string key, CancellationToken ct) where T : class
    {
        if (_cache is null) return null;
        var cached = await _cache.TryGetAsync<T>(key, token: ct);
        return cached.HasValue ? cached.Value : null;
    }

    private Task SetCacheAsync<T>(string key, T value, CancellationToken ct) where T : class
        => _cache is null
            ? Task.CompletedTask
            : _cache.SetAsync(key, value, tags: [DocsCacheTag], token: ct).AsTask();

    private static string NormalizeCachePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "_" : value.Trim().Trim('/').ToLowerInvariant();

    private sealed record DocsPageCollectionCacheEntry(List<DocsPage> Items);
    private sealed record DocsPagePagedCacheEntry(List<DocsPage> Items, long TotalCount);
}
