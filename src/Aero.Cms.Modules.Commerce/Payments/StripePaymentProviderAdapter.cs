using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Commerce.Payments;

/// <summary>Stripe REST adapter that keeps provider credentials at the transport boundary.</summary>
public sealed class StripePaymentProviderAdapter(IHttpClientFactory httpClientFactory) : IPaymentProviderAdapter
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
            if (!PaymentAmountLimits.IsValidUsd(request.Amount)) return PaymentProviderInitiationOutcome.Terminal("Invalid payment amount.");
            using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(account, "v1/payment_intents"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.SecretKey);
            message.Headers.Add("Idempotency-Key", request.OperationKey);
            message.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["amount"] = ToMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture),
                ["currency"] = request.Currency.ToLowerInvariant(),
                ["automatic_payment_methods[enabled]"] = "true",
                ["metadata[commerce_operation_key]"] = request.OperationKey,
                ["metadata[commerce_order_id]"] = request.OrderId.ToString(CultureInfo.InvariantCulture)
            });

            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
                return IsRetryable(response.StatusCode) ? PaymentProviderInitiationOutcome.Retryable("Stripe initiation outcome is uncertain.") : PaymentProviderInitiationOutcome.Terminal("Stripe payment initiation was rejected.");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;
            if (!TryGetString(root, "id", out var reference) || !TryGetString(root, "status", out var status))
                return PaymentProviderInitiationOutcome.Retryable("Stripe returned an incomplete successful response.");

            TryGetString(root, "client_secret", out var clientSecret);
            return PaymentProviderInitiationOutcome.Succeeded(new(0, reference, MapStatus(status), clientSecret, null));
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
            using var message = new HttpRequestMessage(HttpMethod.Get, BuildUri(account, $"v1/payment_intents/{Uri.EscapeDataString(providerReference)}"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.SecretKey);
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError("Stripe payment continuation could not be loaded."));
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;
            if (!TryGetString(root, "id", out var reference) || !TryGetString(root, "status", out var status)) return Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError("Stripe returned an invalid payment response."));
            TryGetString(root, "client_secret", out var clientSecret);
            return Prelude.Ok<PaymentInitiation, AeroError>(new(0, reference, MapStatus(status), clientSecret, null));
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
            if (!TryGetString(paymentIntent, "id", out var reference) || !TryGetString(paymentIntent, "currency", out var currency) || !paymentIntent.TryGetProperty("amount", out var amountElement) || !amountElement.TryGetInt64(out var minorAmount))
                return Task.FromResult(Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("Invalid Stripe payment payload.")));

            var eventType = TryGetString(root, "type", out var value) ? value : string.Empty;
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

    private static Uri BuildUri(PaymentProviderAccount account, string relativePath) => new(new Uri(account.BaseUrl!, UriKind.Absolute), relativePath);
    private static long ToMinorUnits(decimal amount)
    {
        if (!PaymentAmountLimits.IsValidUsd(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
        return checked(decimal.ToInt64(amount * 100m));
    }
    private static bool IsRetryable(System.Net.HttpStatusCode status) => status == System.Net.HttpStatusCode.RequestTimeout || status == System.Net.HttpStatusCode.Conflict || status == (System.Net.HttpStatusCode)429 || (int)status >= 500;
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
