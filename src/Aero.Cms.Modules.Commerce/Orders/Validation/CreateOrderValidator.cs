using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Payments;
using FluentValidation;

namespace Aero.Cms.Modules.Commerce.Orders.Validation;

/// <summary>
/// Represents a class for CreateOrderValidator.
/// </summary>
public sealed class CreateOrderValidator : AbstractValidator<OrderEntity>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="CreateOrderValidator"/> class.
    /// </summary>
public CreateOrderValidator()
    {
        RuleFor(x => x.ExternalMemberId).GreaterThan(0);

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Order must have at least one item");

        RuleForEach(x => x.Items)
            .SetValidator(new OrderItemValidator());

        RuleFor(x => x.TotalAmount).Must(PaymentAmountLimits.IsValidUsd)
            .WithMessage("Order total must be a positive USD amount with no more than two decimal places.");
        RuleFor(x => x.BillingIntervalDays).InclusiveBetween(1, 365).When(x => x.BillingKind == OrderBillingKind.Recurring);
        RuleFor(x => x.BillingIntervalDays).Null().When(x => x.BillingKind == OrderBillingKind.OneTime);
        RuleFor(x => x).Must(HasConsistentBilling)
            .WithMessage("Order billing kind, interval, and line provider bindings must be consistent.");
    }

    private static bool HasConsistentBilling(OrderEntity order)
    {
        if (order.BillingKind == OrderBillingKind.OneTime)
            return order.Items.All(item => item.BillingKind == OrderBillingKind.OneTime && item.BillingIntervalDays is null && string.IsNullOrWhiteSpace(item.StripePriceId) && string.IsNullOrWhiteSpace(item.PayPalPlanId));
        return order.BillingIntervalDays is >= 1 and <= 365 && order.Items.All(item =>
            item.BillingKind == OrderBillingKind.Recurring && item.FulfillmentMode == Aero.Cms.Modules.Commerce.Catalog.Models.ProductFulfillmentMode.NonInventoryRecurring &&
            item.BillingIntervalDays == order.BillingIntervalDays && (!string.IsNullOrWhiteSpace(item.StripePriceId) || !string.IsNullOrWhiteSpace(item.PayPalPlanId)));
    }
}
