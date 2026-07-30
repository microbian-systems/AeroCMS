using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Aero.Cms.Modules.Commerce.Payments;

namespace Aero.Cms.Modules.Commerce.Subscriptions;

/// <summary>Creates one PayPal Billing Plan subscription and returns only a validated approval link.</summary>
public sealed class PayPalSubscriptionCheckoutProviderAdapter(IHttpClientFactory httpClientFactory) : ISubscriptionCheckoutProviderAdapter
{
    public string Provider => "paypal";

    public async Task<SubscriptionCheckoutOutcome> InitiateAsync(PaymentProviderAccount account, SubscriptionProviderCheckout request, CancellationToken ct = default)
    {
        if (request.Items.Count != 1 || string.IsNullOrWhiteSpace(request.Items[0].PayPalPlanId) || request.Items[0].Quantity <= 0 || !IsHttps(request.SuccessUrl.AbsoluteUri) || !IsHttps(request.CancelUrl.AbsoluteUri)) return SubscriptionCheckoutOutcome.Terminal("PayPal supports one configured subscription plan per checkout.");
        var accessToken = await GetAccessTokenAsync(account, ct);
        if (accessToken is not Result<string, AeroError>.Ok(var token)) return SubscriptionCheckoutOutcome.Retryable();
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(account, "v1/billing/subscriptions"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            message.Headers.Add("PayPal-Request-Id", BuildRequestId(request.OrderId));
            message.Headers.TryAddWithoutValidation("Prefer", "return=representation");
            message.Content = JsonContent(new
            {
                plan_id = request.Items[0].PayPalPlanId,
                quantity = request.Items[0].Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                custom_id = request.OrderId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                application_context = new { return_url = request.SuccessUrl.AbsoluteUri, cancel_url = request.CancelUrl.AbsoluteUri, user_action = "SUBSCRIBE_NOW" }
            });
            using var response = await httpClientFactory.CreateClient(PayPalPaymentProviderAdapter.HttpClientName).SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return IsRetryable(response.StatusCode) ? SubscriptionCheckoutOutcome.Retryable() : SubscriptionCheckoutOutcome.Terminal();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            if (!TryGetString(document.RootElement, "id", out var reference) || !TryFindApprovalUrl(document.RootElement, out var approvalUrl)) return SubscriptionCheckoutOutcome.Retryable("PayPal returned an incomplete subscription response.");
            return SubscriptionCheckoutOutcome.Succeeded(new(0, reference, approvalUrl));
        }
        catch (HttpRequestException) { return SubscriptionCheckoutOutcome.Retryable(); }
        catch (JsonException) { return SubscriptionCheckoutOutcome.Retryable(); }
    }

    public async Task<Result<SubscriptionCheckoutInitiation, AeroError>> RetrieveAsync(PaymentProviderAccount account, string checkoutReference, long subscriptionId, CancellationToken ct = default)
    {
        var accessToken = await GetAccessTokenAsync(account, ct);
        if (accessToken is not Result<string, AeroError>.Ok(var token)) return Fail();
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, BuildUri(account, $"v1/billing/subscriptions/{Uri.EscapeDataString(checkoutReference)}"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await httpClientFactory.CreateClient(PayPalPaymentProviderAdapter.HttpClientName).SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return Fail();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            if (!TryGetString(document.RootElement, "id", out var reference) || !TryFindApprovalUrl(document.RootElement, out var approvalUrl)) return Fail();
            return Prelude.Ok<SubscriptionCheckoutInitiation, AeroError>(new(subscriptionId, reference, approvalUrl));
        }
        catch (HttpRequestException) { return Fail(); }
        catch (JsonException) { return Fail(); }
    }

    private async Task<Result<string, AeroError>> GetAccessTokenAsync(PaymentProviderAccount account, CancellationToken ct)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(account, "v1/oauth2/token"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{account.ClientId}:{account.ClientSecret}")));
            message.Content = new FormUrlEncodedContent([new("grant_type", "client_credentials")]);
            using var response = await httpClientFactory.CreateClient(PayPalPaymentProviderAdapter.HttpClientName).SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return Prelude.Fail<string, AeroError>(AeroError.CreateError("PayPal authorization failed."));
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            return TryGetString(document.RootElement, "access_token", out var token) ? Prelude.Ok<string, AeroError>(token) : Prelude.Fail<string, AeroError>(AeroError.CreateError("PayPal authorization failed."));
        }
        catch (HttpRequestException) { return Prelude.Fail<string, AeroError>(AeroError.CreateError("PayPal authorization failed.")); }
        catch (JsonException) { return Prelude.Fail<string, AeroError>(AeroError.CreateError("PayPal authorization failed.")); }
    }

    private static Result<SubscriptionCheckoutInitiation, AeroError> Fail() => Prelude.Fail<SubscriptionCheckoutInitiation, AeroError>(AeroError.CreateError("PayPal subscription continuation could not be loaded."));
    // PayPal limits this header to 38 bytes. Snowflake order IDs are globally unique and at most
    // 19 decimal digits, so this stable ASCII value is always at most 32 bytes.
    private static string BuildRequestId(long orderId) => $"commerce-sub-{orderId.ToString(CultureInfo.InvariantCulture)}";
    private static Uri BuildUri(PaymentProviderAccount account, string relativePath) => new(new Uri(account.BaseUrl!, UriKind.Absolute), relativePath);
    private static HttpContent JsonContent<T>(T value) => new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
    private static bool IsRetryable(System.Net.HttpStatusCode status) => status == System.Net.HttpStatusCode.RequestTimeout || status == System.Net.HttpStatusCode.Conflict || (int)status == 429 || (int)status >= 500;
    private static bool TryFindApprovalUrl(JsonElement root, out string approvalUrl)
    {
        approvalUrl = string.Empty;
        if (!root.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array) return false;
        foreach (var link in links.EnumerateArray())
            if (TryGetString(link, "rel", out var rel) && string.Equals(rel, "approve", StringComparison.OrdinalIgnoreCase) && TryGetString(link, "href", out var value) && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps) { approvalUrl = value; return true; }
        return false;
    }
    private static bool IsHttps(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    private static bool TryGetString(JsonElement element, string name, out string value) { value = string.Empty; return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value = property.GetString() ?? string.Empty); }
}
