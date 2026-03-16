using Aero.Cms.Core.Pipelines;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Cache;

/// <summary>
/// Hook to handle cache lookup for pages.
/// </summary>
public class PageCacheHook : IPageReadHook
{
    private readonly IFusionCache _cache;

    public PageCacheHook(IFusionCache cache)
    {
        _cache = cache;
    }

    public int Order => -100; // Run early to catch cache hits

    public async Task ExecuteAsync(PageReadContext ctx, CancellationToken ct)
    {
        var key = GetCacheKey(ctx);
        
        // We use GetOrDefaultAsync to avoid the factory-based GetOrSetAsync pattern here
        // because the "factory" is the rest of the pipeline.
        // A better integration would be in the PipelineRunner itself.
        var cachedPage = await _cache.GetOrDefaultAsync<object>(key, token: ct);

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
/// Hook to store successfully read pages into the cache.
/// </summary>
public class PageCacheStoreHook : IPageReadHook
{
    private readonly IFusionCache _cache;

    public PageCacheStoreHook(IFusionCache cache)
    {
        _cache = cache;
    }

    public int Order => 1000; // Run late to capture the loaded page

    public async Task ExecuteAsync(PageReadContext ctx, CancellationToken ct)
    {
        // Don't re-cache if it was already a short-circuit (e.g. cache hit)
        if (ctx.Page != null && !ctx.IsShortCircuited)
        {
            var key = GetCacheKey(ctx);
            await _cache.SetAsync(key, ctx.Page, token: ct);
        }
    }

    private static string GetCacheKey(PageReadContext ctx)
    {
        return $"page:{ctx.TenantId ?? 0}:{ctx.Culture}:{ctx.Slug}:{ctx.IncludeDraft}";
    }
}

/// <summary>
/// Hook to invalidate cache when a page is saved.
/// </summary>
public class PageCacheInvalidatorHook : IPageSaveHook
{
    private readonly IFusionCache _cache;

    public PageCacheInvalidatorHook(IFusionCache cache)
    {
        _cache = cache;
    }

    public int Order => 1000; // Run after save is confirmed

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
