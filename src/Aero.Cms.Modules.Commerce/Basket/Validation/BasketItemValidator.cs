using Aero.Cms.Modules.Commerce.Basket.Models;
using FluentValidation;

namespace Aero.Cms.Modules.Commerce.Basket.Validation;

/// <summary>
/// Represents a class for BasketItemValidator.
/// </summary>
public sealed class BasketItemValidator : AbstractValidator<BasketItem>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="BasketItemValidator"/> class.
    /// </summary>
public BasketItemValidator()
    {
        RuleFor(x => x.ListingId)
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
