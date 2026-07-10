
using Aero.Cms.Modules.Pages.Validators;
using Aero.Core.Extensions;
using Wolverine;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Shared.Blocks.Rendering;
using Aero.Cms.Shared.Localization;
using Aero.Core.Http;
using System.Globalization;
using System.Text.Json;
using ZiggyCreatures.Caching.Fusion;
using static Aero.Core.Railway.Prelude;


namespace Aero.Cms.Modules.Pages;

public interface IPageContentService
{
    Task<Result<PageDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<PageDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<PageDocument?, AeroError>> FindBySlugAsync(string slug, string? culture, CancellationToken cancellationToken = default);
    Task<Result<PageDocument?, AeroError>> LoadHomepageAsync(CancellationToken cancellationToken = default);
    Task<Result<PageDocument?, AeroError>> LoadBlogListingAsync(CancellationToken cancellationToken = default);
    Task<Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>> GetAllPagesAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PageDocument>, AeroError>> ListCultureVariantsAsync(long TranslationGroupId, CancellationToken cancellationToken = default);
    Task<Result<PageDocument, AeroError>> ForkPageForCultureAsync(long sourcePageId, string targetCulture, string targetSlug, CancellationToken cancellationToken = default);
    Task<Result<PageDocument, AeroError>> SaveAsync(PageDocument page, CancellationToken cancellationToken = default);
    Task<Result<PageDocument, AeroError>> CreateAsync(CreatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<PageDocument, AeroError>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, bool deleteDescendants, CancellationToken cancellationToken = default);
    Task<Result<int, AeroError>> DeleteMultipleAsync(IReadOnlyList<long> ids, bool deleteDescendants, CancellationToken cancellationToken = default);
    Task<Result<int, AeroError>> DeleteTranslationGroupAsync(long translationGroupId, CancellationToken cancellationToken = default);
}

