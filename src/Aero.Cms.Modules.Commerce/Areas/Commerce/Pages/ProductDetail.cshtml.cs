using System.Globalization;
using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Commerce.Storefront;
using Aero.Core.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class ProductDetailModel(IProductService products, IBasketService baskets, ISiteContext site, IStorefrontMemberAccessor storefrontMember) : PageModel
{
    public PublicListingResponse? Product { get; private set; }
    public int ItemCount { get; private set; }
    public StorefrontMemberStateKind MemberState { get; private set; } = StorefrontMemberStateKind.Unauthenticated;
    public bool LoadFailed { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct)
    {
        if (!CatalogSlug.IsCanonical(slug)) return NotFound();
        var result = await products.GetPublishedListingBySlugAsync(site.TenantId, site.SiteId, CultureInfo.CurrentUICulture.Name, slug, ct);
        if (result is Result<ProductListingDocument?, AeroError>.Failure)
        {
            LoadFailed = true;
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Page();
        }
        if (result is not Result<ProductListingDocument?, AeroError>.Ok { Value: { } listing }) return NotFound();
        Product = PublicListingResponse.From(listing);
        await LoadMemberStateAndItemCount(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAddToCartAsync(long listingId, CancellationToken ct)
    {
        var member = await storefrontMember.GetAsync(ct);
        if (member.Kind == StorefrontMemberStateKind.Unauthenticated) return Unauthorized();
        if (member.Kind == StorefrontMemberStateKind.NotCurrentSiteMember) return StatusCode(StatusCodes.Status403Forbidden);
        var memberId = member.MemberId!.Value;
        var result = await baskets.AddItemAsync(site.TenantId, site.SiteId, memberId, listingId, 1, CultureInfo.CurrentUICulture.Name, ct);
        return result.IsSuccess ? Redirect("/shop/cart") : NotFound();
    }

    private async Task LoadMemberStateAndItemCount(CancellationToken ct)
    {
        var member = await storefrontMember.GetAsync(ct);
        MemberState = member.Kind;
        if (!member.IsAuthorized) return;
        var basket = await baskets.GetAsync(site.TenantId, site.SiteId, member.MemberId!.Value, ct);
        if (basket is Result<BasketDocument?, AeroError>.Ok(var value) && value is not null) ItemCount = value.Items.Sum(x => x.Quantity);
    }
}
