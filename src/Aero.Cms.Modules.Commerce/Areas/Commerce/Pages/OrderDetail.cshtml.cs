using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Aero.Cms.Modules.Commerce.Payments;
using Aero.Cms.Modules.Commerce.Subscriptions;
using Aero.Core.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

[Authorize(Policy = ExternalMemberAuthenticationDefaults.Policy)]
[Authorize(Policy = ExternalMemberAuthenticationDefaults.SitePolicy)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class OrderDetailModel(
    IOrderService orders,
    IPaymentApplicationService payments,
    ISubscriptionCheckoutService subscriptions,
    ISubscriptionVisibilityService subscriptionVisibility,
    ICurrentPrincipal principal,
    ISiteContext site) : PageModel
{
    public OrderEntity? Order { get; set; }
    public PaymentAttemptDocument? PaymentAttempt { get; private set; }
    public MemberSubscriptionReceipt? SubscriptionReceipt { get; private set; }
    public async Task<IActionResult> OnGetAsync(long id)
    {
        var result = await orders.GetForMemberAsync(site.TenantId, site.SiteId, Member(), id);
        if (result is not Result<OrderEntity?, AeroError>.Ok(var order) || order is null)
            return NotFound();

        Order = order;
        if (order.BillingKind == OrderBillingKind.Recurring)
        {
            var subscription = await subscriptionVisibility.GetForMemberOrderAsync(site.TenantId, site.SiteId, Member(), id);
            if (subscription is Result<MemberSubscriptionReceipt?, AeroError>.Ok(var receipt)) SubscriptionReceipt = receipt;
        }
        else
        {
            var payment = await payments.GetForMemberAsync(site.TenantId, site.SiteId, Member(), id);
            if (payment is Result<PaymentAttemptDocument?, AeroError>.Ok(var attempt)) PaymentAttempt = attempt;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostResumeSubscriptionAsync(long id, CancellationToken ct)
    {
        var lookup = await subscriptions.GetForMemberAsync(site.TenantId, site.SiteId, Member(), id, ct);
        if (lookup is not Result<SubscriptionDocument?, AeroError>.Ok { Value: { } subscription }) return NotFound();
        if (!TryBuildSubscriptionUrls(id, out var successUrl, out var cancelUrl))
        {
            TempData["OrderNotice"] = "Secure subscription checkout is unavailable for this request.";
            return RedirectToPage(new { id });
        }
        var continuation = await subscriptions.InitiateAsync(site.TenantId, site.SiteId, Member(),
            new SubscriptionCheckoutRequest(id, subscription.Provider, subscription.ProviderOperationKey, successUrl, cancelUrl), ct);
        if (continuation is Result<SubscriptionCheckoutInitiation, AeroError>.Ok(var initiation) && IsHttpsUrl(initiation.ApprovalUrl))
        {
            return Redirect(initiation.ApprovalUrl);
        }
        else TempData["OrderNotice"] = "Subscription checkout could not be resumed. Do not create a second order.";
        return RedirectToPage(new { id });
    }

    private bool TryBuildSubscriptionUrls(long orderId, out Uri successUrl, out Uri cancelUrl)
    {
        successUrl = cancelUrl = null!;
        if (!Request.IsHttps || !Request.Host.HasValue) return false;
        if (!Uri.TryCreate($"https://{Request.Host}", UriKind.Absolute, out var origin)) return false;
        var detailPath = Request.PathBase.Add($"/shop/orders/{orderId}").ToString();
        return Uri.TryCreate(origin, $"{detailPath}?subscription=success", out successUrl)
            && Uri.TryCreate(origin, $"{detailPath}?subscription=cancel", out cancelUrl)
            && successUrl.Scheme == Uri.UriSchemeHttps && cancelUrl.Scheme == Uri.UriSchemeHttps;
    }
    private static bool IsHttpsUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    public async Task<IActionResult> OnPostResumePaymentAsync(long id, CancellationToken ct)
    {
        var lookup = await payments.GetForMemberAsync(site.TenantId, site.SiteId, Member(), id, ct);
        if (lookup is not Result<PaymentAttemptDocument?, AeroError>.Ok { Value: { } attempt })
            return NotFound();

        var continuation = await payments.InitiateAsync(
            site.TenantId,
            site.SiteId,
            Member(),
            new InitiatePaymentRequest(id, attempt.Provider, attempt.RequestIdempotencyKey),
            ct);
        if (continuation is Result<PaymentInitiation, AeroError>.Ok(var initiation))
        {
            TempData["OrderNotice"] = initiation.Status == PaymentAttemptStatus.Succeeded
                ? "Payment is confirmed."
                : "Payment still needs customer action. Do not start a second order.";
            if (Uri.TryCreate(initiation.ApprovalUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                return Redirect(initiation.ApprovalUrl);
        }
        else
        {
            TempData["OrderNotice"] = "Payment could not be resumed. Review the current order status before trying again.";
        }

        return RedirectToPage(new { id });
    }
    private long Member() => principal.PrincipalId ?? throw new InvalidOperationException("External member is required.");
}
