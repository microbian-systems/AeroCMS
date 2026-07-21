using Aero.Cms.Abstractions.Authentication;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Commerce.Payments.Api;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentApi(this IEndpointRouteBuilder builder)
    {
        var customer = builder.MapGroup("/api/commerce/payments").RequireAuthorization(ExternalMemberAuthenticationDefaults.Policy, ExternalMemberAuthenticationDefaults.SitePolicy);
        customer.MapPost("/initiate", async (InitiatePaymentRequest request, IPaymentApplicationService payments, ICurrentPrincipal principal, ISiteContext site, CancellationToken ct) =>
        {
            var result = await payments.InitiateAsync(site.TenantId, site.SiteId, principal.PrincipalId ?? 0, request, ct);
            return result is Result<PaymentInitiation, AeroError>.Ok(var value)
                ? Results.Ok(value)
                : Results.BadRequest(new { title = "Payment initiation failed." });
        });
        customer.MapGet("/status/{orderId:long}", async (long orderId, IPaymentApplicationService payments, ICurrentPrincipal principal, ISiteContext site, CancellationToken ct) =>
        { var result = await payments.GetForMemberAsync(site.TenantId, site.SiteId, principal.PrincipalId ?? 0, orderId, ct); return result is Result<PaymentAttemptDocument?, AeroError>.Ok(var attempt) && attempt is not null ? Results.Ok(new { attempt.Id, attempt.Status, attempt.ProviderReference }) : Results.NotFound(); });
        builder.MapPost("/api/commerce/payments/webhooks/{provider}/{accountKey}", async (string provider, string accountKey, HttpRequest request, IPaymentApplicationService payments, CancellationToken ct) =>
        {
            const int maximumWebhookBytes = 1_048_576;
            if (request.ContentLength is > maximumWebhookBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            var raw = await ReadRawBodyAsync(request.Body, maximumWebhookBytes, ct);
            if (raw is null) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            var result = await payments.ReconcileAsync(provider, accountKey, raw, request.Headers, ct);
            return result.IsSuccess ? Results.Ok() : Results.BadRequest(new { title = "Webhook rejected." });
        }).AllowAnonymous().DisableAntiforgery();
        return builder;
    }

    private static async Task<byte[]?> ReadRawBodyAsync(Stream body, int maximumBytes, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await body.ReadAsync(chunk, ct);
            if (read == 0) return buffer.ToArray();
            if (buffer.Length + read > maximumBytes) return null;
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
    }
}
