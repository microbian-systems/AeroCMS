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
    }
}
