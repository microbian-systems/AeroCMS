
using Aero.Cms.Modules.Pages.Validators;
using Aero.Core;
using Aero.Core.Extensions;
using Wolverine;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Http;
using ZiggyCreatures.Caching.Fusion;


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
    IHttpContextAccessor? httpContextAccessor = null,
    IFusionCache? cache = null) : IPageContentService
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
            if (document is null || document.SiteId != _siteContext.SiteId)
            {
                return Prelude.Fail<PageDocument?, AeroError>(AeroError.CreateError($"Page with id '{id}' not found or access denied"));
            }

            await SetCacheAsync(cacheKey, document, cancellationToken);
            return Prelude.Ok<PageDocument?, AeroError>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PageDocument?, AeroError>(AeroError.CreateError(ex.Message));
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
            return Prelude.Fail<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>(AeroError.CreateError(ex.Message));
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

            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x =>
                    x.SiteId == _siteContext.SiteId &&
                    string.Equals(slug, x.Slug, StringComparison.CurrentCultureIgnoreCase), token: cancellationToken);
            if (reservation is null || reservation.OwnerType != ContentSlugOwnerType.Page)
            {
                return Prelude.Fail<PageDocument?, AeroError>(AeroError.NotFoundError($"Page with slug '{slug}' not found"));
            }

            var document = await session.LoadAsync<PageDocument>(reservation.OwnerId, cancellationToken);
            if (document is null)
                return Prelude.Fail<PageDocument?, AeroError>(AeroError.NotFoundError($"Page with id '{reservation.OwnerId}' not found"));

            // Filter by published state — unpublished pages must not be publicly accessible
            if (document.PublicationState != ContentPublicationState.Published)
                return Prelude.Fail<PageDocument?, AeroError>(AeroError.NotFoundError($"Page with slug '{slug}' not found"));

            await SetCacheAsync(cacheKey, document, cancellationToken);
            return Prelude.Ok<PageDocument?, AeroError>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PageDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<PageDocument, AeroError>> CreateAsync(CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            SiteId = _siteContext.SiteId,
            Title = request.Title,
            Slug = string.IsNullOrEmpty(request.Slug)
                ? request.Title.GenerateSlug()
                : request.Slug,
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
            page.LayoutRegions = await MapEditorBlocksToLayoutRegions(request.EditorBlocks, cancellationToken);
        }
        else
        {
            page.Blocks = new List<EditorBlock>();
            page.LayoutRegions = request.LayoutRegions?.ToList() ?? [];
        }

        return await SaveAsync(page, cancellationToken);
    }

    public async Task<Result<PageDocument, AeroError>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);
            if (page is null || page.SiteId != _siteContext.SiteId)
            {
                return Prelude.Fail<PageDocument, AeroError>(AeroError.CreateError($"Page with id '{id}' not found or access denied"));
            }

            ApplyUpdateRequest(page, request);

            if (request.EditorBlocks is { Count: > 0 })
            {
                page.Blocks = request.EditorBlocks.ToList();
                page.LayoutRegions = await MapEditorBlocksToLayoutRegions(request.EditorBlocks, cancellationToken);
            }
            else
            {
                page.Blocks = new List<EditorBlock>();
                page.LayoutRegions = request.LayoutRegions?.ToList() ?? [];
            }

            return await SaveAsync(page, cancellationToken);
        }

        catch (Exception ex)
        {
            return Prelude.Fail<PageDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);
            if (page is null || page.SiteId != _siteContext.SiteId)
                return Prelude.Fail<bool, AeroError>(AeroError.CreateError($"Page with id '{id}' not found or access denied"));

            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x => x.OwnerId == id && x.OwnerType == ContentSlugOwnerType.Page && x.SiteId == _siteContext.SiteId, token: cancellationToken);

            if (reservation is not null)
            {
                session.Delete(reservation);
            }

            session.Delete<PageDocument>(id);
            await session.SaveChangesAsync(cancellationToken);
            await bus.PublishAsync(new PageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, page.Slug));
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<bool, AeroError>(AeroError.CreateError(ex.Message));
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

            await ValidatePage(page);

            var existingPage = await session.LoadAsync<PageDocument>(page.Id, cancellationToken);
            if (existingPage is not null && existingPage.SiteId != _siteContext.SiteId)
            {
                return Prelude.Fail<PageDocument, AeroError>(AeroError.CreateError($"Page with id '{page.Id}' not found or access denied"));
            }

            var targetPage = existingPage ?? page;
            var oldSlug = existingPage?.Slug;
            if (existingPage is not null && !ReferenceEquals(page, existingPage))
            {
                ApplyPersistedValues(page, existingPage);
            }

            await ContentSlugReservation.ReserveAsync(
                session,
                targetPage.Id,
                ContentSlugOwnerType.Page,
                targetPage.Slug,
                targetPage.SiteId,
                oldSlug,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var existingCreatedOn = existingPage?.CreatedOn;
            targetPage.CreatedOn = existingCreatedOn is null || existingCreatedOn == default ? now : existingCreatedOn.Value;
            targetPage.ModifiedOn = now;
            targetPage.ModifiedBy = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
            targetPage.PublishedOn = targetPage.PublicationState == ContentPublicationState.Published
                ? existingPage?.PublishedOn ?? now
                : null;

            session.Store(targetPage);
            await session.SaveChangesAsync(cancellationToken);

            if (targetPage.PublicationState == ContentPublicationState.Published)
            {
                await bus.PublishAsync(new SlugUpdated(targetPage.Id, "Page", targetPage.Slug, oldSlug));
            }

            await bus.PublishAsync(new PageContentUpdatedEvent(targetPage.Id, targetPage.SiteId, targetPage.Slug, oldSlug));

            return Prelude.Ok<PageDocument, AeroError>(targetPage);

        }
        catch (ArgumentException ex)
        {
            return Prelude.Fail<PageDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PageDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    private async Task<List<LayoutRegion>> MapEditorBlocksToLayoutRegions(IReadOnlyList<EditorBlock> editorBlocks, CancellationToken cancellationToken)
    {
        var placements = new List<BlockPlacement>();
        int order = 0;

        foreach (var eb in editorBlocks)
        {
            var block = EditorBlockMapper.MapBlock(eb);
            if (block != null)
            {
                await blockService.SaveAsync(block, cancellationToken);
                placements.Add(new BlockPlacement
                {
                    BlockId = block.Id,
                    Order = order++
                });
            }
        }

        // For now, put all editor blocks in a single column in one "Main" region
        var column = new LayoutColumn
        {
            Width = 12, // full width
            Blocks = placements
        };

        return [
            new LayoutRegion
            {
                Name = "Main",
                Order = 0,
                Columns = [column]
            }
        ];
    }

    private static async Task ValidatePage(PageDocument page)
    {
        var validator = new PageDocumentValidator();
        var valid = await validator.ValidateAsync(page);

        if (valid.Errors.Any())
        {
            throw new ArgumentException($"page errors: {string.Join(", ", valid.Errors.Select(e => e.ErrorMessage))}");
        }
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
