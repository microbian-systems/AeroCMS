using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Aero.Cms.Modules.Pages.Areas.Cms.Pages;

[OutputCache(PolicyName = "PagesPolicy")]
public class DynamicPageModel(
    IPageContentService pageService,
    IBlockService blockService,
    BlockRenderCache blockCache) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Slug { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? DraftId { get; set; }

    public PageDocument? PageDocument { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        Result<PageDocument?, AeroError> result;

        if (DraftId is { } draftId)
        {
            // TODO: Restore auth guard with preview API key. The draft preview
            // iframe loads cross-domain (sub-site hostname), so the auth cookie
            // from the manager domain (localhost) is not sent by the browser.
            // Future options:
            //   (B) Signed token in preview URL — append ?token={signed} validated
            //       server-side via IDataProtector.
            //   (C) Cross-domain SSO on site switch — hidden request to new
            //       domain with login token to set auth cookie there.
            //   (D) Seeded preview API key — validate ?key={apiKey} query param
            //       against a PreviewApiKey document in Marten.
            // For now: no auth on draft preview since IDs are Snowflakes
            // (unguessable) and access requires knowing the exact page ID.

            result = await pageService.LoadAsync(draftId, cancellationToken);
        }
        // If no slug provided, load the homepage
        else if (string.IsNullOrWhiteSpace(Slug))
        {
            result = await pageService.LoadHomepageAsync(cancellationToken);
        }
        else
        {
            // Normalize slug - remove leading slash if present for consistency
            var normalizedSlug = Slug.TrimStart('/');
            result = await pageService.FindBySlugAsync(normalizedSlug, cancellationToken);
        }

        var page = result switch
        {
            Result<PageDocument?, AeroError>.Ok(var foundPage) => foundPage,
            Result<PageDocument?, AeroError>.Failure => (PageDocument?)null,
            _ => (PageDocument?)null
        };

        if (page is null)
        {
            return NotFound();
        }

        PageDocument = page;

        // Store page ID + slug in HttpContext.Items so CmsOutputCachePolicy
        // can read them during ServeResponseAsync and tag the cached response
        // with fine-grained per-page identifiers (page-id-{id}, page-slug-{slug}).
        // This enables single-page OutputCache eviction without invalidating
        // the entire pages-list tag.
        HttpContext.Items["AeroCms.PageId"] = page.Id;
        HttpContext.Items["AeroCms.PageSlug"] = page.Slug;

        // Preload all block IDs from the page's layout regions into BlockRenderCache.
        // This eliminates N+1 database round-trips during page rendering: instead of
        // each BlockPlacementRenderer calling IBlockService.GetByIdAsync individually
        // (one Marten LoadAsync per block), we batch-load all blocks in a single
        // WHERE Id = ANY(@ids) query. The cached result is then served to each
        // BlockPlacementRenderer via O(1) dictionary lookup in GetBlock().
        await PreloadBlockCacheAsync(page, cancellationToken);

        PreserveReExecutedStatusCode();
        ApplyResponseCacheHeaders();
        return Page();
    }

    /// <summary>
    /// Collects all distinct block IDs from every LayoutRegion/Column/Placement
    /// on the page and bulk-loads them into the request-scoped BlockRenderCache.
    /// 
    /// N+1 REMEDY: Without this preload, the <c>&lt;component type="LayoutRegionRenderer"&gt;</c>
    /// components in Page.cshtml would each independently call IBlockService.GetByIdAsync
    /// for every block placement. A page with N blocks makes N database round-trips.
    /// With this preload, all N blocks are resolved in a single batch query, then
    /// served from an in-memory dictionary during rendering.
    /// </summary>
    private async Task PreloadBlockCacheAsync(PageDocument page, CancellationToken ct)
    {
        // Collect all distinct BlockIds from the published layout manifest.
        // LayoutRegions → LayoutColumns → Blocks (List<BlockPlacement>) → BlockId
        var blockIds = page.LayoutRegions
            .SelectMany(r => r.Columns)
            .SelectMany(c => c.Blocks)
            .Where(p => p.BlockId > 0)       // skip unset / sentinel values
            .Select(p => p.BlockId)
            .Distinct()
            .ToList();

        if (blockIds.Count == 0)
            return;

        // Fire-and-forget? No — we must await the preload so the cache is
        // fully populated BEFORE the Blazor component tree renders (which
        // happens synchronously after OnGetAsync returns).
        // BlockRenderCache is registered as AddScoped → same instance shared
        // between DynamicPageModel and all <component> tag helpers in this request.
        // We don't store the IBlockService reference in the cache; instead we
        // pass it as a parameter so the cache stays a pure data holder.
        await blockCache.PreloadAsync(blockIds, blockService, ct);
    }

    private void PreserveReExecutedStatusCode()
    {
        var reExecuteFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        if (reExecuteFeature is null)
        {
            return;
        }

        if (reExecuteFeature.OriginalStatusCode is >= 400 and <= 599)
        {
            Response.StatusCode = reExecuteFeature.OriginalStatusCode;
        }
    }

    private void ApplyResponseCacheHeaders()
    {
        if (DraftId is not null)
        {
            Response.Headers.CacheControl = "no-store, no-cache";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
            return;
        }

        Response.Headers.CacheControl = "public,max-age=300";
    }
}
