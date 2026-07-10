using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

/// <summary>
/// Represents a class for OrderDetailModel.
/// </summary>
[Microsoft.AspNetCore.Authorization.Authorize]
public class OrderDetailModel : PageModel
{
    private readonly IOrderService _orderService;

        /// <summary>
    /// Initializes a new instance of the <see cref="OrderDetailModel"/> class.
    /// </summary>
public OrderDetailModel(IOrderService orderService)
    {
        _orderService = orderService;
    }

        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public OrderEntity? Order { get; set; }

        /// <summary>
    /// OnGetAsync method.
    /// </summary>
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
