using Aero.Core.Data;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Commerce.Payments;

public enum PaymentAttemptStatus { Initiating, RequiresCustomerAction, Succeeded, Failed, Cancelled, ManualReview }

/// <summary>Durable, non-secret record of one externally initiated payment operation.</summary>
public sealed class PaymentAttemptDocument : SableDocument, IAuditable, IVersioned
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public long ExternalMemberId { get; set; }
    public long OrderId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderAccountKey { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string RequestIdempotencyKey { get; set; } = string.Empty;
    public string ProviderOperationKey { get; set; } = string.Empty;
    public string? ProviderReference { get; set; }
    /// <summary>Latest time at which the stable provider operation may be safely resumed.</summary>
    public DateTimeOffset InitiationRetryExpiresAt { get; set; }
    public PaymentAttemptStatus Status { get; set; } = PaymentAttemptStatus.Initiating;
    public string? FailureOrReviewDetail { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public long Version { get; set; }
}

/// <summary>Deduplicates verified provider webhook receipts; the reference is non-null by construction.</summary>
public sealed class PaymentWebhookReceiptDocument : SableDocument, IVersioned
{
    public long TenantId { get; set; }
    public long SiteId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderAccountKey { get; set; } = string.Empty;
    public string ProviderEventId { get; set; } = string.Empty;
    public long PaymentAttemptId { get; set; }
    public DateTimeOffset ReceivedOn { get; set; } = DateTimeOffset.UtcNow;
    public long Version { get; set; }
}
