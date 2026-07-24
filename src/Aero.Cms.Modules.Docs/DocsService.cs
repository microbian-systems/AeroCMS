using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using static Aero.Core.Railway.Prelude;

namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Implements site-scoped documentation reads, writes, translations, and publication transitions.
/// </summary>
/// <remarks>
/// Reads can use an optional FusionCache instance and all cache keys include the current site.
/// Mutations commit the document session before publishing Wolverine notifications, so notification
/// failures can be returned after the persisted state has changed. This service does not perform
/// user authorization; callers must establish a trustworthy <see cref="ISiteContext"/> and actor.
/// Most methods catch cancellation together with other exceptions and return a database failure.
/// </remarks>
public sealed class DocsContentService : IDocsService
{
    private readonly IDocumentSession _session;
    private readonly IMessageBus _bus;
    private readonly ISiteContext _siteContext;
    private readonly ILogger<DocsContentService> _logger;
    private readonly string? _actor;
    private readonly IFusionCache? _cache;
    private const string DocsCacheTag = "docs-index";

    /// <summary>
    /// Initializes a new instance of the <see cref="DocsContentService"/> class.
    /// </summary>
    /// <param name="session">The operation-scoped document session.</param>
    /// <param name="bus">The message bus used after successful mutations.</param>
    /// <param name="siteContext">The site boundary applied to service operations.</param>
    /// <param name="logger">The diagnostic logger.</param>
    /// <param name="actor">The audit actor, or <see langword="null"/> to use <c>system</c>.</param>
    /// <param name="cache">The optional site-keyed read cache.</param>
public DocsContentService(
        IDocumentSession session,
        IMessageBus bus,
        ISiteContext siteContext,
        ILogger<DocsContentService> logger,
        string? actor = null,
        IFusionCache? cache = null)
    {
        _session = session;
        _bus = bus;
        _siteContext = siteContext;
        _logger = logger;
        _actor = actor;
        _cache = cache;
    }

