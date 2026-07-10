using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

/// <summary>
/// Represents a class for OrdersModel.
/// </summary>
[Microsoft.AspNetCore.Authorization.Authorize]
public class OrdersModel : PageModel
{
    private readonly IOrderService _orderService;

        /// <summary>
    /// Initializes a new instance of the <see cref="OrdersModel"/> class.
    /// </summary>
public OrdersModel(IOrderService orderService)
    {
        _orderService = orderService;
    }

        /// <summary>
    /// Gets or sets the Orders.
    /// </summary>
public List<OrderEntity>? Orders { get; set; }

        /// <summary>
    /// OnGetAsync method.
    /// </summary>
public async Task<IActionResult> OnGetAsync()
    {
        var customerId = User.Identity!.Name!;
        var orders = await _orderService.FindAsync(o => o.CustomerId == customerId);
        Orders = orders.OrderByDescending(o => o.CreatedOn).ToList();
        return Page();
    }
}
