using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

/// <summary>
/// Represents a class for ShopHomeModel.
/// </summary>
public class ShopHomeModel : PageModel
{
    private readonly IProductService _productService;

        /// <summary>
    /// Initializes a new instance of the <see cref="ShopHomeModel"/> class.
    /// </summary>
public ShopHomeModel(IProductService productService)
    {
        _productService = productService;
    }

        /// <summary>
    /// Gets or sets the Featured Products.
    /// </summary>
public List<ProductDocument>? FeaturedProducts { get; set; }
        /// <summary>
    /// Gets or sets the Gallery Images.
    /// </summary>
public List<string> GalleryImages { get; set; } = [];

        /// <summary>
    /// OnGetAsync method.
    /// </summary>
public async Task<IActionResult> OnGetAsync()
    {
        var result = await _productService.SearchAsync(take: 3);
        if (result is Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>.Ok(var ok))
        {
            FeaturedProducts = ok.Items.ToList();
        }

        // Gallery images use static.photos placeholders (will be replaced by Pexels in a later update)
        GalleryImages =
        [
            "https://static.photos/outdoor/640x360/1",
            "https://static.photos/outdoor/640x360/2",
            "https://static.photos/outdoor/640x360/3",
            "https://static.photos/outdoor/640x360/4",
            "https://static.photos/outdoor/640x360/5",
            "https://static.photos/outdoor/640x360/6"
        ];

        return Page();
    }
}
