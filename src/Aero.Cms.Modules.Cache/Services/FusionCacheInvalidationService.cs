using Aero.Cms.Abstractions.Events;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Cache.Services;

public sealed class FusionCacheInvalidationService(
    IFusionCache cache,
    IOutputCacheStore outputCacheStore,
    ILogger<FusionCacheInvalidationService> logger) : ICacheInvalidationService
{
    private static readonly IReadOnlyDictionary<string, CacheTagSet> CacheTags = new Dictionary<string, CacheTagSet>(StringComparer.OrdinalIgnoreCase)
    {
        ["page"] = new("pages-list"),
        ["blog"] = new("blog-index"),
        ["docs"] = new("docs-index")
    };

    private static readonly CacheTagSet[] NavigationAffectedTags =
    [
        new("pages-list"),
        new("blog-index"),
        new("docs-index")
    ];

    private static readonly CacheTagSet[] FooterAffectedTags =
    [
        new("pages-list"),
        new("blog-index"),
        new("docs-index")
    ];

    public async Task InvalidateContentAsync(ContentUpdatedEvent @event, CancellationToken cancellationToken = default)
    {
        if (!CacheTags.TryGetValue(@event.ContentType, out var tags))
        {
            logger.LogDebug("Skipping cache invalidation for unknown content type {ContentType}", @event.ContentType);
            return;
        }

        await RemoveSlugKeyAsync(@event.ContentType, @event.SiteId, @event.NewSlug, cancellationToken);

        if (!string.IsNullOrWhiteSpace(@event.OldSlug) &&
            !string.Equals(@event.OldSlug, @event.NewSlug, StringComparison.OrdinalIgnoreCase))
        {
            await RemoveSlugKeyAsync(@event.ContentType, @event.SiteId, @event.OldSlug, cancellationToken);
        }

        await cache.RemoveByTagAsync(tags.FusionCacheTag, token: cancellationToken);
        await outputCacheStore.EvictByTagAsync(tags.OutputCacheTag, cancellationToken);

        logger.LogDebug(
            "Invalidated {ContentType} cache for site {SiteId} using tag {CacheTag}",
            @event.ContentType,
            @event.SiteId,
            tags.FusionCacheTag);
    }

    public async Task InvalidateNavigationAsync(NavigationMenuChangedEvent @event, CancellationToken cancellationToken = default)
    {
        foreach (var tags in NavigationAffectedTags)
        {
            await cache.RemoveByTagAsync(tags.FusionCacheTag, token: cancellationToken);
            await outputCacheStore.EvictByTagAsync(tags.OutputCacheTag, cancellationToken);
        }

        logger.LogDebug(
            "Invalidated navigation-dependent cache for site {SiteId} after nav menu {NavMenuId} was {ChangeKind}",
            @event.SiteId,
            @event.NavMenuId,
            @event.ChangeKind);
    }

    public async Task InvalidateFooterAsync(FooterChangedEvent @event, CancellationToken cancellationToken = default)
    {
        foreach (var tags in FooterAffectedTags)
        {
            await cache.RemoveByTagAsync(tags.FusionCacheTag, token: cancellationToken);
            await outputCacheStore.EvictByTagAsync(tags.OutputCacheTag, cancellationToken);
        }

        logger.LogDebug(
            "Invalidated footer-dependent cache for site {SiteId} after footer {FooterId} was {ChangeKind}",
            @event.SiteId,
            @event.FooterId,
            @event.ChangeKind);
    }

    private async Task RemoveSlugKeyAsync(string contentType, long siteId, string? slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        var cacheKey = $"cms:{contentType}:{siteId}:slug:{NormalizeCachePart(slug)}";
        await cache.RemoveAsync(cacheKey, token: cancellationToken);
    }

    private static string NormalizeCachePart(string value)
        => value.Trim().Trim('/').ToLowerInvariant();

    private sealed record CacheTagSet(string OutputCacheTag)
    {
        public string FusionCacheTag => OutputCacheTag;
    }
}
