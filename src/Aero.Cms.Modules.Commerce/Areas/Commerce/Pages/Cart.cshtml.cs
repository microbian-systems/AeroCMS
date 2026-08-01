using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Core.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

[Authorize(Policy = ExternalMemberAuthenticationDefaults.Policy)]
[Authorize(Policy = ExternalMemberAuthenticationDefaults.SitePolicy)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class CartModel(IBasketService baskets, ICurrentPrincipal principal, ISiteContext site) : PageModel
{
    public List<BasketItem>? Items { get; set; }
    public int TotalQuantity => Items?.Sum(x => x.Quantity) ?? 0;
    public decimal? TotalPrice => Items?.Sum(x => x.TotalPrice);
    public async Task<IActionResult> OnGetAsync() { var result = await baskets.GetOrCreateAsync(site.TenantId, site.SiteId, Member()); if (result is Result<BasketDocument, AeroError>.Ok(var basket)) Items = basket.Items; return Page(); }
    public async Task<IActionResult> OnPostUpdateQuantityAsync(long listingId, int quantity) { await baskets.UpdateQuantityAsync(site.TenantId, site.SiteId, Member(), listingId, quantity, System.Globalization.CultureInfo.CurrentUICulture.Name); return RedirectToPage(); }
    private long Member() => principal.PrincipalId ?? throw new InvalidOperationException("External member is required.");
}
