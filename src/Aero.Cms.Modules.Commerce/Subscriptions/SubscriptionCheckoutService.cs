using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Payments;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Commerce.Subscriptions;

/// <summary>
/// Persists the provider/order binding before external checkout creation. The stable operation
/// key lets a retry ask the same provider for the same checkout without retaining browser URLs.
/// </summary>
public sealed class SubscriptionCheckoutService(
    IDocumentSession session,
    IPaymentProviderRegistry registry,
    IEnumerable<ISubscriptionCheckoutProviderAdapter> adapters) : ISubscriptionCheckoutService
{
    public async Task<Result<SubscriptionCheckoutInitiation, AeroError>> InitiateAsync(long tenantId, long siteId, long memberId, SubscriptionCheckoutRequest request, CancellationToken ct = default)
    {
        if (tenantId <= 0 || siteId <= 0 || memberId <= 0 || request.OrderId <= 0 || !IsHttps(request.SuccessUrl) || !IsHttps(request.CancelUrl)
            || !string.Equals(request.OperationKey, $"commerce-subscription-order-{request.OrderId}", StringComparison.Ordinal))
            return Fail("Invalid subscription checkout request.");

        var accountResult = registry.GetAccount(request.Provider, tenantId, siteId);
        var adapterMatches = adapters.Where(x => string.Equals(x.Provider, request.Provider, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        var adapter = adapterMatches.Count == 1 ? adapterMatches[0] : null;
        if (accountResult is not Result<PaymentProviderAccount, AeroError>.Ok(var account) || adapter is null)
            return Fail("Subscription provider is unavailable.");

        OrderEntity? order;
        try { order = await FindOrderAsync(tenantId, siteId, memberId, request.OrderId, ct); }
        catch { return Fail("Subscription checkout could not be loaded."); }
        if (order is null) return Fail("Order not found.");
        if (!IsRecurringOrder(order) || order.Items.Count > 20) return Fail("Order is not eligible for subscription checkout.");
        if (!HasProviderOffer(order, account.Provider)) return Fail("The selected provider is not configured for this subscription.");

        SubscriptionDocument? existing;
        try { existing = await FindSubscriptionAsync(tenantId, siteId, memberId, order.Id, ct); }
        catch { return Fail("Subscription checkout could not be loaded."); }
        if (existing is null)
        {
            existing = new SubscriptionDocument
            {
                Id = Snowflake.NewId(), TenantId = tenantId, SiteId = siteId, ExternalMemberId = memberId, OrderId = order.Id,
                Provider = account.Provider, ProviderAccountKey = account.AccountKey, ProviderOperationKey = request.OperationKey,
                Lines = CreateLines(order, account.Provider), Currency = order.Currency, IntervalDays = order.BillingIntervalDays!.Value,
                State = SubscriptionState.PendingProviderConfirmation, CreatedOn = DateTimeOffset.UtcNow
            };
            try
            {
                // This durable write is the compare-and-swap binding point; no external I/O happened yet.
                order.PaymentStatus = OrderPaymentStatus.Pending;
                order.ModifiedOn = DateTimeOffset.UtcNow;
                session.Store(order); session.Store(existing);
                await session.SaveChangesAsync(ct);
            }
            catch (Exception)
            {
                session.ClearChanges();
                existing = await FindSubscriptionAsync(tenantId, siteId, memberId, order.Id, ct);
                if (existing is null) return Fail("Subscription checkout could not be saved; retry with the same order.");
            }
        }

        if (!string.Equals(existing.Provider, account.Provider, StringComparison.Ordinal) || !string.Equals(existing.ProviderOperationKey, request.OperationKey, StringComparison.Ordinal))
            return Fail("An existing subscription checkout is already bound to this order.");
        return await ContinueAsync(existing, account, adapter, request.SuccessUrl, request.CancelUrl, ct);
    }

    public async Task<Result<SubscriptionDocument?, AeroError>> GetForMemberAsync(long tenantId, long siteId, long memberId, long orderId, CancellationToken ct = default)
    {
        try { return Prelude.Ok<SubscriptionDocument?, AeroError>(await FindSubscriptionAsync(tenantId, siteId, memberId, orderId, ct)); }
        catch { return Prelude.Fail<SubscriptionDocument?, AeroError>(AeroError.CreateError("Subscription checkout status could not be loaded.")); }
    }

    private async Task<Result<SubscriptionCheckoutInitiation, AeroError>> ContinueAsync(SubscriptionDocument subscription, PaymentProviderAccount account, ISubscriptionCheckoutProviderAdapter adapter, Uri successUrl, Uri cancelUrl, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(subscription.ProviderCheckoutReference))
            return await adapter.RetrieveAsync(account, subscription.ProviderCheckoutReference, subscription.Id, ct);

        var order = await FindOrderAsync(subscription.TenantId, subscription.SiteId, subscription.ExternalMemberId, subscription.OrderId, ct);
        if (order is null || !IsRecurringOrder(order)) return Fail("Subscription order is no longer available.");
        var outcome = await adapter.InitiateAsync(account, new SubscriptionProviderCheckout(subscription.ProviderOperationKey, order.Id, order.Items, successUrl, cancelUrl), ct);
        if (outcome.Disposition != SubscriptionCheckoutDisposition.Succeeded || outcome.Initiation is null)
            return Fail(outcome.Disposition == SubscriptionCheckoutDisposition.RetryableUncertain
                ? "Subscription checkout could not be confirmed; resume this order rather than creating another."
                : "Subscription checkout was rejected by the payment provider.");

        try
        {
            subscription.ProviderCheckoutReference = outcome.Initiation.ProviderCheckoutReference;
            subscription.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(subscription); await session.SaveChangesAsync(ct);
            return Prelude.Ok<SubscriptionCheckoutInitiation, AeroError>(outcome.Initiation with { SubscriptionId = subscription.Id });
        }
        catch (Exception)
        {
            session.ClearChanges();
            var winner = await FindSubscriptionAsync(subscription.TenantId, subscription.SiteId, subscription.ExternalMemberId, subscription.OrderId, ct);
            if (winner is not null && string.Equals(winner.ProviderCheckoutReference, outcome.Initiation.ProviderCheckoutReference, StringComparison.Ordinal))
                return Prelude.Ok<SubscriptionCheckoutInitiation, AeroError>(outcome.Initiation with { SubscriptionId = winner.Id });
            return Fail("Subscription checkout was created but could not be recorded; resume the order to recover it.");
        }
    }

    private Task<OrderEntity?> FindOrderAsync(long tenantId, long siteId, long memberId, long orderId, CancellationToken ct)
        => session.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == orderId && x.TenantId == tenantId && x.SiteId == siteId && x.ExternalMemberId == memberId, ct);
    private Task<SubscriptionDocument?> FindSubscriptionAsync(long tenantId, long siteId, long memberId, long orderId, CancellationToken ct)
        => session.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SiteId == siteId && x.ExternalMemberId == memberId && x.OrderId == orderId, ct);
    private static bool IsHttps(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps;
    private static bool IsRecurringOrder(OrderEntity order) => order.BillingKind == OrderBillingKind.Recurring && order.Status == OrderStatus.Submitted && order.PaymentStatus is OrderPaymentStatus.Unpaid or OrderPaymentStatus.Pending && order.BillingIntervalDays is >= 1 and <= 365 && order.Items.Count > 0 && order.Items.All(x => x.BillingKind == OrderBillingKind.Recurring && x.FulfillmentMode == Aero.Cms.Modules.Commerce.Catalog.Models.ProductFulfillmentMode.NonInventoryRecurring && x.BillingIntervalDays == order.BillingIntervalDays);
    private static bool HasProviderOffer(OrderEntity order, string provider) => provider == "stripe" ? order.Items.All(x => !string.IsNullOrWhiteSpace(x.StripePriceId)) : provider == "paypal" && order.Items.Count == 1 && !string.IsNullOrWhiteSpace(order.Items[0].PayPalPlanId);
    private static List<SubscriptionLineSnapshot> CreateLines(OrderEntity order, string provider) => order.Items.Select(x => new SubscriptionLineSnapshot { ProductId = x.ProductId, ListingId = x.ListingId, ProductName = x.ProductName, ListingName = x.ProductName, Sku = x.Sku, Quantity = x.Quantity, UnitAmount = x.UnitPrice, ProviderOfferReference = provider == "stripe" ? x.StripePriceId! : x.PayPalPlanId! }).ToList();
    private static Result<SubscriptionCheckoutInitiation, AeroError> Fail(string message) => Prelude.Fail<SubscriptionCheckoutInitiation, AeroError>(AeroError.CreateError(message));
}
