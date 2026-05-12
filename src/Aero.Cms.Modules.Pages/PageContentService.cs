
using Aero.Cms.Modules.Pages.Validators;
using Aero.Core;
using Aero.Core.Extensions;
using Wolverine;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using static Aero.Core.Railway.Prelude;


namespace Aero.Cms.Modules.Pages;

public interface IPageContentService
{
    Task<Result<PageDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<PageDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<PageDocument?, AeroError>> LoadHomepageAsync(CancellationToken cancellationToken = default);
    Task<Result<PageDocument?, AeroError>> LoadBlogListingAsync(CancellationToken cancellationToken = default);
    Task<Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>> GetAllPagesAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default);
    Task<Result<PageDocument, AeroError>> SaveAsync(PageDocument page, CancellationToken cancellationToken = default);
    Task<Result<PageDocument, AeroError>> CreateAsync(CreatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<PageDocument, AeroError>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class MartenPageContentService(
    IDocumentSession session,
    IBlockService blockService,
    IMessageBus bus,
    ISiteContext siteContext,
    ILogger<MartenPageContentService> logger,
    IHttpContextAccessor? httpContextAccessor = null,
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
            var stats = new global::Marten.Linq.QueryStatistics();
            var pages = await ((global::Marten.Linq.IMartenQueryable<PageDocument>)filteredQuery)
                .OrderBy(x => x.Title)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(token: cancellationToken);

            await SetCacheAsync(cacheKey, new PageListCacheEntry(pages.ToList(), stats.TotalResults), cancellationToken);
            return Prelude.Ok<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>((pages, stats.TotalResults));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query all pages (skip={Skip}, take={Take})", skip, take);
            return Prelude.Fail<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>(AeroError.DatabaseError(ex.Message));
        }
    }

    public async Task<Result<PageDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"slug:{NormalizeCachePart(slug)}");
            var cached = await TryGetCacheAsync<PageDocument>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Prelude.Ok<PageDocument?, AeroError>(cached);
            }

            var normalized = ContentSlugDocument.Normalize(slug);

            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x =>
                    x.SiteId == _siteContext.SiteId &&
                    string.Equals(normalized, x.NormalizedSlug, StringComparison.OrdinalIgnoreCase), token: cancellationToken);
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
            var directPage = await session.Query<PageDocument>()
                .FirstOrDefaultAsync(x =>
                    x.SiteId == _siteContext.SiteId &&
                    string.Equals(pathToMatch, x.Path, StringComparison.OrdinalIgnoreCase) &&
                    x.PublicationState == ContentPublicationState.Published, token: cancellationToken);
            if (directPage is not null)
            {
                await SetCacheAsync(cacheKey, directPage, cancellationToken);
                return Prelude.Ok<PageDocument?, AeroError>(directPage);
            }

            return Prelude.Fail<PageDocument?, AeroError>(AeroError.NotFoundError($"Page with slug '{slug}' not found"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to find page by slug {Slug}", slug);
            return Prelude.Fail<PageDocument?, AeroError>(AeroError.DatabaseError(ex.Message));
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

            if (request.EditorBlocks is { Count: > 0 })
            {
                page.Blocks = request.EditorBlocks.ToList();
                (page.LayoutRegions, page.BlockIdMap) = await ProcessEditorBlocks(request.EditorBlocks, [], cancellationToken);
            }
            else
            {
                page.Blocks = new List<EditorBlock>();
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
                previousSlug: null,
                cancellationToken: cancellationToken);

            // Start an event stream for versioning (projection handles document persistence).
            // PageCreated establishes the page; PageContentUpdated carries blocks + layout.
            session.Events.StartStream($"page-{page.Id}",
                new PageCreated(siteId, page.Title, page.Slug, parentId, order, path, depth, page.PublicationState, page.Kind));
            session.Events.Append($"page-{page.Id}", new PageContentUpdated(
                page.Title,
                page.Slug,
                page.Summary,
                page.SeoTitle,
                page.SeoDescription,
                page.LayoutRegions,
                page.Blocks,
                Kind: page.Kind,
                ShowHeaderNavigation: page.ShowHeaderNavigation,
                HeaderImageUrl: page.HeaderImageUrl,
                HideHeader: page.HideHeader,
                HideFooter: page.HideFooter,
                ShowChatAgent: page.ShowChatAgent,
                BlockIdMap: page.BlockIdMap));
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

            // Process editor blocks — map to layout regions and persist BlockBase entities
            if (request.EditorBlocks is { Count: > 0 })
            {
                page.Blocks = request.EditorBlocks.ToList();
                (page.LayoutRegions, page.BlockIdMap) = await ProcessEditorBlocks(
                    request.EditorBlocks,
                    page.BlockIdMap,
                    cancellationToken);
                logger.LogInformation("Mapped {BlockCount} editor blocks to layout regions for page {PageId}", request.EditorBlocks.Count, id);
            }
            else if (request.LayoutRegions is { Count: > 0 })
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
                    oldPublicSlug,
                    cancellationToken);
            }

            // Append update event for version history
            session.Events.Append($"page-{id}", new PageContentUpdated(
                Title: page.Title,
                Slug: page.Slug,
                Summary: page.Summary,
                SeoTitle: page.SeoTitle,
                SeoDescription: page.SeoDescription,
                LayoutRegions: page.LayoutRegions,
                Blocks: page.Blocks,
                Kind: page.Kind,
                ShowHeaderNavigation: page.ShowHeaderNavigation,
                HeaderImageUrl: page.HeaderImageUrl,
                HideHeader: page.HideHeader,
                HideFooter: page.HideFooter,
                ShowChatAgent: page.ShowChatAgent,
                BlockIdMap: page.BlockIdMap));

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
                .FirstOrDefaultAsync(x => x.OwnerId == id && x.OwnerType == ContentSlugOwnerType.Page && x.SiteId == _siteContext.SiteId, token: cancellationToken);

            if (reservation is not null)
            {
                session.Delete(reservation);
            }

            // Append delete event for version history, then soft-delete via Marten ISoftDeleted
            session.Events.Append($"page-{id}", new PageDeleted(null));
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

            // If the caller provided editor blocks but no layout regions, map them now.
            // This handles both new pages (no existing) and updates where blocks were
            // added but not yet persisted as BlockBase entities.
            if (targetPage.Blocks.Count > 0 && targetPage.LayoutRegions.Count == 0)
            {
                (targetPage.LayoutRegions, targetPage.BlockIdMap) = await ProcessEditorBlocks(
                    targetPage.Blocks, targetPage.BlockIdMap, cancellationToken);
                logger.LogInformation("Generated layout regions from {BlockCount} blocks for page {PageId}", targetPage.Blocks.Count, targetPage.Id);
            }

            var targetPublicSlug = targetPage.Path.TrimStart('/');
            await ContentSlugReservation.ReserveAsync(
                session,
                targetPage.Id,
                ContentSlugOwnerType.Page,
                targetPublicSlug,
                targetPage.SiteId,
                oldSlug,  // oldSlug is the leaf; reservation handles full-path matching
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var existingCreatedOn = existingPage?.CreatedOn;
            targetPage.CreatedOn = existingCreatedOn is null || existingCreatedOn == default ? now : existingCreatedOn.Value;
            targetPage.ModifiedOn = now;
            targetPage.ModifiedBy = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
            targetPage.PublishedOn = targetPage.PublicationState == ContentPublicationState.Published
                ? existingPage?.PublishedOn ?? now
                : null;

            // Append events so the inline projection persists the page document.
            // PageCreated for new pages; PageContentUpdated carries blocks + layout regions.
            if (existingPage is null)
            {
                session.Events.StartStream($"page-{targetPage.Id}",
                    new PageCreated(targetPage.SiteId, targetPage.Title, targetPage.Slug,
                                    targetPage.ParentId, targetPage.Order, targetPage.Path, targetPage.Depth,
                                    targetPage.PublicationState, targetPage.Kind));
            }
            session.Events.Append($"page-{targetPage.Id}", new PageContentUpdated(
                targetPage.Title,
                targetPage.Slug,
                targetPage.Summary,
                targetPage.SeoTitle,
                targetPage.SeoDescription,
                targetPage.LayoutRegions,
                targetPage.Blocks,
                Kind: targetPage.Kind,
                ShowHeaderNavigation: targetPage.ShowHeaderNavigation,
                HeaderImageUrl: targetPage.HeaderImageUrl,
                HideHeader: targetPage.HideHeader,
                HideFooter: targetPage.HideFooter,
                ShowChatAgent: targetPage.ShowChatAgent,
                BlockIdMap: targetPage.BlockIdMap));

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
    /// Maps editor blocks to <see cref="BlockBase"/> entities, persists them via
    /// <see cref="IBlockService"/>, and produces an ordered list of
    /// <see cref="LayoutRegion"/>s.  Uses <paramref name="existingMap"/> to reuse
    /// block IDs for blocks that have been saved before (keyed by
    /// <see cref="EditorBlock.EditorId"/>), avoiding orphaned duplicate blocks on
    /// every save.  Returns the updated map together with the layout regions.
    /// </summary>
    /// <param name="editorBlocks">The current set of editor blocks from the client.</param>
    /// <param name="existingMap">
    /// The previous <c>EditorId → BlockId</c> map (may be empty on first save).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple containing the generated <see cref="LayoutRegion"/> list and the
    /// updated <see cref="PageDocument.BlockIdMap"/>.
    /// </returns>
    private async Task<(List<LayoutRegion> LayoutRegions, Dictionary<string, long> BlockIdMap)>
        ProcessEditorBlocks(
            IReadOnlyList<EditorBlock> editorBlocks,
            Dictionary<string, long> existingMap,
            CancellationToken cancellationToken)
    {
        var newMap = new Dictionary<string, long>();
        var placements = new List<BlockPlacement>();
        int order = 0;

        foreach (var eb in editorBlocks)
        {
            var block = EditorBlockMapper.MapBlock(eb);
            if (block is null)
                continue;

            if (!string.IsNullOrEmpty(eb.EditorId) && existingMap.TryGetValue(eb.EditorId, out var existingBlockId))
            {
                // Block already exists — update in-place (upsert via Marten)
                block.Id = existingBlockId;
                await blockService.SaveAsync(block, cancellationToken);
                newMap[eb.EditorId] = existingBlockId;
            }
            else
            {
                // First-time save — get a fresh Snowflake ID
                block.Id = Snowflake.NewId();
                var saved = await blockService.SaveAsync(block, cancellationToken);
                newMap[eb.EditorId] = saved.Id;
            }

            placements.Add(new BlockPlacement
            {
                BlockId = block.Id,
                BlockType = block.BlockType,
                Order = order++
            });
        }

        // Put all editor blocks into a single full-width column in the "Main" region
        var column = new LayoutColumn
        {
            Width = 12,
            Blocks = placements
        };

        var regions = new List<LayoutRegion>
        {
            new()
            {
                Name = "Main",
                Order = 0,
                Columns = [column]
            }
        };

        return (regions, newMap);
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
        target.Blocks = source.Blocks;
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
