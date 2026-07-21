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
public class OrderDetailModel(IOrderService orders, ICurrentPrincipal principal, ISiteContext site) : PageModel
{
    public OrderEntity? Order { get; set; }
    public async Task<IActionResult> OnGetAsync(long id)
    { var result = await orders.GetForMemberAsync(site.TenantId, site.SiteId, Member(), id); if (result is Result<OrderEntity?, AeroError>.Ok(var order) && order is not null) { Order = order; return Page(); } return NotFound(); }
    private long Member() => principal.PrincipalId ?? throw new InvalidOperationException("External member is required.");
}
