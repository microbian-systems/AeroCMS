using Aero.Cms.Modules.Commerce.Catalog.Models;
using FluentValidation;

namespace Aero.Cms.Modules.Commerce.Catalog.Validation;

/// <summary>
/// Represents a class for ProductValidator.
/// </summary>
public sealed class ProductValidator : AbstractValidator<ProductDocument>
{
        /// <summary>
    /// Initializes a new instance of the <see cref="ProductValidator"/> class.
    /// </summary>
public ProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Product name is required and must be at most 500 characters");

        RuleFor(x => x.Sku).NotEmpty().MaximumLength(128).Matches("^[A-Za-z0-9._-]+$");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Stock quantity must be greater than or equal to 0");
    }
}
