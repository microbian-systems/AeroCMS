using System.Globalization;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Core.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class CatalogModel(IProductService products, ISiteContext site) : PageModel
{
    private const int PageSize = 9;
    public IReadOnlyList<PublicListingResponse> Products { get; private set; } = [];
    public int CurrentPage { get; private set; } = 1;
    public int TotalPages { get; private set; }
    public string? Search { get; private set; }
    public string? Category { get; private set; }
    public bool LoadFailed { get; private set; }
    public CatalogPaginationWindow Pagination { get; private set; } = CatalogPaginationWindow.Empty;
    public CatalogSearchViewModel SearchViewModel { get; private set; } = new([], null, null);

    public async Task<IActionResult> OnGetAsync([FromQuery] string? search, [FromQuery] string? category, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        Search = NormalizeFilter(search);
        Category = NormalizeFilter(category);
        if (page < 1)
            return RedirectToCanonicalPage(1);

        CurrentPage = page;
        var requestedSkip = Math.Min((long)(page - 1) * PageSize, int.MaxValue);
        var result = await products.SearchPublishedAsync(site.TenantId, site.SiteId, CultureInfo.CurrentUICulture.Name, Search, Category, (int)requestedSkip, PageSize, ct: ct);
        if (result is Result<(IReadOnlyList<ProductListingDocument> Items, long TotalCount), AeroError>.Ok(var current))
        {
            Products = current.Items.Select(PublicListingResponse.From).ToList();
            var pageCount = current.TotalCount / PageSize + (current.TotalCount % PageSize == 0 ? 0 : 1);
            TotalPages = pageCount > int.MaxValue ? int.MaxValue : (int)pageCount;
            var canonicalPage = TotalPages == 0 ? 1 : Math.Min(CurrentPage, TotalPages);
            if (CurrentPage != canonicalPage)
                return RedirectToCanonicalPage(canonicalPage);
            Pagination = CatalogPaginationWindow.Create(CurrentPage, TotalPages);

            var categories = await products.GetPublishedCategoriesAsync(site.TenantId, site.SiteId, CultureInfo.CurrentUICulture.Name, ct);
            if (categories is Result<IReadOnlyList<string>, AeroError>.Ok(var values))
                SearchViewModel = new(values, Category, Search);
            else
                return CatalogUnavailable();
        }
        else
            return CatalogUnavailable();

        return Page();
    }

    private IActionResult RedirectToCanonicalPage(int page)
        => RedirectToPage("Catalog", new { search = Search, category = Category, page = page == 1 ? null : (int?)page });

    private IActionResult CatalogUnavailable()
    {
        LoadFailed = true;
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return Page();
    }

    private static string? NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CatalogPaginationWindow(IReadOnlyList<int> Pages, bool HasPrevious, bool HasNext)
{
    public static CatalogPaginationWindow Empty { get; } = new([], false, false);

    public static CatalogPaginationWindow Create(int currentPage, int totalPages, int windowSize = 5)
    {
        if (totalPages <= 0) return Empty;
        var size = Math.Clamp(windowSize, 1, 21);
        var current = Math.Clamp(currentPage, 1, totalPages);
        var start = Math.Max(1, current - size / 2);
        var end = Math.Min(totalPages, start + size - 1);
        start = Math.Max(1, end - size + 1);
        return new(Enumerable.Range(start, end - start + 1).ToList(), current > 1, current < totalPages);
    }
}

public sealed record CatalogSearchViewModel(IReadOnlyList<string> Categories, string? CurrentCategory, string? Search)
{
    public string CategoryUri(string? category)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(Search)) query.Add($"search={Uri.EscapeDataString(Search)}");
        if (!string.IsNullOrWhiteSpace(category)) query.Add($"category={Uri.EscapeDataString(category)}");
        return query.Count == 0 ? "/shop/products" : $"/shop/products?{string.Join('&', query)}";
    }
}
