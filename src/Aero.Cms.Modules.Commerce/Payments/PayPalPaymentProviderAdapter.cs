using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Commerce.Payments;

/// <summary>PayPal REST adapter with provider-side idempotency and signature verification.</summary>
public sealed class PayPalPaymentProviderAdapter(IHttpClientFactory httpClientFactory) : IPaymentProviderAdapter
{
    public const string HttpClientName = "Commerce.PayPal";
    public string Provider => "paypal";

    public async Task<PaymentProviderInitiationOutcome> InitiateAsync(
        PaymentProviderAccount account,
        PaymentProviderInitiation request,
        CancellationToken ct = default)
    {
        var accessToken = await GetAccessTokenAsync(account, ct);
        if (!PaymentAmountLimits.IsValidUsd(request.Amount)) return PaymentProviderInitiationOutcome.Terminal("Invalid payment amount.");
        if (accessToken is not Result<string, AeroError>.Ok(var token)) return PaymentProviderInitiationOutcome.Retryable("PayPal payment initiation could not be confirmed.");

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(account, "v2/checkout/orders"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            message.Headers.Add("PayPal-Request-Id", request.OperationKey);
            message.Content = JsonContent(new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        reference_id = request.OperationKey,
                        custom_id = request.OrderId.ToString(CultureInfo.InvariantCulture),
                        amount = new { currency_code = request.Currency, value = request.Amount.ToString("0.00", CultureInfo.InvariantCulture) }
                    }
                }
            });

            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return IsRetryable(response.StatusCode) ? PaymentProviderInitiationOutcome.Retryable("PayPal initiation outcome is uncertain.") : PaymentProviderInitiationOutcome.Terminal("PayPal payment initiation was rejected.");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;
            if (!TryGetString(root, "id", out var reference) || !TryGetString(root, "status", out var status))
                return PaymentProviderInitiationOutcome.Retryable("PayPal returned an incomplete successful response.");

            var approvalUrl = FindApprovalUrl(root);
            return PaymentProviderInitiationOutcome.Succeeded(new(0, reference, MapStatus(status), null, approvalUrl));
        }
        catch (HttpRequestException)
        {
            return PaymentProviderInitiationOutcome.Retryable("PayPal payment initiation could not be confirmed.");
        }
        catch (JsonException)
        {
            return PaymentProviderInitiationOutcome.Retryable("PayPal returned an invalid successful response.");
        }
    }

    public async Task<Result<PaymentInitiation, AeroError>> RetrieveAsync(PaymentProviderAccount account, string providerReference, CancellationToken ct = default)
    {
        var accessToken = await GetAccessTokenAsync(account, ct);
        if (accessToken is not Result<string, AeroError>.Ok(var token)) return Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError("PayPal payment continuation could not be loaded."));
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, BuildUri(account, $"v2/checkout/orders/{Uri.EscapeDataString(providerReference)}"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError("PayPal payment continuation could not be loaded."));
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;
            if (!TryGetString(root, "id", out var reference) || !TryGetString(root, "status", out var status)) return Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError("PayPal returned an invalid payment response."));
            return Prelude.Ok<PaymentInitiation, AeroError>(new(0, reference, MapStatus(status), null, FindApprovalUrl(root)));
        }
        catch (HttpRequestException) { return Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError("PayPal payment continuation could not be loaded.")); }
        catch (JsonException) { return Prelude.Fail<PaymentInitiation, AeroError>(AeroError.CreateError("PayPal returned an invalid payment response.")); }
    }

    public async Task<Result<VerifiedPaymentCallback, AeroError>> VerifyAndTranslateAsync(
        PaymentProviderAccount account,
        byte[] rawBody,
        IHeaderDictionary headers,
        CancellationToken ct = default)
    {
        if (!TryReadRequiredHeaders(headers, out var transmissionId, out var transmissionTime, out var certUrl, out var authAlgo, out var transmissionSignature))
            return Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("Invalid PayPal webhook signature headers."));

        var accessToken = await GetAccessTokenAsync(account, ct);
        if (accessToken is not Result<string, AeroError>.Ok(var token)) return Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("PayPal webhook could not be verified."));

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(account, "v1/notifications/verify-webhook-signature"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            message.Content = BuildVerificationContent(account, rawBody, transmissionId, transmissionTime, certUrl, authAlgo, transmissionSignature);
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("PayPal webhook could not be verified."));

            await using var verificationStream = await response.Content.ReadAsStreamAsync(ct);
            using var verification = await JsonDocument.ParseAsync(verificationStream, cancellationToken: ct);
            if (!TryGetString(verification.RootElement, "verification_status", out var verificationStatus) || !string.Equals(verificationStatus, "SUCCESS", StringComparison.Ordinal))
                return Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("PayPal webhook signature was not verified."));

            return TranslateCallback(rawBody);
        }
        catch (HttpRequestException)
        {
            return Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("PayPal webhook could not be verified."));
        }
        catch (JsonException)
        {
            return Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("Invalid PayPal webhook payload."));
        }
    }

    private async Task<Result<string, AeroError>> GetAccessTokenAsync(PaymentProviderAccount account, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(account, "v1/oauth2/token"));
            var credential = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{account.ClientId}:{account.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credential);
            request.Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")]);
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return Prelude.Fail<string, AeroError>(AeroError.CreateError("PayPal authentication was rejected."));
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return TryGetString(document.RootElement, "access_token", out var token)
                ? Prelude.Ok<string, AeroError>(token)
                : Prelude.Fail<string, AeroError>(AeroError.CreateError("PayPal returned an invalid authentication response."));
        }
        catch (HttpRequestException)
        {
            return Prelude.Fail<string, AeroError>(AeroError.CreateError("PayPal authentication could not be confirmed."));
        }
        catch (JsonException)
        {
            return Prelude.Fail<string, AeroError>(AeroError.CreateError("PayPal returned an invalid authentication response."));
        }
    }

    internal static Result<VerifiedPaymentCallback, AeroError> TranslateCallback(byte[] rawBody)
    {
        using var callback = JsonDocument.Parse(rawBody);
        var root = callback.RootElement;
        if (!TryGetString(root, "id", out var eventId) || !TryGetString(root, "event_type", out var eventType) || !root.TryGetProperty("resource", out var resource))
            return Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("Invalid PayPal webhook payload."));
        var isCapture = eventType.StartsWith("PAYMENT.CAPTURE.", StringComparison.Ordinal);
        string reference;
        if (isCapture)
        {
            if (!resource.TryGetProperty("supplementary_data", out var supplementary) || !supplementary.TryGetProperty("related_ids", out var related) || !TryGetString(related, "order_id", out reference))
                return Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("PayPal capture is missing its checkout order reference."));
        }
        else if (!TryGetString(resource, "id", out reference))
        {
            return Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("Invalid PayPal webhook payload."));
        }
        if (!TryReadAmount(resource, out var amount, out var currency))
            return Prelude.Fail<VerifiedPaymentCallback, AeroError>(AeroError.CreateError("Invalid PayPal payment amount."));

        var status = eventType switch
        {
            "PAYMENT.CAPTURE.COMPLETED" => PaymentAttemptStatus.Succeeded,
            "PAYMENT.CAPTURE.DENIED" or "PAYMENT.CAPTURE.DECLINED" => PaymentAttemptStatus.Failed,
            "PAYMENT.CAPTURE.REVERSED" or "CHECKOUT.ORDER.VOIDED" => PaymentAttemptStatus.Cancelled,
            _ => PaymentAttemptStatus.ManualReview
        };
        return Prelude.Ok<VerifiedPaymentCallback, AeroError>(new(eventId, reference, status, amount, currency, status == PaymentAttemptStatus.ManualReview ? "Unhandled PayPal event type." : null));
    }

    private static HttpContent BuildVerificationContent(PaymentProviderAccount account, byte[] rawBody, string transmissionId, string transmissionTime, string certUrl, string authAlgo, string transmissionSignature)
    {
        var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("auth_algo", authAlgo);
            writer.WriteString("cert_url", certUrl);
            writer.WriteString("transmission_id", transmissionId);
            writer.WriteString("transmission_sig", transmissionSignature);
            writer.WriteString("transmission_time", transmissionTime);
            writer.WriteString("webhook_id", account.WebhookId);
            writer.WritePropertyName("webhook_event");
            writer.WriteRawValue(rawBody, skipInputValidation: false);
            writer.WriteEndObject();
        }
        return new ByteArrayContent(stream.ToArray()) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } };
    }

    private static bool TryReadRequiredHeaders(IHeaderDictionary headers, out string transmissionId, out string transmissionTime, out string certUrl, out string authAlgo, out string transmissionSignature)
    {
        transmissionId = Header("Paypal-Transmission-Id");
        transmissionTime = Header("Paypal-Transmission-Time");
        certUrl = Header("Paypal-Cert-Url");
        authAlgo = Header("Paypal-Auth-Algo");
        transmissionSignature = Header("Paypal-Transmission-Sig");
        return !string.IsNullOrWhiteSpace(transmissionId) && !string.IsNullOrWhiteSpace(transmissionTime) && !string.IsNullOrWhiteSpace(certUrl) && !string.IsNullOrWhiteSpace(authAlgo) && !string.IsNullOrWhiteSpace(transmissionSignature);

        string Header(string key) => headers.TryGetValue(key, out var value) ? value.ToString() : string.Empty;
    }

    private static bool TryReadAmount(JsonElement resource, out decimal amount, out string currency)
    {
        amount = 0;
        currency = string.Empty;
        if (!resource.TryGetProperty("amount", out var amountElement) || !TryGetString(amountElement, "value", out var value) || !TryGetString(amountElement, "currency_code", out currency)) return false;
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount) && amount >= 0;
    }

    private static Uri BuildUri(PaymentProviderAccount account, string relativePath) => new(new Uri(account.BaseUrl!, UriKind.Absolute), relativePath);
    private static bool IsRetryable(System.Net.HttpStatusCode status) => status == System.Net.HttpStatusCode.RequestTimeout || status == System.Net.HttpStatusCode.Conflict || status == (System.Net.HttpStatusCode)429 || (int)status >= 500;
    private static HttpContent JsonContent<T>(T value) => new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
    private static PaymentAttemptStatus MapStatus(string status) => status switch
    {
        "COMPLETED" => PaymentAttemptStatus.Succeeded,
        "VOIDED" => PaymentAttemptStatus.Cancelled,
        _ => PaymentAttemptStatus.RequiresCustomerAction
    };
    private static string? FindApprovalUrl(JsonElement root)
    {
        if (!root.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array) return null;
        foreach (var link in links.EnumerateArray())
            if (TryGetString(link, "rel", out var rel) && string.Equals(rel, "approve", StringComparison.OrdinalIgnoreCase) && TryGetString(link, "href", out var href)) return href;
        return null;
    }
    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value = property.GetString() ?? string.Empty);
    }
}
