using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

/// <summary>
/// Represents a class for CartModel.
/// </summary>
public class CartModel : PageModel
{
    private readonly IBasketService _basketService;

        /// <summary>
    /// Initializes a new instance of the <see cref="CartModel"/> class.
    /// </summary>
public CartModel(IBasketService basketService)
    {
        _basketService = basketService;
    }

        /// <summary>
    /// Gets or sets the Items.
    /// </summary>
public List<BasketItem>? Items { get; set; }
        /// <summary>
    /// Gets or sets the Total Quantity.
    /// </summary>
public int TotalQuantity => Items?.Sum(i => i.Quantity) ?? 0;
        /// <summary>
    /// Gets or sets the Total Price.
    /// </summary>
public decimal? TotalPrice => Items?.Sum(i => i.TotalPrice);

        /// <summary>
    /// OnGetAsync method.
    /// </summary>
public async Task<IActionResult> OnGetAsync()
    {
        var customerId = GetCustomerId();
        var result = await _basketService.GetOrCreateBasketAsync(customerId);
        if (result is Result<BasketDocument, AeroError>.Ok(var basket))
        {
            Items = basket.Items;
        }
        return Page();
    }

        /// <summary>
    /// OnPostUpdateQuantityAsync method.
    /// </summary>
public async Task<IActionResult> OnPostUpdateQuantityAsync(long productId, int quantity)
    {
        var customerId = GetCustomerId();

        if (quantity <= 0)
        {
            await _basketService.RemoveItemAsync(customerId, productId);
        }
        else
        {
            var basketResult = await _basketService.GetOrCreateBasketAsync(customerId);
            if (basketResult is Result<BasketDocument, AeroError>.Ok(var basket))
            {
                await _basketService.ClearBasketAsync(customerId);
                foreach (var item in basket.Items)
                {
                    var qty = item.ProductId == productId ? quantity : item.Quantity;
                    await _basketService.AddItemAsync(customerId, item with { Quantity = qty });
                }
            }
        }

        return RedirectToPage();
    }

    private string GetCustomerId()
    {
        if (User.Identity?.IsAuthenticated == true)
            return User.Identity.Name!;

        if (Request.Cookies.TryGetValue("shop_cart_id", out var cartId) && !string.IsNullOrEmpty(cartId))
            return cartId;

        cartId = Snowflake.NewId().ToString();
        Response.Cookies.Append("shop_cart_id", cartId, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax
        });
        return cartId;
    }
}
