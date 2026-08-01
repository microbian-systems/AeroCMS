using System.Globalization;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Core.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class ShopHomeModel(IProductService products, ISiteContext site) : PageModel
{
    public IReadOnlyList<PublicListingResponse> FeaturedProducts { get; private set; } = [];
    public IReadOnlyList<PublicListingResponse> RecentProducts { get; private set; } = [];
    public bool LoadFailed { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await products.SearchPublishedAsync(site.TenantId, site.SiteId, CultureInfo.CurrentUICulture.Name, take: 6, featuredOnly: true, ct: ct);
        if (result is Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Ok(var values))
            FeaturedProducts = values.Items.Select(PublicListingResponse.From).ToList();
        else
        {
            LoadFailed = true;
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Page();
        }

        var recent = await products.GetRecentPublishedAsync(site.TenantId, site.SiteId, CultureInfo.CurrentUICulture.Name, 6, ct);
        if (recent is Result<IReadOnlyList<ProductListingDocument>, AeroError>.Ok(var recentValues))
        {
            var featuredIds = FeaturedProducts.Select(x => x.Id).ToHashSet();
            RecentProducts = recentValues.Where(x => !featuredIds.Contains(x.Id)).Select(PublicListingResponse.From).Take(6).ToList();
        }
        else
        {
            LoadFailed = true;
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        return Page();
    }
}
