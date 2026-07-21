using System.Globalization;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Events;
using Aero.Cms.Modules.Commerce.Orders.Services;
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
public class CheckoutModel(IOrderService orders, ICurrentPrincipal principal, ISiteContext site, IMessageBus bus, ILogger<CheckoutModel> log) : PageModel
{
    [BindProperty] public string? Street { get; set; }
    [BindProperty] public string? City { get; set; }
    [BindProperty] public string? State { get; set; }
    [BindProperty] public string? ZipCode { get; set; }
    [BindProperty] public string? Country { get; set; }
    public void OnGet() { }
    public async Task<IActionResult> OnPostPlaceOrderAsync()
    {
        var address = new Address { Street = Street ?? "", City = City ?? "", State = State, PostalCode = ZipCode ?? "", Country = Country ?? "" };
        var result = await orders.CheckoutAsync(site.TenantId, site.SiteId, Member(), address, null, CultureInfo.CurrentUICulture.Name);
        if (result is not Result<OrderEntity, AeroError>.Ok(var order)) { ModelState.AddModelError("", "Your order could not be placed."); return Page(); }
        try { await bus.PublishAsync(new OrderStarted(order.Id, order.TenantId, order.SiteId, order.ExternalMemberId)); await bus.PublishAsync(new OrderStatusChangedToSubmitted(order.Id, order.TenantId, order.SiteId, order.ExternalMemberId, order.TotalAmount)); }
        catch (Exception ex) { log.LogError(ex, "Order {OrderId} committed but follow-up publication failed", order.Id); }
        return Redirect("/shop/orders");
    }
    private long Member() => principal.PrincipalId ?? throw new InvalidOperationException("External member is required.");
}
