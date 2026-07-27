using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Validation;
using Aero.Cms.Modules.Commerce.Subscriptions;
using Aero.Core;
using AeroDB.Sable;
using Shouldly;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceSubscriptionDomainTests
{
    [Test]
    public async Task Listing_offer_accepts_draft_interval_only_configuration_and_rejects_invalid_intervals()
    {
        var validator = new ProductListingValidator();
        var valid = Listing(new SubscriptionOffer
        {
            IntervalDays = 30,
            StripePriceId = "price_monthly_30",
            PayPalPlanId = "P-MONTHLY-30"
        });

        (await validator.ValidateAsync(valid)).IsValid.ShouldBeTrue();

        valid.SubscriptionOffer!.IntervalDays = 366;
        (await validator.ValidateAsync(valid)).IsValid.ShouldBeFalse();

        valid.SubscriptionOffer.IntervalDays = 30;
        valid.SubscriptionOffer.StripePriceId = null;
        valid.SubscriptionOffer.PayPalPlanId = null;
        (await validator.ValidateAsync(valid)).IsValid.ShouldBeTrue();
    }

    [Test]
    public async Task Subscription_documents_require_scoped_provider_snapshots_and_manual_review_detail()
    {
        var subscription = Subscription();
        var cycle = Cycle(subscription.Id);
        var receipt = Receipt(subscription.Id, cycle.Id);

        (await new SubscriptionDocumentValidator().ValidateAsync(subscription)).IsValid.ShouldBeTrue();
        (await new SubscriptionCycleDocumentValidator().ValidateAsync(cycle)).IsValid.ShouldBeTrue();
        (await new SubscriptionWebhookReceiptDocumentValidator().ValidateAsync(receipt)).IsValid.ShouldBeTrue();

        subscription.RequiresManualReview = true;
        subscription.ManualReviewReason = null;
        subscription.ManualReviewRequestedOn = null;
        (await new SubscriptionDocumentValidator().ValidateAsync(subscription)).IsValid.ShouldBeFalse();
    }

    [Test]
    public async Task Commerce_schema_persists_scoped_subscription_documents_and_deduplicates_webhook_receipts()
    {
        await using var harness = new SableTestHarness().WithConfiguration(new CommerceModule().Configure);
        await harness.InitializeAsync();

        var subscription = Subscription();
        var cycle = Cycle(subscription.Id);
        var receipt = Receipt(subscription.Id, cycle.Id);
        harness.Session.Store(subscription);
        harness.Session.Store(cycle);
        harness.Session.Store(receipt);
        await harness.Session.SaveChangesAsync();

        await using var verify = await harness.OpenSessionAsync();
        (await verify.Query<SubscriptionDocument>().FirstOrDefaultAsync(x => x.Id == subscription.Id))!.ExternalMemberId.ShouldBe(300);
        (await verify.Query<SubscriptionCycleDocument>().FirstOrDefaultAsync(x => x.Id == cycle.Id))!.ProviderPaymentReference.ShouldBe("in_123");
        (await verify.Query<SubscriptionWebhookReceiptDocument>().FirstOrDefaultAsync(x => x.Id == receipt.Id))!.ProviderEventId.ShouldBe("evt_123");

        await using var duplicate = await harness.OpenSessionAsync();
        duplicate.Store(Receipt(subscription.Id, cycle.Id, "evt_123"));
        await Assert.That(async () => await duplicate.SaveChangesAsync()).Throws<Exception>();
    }

    [Test]
    public void Canonical_product_exposes_non_inventory_recurring_mode_for_subscription_offer_eligibility()
    {
        var product = new ProductDocument { Id = Snowflake.NewId(), FulfillmentMode = ProductFulfillmentMode.NonInventoryRecurring };

        product.Id.ShouldBeGreaterThan(0L);
        product.FulfillmentMode.ShouldBe(ProductFulfillmentMode.NonInventoryRecurring);
    }

    private static ProductListingDocument Listing(SubscriptionOffer offer) => new()
    {
        Id = Snowflake.NewId(),
        TenantId = 100,
        SiteId = 200,
        ProductId = 101,
        Culture = "en-US",
        Slug = "recurring-service",
        Name = "Recurring service",
        Price = 12m,
        SubscriptionOffer = offer
    };

    private static SubscriptionDocument Subscription() => new()
    {
        Id = Snowflake.NewId(),
        TenantId = 100,
        SiteId = 200,
        ExternalMemberId = 300,
        OrderId = 400,
        Provider = "stripe",
        ProviderAccountKey = "stripe-store",
        ProviderOperationKey = "commerce-subscription-order-400",
        ProviderCheckoutReference = "cs_123",
        ProviderSubscriptionReference = "sub_123",
        ProviderCustomerReference = "cus_123",
        Lines = [Line()],
        Currency = "USD",
        IntervalDays = 30,
        State = SubscriptionState.Active,
        LastAppliedProviderEventId = "evt_123",
        LastAppliedProviderOccurredOn = DateTimeOffset.UtcNow
    };

    private static SubscriptionCycleDocument Cycle(long subscriptionId) => new()
    {
        Id = Snowflake.NewId(),
        TenantId = 100,
        SiteId = 200,
        ExternalMemberId = 300,
        SubscriptionId = subscriptionId,
        CycleNumber = 1,
        Provider = "stripe",
        ProviderAccountKey = "stripe-store",
        ProviderCycleReference = "cycle_123",
        ProviderPaymentReference = "in_123",
        Lines = [Line()],
        AmountSnapshot = 12m,
        Currency = "USD",
        PeriodStartsOn = DateTimeOffset.UtcNow,
        PeriodEndsOn = DateTimeOffset.UtcNow.AddDays(30),
        State = SubscriptionCycleState.Paid,
        LastAppliedProviderEventId = "evt_123",
        LastAppliedProviderOccurredOn = DateTimeOffset.UtcNow
    };

    private static SubscriptionWebhookReceiptDocument Receipt(long subscriptionId, long cycleId, string eventId = "evt_123") => new()
    {
        Id = Snowflake.NewId(),
        TenantId = 100,
        SiteId = 200,
        ExternalMemberId = 300,
        SubscriptionId = subscriptionId,
        SubscriptionCycleId = cycleId,
        Provider = "stripe",
        ProviderAccountKey = "stripe-store",
        ProviderEventId = eventId,
        ProviderSubscriptionReference = "sub_123",
        ProviderCycleReference = "cycle_123",
        ProviderPaymentReference = "in_123",
        ProviderOccurredOn = DateTimeOffset.UtcNow,
        State = SubscriptionWebhookReceiptState.Applied
    };

    private static SubscriptionLineSnapshot Line() => new()
    {
        ProductId = 101,
        ListingId = 201,
        ProductName = "Recurring service",
        ListingName = "Recurring service",
        Sku = "RECUR-101",
        Quantity = 1,
        UnitAmount = 12m,
        ProviderOfferReference = "price_monthly_30"
    };
}
