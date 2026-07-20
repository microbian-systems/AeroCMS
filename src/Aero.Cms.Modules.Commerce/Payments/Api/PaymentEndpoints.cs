using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Commerce.Payments.Api;

/// <summary>
/// Registers the authenticated, informational payment-status endpoint.
/// </summary>
public static class PaymentEndpoints
{
    /// <summary>
    /// Maps the <c>/api/commerce/payments</c> route group.
    /// </summary>
    /// <param name="builder">The route builder to which the authenticated route is added.</param>
    /// <returns>The supplied <paramref name="builder"/>.</returns>
    /// <remarks>
    /// GET <c>/status/{orderId}</c> always returns an informational 200 response and does not load an order, verify
    /// its payment state, or check customer ownership. Payment capture, refunds, and provider interaction are not
    /// exposed by this endpoint.
    /// </remarks>
    public static IEndpointRouteBuilder MapPaymentApi(this IEndpointRouteBuilder builder)
    {
        var group = builder
            .MapGroup("/api/commerce/payments")
            .RequireAuthorization();

        // Payment status is tracked on the Order entity itself.
        // This endpoint provides a basic payment status lookup.
        group.MapGet("/status/{orderId:long}", async (
            long orderId) =>
        {
            // Payment status is available via GET /api/commerce/orders/{id}
            return Results.Ok(new { OrderId = orderId, Message = "See order status for payment details" });
        });

        return builder;
    }
}
