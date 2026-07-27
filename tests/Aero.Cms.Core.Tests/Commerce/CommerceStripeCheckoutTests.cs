using System.Net;
using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Payments;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceStripeCheckoutTests
{
    [Test]
    public async Task Hosted_checkout_posts_one_authoritative_line_and_preserves_the_account_api_path()
    {
        var handler = new RecordingHandler(_ => Json("""{"id":"cs_123","status":"open","url":"https://checkout.stripe.test/session/cs_123"}"""));
        var adapter = new StripePaymentProviderAdapter(new SingleClientFactory(handler));

        var result = await adapter.InitiateAsync(Account("https://stripe.test/api"), new("commerce-attempt-1", 12.34m, "USD", 44, ReturnUrls()));

        var initiation = result.Initiation!;
        result.Disposition.ShouldBe(PaymentInitiationDisposition.Succeeded);
        initiation.ProviderReference.ShouldBe("cs_123");
        initiation.ClientSecret.ShouldBeNull();
        initiation.ApprovalUrl.ShouldBe("https://checkout.stripe.test/session/cs_123");
        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Path.ShouldBe("/api/v1/checkout/sessions");
        handler.Body.ShouldContain("mode=payment");
        handler.Body.ShouldContain("line_items%5B0%5D%5Bprice_data%5D%5Bunit_amount%5D=1234");
        handler.Body.ShouldContain("line_items%5B0%5D%5Bprice_data%5D%5Bcurrency%5D=usd");
        handler.Body.ShouldContain("metadata%5Bcommerce_operation_key%5D=commerce-attempt-1");
        handler.Body.ShouldNotContain("payment_method_types");
        handler.Body.ShouldNotContain("automatic_payment_methods");
    }

    [Test]
    public async Task Hosted_checkout_retrieve_allows_terminal_sessions_without_urls_but_requires_one_while_open()
    {
        var openHandler = new RecordingHandler(_ => Json("""{"id":"cs_open","status":"open","payment_status":"unpaid","url":"https://checkout.stripe.test/session/cs_open"}"""));
        var open = await new StripePaymentProviderAdapter(new SingleClientFactory(openHandler)).RetrieveAsync(Account(), "cs_open");
        open.ShouldBeOfType<Result<PaymentInitiation, AeroError>.Ok>().Value.Status.ShouldBe(PaymentAttemptStatus.RequiresCustomerAction);
        open.ShouldBeOfType<Result<PaymentInitiation, AeroError>.Ok>().Value.ApprovalUrl.ShouldBe("https://checkout.stripe.test/session/cs_open");

        var paidHandler = new RecordingHandler(_ => Json("""{"id":"cs_paid","status":"complete","payment_status":"paid"}"""));
        var paid = await new StripePaymentProviderAdapter(new SingleClientFactory(paidHandler)).RetrieveAsync(Account(), "cs_paid");
        paid.ShouldBeOfType<Result<PaymentInitiation, AeroError>.Ok>().Value.Status.ShouldBe(PaymentAttemptStatus.Succeeded);
        paid.ShouldBeOfType<Result<PaymentInitiation, AeroError>.Ok>().Value.ApprovalUrl.ShouldBeNull();

        var expiredHandler = new RecordingHandler(_ => Json("""{"id":"cs_expired","status":"expired","payment_status":"unpaid"}"""));
        var expired = await new StripePaymentProviderAdapter(new SingleClientFactory(expiredHandler)).RetrieveAsync(Account(), "cs_expired");
        expired.ShouldBeOfType<Result<PaymentInitiation, AeroError>.Ok>().Value.Status.ShouldBe(PaymentAttemptStatus.Cancelled);
        expired.ShouldBeOfType<Result<PaymentInitiation, AeroError>.Ok>().Value.ApprovalUrl.ShouldBeNull();
    }

    [Test]
    public async Task Hosted_checkout_webhooks_translate_all_session_terminal_states_by_session_id()
    {
        var adapter = new StripePaymentProviderAdapter(new SingleClientFactory(new RecordingHandler(_ => Json("{}"))));
        foreach (var (type, expected) in new[]
        {
            ("checkout.session.completed", PaymentAttemptStatus.Succeeded),
            ("checkout.session.async_payment_succeeded", PaymentAttemptStatus.Succeeded),
            ("checkout.session.async_payment_failed", PaymentAttemptStatus.Failed),
            ("checkout.session.expired", PaymentAttemptStatus.Cancelled)
        })
        {
            var raw = Encoding.UTF8.GetBytes($"{{\"id\":\"evt_{expected}\",\"type\":\"{type}\",\"data\":{{\"object\":{{\"id\":\"cs_{expected}\",\"currency\":\"usd\",\"amount_total\":1234,\"payment_status\":\"paid\"}}}}}}");
            var headers = new HeaderDictionary { ["Stripe-Signature"] = Signature("hook", raw) };

            var result = await adapter.VerifyAndTranslateAsync(Account(), raw, headers);

            var callback = result.ShouldBeOfType<Result<VerifiedPaymentCallback, AeroError>.Ok>().Value;
            callback.ProviderReference.ShouldBe($"cs_{expected}");
            callback.Status.ShouldBe(expected);
            callback.Amount.ShouldBe(12.34m);
        }
    }

    [Test]
    public async Task Hosted_checkout_replay_retrieves_the_bound_session_without_persisting_or_requiring_return_urls()
    {
        await using var harness = new SableTestHarness().WithConfiguration(new CommerceModule().Configure);
        await harness.InitializeAsync();
        harness.Session.Store(new OrderEntity
        {
            Id = 114, TenantId = 1, SiteId = 10, ExternalMemberId = 7, Currency = "USD", Status = OrderStatus.Submitted, PaymentStatus = OrderPaymentStatus.Unpaid,
            Items = [new OrderItem { ListingId = 114, ProductId = 114, ProductName = "Display", Sku = "SKU-114", Quantity = 1, UnitPrice = 12.34m, Currency = "USD" }]
        });
        await harness.Session.SaveChangesAsync();
        var handler = new SequenceHandler(
            """{"id":"cs_replay","status":"open","url":"https://checkout.stripe.test/session/cs_replay"}""",
            """{"id":"cs_replay","status":"open","payment_status":"unpaid","url":"https://checkout.stripe.test/session/cs_replay"}""");
        var adapter = new StripePaymentProviderAdapter(new SingleClientFactory(handler));
        var service = new PaymentApplicationService(harness.Session,
            new PaymentProviderRegistry([adapter], Options.Create(new CommercePaymentOptions { Accounts = [AccountOptions()] })),
            new InitiatePaymentRequestValidator());

        var first = await service.InitiateAsync(1, 10, 7, new InitiatePaymentRequest(114, "stripe", "replay-key"), returnUrls: ReturnUrls());
        var replay = await service.InitiateAsync(1, 10, 7, new InitiatePaymentRequest(114, "stripe", "replay-key"));

        first.ShouldBeOfType<Result<PaymentInitiation, AeroError>.Ok>().Value.ProviderReference.ShouldBe("cs_replay");
        replay.ShouldBeOfType<Result<PaymentInitiation, AeroError>.Ok>().Value.ProviderReference.ShouldBe("cs_replay");
        handler.Methods.ShouldBe([HttpMethod.Post, HttpMethod.Get]);
        await using var verify = await harness.OpenSessionAsync();
        var attempt = (await verify.Query<PaymentAttemptDocument>().FirstOrDefaultAsync(x => x.OrderId == 114))!;
        attempt.ProviderReference.ShouldBe("cs_replay");
    }

    private static PaymentProviderAccount Account(string baseUrl = "https://stripe.test/") => new("stripe", "stripe-store", 1, 10, "secret", "hook", null, null, null, baseUrl, []);
    private static PaymentProviderAccountOptions AccountOptions() => new() { Enabled = true, Provider = "stripe", AccountKey = "stripe-store", TenantId = 1, SiteId = 10, BaseUrl = "https://stripe.test/api", SecretKey = "secret", WebhookSecret = "hook" };
    private static PaymentReturnUrls ReturnUrls() => new(new Uri("https://shop.test/shop/orders/114?payment=success"), new Uri("https://shop.test/shop/orders/114?payment=cancel"));
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private static string Signature(string secret, byte[] raw)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var prefix = Encoding.ASCII.GetBytes($"{timestamp}.");
        var data = new byte[prefix.Length + raw.Length];
        Buffer.BlockCopy(prefix, 0, data, 0, prefix.Length); Buffer.BlockCopy(raw, 0, data, prefix.Length, raw.Length);
        return $"t={timestamp},v1={Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), data)).ToLowerInvariant()}";
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string Body { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return response(request);
        }
    }

    private sealed class SequenceHandler(params string[] responses) : HttpMessageHandler
    {
        public List<HttpMethod> Methods { get; } = [];
        private int call;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            return Task.FromResult(Json(responses[call++]));
        }
    }
}
