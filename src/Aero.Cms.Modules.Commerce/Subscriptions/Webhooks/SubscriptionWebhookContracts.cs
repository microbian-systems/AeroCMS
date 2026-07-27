using Aero.Cms.Modules.Commerce.Payments;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Commerce.Subscriptions.Webhooks;

public enum SubscriptionWebhookEventKind
{
    CheckoutCompleted,
    SubscriptionActivated,
    SubscriptionReactivated,
    SubscriptionUpdated,
    SubscriptionCancelled,
    SubscriptionExpired,
    SubscriptionSuspended,
    InvoicePaid,
    InvoicePaymentFailed,
    PaymentActionRequired,
    Unknown
}

/// <summary>Verified, safe provider metadata. No raw payload, signature, secret, or continuation URL crosses this boundary.</summary>
public sealed record VerifiedSubscriptionWebhook(
    string EventId,
    DateTimeOffset OccurredOn,
    SubscriptionWebhookEventKind Kind,
    string? CheckoutReference,
    string? SubscriptionReference,
    string? CustomerReference,
    string? CycleReference,
    string? PaymentReference,
    decimal? Amount,
    string? Currency,
    DateTimeOffset? PeriodStartsOn,
    DateTimeOffset? PeriodEndsOn,
    IReadOnlyList<string> ProviderOfferReferences,
    string? Detail);

public interface ISubscriptionWebhookProviderAdapter
{
    string Provider { get; }

    /// <summary>Authenticates the raw provider request before producing a parsed, safe callback.</summary>
    Task<Result<VerifiedSubscriptionWebhook, AeroError>> VerifyAndTranslateSubscriptionAsync(
        PaymentProviderAccount account,
        byte[] rawBody,
        IHeaderDictionary headers,
        CancellationToken ct = default);
}

public interface ISubscriptionReconciliationService
{
    Task<Result<bool, AeroError>> ReconcileAsync(
        string provider,
        string accountKey,
        byte[] rawBody,
        IHeaderDictionary headers,
        CancellationToken ct = default);
}
