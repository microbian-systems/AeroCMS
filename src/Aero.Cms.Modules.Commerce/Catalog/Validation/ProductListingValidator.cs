using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Payments;
using FluentValidation;

namespace Aero.Cms.Modules.Commerce.Catalog.Validation;

/// <summary>Validates manager-supplied storefront presentation before trusted scope is applied.</summary>
public sealed class ProductListingValidator : AbstractValidator<ProductListingDocument>
{
    public ProductListingValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Culture).NotEmpty().MaximumLength(32).Matches("^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{2,8})*$");
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(256).Must(CatalogSlug.IsCanonical)
            .WithMessage("Slug must contain only lowercase letters, numbers, and single hyphens.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ShortDescription).MaximumLength(1_000);
        RuleFor(x => x.Description).MaximumLength(10_000);
        RuleFor(x => x.Category).MaximumLength(256);
        RuleFor(x => x.ImageUrl).MaximumLength(2_048).Must(value => string.IsNullOrWhiteSpace(value) || Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out _));
        RuleFor(x => x.Price).Must(PaymentAmountLimits.IsValidUsd).WithMessage("Price must be a positive USD amount with no more than two decimal places.");
        RuleFor(x => x.CompareAtPrice).Must((x, value) => value is null || (PaymentAmountLimits.IsValidUsd(value.Value) && value >= x.Price));
        RuleFor(x => x.Currency).Equal("USD");
    }
}
