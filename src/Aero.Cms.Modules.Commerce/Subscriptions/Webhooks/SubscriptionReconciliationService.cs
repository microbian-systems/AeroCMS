using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Payments;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Commerce.Subscriptions.Webhooks;

/// <summary>
/// Applies only authenticated provider lifecycle facts to the immutable subscription ledger.
/// It never schedules, retries, charges, creates another order, or touches inventory.
/// </summary>
public sealed class SubscriptionReconciliationService(
    IDocumentSession session,
    IPaymentProviderRegistry registry,
    IEnumerable<ISubscriptionWebhookProviderAdapter> adapters) : ISubscriptionReconciliationService
{
    public async Task<Result<bool, AeroError>> ReconcileAsync(
        string provider,
        string accountKey,
        byte[] rawBody,
        Microsoft.AspNetCore.Http.IHeaderDictionary headers,
        CancellationToken ct = default)
    {
        // Route-key account resolution exposes only configured accounts. Parsing and all document
        // lookups deliberately occur after the provider has authenticated the raw request.
        if (registry.GetAccountByKey(provider, accountKey) is not Result<PaymentProviderAccount, AeroError>.Ok(var account))
            return Fail("Unknown subscription provider account.");

        var matches = adapters.Where(x => string.Equals(x.Provider, account.Provider, StringComparison.Ordinal)).Take(2).ToList();
        if (matches.Count != 1) return Fail("Subscription provider is unavailable.");

        var verified = await matches[0].VerifyAndTranslateSubscriptionAsync(account, rawBody, headers, ct);
        if (verified is not Result<VerifiedSubscriptionWebhook, AeroError>.Ok(var callback)) return Fail("Invalid subscription webhook.");
        if (!HasSafeIdentity(callback)) return Fail("Invalid subscription webhook.");

        try
        {
            var duplicate = await session.Query<SubscriptionWebhookReceiptDocument>()
                .FirstOrDefaultAsync(x => x.Provider == account.Provider && x.ProviderAccountKey == account.AccountKey && x.ProviderEventId == callback.EventId, ct);
            if (duplicate is not null) return Prelude.Ok<bool, AeroError>(true);

            var subscriptions = await FindSubscriptionsAsync(account, callback, ct);
            if (subscriptions.Count != 1)
                // A verified event with no unique local binding must remain retryable. Do not consume
                // its provider event ID, because a preceding checkout/subscription write may still win.
                return Fail("Subscription reference was not found.");

            var subscription = subscriptions[0];
            if (!ReferencesAreConsistent(subscription, callback))
                return await PersistManualReviewAsync(account, subscription, callback, "Provider subscription reference mismatch.", ct);

            if (subscription.LastAppliedProviderOccurredOn is { } last && callback.OccurredOn < last)
            {
                session.Store(Receipt(account, subscription, callback, null, SubscriptionWebhookReceiptState.Ignored));
                await session.SaveChangesAsync(ct);
                return Prelude.Ok<bool, AeroError>(true);
            }

            if (callback.Kind == SubscriptionWebhookEventKind.Unknown)
            {
                session.Store(Receipt(account, subscription, callback, null, SubscriptionWebhookReceiptState.Ignored));
                await session.SaveChangesAsync(ct);
                return Prelude.Ok<bool, AeroError>(true);
            }

            if (HasOfferReferenceMismatch(subscription, callback))
                return await PersistManualReviewAsync(account, subscription, callback, "Provider offer reference mismatch.", ct);

            if (subscription.State == SubscriptionState.ManualReview)
                return await PersistManualReviewAsync(account, subscription, callback, subscription.ManualReviewReason ?? "Subscription requires manual review.", ct);

            if (callback.Kind == SubscriptionWebhookEventKind.InvoicePaid)
            {
                if (subscription.State is SubscriptionState.Cancelled or SubscriptionState.Expired)
                    return await PersistManualReviewAsync(account, subscription, callback, "A paid callback cannot reactivate a terminal subscription.", ct);
                var mismatch = "Provider billing period is unavailable.";
                if (!TryNormalizeProviderPeriod(subscription, callback, out var paidCallback) || !HasMatchingCycleSnapshot(subscription, paidCallback, out mismatch))
                    return await PersistManualReviewAsync(account, subscription, callback, mismatch, ct);
                return await ApplyPaidCycleAsync(account, subscription, paidCallback, ct);
            }

            SubscriptionCycleDocument? reconciledCycle = null;
            string? reconciliationManualReason = null;
            if (callback.Kind == SubscriptionWebhookEventKind.InvoicePaymentFailed)
                (reconciledCycle, reconciliationManualReason) = await ApplyFailureAsync(account, subscription, callback, ct);
            else
                ApplyLifecycle(subscription, callback);

            subscription.LastAppliedProviderEventId = callback.EventId;
            subscription.LastAppliedProviderOccurredOn = callback.OccurredOn;
            subscription.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(subscription);
            session.Store(Receipt(account, subscription, callback, reconciledCycle, reconciliationManualReason is null ? SubscriptionWebhookReceiptState.Applied : SubscriptionWebhookReceiptState.ManualReview, reconciliationManualReason));
            await session.SaveChangesAsync(ct);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception)
        {
            session.ClearChanges();
            return Fail("Subscription webhook reconciliation conflicted; retry delivery.");
        }
    }

    private async Task<Result<bool, AeroError>> ApplyPaidCycleAsync(PaymentProviderAccount account, SubscriptionDocument subscription, VerifiedSubscriptionWebhook callback, CancellationToken ct)
    {
        var paymentReference = callback.PaymentReference!;
        SubscriptionCycleDocument? existing = null;
        if (!string.IsNullOrWhiteSpace(callback.CycleReference))
            existing = await session.Query<SubscriptionCycleDocument>().FirstOrDefaultAsync(x =>
                x.Provider == account.Provider && x.ProviderAccountKey == account.AccountKey && x.ProviderCycleReference == callback.CycleReference, ct);
        if (existing is null)
            existing = await session.Query<SubscriptionCycleDocument>().FirstOrDefaultAsync(x =>
                x.Provider == account.Provider && x.ProviderAccountKey == account.AccountKey && x.ProviderPaymentReference == paymentReference, ct);

        SubscriptionCycleDocument cycle;
        if (existing is null)
        {
            var cycles = await session.Query<SubscriptionCycleDocument>().Where(x => x.SubscriptionId == subscription.Id).ToListAsync(ct);
            cycle = new SubscriptionCycleDocument
            {
                Id = Snowflake.NewId(), TenantId = subscription.TenantId, SiteId = subscription.SiteId,
                ExternalMemberId = subscription.ExternalMemberId, SubscriptionId = subscription.Id,
                CycleNumber = cycles.Count == 0 ? 1 : cycles.Max(x => x.CycleNumber) + 1,
                Provider = account.Provider, ProviderAccountKey = account.AccountKey,
                ProviderCycleReference = callback.CycleReference ?? paymentReference,
                ProviderPaymentReference = paymentReference, Lines = subscription.Lines.Select(CloneLine).ToList(),
                AmountSnapshot = callback.Amount!.Value, Currency = "USD",
                PeriodStartsOn = callback.PeriodStartsOn!.Value, PeriodEndsOn = callback.PeriodEndsOn!.Value,
                State = SubscriptionCycleState.Paid, LastAppliedProviderEventId = callback.EventId,
                LastAppliedProviderOccurredOn = callback.OccurredOn, CreatedOn = DateTimeOffset.UtcNow
            };
            session.Store(cycle);
        }
        else
        {
            cycle = existing;
            if (cycle.SubscriptionId != subscription.Id || cycle.AmountSnapshot != callback.Amount || cycle.Currency != "USD"
                || cycle.PeriodStartsOn != callback.PeriodStartsOn || cycle.PeriodEndsOn != callback.PeriodEndsOn)
                return await PersistManualReviewAsync(account, subscription, callback, "Provider payment reference conflicts with an existing cycle.", ct, cycle);
            if (cycle.LastAppliedProviderOccurredOn is null || callback.OccurredOn >= cycle.LastAppliedProviderOccurredOn)
            {
                cycle.State = SubscriptionCycleState.Paid;
                cycle.ProviderPaymentReference ??= paymentReference;
                cycle.LastAppliedProviderEventId = callback.EventId;
                cycle.LastAppliedProviderOccurredOn = callback.OccurredOn;
                cycle.ModifiedOn = DateTimeOffset.UtcNow;
                session.Store(cycle);
            }
        }

        var order = await session.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == subscription.OrderId && x.TenantId == subscription.TenantId && x.SiteId == subscription.SiteId && x.ExternalMemberId == subscription.ExternalMemberId, ct);
        if (order is null) return Fail("Subscription order was not found.");
        // This is the initial recurring order's payment acknowledgement only. Renewals only append
        // immutable cycles above; they never create OrderEntity rows or decrement stock.
        if (order.PaymentStatus != OrderPaymentStatus.Paid)
        {
            order.PaymentStatus = OrderPaymentStatus.Paid;
            order.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(order);
        }

        subscription.State = SubscriptionState.Active;
        subscription.RequiresManualReview = false;
        subscription.ManualReviewReason = null;
        subscription.ManualReviewRequestedOn = null;
        subscription.LastAppliedProviderEventId = callback.EventId;
        subscription.LastAppliedProviderOccurredOn = callback.OccurredOn;
        subscription.CurrentPeriodStartsOn = callback.PeriodStartsOn;
        subscription.CurrentPeriodEndsOn = callback.PeriodEndsOn;
        subscription.ModifiedOn = DateTimeOffset.UtcNow;
        session.Store(subscription);
        session.Store(Receipt(account, subscription, callback, cycle, SubscriptionWebhookReceiptState.Applied));
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<bool, AeroError>(true);
    }

    private async Task<(SubscriptionCycleDocument? Cycle, string? ManualReason)> ApplyFailureAsync(PaymentProviderAccount account, SubscriptionDocument subscription, VerifiedSubscriptionWebhook callback, CancellationToken ct)
    {
        subscription.State = SubscriptionState.PastDue;
        subscription.LastAppliedProviderEventId = callback.EventId;
        subscription.LastAppliedProviderOccurredOn = callback.OccurredOn;
        var cycleReference = callback.CycleReference ?? callback.PaymentReference;
        SubscriptionCycleDocument? resolvedCycle = null;
        if (!string.IsNullOrWhiteSpace(cycleReference))
        {
            var cycle = await session.Query<SubscriptionCycleDocument>().FirstOrDefaultAsync(x => x.Provider == account.Provider && x.ProviderAccountKey == account.AccountKey && x.ProviderCycleReference == cycleReference, ct);
            if (cycle is null && HasMatchingCycleSnapshot(subscription, callback, out _, requirePaymentReference: false))
            {
                var cycles = await session.Query<SubscriptionCycleDocument>().Where(x => x.SubscriptionId == subscription.Id).ToListAsync(ct);
                cycle = new SubscriptionCycleDocument
                {
                    Id = Snowflake.NewId(), TenantId = subscription.TenantId, SiteId = subscription.SiteId, ExternalMemberId = subscription.ExternalMemberId,
                    SubscriptionId = subscription.Id, CycleNumber = cycles.Count == 0 ? 1 : cycles.Max(x => x.CycleNumber) + 1,
                    Provider = account.Provider, ProviderAccountKey = account.AccountKey, ProviderCycleReference = cycleReference,
                    ProviderPaymentReference = callback.PaymentReference, Lines = subscription.Lines.Select(CloneLine).ToList(),
                    AmountSnapshot = callback.Amount!.Value, Currency = "USD", PeriodStartsOn = callback.PeriodStartsOn!.Value,
                    PeriodEndsOn = callback.PeriodEndsOn!.Value, State = SubscriptionCycleState.Failed,
                    LastAppliedProviderEventId = callback.EventId, LastAppliedProviderOccurredOn = callback.OccurredOn, CreatedOn = DateTimeOffset.UtcNow
                };
                session.Store(cycle);
                resolvedCycle = cycle;
            }
            else if (cycle is not null && (cycle.LastAppliedProviderOccurredOn is null || callback.OccurredOn >= cycle.LastAppliedProviderOccurredOn))
            {
                if (cycle.State == SubscriptionCycleState.Paid)
                {
                    const string reason = "Provider failure conflicts with an already paid subscription cycle.";
                    cycle.State = SubscriptionCycleState.ManualReview;
                    cycle.RequiresManualReview = true;
                    cycle.ManualReviewReason = reason;
                    cycle.ManualReviewRequestedOn = DateTimeOffset.UtcNow;
                    subscription.State = SubscriptionState.ManualReview;
                    subscription.RequiresManualReview = true;
                    subscription.ManualReviewReason = reason;
                    subscription.ManualReviewRequestedOn = DateTimeOffset.UtcNow;
                    cycle.LastAppliedProviderEventId = callback.EventId;
                    cycle.LastAppliedProviderOccurredOn = callback.OccurredOn;
                    cycle.ModifiedOn = DateTimeOffset.UtcNow;
                    session.Store(cycle);
                    var paidOrder = await FindOrderAsync(subscription, ct);
                    if (paidOrder is not null) { paidOrder.PaymentStatus = OrderPaymentStatus.ManualReview; paidOrder.ModifiedOn = DateTimeOffset.UtcNow; session.Store(paidOrder); }
                    return (cycle, reason);
                }
                cycle.State = SubscriptionCycleState.Failed;
                cycle.ProviderPaymentReference ??= callback.PaymentReference;
                cycle.LastAppliedProviderEventId = callback.EventId;
                cycle.LastAppliedProviderOccurredOn = callback.OccurredOn;
                cycle.ModifiedOn = DateTimeOffset.UtcNow;
                session.Store(cycle);
                resolvedCycle = cycle;
            }
        }
        var order = await FindOrderAsync(subscription, ct);
        if (order is not null && order.PaymentStatus != OrderPaymentStatus.Paid)
        {
            order.PaymentStatus = OrderPaymentStatus.Failed;
            order.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(order);
        }
        // Deliberately no retry, charge, or provider call: payment recovery is provider-owned.
        return (resolvedCycle, null);
    }

    private async Task<Result<bool, AeroError>> PersistManualReviewAsync(PaymentProviderAccount account, SubscriptionDocument subscription, VerifiedSubscriptionWebhook callback, string reason, CancellationToken ct, SubscriptionCycleDocument? cycle = null)
    {
        subscription.State = SubscriptionState.ManualReview;
        subscription.RequiresManualReview = true;
        subscription.ManualReviewReason = reason;
        subscription.ManualReviewRequestedOn = DateTimeOffset.UtcNow;
        subscription.LastAppliedProviderEventId = callback.EventId;
        subscription.LastAppliedProviderOccurredOn = callback.OccurredOn;
        subscription.ModifiedOn = DateTimeOffset.UtcNow;
        if (cycle is not null)
        {
            cycle.State = SubscriptionCycleState.ManualReview;
            cycle.RequiresManualReview = true;
            cycle.ManualReviewReason = reason;
            cycle.ManualReviewRequestedOn = DateTimeOffset.UtcNow;
            cycle.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(cycle);
        }
        else if (!string.IsNullOrWhiteSpace(callback.CycleReference) && TryNormalizeProviderPeriod(subscription, callback, out var safeCallback) && HasSafeCycleMetadata(subscription, safeCallback))
        {
            var existingCycle = await session.Query<SubscriptionCycleDocument>().FirstOrDefaultAsync(x => x.Provider == account.Provider && x.ProviderAccountKey == account.AccountKey && x.ProviderCycleReference == callback.CycleReference, ct);
            if (existingCycle is null)
            {
                var cycles = await session.Query<SubscriptionCycleDocument>().Where(x => x.SubscriptionId == subscription.Id).ToListAsync(ct);
                existingCycle = new SubscriptionCycleDocument
                {
                    Id = Snowflake.NewId(), TenantId = subscription.TenantId, SiteId = subscription.SiteId, ExternalMemberId = subscription.ExternalMemberId,
                    SubscriptionId = subscription.Id, CycleNumber = cycles.Count == 0 ? 1 : cycles.Max(x => x.CycleNumber) + 1,
                    Provider = account.Provider, ProviderAccountKey = account.AccountKey, ProviderCycleReference = callback.CycleReference,
                    ProviderPaymentReference = safeCallback.PaymentReference, Lines = subscription.Lines.Select(CloneLine).ToList(),
                    AmountSnapshot = safeCallback.Amount!.Value, Currency = "USD", PeriodStartsOn = safeCallback.PeriodStartsOn!.Value,
                    PeriodEndsOn = safeCallback.PeriodEndsOn!.Value, State = SubscriptionCycleState.ManualReview,
                    RequiresManualReview = true, ManualReviewReason = reason, ManualReviewRequestedOn = DateTimeOffset.UtcNow,
                    LastAppliedProviderEventId = callback.EventId, LastAppliedProviderOccurredOn = callback.OccurredOn, CreatedOn = DateTimeOffset.UtcNow
                };
            }
            else
            {
                existingCycle.State = SubscriptionCycleState.ManualReview;
                existingCycle.RequiresManualReview = true;
                existingCycle.ManualReviewReason = reason;
                existingCycle.ManualReviewRequestedOn = DateTimeOffset.UtcNow;
                existingCycle.ModifiedOn = DateTimeOffset.UtcNow;
            }
            cycle = existingCycle;
            session.Store(cycle);
        }
        var order = await FindOrderAsync(subscription, ct);
        if (order is not null)
        {
            order.PaymentStatus = OrderPaymentStatus.ManualReview;
            order.ModifiedOn = DateTimeOffset.UtcNow;
            session.Store(order);
        }
        session.Store(subscription);
        session.Store(Receipt(account, subscription, callback, cycle, SubscriptionWebhookReceiptState.ManualReview, reason));
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<bool, AeroError>(true);
    }

    private static void ApplyLifecycle(SubscriptionDocument subscription, VerifiedSubscriptionWebhook callback)
    {
        if (!string.IsNullOrWhiteSpace(callback.SubscriptionReference)) subscription.ProviderSubscriptionReference = callback.SubscriptionReference;
        if (!string.IsNullOrWhiteSpace(callback.CustomerReference)) subscription.ProviderCustomerReference = callback.CustomerReference;
        if (TryNormalizeProviderPeriod(subscription, callback, out var normalizedPeriod)
            && HasExactProviderPeriod(subscription, normalizedPeriod))
        {
            subscription.CurrentPeriodStartsOn = normalizedPeriod.PeriodStartsOn;
            subscription.CurrentPeriodEndsOn = normalizedPeriod.PeriodEndsOn;
        }
        var requested = callback.Kind switch
        {
            SubscriptionWebhookEventKind.SubscriptionActivated or SubscriptionWebhookEventKind.SubscriptionUpdated => SubscriptionState.Active,
            SubscriptionWebhookEventKind.SubscriptionReactivated when subscription.State is SubscriptionState.PastDue => SubscriptionState.Active,
            SubscriptionWebhookEventKind.SubscriptionCancelled => SubscriptionState.Cancelled,
            SubscriptionWebhookEventKind.SubscriptionExpired => SubscriptionState.Expired,
            SubscriptionWebhookEventKind.SubscriptionSuspended or SubscriptionWebhookEventKind.PaymentActionRequired => SubscriptionState.PastDue,
            _ => subscription.State
        };
        // Provider state is monotonic: generic activated/updated events cannot revive terminal
        // Commerce state. PayPal re-activated is accepted only from a nonterminal past-due state.
        if (subscription.State is not (SubscriptionState.Cancelled or SubscriptionState.Expired or SubscriptionState.ManualReview) || requested is SubscriptionState.Cancelled or SubscriptionState.Expired)
            subscription.State = requested;
    }

    private async Task<List<SubscriptionDocument>> FindSubscriptionsAsync(PaymentProviderAccount account, VerifiedSubscriptionWebhook callback, CancellationToken ct)
    {
        var matches = new Dictionary<long, SubscriptionDocument>();
        if (!string.IsNullOrWhiteSpace(callback.CheckoutReference))
            foreach (var document in await session.Query<SubscriptionDocument>().Where(x => x.TenantId == account.TenantId && x.SiteId == account.SiteId && x.Provider == account.Provider && x.ProviderAccountKey == account.AccountKey && x.ProviderCheckoutReference == callback.CheckoutReference).Take(2).ToListAsync(ct)) matches[document.Id] = document;
        if (!string.IsNullOrWhiteSpace(callback.SubscriptionReference))
            foreach (var document in await session.Query<SubscriptionDocument>().Where(x => x.TenantId == account.TenantId && x.SiteId == account.SiteId && x.Provider == account.Provider && x.ProviderAccountKey == account.AccountKey && x.ProviderSubscriptionReference == callback.SubscriptionReference).Take(2).ToListAsync(ct)) matches[document.Id] = document;
        return matches.Values.Take(2).ToList();
    }

    private static bool ReferencesAreConsistent(SubscriptionDocument subscription, VerifiedSubscriptionWebhook callback)
    {
        if (!string.IsNullOrWhiteSpace(callback.CheckoutReference) && subscription.ProviderCheckoutReference != callback.CheckoutReference) return false;
        if (!string.IsNullOrWhiteSpace(subscription.ProviderSubscriptionReference) && !string.IsNullOrWhiteSpace(callback.SubscriptionReference) && subscription.ProviderSubscriptionReference != callback.SubscriptionReference) return false;
        return callback.Kind == SubscriptionWebhookEventKind.CheckoutCompleted
            ? !string.IsNullOrWhiteSpace(callback.CheckoutReference) && !string.IsNullOrWhiteSpace(callback.SubscriptionReference)
            : !string.IsNullOrWhiteSpace(callback.SubscriptionReference);
    }

    private static bool HasOfferReferenceMismatch(SubscriptionDocument subscription, VerifiedSubscriptionWebhook callback)
    {
        if (callback.ProviderOfferReferences.Count == 0) return false;
        var expected = subscription.Lines.Select(x => x.ProviderOfferReference).Order(StringComparer.Ordinal).ToArray();
        var actual = callback.ProviderOfferReferences.Order(StringComparer.Ordinal).ToArray();
        return !expected.SequenceEqual(actual, StringComparer.Ordinal);
    }

    private static bool HasMatchingCycleSnapshot(SubscriptionDocument subscription, VerifiedSubscriptionWebhook callback, out string mismatch, bool requirePaymentReference = true)
    {
        mismatch = "Webhook amount, currency, interval, or payment reference mismatch.";
        if ((requirePaymentReference && string.IsNullOrWhiteSpace(callback.PaymentReference)) || !HasSafeCycleMetadata(subscription, callback)
            || callback.Amount != subscription.TotalAmount || callback.PeriodStartsOn is null || callback.PeriodEndsOn is null
            || callback.PeriodEndsOn <= callback.PeriodStartsOn) return false;
        return true;
    }

    private static bool HasSafeCycleMetadata(SubscriptionDocument subscription, VerifiedSubscriptionWebhook callback)
        => callback.Amount is > 0 && callback.Currency == "USD" && callback.PeriodStartsOn is not null && callback.PeriodEndsOn is not null
            && HasExactProviderPeriod(subscription, callback);

    private static bool HasExactProviderPeriod(SubscriptionDocument subscription, VerifiedSubscriptionWebhook callback)
        => callback.PeriodStartsOn is not null && callback.PeriodEndsOn is not null
            && callback.PeriodEndsOn > callback.PeriodStartsOn
            && callback.PeriodEndsOn.Value - callback.PeriodStartsOn.Value == TimeSpan.FromDays(subscription.IntervalDays);

    private static bool HasSafeIdentity(VerifiedSubscriptionWebhook callback)
        => callback.EventId.Length is > 0 and <= 256 && callback.OccurredOn != default
            && (callback.CheckoutReference?.Length ?? 0) <= 256 && (callback.SubscriptionReference?.Length ?? 0) <= 256
            && (callback.CycleReference?.Length ?? 0) <= 256 && (callback.PaymentReference?.Length ?? 0) <= 256;

    /// <summary>
    /// Normalizes a provider-supplied billing boundary without carrying a stale period forward or
    /// inventing dates from webhook delivery time. An end-only next-billing fact defines the period.
    /// </summary>
    private static bool TryNormalizeProviderPeriod(SubscriptionDocument subscription, VerifiedSubscriptionWebhook callback, out VerifiedSubscriptionWebhook normalized)
    {
        normalized = callback;
        if (subscription.IntervalDays is < 1 or > 365) return false;
        if (callback.PeriodStartsOn.HasValue && callback.PeriodEndsOn.HasValue) return true;
        if (callback.PeriodEndsOn.HasValue)
        {
            normalized = callback with { PeriodStartsOn = callback.PeriodEndsOn.Value.AddDays(-subscription.IntervalDays) };
            return true;
        }
        if (callback.PeriodStartsOn.HasValue)
        {
            normalized = callback with { PeriodEndsOn = callback.PeriodStartsOn.Value.AddDays(subscription.IntervalDays) };
            return true;
        }
        return false;
    }

    private Task<OrderEntity?> FindOrderAsync(SubscriptionDocument subscription, CancellationToken ct)
        => session.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == subscription.OrderId && x.TenantId == subscription.TenantId && x.SiteId == subscription.SiteId && x.ExternalMemberId == subscription.ExternalMemberId, ct);

    private static SubscriptionWebhookReceiptDocument Receipt(PaymentProviderAccount account, SubscriptionDocument subscription, VerifiedSubscriptionWebhook callback, SubscriptionCycleDocument? cycle, SubscriptionWebhookReceiptState state, string? reason = null) => new()
    {
        Id = Snowflake.NewId(), TenantId = subscription.TenantId, SiteId = subscription.SiteId, ExternalMemberId = subscription.ExternalMemberId,
        SubscriptionId = subscription.Id, SubscriptionCycleId = cycle?.Id, Provider = account.Provider, ProviderAccountKey = account.AccountKey,
        ProviderEventId = callback.EventId, ProviderOccurredOn = callback.OccurredOn, ProviderSubscriptionReference = callback.SubscriptionReference,
        ProviderCycleReference = callback.CycleReference, ProviderPaymentReference = callback.PaymentReference,
        ReceivedOn = DateTimeOffset.UtcNow, State = state, ManualReviewReason = reason
    };

    private static SubscriptionLineSnapshot CloneLine(SubscriptionLineSnapshot line) => new()
    {
        ProductId = line.ProductId, ListingId = line.ListingId, ProductName = line.ProductName, ListingName = line.ListingName,
        Sku = line.Sku, Quantity = line.Quantity, UnitAmount = line.UnitAmount, ProviderOfferReference = line.ProviderOfferReference
    };

    private static Result<bool, AeroError> Fail(string message) => Prelude.Fail<bool, AeroError>(AeroError.CreateError(message));
}
