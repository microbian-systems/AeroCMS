using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Payments;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;
using System.Text.Json;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommercePaymentsTests
{
    [Test]
    public void Registry_requires_exact_provider_tenant_and_site_scope()
    {
        var stripe = new FakeProvider("stripe");
        var registry = CreateRegistry([stripe], [Account("stripe", "stripe-a", 1, 10)]);

        registry.Resolve("stripe", 1, 10).IsSuccess.ShouldBeTrue();
        registry.Resolve("stripe", 1, 11).IsSuccess.ShouldBeFalse();
        registry.Resolve("unsupported", 1, 10).IsSuccess.ShouldBeFalse();
    }

    [Test]
    public void Options_validator_accepts_empty_accounts_and_rejects_malformed_enabled_accounts()
    {
        var validator = new CommercePaymentOptionsValidator();
        validator.Validate(null, new CommercePaymentOptions()).Succeeded.ShouldBeTrue();
        validator.Validate(null, new CommercePaymentOptions
        {
            Accounts = [new PaymentProviderAccountOptions { Enabled = true, Provider = "Stripe", AccountKey = "dup", TenantId = 0, SiteId = 0, BaseUrl = "http://example.test" }]
        }).Succeeded.ShouldBeFalse();
    }

    [Test]
    public async Task Initiation_uses_authoritative_order_total_and_idempotent_attempt()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(Order(101, 1, 10, 7, 23m));
        await harness.Session.SaveChangesAsync();
        var stripe = new FakeProvider("stripe");
        var service = CreateService(harness.Session, stripe, [Account("stripe", "stripe-a", 1, 10)]);

        var first = await service.InitiateAsync(1, 10, 7, new(101, "stripe", "request-1"), returnUrls: StripeReturnUrls());
        var second = await service.InitiateAsync(1, 10, 7, new(101, "stripe", "request-1"));

        first.ShouldBeOfType<Result<PaymentInitiation, AeroError>.Ok>().Value.Status.ShouldBe(PaymentAttemptStatus.RequiresCustomerAction);
        second.ShouldBeOfType<Result<PaymentInitiation, AeroError>.Ok>().Value.AttemptId.ShouldBe(first.ShouldBeOfType<Result<PaymentInitiation, AeroError>.Ok>().Value.AttemptId);
        stripe.Initiations.ShouldBe(1);
        stripe.LastAmount.ShouldBe(23m);
        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == 101))!.PaymentStatus.ShouldBe(OrderPaymentStatus.Pending);
    }

    [Test]
    public async Task New_stripe_initiation_without_secure_return_urls_does_not_mutate_the_order_or_create_an_attempt()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(Order(111, 1, 10, 7, 23m));
        await harness.Session.SaveChangesAsync();
        var stripe = new FakeProvider("stripe");
        var service = CreateService(harness.Session, stripe, [Account("stripe", "stripe-a", 1, 10)]);

        (await service.InitiateAsync(1, 10, 7, new(111, "stripe", "missing-return-targets"))).IsSuccess.ShouldBeFalse();
        stripe.Initiations.ShouldBe(0);

        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == 111))!.PaymentStatus.ShouldBe(OrderPaymentStatus.Unpaid);
        (await verify.Query<PaymentAttemptDocument>().FirstOrDefaultAsync(x => x.OrderId == 111)).ShouldBeNull();
    }

    [Test]
    public async Task Initiation_rejects_same_key_for_different_provider_and_wrong_member()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(Order(102, 1, 10, 7, 23m));
        await harness.Session.SaveChangesAsync();
        var stripe = new FakeProvider("stripe");
        var paypal = new FakeProvider("paypal");
        var service = CreateService(harness.Session, [stripe, paypal], [Account("stripe", "stripe-a", 1, 10), Account("paypal", "paypal-a", 1, 10)]);

        (await service.InitiateAsync(1, 10, 8, new(102, "stripe", "request-2"))).IsSuccess.ShouldBeFalse();
        (await service.InitiateAsync(1, 10, 7, new(102, "stripe", "request-2"), returnUrls: StripeReturnUrls())).IsSuccess.ShouldBeTrue();
        (await service.InitiateAsync(1, 10, 7, new(102, "stripe", "different-request"))).IsSuccess.ShouldBeFalse();
        (await service.InitiateAsync(1, 10, 7, new(102, "paypal", "request-2"))).IsSuccess.ShouldBeFalse();
        stripe.Initiations.ShouldBe(1);
        paypal.Initiations.ShouldBe(0);
    }

    [Test]
    public async Task Verified_success_reconciles_once_without_changing_order_fulfillment_status()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(Order(103, 1, 10, 7, 15m));
        harness.Session.Store(Attempt(203, 103, "pi_103", 15m));
        await harness.Session.SaveChangesAsync();
        var stripe = new FakeProvider("stripe") { Callback = new("evt_103", "pi_103", PaymentAttemptStatus.Succeeded, 15m, "USD", null) };
        var service = CreateService(harness.Session, stripe, [Account("stripe", "stripe-a", 1, 10)]);

        (await service.ReconcileAsync("stripe", "stripe-a", [1], new HeaderDictionary())).IsSuccess.ShouldBeTrue();
        (await service.ReconcileAsync("stripe", "stripe-a", [1], new HeaderDictionary())).IsSuccess.ShouldBeTrue();
        await using var verify = await harness.OpenSessionAsync();
        var order = (await verify.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == 103))!;
        order.Status.ShouldBe(OrderStatus.Submitted);
        order.PaymentStatus.ShouldBe(OrderPaymentStatus.Paid);
        (await verify.Query<PaymentWebhookReceiptDocument>().ToListAsync()).Count.ShouldBe(1);
        (await service.InitiateAsync(1, 10, 7, new(103, "stripe", "request-203"), returnUrls: StripeReturnUrls())).IsSuccess.ShouldBeTrue();
        stripe.Initiations.ShouldBe(0);
    }

    [Test]
    public async Task Amount_mismatch_is_manual_review_and_failed_callback_preserves_stock_and_order_status()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(new ProductDocument { Id = 500, TenantId = 1, Name = "Canonical", Sku = "SKU-500", StockQuantity = 4, IsActive = true });
        harness.Session.Store(Order(104, 1, 10, 7, 19m));
        harness.Session.Store(Attempt(204, 104, "pi_104", 19m));
        await harness.Session.SaveChangesAsync();
        var stripe = new FakeProvider("stripe") { Callback = new("evt_104", "pi_104", PaymentAttemptStatus.Succeeded, 18m, "USD", null) };
        var service = CreateService(harness.Session, stripe, [Account("stripe", "stripe-a", 1, 10)]);

        (await service.ReconcileAsync("stripe", "stripe-a", [1], new HeaderDictionary())).IsSuccess.ShouldBeTrue();
        await using var mismatchVerify = await harness.OpenSessionAsync();
        (await mismatchVerify.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == 104))!.PaymentStatus.ShouldBe(OrderPaymentStatus.ManualReview);

        await using var failureHarness = await CreateHarnessAsync();
        failureHarness.Session.Store(new ProductDocument { Id = 501, TenantId = 1, Name = "Canonical", Sku = "SKU-501", StockQuantity = 4, IsActive = true });
        failureHarness.Session.Store(Order(105, 1, 10, 7, 19m));
        failureHarness.Session.Store(Attempt(205, 105, "pi_105", 19m));
        await failureHarness.Session.SaveChangesAsync();
        var failedStripe = new FakeProvider("stripe") { Callback = new("evt_105", "pi_105", PaymentAttemptStatus.Failed, 19m, "USD", "declined") };
        var failureService = CreateService(failureHarness.Session, failedStripe, [Account("stripe", "stripe-a", 1, 10)]);
        (await failureService.ReconcileAsync("stripe", "stripe-a", [1], new HeaderDictionary())).IsSuccess.ShouldBeTrue();
        await using var failureVerify = await failureHarness.OpenSessionAsync();
        var failedOrder = (await failureVerify.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == 105))!;
        failedOrder.Status.ShouldBe(OrderStatus.Submitted);
        failedOrder.PaymentStatus.ShouldBe(OrderPaymentStatus.Failed);
        (await failureVerify.Query<ProductDocument>().FirstOrDefaultAsync(x => x.Id == 501))!.StockQuantity.ShouldBe(4);
    }

    [Test]
    public async Task Invalid_callback_makes_no_database_mutation()
    {
        await using var harness = await CreateHarnessAsync();
        harness.Session.Store(Order(106, 1, 10, 7, 19m));
        harness.Session.Store(Attempt(206, 106, "pi_106", 19m));
        await harness.Session.SaveChangesAsync();
        var stripe = new FakeProvider("stripe") { RejectCallback = true };
        var service = CreateService(harness.Session, stripe, [Account("stripe", "stripe-a", 1, 10)]);

        (await service.ReconcileAsync("stripe", "stripe-a", [1], new HeaderDictionary())).IsSuccess.ShouldBeFalse();
        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == 106))!.PaymentStatus.ShouldBe(OrderPaymentStatus.Unpaid);
        (await verify.Query<PaymentWebhookReceiptDocument>().ToListAsync()).ShouldBeEmpty();
    }

    [Test]
    public void Stripe_signature_rejects_stale_or_malformed_and_accepts_exact_raw_payload()
    {
        var raw = "{\"id\":\"evt\"}"u8.ToArray();
        const string secret = "whsec_test";
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.ToUnixTimeSeconds();
        var signature = Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret), System.Text.Encoding.UTF8.GetBytes($"{timestamp}.").Concat(raw).ToArray())).ToLowerInvariant();

        StripeSignatureVerifier.IsValid($"t={timestamp},v1={signature}", secret, raw, now).ShouldBeTrue();
        StripeSignatureVerifier.IsValid("t=not-a-time,v1=00", secret, raw, now).ShouldBeFalse();
        StripeSignatureVerifier.IsValid($"t={timestamp - 301},v1={signature}", secret, raw, now).ShouldBeFalse();
    }

    [Test]
    public async Task Terminal_and_ambiguous_provider_failures_have_different_durable_dispositions()
    {
        await using var terminalHarness = await CreateHarnessAsync();
        terminalHarness.Session.Store(Order(107, 1, 10, 7, 10m)); await terminalHarness.Session.SaveChangesAsync();
        var terminal = new FakeProvider("stripe") { Outcome = PaymentProviderInitiationOutcome.Terminal("declined") };
        var terminalService = CreateService(terminalHarness.Session, terminal, [Account("stripe", "stripe-a", 1, 10)]);
        (await terminalService.InitiateAsync(1, 10, 7, new(107, "stripe", "terminal"), returnUrls: StripeReturnUrls())).IsSuccess.ShouldBeFalse();
        (await terminalService.InitiateAsync(1, 10, 7, new(107, "stripe", "terminal"))).IsSuccess.ShouldBeFalse();
        terminal.Initiations.ShouldBe(1);
        await using var terminalVerify = await terminalHarness.OpenSessionAsync();
        (await terminalVerify.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == 107))!.PaymentStatus.ShouldBe(OrderPaymentStatus.Failed);

        await using var ambiguousHarness = await CreateHarnessAsync();
        ambiguousHarness.Session.Store(Order(108, 1, 10, 7, 10m)); await ambiguousHarness.Session.SaveChangesAsync();
        var ambiguous = new FakeProvider("stripe") { Outcome = PaymentProviderInitiationOutcome.Retryable("timeout") };
        var ambiguousService = CreateService(ambiguousHarness.Session, ambiguous, [Account("stripe", "stripe-a", 1, 10)]);
        (await ambiguousService.InitiateAsync(1, 10, 7, new(108, "stripe", "ambiguous"), returnUrls: StripeReturnUrls())).IsSuccess.ShouldBeFalse();
        await using var ambiguousVerify = await ambiguousHarness.OpenSessionAsync();
        var attempt = (await ambiguousVerify.Query<PaymentAttemptDocument>().FirstOrDefaultAsync(x => x.OrderId == 108))!;
        attempt.Status.ShouldBe(PaymentAttemptStatus.Initiating);
        (await ambiguousVerify.Query<OrderEntity>().FirstOrDefaultAsync(x => x.Id == 108))!.PaymentStatus.ShouldBe(OrderPaymentStatus.Pending);
    }

    [Test]
    public async Task Expired_initiating_attempt_is_manual_review_without_another_provider_call()
    {
        await using var harness = await CreateHarnessAsync();
        var order = Order(109, 1, 10, 7, 10m); order.PaymentStatus = OrderPaymentStatus.Pending;
        harness.Session.Store(order);
        harness.Session.Store(new PaymentAttemptDocument { Id = 209, TenantId = 1, SiteId = 10, ExternalMemberId = 7, OrderId = 109, Provider = "stripe", ProviderAccountKey = "stripe-a", Amount = 10m, Currency = "USD", RequestIdempotencyKey = "expired", ProviderOperationKey = "stable-key", Status = PaymentAttemptStatus.Initiating, InitiationRetryExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        await harness.Session.SaveChangesAsync();
        var stripe = new FakeProvider("stripe");
        var service = CreateService(harness.Session, stripe, [Account("stripe", "stripe-a", 1, 10)]);
        (await service.InitiateAsync(1, 10, 7, new(109, "stripe", "expired"))).IsSuccess.ShouldBeFalse();
        (await service.InitiateAsync(1, 10, 7, new(109, "stripe", "expired"))).IsSuccess.ShouldBeFalse();
        stripe.Initiations.ShouldBe(0);
        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<PaymentAttemptDocument>().FirstOrDefaultAsync(x => x.Id == 209))!.Status.ShouldBe(PaymentAttemptStatus.ManualReview);
    }

    [Test]
    public async Task Cancelled_attempt_replay_is_failure_without_another_provider_call()
    {
        await using var harness = await CreateHarnessAsync();
        var order = Order(110, 1, 10, 7, 10m);
        order.PaymentStatus = OrderPaymentStatus.Cancelled;
        var attempt = Attempt(210, 110, "pi_110", 10m);
        attempt.RequestIdempotencyKey = "cancelled";
        attempt.Status = PaymentAttemptStatus.Cancelled;
        harness.Session.Store(order);
        harness.Session.Store(attempt);
        await harness.Session.SaveChangesAsync();
        var stripe = new FakeProvider("stripe");
        var service = CreateService(harness.Session, stripe, [Account("stripe", "stripe-a", 1, 10)]);

        (await service.InitiateAsync(1, 10, 7, new(110, "stripe", "cancelled"))).IsSuccess.ShouldBeFalse();
        stripe.Initiations.ShouldBe(0);
    }

    [Test]
    public void Options_validator_rejects_whitespace_and_colliding_account_keys()
    {
        var validator = new CommercePaymentOptionsValidator();
        validator.Validate(null, new CommercePaymentOptions { Accounts = [Account("stripe", " key", 1, 10)] }).Succeeded.ShouldBeFalse();
        validator.Validate(null, new CommercePaymentOptions { Accounts = [Account("stripe", "same", 1, 10), Account("stripe", "same", 2, 20)] }).Succeeded.ShouldBeFalse();
    }

    [Test]
    public void Paypal_capture_translation_uses_related_checkout_order_id_not_capture_id()
    {
        var payload = """{"id":"WH-1","event_type":"PAYMENT.CAPTURE.COMPLETED","resource":{"id":"CAPTURE-1","amount":{"currency_code":"USD","value":"12.34"},"supplementary_data":{"related_ids":{"order_id":"ORDER-1"}}}}"""u8.ToArray();
        var translated = PayPalPaymentProviderAdapter.TranslateCallback(payload).ShouldBeOfType<Result<VerifiedPaymentCallback, AeroError>.Ok>().Value;
        translated.ProviderReference.ShouldBe("ORDER-1");
        translated.Amount.ShouldBe(12.34m);
    }

    [Test]
    public void Persisted_attempt_shape_has_no_customer_continuation_fields()
    {
        var json = JsonSerializer.Serialize(new PaymentAttemptDocument { Id = 1, ProviderReference = "pi_1", Amount = 10m });
        json.ShouldNotContain("client_secret");
        json.ShouldNotContain("approval_url");
        json.ShouldNotContain("ClientSecret");
        json.ShouldNotContain("ApprovalUrl");
    }

    private static PaymentApplicationService CreateService(AeroDB.Sable.IDocumentSession session, FakeProvider provider, IReadOnlyList<PaymentProviderAccountOptions> accounts) => CreateService(session, [provider], accounts);
    private static PaymentApplicationService CreateService(AeroDB.Sable.IDocumentSession session, IReadOnlyList<FakeProvider> providers, IReadOnlyList<PaymentProviderAccountOptions> accounts) =>
        new(session, CreateRegistry(providers, accounts), new InitiatePaymentRequestValidator());
    private static PaymentProviderRegistry CreateRegistry(IEnumerable<IPaymentProviderAdapter> providers, IReadOnlyList<PaymentProviderAccountOptions> accounts) =>
        new(providers, Options.Create(new CommercePaymentOptions { Accounts = accounts.ToList() }));
    private static PaymentProviderAccountOptions Account(string provider, string key, long tenantId, long siteId) => new()
    {
        Enabled = true, Provider = provider, AccountKey = key, TenantId = tenantId, SiteId = siteId, BaseUrl = "https://payments.example.test",
        SecretKey = "stripe-secret", WebhookSecret = "stripe-webhook", ClientId = "paypal-client", ClientSecret = "paypal-secret", WebhookId = "paypal-webhook"
    };
    private static PaymentReturnUrls StripeReturnUrls() => new(new Uri("https://shop.example.test/orders/success"), new Uri("https://shop.example.test/orders/cancel"));
    private static OrderEntity Order(long id, long tenantId, long siteId, long memberId, decimal total) => new()
    {
        Id = id, TenantId = tenantId, SiteId = siteId, ExternalMemberId = memberId, Currency = "USD", Status = OrderStatus.Submitted, PaymentStatus = OrderPaymentStatus.Unpaid,
        Items = [new OrderItem { ListingId = id, ProductId = id, ProductName = "Display", Sku = $"SKU-{id}", Quantity = 1, UnitPrice = total, Currency = "USD" }]
    };
    private static PaymentAttemptDocument Attempt(long id, long orderId, string reference, decimal amount) => new()
    {
        Id = id, TenantId = 1, SiteId = 10, ExternalMemberId = 7, OrderId = orderId, Provider = "stripe", ProviderAccountKey = "stripe-a", ProviderReference = reference,
        ProviderOperationKey = $"commerce-attempt-{id}", RequestIdempotencyKey = $"request-{id}", Amount = amount, Currency = "USD", Status = PaymentAttemptStatus.RequiresCustomerAction
    };
    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness().WithConfiguration(new CommerceModule().Configure);
        await harness.InitializeAsync();
        return harness;
    }

    private sealed class FakeProvider(string provider) : IPaymentProviderAdapter
    {
        public string Provider => provider;
        public int Initiations { get; private set; }
        public decimal LastAmount { get; private set; }
        public bool RejectCallback { get; init; }
        public VerifiedPaymentCallback? Callback { get; init; }
        public PaymentProviderInitiationOutcome? Outcome { get; init; }

        public Task<PaymentProviderInitiationOutcome> InitiateAsync(PaymentProviderAccount account, PaymentProviderInitiation request, CancellationToken ct = default)
        {
            Initiations++;
            LastAmount = request.Amount;
            return Task.FromResult(Outcome ?? PaymentProviderInitiationOutcome.Succeeded(new(0, $"{Provider}-{request.OperationKey}", PaymentAttemptStatus.RequiresCustomerAction, "client-action", "https://approve.example.test")));
        }

        public Task<Result<PaymentInitiation, AeroError>> RetrieveAsync(PaymentProviderAccount account, string providerReference, CancellationToken ct = default) =>
            Task.FromResult(Prelude.Ok<PaymentInitiation, AeroError>(new(0, providerReference, PaymentAttemptStatus.RequiresCustomerAction, "client-action", "https://approve.example.test")));

        public Task<Result<VerifiedPaymentCallback, AeroError>> VerifyAndTranslateAsync(PaymentProviderAccount account, byte[] rawBody, IHeaderDictionary headers, CancellationToken ct = default) =>
            Task.FromResult(RejectCallback || Callback is null
                ? Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("Invalid callback."))
                : Prelude.Ok<VerifiedPaymentCallback, AeroError>(Callback));
    }
}
