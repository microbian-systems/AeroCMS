using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Shared.Localization;
using Aero.Cms.Shared.Components;
using Aero.Cms.Core.Entities;
using Aero.Core.Http;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Marten;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using System.Globalization;

namespace Aero.Cms.Modules.Pages.Areas.Cms.Pages;

[OutputCache(PolicyName = "PagesPolicy")]
public class DynamicPageModel(
    IAeroPageActor pageActor,
    IBlockService blockService,
    BlockRenderCache blockCache,
    ISiteContext siteContext,
    IDocumentStore documentStore) : PageModel
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
    public List<LayoutRegion> LayoutRegions { get; private set; } = [];
    public NeoPageNode? RootNode { get; private set; }
    public long? PageId { get; private set; }
    public string? PageSlug { get; private set; }
    public string RequestedCulture { get; private set; } = SitesModel.DefaultCultureName;
    public string RenderedCulture { get; private set; } = SitesModel.DefaultCultureName;
    public bool IsCultureFallback { get; private set; }
    public string CanonicalUrl { get; private set; } = string.Empty;
    public IReadOnlyList<AlternatePageLink> AlternateLinks { get; private set; } = [];
    public IReadOnlyList<CultureSwitcherLink> CultureSwitcherLinks { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        AeroRequestResponse<Aero.Cms.Abstractions.Models.PageViewModel> result;
        RequestedCulture = CultureInfo.CurrentUICulture.Name;

        if (DraftId is { } draftId)
        {
            result = await pageActor.GetByIdAsync(draftId, cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(Slug))
        {
            result = await pageActor.GetBySlugAsync(siteContext.SiteId, "/", CultureInfo.CurrentUICulture.Name, cancellationToken);
        }
        else
        {
            var normalizedSlug = AeroCultureRoute.StripLeadingCulture(Slug);
            result = await pageActor.GetBySlugAsync(siteContext.SiteId, normalizedSlug, CultureInfo.CurrentUICulture.Name, cancellationToken);
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
        RenderedCulture = vm.Culture;
        IsCultureFallback = !string.Equals(RequestedCulture, RenderedCulture, StringComparison.OrdinalIgnoreCase);
        CanonicalUrl = BuildCultureUrl(RenderedCulture, vm.Slug);

        await LoadCompositionAsync(vm, cancellationToken);

        // Store page ID + slug for output cache tagging
        HttpContext.Items["AeroCms.PageId"] = vm.Id;
        HttpContext.Items["AeroCms.PageSlug"] = vm.Slug;
        ViewData["RequestedCulture"] = RequestedCulture;
        ViewData["RenderedCulture"] = RenderedCulture;
        ViewData["IsCultureFallback"] = IsCultureFallback;
        AlternateLinks = await BuildAlternateLinksAsync(vm, cancellationToken);
        CultureSwitcherLinks = BuildCultureSwitcherLinks(AlternateLinks);
        ViewData["CultureSwitcherLinks"] = CultureSwitcherLinks;

        // Preload block cache (N+1 fix)
        await PreloadBlockCacheAsync(LayoutRegions, cancellationToken);

        PreserveReExecutedStatusCode();
        ApplyResponseCacheHeaders();
        return Page();
    }

    private async Task<IReadOnlyList<AlternatePageLink>> BuildAlternateLinksAsync(
        PageViewModel page,
        CancellationToken cancellationToken)
    {
        var variants = await pageActor.ListCultureVariantsAsync(page.Id, cancellationToken);
        if (variants.Count == 0)
            variants = [page];

        var publishedVariants = variants
            .Where(variant => variant.IsPublished)
            .Where(variant => !string.IsNullOrWhiteSpace(variant.Culture))
            .GroupBy(variant => variant.Culture, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (publishedVariants.Count == 0 && page.IsPublished)
            publishedVariants.Add(page);

        var links = publishedVariants
            .Select(variant => new AlternatePageLink(
                variant.Culture.ToLowerInvariant(),
                BuildCultureUrl(variant.Culture, variant.Slug)))
            .ToList();

        var siteDefaultCulture = HttpContext.Features.Get<IAeroSiteSlice>()?.DefaultCulture
            ?? page.Culture
            ?? SitesModel.DefaultCultureName;
        var defaultVariant = publishedVariants.FirstOrDefault(variant =>
            string.Equals(variant.Culture, siteDefaultCulture, StringComparison.OrdinalIgnoreCase));

        if (defaultVariant is not null)
        {
            links.Add(new AlternatePageLink("x-default", BuildCultureUrl(defaultVariant.Culture, defaultVariant.Slug)));
        }

        return links;
    }

    private async Task LoadCompositionAsync(PageViewModel page, CancellationToken cancellationToken)
    {
        var compositionId = DraftId is not null
            ? page.DraftCompositionId ?? page.PublishedCompositionId
            : page.PublishedCompositionId ?? page.DraftCompositionId;

        if (compositionId is { } id)
        {
            await using var session = documentStore.QuerySession();
            var composition = await session.LoadAsync<PageCompositionDocument>(id, cancellationToken);

            if (composition is not null)
            {
                RootNode = BuildRootNode(composition.RootNodes);
                LayoutRegions = composition.LayoutRegions;
                return;
            }
        }

        LayoutRegions = page.LayoutRegionsJson is not null
            ? System.Text.Json.JsonSerializer.Deserialize<List<LayoutRegion>>(
                page.LayoutRegionsJson, BlockJsonContext.Default.Options) ?? []
            : [];

        RootNode = page.RootNodeJson is not null
            ? System.Text.Json.JsonSerializer.Deserialize<NeoPageNode>(
                page.RootNodeJson, BlockJsonContext.Default.Options)
            : null;
    }

    private static NeoPageNode? BuildRootNode(IReadOnlyList<NeoPageNode> rootNodes)
    {
        if (rootNodes.Count == 0)
        {
            return null;
        }

        return new NeoPageNode
        {
            NodeId = "page-root",
            CatalogId = "page.root",
            Kind = NeoPageNodeKind.Page,
            Children = rootNodes.ToList()
        };
    }

    private IReadOnlyList<CultureSwitcherLink> BuildCultureSwitcherLinks(IReadOnlyList<AlternatePageLink> alternateLinks)
        => alternateLinks
            .Where(link => !string.Equals(link.Hreflang, "x-default", StringComparison.OrdinalIgnoreCase))
            .Select(link => CultureSwitcher.CreateLink(
                link.Hreflang,
                link.Href,
                string.Equals(link.Hreflang, RequestedCulture, StringComparison.OrdinalIgnoreCase)
                || string.Equals(link.Hreflang, RenderedCulture, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(link => link.Hreflang, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

    private string BuildCultureUrl(string culture, string? slug)
    {
        var normalizedSlug = (slug ?? string.Empty).Trim().Trim('/');
        var path = string.IsNullOrWhiteSpace(normalizedSlug)
            ? $"/{culture.ToLowerInvariant()}"
            : $"/{culture.ToLowerInvariant()}/{normalizedSlug}";

        return UriHelper.BuildAbsolute(Request.Scheme, Request.Host, Request.PathBase, path);
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

    public sealed record AlternatePageLink(string Hreflang, string Href);
}
