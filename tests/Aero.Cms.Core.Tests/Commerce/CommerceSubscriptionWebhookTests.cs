using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Payments;
using Aero.Cms.Modules.Commerce.Subscriptions;
using Aero.Cms.Modules.Commerce.Subscriptions.Webhooks;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceSubscriptionWebhookTests
{
    [Test]
    public async Task Stripe_signed_subscription_invoice_is_verified_then_translated()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var raw = Encoding.UTF8.GetBytes("{\"id\":\"evt_sig\",\"type\":\"invoice.paid\",\"created\":" + now + ",\"data\":{\"object\":{\"id\":\"in_sig\",\"subscription\":\"sub_123\",\"customer\":\"cus_123\",\"payment_intent\":\"pi_sig\",\"currency\":\"usd\",\"amount_paid\":1200,\"lines\":{\"data\":[{\"price\":{\"id\":\"price_monthly\"},\"period\":{\"start\":" + now + ",\"end\":" + (now + 2592000) + "}}]}}}}");
        var header = StripeHeader("hook", raw, now);
        var headers = new HeaderDictionary { ["Stripe-Signature"] = header };
        var adapter = new StripePaymentProviderAdapter(new NoopClientFactory());

        var result = await adapter.VerifyAndTranslateSubscriptionAsync(StripeAccount(), raw, headers);

        result.ShouldBeOfType<Result<VerifiedSubscriptionWebhook, AeroError>.Ok>().Value.ShouldSatisfyAllConditions(
            x => x.Kind.ShouldBe(SubscriptionWebhookEventKind.InvoicePaid),
            x => x.PaymentReference.ShouldBe("pi_sig"),
            x => x.ProviderOfferReferences.Single().ShouldBe("price_monthly"));
    }

    [Test]
    public async Task PayPal_signature_verification_response_precedes_subscription_translation()
    {
        var handler = new SequenceHandler("""{"access_token":"token"}""", """{"verification_status":"SUCCESS"}""");
        var adapter = new PayPalPaymentProviderAdapter(new SingleClientFactory(handler));
        var raw = Encoding.UTF8.GetBytes("""{"id":"WH-1","event_type":"BILLING.SUBSCRIPTION.ACTIVATED","create_time":"2026-07-01T00:00:00Z","resource":{"id":"I-123","plan_id":"P-MONTHLY-30","subscriber":{"payer_id":"payer"},"start_time":"2026-07-01T00:00:00Z","billing_info":{"next_billing_time":"2026-07-31T00:00:00Z"}}}""");
        var headers = new HeaderDictionary { ["Paypal-Transmission-Id"] = "id", ["Paypal-Transmission-Time"] = "2026-07-01T00:00:00Z", ["Paypal-Cert-Url"] = "https://paypal.test/cert", ["Paypal-Auth-Algo"] = "SHA256withRSA", ["Paypal-Transmission-Sig"] = "sig" };

        var result = await adapter.VerifyAndTranslateSubscriptionAsync(PayPalAccount(), raw, headers);

        result.ShouldBeOfType<Result<VerifiedSubscriptionWebhook, AeroError>.Ok>().Value.Kind.ShouldBe(SubscriptionWebhookEventKind.SubscriptionActivated);
        handler.Calls.ShouldBe(2);
    }

    [Test]
    public void Stripe_paused_resumed_and_paused_status_events_translate_to_safe_lifecycle_kinds()
    {
        var paused = StripePaymentProviderAdapter.TranslateSubscriptionWebhook(Encoding.UTF8.GetBytes("""{"id":"evt-paused","type":"customer.subscription.paused","created":1782864000,"data":{"object":{"id":"sub_123","customer":"cus_123","status":"paused"}}}"""));
        var updatedPaused = StripePaymentProviderAdapter.TranslateSubscriptionWebhook(Encoding.UTF8.GetBytes("""{"id":"evt-updated-paused","type":"customer.subscription.updated","created":1782864000,"data":{"object":{"id":"sub_123","customer":"cus_123","status":"paused"}}}"""));
        var resumed = StripePaymentProviderAdapter.TranslateSubscriptionWebhook(Encoding.UTF8.GetBytes("""{"id":"evt-resumed","type":"customer.subscription.resumed","created":1782864000,"data":{"object":{"id":"sub_123","customer":"cus_123","status":"active"}}}"""));

        paused.ShouldBeOfType<Result<VerifiedSubscriptionWebhook, AeroError>.Ok>().Value.Kind.ShouldBe(SubscriptionWebhookEventKind.SubscriptionSuspended);
        updatedPaused.ShouldBeOfType<Result<VerifiedSubscriptionWebhook, AeroError>.Ok>().Value.Kind.ShouldBe(SubscriptionWebhookEventKind.SubscriptionSuspended);
        resumed.ShouldBeOfType<Result<VerifiedSubscriptionWebhook, AeroError>.Ok>().Value.Kind.ShouldBe(SubscriptionWebhookEventKind.SubscriptionReactivated);
    }

    [Test]
    public async Task PayPal_lifecycle_derives_a_non_thirty_day_period_without_pairing_subscription_start_with_next_billing()
    {
        var raw = Encoding.UTF8.GetBytes("""{"id":"WH-14","event_type":"BILLING.SUBSCRIPTION.ACTIVATED","create_time":"2026-07-01T00:00:00Z","resource":{"id":"I-14","plan_id":"P-EVERY-14-DAYS","status":"ACTIVE","start_time":"2026-01-01T00:00:00Z","billing_info":{"next_billing_time":"2026-07-15T00:00:00Z"}}}""");
        var translated = PayPalPaymentProviderAdapter.TranslateSubscriptionWebhook(raw)
            .ShouldBeOfType<Result<VerifiedSubscriptionWebhook, AeroError>.Ok>().Value;
        translated.PeriodStartsOn.ShouldBeNull();
        translated.PeriodEndsOn.ShouldBe(DateTimeOffset.Parse("2026-07-15T00:00:00Z"));

        await using var harness = await HarnessAsync();
        harness.Session.Store(PayPalSubscription(14));
        harness.Session.Store(Order(14));
        await harness.Session.SaveChangesAsync();
        var adapter = new FakeWebhookAdapter(Prelude.Ok<VerifiedSubscriptionWebhook, AeroError>(translated with { CheckoutReference = null }), "paypal");

        (await Service(harness.Session, adapter, "paypal", "paypal-store").ReconcileAsync("paypal", "paypal-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();

        await using var verify = await harness.OpenSessionAsync();
        var subscription = (await verify.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.Id == 501))!;
        subscription.State.ShouldBe(SubscriptionState.Active);
        subscription.CurrentPeriodEndsOn.ShouldBe(DateTimeOffset.Parse("2026-07-15T00:00:00Z"));
        subscription.CurrentPeriodStartsOn.ShouldBe(DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
    }

    [Test]
    public async Task PayPal_end_only_lifecycle_update_derives_a_new_period_from_the_end_not_the_old_start()
    {
        await using var harness = await HarnessAsync();
        var subscription = PayPalSubscription(30);
        subscription.Lines[0].ProviderOfferReference = "P-EVERY-30-DAYS";
        subscription.CurrentPeriodStartsOn = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        subscription.CurrentPeriodEndsOn = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        harness.Session.Store(subscription);
        harness.Session.Store(Order(30));
        await harness.Session.SaveChangesAsync();
        var update = new VerifiedSubscriptionWebhook("WH-30", DateTimeOffset.Parse("2026-07-02T00:00:00Z"), SubscriptionWebhookEventKind.SubscriptionUpdated,
            null, "I-14", null, null, null, null, null, null, DateTimeOffset.Parse("2026-08-01T00:00:00Z"), ["P-EVERY-30-DAYS"], null);

        (await Service(harness.Session, new FakeWebhookAdapter(Prelude.Ok<VerifiedSubscriptionWebhook, AeroError>(update), "paypal"), "paypal", "paypal-store")
            .ReconcileAsync("paypal", "paypal-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();

        await using var verify = await harness.OpenSessionAsync();
        var saved = (await verify.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.Id == 501))!;
        saved.CurrentPeriodStartsOn.ShouldBe(DateTimeOffset.Parse("2026-07-02T00:00:00Z"));
        saved.CurrentPeriodEndsOn.ShouldBe(DateTimeOffset.Parse("2026-08-01T00:00:00Z"));
    }

    [Test]
    public async Task Paid_callback_without_provider_period_bounds_requires_manual_review_without_creating_a_cycle()
    {
        await using var harness = await HarnessAsync();
        harness.Session.Store(Subscription());
        harness.Session.Store(Order());
        await harness.Session.SaveChangesAsync();
        var unsafePaid = Paid("evt-no-period", DateTimeOffset.Parse("2026-07-01T00:00:00Z")) with { PeriodStartsOn = null, PeriodEndsOn = null };

        (await Service(harness.Session, new FakeWebhookAdapter(Prelude.Ok<VerifiedSubscriptionWebhook, AeroError>(unsafePaid)))
            .ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();

        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.Id == 500))!.State.ShouldBe(SubscriptionState.ManualReview);
        (await verify.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == 400))!.PaymentStatus.ShouldBe(OrderPaymentStatus.ManualReview);
        (await verify.Query<SubscriptionCycleDocument>().ToListAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Lifecycle_callback_without_period_bounds_preserves_the_existing_period()
    {
        await using var harness = await HarnessAsync();
        var subscription = Subscription();
        subscription.CurrentPeriodStartsOn = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        subscription.CurrentPeriodEndsOn = DateTimeOffset.Parse("2026-07-31T00:00:00Z");
        harness.Session.Store(subscription);
        harness.Session.Store(Order());
        await harness.Session.SaveChangesAsync();
        var lifecycle = SubscriptionEvent("evt-no-bounds", SubscriptionWebhookEventKind.SubscriptionUpdated);

        (await Service(harness.Session, new FakeWebhookAdapter(Prelude.Ok<VerifiedSubscriptionWebhook, AeroError>(lifecycle)))
            .ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();

        await using var verify = await harness.OpenSessionAsync();
        var saved = (await verify.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.Id == 500))!;
        saved.CurrentPeriodStartsOn.ShouldBe(DateTimeOffset.Parse("2026-07-01T00:00:00Z"));
        saved.CurrentPeriodEndsOn.ShouldBe(DateTimeOffset.Parse("2026-07-31T00:00:00Z"));
    }
    [Test]
    public async Task Unverified_callback_does_not_create_a_receipt()
    {
        await using var harness = await HarnessAsync();
        var adapter = new FakeWebhookAdapter(Prelude.Fail<VerifiedSubscriptionWebhook, AeroError>(AeroError.CreateError("signature")));
        var service = Service(harness.Session, adapter);

        (await service.ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeFalse();
        adapter.Calls.ShouldBe(1);
        (await harness.Session.Query<SubscriptionWebhookReceiptDocument>().ToListAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Paid_cycle_is_idempotent_and_older_distinct_event_is_ignored()
    {
        await using var harness = await HarnessAsync();
        harness.Session.Store(Subscription());
        harness.Session.Store(Order());
        await harness.Session.SaveChangesAsync();
        var adapter = new FakeWebhookAdapter(Paid("evt-paid", DateTimeOffset.Parse("2026-07-01T00:00:00Z")));
        var service = Service(harness.Session, adapter);

        (await service.ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();
        (await service.ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();
        adapter.Result = Prelude.Ok<VerifiedSubscriptionWebhook, AeroError>(new("evt-older", DateTimeOffset.Parse("2026-06-30T00:00:00Z"), SubscriptionWebhookEventKind.SubscriptionUpdated, null, "sub_123", null, null, null, null, null, null, null, ["price_monthly"], null));
        (await service.ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();

        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<SubscriptionCycleDocument>().ToListAsync()).Count.ShouldBe(1);
        (await verify.Query<SubscriptionWebhookReceiptDocument>().ToListAsync()).Count.ShouldBe(2);
        (await verify.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == 400))!.PaymentStatus.ShouldBe(OrderPaymentStatus.Paid);
        (await verify.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.Id == 500))!.State.ShouldBe(SubscriptionState.Active);
    }

    [Test]
    public async Task Value_mismatch_marks_order_and_subscription_for_manual_review()
    {
        await using var harness = await HarnessAsync();
        harness.Session.Store(Subscription());
        harness.Session.Store(Order());
        await harness.Session.SaveChangesAsync();
        var adapter = new FakeWebhookAdapter(Paid("evt-mismatch", DateTimeOffset.Parse("2026-07-01T00:00:00Z")) with { Amount = 11m });

        (await Service(harness.Session, adapter).ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();

        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.Id == 500))!.State.ShouldBe(SubscriptionState.ManualReview);
        (await verify.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == 400))!.PaymentStatus.ShouldBe(OrderPaymentStatus.ManualReview);
        var cycle = (await verify.Query<SubscriptionCycleDocument>().FirstOrDefaultAsync(x => x.ProviderCycleReference == "in_123"))!;
        cycle.State.ShouldBe(SubscriptionCycleState.ManualReview);
        var receipt = (await verify.Query<SubscriptionWebhookReceiptDocument>().FirstOrDefaultAsync(x => x.ProviderEventId == "evt-mismatch"))!;
        receipt.State.ShouldBe(SubscriptionWebhookReceiptState.ManualReview);
        receipt.SubscriptionCycleId.ShouldBe(cycle.Id);
    }

    [Test]
    public async Task Unknown_route_account_is_not_verified_or_consumed()
    {
        await using var harness = await HarnessAsync();
        var adapter = new FakeWebhookAdapter(Paid("evt-unknown", DateTimeOffset.UtcNow));

        (await Service(harness.Session, adapter).ReconcileAsync("stripe", "other-store", [], new HeaderDictionary())).IsSuccess.ShouldBeFalse();
        adapter.Calls.ShouldBe(0);
        (await harness.Session.Query<SubscriptionWebhookReceiptDocument>().ToListAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task Same_second_failed_cycle_is_upgraded_by_paid_event_without_a_second_cycle()
    {
        await using var harness = await HarnessAsync(); harness.Session.Store(Subscription()); harness.Session.Store(Order()); await harness.Session.SaveChangesAsync();
        var at = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        var adapter = new FakeWebhookAdapter(Paid("evt-failed", at) with { Kind = SubscriptionWebhookEventKind.InvoicePaymentFailed, PaymentReference = null });
        var service = Service(harness.Session, adapter);
        (await service.ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();
        adapter.Result = Prelude.Ok<VerifiedSubscriptionWebhook, AeroError>(Paid("evt-paid-after-failed", at));
        (await service.ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();
        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<SubscriptionCycleDocument>().ToListAsync()).Count.ShouldBe(1);
        (await verify.Query<SubscriptionCycleDocument>().FirstOrDefaultAsync(x => x.ProviderCycleReference == "in_123"))!.State.ShouldBe(SubscriptionCycleState.Paid);
    }

    [Test]
    public async Task Late_failure_for_a_paid_cycle_never_marks_it_failed_and_requires_manual_review()
    {
        await using var harness = await HarnessAsync();
        harness.Session.Store(Subscription());
        harness.Session.Store(Order());
        await harness.Session.SaveChangesAsync();
        var at = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        var adapter = new FakeWebhookAdapter(Paid("evt-paid-first", at));
        var service = Service(harness.Session, adapter);
        (await service.ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();

        adapter.Result = Prelude.Ok<VerifiedSubscriptionWebhook, AeroError>(Paid("evt-late-reversal", at.AddMinutes(1)) with
        {
            Kind = SubscriptionWebhookEventKind.InvoicePaymentFailed,
            PaymentReference = null
        });
        (await service.ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();

        await using var verify = await harness.OpenSessionAsync();
        var cycle = (await verify.Query<SubscriptionCycleDocument>().FirstOrDefaultAsync(x => x.ProviderCycleReference == "in_123"))!;
        cycle.State.ShouldBe(SubscriptionCycleState.ManualReview);
        cycle.State.ShouldNotBe(SubscriptionCycleState.Failed);
        (await verify.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == 400))!.PaymentStatus.ShouldBe(OrderPaymentStatus.ManualReview);
        (await verify.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.Id == 500))!.State.ShouldBe(SubscriptionState.ManualReview);
    }

    [Test]
    public async Task Generic_lifecycle_events_never_reactivate_terminal_or_manual_review_subscriptions()
    {
        await using var cancelledHarness = await HarnessAsync();
        var cancelled = Subscription();
        cancelled.State = SubscriptionState.Cancelled;
        cancelledHarness.Session.Store(cancelled);
        cancelledHarness.Session.Store(Order());
        await cancelledHarness.Session.SaveChangesAsync();
        var cancelledAdapter = new FakeWebhookAdapter(SubscriptionEvent("evt-cancelled-generic", SubscriptionWebhookEventKind.SubscriptionActivated));
        (await Service(cancelledHarness.Session, cancelledAdapter).ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();
        await using (var verify = await cancelledHarness.OpenSessionAsync())
            (await verify.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.Id == 500))!.State.ShouldBe(SubscriptionState.Cancelled);

        await using var expiredHarness = await HarnessAsync();
        var expired = Subscription();
        expired.State = SubscriptionState.Expired;
        expiredHarness.Session.Store(expired);
        expiredHarness.Session.Store(Order());
        await expiredHarness.Session.SaveChangesAsync();
        var expiredAdapter = new FakeWebhookAdapter(SubscriptionEvent("evt-expired-generic", SubscriptionWebhookEventKind.SubscriptionUpdated));
        (await Service(expiredHarness.Session, expiredAdapter).ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();
        await using (var verify = await expiredHarness.OpenSessionAsync())
            (await verify.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.Id == 500))!.State.ShouldBe(SubscriptionState.Expired);

        await using var manualHarness = await HarnessAsync();
        var manual = Subscription();
        manual.State = SubscriptionState.ManualReview;
        manual.RequiresManualReview = true;
        manual.ManualReviewReason = "amount mismatch";
        manualHarness.Session.Store(manual);
        manualHarness.Session.Store(Order());
        await manualHarness.Session.SaveChangesAsync();
        var manualAdapter = new FakeWebhookAdapter(SubscriptionEvent("evt-manual-generic", SubscriptionWebhookEventKind.SubscriptionUpdated));
        (await Service(manualHarness.Session, manualAdapter).ReconcileAsync("stripe", "stripe-store", [], new HeaderDictionary())).IsSuccess.ShouldBeTrue();
        await using var manualVerify = await manualHarness.OpenSessionAsync();
        var verifiedManual = (await manualVerify.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.Id == 500))!;
        verifiedManual.State.ShouldBe(SubscriptionState.ManualReview);
        verifiedManual.RequiresManualReview.ShouldBeTrue();
        verifiedManual.ManualReviewReason.ShouldBe("amount mismatch");
    }

    private static ISubscriptionReconciliationService Service(IDocumentSession session, FakeWebhookAdapter adapter, string provider = "stripe", string accountKey = "stripe-store")
        => new SubscriptionReconciliationService(session, new PaymentProviderRegistry([], Options.Create(new CommercePaymentOptions { Accounts = [new() { Enabled = true, Provider = provider, AccountKey = accountKey, TenantId = 1, SiteId = 10, BaseUrl = $"https://{provider}.test/", SecretKey = "secret", WebhookSecret = "hook", ClientId = "client", ClientSecret = "secret", WebhookId = "webhook" }] })), [adapter]);

    private static VerifiedSubscriptionWebhook Paid(string eventId, DateTimeOffset occurred) => new(eventId, occurred, SubscriptionWebhookEventKind.InvoicePaid, null, "sub_123", "cus_123", "in_123", "pi_123", 12m, "USD", occurred, occurred.AddDays(30), ["price_monthly"], null);
    private static VerifiedSubscriptionWebhook SubscriptionEvent(string eventId, SubscriptionWebhookEventKind kind) => new(eventId, DateTimeOffset.Parse("2026-07-01T00:00:00Z"), kind, null, "sub_123", "cus_123", null, null, null, null, null, null, ["price_monthly"], null);
    private static PaymentProviderAccount StripeAccount() => new("stripe", "stripe-store", 1, 10, "secret", "hook", null, null, null, "https://stripe.test/", []);
    private static PaymentProviderAccount PayPalAccount() => new("paypal", "paypal-store", 1, 10, null, null, "client", "secret", "webhook", "https://paypal.test/", []);
    private static string StripeHeader(string secret, byte[] raw, long timestamp)
    {
        var prefix = Encoding.ASCII.GetBytes($"{timestamp}."); var data = new byte[prefix.Length + raw.Length]; Buffer.BlockCopy(prefix, 0, data, 0, prefix.Length); Buffer.BlockCopy(raw, 0, data, prefix.Length, raw.Length);
        return $"t={timestamp},v1={Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), data)).ToLowerInvariant()}";
    }
    private static SubscriptionDocument Subscription() => new() { Id = 500, TenantId = 1, SiteId = 10, ExternalMemberId = 7, OrderId = 400, Provider = "stripe", ProviderAccountKey = "stripe-store", ProviderOperationKey = "commerce-subscription-order-400", ProviderCheckoutReference = "cs_123", ProviderSubscriptionReference = "sub_123", Lines = [new() { ProductId = 1, ListingId = 2, ProductName = "Monthly", ListingName = "Monthly", Sku = "MONTH", Quantity = 1, UnitAmount = 12m, ProviderOfferReference = "price_monthly" }], Currency = "USD", IntervalDays = 30 };
    private static SubscriptionDocument PayPalSubscription(int intervalDays) => new() { Id = 501, TenantId = 1, SiteId = 10, ExternalMemberId = 7, OrderId = 400, Provider = "paypal", ProviderAccountKey = "paypal-store", ProviderOperationKey = "commerce-subscription-order-400", ProviderCheckoutReference = "I-14", ProviderSubscriptionReference = "I-14", Lines = [new() { ProductId = 1, ListingId = 2, ProductName = "Every two weeks", ListingName = "Every two weeks", Sku = "EVERY-14", Quantity = 1, UnitAmount = 12m, ProviderOfferReference = "P-EVERY-14-DAYS" }], Currency = "USD", IntervalDays = intervalDays };
    private static OrderEntity Order(int intervalDays = 30) => new() { Id = 400, TenantId = 1, SiteId = 10, ExternalMemberId = 7, Status = OrderStatus.Submitted, PaymentStatus = OrderPaymentStatus.Pending, BillingKind = OrderBillingKind.Recurring, BillingIntervalDays = intervalDays, Currency = "USD", Items = [new() { ProductId = 1, ListingId = 2, ProductName = "Monthly", Sku = "MONTH", Quantity = 1, UnitPrice = 12m, Currency = "USD", BillingKind = OrderBillingKind.Recurring, FulfillmentMode = ProductFulfillmentMode.NonInventoryRecurring, BillingIntervalDays = intervalDays, StripePriceId = "price_monthly" }] };
    private static async Task<SableTestHarness> HarnessAsync() { var value = new SableTestHarness().WithConfiguration(new CommerceModule().Configure); await value.InitializeAsync(); return value; }

    private sealed class FakeWebhookAdapter(Result<VerifiedSubscriptionWebhook, AeroError> result, string provider = "stripe") : ISubscriptionWebhookProviderAdapter
    {
        public string Provider => provider;
        public int Calls { get; private set; }
        public Result<VerifiedSubscriptionWebhook, AeroError> Result { get; set; } = result;
        public Task<Result<VerifiedSubscriptionWebhook, AeroError>> VerifyAndTranslateSubscriptionAsync(PaymentProviderAccount account, byte[] rawBody, IHeaderDictionary headers, CancellationToken ct = default) { Calls++; return Task.FromResult(Result); }
    }
    private sealed class NoopClientFactory : IHttpClientFactory { public HttpClient CreateClient(string name) => new(); }
    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(handler, false); }
    private sealed class SequenceHandler(params string[] responses) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responses[Calls++], Encoding.UTF8, "application/json") });
    }
}
