using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Payments;
using FluentValidation;

namespace Aero.Cms.Modules.Commerce.Subscriptions;

/// <summary>Validates a provider-neutral recurring offer saved with a storefront listing.</summary>
public sealed class SubscriptionOfferValidator : AbstractValidator<SubscriptionOffer>
{
    public SubscriptionOfferValidator()
    {
        RuleFor(x => x.IntervalDays).InclusiveBetween(1, 365);
        RuleFor(x => x.StripePriceId).MaximumLength(256);
        RuleFor(x => x.PayPalPlanId).MaximumLength(256);
        RuleFor(x => x.StripePriceId)
            .Matches("^price_[A-Za-z0-9_]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.StripePriceId))
            .WithMessage("Stripe price identifiers must use the price_ format.");
        RuleFor(x => x.PayPalPlanId)
            .Matches("^P-[A-Z0-9-]+$")
            .When(x => !string.IsNullOrWhiteSpace(x.PayPalPlanId))
            .WithMessage("PayPal plan identifiers must use the P- format.");
    }
}

/// <summary>Validates one immutable commercial line snapshot.</summary>
public sealed class SubscriptionLineSnapshotValidator : AbstractValidator<SubscriptionLineSnapshot>
{
    public SubscriptionLineSnapshotValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.ListingId).GreaterThan(0);
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ListingName).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 999_999);
        RuleFor(x => x.UnitAmount)
            .Must(PaymentAmountLimits.IsValidUsd)
            .WithMessage("Unit amount must be a positive USD amount with no more than two decimal places.");
        RuleFor(x => x.ProviderOfferReference).NotEmpty().MaximumLength(256);
    }
}

/// <summary>Validates safe, provider-owned subscription snapshots before persistence.</summary>
public sealed class SubscriptionDocumentValidator : AbstractValidator<SubscriptionDocument>
{
    public SubscriptionDocumentValidator()
    {
        RuleFor(x => x.TenantId).GreaterThan(0);
        RuleFor(x => x.SiteId).GreaterThan(0);
        RuleFor(x => x.ExternalMemberId).GreaterThan(0);
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.Provider).Must(SubscriptionProviderRules.IsSupported)
            .WithMessage("Provider must be stripe or paypal.");
        RuleFor(x => x.ProviderAccountKey).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ProviderOperationKey).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ProviderCheckoutReference).MaximumLength(256);
        RuleFor(x => x.ProviderCheckoutReference).NotEmpty()
            .When(x => x.State != SubscriptionState.PendingProviderConfirmation);
        RuleFor(x => x.ProviderSubscriptionReference).MaximumLength(256);
        RuleFor(x => x.ProviderCustomerReference).MaximumLength(256);
        RuleFor(x => x.ProviderSubscriptionReference)
            .NotEmpty()
            .When(x => x.State != SubscriptionState.PendingProviderConfirmation);
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= 20)
            .WithMessage("A subscription must contain between 1 and 20 lines.");
        RuleForEach(x => x.Lines).SetValidator(new SubscriptionLineSnapshotValidator());
        RuleFor(x => x.Currency).Equal("USD");
        RuleFor(x => x.IntervalDays).InclusiveBetween(1, 365);
        RuleFor(x => x.State).IsInEnum();
        RuleFor(x => x.LastAppliedProviderEventId).MaximumLength(256);
        RuleFor(x => x.CurrentPeriodEndsOn)
            .GreaterThan(x => x.CurrentPeriodStartsOn)
            .When(x => x.CurrentPeriodStartsOn.HasValue && x.CurrentPeriodEndsOn.HasValue);
        RuleFor(x => x.ManualReviewReason).NotEmpty().MaximumLength(2_000)
            .When(x => x.RequiresManualReview);
        RuleFor(x => x.ManualReviewRequestedOn).NotNull()
            .When(x => x.RequiresManualReview);
        RuleFor(x => x).Must(HasPayPalCompatibleLines)
            .WithMessage("PayPal subscriptions support one listing per checkout.");
    }

    private static bool HasPayPalCompatibleLines(SubscriptionDocument document)
        => document.Provider != "paypal" || document.Lines.Count == 1;
}

