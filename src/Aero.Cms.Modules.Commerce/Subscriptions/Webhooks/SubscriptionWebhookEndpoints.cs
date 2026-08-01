using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Commerce.Subscriptions.Webhooks;

public static class SubscriptionWebhookEndpoints
{
    private const int MaximumWebhookBytes = 1_048_576;

    public static IEndpointRouteBuilder MapSubscriptionWebhookApi(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("/api/commerce/subscriptions/webhooks/{provider}/{accountKey}", async (
            string provider,
            string accountKey,
            HttpRequest request,
            ISubscriptionReconciliationService reconciliation,
            CancellationToken ct) =>
        {
            if (request.ContentLength is > MaximumWebhookBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var raw = await ReadRawBodyAsync(request.Body, MaximumWebhookBytes, ct);
            if (raw is null)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var result = await reconciliation.ReconcileAsync(provider, accountKey, raw, request.Headers, ct);
            return result.IsSuccess ? Results.Ok() : Results.BadRequest(new { title = "Subscription webhook rejected." });
        }).AllowAnonymous().DisableAntiforgery();

        return builder;
    }

    private static async Task<byte[]?> ReadRawBodyAsync(Stream body, int maximumBytes, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        while (true)
        {
            var read = await body.ReadAsync(chunk, ct);
            if (read == 0) return buffer.ToArray();
            if (buffer.Length + read > maximumBytes) return null;
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
    }
}
