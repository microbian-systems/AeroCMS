using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Core.Http;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace Aero.Cms.Modules.Pages.Areas.Cms.Pages;

[OutputCache(PolicyName = "PagesPolicy")]
public class DynamicPageModel(
    IAeroPageActor pageActor,
    IBlockService blockService,
    BlockRenderCache blockCache,
    ISiteContext siteContext) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Slug { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? DraftId { get; set; }

    public string? SeoTitle { get; private set; }
    public string? Title { get; private set; }
    public bool ShowHeaderNavigation { get; private set; } = true;
    public bool HideFooter { get; private set; }
    public bool ShowChatAgent { get; private set; } = true;
    public List<LayoutRegion>? LayoutRegions { get; private set; }
    public long? PageId { get; private set; }
    public string? PageSlug { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        AeroRequestResponse<Aero.Cms.Abstractions.Models.PageViewModel> result;

        if (DraftId is { } draftId)
        {
            result = await pageActor.GetByIdAsync(draftId, cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(Slug))
        {
            result = await pageActor.GetBySlugAsync(siteContext.SiteId, "/", cancellationToken);
        }
        else
        {
            var normalizedSlug = Slug.TrimStart('/');
            result = await pageActor.GetBySlugAsync(siteContext.SiteId, normalizedSlug, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(result.error?.Message))
        {
            return NotFound();
        }

        var vm = result.data;
        if (vm is null)
        {
            return NotFound();
        }

        SeoTitle = vm.SeoTitle ?? vm.Title;
        Title = vm.Title;
        ShowHeaderNavigation = vm.ShowHeaderNavigation;
        HideFooter = vm.HideFooter;
        ShowChatAgent = vm.ShowChatAgent;
        PageId = vm.Id;
        PageSlug = vm.Slug;

        // Deserialize layout regions for block preloading and rendering
        LayoutRegions = vm.LayoutRegionsJson is not null
            ? System.Text.Json.JsonSerializer.Deserialize<List<LayoutRegion>>(
                vm.LayoutRegionsJson, Aero.Cms.Abstractions.Blocks.Serialization.BlockJsonContext.Default.Options)
            : [];

        // Store page ID + slug for output cache tagging
        HttpContext.Items["AeroCms.PageId"] = vm.Id;
        HttpContext.Items["AeroCms.PageSlug"] = vm.Slug;

        // Preload block cache (N+1 fix)
        await PreloadBlockCacheAsync(LayoutRegions, cancellationToken);

        PreserveReExecutedStatusCode();
        ApplyResponseCacheHeaders();
        return Page();
    }

    private async Task PreloadBlockCacheAsync(List<LayoutRegion> layoutRegions, CancellationToken ct)
    {
        var blockIds = layoutRegions
            .SelectMany(r => r.Columns)
            .SelectMany(c => c.Blocks)
            .Where(p => p.BlockId > 0)
            .Select(p => p.BlockId)
            .Distinct()
            .ToList();

        if (blockIds.Count == 0)
            return;

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
