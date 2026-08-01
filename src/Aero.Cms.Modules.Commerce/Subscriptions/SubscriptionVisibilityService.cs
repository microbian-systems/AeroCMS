using AeroDB.Sable;

namespace Aero.Cms.Modules.Commerce.Subscriptions;

/// <summary>
/// Read-only, redacted subscription receipts for the owning storefront member and selected-site managers.
/// Provider references, credentials, webhook receipts, and provider continuation URLs intentionally never
/// cross this boundary.
/// </summary>
public interface ISubscriptionVisibilityService
{
    Task<Result<IReadOnlyList<MemberSubscriptionSummary>, AeroError>> ListForMemberAsync(long tenantId, long siteId, long memberId, CancellationToken ct = default);
    Task<Result<MemberSubscriptionReceipt?, AeroError>> GetForMemberOrderAsync(long tenantId, long siteId, long memberId, long orderId, CancellationToken ct = default);
    Task<Result<ManagerSubscriptionPage, AeroError>> ListForManagerAsync(long tenantId, long siteId, int skip, int take, CancellationToken ct = default);
    Task<Result<ManagerSubscriptionReceipt?, AeroError>> GetForManagerAsync(long tenantId, long siteId, long subscriptionId, CancellationToken ct = default);
}

public sealed record MemberSubscriptionSummary(
    long SubscriptionId,
    long OrderId,
    string Provider,
    SubscriptionState State,
    int IntervalDays,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedOn,
    DateTimeOffset? CurrentPeriodEndsOn,
    bool CancelAtPeriodEnd,
    bool RequiresManualReview);

public sealed record SubscriptionCycleReceipt(
    int CycleNumber,
    decimal Amount,
    string Currency,
    DateTimeOffset PeriodStartsOn,
    DateTimeOffset PeriodEndsOn,
    SubscriptionCycleState State,
    bool RequiresManualReview);

/// <summary>Commercial line snapshot with every provider offer/reference field deliberately removed.</summary>
public sealed record SubscriptionLineReceipt(string ProductName, string ListingName, string Sku, int Quantity, decimal UnitAmount, decimal TotalAmount);

public sealed record MemberSubscriptionReceipt(MemberSubscriptionSummary Subscription, IReadOnlyList<SubscriptionLineReceipt> Lines, IReadOnlyList<SubscriptionCycleReceipt> Cycles);

public sealed record ManagerSubscriptionSummary(
    long SubscriptionId,
    long OrderId,
    string Provider,
    SubscriptionState State,
    int IntervalDays,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedOn,
    DateTimeOffset? CurrentPeriodEndsOn,
    bool CancelAtPeriodEnd,
    bool RequiresManualReview);

public sealed record ManagerSubscriptionReceipt(ManagerSubscriptionSummary Subscription, IReadOnlyList<SubscriptionLineReceipt> Lines, IReadOnlyList<SubscriptionCycleReceipt> Cycles);
public sealed record ManagerSubscriptionPage(IReadOnlyList<ManagerSubscriptionSummary> Items, long TotalCount);

public sealed class SubscriptionVisibilityService(IDocumentSession session) : ISubscriptionVisibilityService
{
    public async Task<Result<IReadOnlyList<MemberSubscriptionSummary>, AeroError>> ListForMemberAsync(long tenantId, long siteId, long memberId, CancellationToken ct = default)
    {
        if (!IsMemberScope(tenantId, siteId, memberId)) return Fail<IReadOnlyList<MemberSubscriptionSummary>>("Subscription history is unavailable.");
        try
        {
            var values = await session.Query<SubscriptionDocument>()
                .Where(x => x.TenantId == tenantId && x.SiteId == siteId && x.ExternalMemberId == memberId)
                .ToListAsync(ct);
            return Prelude.Ok<IReadOnlyList<MemberSubscriptionSummary>, AeroError>(values
                .OrderByDescending(x => x.CreatedOn)
                .Select(MemberSummary)
                .ToList());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return Fail<IReadOnlyList<MemberSubscriptionSummary>>("Subscription history could not be loaded."); }
    }

