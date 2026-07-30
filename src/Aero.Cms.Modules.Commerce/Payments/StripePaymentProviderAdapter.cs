using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Aero.Cms.Modules.Commerce.Subscriptions.Webhooks;

namespace Aero.Cms.Modules.Commerce.Payments;

/// <summary>Stripe REST adapter that keeps provider credentials at the transport boundary.</summary>
public sealed class StripePaymentProviderAdapter(IHttpClientFactory httpClientFactory) : IPaymentProviderAdapter, ISubscriptionWebhookProviderAdapter
{
    public const string HttpClientName = "Commerce.Stripe";
    public string Provider => "stripe";

    public async Task<PaymentProviderInitiationOutcome> InitiateAsync(
        PaymentProviderAccount account,
        PaymentProviderInitiation request,
        CancellationToken ct = default)
    {
        try
        {
            if (!PaymentAmountLimits.IsValidUsd(request.Amount) || request.ReturnUrls is not { IsHttps: true }) return PaymentProviderInitiationOutcome.Terminal("Invalid hosted Stripe checkout request.");
            using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(account, "v1/checkout/sessions"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.SecretKey);
            message.Headers.Add("Idempotency-Key", request.OperationKey);
            message.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["mode"] = "payment",
                ["success_url"] = request.ReturnUrls.SuccessUrl.ToString(),
                ["cancel_url"] = request.ReturnUrls.CancelUrl.ToString(),
                ["client_reference_id"] = request.OperationKey,
                ["line_items[0][price_data][unit_amount]"] = ToMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture),
                ["line_items[0][price_data][currency]"] = request.Currency.ToLowerInvariant(),
                ["line_items[0][price_data][product_data][name]"] = $"Order #{request.OrderId}",
                ["line_items[0][quantity]"] = "1",
                ["metadata[commerce_operation_key]"] = request.OperationKey,
                ["metadata[commerce_order_id]"] = request.OrderId.ToString(CultureInfo.InvariantCulture),
                ["payment_intent_data[metadata][commerce_operation_key]"] = request.OperationKey,
                ["payment_intent_data[metadata][commerce_order_id]"] = request.OrderId.ToString(CultureInfo.InvariantCulture)
            });

            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
                return IsRetryable(response.StatusCode) ? PaymentProviderInitiationOutcome.Retryable("Stripe initiation outcome is uncertain.") : PaymentProviderInitiationOutcome.Terminal("Stripe payment initiation was rejected.");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;
            if (!TryGetString(root, "id", out var reference) || !TryGetString(root, "url", out var approvalUrl) || !IsHttpsUrl(approvalUrl))
                return PaymentProviderInitiationOutcome.Retryable("Stripe returned an incomplete successful response.");
            return PaymentProviderInitiationOutcome.Succeeded(new(0, reference, PaymentAttemptStatus.RequiresCustomerAction, null, approvalUrl));
        }
        catch (HttpRequestException)
        {
            return PaymentProviderInitiationOutcome.Retryable("Stripe payment initiation could not be confirmed.");
        }
        catch (JsonException)
        {
            return PaymentProviderInitiationOutcome.Retryable("Stripe returned an invalid successful response.");
        }
    }

    public async Task<Result<PaymentInitiation, AeroError>> RetrieveAsync(PaymentProviderAccount account, string providerReference, CancellationToken ct = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, BuildUri(account, $"v1/checkout/sessions/{Uri.EscapeDataString(providerReference)}"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.SecretKey);
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError("Stripe payment continuation could not be loaded."));
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;
            if (!TryGetString(root, "id", out var reference))
                return Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError("Stripe returned an invalid checkout response."));

            var status = TryGetString(root, "status", out var checkoutStatus) ? checkoutStatus : string.Empty;
            var paymentStatus = TryGetString(root, "payment_status", out var checkoutPaymentStatus) ? checkoutPaymentStatus : string.Empty;
            var attemptStatus = status switch
            {
                "expired" => PaymentAttemptStatus.Cancelled,
                "complete" when paymentStatus == "paid" => PaymentAttemptStatus.Succeeded,
                "open" => PaymentAttemptStatus.RequiresCustomerAction,
                _ => PaymentAttemptStatus.ManualReview
            };

            if (attemptStatus == PaymentAttemptStatus.RequiresCustomerAction)
            {
                if (!TryGetString(root, "url", out var approvalUrl) || !IsHttpsUrl(approvalUrl))
                    return Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError("Stripe returned an open checkout session without a secure URL."));
                return Prelude.Ok<PaymentInitiation, AeroError>(new(0, reference, attemptStatus, null, approvalUrl));
            }

            return Prelude.Ok<PaymentInitiation, AeroError>(new(0, reference, attemptStatus, null, null));
        }
        catch (HttpRequestException) { return Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError("Stripe payment continuation could not be loaded.")); }
        catch (JsonException) { return Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError("Stripe returned an invalid payment response.")); }
    }

    public Task<Result<VerifiedPaymentCallback, AeroError>> VerifyAndTranslateAsync(
        PaymentProviderAccount account,
        byte[] rawBody,
        IHeaderDictionary headers,
        CancellationToken ct = default)
    {
        if (!headers.TryGetValue("Stripe-Signature", out var signature) || !StripeSignatureVerifier.IsValid(signature.ToString(), account.WebhookSecret, rawBody, DateTimeOffset.UtcNow))
            return Task.FromResult(Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("Invalid Stripe webhook signature.")));

        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            if (!TryGetString(root, "id", out var eventId) || !root.TryGetProperty("data", out var data) || !data.TryGetProperty("object", out var paymentIntent))
                return Task.FromResult(Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("Invalid Stripe webhook payload.")));
            var eventType = TryGetString(root, "type", out var value) ? value : string.Empty;
            if (eventType is "checkout.session.completed" or "checkout.session.async_payment_succeeded" or "checkout.session.async_payment_failed" or "checkout.session.expired")
            {
                if (!TryGetString(paymentIntent, "id", out var checkoutReference) || !TryGetString(paymentIntent, "currency", out var checkoutCurrency) || !paymentIntent.TryGetProperty("amount_total", out var checkoutAmount) || !checkoutAmount.TryGetInt64(out var checkoutMinor))
                    return Task.FromResult(Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("Invalid Stripe checkout payload.")));
                var paid = TryGetString(paymentIntent, "payment_status", out var paymentStatus) && paymentStatus == "paid";
                var checkoutAttemptStatus = eventType switch
                {
                    "checkout.session.async_payment_succeeded" => PaymentAttemptStatus.Succeeded,
                    "checkout.session.async_payment_failed" => PaymentAttemptStatus.Failed,
                    "checkout.session.expired" => PaymentAttemptStatus.Cancelled,
                    _ when paid => PaymentAttemptStatus.Succeeded,
                    _ => PaymentAttemptStatus.RequiresCustomerAction
                };
                return Task.FromResult(Prelude.Ok<VerifiedPaymentCallback, AeroError>(new(eventId, checkoutReference, checkoutAttemptStatus, checkoutMinor / 100m, checkoutCurrency.ToUpperInvariant(), checkoutAttemptStatus == PaymentAttemptStatus.RequiresCustomerAction ? "Stripe checkout payment is still pending." : null)));
            }
            if (!TryGetString(paymentIntent, "id", out var reference) || !TryGetString(paymentIntent, "currency", out var currency) || !paymentIntent.TryGetProperty("amount", out var amountElement) || !amountElement.TryGetInt64(out var minorAmount))
                return Task.FromResult(Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("Invalid Stripe payment payload.")));
            var status = eventType switch
            {
                "payment_intent.succeeded" => PaymentAttemptStatus.Succeeded,
                "payment_intent.canceled" => PaymentAttemptStatus.Cancelled,
                "payment_intent.payment_failed" => PaymentAttemptStatus.Failed,
                _ => PaymentAttemptStatus.ManualReview
            };
            return Task.FromResult(Prelude.Ok<VerifiedPaymentCallback, AeroError>(new(eventId, reference, status, minorAmount / 100m, currency.ToUpperInvariant(), status == PaymentAttemptStatus.ManualReview ? "Unhandled Stripe event type." : null)));
        }
        catch (JsonException)
        {
            return Task.FromResult(Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("Invalid Stripe webhook payload.")));
        }
    }

    public Task<Result<VerifiedSubscriptionWebhook, AeroError>> VerifyAndTranslateSubscriptionAsync(
        PaymentProviderAccount account,
        byte[] rawBody,
        IHeaderDictionary headers,
        CancellationToken ct = default)
    {
        if (!headers.TryGetValue("Stripe-Signature", out var signature) || !StripeSignatureVerifier.IsValid(signature.ToString(), account.WebhookSecret, rawBody, DateTimeOffset.UtcNow))
            return Task.FromResult(Prelude.Fail<VerifiedSubscriptionWebhook, AeroError>(AeroError.CreateError("Invalid Stripe webhook signature.")));

        try { return Task.FromResult(TranslateSubscriptionWebhook(rawBody)); }
        catch (JsonException) { return Task.FromResult(Prelude.Fail<VerifiedSubscriptionWebhook, AeroError>(AeroError.CreateError("Invalid Stripe subscription webhook payload."))); }
    }

    internal static Result<VerifiedSubscriptionWebhook, AeroError> TranslateSubscriptionWebhook(byte[] rawBody)
    {
        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        if (!TryGetString(root, "id", out var eventId) || !TryGetString(root, "type", out var type)
            || !root.TryGetProperty("created", out var created) || !created.TryGetInt64(out var occurredSeconds)
            || !root.TryGetProperty("data", out var data) || !data.TryGetProperty("object", out var value))
            return SubscriptionFail();

        var occurred = DateTimeOffset.FromUnixTimeSeconds(occurredSeconds);
        return type switch
        {
            "checkout.session.completed" => TranslateCheckout(eventId, occurred, value),
            "customer.subscription.created" => TranslateSubscription(eventId, occurred, SubscriptionWebhookEventKind.SubscriptionActivated, value),
            "customer.subscription.updated" => TranslateSubscription(eventId, occurred, SubscriptionWebhookEventKind.SubscriptionUpdated, value),
            "customer.subscription.paused" => TranslateSubscription(eventId, occurred, SubscriptionWebhookEventKind.SubscriptionSuspended, value),
            "customer.subscription.resumed" => TranslateSubscription(eventId, occurred, SubscriptionWebhookEventKind.SubscriptionReactivated, value),
            "customer.subscription.deleted" => TranslateSubscription(eventId, occurred, SubscriptionWebhookEventKind.SubscriptionCancelled, value),
            "invoice.paid" => TranslateInvoice(eventId, occurred, SubscriptionWebhookEventKind.InvoicePaid, value),
            "invoice.payment_failed" => TranslateInvoice(eventId, occurred, SubscriptionWebhookEventKind.InvoicePaymentFailed, value),
            "invoice.payment_action_required" => TranslateInvoice(eventId, occurred, SubscriptionWebhookEventKind.PaymentActionRequired, value),
            _ => Prelude.Ok<VerifiedSubscriptionWebhook, AeroError>(new(eventId, occurred, SubscriptionWebhookEventKind.Unknown, null, null, null, null, null, null, null, null, null, [], "Unhandled Stripe subscription event."))
        };
    }

    private static Result<VerifiedSubscriptionWebhook, AeroError> TranslateCheckout(string eventId, DateTimeOffset occurred, JsonElement value)
    {
        if (!TryGetString(value, "id", out var checkout) || !TryGetString(value, "subscription", out var subscription)) return SubscriptionFail();
        TryGetString(value, "customer", out var customer);
        TryGetString(value, "currency", out var currency);
        decimal? amount = TryMinorAmount(value, "amount_total", out var total) ? total : null;
        return Prelude.Ok<VerifiedSubscriptionWebhook, AeroError>(new(eventId, occurred, SubscriptionWebhookEventKind.CheckoutCompleted, checkout, subscription, customer, null, null, amount, currency.ToUpperInvariant(), null, null, [], null));
    }

    private static Result<VerifiedSubscriptionWebhook, AeroError> TranslateSubscription(string eventId, DateTimeOffset occurred, SubscriptionWebhookEventKind kind, JsonElement value)
    {
        if (!TryGetString(value, "id", out var subscription)) return SubscriptionFail();
        TryGetString(value, "customer", out var customer);
        var effectiveKind = TryGetString(value, "status", out var status)
            ? status switch
            {
                "active" or "trialing" => kind,
                "incomplete" => SubscriptionWebhookEventKind.Unknown,
                "incomplete_expired" => SubscriptionWebhookEventKind.SubscriptionExpired,
                "past_due" or "unpaid" or "paused" => SubscriptionWebhookEventKind.SubscriptionSuspended,
                "canceled" => SubscriptionWebhookEventKind.SubscriptionCancelled,
                _ => SubscriptionWebhookEventKind.Unknown
            }
            : SubscriptionWebhookEventKind.Unknown;
        if (value.TryGetProperty("pause_collection", out var pause) && pause.ValueKind != JsonValueKind.Null) effectiveKind = SubscriptionWebhookEventKind.SubscriptionSuspended;
        DateTimeOffset? starts = TryUnixTime(value, "current_period_start", out var start) ? start : null;
        DateTimeOffset? ends = TryUnixTime(value, "current_period_end", out var end) ? end : null;
        return Prelude.Ok<VerifiedSubscriptionWebhook, AeroError>(new(eventId, occurred, effectiveKind, null, subscription, customer, null, null, null, null, starts, ends, ReadOfferReferences(value), null));
    }

    private static Result<VerifiedSubscriptionWebhook, AeroError> TranslateInvoice(string eventId, DateTimeOffset occurred, SubscriptionWebhookEventKind kind, JsonElement value)
    {
        if (!TryGetString(value, "id", out var invoice) || !TryGetString(value, "subscription", out var subscription)) return SubscriptionFail();
        TryGetString(value, "customer", out var customer);
        TryGetString(value, "payment_intent", out var payment);
        TryGetString(value, "currency", out var currency);
        var amountField = kind == SubscriptionWebhookEventKind.InvoicePaid ? "amount_paid" : "amount_due";
        decimal? amount = TryMinorAmount(value, amountField, out var paid) ? paid : null;
        var (start, end) = ReadInvoicePeriod(value);
        return Prelude.Ok<VerifiedSubscriptionWebhook, AeroError>(new(eventId, occurred, kind, null, subscription, customer, invoice, payment, amount, currency.ToUpperInvariant(), start, end, ReadOfferReferences(value), null));
    }

    private static IReadOnlyList<string> ReadOfferReferences(JsonElement value)
    {
        var result = new List<string>();
        if (!value.TryGetProperty("items", out var items) || !items.TryGetProperty("data", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            if (!value.TryGetProperty("lines", out var lines) || !lines.TryGetProperty("data", out values) || values.ValueKind != JsonValueKind.Array) return result;
        }
        foreach (var item in values.EnumerateArray())
        {
            if (item.TryGetProperty("price", out var price) && TryGetString(price, "id", out var reference)) result.Add(reference);
        }
        return result;
    }

    private static (DateTimeOffset? Start, DateTimeOffset? End) ReadInvoicePeriod(JsonElement value)
    {
        if (!value.TryGetProperty("lines", out var lines) || !lines.TryGetProperty("data", out var items) || items.ValueKind != JsonValueKind.Array) return (null, null);
        foreach (var item in items.EnumerateArray())
            if (item.TryGetProperty("period", out var period) && TryUnixTime(period, "start", out var start) && TryUnixTime(period, "end", out var end)) return (start, end);
        return (null, null);
    }

    private static bool TryMinorAmount(JsonElement value, string name, out decimal amount)
    {
        amount = 0;
        return value.TryGetProperty(name, out var raw) && raw.TryGetInt64(out var minor) && (amount = minor / 100m) >= 0;
    }
    private static bool TryUnixTime(JsonElement value, string name, out DateTimeOffset time)
    {
        time = default;
        return value.TryGetProperty(name, out var raw) && raw.TryGetInt64(out var seconds) && (time = DateTimeOffset.FromUnixTimeSeconds(seconds)) != default;
    }
    private static Result<VerifiedSubscriptionWebhook, AeroError> SubscriptionFail() => Prelude.Fail<VerifiedSubscriptionWebhook, AeroError>(AeroError.CreateError("Invalid Stripe subscription webhook payload."));

    private static Uri BuildUri(PaymentProviderAccount account, string relativePath)
    {
        var baseUrl = account.BaseUrl!;
        if (!baseUrl.EndsWith("/", StringComparison.Ordinal)) baseUrl += "/";
        return new Uri(new Uri(baseUrl, UriKind.Absolute), relativePath.TrimStart('/'));
    }
    private static long ToMinorUnits(decimal amount)
    {
        if (!PaymentAmountLimits.IsValidUsd(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
        return checked(decimal.ToInt64(amount * 100m));
    }
    private static bool IsRetryable(System.Net.HttpStatusCode status) => status == System.Net.HttpStatusCode.RequestTimeout || status == System.Net.HttpStatusCode.Conflict || status == (System.Net.HttpStatusCode)429 || (int)status >= 500;
    private static bool IsHttpsUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    private static PaymentAttemptStatus MapStatus(string status) => status switch
    {
        "succeeded" => PaymentAttemptStatus.Succeeded,
        "canceled" => PaymentAttemptStatus.Cancelled,
        "requires_payment_method" or "requires_confirmation" or "requires_action" or "processing" => PaymentAttemptStatus.RequiresCustomerAction,
        _ => PaymentAttemptStatus.ManualReview
    };
    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value = property.GetString() ?? string.Empty);
    }
}
