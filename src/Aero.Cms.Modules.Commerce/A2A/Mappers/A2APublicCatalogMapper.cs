using Aero.Cms.Modules.Commerce.A2A.Models;
using Aero.Cms.Modules.Commerce.Catalog.Models;

namespace Aero.Cms.Modules.Commerce.A2A.Mappers;

/// <summary>Maps published storefront listings to the deliberately limited A2A catalog contract.</summary>
public static class A2APublicCatalogMapper
{
    /// <summary>Maps a published search page without ownership, inventory, audit, or concurrency data.</summary>
    public static A2ASearchProductsOutput ToSearchOutput((IReadOnlyList<ProductListingDocument> Items, long TotalCount) page)
        => new(page.Items.Select(ToListing).ToList(), page.TotalCount);

    /// <summary>Maps one published listing, or a safe empty catalog result, without internal data.</summary>
    public static A2AGetProductOutput ToProductOutput(ProductListingDocument? listing)
        => new(listing is null ? null : ToListing(listing));

    private static A2APublicListing ToListing(ProductListingDocument listing) => new(
        listing.Id,
        listing.Slug,
        listing.Name,
        listing.ShortDescription,
        listing.Description,
        listing.Category,
        listing.ImageUrl,
        listing.Price,
        listing.CompareAtPrice,
        listing.Currency,
        listing.IsFeatured);
}