public sealed class AeroPageContentService(
    IDocumentSession session,
    IMessageBus bus,
    ISiteContext siteContext,
    ILogger<AeroPageContentService> logger,
    string? actor = null,
    IFusionCache? cache = null,
    IPageTreeService? pageTreeService = null) : IPageContentService
{
    private const string PageCacheTag = "pages-list";
    private readonly ISiteContext _siteContext = siteContext;

    public async Task<Result<PageDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"id:{id}");
            var cached = await TryGetCacheAsync<PageDocument>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Prelude.Ok<PageDocument?, AeroError>(cached);
            }

            var document = await session.LoadAsync<PageDocument>(id, cancellationToken);
            if (document is null)
            {
                return Prelude.Fail<PageDocument?, AeroError>(AeroError.NotFoundError($"Page with id '{id}' not found or access denied"));
            }

            await SetCacheAsync(cacheKey, document, cancellationToken);
            return Prelude.Ok<PageDocument?, AeroError>(document);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load page {PageId}", id);
            return Prelude.Fail<PageDocument?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public Task<Result<PageDocument?, AeroError>> LoadHomepageAsync(CancellationToken cancellationToken = default)
        => FindBySlugAsync("/", cancellationToken);

    public Task<Result<PageDocument?, AeroError>> LoadBlogListingAsync(CancellationToken cancellationToken = default)
        => FindBySlugAsync("blog", cancellationToken);

    public async Task<Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>> GetAllPagesAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"list:{skip}:{take}:{NormalizeCachePart(search)}");
            var cached = await TryGetCacheAsync<PageListCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Prelude.Ok<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>((cached.Items, cached.TotalCount));
            }

            var query = session.Query<PageDocument>().Where(x => x.SiteId == _siteContext.SiteId);

            IQueryable<PageDocument> filteredQuery = query;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                filteredQuery = query.Where(x => x.Title.ToLower().Contains(s) || x.Slug.ToLower().Contains(s));
            }
            var stats = new global::AeroDB.Sable.QueryStatistics();
            var pages = await ((global::AeroDB.Sable.ISurrealDbQueryable<PageDocument>)filteredQuery)
                .OrderBy(x => x.Title)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            await SetCacheAsync(cacheKey, new PageListCacheEntry(pages.ToList(), stats.TotalResults), cancellationToken);
            return Prelude.Ok<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>((pages, stats.TotalResults));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query all pages (skip={Skip}, take={Take})", skip, take);
            return Prelude.Fail<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public Task<Result<PageDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => FindBySlugAsync(slug, culture: null, cancellationToken);

    public async Task<Result<PageDocument?, AeroError>> FindBySlugAsync(string slug, string? culture, CancellationToken cancellationToken = default)
    {
        try
        {
            var currentCulture = GetCurrentCulture(culture);
            var routeSlug = AeroCultureRoute.StripLeadingCulture(slug);
            var cacheKey = BuildCacheKey($"slug:{currentCulture}:{NormalizeCachePart(routeSlug)}");
            var cached = await TryGetCacheAsync<PageDocument>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Prelude.Ok<PageDocument?, AeroError>(cached);
            }

            var normalized = ContentSlugDocument.Normalize(routeSlug);

            var reservation = await FindSlugReservationAsync(normalized, currentCulture, cancellationToken)
                ?? await FindDefaultCultureSlugReservationAsync(normalized, currentCulture, cancellationToken);

            if (reservation is not null && reservation.OwnerType == ContentSlugOwnerType.Page)
            {
                var document = await session.LoadAsync<PageDocument>(reservation.OwnerId, cancellationToken);
                if (document is not null)
                {
                    // Filter by published state — unpublished pages must not be publicly accessible
                    if (document.PublicationState != ContentPublicationState.Published)
                        return Prelude.Fail<PageDocument?, AeroError>(AeroError.NotFoundError($"Page with slug '{slug}' not found"));

                    await SetCacheAsync(cacheKey, document, cancellationToken);
                    return Prelude.Ok<PageDocument?, AeroError>(document);
                }
            }

            // Fallback: direct Path lookup (handles pages created without slug reservation)
            // PageDocument.Path stores the leading "/" (e.g., "/main-page/child-page")
            var pathToMatch = "/" + normalized;
            var directPage = await FindDirectPageAsync(pathToMatch, currentCulture, cancellationToken)
                ?? await FindDefaultCultureDirectPageAsync(pathToMatch, currentCulture, cancellationToken);
            if (directPage is not null)
            {
                await SetCacheAsync(cacheKey, directPage, cancellationToken);
                return Prelude.Ok<PageDocument?, AeroError>(directPage);
            }

            return Prelude.Fail<PageDocument?, AeroError>(AeroError.NotFoundError($"Page with slug '{routeSlug}' not found"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to find page by slug {Slug}", slug);
            return Prelude.Fail<PageDocument?, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<PageDocument>, AeroError>> ListCultureVariantsAsync(
        long TranslationGroupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var variants = await session.Query<PageDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId
                    && x.TranslationGroupId == TranslationGroupId
                    && x.Deleted == false)
                .OrderBy(x => x.Culture)
                .ToListAsync(cancellationToken);

            return Prelude.Ok<IReadOnlyList<PageDocument>, AeroError>(variants);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list page culture variants for Translation Group {TranslationGroupId}", TranslationGroupId);
            return Prelude.Fail<IReadOnlyList<PageDocument>, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<PageDocument, AeroError>> ForkPageForCultureAsync(
        long sourcePageId,
        string targetCulture,
        string targetSlug,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var source = await session.LoadAsync<PageDocument>(sourcePageId, cancellationToken);
            if (source is null || source.SiteId != _siteContext.SiteId)
            {
                return Prelude.Fail<PageDocument, AeroError>(
                    AeroError.NotFoundError($"Page with id '{sourcePageId}' not found or access denied"));
            }

            var normalizedCulture = ContentSlugDocument.NormalizeCulture(targetCulture);
            var supported = await IsSupportedCultureAsync(source.SiteId, normalizedCulture, cancellationToken);
            if (!supported)
            {
                return Prelude.Fail<PageDocument, AeroError>(
                    AeroError.ValidationError([$"Culture '{normalizedCulture}' is not supported by the current site."]));
            }

            var TranslationGroupId = source.TranslationGroupId ?? source.Id;
            var existingVariant = await session.Query<PageDocument>()
                .FirstOrDefaultAsync(x =>
                    x.SiteId == source.SiteId &&
                    x.TranslationGroupId == TranslationGroupId &&
                    x.Culture == normalizedCulture &&
                    x.Deleted == false,
                    cancellationToken);

            if (existingVariant is not null)
            {
                return Prelude.Fail<PageDocument, AeroError>(
                    AeroError.ConflictError($"Page already has a '{normalizedCulture}' variant."));
            }

            var fork = PageCultureForker.Fork(source, Snowflake.NewId(), normalizedCulture, targetSlug);
            if (source.ParentId is long sourceParentId)
            {
                var parentVariant = await FindParentCultureVariantAsync(sourceParentId, normalizedCulture, cancellationToken);
                if (parentVariant is not null)
                {
                    fork.ParentId = parentVariant.Id;
                    if (pageTreeService is not null)
                    {
                        var pathResult = await pageTreeService.ComputePathAsync(
                            source.SiteId,
                            parentVariant.Id,
                            fork.Slug,
                            ct: cancellationToken);

                        if (pathResult is Result<(string Path, int Depth), AeroError>.Ok pathOk)
                        {
                            fork.Path = pathOk.Value.Path;
                            fork.Depth = pathOk.Value.Depth;
                        }

                        var orderResult = await pageTreeService.GetNextSiblingOrderAsync(
                            source.SiteId,
                            parentVariant.Id,
                            cancellationToken);

                        if (orderResult is Result<int, AeroError>.Ok orderOk)
                        {
                            fork.Order = orderOk.Value;
                        }
                    }
                }
            }

            return await SaveAsync(fork, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fork page {PageId} to culture {Culture}", sourcePageId, targetCulture);
            return Prelude.Fail<PageDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<PageDocument, AeroError>> CreateAsync(CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var slug = string.IsNullOrEmpty(request.Slug)
                ? request.Title.GenerateSlug()
                : request.Slug;

            var siteId = _siteContext.SiteId;
            var page = new PageDocument
            {
                Id = Snowflake.NewId(),
                SiteId = siteId,
                Culture = SitesModel.DefaultCultureName,
                Title = request.Title,
                Slug = slug,
                Summary = request.Summary,
                SeoTitle = request.SeoTitle,
                SeoDescription = request.SeoDescription,
                PublicationState = request.PublicationState,
                ShowInNavMenu = request.ShowInNavMenu,
                ShowHeaderNavigation = request.ShowHeaderNavigation,
                HideFooter = request.HideFooter,
                ShowChatAgent = request.ShowChatAgent
            };
            page.TranslationGroupId = page.Id;
            page.RootNodes = DeserializeRootNodes(request.RootNodeJson);

            if (page.RootNodes is not { Count: > 0 })
            {
                page.LayoutRegions = request.LayoutRegions?.ToList() ?? [];
            }

            // Compute hierarchy fields (Path, Depth, Order) BEFORE validation
            // so Path is available for both the validator and slug reservation.
            var parentId = request.ParentId;
            var path = "/" + page.Slug;
            var depth = 0;
            var order = 0;

            if (parentId is not null and > 0 && pageTreeService is not null)
            {
                var pathResult = await pageTreeService.ComputePathAsync(siteId, parentId, page.Slug, ct: cancellationToken);
                if (pathResult is Result<(string Path, int Depth), AeroError>.Ok pathOk)
                {
                    path = pathOk.Value.Path;
                    depth = pathOk.Value.Depth;
                }

                var orderResult = await pageTreeService.GetNextSiblingOrderAsync(siteId, parentId, cancellationToken);
                if (orderResult is Result<int, AeroError>.Ok orderOk)
                {
                    order = orderOk.Value;
                }
            }

            page.ParentId = parentId;
            page.Path = path;
            page.Depth = depth;
            page.Order = order;

            var validationResult = await ValidatePage(page);
            if (validationResult is Result<bool, AeroError>.Failure vf)
            {
                logger.LogWarning("Validation failed creating page '{Title}' (slug={Slug}): {Errors}", request.Title, request.Slug ?? "(auto)", vf.Error);
                return Prelude.Fail<PageDocument, AeroError>(vf.Error);
            }

            // Reserve slug for public URL routing — use the full Path for
            // hierarchical pages so /parent/child resolves correctly.
            var publicSlug = page.Path.TrimStart('/'); // "/about/team" → "about/team"
            await ContentSlugReservation.ReserveAsync(
                session,
                page.Id,
                ContentSlugOwnerType.Page,
                publicSlug,
                siteId,
                page.Culture,
                previousSlug: null,
                cancellationToken: cancellationToken);

            // Start an event stream for versioning (projection handles document persistence).
            // PageCreated establishes metadata; composition pages use a coarse page-tree
            // snapshot event, while older pages can still flow through PageContentUpdated.
            session.Events.StartStream($"page-{page.Id}",
                new object[] { new PageCreated(siteId, page.Title, page.Slug, parentId, order, path, depth, page.PublicationState, page.Kind, page.Culture, page.TranslationGroupId) });

            if (page.RootNodes is { Count: > 0 })
            {
                session.Events.Append($"page-{page.Id}", new object[] { CreateCompositionDraftSaved(page) });
            }
            else
            {
                session.Events.Append($"page-{page.Id}", new object[] { new PageContentUpdated(
                    page.Title,
                    page.Slug,
                    page.Summary,
                    page.SeoTitle,
                    page.SeoDescription,
                    page.LayoutRegions,
                    null,
                    Kind: page.Kind,
                    ShowHeaderNavigation: page.ShowHeaderNavigation,
                    HeaderImageUrl: page.HeaderImageUrl,
                    HideHeader: page.HideHeader,
                    HideFooter: page.HideFooter,
                    ShowChatAgent: page.ShowChatAgent,
                    BlockIdMap: page.BlockIdMap) });
            }
            await session.SaveChangesAsync(cancellationToken);

            // Publish events via Wolverine outbox
            await bus.PublishAsync(new PageViewModelCreated(
                page.ToViewModel(), $"Page created: {page.Title}"));
            await bus.PublishAsync(new PageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, null));

            logger.LogInformation("Created page {PageId}: {Title} (slug={Slug})", page.Id, page.Title, page.Slug);
            return Prelude.Ok<PageDocument, AeroError>(page);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create page '{Title}' (slug={Slug})", request.Title, request.Slug ?? "(auto)");
            return Prelude.Fail<PageDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<PageDocument, AeroError>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);

            if (page is null || page.SiteId != _siteContext.SiteId)
            {
                return Fail<PageDocument, AeroError>(
                    AeroError.NotFoundError($"Page with id '{id}' not found or access denied"));
            }

            var oldSlug = page.Slug;

            // Apply metadata update to the document
            ApplyUpdateRequest(page, request);

            if (page.RootNodes is not { Count: > 0 } && request.LayoutRegions is { Count: > 0 })
            {
                page.LayoutRegions = request.LayoutRegions.ToList();
            }

            // Validate the updated page
            var validationResult = await ValidatePage(page);
            if (validationResult is Result<bool, AeroError>.Failure vf)
            {
                logger.LogWarning("Validation failed updating page {PageId}: {Errors}", id, vf.Error);
                return Prelude.Fail<PageDocument, AeroError>(vf.Error);
            }

            // Reserve the new slug path (if changed) — uses full Path so
            // hierarchical pages like /parent/child route correctly.
            if (oldSlug != request.Slug)
            {
                var oldPublicSlug = page.Path.TrimStart('/');
                var segments = oldPublicSlug.Split('/');
                segments[^1] = request.Slug;
                var newPublicSlug = string.Join('/', segments);

                await ContentSlugReservation.ReserveAsync(
                    session,
                    id,
                    ContentSlugOwnerType.Page,
                    newPublicSlug,
                    _siteContext.SiteId,
                    page.Culture,
                    oldPublicSlug,
                    cancellationToken);
            }

            // Append update event for version history. Native composition pages use
            // the page-tree snapshot event; legacy pages retain PageContentUpdated.
            if (page.RootNodes is { Count: > 0 })
            {
                session.Events.Append($"page-{id}", new object[] { CreateCompositionDraftSaved(page) });
            }
            else
            {
                session.Events.Append($"page-{id}", new object[] { new PageContentUpdated(
                    Title: page.Title,
                    Slug: page.Slug,
                    Summary: page.Summary,
                    SeoTitle: page.SeoTitle,
                    SeoDescription: page.SeoDescription,
                    LayoutRegions: page.LayoutRegions,
                    null,
                    Kind: page.Kind,
                    ShowHeaderNavigation: page.ShowHeaderNavigation,
                    HeaderImageUrl: page.HeaderImageUrl,
                    HideHeader: page.HideHeader,
                    HideFooter: page.HideFooter,
                    ShowChatAgent: page.ShowChatAgent,
                    BlockIdMap: page.BlockIdMap) });
            }

            await session.SaveChangesAsync(cancellationToken);

            // Publish events via Wolverine outbox
            await bus.PublishAsync(new PageViewModelUpdated(
                page.ToViewModel(), $"Page updated: {page.Title}"));

            if (page.PublicationState == ContentPublicationState.Published)
            {
                await bus.PublishAsync(new SlugUpdated(id, "Page", request.Slug, oldSlug));
            }

            await bus.PublishAsync(new PageContentUpdatedEvent(id, _siteContext.SiteId, request.Slug, oldSlug));

            logger.LogInformation("Updated page {PageId}: {Title} (slug={Slug})", id, page.Title, page.Slug);
            return Prelude.Ok<PageDocument, AeroError>(page);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update page {PageId}", id);
            return Prelude.Fail<PageDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);

            if (page is null || page.SiteId != _siteContext.SiteId)
                return Prelude.Fail<bool, AeroError>(AeroError.NotFoundError($"Page with id '{id}' not found or access denied"));

            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x => x.OwnerId == id && x.OwnerType == ContentSlugOwnerType.Page && x.SiteId == _siteContext.SiteId, cancellationToken);

            if (reservation is not null)
            {
                session.Delete(reservation);
            }

            // Append delete event for version history, then soft-delete via AeroDB ISoftDeleted
            session.Events.Append($"page-{id}", new object[] { new PageDeleted(null) });
            session.Delete(page);

            await session.SaveChangesAsync(cancellationToken);
            await bus.PublishAsync(new PageViewModelDeleted(
                page.ToViewModel(), $"Page deleted: {page.Title}"));
            await bus.PublishAsync(new PageContentUpdatedEvent(id, _siteContext.SiteId, page.Slug, page.Slug));

            logger.LogInformation("Deleted page {PageId}: {Slug}", id, page.Slug);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete page {PageId}", id);
            return Prelude.Fail<bool, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<bool, AeroError>> DeleteAsync(long id, bool deleteDescendants, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);
            if (page is null || page.SiteId != _siteContext.SiteId)
                return Prelude.Fail<bool, AeroError>(AeroError.NotFoundError($"Page with id '{id}' not found or access denied"));

            if (!deleteDescendants)
            {
                // Unpublish the parent page — children remain as-is
                var previousState = page.PublicationState;
                page.PublicationState = ContentPublicationState.Draft;
                session.Events.Append($"page-{id}", new object[] { new PageStateChanged(ContentPublicationState.Draft) });
                session.Store(page);
                await session.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Unpublished page {PageId} (was {PreviousState})", id, previousState);
                return Prelude.Ok<bool, AeroError>(true);
            }

            // Cascade delete: find all descendants by Path prefix using NgramIndex
            var prefix = page.Path == "/" ? "/" : page.Path.TrimEnd('/') + "/";
            var descendants = await session.Query<PageDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId
                    && x.Path.StartsWith(prefix)
                    && x.Deleted == false)
                .ToListAsync(cancellationToken);

            // Exclude self — for root pages the prefix "/" matches everything
            descendants = descendants.Where(d => d.Id != page.Id).ToList();

            // Delete parent + descendants (deepest first)
            var toDelete = new List<PageDocument> { page };
            toDelete.AddRange(descendants.OrderByDescending(d => d.Path.Length));

            logger.LogInformation("Cascade-deleting page {PageId} ({Path}) with {Count} descendants",
                id, page.Path, descendants.Count);

            foreach (var doc in toDelete)
            {
                var reservation = await session.Query<ContentSlugDocument>()
                    .FirstOrDefaultAsync(x => x.OwnerId == doc.Id
                        && x.OwnerType == ContentSlugOwnerType.Page
                        && x.SiteId == _siteContext.SiteId, cancellationToken);
                if (reservation is not null)
                    session.Delete(reservation);

                session.Events.Append($"page-{doc.Id}", new object[] { new PageDeleted(null) });
                session.Delete(doc);
            }

            await session.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Cascade-deleted page {PageId} and {DescendantCount} descendants", id, descendants.Count);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete page {PageId}", id);
            return Prelude.Fail<bool, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<int, AeroError>> DeleteMultipleAsync(IReadOnlyList<long> ids, bool deleteDescendants, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return Prelude.Ok<int, AeroError>(0);

        try
        {
            var idList = ids.ToList();

            // If cascade requested, expand the id list to include all descendants
            if (deleteDescendants)
            {
                var pages = await session.Query<PageDocument>()
                    .Where(x => x.SiteId == _siteContext.SiteId && idList.Contains(x.Id) && x.Deleted == false)
                    .ToListAsync(cancellationToken);

                foreach (var page in pages)
                {
                    var prefix = page.Path == "/" ? "/" : page.Path.TrimEnd('/') + "/";
                    var descendants = await session.Query<PageDocument>()
                        .Where(x => x.SiteId == _siteContext.SiteId
                            && x.Path.StartsWith(prefix)
                            && x.Deleted == false)
                        .ToListAsync(cancellationToken);
                    idList.AddRange(descendants.Where(d => d.Id != page.Id).Select(d => d.Id));
                }

                idList = idList.Distinct().ToList();
            }

            // Bulk soft-delete documents via single SQL UPDATE (ISoftDeleted)
            session.DeleteWhere<PageDocument>(x =>
                x.SiteId == _siteContext.SiteId && idList.Contains(x.Id));

            // Bulk-clean slug reservations
            session.DeleteWhere<ContentSlugDocument>(x =>
                x.SiteId == _siteContext.SiteId
                && idList.Contains(x.OwnerId)
                && x.OwnerType == ContentSlugOwnerType.Page);

            // Append PageDeleted events to each stream (audit trail)
            foreach (var id in idList)
                session.Events.Append($"page-{id}", new object[] { new PageDeleted(null) });

            await session.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Bulk-deleted {Count} pages (deleteDescendants={Cascade})", idList.Count, deleteDescendants);
            return Prelude.Ok<int, AeroError>(idList.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bulk-delete {Count} pages", ids.Count);
            return Prelude.Fail<int, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<int, AeroError>> DeleteTranslationGroupAsync(long translationGroupId, CancellationToken cancellationToken = default)
    {
        try
        {
            var variants = await session.Query<PageDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId
                    && x.TranslationGroupId == translationGroupId
                    && x.Deleted == false)
                .ToListAsync(cancellationToken);

            if (variants.Count == 0)
            {
                return Prelude.Ok<int, AeroError>(0);
            }

            var ids = variants.Select(x => x.Id).ToList();

            session.DeleteWhere<PageDocument>(x =>
                x.SiteId == _siteContext.SiteId && ids.Contains(x.Id));

            session.DeleteWhere<ContentSlugDocument>(x =>
                x.SiteId == _siteContext.SiteId
                && ids.Contains(x.OwnerId)
                && x.OwnerType == ContentSlugOwnerType.Page);

            foreach (var id in ids)
            {
                session.Events.Append($"page-{id}", new object[] { new PageDeleted(null) });
            }

            await session.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Deleted page translation group {TranslationGroupId} with {Count} variants", translationGroupId, ids.Count);
            return Prelude.Ok<int, AeroError>(ids.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete page translation group {TranslationGroupId}", translationGroupId);
            return Prelude.Fail<int, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<PageDocument, AeroError>> SaveAsync(PageDocument page, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(page);
            if (page.SiteId == 0)
            {
                page.SiteId = _siteContext.SiteId;
            }

            var validationResult = await ValidatePage(page);
            if (validationResult is Result<bool, AeroError>.Failure vf)
            {
                logger.LogWarning("Validation failed saving page {PageId}: {Errors}", page.Id, vf.Error);
                return Prelude.Fail<PageDocument, AeroError>(vf.Error);
            }

            var existingPage = await session.LoadAsync<PageDocument>(page.Id, cancellationToken);
            if (existingPage is not null && existingPage.SiteId != _siteContext.SiteId)
            {
                return Prelude.Fail<PageDocument, AeroError>(AeroError.NotFoundError($"Page with id '{page.Id}' not found or access denied"));
            }

            var targetPage = existingPage ?? page;
            var oldSlug = existingPage?.Slug;
            if (existingPage is not null && !ReferenceEquals(page, existingPage))
            {
                ApplyPersistedValues(page, existingPage);
            }

            targetPage.Culture = ContentSlugDocument.NormalizeCulture(targetPage.Culture);
            targetPage.TranslationGroupId ??= targetPage.Id;

            // Native composition is the source of truth when present. Do not
            // generate synthetic LayoutRegions for tree-backed pages; the old
            // layout manifest is only for legacy block pages.
            if (targetPage.RootNodes is { Count: > 0 })
            {
                targetPage.LayoutRegions = [];
            }

            var targetPublicSlug = targetPage.Path.TrimStart('/');
            await ContentSlugReservation.ReserveAsync(
                session,
                targetPage.Id,
                ContentSlugOwnerType.Page,
                targetPublicSlug,
                targetPage.SiteId,
                targetPage.Culture,
                oldSlug,  // oldSlug is the leaf; reservation handles full-path matching
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var existingCreatedOn = existingPage?.CreatedOn;
            targetPage.CreatedOn = existingCreatedOn is null || existingCreatedOn == default ? now : existingCreatedOn.Value;
            targetPage.ModifiedOn = now;
            targetPage.ModifiedBy = actor ?? "system";
            targetPage.PublishedOn = targetPage.PublicationState == ContentPublicationState.Published
                ? existingPage?.PublishedOn ?? now
                : null;

            // Append events so the inline projection persists the page document.
            // Native composition pages use coarse page-tree snapshot events.
            if (existingPage is null)
            {
                session.Events.StartStream($"page-{targetPage.Id}",
                    new object[] { new PageCreated(targetPage.SiteId, targetPage.Title, targetPage.Slug,
                                    targetPage.ParentId, targetPage.Order, targetPage.Path, targetPage.Depth,
                                    targetPage.PublicationState, targetPage.Kind, targetPage.Culture, targetPage.TranslationGroupId) });
            }

            if (targetPage.RootNodes is { Count: > 0 })
            {
                session.Events.Append($"page-{targetPage.Id}", new object[] { CreateCompositionDraftSaved(targetPage) });
            }
            else
            {
                session.Events.Append($"page-{targetPage.Id}", new object[] { new PageContentUpdated(
                    targetPage.Title,
                    targetPage.Slug,
                    targetPage.Summary,
                    targetPage.SeoTitle,
                    targetPage.SeoDescription,
                    targetPage.LayoutRegions,
                    null,
                    Kind: targetPage.Kind,
                    ShowHeaderNavigation: targetPage.ShowHeaderNavigation,
                    HeaderImageUrl: targetPage.HeaderImageUrl,
                    HideHeader: targetPage.HideHeader,
                    HideFooter: targetPage.HideFooter,
                    ShowChatAgent: targetPage.ShowChatAgent,
                    BlockIdMap: targetPage.BlockIdMap) });
            }

            await session.SaveChangesAsync(cancellationToken);

            // Publish rich event + keep lean events for existing subscribers
            var vm = targetPage.ToViewModel();

            if (existingPage is null)
                await bus.PublishAsync(new PageViewModelCreated(vm, $"Page saved: {targetPage.Title}"));
            else
                await bus.PublishAsync(new PageViewModelUpdated(vm, $"Page saved: {targetPage.Title}"));

            if (targetPage.PublicationState == ContentPublicationState.Published)
            {
                await bus.PublishAsync(new SlugUpdated(targetPage.Id, "Page", targetPage.Slug, oldSlug));
            }

            await bus.PublishAsync(new PageContentUpdatedEvent(targetPage.Id, targetPage.SiteId, targetPage.Slug, oldSlug));

            logger.LogInformation("Saved page {PageId}: {Title} (slug={Slug})", targetPage.Id, targetPage.Title, targetPage.Slug);
            return Prelude.Ok<PageDocument, AeroError>(targetPage);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save page {PageId} (slug={Slug})", page.Id, page.Slug);
            return Prelude.Fail<PageDocument, AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    /// <summary>
    /// Processes a NeoPageNode tree directly into a single NeoCompositionBlock for persistence.
    /// Replaces ProcessEditorBlocks in Phase 2b.
    /// </summary>
    private static PageCompositionDraftSaved CreateCompositionDraftSaved(PageDocument page)
        => new(
            PageId: page.Id,
            SiteId: page.SiteId,
            CompositionId: Snowflake.NewId(),
            Culture: page.Culture,
            ContentRevision: Snowflake.NewId(),
            Title: page.Title,
            Slug: page.Slug,
            Summary: page.Summary,
            SeoTitle: page.SeoTitle,
            SeoDescription: page.SeoDescription,
            RootNodes: page.RootNodes.Select(n => EditorNodeMemento.Capture(n).Restore()).ToList(),
            LayoutRegions: [],
            Kind: page.Kind,
            ShowHeaderNavigation: page.ShowHeaderNavigation,
            HeaderImageUrl: page.HeaderImageUrl,
            HideHeader: page.HideHeader,
            HideFooter: page.HideFooter,
            ShowChatAgent: page.ShowChatAgent,
            BlockIdMap: page.BlockIdMap);

    private static NeoPageNode DeepCloneNode(NeoPageNode source)
    {
        // Use EditorNodeMemento for deep cloning via public Capture/Restore API
        return EditorNodeMemento.Capture(source).Restore();
    }

    private static async Task<Result<bool, AeroError>> ValidatePage(PageDocument page)
    {
        var validator = new PageDocumentValidator();
        var valid = await validator.ValidateAsync(page);

        if (valid.Errors.Any())
            return Prelude.Fail<bool, AeroError>(AeroError.ValidationError(valid.Errors.Select(e => e.ErrorMessage)));

        return Prelude.Ok<bool, AeroError>(true);
    }

    private static void ApplyUpdateRequest(PageDocument page, UpdatePageRequest request)
    {
        page.Title = request.Title;
        page.Slug = request.Slug;
        page.Summary = request.Summary;
        page.SeoTitle = request.SeoTitle;
        page.SeoDescription = request.SeoDescription;
        page.PublicationState = request.PublicationState;
        page.ShowInNavMenu = request.ShowInNavMenu;
        page.ShowHeaderNavigation = request.ShowHeaderNavigation;
        page.HideFooter = request.HideFooter;
        page.ShowChatAgent = request.ShowChatAgent;
        page.RootNodes = DeserializeRootNodes(request.RootNodeJson);
    }

    private static List<NeoPageNode> DeserializeRootNodes(string? rootNodeJson)
    {
        if (string.IsNullOrWhiteSpace(rootNodeJson))
        {
            return [];
        }

        var root = JsonSerializer.Deserialize<NeoPageNode>(rootNodeJson, BlockJsonContext.Default.Options);
        if (root is null)
        {
            return [];
        }

        var nodes = string.Equals(root.CatalogId, "page.root", StringComparison.OrdinalIgnoreCase) ||
                    root.Kind == NeoPageNodeKind.Page
            ? root.Children
            : [root];

        return PageTreeLegacyNodeMigrator.CloneTree(nodes);
    }

    private static void ApplyPersistedValues(PageDocument source, PageDocument target)
    {
        target.Kind = source.Kind;
        target.Slug = source.Slug;
        target.Title = source.Title;
        target.Summary = source.Summary;
        target.SeoTitle = source.SeoTitle;
        target.SeoDescription = source.SeoDescription;
        target.LayoutRegions = source.LayoutRegions;
        target.RootNodes = source.RootNodes?
            .Select(n => DeepCloneNode(n))
            .ToList() ?? [];
        target.BlockIdMap = source.BlockIdMap;
        target.PublicationState = source.PublicationState;
        target.ShowInNavMenu = source.ShowInNavMenu;
        target.ShowHeaderNavigation = source.ShowHeaderNavigation;
        target.HeaderImageUrl = source.HeaderImageUrl;
        target.HideHeader = source.HideHeader;
        target.HideFooter = source.HideFooter;
        target.ShowChatAgent = source.ShowChatAgent;
    }

    private string BuildCacheKey(string suffix)
        => $"cms:page:{_siteContext.SiteId}:{suffix}";

    private async Task<ContentSlugDocument?> FindSlugReservationAsync(
        string normalizedSlug,
        string culture,
        CancellationToken cancellationToken)
        => await session.Query<ContentSlugDocument>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == _siteContext.SiteId &&
                x.Culture == culture &&
                string.Equals(normalizedSlug, x.NormalizedSlug, StringComparison.OrdinalIgnoreCase),
                cancellationToken);

    private async Task<ContentSlugDocument?> FindDefaultCultureSlugReservationAsync(
        string normalizedSlug,
        string culture,
        CancellationToken cancellationToken)
    {
        var defaultCulture = await GetSiteDefaultCultureAsync(cancellationToken);
        if (string.Equals(culture, defaultCulture, StringComparison.OrdinalIgnoreCase))
            return null;

        return await FindSlugReservationAsync(normalizedSlug, defaultCulture, cancellationToken);
    }

    private async Task<PageDocument?> FindDirectPageAsync(
        string pathToMatch,
        string culture,
        CancellationToken cancellationToken)
        => await session.Query<PageDocument>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == _siteContext.SiteId &&
                x.Culture == culture &&
                string.Equals(pathToMatch, x.Path, StringComparison.OrdinalIgnoreCase) &&
                x.PublicationState == ContentPublicationState.Published,
                cancellationToken);

    private async Task<PageDocument?> FindDefaultCultureDirectPageAsync(
        string pathToMatch,
        string culture,
        CancellationToken cancellationToken)
    {
        var defaultCulture = await GetSiteDefaultCultureAsync(cancellationToken);
        if (string.Equals(culture, defaultCulture, StringComparison.OrdinalIgnoreCase))
            return null;

        return await FindDirectPageAsync(pathToMatch, defaultCulture, cancellationToken);
    }

    private async Task<string> GetSiteDefaultCultureAsync(CancellationToken cancellationToken)
    {
        var site = await session.LoadAsync<SitesModel>(_siteContext.SiteId, cancellationToken);
        var defaultCulture = site?.DefaultCulture ?? SitesModel.DefaultCultureName;

        return ContentSlugDocument.NormalizeCulture(defaultCulture);
    }

    private async Task<bool> IsSupportedCultureAsync(long siteId, string culture, CancellationToken cancellationToken)
    {
        var site = await session.LoadAsync<SitesModel>(siteId, cancellationToken);
        if (site is null)
        {
            return string.Equals(culture, SitesModel.DefaultCultureName, StringComparison.OrdinalIgnoreCase);
        }

        IReadOnlyList<string> supported = site.SupportedCultures is { Count: > 0 }
            ? site.SupportedCultures
            : [site.DefaultCulture ?? SitesModel.DefaultCultureName];

        return supported
            .Select(ContentSlugDocument.NormalizeCulture)
            .Any(x => string.Equals(x, culture, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<PageDocument?> FindParentCultureVariantAsync(
        long sourceParentId,
        string targetCulture,
        CancellationToken cancellationToken)
    {
        var sourceParent = await session.LoadAsync<PageDocument>(sourceParentId, cancellationToken);
        if (sourceParent is null)
        {
            return null;
        }

        if (string.Equals(sourceParent.Culture, targetCulture, StringComparison.OrdinalIgnoreCase))
        {
            return sourceParent;
        }

        if (sourceParent.TranslationGroupId is null)
        {
            return null;
        }

        return await session.Query<PageDocument>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == sourceParent.SiteId &&
                x.TranslationGroupId == sourceParent.TranslationGroupId &&
                x.Culture == targetCulture &&
                x.Deleted == false,
                cancellationToken);
    }

    private static string GetCurrentCulture(string? culture = null)
        => ContentSlugDocument.NormalizeCulture(culture ?? CultureInfo.CurrentUICulture.Name);

    private async Task<T?> TryGetCacheAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        if (cache is null)
        {
            return null;
        }

        var cached = await cache.TryGetAsync<T>(key, token: cancellationToken);
        return cached.HasValue ? cached.Value : null;
    }

    private Task SetCacheAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class
        => cache is null
            ? Task.CompletedTask
            : cache.SetAsync(key, value, tags: [PageCacheTag], token: cancellationToken).AsTask();

    private static string NormalizeCachePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "_" : value.Trim().Trim('/').ToLowerInvariant();

    private sealed record PageListCacheEntry(List<PageDocument> Items, long TotalCount);
}
