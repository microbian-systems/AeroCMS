using System.Net.Http.Headers;
using System.Text.Json;
using Aero.Cms.Modules.Commerce.Payments;

namespace Aero.Cms.Modules.Commerce.Subscriptions;

/// <summary>Creates Stripe-hosted subscription sessions from merchant-created Price identifiers.</summary>
public sealed class StripeSubscriptionCheckoutProviderAdapter(IHttpClientFactory httpClientFactory) : ISubscriptionCheckoutProviderAdapter
{
    public string Provider => "stripe";

    public async Task<SubscriptionCheckoutOutcome> InitiateAsync(PaymentProviderAccount account, SubscriptionProviderCheckout request, CancellationToken ct = default)
    {
        if (request.Items.Count is < 1 or > 20 || !IsHttps(request.SuccessUrl.AbsoluteUri) || !IsHttps(request.CancelUrl.AbsoluteUri)) return SubscriptionCheckoutOutcome.Terminal("Invalid Stripe subscription checkout request.");
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(account, "v1/checkout/sessions"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.SecretKey);
            message.Headers.Add("Idempotency-Key", request.OperationKey);
            var form = new Dictionary<string, string>
            {
                ["mode"] = "subscription",
                ["success_url"] = request.SuccessUrl.AbsoluteUri,
                ["cancel_url"] = request.CancelUrl.AbsoluteUri,
                ["client_reference_id"] = request.OrderId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["metadata[commerce_order_id]"] = request.OrderId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["metadata[commerce_operation_key]"] = request.OperationKey
            };
            for (var index = 0; index < request.Items.Count; index++)
            {
                var item = request.Items[index];
                if (string.IsNullOrWhiteSpace(item.StripePriceId) || item.Quantity <= 0) return SubscriptionCheckoutOutcome.Terminal("Stripe subscription offer is invalid.");
                form[$"line_items[{index}][price]"] = item.StripePriceId;
                form[$"line_items[{index}][quantity]"] = item.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            message.Content = new FormUrlEncodedContent(form);
            using var response = await httpClientFactory.CreateClient(StripePaymentProviderAdapter.HttpClientName).SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return IsRetryable(response.StatusCode) ? SubscriptionCheckoutOutcome.Retryable() : SubscriptionCheckoutOutcome.Terminal();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            if (!TryGetString(document.RootElement, "id", out var reference) || !TryGetString(document.RootElement, "url", out var approvalUrl) || !IsHttps(approvalUrl)) return SubscriptionCheckoutOutcome.Retryable("Stripe returned an incomplete checkout response.");
            return SubscriptionCheckoutOutcome.Succeeded(new(0, reference, approvalUrl));
        }
        catch (HttpRequestException) { return SubscriptionCheckoutOutcome.Retryable(); }
        catch (JsonException) { return SubscriptionCheckoutOutcome.Retryable(); }
    }

    public async Task<Result<SubscriptionCheckoutInitiation, AeroError>> RetrieveAsync(PaymentProviderAccount account, string checkoutReference, long subscriptionId, CancellationToken ct = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, BuildUri(account, $"v1/checkout/sessions/{Uri.EscapeDataString(checkoutReference)}"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.SecretKey);
            using var response = await httpClientFactory.CreateClient(StripePaymentProviderAdapter.HttpClientName).SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return Fail();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            if (!TryGetString(document.RootElement, "id", out var reference) || !TryGetString(document.RootElement, "url", out var approvalUrl) || !IsHttps(approvalUrl)) return Fail();
            return Prelude.Ok<SubscriptionCheckoutInitiation, AeroError>(new(subscriptionId, reference, approvalUrl));
        }
        catch (HttpRequestException) { return Fail(); }
        catch (JsonException) { return Fail(); }
    }

    private static Result<SubscriptionCheckoutInitiation, AeroError> Fail() => Prelude.Fail<SubscriptionCheckoutInitiation, AeroError>(AeroError.CreateError("Stripe subscription continuation could not be loaded."));
    private static Uri BuildUri(PaymentProviderAccount account, string relativePath) => new(new Uri(account.BaseUrl!, UriKind.Absolute), relativePath);
    private static bool IsRetryable(System.Net.HttpStatusCode status) => status == System.Net.HttpStatusCode.RequestTimeout || status == System.Net.HttpStatusCode.Conflict || status == (System.Net.HttpStatusCode)429 || (int)status >= 500;
    private static bool IsHttps(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    private static bool TryGetString(JsonElement element, string name, out string value) { value = string.Empty; return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value = property.GetString() ?? string.Empty); }
}
