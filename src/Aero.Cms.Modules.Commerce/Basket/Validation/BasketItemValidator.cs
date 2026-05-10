using Aero.Cms.Modules.Commerce.Basket.Models;
using FluentValidation;

namespace Aero.Cms.Modules.Commerce.Basket.Validation;

public sealed class BasketItemValidator : AbstractValidator<BasketItem>
{
    public BasketItemValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("Product ID must be greater than 0");

        RuleFor(x => x.ProductName)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Product name is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Unit price must be greater than or equal to 0");
    }
}
