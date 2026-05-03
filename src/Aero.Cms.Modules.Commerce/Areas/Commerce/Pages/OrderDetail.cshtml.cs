using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

[Microsoft.AspNetCore.Authorization.Authorize]
public class OrderDetailModel : PageModel
{
    private readonly IOrderService _orderService;

    public OrderDetailModel(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public OrderEntity? Order { get; set; }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var customerId = User.Identity!.Name!;

        try
        {
            var order = await _orderService.FindByIdAsync(id);
            if (order.CustomerId == customerId)
            {
                Order = order;
                return Page();
            }
        }
        catch { /* not found — fall through to 404 */ }

        return NotFound();
    }
}
