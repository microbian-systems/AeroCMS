using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

/// <summary>
/// Represents a class for CatalogModel.
/// </summary>
public class CatalogModel : PageModel
{
    private readonly IProductService _productService;
    private const int PageSize = 9;

        /// <summary>
    /// Initializes a new instance of the <see cref="CatalogModel"/> class.
    /// </summary>
public CatalogModel(IProductService productService)
    {
        _productService = productService;
    }

        /// <summary>
    /// Gets or sets the Products.
    /// </summary>
public IReadOnlyList<ProductDocument>? Products { get; set; }
        /// <summary>
    /// Gets or sets the Current Page.
    /// </summary>
public int CurrentPage { get; set; } = 1;
        /// <summary>
    /// Gets or sets the Total Pages.
    /// </summary>
public int TotalPages { get; set; }
        /// <summary>
    /// Gets or sets the Search.
    /// </summary>
public string? Search { get; set; }
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string? Category { get; set; }
        /// <summary>
    /// Gets or sets the Search View Model.
    /// </summary>
public CatalogSearchViewModel SearchViewModel { get; set; } = new([], null);

        /// <summary>
    /// OnGetAsync method.
    /// </summary>
public async Task<IActionResult> OnGetAsync(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] int page = 1)
    {
        Search = search;
        Category = category;
        CurrentPage = page;

        var result = await _productService.SearchAsync(
            search, category,
            skip: (page - 1) * PageSize, take: PageSize);

        if (result is Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>.Ok(var ok))
        {
            Products = ok.Items;
            TotalPages = (int)Math.Ceiling((double)ok.TotalCount / PageSize);

            // Get distinct categories for search sidebar
            var allResult = await _productService.SearchAsync(take: 1000);
            var categories = allResult is Result<(IReadOnlyList<ProductDocument> Items, long TotalCount), AeroError>.Ok(var all)
                ? all.Items.Where(p => p.Category is not null).Select(p => p.Category!).Distinct().OrderBy(c => c).ToList()
                : [];

            SearchViewModel = new CatalogSearchViewModel(categories, category);
        }

        return Page();
    }
}

/// <summary>
/// Represents a record for CatalogSearchViewModel.
/// </summary>
public record CatalogSearchViewModel(IReadOnlyList<string> Categories, string? CurrentCategory)
{
        /// <summary>
    /// CategoryUri method.
    /// </summary>
public string? CategoryUri(string? cat)
        => $"/shop/products?category={Uri.EscapeDataString(cat ?? "")}";
}
