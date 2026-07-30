using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Shared.StateMachine;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Commerce.Orders.Services;

/// <summary>Coordinates stock reservation, order creation, and basket clearing in one save batch.</summary>
public sealed class OrderService(IDocumentSession session) : IOrderService
{
    public async Task<Result<OrderEntity, AeroError>> CheckoutAsync(long tenantId, long siteId, long externalMemberId, Address shippingAddress, Address? billingAddress, string culture, CancellationToken ct = default)
    {
        try
        {
            var basket = await session.Query<BasketDocument>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SiteId == siteId && x.ExternalMemberId == externalMemberId && x.Currency == "USD", ct);
            if (basket is null || basket.Items.Count == 0) return Prelude.Fail<OrderEntity, AeroError>(AeroError.CreateError("Basket is empty."));
            var order = new OrderEntity { Id = Snowflake.NewId(), TenantId = tenantId, SiteId = siteId, ExternalMemberId = externalMemberId, Currency = "USD", Status = OrderStatus.Submitted, ShippingAddress = shippingAddress, BillingAddress = billingAddress ?? shippingAddress, CreatedOn = DateTimeOffset.UtcNow };
            foreach (var item in basket.Items)
            {
                var listing = await session.Query<ProductListingDocument>().FirstOrDefaultAsync(x => x.Id == item.ListingId && x.TenantId == tenantId && x.SiteId == siteId && x.Culture == culture && x.IsPublished && x.Currency == "USD", ct);
                if (listing is null) { session.ClearChanges(); return Prelude.Fail<OrderEntity, AeroError>(AeroError.CreateError("A basket listing is no longer available.")); }
                var product = await session.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == listing.ProductId && x.TenantId == tenantId && x.IsActive, ct);
                if (product is null || item.ProductId != product.Id || item.Quantity <= 0)
                {
                    session.ClearChanges(); return Prelude.Fail<OrderEntity, AeroError>(AeroError.CreateError("A basket item is no longer available."));
                }

                if (!TryResolveBilling(listing, product, item, out var billingKind, out var intervalDays, out var failure))
                {
                    session.ClearChanges(); return Prelude.Fail<OrderEntity, AeroError>(AeroError.CreateError(failure));
                }
                if (order.Items.Count > 0 && (order.BillingKind != billingKind || billingKind == OrderBillingKind.Recurring && order.BillingIntervalDays != intervalDays))
                {
                    session.ClearChanges(); return Prelude.Fail<OrderEntity, AeroError>(AeroError.CreateError("A checkout cannot mix one-time and recurring items or recurring intervals."));
                }

                order.BillingKind = billingKind;
                order.BillingIntervalDays = intervalDays;
                if (product.FulfillmentMode == ProductFulfillmentMode.Inventory)
                {
                    if (product.StockQuantity < item.Quantity) { session.ClearChanges(); return Prelude.Fail<OrderEntity, AeroError>(AeroError.CreateError("Insufficient stock.")); }
                    product.StockQuantity -= item.Quantity; product.ModifiedOn = DateTimeOffset.UtcNow; session.Store(product);
                }
                order.Items.Add(new OrderItem
                {
                    ListingId = listing.Id, ProductId = product.Id, ProductName = listing.Name, Sku = product.Sku,
                    Quantity = item.Quantity, UnitPrice = listing.Price, Currency = "USD", BillingKind = billingKind,
                    FulfillmentMode = product.FulfillmentMode, BillingIntervalDays = intervalDays,
                    StripePriceId = listing.SubscriptionOffer?.StripePriceId, PayPalPlanId = listing.SubscriptionOffer?.PayPalPlanId
                });
            }
            if (order.BillingKind == OrderBillingKind.OneTime) order.GracePeriodExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
            basket.Items.Clear(); basket.ModifiedOn = DateTimeOffset.UtcNow; session.Store(basket); session.Store(order);
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<OrderEntity, AeroError>(order);
        }
        catch (Exception ex) { session.ClearChanges(); return Prelude.Fail<OrderEntity, AeroError>(AeroError.CreateError(ex.Message)); }
    }

    public async Task<Result<(IReadOnlyList<OrderEntity> Items, long TotalCount), AeroError>> GetForMemberAsync(long tenantId, long siteId, long externalMemberId, int skip = 0, int take = 20, CancellationToken ct = default)
    {
        try { var orders = await session.Query<OrderEntity>().Where(x => x.TenantId == tenantId && x.SiteId == siteId && x.ExternalMemberId == externalMemberId).ToListAsync(ct); var total = orders.Count; return Prelude.Ok<(IReadOnlyList<OrderEntity>, long), AeroError>((orders.OrderByDescending(x => x.CreatedOn).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 100)).ToList(), total)); }
        catch (Exception ex) { return Prelude.Fail<(IReadOnlyList<OrderEntity>, long), AeroError>(AeroError.CreateError(ex.Message)); }
    }

    public async Task<Result<OrderEntity?, AeroError>> GetForMemberAsync(long tenantId, long siteId, long externalMemberId, long orderId, CancellationToken ct = default)
    { try { return Prelude.Ok<OrderEntity?, AeroError>(await session.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == orderId && x.TenantId == tenantId && x.SiteId == siteId && x.ExternalMemberId == externalMemberId, ct)); } catch (Exception ex) { return Prelude.Fail<OrderEntity?, AeroError>(AeroError.CreateError(ex.Message)); } }

    public async Task<Result<OrderEntity, AeroError>> CancelAsync(long tenantId, long siteId, long externalMemberId, long orderId, CancellationToken ct = default)
    {
        try
        {
            var order = await session.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == orderId && x.TenantId == tenantId && x.SiteId == siteId && x.ExternalMemberId == externalMemberId, ct);
            if (order is null || order.BillingKind == OrderBillingKind.Recurring || order.Status is not (OrderStatus.Submitted or OrderStatus.AwaitingValidation)
                || order.PaymentStatus is not (OrderPaymentStatus.Unpaid or OrderPaymentStatus.Failed or OrderPaymentStatus.Cancelled))
                return Prelude.Fail<OrderEntity, AeroError>(AeroError.CreateError("Order cannot be cancelled."));
            foreach (var item in order.Items)
            {
                if (item.FulfillmentMode != ProductFulfillmentMode.Inventory) continue;
                var product = await session.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == item.ProductId && x.TenantId == tenantId, ct);
                if (product is null) { session.ClearChanges(); return Prelude.Fail<OrderEntity, AeroError>(AeroError.CreateError("Order not found.")); }
                product.StockQuantity += item.Quantity; product.ModifiedOn = DateTimeOffset.UtcNow; session.Store(product);
            }
            order.Status = OrderStatus.Cancelled; order.ModifiedOn = DateTimeOffset.UtcNow; session.Store(order); await session.SaveChangesAsync(ct);
            return Prelude.Ok<OrderEntity, AeroError>(order);
        }
        catch (Exception ex) { session.ClearChanges(); return Prelude.Fail<OrderEntity, AeroError>(AeroError.CreateError(ex.Message)); }
    }

    public async Task<Result<IReadOnlyList<OrderEntity>, AeroError>> GetExpiredSubmittedAsync(DateTimeOffset now, CancellationToken ct = default)
    { try { return Prelude.Ok<IReadOnlyList<OrderEntity>, AeroError>(await session.Query<OrderEntity>().Where(x => x.Status == OrderStatus.Submitted && x.BillingKind == OrderBillingKind.OneTime && x.GracePeriodExpiresAt <= now).ToListAsync(ct)); } catch (Exception ex) { return Prelude.Fail<IReadOnlyList<OrderEntity>, AeroError>(AeroError.CreateError(ex.Message)); } }
    public async Task<Result<OrderEntity, AeroError>> TransitionAsync(long tenantId, long siteId, long orderId, OrderStatus target, CancellationToken ct = default)
    {
        try { var order = await session.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == orderId && x.TenantId == tenantId && x.SiteId == siteId, ct); if (order is null) return Prelude.Fail<OrderEntity, AeroError>(AeroError.CreateError("Order not found.")); var changed = OrderStateMachine.Transition(order, target); if (changed is Result<OrderEntity, AeroError>.Failure failure) return failure; order.ModifiedOn = DateTimeOffset.UtcNow; session.Store(order); await session.SaveChangesAsync(ct); return Prelude.Ok<OrderEntity, AeroError>(order); } catch (Exception ex) { return Prelude.Fail<OrderEntity, AeroError>(AeroError.CreateError(ex.Message)); }
    }

    private static bool TryResolveBilling(ProductListingDocument listing, ProductDocument product, BasketItem basketItem, out OrderBillingKind billingKind, out int? intervalDays, out string failure)
    {
        billingKind = OrderBillingKind.OneTime; intervalDays = null; failure = string.Empty;
        if (product.FulfillmentMode is ProductFulfillmentMode.Inventory or ProductFulfillmentMode.NonInventoryOneTime)
        {
            if (basketItem.BillingKind == BasketBillingKind.OneTime && listing.SubscriptionOffer is null) return true;
            failure = "A one-time basket item is no longer available.";
            return false;
        }

        var offer = listing.SubscriptionOffer;
        if (product.FulfillmentMode != ProductFulfillmentMode.NonInventoryRecurring || offer is null || offer.IntervalDays is < 1 or > 365
            || string.IsNullOrWhiteSpace(offer.StripePriceId) && string.IsNullOrWhiteSpace(offer.PayPalPlanId)
            || basketItem.BillingKind != BasketBillingKind.Recurring || basketItem.BillingIntervalDays != offer.IntervalDays)
        {
            failure = "A recurring basket item is no longer available.";
            return false;
        }
        billingKind = OrderBillingKind.Recurring; intervalDays = offer.IntervalDays;
        return true;
    }
}
