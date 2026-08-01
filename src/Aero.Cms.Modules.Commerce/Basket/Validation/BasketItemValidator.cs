using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Payments;
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
            .WithMessage("Listing ID must be greater than 0");

        RuleFor(x => x.ProductId).GreaterThan(0);

        RuleFor(x => x.ProductName)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Product name is required");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0");

        RuleFor(x => x.Sku).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Currency).Equal("USD");
        RuleFor(x => x.UnitPrice).Must(PaymentAmountLimits.IsValidUsd)
            .WithMessage("Unit price must be a positive USD amount with no more than two decimal places.");
        RuleFor(x => x.BillingKind).IsInEnum();
        RuleFor(x => x.BillingIntervalDays).Null().When(x => x.BillingKind == BasketBillingKind.OneTime);
        RuleFor(x => x.SubscriptionOffer).Null().When(x => x.BillingKind == BasketBillingKind.OneTime);
        RuleFor(x => x.BillingIntervalDays).InclusiveBetween(1, 365)
            .When(x => x.BillingKind == BasketBillingKind.Recurring);
        RuleFor(x => x.SubscriptionOffer).NotNull()
            .When(x => x.BillingKind == BasketBillingKind.Recurring);
        When(x => x.SubscriptionOffer is not null, () =>
        {
            RuleFor(x => x.SubscriptionOffer!.IntervalDays).InclusiveBetween(1, 365);
            RuleFor(x => x.SubscriptionOffer!.StripePriceId).MaximumLength(256);
            RuleFor(x => x.SubscriptionOffer!.PayPalPlanId).MaximumLength(256);
            RuleFor(x => x.SubscriptionOffer!).Must(HasProviderBinding)
                .WithMessage("A recurring basket item needs a Stripe price or PayPal plan binding.");
            RuleFor(x => x).Must(HasMatchingInterval)
                .When(x => x.BillingKind == BasketBillingKind.Recurring && x.SubscriptionOffer is not null)
                .WithMessage("Recurring basket item interval must match its provider offer.");
        });
    }

    private static bool HasProviderBinding(BasketSubscriptionOfferSnapshot offer)
        => !string.IsNullOrWhiteSpace(offer.StripePriceId) || !string.IsNullOrWhiteSpace(offer.PayPalPlanId);

    private static bool HasMatchingInterval(BasketItem item)
        => item.BillingIntervalDays == item.SubscriptionOffer!.IntervalDays;
}

/// <summary>Prevents mixed one-time and recurring checkout intents in one scoped basket.</summary>
public sealed class BasketDocumentValidator : AbstractValidator<BasketDocument>
{
    public BasketDocumentValidator()
    {
        RuleFor(x => x.TenantId).GreaterThan(0);
        RuleFor(x => x.SiteId).GreaterThan(0);
        RuleFor(x => x.ExternalMemberId).GreaterThan(0);
        RuleFor(x => x.Currency).Equal("USD");
        RuleForEach(x => x.Items).SetValidator(new BasketItemValidator());
        RuleFor(x => x.Items).Must(HaveCompatibleBilling)
            .WithMessage("A basket cannot mix one-time and recurring items or recurring intervals.");
    }

    private static bool HaveCompatibleBilling(IReadOnlyCollection<BasketItem> items)
    {
        var first = items.FirstOrDefault();
        return first is null || items.All(item =>
            item.BillingKind == first.BillingKind &&
            (item.BillingKind != BasketBillingKind.Recurring || item.BillingIntervalDays == first.BillingIntervalDays));
    }
}
