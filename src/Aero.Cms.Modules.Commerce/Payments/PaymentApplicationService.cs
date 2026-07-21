using Aero.Cms.Modules.Commerce.Orders.Domain;
using AeroDB.Sable;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Commerce.Payments;

public sealed record InitiatePaymentRequest(long OrderId, string Provider, string IdempotencyKey);

public interface IPaymentApplicationService
{
    Task<Result<PaymentInitiation, AeroError>> InitiateAsync(long tenantId, long siteId, long memberId, InitiatePaymentRequest request, CancellationToken ct = default);
    Task<Result<PaymentAttemptDocument?, AeroError>> GetForMemberAsync(long tenantId, long siteId, long memberId, long orderId, CancellationToken ct = default);
    Task<Result<bool, AeroError>> ReconcileAsync(string provider, string accountKey, byte[] raw, IHeaderDictionary headers, CancellationToken ct = default);
}

/// <summary>Coordinates one durable external payment attempt per customer order.</summary>
public sealed class PaymentApplicationService(
    IDocumentSession session,
    IPaymentProviderRegistry registry,
    IValidator<InitiatePaymentRequest> requestValidator) : IPaymentApplicationService
{
    private static readonly TimeSpan StripeRetryWindow = TimeSpan.FromHours(23);
    private static readonly TimeSpan PayPalRetryWindow = TimeSpan.FromHours(5);

    public async Task<Result<PaymentInitiation, AeroError>> InitiateAsync(long tenantId, long siteId, long memberId, InitiatePaymentRequest request, CancellationToken ct = default)
    {
        var validation = await requestValidator.ValidateAsync(request, ct);
        if (!validation.IsValid || tenantId <= 0 || siteId <= 0 || memberId <= 0)
            return Fail("Invalid payment request.");

        if (registry.GetAccount(request.Provider, tenantId, siteId) is not Result<PaymentProviderAccount, AeroError>.Ok(var account)
            || registry.Resolve(request.Provider, tenantId, siteId) is not Result<IPaymentProviderAdapter, AeroError>.Ok(var adapter))
            return Fail("Payment provider is unavailable.");

        var order = await FindOrderAsync(tenantId, siteId, memberId, request.OrderId, ct);
        if (order is null) return Fail("Order not found.");
        if (!PaymentAmountLimits.IsValidUsd(order.TotalAmount) || order.Currency != "USD") return Fail("Order amount is invalid.");

        // The order is the idempotency boundary. A request key is only a replay key for it.
        var existing = await FindAttemptForOrderAsync(tenantId, siteId, memberId, request.OrderId, ct);
        if (existing is not null)
        {
            if (!string.Equals(existing.Provider, account.Provider, StringComparison.Ordinal)
                || !string.Equals(existing.RequestIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
                return Fail("An existing payment attempt is already bound to this order.");
            return await ContinueInitiationAsync(tenantId, siteId, memberId, existing.Id, account, adapter, ct);
        }

        if (order.Status is not OrderStatus.Submitted || order.PaymentStatus is not OrderPaymentStatus.Unpaid)
            return Fail("Order cannot be paid.");

        var now = DateTimeOffset.UtcNow;
        var attempt = new PaymentAttemptDocument
        {
            Id = Snowflake.NewId(), TenantId = tenantId, SiteId = siteId, ExternalMemberId = memberId, OrderId = order.Id,
            Provider = account.Provider, ProviderAccountKey = account.AccountKey, Amount = order.TotalAmount, Currency = "USD",
            RequestIdempotencyKey = request.IdempotencyKey, ProviderOperationKey = $"commerce-attempt-{Snowflake.NewId()}",
            CreatedOn = now, Status = PaymentAttemptStatus.Initiating, InitiationRetryExpiresAt = now.Add(GetRetryWindow(account.Provider))
        };

        try
        {
            order.PaymentStatus = OrderPaymentStatus.Pending;
            order.ModifiedOn = now;
            session.Store(order);
            session.Store(attempt);
            await session.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            session.ClearChanges();
            var concurrent = await FindAttemptForOrderAsync(tenantId, siteId, memberId, request.OrderId, ct);
            if (concurrent is not null)
            {
                if (!string.Equals(concurrent.Provider, account.Provider, StringComparison.Ordinal)
                    || !string.Equals(concurrent.RequestIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
                    return Fail("An existing payment attempt is already bound to this order.");
                return await ContinueInitiationAsync(tenantId, siteId, memberId, concurrent.Id, account, adapter, ct);
            }
            return Fail("Payment initiation could not be saved; retry with the same idempotency key.");
        }

        return await ContinueInitiationAsync(tenantId, siteId, memberId, attempt.Id, account, adapter, ct);
    }

    public async Task<Result<PaymentAttemptDocument?, AeroError>> GetForMemberAsync(long tenantId, long siteId, long memberId, long orderId, CancellationToken ct = default)
    {
        try { return Prelude.Ok<PaymentAttemptDocument?, AeroError>(await FindAttemptForOrderAsync(tenantId, siteId, memberId, orderId, ct)); }
        catch { return Prelude.Fail<PaymentAttemptDocument?, AeroError>(AeroError.CreateError("Payment status could not be loaded.")); }
    }

    public async Task<Result<bool, AeroError>> ReconcileAsync(string provider, string accountKey, byte[] raw, IHeaderDictionary headers, CancellationToken ct = default)
    {
        if (registry.GetAccountByKey(provider, accountKey) is not Result<PaymentProviderAccount, AeroError>.Ok(var account)
            || registry.Resolve(account.Provider, account.TenantId, account.SiteId) is not Result<IPaymentProviderAdapter, AeroError>.Ok(var adapter))
            return Prelude.Fail<bool, AeroError>(AeroError.CreateError("Unknown payment provider account."));

        var verified = await adapter.VerifyAndTranslateAsync(account, raw, headers, ct);
        if (verified is not Result<VerifiedPaymentCallback, AeroError>.Ok(var callback))
            return Prelude.Fail<bool, AeroError>(AeroError.CreateError("Invalid webhook."));

        try
        {
            var receipt = await session.Query<PaymentWebhookReceiptDocument>().FirstOrDefaultAsync(x => x.Provider == account.Provider && x.ProviderAccountKey == account.AccountKey && x.ProviderEventId == callback.EventId, ct);
            if (receipt is not null) return Prelude.Ok<bool, AeroError>(true);
            var attempt = await session.Query<PaymentAttemptDocument>().FirstOrDefaultAsync(x => x.TenantId == account.TenantId && x.SiteId == account.SiteId && x.Provider == account.Provider && x.ProviderAccountKey == account.AccountKey && x.ProviderReference == callback.ProviderReference, ct);
            if (attempt is null) return Prelude.Fail<bool, AeroError>(AeroError.CreateError("Payment reference not found."));
            var order = await FindOrderAsync(attempt.TenantId, attempt.SiteId, attempt.ExternalMemberId, attempt.OrderId, ct);
            if (order is null) return Prelude.Fail<bool, AeroError>(AeroError.CreateError("Order not found."));

            if (callback.Amount != attempt.Amount || callback.Currency != "USD" || callback.Currency != attempt.Currency || order.Currency != attempt.Currency || order.TotalAmount != attempt.Amount)
            {
                attempt.Status = PaymentAttemptStatus.ManualReview;
                attempt.FailureOrReviewDetail = "Webhook amount or currency mismatch.";
                order.PaymentStatus = OrderPaymentStatus.ManualReview;
            }
            else ApplyCallbackState(attempt, order, callback);

            attempt.ModifiedOn = order.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(attempt); session.Store(order);
            session.Store(new PaymentWebhookReceiptDocument { Id = Snowflake.NewId(), TenantId = attempt.TenantId, SiteId = attempt.SiteId, Provider = attempt.Provider, ProviderAccountKey = attempt.ProviderAccountKey, ProviderEventId = callback.EventId, PaymentAttemptId = attempt.Id, ReceivedOn = DateTimeOffset.UtcNow });
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception)
        {
            session.ClearChanges();
            return Prelude.Fail<bool, AeroError>(AeroError.CreateError("Webhook reconciliation conflicted; retry delivery."));
        }
    }

    private async Task<Result<PaymentInitiation, AeroError>> ContinueInitiationAsync(long tenantId, long siteId, long memberId, long attemptId, PaymentProviderAccount account, IPaymentProviderAdapter adapter, CancellationToken ct)
    {
        var attempt = await FindAttemptByIdAsync(tenantId, siteId, memberId, attemptId, ct);
        var order = attempt is null ? null : await FindOrderAsync(tenantId, siteId, memberId, attempt.OrderId, ct);
        if (attempt is null || order is null) return Fail("Payment attempt not found.");
        if (!string.Equals(attempt.Provider, account.Provider, StringComparison.Ordinal) || !PaymentAmountLimits.IsValidUsd(attempt.Amount)) return Fail("Payment attempt is invalid.");

        if (attempt.Status == PaymentAttemptStatus.RequiresCustomerAction && !string.IsNullOrWhiteSpace(attempt.ProviderReference))
        {
            if (order.Status != OrderStatus.Submitted || order.PaymentStatus != OrderPaymentStatus.Pending)
                return Fail("Order cannot be paid.");
            var resumed = await adapter.RetrieveAsync(account, attempt.ProviderReference, ct);
            return resumed is Result<PaymentInitiation, AeroError>.Ok(var action)
                ? Prelude.Ok<PaymentInitiation, AeroError>(WithAttemptId(action, attempt.Id))
                : resumed;
        }
        if (attempt.Status is not PaymentAttemptStatus.Initiating)
            return ToReplayResult(attempt);

        if (DateTimeOffset.UtcNow >= attempt.InitiationRetryExpiresAt)
        {
            await MarkManualReviewAsync(tenantId, siteId, memberId, attempt.Id, "Provider initiation retry window elapsed.", ct);
            return Fail("Payment requires manual review.");
        }

        // Persist a CAS-guarded initiation gate immediately before provider I/O. Cancellation
        // writes the same order; either it wins, or the caller observes the conflict and makes no call.
        if (!await AcquireInitiationGateAsync(tenantId, siteId, memberId, attempt.Id, ct)) return Fail("Order cannot be paid.");

        var outcome = await adapter.InitiateAsync(account, new PaymentProviderInitiation(attempt.ProviderOperationKey, attempt.Amount, attempt.Currency, order.Id), ct);
        return outcome.Disposition switch
        {
            PaymentInitiationDisposition.Succeeded when outcome.Initiation is not null => await PersistSuccessfulInitiationAsync(tenantId, siteId, memberId, attempt.Id, outcome.Initiation, ct),
            PaymentInitiationDisposition.TerminalFailure => await PersistTerminalFailureAsync(tenantId, siteId, memberId, attempt.Id, outcome.Detail, ct),
            _ => await HandleAmbiguousInitiationAsync(tenantId, siteId, memberId, attempt.Id, outcome.Detail, ct)
        };
    }

    private async Task<bool> AcquireInitiationGateAsync(long tenantId, long siteId, long memberId, long attemptId, CancellationToken ct)
    {
        try
        {
            var attempt = await FindAttemptByIdAsync(tenantId, siteId, memberId, attemptId, ct);
            var order = attempt is null ? null : await FindOrderAsync(tenantId, siteId, memberId, attempt.OrderId, ct);
            if (attempt is null || order is null || attempt.Status != PaymentAttemptStatus.Initiating || order.Status != OrderStatus.Submitted || order.PaymentStatus != OrderPaymentStatus.Pending) return false;
            attempt.ModifiedOn = order.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(attempt); session.Store(order);
            await session.SaveChangesAsync(ct);
            return true;
        }
        catch { session.ClearChanges(); return false; }
    }

    private async Task<Result<PaymentInitiation, AeroError>> PersistSuccessfulInitiationAsync(long tenantId, long siteId, long memberId, long attemptId, PaymentInitiation initiation, CancellationToken ct)
    {
        try
        {
            var attempt = await FindAttemptByIdAsync(tenantId, siteId, memberId, attemptId, ct);
            var order = attempt is null ? null : await FindOrderAsync(tenantId, siteId, memberId, attempt.OrderId, ct);
            if (attempt is null || order is null) return Fail("Payment attempt not found.");
            attempt.ProviderReference = initiation.ProviderReference;
            attempt.ModifiedOn = DateTimeOffset.UtcNow;
            if (order.Status == OrderStatus.Cancelled)
            {
                attempt.Status = PaymentAttemptStatus.ManualReview;
                attempt.FailureOrReviewDetail = "Provider response arrived after order cancellation.";
                order.PaymentStatus = OrderPaymentStatus.ManualReview;
            }
            else
            {
                attempt.Status = initiation.Status;
                order.PaymentStatus = initiation.Status switch
                {
                    PaymentAttemptStatus.Succeeded => OrderPaymentStatus.Paid,
                    PaymentAttemptStatus.Failed => OrderPaymentStatus.Failed,
                    PaymentAttemptStatus.Cancelled => OrderPaymentStatus.Cancelled,
                    PaymentAttemptStatus.ManualReview => OrderPaymentStatus.ManualReview,
                    _ => OrderPaymentStatus.Pending
                };
            }
            order.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(attempt); session.Store(order);
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<PaymentInitiation, AeroError>(WithAttemptId(initiation, attempt.Id));
        }
        catch { session.ClearChanges(); return Fail("Payment response could not be saved; retry with the same idempotency key."); }
    }

    private async Task<Result<PaymentInitiation, AeroError>> PersistTerminalFailureAsync(long tenantId, long siteId, long memberId, long attemptId, string? detail, CancellationToken ct)
    {
        try
        {
            var attempt = await FindAttemptByIdAsync(tenantId, siteId, memberId, attemptId, ct);
            var order = attempt is null ? null : await FindOrderAsync(tenantId, siteId, memberId, attempt.OrderId, ct);
            if (attempt is null || order is null) return Fail("Payment attempt not found.");
            attempt.Status = PaymentAttemptStatus.Failed; attempt.FailureOrReviewDetail = detail; attempt.ModifiedOn = DateTimeOffset.UtcNow;
            order.PaymentStatus = OrderPaymentStatus.Failed; order.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(attempt); session.Store(order); await session.SaveChangesAsync(ct);
        }
        catch { session.ClearChanges(); }
        return Fail("Payment initiation was rejected.");
    }

    private async Task<Result<PaymentInitiation, AeroError>> HandleAmbiguousInitiationAsync(long tenantId, long siteId, long memberId, long attemptId, string? detail, CancellationToken ct)
    {
        var attempt = await FindAttemptByIdAsync(tenantId, siteId, memberId, attemptId, ct);
        if (attempt is null) return Fail("Payment attempt not found.");
        if (DateTimeOffset.UtcNow >= attempt.InitiationRetryExpiresAt)
        {
            await MarkManualReviewAsync(tenantId, siteId, memberId, attemptId, detail ?? "Provider initiation retry window elapsed.", ct);
            return Fail("Payment requires manual review.");
        }
        return Fail("Payment initiation could not be confirmed; retry with the same idempotency key.");
    }

    private async Task MarkManualReviewAsync(long tenantId, long siteId, long memberId, long attemptId, string detail, CancellationToken ct)
    {
        try
        {
            var attempt = await FindAttemptByIdAsync(tenantId, siteId, memberId, attemptId, ct);
            var order = attempt is null ? null : await FindOrderAsync(tenantId, siteId, memberId, attempt.OrderId, ct);
            if (attempt is null || order is null) return;
            attempt.Status = PaymentAttemptStatus.ManualReview; attempt.FailureOrReviewDetail = detail; attempt.ModifiedOn = DateTimeOffset.UtcNow;
            order.PaymentStatus = OrderPaymentStatus.ManualReview; order.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(attempt); session.Store(order); await session.SaveChangesAsync(ct);
        }
        catch { session.ClearChanges(); }
    }

    private static void ApplyCallbackState(PaymentAttemptDocument attempt, OrderEntity order, VerifiedPaymentCallback callback)
    {
        if (order.Status == OrderStatus.Cancelled && callback.Status == PaymentAttemptStatus.Succeeded)
        {
            attempt.Status = PaymentAttemptStatus.ManualReview; attempt.FailureOrReviewDetail = "Successful callback after order cancellation."; order.PaymentStatus = OrderPaymentStatus.ManualReview; return;
        }
        if (order.PaymentStatus == OrderPaymentStatus.Paid && callback.Status != PaymentAttemptStatus.Succeeded)
        {
            attempt.Status = PaymentAttemptStatus.ManualReview; attempt.FailureOrReviewDetail = "Conflicting payment callback after a successful payment."; order.PaymentStatus = OrderPaymentStatus.ManualReview; return;
        }
        switch (callback.Status)
        {
            case PaymentAttemptStatus.Succeeded: attempt.Status = PaymentAttemptStatus.Succeeded; attempt.FailureOrReviewDetail = null; order.PaymentStatus = OrderPaymentStatus.Paid; break;
            case PaymentAttemptStatus.Cancelled: attempt.Status = PaymentAttemptStatus.Cancelled; attempt.FailureOrReviewDetail = callback.Detail; order.PaymentStatus = OrderPaymentStatus.Cancelled; break;
            case PaymentAttemptStatus.Failed: attempt.Status = PaymentAttemptStatus.Failed; attempt.FailureOrReviewDetail = callback.Detail; order.PaymentStatus = OrderPaymentStatus.Failed; break;
            default: attempt.Status = PaymentAttemptStatus.ManualReview; attempt.FailureOrReviewDetail = callback.Detail ?? "Unhandled provider payment state."; order.PaymentStatus = OrderPaymentStatus.ManualReview; break;
        }
    }

    private static PaymentInitiation WithAttemptId(PaymentInitiation value, long attemptId) => value with { AttemptId = attemptId };
    private static PaymentInitiation ToPersistedInitiation(PaymentAttemptDocument attempt) => new(attempt.Id, attempt.ProviderReference ?? string.Empty, attempt.Status, null, null);
    private static Result<PaymentInitiation, AeroError> ToReplayResult(PaymentAttemptDocument attempt) =>
        attempt.Status switch
        {
            PaymentAttemptStatus.Failed => Fail("Payment initiation was rejected."),
            PaymentAttemptStatus.ManualReview => Fail("Payment requires manual review."),
            PaymentAttemptStatus.Cancelled => Fail("Payment was cancelled."),
            _ => Prelude.Ok<PaymentInitiation, AeroError>(ToPersistedInitiation(attempt))
        };
    private static TimeSpan GetRetryWindow(string provider) => provider == "paypal" ? PayPalRetryWindow : StripeRetryWindow;
    private static Result<PaymentInitiation, AeroError> Fail(string message) => Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError(message));
    private Task<OrderEntity?> FindOrderAsync(long tenantId, long siteId, long memberId, long orderId, CancellationToken ct) => session.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == orderId && x.TenantId == tenantId && x.SiteId == siteId && x.ExternalMemberId == memberId, ct);
    private Task<PaymentAttemptDocument?> FindAttemptForOrderAsync(long tenantId, long siteId, long memberId, long orderId, CancellationToken ct) => session.Query<PaymentAttemptDocument>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SiteId == siteId && x.ExternalMemberId == memberId && x.OrderId == orderId, ct);
    private Task<PaymentAttemptDocument?> FindAttemptByIdAsync(long tenantId, long siteId, long memberId, long attemptId, CancellationToken ct) => session.Query<PaymentAttemptDocument>().FirstOrDefaultAsync(x => x.Id == attemptId && x.TenantId == tenantId && x.SiteId == siteId && x.ExternalMemberId == memberId, ct);
}
