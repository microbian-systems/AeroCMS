using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Payments;
using FluentValidation;

namespace Aero.Cms.Modules.Commerce.Orders.Validation;

/// <summary>
/// Represents a class for OrderItemValidator.
/// </summary>
public sealed class OrderItemValidator : AbstractValidator<OrderItem>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="OrderItemValidator"/> class.
    /// </summary>
public OrderItemValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("Product ID must be greater than 0");

        RuleFor(x => x.ProductName)
            .NotEmpty()
            .WithMessage("Product name is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0");

        RuleFor(x => x.UnitPrice).Must(PaymentAmountLimits.IsValidUsd)
            .WithMessage("Unit price must be a positive USD amount with no more than two decimal places");
    }
}
