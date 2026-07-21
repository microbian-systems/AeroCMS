using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Aero.Core.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

[Authorize(Policy = ExternalMemberAuthenticationDefaults.Policy)]
[Authorize(Policy = ExternalMemberAuthenticationDefaults.SitePolicy)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class OrdersModel(IOrderService orders, ICurrentPrincipal principal, ISiteContext site) : PageModel
{
    public List<OrderEntity>? Orders { get; set; }
    public async Task<IActionResult> OnGetAsync()
    { var result = await orders.GetForMemberAsync(site.TenantId, site.SiteId, Member()); if (result is Result<(IReadOnlyList<OrderEntity> Items, long TotalCount), AeroError>.Ok(var page)) Orders = page.Items.ToList(); return Page(); }
    private long Member() => principal.PrincipalId ?? throw new InvalidOperationException("External member is required.");
}