    // ── CRUD ─────────────────────────────────────────────────────────────

/// <inheritdoc />
public async Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey("all");
            var cached = await TryGetCacheAsync<DocsPageCollectionCacheEntry>(cacheKey, ct);
            if (cached is not null && cached.Items.All(x => x.SiteId == _siteContext.SiteId))
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

/// <inheritdoc />
public Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetPublishedAsync(CancellationToken ct = default)
        => GetPublishedAsync(null, ct);

/// <inheritdoc />
public async Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetPublishedAsync(string? culture, CancellationToken ct = default)
    {
        try
        {
            var currentCulture = GetCurrentCulture(culture);
            var cacheKey = BuildCacheKey($"published:{currentCulture}");
            var cached = await TryGetCacheAsync<DocsPageCollectionCacheEntry>(cacheKey, ct);
            if (cached is not null && cached.Items.All(x => x.SiteId == _siteContext.SiteId && x.PublicationState == ContentPublicationState.Published && x.Culture == currentCulture))
                return Ok<IReadOnlyList<DocsPage>, AeroError>(cached.Items);

            var docs = await _session.Query<DocsPage>()
                .Where(x => x.SiteId == _siteContext.SiteId
                         && x.Culture == currentCulture
                         && x.PublicationState == ContentPublicationState.Published)
                .OrderBy(x => x.Order)
                .ToListAsync(ct);
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

/// <inheritdoc />
public async Task<Result<(IReadOnlyList<DocsPage> Items, long TotalCount), AeroError>> GetPagedAsync(int skip, int take, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"paged:{skip}:{take}");
            var cached = await TryGetCacheAsync<DocsPagePagedCacheEntry>(cacheKey, ct);
            if (cached is not null && cached.Items.All(x => x.SiteId == _siteContext.SiteId))
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

/// <inheritdoc />
public async Task<Result<DocsPage?, AeroError>> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"slug:{NormalizeCachePart(slug)}");
            var cached = await TryGetCacheAsync<DocsPage>(cacheKey, ct);
            if (cached is not null && cached.SiteId == _siteContext.SiteId)
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

/// <inheritdoc />
public Task<Result<DocsPage?, AeroError>> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
        => GetPublishedBySlugAsync(slug, null, ct);

/// <inheritdoc />
public async Task<Result<DocsPage?, AeroError>> GetPublishedBySlugAsync(string slug, string? culture, CancellationToken ct = default)
    {
        try
        {
            var currentCulture = GetCurrentCulture(culture);
            var cacheKey = BuildCacheKey($"slug-pub:{currentCulture}:{NormalizeCachePart(slug)}");
            var cached = await TryGetCacheAsync<DocsPage>(cacheKey, ct);
            if (cached is not null && cached.SiteId == _siteContext.SiteId && cached.PublicationState == ContentPublicationState.Published)
                return Ok<DocsPage?, AeroError>(cached);

            var doc = await FindPublishedBySlugAndCultureAsync(slug, currentCulture, ct);
            if (doc is null)
            {
                var defaultCulture = await GetSiteDefaultCultureAsync(ct);
                if (!string.Equals(currentCulture, defaultCulture, StringComparison.OrdinalIgnoreCase))
                    doc = await FindPublishedBySlugAndCultureAsync(slug, defaultCulture, ct);
            }

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

/// <inheritdoc />
public async Task<Result<DocsPage?, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"id:{id}");
            var cached = await TryGetCacheAsync<DocsPage>(cacheKey, ct);
            if (cached is not null && cached.SiteId == _siteContext.SiteId)
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

    /// <inheritdoc />
    public async Task<Result<DocsPage, AeroError>> SaveAsync(DocsPage page, CancellationToken ct = default)
    {
        try
        {
            if (page.SiteId != 0 && page.SiteId != _siteContext.SiteId)
                return Fail<DocsPage, AeroError>(AeroError.NotFoundError($"Doc with id '{page.Id}' not found or access denied"));

            var existing = page.Id == 0 ? null : await _session.LoadAsync<DocsPage>(page.Id, ct);
            if (existing is not null && existing.SiteId != _siteContext.SiteId)
                return Fail<DocsPage, AeroError>(AeroError.NotFoundError($"Doc with id '{page.Id}' not found or access denied"));

            if (page.Id == 0)
                page.Id = Snowflake.NewId();
            if (page.ParentId is { } parentId && !await BelongsToCurrentSiteAsync(parentId, ct))
                return Fail<DocsPage, AeroError>(AeroError.NotFoundError("Parent doc not found or access denied"));
            if (page.TranslationGroupId is { } groupId && !await TranslationGroupBelongsToCurrentSiteAsync(groupId, ct))
                return Fail<DocsPage, AeroError>(AeroError.NotFoundError("Translation group not found or access denied"));

            var oldSlug = existing?.Slug;
            page.SiteId = _siteContext.SiteId;
            page.Culture = NormalizeCulture(page.Culture);
            page.TranslationGroupId = existing?.TranslationGroupId ?? page.TranslationGroupId ?? page.Id;

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

/// <inheritdoc />
public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var page = await _session.LoadAsync<DocsPage>(id, ct);
            if (page is null || page.SiteId != _siteContext.SiteId)
                return Fail<bool, AeroError>(AeroError.NotFoundError($"Doc with id '{id}' not found or access denied"));

            var child = await _session.Query<DocsPage>()
                .FirstOrDefaultAsync(
                    candidate => candidate.SiteId == _siteContext.SiteId
                        && candidate.ParentId == id,
                    ct);
            if (child is not null)
                return Fail<bool, AeroError>(
                    AeroError.ValidationError(["A documentation section with child sections cannot be deleted."]));

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

/// <inheritdoc />
public async Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetByIdsAsync(long[] ids, CancellationToken ct = default)
    {
        try
        {
            var siteDocs = await _session.Query<DocsPage>()
                .Where(x => x.SiteId == _siteContext.SiteId)
                .ToListAsync(ct);
            var requested = ids.ToHashSet();
            return Ok<IReadOnlyList<DocsPage>, AeroError>(siteDocs.Where(x => requested.Contains(x.Id)).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load docs by ids");
            return Fail<IReadOnlyList<DocsPage>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

/// <inheritdoc />
public async Task<Result<IReadOnlyList<DocsPage>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var source = await _session.LoadAsync<DocsPage>(id, ct);
            if (source is null || source.SiteId != _siteContext.SiteId)
                return Fail<IReadOnlyList<DocsPage>, AeroError>(AeroError.NotFoundError($"Doc with id '{id}' not found or access denied"));

            var TranslationGroupId = source.TranslationGroupId ?? source.Id;
            var docs = await _session.Query<DocsPage>()
                .Where(doc => doc.SiteId == _siteContext.SiteId && doc.TranslationGroupId == TranslationGroupId)
                .OrderBy(doc => doc.Culture)
                .ToListAsync(ct);

            return Ok<IReadOnlyList<DocsPage>, AeroError>(docs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list doc translations for {DocId}", id);
            return Fail<IReadOnlyList<DocsPage>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

/// <inheritdoc />
public async Task<Result<DocsPage, AeroError>> ForkToCultureAsync(long id, string targetCulture, string slug, CancellationToken ct = default)
    {
        try
        {
            var source = await _session.LoadAsync<DocsPage>(id, ct);
            if (source is null || source.SiteId != _siteContext.SiteId)
                return Fail<DocsPage, AeroError>(AeroError.NotFoundError($"Doc with id '{id}' not found or access denied"));

            var culture = NormalizeCulture(targetCulture);
            var TranslationGroupId = source.TranslationGroupId ?? source.Id;
            var existing = await _session.Query<DocsPage>()
                .FirstOrDefaultAsync(doc =>
                    doc.SiteId == _siteContext.SiteId
                    && doc.TranslationGroupId == TranslationGroupId
                    && doc.Culture == culture,
                    ct);

            if (existing is not null)
                return Fail<DocsPage, AeroError>(AeroError.ValidationError([$"A {culture} translation already exists."]));

            var parentResult = await ResolveTranslatedParentIdAsync(source.ParentId, culture, ct);
            if (parentResult is Result<long?, AeroError>.Failure parentFailure)
                return Fail<DocsPage, AeroError>(parentFailure.Error);
            var parentId = ((Result<long?, AeroError>.Ok)parentResult).Value;
            var now = DateTimeOffset.UtcNow;
            var fork = new DocsPage
            {
                Id = Snowflake.NewId(),
                SiteId = source.SiteId,
                TranslationGroupId = TranslationGroupId,
                Culture = culture,
                Slug = slug.Trim().Trim('/'),
                Title = source.Title,
                Summary = source.Summary,
                MarkdownContent = source.MarkdownContent,
                SeoTitle = source.SeoTitle,
                SeoDescription = source.SeoDescription,
                PublicationState = ContentPublicationState.Draft,
                PublishedOn = null,
                PublishedVersion = 0,
                ShowHeaderNavigation = source.ShowHeaderNavigation,
                HeaderImageUrl = source.HeaderImageUrl,
                ParentId = parentId,
                Order = source.Order,
                CreatedOn = now,
                ModifiedOn = now,
                ModifiedBy = _actor ?? "system"
            };

            return await SaveAsync(fork, ct);
        }
        catch (CultureNotFoundException)
        {
            return Fail<DocsPage, AeroError>(AeroError.ValidationError(["Culture must be a valid .NET culture name."]));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fork doc {DocId} to culture {Culture}", id, targetCulture);
            return Fail<DocsPage, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    // ── Request-based CRUD (thin mapping, delegates to SaveAsync) ──────────

/// <inheritdoc />
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

/// <inheritdoc />
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

/// <inheritdoc />
public Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, CancellationToken ct = default)
        => GetChildrenAsync(parentId, null, ct);

/// <inheritdoc />
public async Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, string? culture, CancellationToken ct = default)
    {
        try
        {
            var currentCulture = GetCurrentCulture(culture);
            var cacheKey = BuildCacheKey($"children:{currentCulture}:{parentId}");
            var cached = await TryGetCacheAsync<DocsPageCollectionCacheEntry>(cacheKey, ct);
            if (cached is not null && cached.Items.All(x => x.SiteId == _siteContext.SiteId))
                return Ok<IReadOnlyList<DocsPage>, AeroError>(cached.Items);

            var children = await _session.Query<DocsPage>()
                .Where(x => x.SiteId == _siteContext.SiteId
                         && x.Culture == currentCulture
                         && x.ParentId == parentId)
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

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetTopLevelCategoriesAsync(CancellationToken ct = default)
    {
        try
        {
            var cacheKey = BuildCacheKey("top-level-categories");
            var cached = await TryGetCacheAsync<DocsPageCollectionCacheEntry>(cacheKey, ct);
            if (cached is not null && cached.Items.All(x => x.SiteId == _siteContext.SiteId))
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
    /// Publishes a page in the current site and increments its published version.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="ct">The token used through persistence.</param>
    /// <returns>
    /// The persisted page, or a not-found, database, or post-commit event-publication failure.
    /// </returns>
    /// <remarks>
    /// The publication timestamp is replaced with the current UTC time. Unlike most service
    /// methods, exceptions raised before delegation to <see cref="SaveAsync"/> are not translated.
    /// </remarks>
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
    /// Returns a page in the current site to draft state.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="ct">The token used through persistence.</param>
    /// <returns>
    /// The persisted page, or a not-found, database, or post-commit event-publication failure.
    /// </returns>
    /// <remarks>
    /// The publication timestamp is cleared but the published version is retained. Unlike most
    /// service methods, exceptions raised before delegation to <see cref="SaveAsync"/> are not translated.
    /// </remarks>
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

    /// <inheritdoc />
    public async Task<Result<DocsPage, AeroError>> SaveFromViewModelAsync(DocViewModel vm, CancellationToken ct = default)
    {
        try
        {
            var existing = vm.Id == 0 ? null : await _session.LoadAsync<DocsPage>(vm.Id, ct);
            if (vm.Id != 0 && existing is null)
                return Fail<DocsPage, AeroError>(AeroError.NotFoundError($"Doc with id '{vm.Id}' not found or access denied"));
            if (existing is not null && existing.SiteId != _siteContext.SiteId)
                return Fail<DocsPage, AeroError>(AeroError.NotFoundError($"Doc with id '{vm.Id}' not found or access denied"));
            var isNew = existing is null;

            // Validate every submitted relationship before mutating a tracked existing record.
            if (vm.ParentId is { } parentId && !await BelongsToCurrentSiteAsync(parentId, ct))
                return Fail<DocsPage, AeroError>(AeroError.NotFoundError("Parent doc not found or access denied"));

            var doc = isNew
                ? new DocsPage { Id = Snowflake.NewId() }
                : existing!;

            doc.SiteId = _siteContext.SiteId;
            doc.TranslationGroupId = isNew ? doc.Id : doc.TranslationGroupId;
            doc.Culture = NormalizeCulture(vm.Culture);
            doc.Title = vm.Title ?? string.Empty;
            doc.Slug = vm.Slug ?? string.Empty;
            doc.Summary = vm.Summary;
            doc.MarkdownContent = vm.MarkdownContent;
            doc.SeoTitle = vm.SeoTitle;
            doc.SeoDescription = vm.SeoDescription;
            doc.PublicationState = isNew ? ContentPublicationState.Draft : vm.PublicationState;
            doc.PublishedOn = isNew ? null : vm.PublishedOn;
            doc.ShowHeaderNavigation = vm.ShowHeaderNavigation;
            doc.HeaderImageUrl = vm.HeaderImageUrl;
            doc.ParentId = vm.ParentId;
            doc.Order = vm.Order;
            return await SaveAsync(doc, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save doc from view model {DocId}", vm.Id);
            return Fail<DocsPage, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    // ── Validation ─────────────────────────────────────────────────────────

    /// <summary>
    /// Applies the create path's current minimal title and slug checks.
    /// </summary>
    /// <remarks>
    /// The cancellation token is currently unused apart from an already-completed task; a
    /// dedicated page validator remains deferred by the preserved TODO.
    /// </remarks>
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

    /// <summary>
    /// Copies persisted and audit fields into the event-facing view model.
    /// </summary>
    private static DocViewModel MapToViewModel(DocsPage page) => new()
    {
        Id = page.Id,
        SiteId = page.SiteId,
        TranslationGroupId = page.TranslationGroupId,
        Culture = page.Culture,
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
        CreatedOn = page.CreatedOn,
        ModifiedOn = page.ModifiedOn,
        CreatedBy = page.CreatedBy,
        ModifiedBy = page.ModifiedBy
    };

    // ── Cache helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Prefixes a cache suffix with the current site identifier.
    /// </summary>
    private string BuildCacheKey(string suffix)
        => $"cms:docs:{_siteContext.SiteId}:{suffix}";

    /// <summary>
    /// Finds an exact published site/culture/slug tuple without fallback.
    /// </summary>
    private async Task<DocsPage?> FindPublishedBySlugAndCultureAsync(string slug, string culture, CancellationToken ct)
        => await _session.Query<DocsPage>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == _siteContext.SiteId
                && x.Slug == slug
                && x.Culture == culture
                && x.PublicationState == ContentPublicationState.Published,
                ct);

    /// <summary>
    /// Loads the current site's default culture, normalizing absent values to <c>en-US</c>.
    /// </summary>
    private async Task<string> GetSiteDefaultCultureAsync(CancellationToken ct)
    {
        var site = await _session.LoadAsync<SitesModel>(_siteContext.SiteId, ct);
        return NormalizeCulture(site?.DefaultCulture);
    }

    /// <summary>
    /// Chooses the target-culture variant of a source parent when one exists.
    /// </summary>
    /// <remarks>
    /// A missing parent or translated parent leaves the original parent identifier in place.
    /// The initial parent load is identifier-only; the subsequent translation lookup is site-scoped.
    /// </remarks>
    private async Task<Result<long?, AeroError>> ResolveTranslatedParentIdAsync(long? sourceParentId, string culture, CancellationToken ct)
    {
        if (sourceParentId is not { } parentId)
            return Ok<long?, AeroError>(null);

        var parent = await _session.LoadAsync<DocsPage>(parentId, ct);
        if (parent is null || parent.SiteId != _siteContext.SiteId)
            return Fail<long?, AeroError>(AeroError.NotFoundError("Source parent not found or access denied"));

        var parentSetId = parent.TranslationGroupId ?? parent.Id;
        var translatedParent = await _session.Query<DocsPage>()
            .FirstOrDefaultAsync(doc =>
                doc.SiteId == _siteContext.SiteId
                && doc.TranslationGroupId == parentSetId
                && doc.Culture == culture,
                ct);

        return Ok<long?, AeroError>(translatedParent?.Id ?? parent.Id);
    }

    private async Task<bool> BelongsToCurrentSiteAsync(long id, CancellationToken ct)
    {
        var page = await _session.LoadAsync<DocsPage>(id, ct);
        return page is not null && page.SiteId == _siteContext.SiteId;
    }

    private async Task<bool> TranslationGroupBelongsToCurrentSiteAsync(long translationGroupId, CancellationToken ct)
        => await _session.Query<DocsPage>()
            .FirstOrDefaultAsync(x => x.SiteId == _siteContext.SiteId && (x.TranslationGroupId == translationGroupId || x.Id == translationGroupId), ct)
            is not null;

    /// <summary>
    /// Canonicalizes a .NET culture name and substitutes <c>en-US</c> for blank input.
    /// </summary>
    /// <exception cref="CultureNotFoundException">The supplied non-blank culture name is invalid.</exception>
    private static string NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return "en-US";

        return CultureInfo.GetCultureInfo(culture.Trim()).Name;
    }

    /// <summary>
    /// Normalizes an explicit culture or the process's current UI culture.
    /// </summary>
    private static string GetCurrentCulture(string? culture = null)
        => NormalizeCulture(culture ?? CultureInfo.CurrentUICulture.Name);

    /// <summary>
    /// Reads an optional FusionCache entry, treating a disabled or absent entry as a miss.
    /// </summary>
    private async Task<T?> TryGetCacheAsync<T>(string key, CancellationToken ct) where T : class
    {
        if (_cache is null) return null;
        var cached = await _cache.TryGetAsync<T>(key, token: ct);
        return cached.HasValue ? cached.Value : null;
    }

    /// <summary>
    /// Stores an optional FusionCache entry under the module-wide invalidation tag.
    /// </summary>
    private Task SetCacheAsync<T>(string key, T value, CancellationToken ct) where T : class
        => _cache is null
            ? Task.CompletedTask
            : _cache.SetAsync(key, value, tags: [DocsCacheTag], token: ct).AsTask();

    /// <summary>
    /// Trims, de-slashes, and lowercases a cache-key component, using an underscore for blank input.
    /// </summary>
    private static string NormalizeCachePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "_" : value.Trim().Trim('/').ToLowerInvariant();

    /// <summary>
    /// Wraps cached collections so an empty result remains distinguishable from a cache miss.
    /// </summary>
    private sealed record DocsPageCollectionCacheEntry(List<DocsPage> Items);

    /// <summary>
    /// Wraps a cached page and its unpaged total count.
    /// </summary>
    private sealed record DocsPagePagedCacheEntry(List<DocsPage> Items, long TotalCount);
}
