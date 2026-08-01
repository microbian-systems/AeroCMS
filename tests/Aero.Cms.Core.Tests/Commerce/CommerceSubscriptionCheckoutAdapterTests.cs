using System.Net;
using System.Text;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Payments;
using Aero.Cms.Modules.Commerce.Subscriptions;
using Aero.Core;
using Aero.Core.Railway;
using Shouldly;
using Microsoft.Extensions.Options;
using AeroDB.Sable;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceSubscriptionCheckoutAdapterTests
{
    [Test]
    public async Task Stripe_hosted_subscription_checkout_uses_prices_quantities_https_urls_and_stable_idempotency_key()
    {
        var handler = new RecordingHandler(_ => Json("""{"id":"cs_123","url":"https://checkout.stripe.test/cs_123"}"""));
        var adapter = new StripeSubscriptionCheckoutProviderAdapter(new SingleClientFactory(handler));

        var result = await adapter.InitiateAsync(StripeAccount(), Request("commerce-subscription-order-44", [Line("price_monthly", null, 2)]));

        result.Disposition.ShouldBe(SubscriptionCheckoutDisposition.Succeeded);
        handler.Request!.Method.ShouldBe(HttpMethod.Post);
        handler.Request.RequestUri!.AbsolutePath.ShouldBe("/v1/checkout/sessions");
        handler.Request.Headers.GetValues("Idempotency-Key").Single().ShouldBe("commerce-subscription-order-44");
        handler.Body.ShouldContain("mode=subscription");
        handler.Body.ShouldContain("line_items%5B0%5D%5Bprice%5D=price_monthly");
        handler.Body.ShouldContain("line_items%5B0%5D%5Bquantity%5D=2");
        handler.Body.ShouldContain("success_url=https%3A%2F%2Fstore.example.test%2Fsuccess");
        handler.Body.ShouldNotContain("payment_method_types");
    }

    [Test]
    public async Task Paypal_hosted_subscription_checkout_uses_one_plan_quantity_https_approval_and_request_id()
    {
        var handler = new RecordingHandler(call => call == 1
            ? Json("""{"access_token":"token"}""")
            : Json("""{"id":"I-123","links":[{"rel":"approve","href":"https://paypal.test/approve"}]}"""));
        var adapter = new PayPalSubscriptionCheckoutProviderAdapter(new SingleClientFactory(handler));

        var result = await adapter.InitiateAsync(PayPalAccount(), Request("commerce-subscription-order-44", [Line(null, "P-MONTHLY-30", 3)]));

        result.Disposition.ShouldBe(SubscriptionCheckoutDisposition.Succeeded);
        handler.Request!.RequestUri!.AbsolutePath.ShouldBe("/v1/billing/subscriptions");
        handler.Request.Headers.GetValues("PayPal-Request-Id").Single().ShouldBe("commerce-sub-44");
        handler.Request.Headers.GetValues("Prefer").Single().ShouldBe("return=representation");
        handler.Body.ShouldContain("\"plan_id\":\"P-MONTHLY-30\"");
        handler.Body.ShouldContain("\"quantity\":\"3\"");
        handler.Body.ShouldContain("\"user_action\":\"SUBSCRIBE_NOW\"");
    }

    [Test]
    public async Task Paypal_subscription_request_id_is_stable_and_within_the_38_byte_limit_for_a_maximum_snowflake()
    {
        var handler = new RecordingHandler(call => call == 1
            ? Json("""{"access_token":"token"}""")
            : Json("""{"id":"I-max","links":[{"rel":"approve","href":"https://paypal.test/approve"}]}"""));
        var adapter = new PayPalSubscriptionCheckoutProviderAdapter(new SingleClientFactory(handler));

        var result = await adapter.InitiateAsync(
            PayPalAccount(),
            Request("commerce-subscription-order-9223372036854775807", [Line(null, "P-MONTHLY-30", 1)], long.MaxValue));

        result.Disposition.ShouldBe(SubscriptionCheckoutDisposition.Succeeded);
        var requestId = handler.Request!.Headers.GetValues("PayPal-Request-Id").Single();
        requestId.ShouldBe("commerce-sub-9223372036854775807");
        Encoding.UTF8.GetByteCount(requestId).ShouldBeLessThanOrEqualTo(38);
    }

    [Test]
    public void Recurring_order_validator_rejects_mixed_lines_and_missing_provider_bindings()
    {
        var order = new OrderEntity
        {
            ExternalMemberId = 7, BillingKind = OrderBillingKind.Recurring, BillingIntervalDays = 30,
            Items = [new OrderItem { ProductId = 1, ProductName = "Recurring", Quantity = 1, UnitPrice = 10m, BillingKind = OrderBillingKind.Recurring, FulfillmentMode = Aero.Cms.Modules.Commerce.Catalog.Models.ProductFulfillmentMode.NonInventoryRecurring, BillingIntervalDays = 30 }]
        };
        var validator = new Aero.Cms.Modules.Commerce.Orders.Validation.CreateOrderValidator();

        validator.Validate(order).IsValid.ShouldBeFalse();
    }

    [Test]
    public async Task Subscription_service_enforces_scope_https_and_durable_provider_binding_with_replay()
    {
        await using var harness = await HarnessAsync();
        harness.Session.Store(RecurringOrder(44));
        await harness.Session.SaveChangesAsync();
        var stripe = new FakeSubscriptionAdapter("stripe");
        var paypal = new FakeSubscriptionAdapter("paypal");
        var registry = new PaymentProviderRegistry([], Options.Create(new CommercePaymentOptions { Accounts = [AccountOption("stripe"), AccountOption("paypal")] }));
        var service = new SubscriptionCheckoutService(harness.Session, registry, [stripe, paypal]);
        var request = new SubscriptionCheckoutRequest(44, "stripe", "commerce-subscription-order-44", new Uri("https://store.test/success"), new Uri("https://store.test/cancel"));

        (await service.InitiateAsync(1, 10, 8, request)).IsSuccess.ShouldBeFalse();
        (await service.InitiateAsync(1, 10, 7, request with { SuccessUrl = new Uri("http://store.test/success") })).IsSuccess.ShouldBeFalse();
        (await service.InitiateAsync(1, 10, 7, request)).IsSuccess.ShouldBeTrue();
        (await service.InitiateAsync(1, 10, 7, request)).IsSuccess.ShouldBeTrue();
        (await service.InitiateAsync(1, 10, 7, request with { Provider = "paypal" })).IsSuccess.ShouldBeFalse();
        stripe.Creates.ShouldBe(1);
        stripe.Retrieves.ShouldBe(1);
        paypal.Creates.ShouldBe(0);

        await using var verify = await harness.OpenSessionAsync();
        var subscription = (await verify.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.OrderId == 44))!;
        subscription.ExternalMemberId.ShouldBe(7);
        subscription.Provider.ShouldBe("stripe");
        subscription.ProviderOperationKey.ShouldBe("commerce-subscription-order-44");
    }

    [Test]
    public async Task Order_service_only_reserves_inventory_and_excludes_recurring_from_cancel_and_grace_expiry()
    {
        await using var inventoryHarness = await HarnessAsync();
        inventoryHarness.Session.Store(new ProductDocument { Id = 1, TenantId = 1, Name = "Stock", Sku = "STOCK", StockQuantity = 3, IsActive = true, FulfillmentMode = ProductFulfillmentMode.Inventory });
        inventoryHarness.Session.Store(Listing(1, 11));
        inventoryHarness.Session.Store(new BasketDocument { Id = 101, TenantId = 1, SiteId = 10, ExternalMemberId = 7, Items = [new BasketItem { ListingId = 11, ProductId = 1, ProductName = "Stock", Sku = "STOCK", Quantity = 2, UnitPrice = 10m, BillingKind = BasketBillingKind.OneTime }] });
        await inventoryHarness.Session.SaveChangesAsync();
        var inventoryOrders = new OrderService(inventoryHarness.Session);
        var inventoryOrder = (await inventoryOrders.CheckoutAsync(1, 10, 7, Address(), null, "en-US")).ShouldBeOfType<Result<OrderEntity, AeroError>.Ok>().Value;
        (await inventoryHarness.Session.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == 1))!.StockQuantity.ShouldBe(1);
        (await inventoryOrders.CancelAsync(1, 10, 7, inventoryOrder.Id)).IsSuccess.ShouldBeTrue();
        (await inventoryHarness.Session.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == 1))!.StockQuantity.ShouldBe(3);

        await using var recurringHarness = await HarnessAsync();
        recurringHarness.Session.Store(new ProductDocument { Id = 2, TenantId = 1, Name = "Recurring", Sku = "RECUR", StockQuantity = 0, IsActive = true, FulfillmentMode = ProductFulfillmentMode.NonInventoryRecurring });
        recurringHarness.Session.Store(Listing(2, 12, new SubscriptionOffer { IntervalDays = 30, StripePriceId = "price_monthly" }));
        recurringHarness.Session.Store(new BasketDocument { Id = 102, TenantId = 1, SiteId = 10, ExternalMemberId = 7, Items = [new BasketItem { ListingId = 12, ProductId = 2, ProductName = "Recurring", Sku = "RECUR", Quantity = 1, UnitPrice = 10m, BillingKind = BasketBillingKind.Recurring, BillingIntervalDays = 30, SubscriptionOffer = new BasketSubscriptionOfferSnapshot { IntervalDays = 30, StripePriceId = "price_monthly" } }] });
        await recurringHarness.Session.SaveChangesAsync();
        var recurringOrders = new OrderService(recurringHarness.Session);
        var recurringOrder = (await recurringOrders.CheckoutAsync(1, 10, 7, Address(), null, "en-US")).ShouldBeOfType<Result<OrderEntity, AeroError>.Ok>().Value;
        (await recurringHarness.Session.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == 2))!.StockQuantity.ShouldBe(0);
        (await recurringOrders.CancelAsync(1, 10, 7, recurringOrder.Id)).IsSuccess.ShouldBeFalse();
        (await recurringOrders.GetExpiredSubmittedAsync(DateTimeOffset.UtcNow.AddYears(1))).ShouldBeOfType<Result<IReadOnlyList<OrderEntity>, AeroError>.Ok>().Value.ShouldBeEmpty();
    }

    private static SubscriptionProviderCheckout Request(string operationKey, IReadOnlyList<OrderItem> lines, long orderId = 44) => new(operationKey, orderId, lines, new Uri("https://store.example.test/success"), new Uri("https://store.example.test/cancel"));
    private static OrderItem Line(string? stripe, string? paypal, int quantity) => new() { ProductId = 1, ListingId = 2, ProductName = "Subscription", Sku = "SUB", Quantity = quantity, UnitPrice = 10m, Currency = "USD", BillingKind = OrderBillingKind.Recurring, FulfillmentMode = Aero.Cms.Modules.Commerce.Catalog.Models.ProductFulfillmentMode.NonInventoryRecurring, BillingIntervalDays = 30, StripePriceId = stripe, PayPalPlanId = paypal };
    private static PaymentProviderAccount StripeAccount() => new("stripe", "stripe-store", 1, 10, "secret", null, null, null, null, "https://stripe.test/", []);
    private static PaymentProviderAccount PayPalAccount() => new("paypal", "paypal-store", 1, 10, null, null, "client", "secret", null, "https://paypal.test/", []);
    private static PaymentProviderAccountOptions AccountOption(string provider) => new() { Enabled = true, Provider = provider, AccountKey = $"{provider}-store", TenantId = 1, SiteId = 10, BaseUrl = $"https://{provider}.test/", SecretKey = "secret", WebhookSecret = "webhook", ClientId = "client", ClientSecret = "secret", WebhookId = "webhook" };
    private static OrderEntity RecurringOrder(long id) => new() { Id = id, TenantId = 1, SiteId = 10, ExternalMemberId = 7, Status = OrderStatus.Submitted, PaymentStatus = OrderPaymentStatus.Unpaid, BillingKind = OrderBillingKind.Recurring, BillingIntervalDays = 30, Items = [Line("price_monthly", "P-MONTHLY-30", 1)] };
    private static ProductListingDocument Listing(long productId, long listingId, SubscriptionOffer? offer = null) => new() { Id = listingId, TenantId = 1, SiteId = 10, ProductId = productId, Culture = "en-US", Slug = $"item-{listingId}", Name = $"Item {listingId}", Price = 10m, Currency = "USD", IsPublished = true, SubscriptionOffer = offer };
    private static Address Address() => new() { Street = "1 Main", City = "Town", PostalCode = "11111", Country = "US" };
    private static async Task<SableTestHarness> HarnessAsync() { var harness = new SableTestHarness().WithConfiguration(new CommerceModule().Configure); await harness.InitializeAsync(); return harness; }
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private int calls;
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            calls++; Request = request; Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            return responder(calls);
        }
    }

    private sealed class FakeSubscriptionAdapter(string provider) : ISubscriptionCheckoutProviderAdapter
    {
        public string Provider => provider;
        public int Creates { get; private set; }
        public int Retrieves { get; private set; }
        public Task<SubscriptionCheckoutOutcome> InitiateAsync(PaymentProviderAccount account, SubscriptionProviderCheckout request, CancellationToken ct = default) { Creates++; return Task.FromResult(SubscriptionCheckoutOutcome.Succeeded(new(0, $"{provider}-checkout", "https://provider.test/approve"))); }
        public Task<Result<SubscriptionCheckoutInitiation, AeroError>> RetrieveAsync(PaymentProviderAccount account, string checkoutReference, long subscriptionId, CancellationToken ct = default) { Retrieves++; return Task.FromResult(Prelude.Ok<SubscriptionCheckoutInitiation, AeroError>(new(subscriptionId, checkoutReference, "https://provider.test/approve"))); }
    }
}