    public async Task<Result<MemberSubscriptionReceipt?, AeroError>> GetForMemberOrderAsync(long tenantId, long siteId, long memberId, long orderId, CancellationToken ct = default)
    {
        if (!IsMemberScope(tenantId, siteId, memberId) || orderId <= 0) return Fail<MemberSubscriptionReceipt?>("Subscription receipt is unavailable.");
        try
        {
            var subscription = await session.Query<SubscriptionDocument>().FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.SiteId == siteId && x.ExternalMemberId == memberId && x.OrderId == orderId, ct);
            return subscription is null
                ? Prelude.Ok<MemberSubscriptionReceipt?, AeroError>(null)
                : Prelude.Ok<MemberSubscriptionReceipt?, AeroError>(new(MemberSummary(subscription), Lines(subscription), await CyclesAsync(subscription, ct)));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return Fail<MemberSubscriptionReceipt?>("Subscription receipt could not be loaded."); }
    }

    public async Task<Result<ManagerSubscriptionPage, AeroError>> ListForManagerAsync(long tenantId, long siteId, int skip, int take, CancellationToken ct = default)
    {
        if (tenantId <= 0 || siteId <= 0) return Fail<ManagerSubscriptionPage>("Subscription status is unavailable.");
        try
        {
            var values = (ISableQueryable<SubscriptionDocument>)session.Query<SubscriptionDocument>()
                .Where(x => x.TenantId == tenantId && x.SiteId == siteId)
                ;
            var total = await values.CountAsync(ct);
            var page = await values.OrderByDescending(x => x.CreatedOn)
                .Skip(Math.Max(0, skip))
                .Take(Math.Clamp(take, 1, 100))
                .ToListAsync(ct);
            return Prelude.Ok<ManagerSubscriptionPage, AeroError>(new(
                page.Select(ManagerSummary).ToList(), total));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return Fail<ManagerSubscriptionPage>("Subscription status could not be loaded."); }
    }

    public async Task<Result<ManagerSubscriptionReceipt?, AeroError>> GetForManagerAsync(long tenantId, long siteId, long subscriptionId, CancellationToken ct = default)
    {
        if (tenantId <= 0 || siteId <= 0 || subscriptionId <= 0) return Fail<ManagerSubscriptionReceipt?>("Subscription receipt is unavailable.");
        try
        {
            var subscription = await session.Query<SubscriptionDocument>().FirstOrDefaultAsync(
                x => x.Id == subscriptionId && x.TenantId == tenantId && x.SiteId == siteId, ct);
            return subscription is null
                ? Prelude.Ok<ManagerSubscriptionReceipt?, AeroError>(null)
                : Prelude.Ok<ManagerSubscriptionReceipt?, AeroError>(new(ManagerSummary(subscription), Lines(subscription), await CyclesAsync(subscription, ct)));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return Fail<ManagerSubscriptionReceipt?>("Subscription receipt could not be loaded."); }
    }

    private async Task<IReadOnlyList<SubscriptionCycleReceipt>> CyclesAsync(SubscriptionDocument subscription, CancellationToken ct)
    {
        var cycles = await session.Query<SubscriptionCycleDocument>()
            .Where(x => x.TenantId == subscription.TenantId && x.SiteId == subscription.SiteId && x.ExternalMemberId == subscription.ExternalMemberId && x.SubscriptionId == subscription.Id)
            .ToListAsync(ct);
        return cycles.OrderByDescending(x => x.CycleNumber).Select(Cycle).ToList();
    }

    private static MemberSubscriptionSummary MemberSummary(SubscriptionDocument value) => new(
        value.Id, value.OrderId, ProviderLabel(value.Provider), value.State, value.IntervalDays, value.TotalAmount, value.Currency,
        value.CreatedOn, value.CurrentPeriodEndsOn, value.CancelAtPeriodEnd, value.RequiresManualReview);

    private static ManagerSubscriptionSummary ManagerSummary(SubscriptionDocument value) => new(
        value.Id, value.OrderId, ProviderLabel(value.Provider), value.State, value.IntervalDays, value.TotalAmount,
        value.Currency, value.CreatedOn, value.CurrentPeriodEndsOn, value.CancelAtPeriodEnd, value.RequiresManualReview);

    private static SubscriptionCycleReceipt Cycle(SubscriptionCycleDocument value) => new(
        value.CycleNumber, value.AmountSnapshot, value.Currency, value.PeriodStartsOn, value.PeriodEndsOn,
        value.State, value.RequiresManualReview);

    private static IReadOnlyList<SubscriptionLineReceipt> Lines(SubscriptionDocument value) => value.Lines
        .Select(line => new SubscriptionLineReceipt(line.ProductName, line.ListingName, line.Sku, line.Quantity, line.UnitAmount, line.TotalAmount))
        .ToList();

    private static string ProviderLabel(string provider) => provider.Equals("stripe", StringComparison.OrdinalIgnoreCase)
        ? "Stripe" : provider.Equals("paypal", StringComparison.OrdinalIgnoreCase) ? "PayPal" : "Provider";
    private static bool IsMemberScope(long tenantId, long siteId, long memberId) => tenantId > 0 && siteId > 0 && memberId > 0;
    private static Result<T, AeroError> Fail<T>(string message) => Prelude.Fail<T, AeroError>(AeroError.CreateError(message));
}
