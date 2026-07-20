using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Content.Caching;

/// <summary>
/// Read-through cache decorator for content items.
/// </summary>
/// <param name="inner">The persistence service used on cache misses and for every mutation.</param>
/// <param name="cache">The FusionCache instance holding detached item snapshots.</param>
/// <param name="invalidator">The post-commit invalidator for stale identities and rendered responses.</param>
/// <param name="logger">The logger for best-effort cache-write failures.</param>
/// <remarks>
/// Identifier and slug keys are site-qualified. Cache read failures propagate; cache population
/// and post-commit invalidation failures are logged and suppressed.
/// </remarks>
internal sealed class CachedContentService(
    AeroContentService inner,
    IFusionCache cache,
    ContentCacheInvalidator invalidator,
    ILogger<CachedContentService> logger) : IContentService
{
    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> LoadAsync(
        long siteId, long id,
        CancellationToken ct = default)
    {
        var key = ContentCacheKeys.ItemById(siteId, id);
        var cached = await cache.TryGetAsync<ContentItem>(key, token: ct);
        if (cached.HasValue && cached.Value.SiteId == siteId && cached.Value.Id == id)
        {
            return Prelude.Ok<ContentItem, AeroError>(ContentCacheSnapshot.Clone(cached.Value));
        }

        var result = await inner.LoadAsync(siteId, id, ct);
        if (result is Result<ContentItem, AeroError>.Ok ok)
        {
            await SetAsync(ok.Value, ct);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> GetBySlugAsync(
        long siteId,
        string slug,
        CancellationToken ct = default)
    {
        var key = ContentCacheKeys.ItemBySlug(siteId, slug);
        var cached = await cache.TryGetAsync<ContentItem>(key, token: ct);
        if (cached.HasValue && cached.Value.SiteId == siteId && string.Equals(cached.Value.Slug, slug, StringComparison.OrdinalIgnoreCase))
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

    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> GetBySlugAndTypeAsync(
        long siteId,
        string contentTypeAlias,
        string culture,
        string slug,
        CancellationToken ct = default)
    {
        var key = ContentCacheKeys.ItemByTypedSlug(siteId, contentTypeAlias, culture, slug);
        var cached = await cache.TryGetAsync<ContentItem>(key, token: ct);
        if (cached.HasValue &&
            cached.Value.SiteId == siteId &&
            string.Equals(cached.Value.ContentTypeAlias, contentTypeAlias, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(cached.Value.Culture, System.Globalization.CultureInfo.GetCultureInfo(culture).Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(cached.Value.Slug, slug, StringComparison.OrdinalIgnoreCase))
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

    /// <inheritdoc />
    /// <remarks>
    /// For updates, the previous identity is loaded before persistence so renamed slugs, cultures,
    /// or types can be invalidated. A successful commit is returned even when invalidation or cache
    /// repopulation fails.
    /// </remarks>
    public async Task<Result<ContentItem, AeroError>> SaveAsync(
        ContentItem item,
        CancellationToken ct = default)
    {
        ContentItemCacheIdentity? oldIdentity = null;
        if (item.Id > 0)
        {
            var existing = await inner.LoadAsync(item.SiteId, item.Id, ct);
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

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(long siteId, long id, CancellationToken ct = default)
    {
        var cached = await cache.TryGetAsync<ContentItem>(
            ContentCacheKeys.ItemById(siteId, id),
            token: ct);
        return (cached.HasValue && cached.Value.SiteId == siteId && cached.Value.Id == id) ||
               await inner.ExistsAsync(siteId, id, ct);
    }

    /// <inheritdoc />
    /// <remarks>A successful delete is not converted to failure by cache invalidation errors.</remarks>
    public async Task<Result<bool, AeroError>> DeleteAsync(
        long siteId, long id,
        CancellationToken ct = default)
    {
        ContentItemCacheIdentity? oldIdentity = null;
        var existing = await inner.LoadAsync(siteId, id, ct);
        if (existing is Result<ContentItem, AeroError>.Ok ok)
        {
            oldIdentity = ToIdentity(ok.Value);
        }

        var result = await inner.DeleteAsync(siteId, id, ct);
        if (result is Result<bool, AeroError>.Ok { Value: true })
        {
            await invalidator.InvalidateItemAsync(oldIdentity, null);
        }

        return result;
    }

    /// <summary>
    /// Stores one detached snapshot under identifier, untyped slug, and typed culture-slug keys.
    /// </summary>
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
                ContentCacheKeys.ItemById(item.SiteId, item.Id),
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

    /// <summary>
    /// Executes a cache update and logs any exception without rethrowing it.
    /// </summary>
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

    /// <summary>
    /// Captures the fields needed to invalidate every cache identity for an item.
    /// </summary>
    private static ContentItemCacheIdentity ToIdentity(ContentItem item) =>
        new(item.SiteId, item.Id, item.ContentTypeAlias, item.Culture, item.Slug);
}
