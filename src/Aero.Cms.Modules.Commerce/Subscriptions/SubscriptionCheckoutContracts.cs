using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Payments;

namespace Aero.Cms.Modules.Commerce.Subscriptions;

/// <summary>Server-generated continuation targets for provider-hosted subscription checkout.</summary>
public sealed record SubscriptionCheckoutRequest(long OrderId, string Provider, string OperationKey, Uri SuccessUrl, Uri CancelUrl);

/// <summary>Non-secret provider result. Approval URLs are returned to the browser but never persisted.</summary>
public sealed record SubscriptionCheckoutInitiation(long SubscriptionId, string ProviderCheckoutReference, string ApprovalUrl);

public enum SubscriptionCheckoutDisposition { Succeeded, RetryableUncertain, TerminalFailure }

public sealed record SubscriptionCheckoutOutcome(SubscriptionCheckoutDisposition Disposition, SubscriptionCheckoutInitiation? Initiation, string? Detail)
{
    public static SubscriptionCheckoutOutcome Succeeded(SubscriptionCheckoutInitiation initiation) => new(SubscriptionCheckoutDisposition.Succeeded, initiation, null);
    public static SubscriptionCheckoutOutcome Retryable(string? detail = null) => new(SubscriptionCheckoutDisposition.RetryableUncertain, null, detail);
    public static SubscriptionCheckoutOutcome Terminal(string? detail = null) => new(SubscriptionCheckoutDisposition.TerminalFailure, null, detail);
}

public sealed record SubscriptionProviderCheckout(string OperationKey, long OrderId, IReadOnlyList<OrderItem> Items, Uri SuccessUrl, Uri CancelUrl);

public interface ISubscriptionCheckoutProviderAdapter
{
    string Provider { get; }
    Task<SubscriptionCheckoutOutcome> InitiateAsync(PaymentProviderAccount account, SubscriptionProviderCheckout request, CancellationToken ct = default);
    Task<Result<SubscriptionCheckoutInitiation, AeroError>> RetrieveAsync(PaymentProviderAccount account, string checkoutReference, long subscriptionId, CancellationToken ct = default);
}

public interface ISubscriptionCheckoutService
{
    Task<Result<SubscriptionCheckoutInitiation, AeroError>> InitiateAsync(long tenantId, long siteId, long memberId, SubscriptionCheckoutRequest request, CancellationToken ct = default);
    Task<Result<SubscriptionDocument?, AeroError>> GetForMemberAsync(long tenantId, long siteId, long memberId, long orderId, CancellationToken ct = default);
}
