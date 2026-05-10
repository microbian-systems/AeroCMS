using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

public class CatalogModel : PageModel
{
    private readonly IProductService _productService;
    private const int PageSize = 9;

    public CatalogModel(IProductService productService)
    {
        _productService = productService;
    }

    public IReadOnlyList<ProductDocument>? Products { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public string? Search { get; set; }
    public string? Category { get; set; }
    public CatalogSearchViewModel SearchViewModel { get; set; } = new([], null);

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

public record CatalogSearchViewModel(IReadOnlyList<string> Categories, string? CurrentCategory)
{
    public string? CategoryUri(string? cat)
        => $"/shop/products?category={Uri.EscapeDataString(cat ?? "")}";
}
