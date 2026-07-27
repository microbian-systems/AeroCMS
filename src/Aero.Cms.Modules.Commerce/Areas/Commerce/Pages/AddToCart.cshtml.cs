using System.Globalization;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Core.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

/// <summary>
/// Provides the private, antiforgery-protected confirmation step for PageEditor product fragments.
/// The fragment only links here; it never embeds per-request antiforgery material in public page output.
/// </summary>
[Authorize(Policy = ExternalMemberAuthenticationDefaults.Policy)]
[Authorize(Policy = ExternalMemberAuthenticationDefaults.SitePolicy)]
[AutoValidateAntiforgeryToken]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class AddToCartModel(
    IProductService products,
    IBasketService baskets,
    ICurrentPrincipal principal,
    ISiteContext site) : PageModel
{
    public PublicListingResponse? Product { get; private set; }
    public bool LoadFailed { get; private set; }
    public string CartPath => StorefrontPath("/shop/cart");

    public async Task<IActionResult> OnGetAsync(long listingId, CancellationToken ct)
    {
        if (listingId <= 0)
            return NotFound();

        var listing = await products.GetPublishedListingAsync(
            site.TenantId,
            site.SiteId,
            CultureInfo.CurrentUICulture.Name,
            listingId,
            ct);
        if (listing is Result<ProductListingDocument?, AeroError>.Failure)
        {
            LoadFailed = true;
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Page();
        }

        if (listing is not Result<ProductListingDocument?, AeroError>.Ok { Value: { } value })
            return NotFound();

        Product = PublicListingResponse.From(value);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long listingId, CancellationToken ct)
    {
        var memberId = principal.PrincipalId;
        if (listingId <= 0 || memberId is not > 0)
            return NotFound();

        // BasketService remains authoritative: it re-resolves publication, active state,
        // tenant, site, culture, and current pricing before it persists any line item.
        var added = await baskets.AddItemAsync(
            site.TenantId,
            site.SiteId,
            memberId.Value,
            listingId,
            1,
            CultureInfo.CurrentUICulture.Name,
            ct);
        return added.IsSuccess ? Redirect(CartPath) : NotFound();
    }

    private static string StorefrontPath(string path)
        => "/" + path.TrimStart('/') + "?culture=" + Uri.EscapeDataString(CultureInfo.CurrentUICulture.Name);
}
