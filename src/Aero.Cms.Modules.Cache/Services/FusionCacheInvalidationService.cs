using Aero.Cms.Abstractions.Events;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Cache.Services;

/// <summary>
/// Evicts selected FusionCache keys/tags and ASP.NET output-cache tags for CMS change events.
/// </summary>
/// <remarks>
/// Eviction calls are awaited in order and exceptions, including cancellation, propagate to the caller; the service
/// does not retry or swallow them. It has no transaction with content persistence or event delivery. FusionCache's
/// distributed cache and backplane behavior are configured elsewhere and are not a coherence guarantee made here.
/// </remarks>
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

        /// <summary>
    /// Removes direct FusionCache keys for a content ID and current/prior slugs, then applies known content-type tags.
    /// </summary>
    /// <remarks>
    /// Direct keys are removed for every content type. Only <c>page</c>, <c>blog</c>, and <c>docs</c> receive coarse
    /// FusionCache and output-cache tag eviction. For those types, matching per-page slug tags and the output-cache
    /// page-ID tag are also evicted; unknown types return after direct-key eviction.
    /// </remarks>
public async Task InvalidateContentAsync(ContentUpdatedEvent @event, CancellationToken cancellationToken = default)
    {
        // ── Slug-based cache key eviction (always) ──────────────────────
        // Works for any content type even if not in CacheTags
        await RemoveSlugKeyAsync(@event.ContentType, @event.SiteId, @event.NewSlug, cancellationToken);

        if (!string.IsNullOrWhiteSpace(@event.OldSlug) &&
            !string.Equals(@event.OldSlug, @event.NewSlug, StringComparison.OrdinalIgnoreCase))
        {
            await RemoveSlugKeyAsync(@event.ContentType, @event.SiteId, @event.OldSlug, cancellationToken);
        }

        // ── Content ID-based cache key eviction (always) ────────────────
        await RemovePageIdKeyAsync(@event.ContentType, @event.SiteId, @event.ContentId, cancellationToken);

        // ── Coarse tag eviction (known content types only) ──────────────
        if (!CacheTags.TryGetValue(@event.ContentType, out var tags))
        {
            logger.LogDebug("Invalidated slug & id keys for {ContentType}; no coarse tags registered", @event.ContentType);
            return;
        }

        // ── Coarse tag eviction (all pages) ───────────────────────────
        await cache.RemoveByTagAsync(tags.FusionCacheTag, token: cancellationToken);
        await outputCacheStore.EvictByTagAsync(tags.OutputCacheTag, cancellationToken);

        // ── Fine-grained per-page tag eviction (single page) ──────────
        // FusionCache tags: page-slug-{siteId}:{slug} (set by PageCacheStoreHook)
        // OutputCache tags: page-id-{id}, page-slug-{slug} (set by CmsOutputCachePolicy)
        if (!string.IsNullOrWhiteSpace(@event.NewSlug))
        {
            await cache.RemoveByTagAsync($"page-slug-{@event.SiteId}:{@event.NewSlug.ToLowerInvariant()}", token: cancellationToken);
            await outputCacheStore.EvictByTagAsync($"page-slug-{@event.NewSlug.ToLowerInvariant()}", cancellationToken);
        }
        if (!string.IsNullOrWhiteSpace(@event.OldSlug) &&
            !string.Equals(@event.OldSlug, @event.NewSlug, StringComparison.OrdinalIgnoreCase))
        {
            await cache.RemoveByTagAsync($"page-slug-{@event.SiteId}:{@event.OldSlug.ToLowerInvariant()}", token: cancellationToken);
            await outputCacheStore.EvictByTagAsync($"page-slug-{@event.OldSlug.ToLowerInvariant()}", cancellationToken);
        }
        await outputCacheStore.EvictByTagAsync($"page-id-{@event.ContentId}", cancellationToken);

        logger.LogDebug(
            "Invalidated {ContentType} cache for site {SiteId} content {ContentId} using coarse tag {CacheTag} + fine-grained page tags",
            @event.ContentType,
            @event.SiteId,
            @event.ContentId,
            tags.FusionCacheTag);
    }

        /// <summary>
    /// Evicts the page, blog, and docs coarse tags from both cache layers.
    /// </summary>
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

        /// <summary>
    /// Evicts the page, blog, and docs coarse tags from both cache layers.
    /// </summary>
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

        /// <summary>
    /// Invalidates rendered page responses for the site whose design tokens changed.
    /// Page-document FusionCache entries contain semantic content rather than compiled
    /// CSS, so only the site-scoped output-cache tag needs eviction.
    /// </summary>
public async Task InvalidateSiteStyleProfileAsync(
    SiteStyleProfileChangedEvent @event,
    CancellationToken cancellationToken = default)
    {
        var tag = $"site-pages-{@event.SiteId}";
        await outputCacheStore.EvictByTagAsync(tag, cancellationToken);

        logger.LogDebug(
            "Invalidated rendered pages for site {SiteId} after style profile revision {Revision}",
            @event.SiteId,
            @event.Revision);
    }

        /// <summary>Invalidates rendered page responses for the site whose theme selection changed.</summary>
public async Task InvalidateSiteThemeAsync(
    SiteThemeChangedEvent @event,
    CancellationToken cancellationToken = default)
    {
        var tag = $"site-pages-{@event.SiteId}";
        await outputCacheStore.EvictByTagAsync(tag, cancellationToken);

        logger.LogDebug(
            "Invalidated rendered pages for site {SiteId} after theme {ThemeId}@{ThemeVersion} revision {Revision}",
            @event.SiteId,
            @event.ThemeId,
            @event.ThemeVersion,
            @event.Revision);
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

    /// <summary>
    /// Removes the FusionCache key for a specific page ID.
    /// Key pattern: <c>cms:{contentType}:{siteId}:id:{contentId}</c>
    /// Complements <see cref="RemoveSlugKeyAsync"/> for pages accessed by ID
    /// (e.g., draft previews, admin lookups).
    /// </summary>
    private async Task RemovePageIdKeyAsync(string contentType, long siteId, long contentId, CancellationToken cancellationToken)
    {
        var cacheKey = $"cms:{contentType}:{siteId}:id:{contentId}";
        await cache.RemoveAsync(cacheKey, token: cancellationToken);
    }

    private static string NormalizeCachePart(string value)
        => value.Trim().Trim('/').ToLowerInvariant();

    private sealed record CacheTagSet(string OutputCacheTag)
    {
        /// <summary>Gets the FusionCache tag, which intentionally matches the output-cache tag.</summary>
        public string FusionCacheTag => OutputCacheTag;
    }
}
