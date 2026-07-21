using Aero.Cms.Modules.Commerce.Orders.Domain;

namespace Aero.Cms.Modules.Commerce.Orders.Services;

/// <summary>Creates, reads, and cancels orders only inside a tenant/site/member boundary.</summary>
public interface IOrderService
{
    Task<Result<OrderEntity, AeroError>> CheckoutAsync(long tenantId, long siteId, long externalMemberId, Address shippingAddress, Address? billingAddress, string culture, CancellationToken ct = default);
    Task<Result<(IReadOnlyList<OrderEntity> Items, long TotalCount), AeroError>> GetForMemberAsync(long tenantId, long siteId, long externalMemberId, int skip = 0, int take = 20, CancellationToken ct = default);
    Task<Result<OrderEntity?, AeroError>> GetForMemberAsync(long tenantId, long siteId, long externalMemberId, long orderId, CancellationToken ct = default);
    Task<Result<OrderEntity, AeroError>> CancelAsync(long tenantId, long siteId, long externalMemberId, long orderId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<OrderEntity>, AeroError>> GetExpiredSubmittedAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<Result<OrderEntity, AeroError>> TransitionAsync(long tenantId, long siteId, long orderId, OrderStatus target, CancellationToken ct = default);
}
