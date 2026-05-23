using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

[Microsoft.AspNetCore.Authorization.Authorize]
public class OrdersModel : PageModel
{
    private readonly IOrderService _orderService;

    public OrdersModel(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public List<OrderEntity>? Orders { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var customerId = User.Identity!.Name!;
        var orders = await _orderService.FindAsync(o => o.CustomerId == customerId);
        Orders = orders.OrderByDescending(o => o.CreatedOn).ToList();
        return Page();
    }
}
