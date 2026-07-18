using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Content.Caching;

/// <summary>
/// Read-through cache decorator for content items.
/// </summary>
internal sealed class CachedContentService(
    AeroContentService inner,
    IFusionCache cache,
    ContentCacheInvalidator invalidator,
    ILogger<CachedContentService> logger) : IContentService
{
    public async Task<Result<ContentItem, AeroError>> LoadAsync(
        long id,
        CancellationToken ct = default)
    {
        var key = ContentCacheKeys.ItemById(id);
        var cached = await cache.TryGetAsync<ContentItem>(key, token: ct);
        if (cached.HasValue)
        {
            return Prelude.Ok<ContentItem, AeroError>(ContentCacheSnapshot.Clone(cached.Value));
        }

        var result = await inner.LoadAsync(id, ct);
        if (result is Result<ContentItem, AeroError>.Ok ok)
        {
            await SetAsync(ok.Value, ct);
        }

        return result;
    }

    public async Task<Result<ContentItem, AeroError>> GetBySlugAsync(
        long siteId,
        string slug,
        CancellationToken ct = default)
    {
        var key = ContentCacheKeys.ItemBySlug(siteId, slug);
        var cached = await cache.TryGetAsync<ContentItem>(key, token: ct);
        if (cached.HasValue)
        {
            return Prelude.Ok<ContentItem, AeroError>(ContentCacheSnapshot.Clone(cached.Value));
        }

        var result = await inner.GetBySlugAsync(siteId, slug, ct);
        if (result is Result<ContentItem, AeroError>.Ok ok)
        {
            await SetAsync(ok.Value, ct);
        }

        return result;
    }

    public async Task<Result<ContentItem, AeroError>> GetBySlugAndTypeAsync(
        long siteId,
        string contentTypeAlias,
        string culture,
        string slug,
        CancellationToken ct = default)
    {
        var key = ContentCacheKeys.ItemByTypedSlug(siteId, contentTypeAlias, culture, slug);
        var cached = await cache.TryGetAsync<ContentItem>(key, token: ct);
        if (cached.HasValue)
        {
            return Prelude.Ok<ContentItem, AeroError>(ContentCacheSnapshot.Clone(cached.Value));
        }

        var result = await inner.GetBySlugAndTypeAsync(
            siteId,
            contentTypeAlias,
            culture,
            slug,
            ct);
        if (result is Result<ContentItem, AeroError>.Ok ok)
        {
            await SetAsync(ok.Value, ct);
        }

        return result;
    }

    public async Task<Result<ContentItem, AeroError>> SaveAsync(
        ContentItem item,
        CancellationToken ct = default)
    {
        ContentItemCacheIdentity? oldIdentity = null;
        if (item.Id > 0)
        {
            var existing = await inner.LoadAsync(item.Id, ct);
            if (existing is Result<ContentItem, AeroError>.Ok ok)
            {
                oldIdentity = ToIdentity(ok.Value);
            }
        }

        var result = await inner.SaveAsync(item, ct);
        if (result is Result<ContentItem, AeroError>.Ok saved)
        {
            await invalidator.InvalidateItemAsync(oldIdentity, ToIdentity(saved.Value));
            await SetAsync(saved.Value, ct);
        }

        return result;
    }

    public async Task<bool> ExistsAsync(long id, CancellationToken ct = default)
    {
        var cached = await cache.TryGetAsync<ContentItem>(
            ContentCacheKeys.ItemById(id),
            token: ct);
        return cached.HasValue || await inner.ExistsAsync(id, ct);
    }

    public async Task<Result<bool, AeroError>> DeleteAsync(
        long id,
        CancellationToken ct = default)
    {
        ContentItemCacheIdentity? oldIdentity = null;
        var existing = await inner.LoadAsync(id, ct);
        if (existing is Result<ContentItem, AeroError>.Ok ok)
        {
            oldIdentity = ToIdentity(ok.Value);
        }

        var result = await inner.DeleteAsync(id, ct);
        if (result is Result<bool, AeroError>.Ok { Value: true })
        {
            await invalidator.InvalidateItemAsync(oldIdentity, null);
        }

        return result;
    }

    private Task SetAsync(ContentItem item, CancellationToken ct) =>
        BestEffortAsync(async () =>
        {
            var identity = ToIdentity(item);
            var snapshot = ContentCacheSnapshot.Clone(item);
            var tags = new[]
            {
                ContentCacheKeys.ContentItemsTag(item.SiteId),
                ContentCacheKeys.ContentItemsByTypeTag(item.SiteId, item.ContentTypeAlias),
                ContentCacheKeys.ContentItemTag(item.SiteId, item.Id),
                ContentCacheKeys.ContentItemSlugTag(
                    item.SiteId,
                    item.ContentTypeAlias,
                    item.Culture,
                    item.Slug)
            };
            await cache.SetAsync(
                ContentCacheKeys.ItemById(item.Id),
                snapshot,
                tags: tags,
                token: ct);
            await cache.SetAsync(
                ContentCacheKeys.ItemBySlug(item.SiteId, item.Slug),
                snapshot,
                tags: tags,
                token: ct);
            await cache.SetAsync(
                ContentCacheKeys.ItemByTypedSlug(
                    identity.SiteId,
                    identity.TypeAlias,
                    identity.Culture,
                    identity.Slug),
                snapshot,
                tags: tags,
                token: ct);
        });

    private async Task BestEffortAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Content-item cache update did not complete.");
        }
    }

    private static ContentItemCacheIdentity ToIdentity(ContentItem item) =>
        new(item.SiteId, item.Id, item.ContentTypeAlias, item.Culture, item.Slug);
}
