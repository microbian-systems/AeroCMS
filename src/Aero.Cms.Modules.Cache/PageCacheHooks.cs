using Aero.Cms.Web.Core.Pipelines;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Cache;

/// <summary>
/// Defines a read-hook implementation that can serve a cached page when explicitly added to a read pipeline.
/// </summary>
/// <remarks>
/// Keys include tenant identifier (or zero), culture, slug, and draft-inclusion mode. They do not include
/// user identity, authorization state, or arbitrary request headers. <see cref="CacheModule"/> registers this type
/// only as itself, not as <see cref="IPageReadHook"/>, and does not add it to the global hook pipeline. It is
/// therefore inactive in the current module configuration.
/// </remarks>
public class PageCacheHook(IFusionCache cache) : IPageReadHook
{
    /// <summary>Gets the early read-hook order used to observe cache hits before page loading.</summary>
public int Order => -100; // Run early to catch cache hits

    /// <summary>Reads the scoped page key and assigns a cached page to the context when present.</summary>
    /// <param name="ctx">The read context supplying the cache-key dimensions and receiving the cached page.</param>
    /// <param name="ct">Token forwarded to FusionCache.</param>
public async Task ExecuteAsync(PageReadContext ctx, CancellationToken ct)
    {
        var key = GetCacheKey(ctx);
        
        // We use GetOrDefaultAsync to avoid the factory-based GetOrSetAsync pattern here
        // because the "factory" is the rest of the pipeline.
        // A better integration would be in the PipelineRunner itself.
        var cachedPage = await cache.GetOrDefaultAsync<object>(key, token: ct);

        if (cachedPage != null)
        {
            ctx.Page = cachedPage;
            ctx.ShortCircuit("FusionCache Hit");
        }
    }

    private static string GetCacheKey(PageReadContext ctx)
    {
        return $"page:{ctx.TenantId ?? 0}:{ctx.Culture}:{ctx.Slug}:{ctx.IncludeDraft}";
    }
}

/// <summary>
/// Defines a read-hook implementation that can store a loaded page when explicitly added to a read pipeline.
/// </summary>
/// <remarks>
/// If invoked, it writes a key composed of tenant identifier (or zero), culture, slug, and draft-inclusion mode;
/// tags it with <c>pages-list</c> and <c>page-slug-{tenantId}:{slug}</c>; and forwards cancellation to FusionCache.
/// It is registered only as its concrete type and is not added to the global <see cref="IPageReadHook"/> pipeline,
/// so it currently stores no entries through that integration.
/// </remarks>
public class PageCacheStoreHook(IFusionCache cache) : IPageReadHook
{
    /// <summary>Gets the late read-hook order used to observe a loaded page.</summary>
public int Order => 1000; // Run late to capture the loaded page

    /// <summary>Stores a non-short-circuited page under its tenant/culture/slug/draft key and invalidation tags.</summary>
    /// <param name="ctx">The read context containing the page and cache-key dimensions.</param>
    /// <param name="ct">Token forwarded to FusionCache.</param>
public async Task ExecuteAsync(PageReadContext ctx, CancellationToken ct)
    {
        // Don't re-cache if it was already a short-circuit (e.g. cache hit)
        if (ctx.Page != null && !ctx.IsShortCircuited)
        {
            var key = GetCacheKey(ctx);

            // Tag with both coarse and fine-grained tags so individual
            // pages can be evicted without invalidating the entire cache.
            // Coarse tag "pages-list" → evict all pages (publish, navigation/footer change)
            // Fine tag "page-slug-{tenantId}:{slug}" → evict single page (slug update)
            var tags = new List<string>(2)
            {
                "pages-list",
                $"page-slug-{ctx.TenantId ?? 0}:{ctx.Slug.ToLowerInvariant()}"
            };

            await cache.SetAsync(key, ctx.Page, tags: tags, token: ct);
        }
    }

    private static string GetCacheKey(PageReadContext ctx)
    {
        return $"page:{ctx.TenantId ?? 0}:{ctx.Culture}:{ctx.Slug}:{ctx.IncludeDraft}";
    }
}

/// <summary>
/// Placeholder save-hook implementation for future page-cache invalidation.
/// </summary>
/// <remarks>
/// Although it implements <see cref="IPageSaveHook"/>, the module registers it only as its concrete type and does
/// not add it to the global save-hook pipeline. Its <see cref="ExecuteAsync"/> body is also intentionally a no-op;
/// it only inspects the context's validation and short-circuit flags before returning. It ignores the remaining
/// context data and cancellation token and does not remove keys or tags, even if invoked directly. No page-save
/// eviction occurs through this hook.
/// </remarks>
public class PageCacheInvalidatorHook(IFusionCache cache) : IPageSaveHook
{
    private readonly IFusionCache _cache = cache;

    /// <summary>Gets the late save-hook order used after save confirmation.</summary>
public int Order => 1000; // Run after save is confirmed

    /// <summary>
    /// Returns without invalidating any cache entry. Validation-error and
    /// short-circuited contexts return early; all other contexts also complete
    /// without eviction.
    /// </summary>
    /// <param name="ctx">
    /// The save context. Only its validation-error and short-circuit flags are
    /// inspected; page identity and other context data are currently ignored.
    /// </param>
    /// <param name="ct">
    /// The cancellation token, which is currently ignored and is not forwarded
    /// to FusionCache because this implementation performs no cache operation.
    /// </param>
public async Task ExecuteAsync(PageSaveContext ctx, CancellationToken ct)
    {
        if (ctx.HasValidationErrors || ctx.IsShortCircuited) return;

        // In a real scenario, we'd extract the slug, tenant, and culture from the page object.
        // Since Page is currently 'object', we'll need a way to identify it.
        // For now, we'll assume a pattern or use a broad invalidation if needed.
        
        // TODO: Implement specific key invalidation once the Page model is finalized.
        // For now, we might need to clear by tags or specific keys if we can resolve them.
        
        // Example (hypothetical):
        // var slug = (ctx.Page as IHasSlug)?.Slug;
        // if (slug != null) await _cache.RemoveAsync($"page:*:{slug}:*");
    }
}
