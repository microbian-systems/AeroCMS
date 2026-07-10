using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Commerce.Payments.Api;

/// <summary>
/// Represents a class for PaymentEndpoints.
/// </summary>
public static class PaymentEndpoints
{
        /// <summary>
    /// MapPaymentApi method.
    /// </summary>
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
