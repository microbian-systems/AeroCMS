using System.Globalization;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Events;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Aero.Cms.Modules.Commerce.Payments;
using Aero.Cms.Modules.Commerce.Subscriptions;
using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Core.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Wolverine;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Commerce.Areas.Commerce.Pages;

[Authorize(Policy = ExternalMemberAuthenticationDefaults.Policy)]
[Authorize(Policy = ExternalMemberAuthenticationDefaults.SitePolicy)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class CheckoutModel(
    IOrderService orders,
    IPaymentApplicationService payments,
    ISubscriptionCheckoutService subscriptions,
    IBasketService baskets,
    IPaymentProviderRegistry paymentProviders,
    ICurrentPrincipal principal,
    ISiteContext site,
    IMessageBus bus,
    ILogger<CheckoutModel> log) : PageModel
{
    [BindProperty] public string? Street { get; set; }
    [BindProperty] public string? City { get; set; }
    [BindProperty] public string? State { get; set; }
    [BindProperty] public string? ZipCode { get; set; }
    [BindProperty] public string? Country { get; set; }
    [BindProperty] public string? PaymentProvider { get; set; }
    public IReadOnlyList<string> AvailablePaymentProviders { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await LoadAvailablePaymentProvidersAsync();
    }

    public async Task<IActionResult> OnPostPlaceOrderAsync()
    {
        await LoadAvailablePaymentProvidersAsync();
        ValidateAddress();
        if (string.IsNullOrWhiteSpace(PaymentProvider) || !AvailablePaymentProviders.Contains(PaymentProvider, StringComparer.Ordinal))
            ModelState.AddModelError(nameof(PaymentProvider), "Choose an available payment method.");
        if (!ModelState.IsValid)
            return Page();

        var address = new Address { Street = Street ?? "", City = City ?? "", State = State, PostalCode = ZipCode ?? "", Country = Country ?? "" };
        var result = await orders.CheckoutAsync(site.TenantId, site.SiteId, Member(), address, null, CultureInfo.CurrentUICulture.Name);
        if (result is not Result<OrderEntity, AeroError>.Ok(var order)) { ModelState.AddModelError("", "Your order could not be placed."); return Page(); }
        try { await bus.PublishAsync(new OrderStarted(order.Id, order.TenantId, order.SiteId, order.ExternalMemberId)); await bus.PublishAsync(new OrderStatusChangedToSubmitted(order.Id, order.TenantId, order.SiteId, order.ExternalMemberId, order.TotalAmount)); }
        catch (Exception ex) { log.LogError(ex, "Order {OrderId} committed but follow-up publication failed", order.Id); }
        if (order.BillingKind == OrderBillingKind.Recurring)
        {
            if (!TryBuildSubscriptionUrls(order.Id, out var successUrl, out var cancelUrl))
            {
                TempData["OrderNotice"] = "Your subscription order was received, but secure checkout could not be started. Open the receipt to resume it.";
                return RedirectToPage("OrderDetail", new { id = order.Id });
            }
            var subscription = await subscriptions.InitiateAsync(site.TenantId, site.SiteId, Member(),
                new SubscriptionCheckoutRequest(order.Id, PaymentProvider!, $"commerce-subscription-order-{order.Id}", successUrl, cancelUrl));
            if (subscription is Result<SubscriptionCheckoutInitiation, AeroError>.Ok(var initiation) && IsHttpsUrl(initiation.ApprovalUrl))
            {
                TempData["OrderNotice"] = "Your subscription order was received. Complete provider approval to start it.";
                return Redirect(initiation.ApprovalUrl);
            }
            else TempData["OrderNotice"] = "Your subscription order was received, but checkout could not be started. Open the receipt to resume it.";
        }
        else
        {
            PaymentReturnUrls? paymentReturnUrls = null;
            if (PaymentProvider == "stripe" && !TryBuildPaymentUrls(order.Id, out paymentReturnUrls))
            {
                TempData["OrderNotice"] = "Your order was received, but secure Stripe checkout could not be started. Open the receipt to review its status before trying again.";
                return RedirectToPage("OrderDetail", new { id = order.Id });
            }
            var payment = await payments.InitiateAsync(
                site.TenantId,
                site.SiteId,
                Member(),
                new InitiatePaymentRequest(order.Id, PaymentProvider!, $"checkout-{order.Id}-{Guid.NewGuid():N}"), returnUrls: paymentReturnUrls);
            if (payment is Result<PaymentInitiation, AeroError>.Ok(var initiation))
            {
                TempData["OrderNotice"] = initiation.Status == PaymentAttemptStatus.Succeeded
                    ? "Your order was received and payment is confirmed."
                    : "Your order was received. Complete payment to finish your purchase.";
                if (IsHttpsUrl(initiation.ApprovalUrl)) return Redirect(initiation.ApprovalUrl);
            }
            else TempData["OrderNotice"] = "Your order was received, but payment could not be started. Open the receipt to review its status before trying again.";
        }

        return RedirectToPage("OrderDetail", new { id = order.Id });
    }

    private async Task LoadAvailablePaymentProvidersAsync()
    {
        var basket = await baskets.GetAsync(site.TenantId, site.SiteId, Member());
        var items = basket is Result<BasketDocument?, AeroError>.Ok(var loaded) ? loaded?.Items : null;
        if (items is null || items.Count == 0) { AvailablePaymentProviders = []; return; }
        var recurring = items.All(item => item.BillingKind == BasketBillingKind.Recurring);
        AvailablePaymentProviders = new[] { "stripe", "paypal" }
            .Where(provider => paymentProviders.GetAccount(provider, site.TenantId, site.SiteId) is Result<PaymentProviderAccount, AeroError>.Ok)
            .Where(provider => !recurring || provider == "stripe" && items.All(item => !string.IsNullOrWhiteSpace(item.SubscriptionOffer?.StripePriceId))
                || provider == "paypal" && items.Count == 1 && !string.IsNullOrWhiteSpace(items[0].SubscriptionOffer?.PayPalPlanId))
            .ToArray();
    }

    private void ValidateAddress()
    {
        Require(Street, nameof(Street), "Enter a street address.");
        Require(City, nameof(City), "Enter a city.");
        Require(ZipCode, nameof(ZipCode), "Enter a postal code.");
        Require(Country, nameof(Country), "Enter a country.");
    }

    private void Require(string? value, string field, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            ModelState.AddModelError(field, message);
    }

    private static bool IsHttpsUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

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

    private bool TryBuildPaymentUrls(long orderId, out PaymentReturnUrls? returnUrls)
    {
        returnUrls = null;
        if (!Request.IsHttps || !Request.Host.HasValue || !Uri.TryCreate($"https://{Request.Host}", UriKind.Absolute, out var origin)) return false;
        var path = Request.PathBase.Add($"/shop/orders/{orderId}").ToString();
        if (!Uri.TryCreate(origin, $"{path}?payment=success", out var success) || !Uri.TryCreate(origin, $"{path}?payment=cancel", out var cancel)) return false;
        returnUrls = new PaymentReturnUrls(success, cancel);
        return returnUrls.IsHttps;
    }

    private long Member() => principal.PrincipalId ?? throw new InvalidOperationException("External member is required.");
}
