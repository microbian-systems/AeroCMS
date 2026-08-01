using System.Text;
using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Commerce.Catalog.Models;

/// <summary>Anonymous storefront projection that deliberately excludes ownership, inventory, audit, and concurrency fields.</summary>
public sealed record PublicListingResponse(
    long Id,
    string Slug,
    string Name,
    string? ShortDescription,
    string? Description,
    string? Category,
    string? ImageUrl,
    decimal Price,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] decimal? CompareAtPrice,
    string Currency,
    bool IsFeatured,
    bool IsSubscription,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? SubscriptionIntervalDays)
{
    public static PublicListingResponse From(ProductListingDocument listing) => new(
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
        listing.IsFeatured,
        listing.SubscriptionOffer is not null,
        listing.SubscriptionOffer?.IntervalDays);
}

/// <summary>A page of anonymous storefront listings.</summary>
public sealed record PublicListingPage(IReadOnlyList<PublicListingResponse> Items, long TotalCount);

/// <summary>Produces canonical, route-safe ASCII slugs for storefront listings.</summary>
public static class CatalogSlug
{
    public static string Normalize(string? value)
    {
        var source = (value ?? string.Empty).Trim().ToLowerInvariant();
        var result = new StringBuilder(source.Length);
        var pendingSeparator = false;

        foreach (var character in source)
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingSeparator && result.Length > 0)
                    result.Append('-');
                result.Append(character);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = result.Length > 0;
            }
        }

        return result.ToString();
    }

    public static bool IsCanonical(string? value)
        => !string.IsNullOrWhiteSpace(value) && string.Equals(value, Normalize(value), StringComparison.Ordinal);
}