/// <summary>Validates a durable cycle snapshot without retaining credentials or browser state.</summary>
public sealed class SubscriptionCycleDocumentValidator : AbstractValidator<SubscriptionCycleDocument>
{
    public SubscriptionCycleDocumentValidator()
    {
        RuleFor(x => x.TenantId).GreaterThan(0);
        RuleFor(x => x.SiteId).GreaterThan(0);
        RuleFor(x => x.ExternalMemberId).GreaterThan(0);
        RuleFor(x => x.SubscriptionId).GreaterThan(0);
        RuleFor(x => x.CycleNumber).GreaterThan(0);
        RuleFor(x => x.Provider).Must(SubscriptionProviderRules.IsSupported)
            .WithMessage("Provider must be stripe or paypal.");
        RuleFor(x => x.ProviderAccountKey).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ProviderCycleReference).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ProviderPaymentReference).MaximumLength(256);
        RuleFor(x => x.PaymentAttemptId).GreaterThan(0).When(x => x.PaymentAttemptId.HasValue);
        RuleFor(x => x.Lines).NotEmpty().Must(lines => lines.Count <= 20);
        RuleForEach(x => x.Lines).SetValidator(new SubscriptionLineSnapshotValidator());
        RuleFor(x => x.AmountSnapshot)
            .Must(PaymentAmountLimits.IsValidUsd)
            .WithMessage("Cycle amount must be a positive USD amount with no more than two decimal places.");
        RuleFor(x => x.Currency).Equal("USD");
        RuleFor(x => x.PeriodEndsOn).GreaterThan(x => x.PeriodStartsOn);
        RuleFor(x => x.State).IsInEnum();
        RuleFor(x => x.LastAppliedProviderEventId).MaximumLength(256);
        RuleFor(x => x.ManualReviewReason).NotEmpty().MaximumLength(2_000)
            .When(x => x.RequiresManualReview);
        RuleFor(x => x.ManualReviewRequestedOn).NotNull()
            .When(x => x.RequiresManualReview);
    }
}

/// <summary>Validates only safe subscription webhook receipt metadata.</summary>
public sealed class SubscriptionWebhookReceiptDocumentValidator : AbstractValidator<SubscriptionWebhookReceiptDocument>
{
    public SubscriptionWebhookReceiptDocumentValidator()
    {
        RuleFor(x => x.TenantId).GreaterThan(0);
        RuleFor(x => x.SiteId).GreaterThan(0);
        RuleFor(x => x.ExternalMemberId).GreaterThan(0).When(x => x.ExternalMemberId.HasValue);
        RuleFor(x => x.SubscriptionId).GreaterThan(0).When(x => x.SubscriptionId.HasValue);
        RuleFor(x => x.SubscriptionCycleId).GreaterThan(0).When(x => x.SubscriptionCycleId.HasValue);
        RuleFor(x => x.Provider).Must(SubscriptionProviderRules.IsSupported)
            .WithMessage("Provider must be stripe or paypal.");
        RuleFor(x => x.ProviderAccountKey).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ProviderEventId).NotEmpty().MaximumLength(256);
        RuleFor(x => x.ProviderSubscriptionReference).MaximumLength(256);
        RuleFor(x => x.ProviderCycleReference).MaximumLength(256);
        RuleFor(x => x.ProviderPaymentReference).MaximumLength(256);
        RuleFor(x => x.ProviderOccurredOn).NotEqual(default(DateTimeOffset));
        RuleFor(x => x.State).IsInEnum();
        RuleFor(x => x.ManualReviewReason).NotEmpty().MaximumLength(2_000)
            .When(x => x.State == SubscriptionWebhookReceiptState.ManualReview);
    }
}

internal static class SubscriptionProviderRules
{
    public static bool IsSupported(string? provider)
        => string.Equals(provider, "stripe", StringComparison.Ordinal)
            || string.Equals(provider, "paypal", StringComparison.Ordinal);
}
