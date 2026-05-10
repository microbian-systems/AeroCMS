using Aero.Cms.Modules.Commerce.Orders.Domain;
using FluentValidation;

namespace Aero.Cms.Modules.Commerce.Orders.Validation;

public sealed class CreateOrderValidator : AbstractValidator<OrderEntity>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Order must have at least one item");

        RuleForEach(x => x.Items)
            .SetValidator(new OrderItemValidator());
    }
}
