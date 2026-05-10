using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

public class ProductDetailModel : PageModel
{
    private readonly IProductService _productService;
    private readonly IBasketService _basketService;

    public ProductDetailModel(IProductService productService, IBasketService basketService)
    {
        _productService = productService;
        _basketService = basketService;
    }

    public ProductDocument? Product { get; set; }
    public int ItemCount { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var result = await _productService.FindBySlugAsync(slug);
        if (result is Result<ProductDocument?, AeroError>.Ok(var product) && product is not null)
        {
            Product = product;
            await LoadItemCount();
            return Page();
        }

        return NotFound();
    }

    public async Task<IActionResult> OnPostAddToCartAsync(long productId)
    {
        var customerId = GetCustomerId();
        var product = await _productService.FindByIdAsync(productId);
        if (product is null) return NotFound();

        var item = new BasketItem
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Sku = product.Sku,
            ImageUrl = product.ImageUrl,
            Quantity = 1,
            UnitPrice = product.Price
        };

        await _basketService.AddItemAsync(customerId, item);
        return RedirectToPage();
    }

    private async Task LoadItemCount()
    {
        var customerId = GetCustomerId();
        var basketResult = await _basketService.GetOrCreateBasketAsync(customerId);
        if (basketResult is Result<BasketDocument, AeroError>.Ok(var basket))
        {
            ItemCount = basket.Items.Sum(i => i.Quantity);
        }
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
