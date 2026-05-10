using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Http;
using Marten;
using Microsoft.AspNetCore.Http;
using Wolverine;
using static global::Aero.Core.Railway.Prelude;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Docs;

public sealed class DocsService(
    IDocumentSession session,
    IMessageBus bus,
    ISiteContext siteContext,
    IHttpContextAccessor? httpContextAccessor = null,
    IFusionCache? cache = null) : IDocsService
{
    private const string DocsCacheTag = "docs-index";
    private readonly ISiteContext _siteContext = siteContext;

    public async Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey("all");
            var cached = await TryGetCacheAsync<DocsPageCollectionCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Ok<IReadOnlyList<DocsPage>, AeroError>(cached.Items);
            }

            var docs = await session.Query<DocsPage>()
                .Where(x => x.SiteId == _siteContext.SiteId)
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);

            await SetCacheAsync(cacheKey, new DocsPageCollectionCacheEntry(docs.ToList()), cancellationToken);
            return Ok<IReadOnlyList<DocsPage>, AeroError>(docs);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    public async Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"slug:{NormalizeCachePart(slug)}");
            var cached = await TryGetCacheAsync<DocsPage>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Ok<DocsPage?, AeroError>(cached);
            }

            var doc = await session.Query<DocsPage>()
                .FirstOrDefaultAsync(x => x.SiteId == _siteContext.SiteId && x.Slug == slug, cancellationToken);

            if (doc is not null)
            {
                await SetCacheAsync(cacheKey, doc, cancellationToken);
            }

            return Ok<DocsPage?, AeroError>(doc);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    public async Task<global::Aero.Core.Railway.Result<DocsPage?, AeroError>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"id:{id}");
            var cached = await TryGetCacheAsync<DocsPage>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Ok<DocsPage?, AeroError>(cached);
            }

            var doc = await session.LoadAsync<DocsPage>(id, cancellationToken);
            if (doc is null || doc.SiteId != _siteContext.SiteId)
            {
                return Ok<DocsPage?, AeroError>(null);
            }

            await SetCacheAsync(cacheKey, doc, cancellationToken);
            return Ok<DocsPage?, AeroError>(doc);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    public async Task<global::Aero.Core.Railway.Result<DocsPage, AeroError>> SaveAsync(DocsPage page, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await session.LoadAsync<DocsPage>(page.Id, cancellationToken);
            var oldSlug = existing?.Slug;
            page.SiteId = _siteContext.SiteId; // stamp from context
            var isNew = existing is null;

            var now = DateTimeOffset.UtcNow;
            page.ModifiedOn = now;
            page.ModifiedBy = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";

            session.Store(page);
            await session.SaveChangesAsync(cancellationToken);

            if (isNew)
                await bus.PublishAsync(new AeroEvent<DocViewModel>.DocCreated(ToViewModel(page), $"Doc created: {page.Slug}"));
            else
                await bus.PublishAsync(new AeroEvent<DocViewModel>.DocUpdated(ToViewModel(page), $"Doc updated: {page.Slug}"));

            await bus.PublishAsync(new DocsPageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, oldSlug));

            return Ok<DocsPage, AeroError>(page);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    public async Task<global::Aero.Core.Railway.Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await session.LoadAsync<DocsPage>(id, cancellationToken);
            if (page is null || page.SiteId != _siteContext.SiteId)
                return AeroError.CreateError($"Doc with id '{id}' not found or access denied");

            session.Delete<DocsPage>(id);
            await session.SaveChangesAsync(cancellationToken);

            if (page is not null)
            {
                await bus.PublishAsync(new AeroEvent<DocViewModel>.DocDeleted(ToViewModel(page), $"Doc deleted: {page.Slug}"));
                await bus.PublishAsync(new DocsPageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, page.Slug));
            }

            return Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    private static DocViewModel ToViewModel(DocsPage page) => new()
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
        CreatedOn = page.CreatedOn,
        ModifiedOn = page.ModifiedOn,
        CreatedBy = page.CreatedBy,
        ModifiedBy = page.ModifiedBy
    };

    public async Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"children:{parentId}");
            var cached = await TryGetCacheAsync<DocsPageCollectionCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Ok<IReadOnlyList<DocsPage>, AeroError>(cached.Items);
            }

            var children = await session.Query<DocsPage>()
                .Where(x => x.SiteId == _siteContext.SiteId && x.ParentId == parentId)
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);

            await SetCacheAsync(cacheKey, new DocsPageCollectionCacheEntry(children.ToList()), cancellationToken);
            return Ok<IReadOnlyList<DocsPage>, AeroError>(children);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    public async Task<global::Aero.Core.Railway.Result<IReadOnlyList<DocsPage>, AeroError>> GetTopLevelCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey("top-level-categories");
            var cached = await TryGetCacheAsync<DocsPageCollectionCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Ok<IReadOnlyList<DocsPage>, AeroError>(cached.Items);
            }

            // First find root "docs" page
            var rootDoc = await session.Query<DocsPage>()
                .FirstOrDefaultAsync(x => x.SiteId == _siteContext.SiteId && x.Slug == "docs", cancellationToken);
            
            if (rootDoc == null)
            {
                return Ok<IReadOnlyList<DocsPage>, AeroError>([]);
            }

            // Find children of root "docs"
            var children = await session.Query<DocsPage>()
                .Where(x => x.SiteId == _siteContext.SiteId && x.ParentId == rootDoc.Id)
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);

            await SetCacheAsync(cacheKey, new DocsPageCollectionCacheEntry(children.ToList()), cancellationToken);
            return Ok<IReadOnlyList<DocsPage>, AeroError>(children);
        }
        catch (Exception ex)
        {
            return AeroError.CreateError(ex.Message);
        }
    }

    private string BuildCacheKey(string suffix)
        => $"cms:docs:{_siteContext.SiteId}:{suffix}";

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
            : cache.SetAsync(key, value, tags: [DocsCacheTag], token: cancellationToken).AsTask();

    private static string NormalizeCachePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "_" : value.Trim().Trim('/').ToLowerInvariant();

    private sealed record DocsPageCollectionCacheEntry(List<DocsPage> Items);
}
