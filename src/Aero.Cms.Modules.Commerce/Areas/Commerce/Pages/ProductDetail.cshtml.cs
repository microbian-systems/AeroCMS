using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

/// <summary>
/// Represents a class for ProductDetailModel.
/// </summary>
public class ProductDetailModel : PageModel
{
    private readonly IProductService _productService;
    private readonly IBasketService _basketService;

        /// <summary>
    /// Initializes a new instance of the <see cref="ProductDetailModel"/> class.
    /// </summary>
public ProductDetailModel(IProductService productService, IBasketService basketService)
    {
        _productService = productService;
        _basketService = basketService;
    }

        /// <summary>
    /// Gets or sets the Product.
    /// </summary>
public ProductDocument? Product { get; set; }
        /// <summary>
    /// Gets or sets the Item Count.
    /// </summary>
public int ItemCount { get; set; }

        /// <summary>
    /// OnGetAsync method.
    /// </summary>
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

        /// <summary>
    /// OnPostAddToCartAsync method.
    /// </summary>
public async Task<IActionResult> OnPostAddToCartAsync(long productId)
    {
        var customerId = GetCustomerId();
        var productResult = await _productService.GetByIdAsync(productId);
        if (productResult is not Result<ProductDocument?, AeroError>.Ok(var product) || product is null) return NotFound();

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
