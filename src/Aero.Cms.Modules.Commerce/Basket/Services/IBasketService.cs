using Aero.Cms.Modules.Commerce.Basket.Models;

namespace Aero.Cms.Modules.Commerce.Basket.Services;

/// <summary>Creates and mutates externally-owned baskets from authoritative listings.</summary>
public interface IBasketService
{
    Task<Result<BasketDocument?, AeroError>> GetAsync(long tenantId, long siteId, long externalMemberId, CancellationToken ct = default);
    Task<Result<BasketDocument, AeroError>> GetOrCreateAsync(long tenantId, long siteId, long externalMemberId, CancellationToken ct = default);
    Task<Result<BasketDocument, AeroError>> AddItemAsync(long tenantId, long siteId, long externalMemberId, long listingId, int quantity, string culture, CancellationToken ct = default);
    Task<Result<BasketDocument, AeroError>> UpdateQuantityAsync(long tenantId, long siteId, long externalMemberId, long listingId, int quantity, string culture, CancellationToken ct = default);
    Task<Result<BasketDocument, AeroError>> ClearAsync(long tenantId, long siteId, long externalMemberId, CancellationToken ct = default);
}
