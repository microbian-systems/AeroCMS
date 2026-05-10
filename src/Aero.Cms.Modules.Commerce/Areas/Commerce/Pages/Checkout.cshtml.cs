using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Handlers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wolverine;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

[Microsoft.AspNetCore.Authorization.Authorize]
public class CheckoutModel : PageModel
{
    private readonly IBasketService _basketService;
    private readonly IMessageBus _bus;

    public CheckoutModel(IBasketService basketService, IMessageBus bus)
    {
        _basketService = basketService;
        _bus = bus;
    }

    [BindProperty]
    public string? Street { get; set; }
    [BindProperty]
    public string? City { get; set; }
    [BindProperty]
    public string? State { get; set; }
    [BindProperty]
    public string? ZipCode { get; set; }
    [BindProperty]
    public string? Country { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostPlaceOrderAsync()
    {
        if (!ModelState.IsValid) return Page();

        var customerId = User.Identity!.Name!;

        // Validate basket exists
        var basketResult = await _basketService.GetOrCreateBasketAsync(customerId);
        if (basketResult is Result<BasketDocument, AeroError>.Failure _)
        {
            ModelState.AddModelError("", "Could not load your basket.");
            return Page();
        }

        var basket = ((Result<BasketDocument, AeroError>.Ok)basketResult).Value;
        if (basket.Items.Count == 0)
        {
            ModelState.AddModelError("", "Your basket is empty.");
            return Page();
        }

        // Send the CreateOrder Wolverine command
        await _bus.InvokeAsync(new CreateOrder(
            customerId,
            new Address
            {
                Street = Street ?? "",
                City = City ?? "",
                State = State,
                PostalCode = ZipCode ?? "",
                Country = Country ?? ""
            }
        ));

        return Redirect("/shop/orders");
    }
}
