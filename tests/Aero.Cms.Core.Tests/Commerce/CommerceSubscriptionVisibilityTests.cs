using System.Text.Json;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Modules.Commerce;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Commerce.Subscriptions;
using Aero.Cms.Modules.Commerce.Subscriptions.Api;
using Aero.Cms.Core.Tests.Integration;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Commerce;

public sealed class CommerceSubscriptionVisibilityTests
{
    [Test]
    public async Task Member_receipt_and_history_are_scoped_to_tenant_site_and_member()
    {
        await using var harness = await HarnessAsync();
        var owned = Subscription(500, 1, 10, 100, 400, "stripe");
        var otherMember = Subscription(501, 1, 10, 101, 401, "paypal");
        var otherSite = Subscription(502, 1, 11, 100, 402, "stripe");
        harness.Session.Store(owned); harness.Session.Store(otherMember); harness.Session.Store(otherSite);
        harness.Session.Store(Cycle(600, owned)); harness.Session.Store(Cycle(601, otherMember));
        await harness.Session.SaveChangesAsync();

        var visibility = new SubscriptionVisibilityService(harness.Session);
        var history = (await visibility.ListForMemberAsync(1, 10, 100)).ShouldBeOfType<Result<IReadOnlyList<MemberSubscriptionSummary>, AeroError>.Ok>().Value;
        history.Count.ShouldBe(1);
        history[0].SubscriptionId.ShouldBe(500);
        history[0].Provider.ShouldBe("Stripe");
        history[0].CurrentPeriodEndsOn.ShouldBe(owned.CurrentPeriodEndsOn);

        var receipt = (await visibility.GetForMemberOrderAsync(1, 10, 100, 400)).ShouldBeOfType<Result<MemberSubscriptionReceipt?, AeroError>.Ok>().Value;
        receipt.ShouldNotBeNull();
        receipt!.Subscription.Amount.ShouldBe(24m);
        receipt.Subscription.State.ShouldBe(SubscriptionState.ManualReview);
        receipt.Subscription.RequiresManualReview.ShouldBeTrue();
        receipt.Cycles.Single().State.ShouldBe(SubscriptionCycleState.ManualReview);
        receipt.Cycles.Single().RequiresManualReview.ShouldBeTrue();
        receipt.Lines.Single().TotalAmount.ShouldBe(24m);

        (await visibility.GetForMemberOrderAsync(1, 10, 100, 401)).ShouldBeOfType<Result<MemberSubscriptionReceipt?, AeroError>.Ok>().Value.ShouldBeNull();
        (await visibility.GetForMemberOrderAsync(1, 11, 100, 400)).ShouldBeOfType<Result<MemberSubscriptionReceipt?, AeroError>.Ok>().Value.ShouldBeNull();
    }

    [Test]
    public async Task Manager_page_is_site_scoped_paged_and_redacts_provider_transport_fields()
    {
        await using var harness = await HarnessAsync();
        var newest = Subscription(700, 1, 10, 100, 410, "stripe");
        newest.ProviderAccountKey = "stripe-account-secret";
        newest.ProviderOperationKey = "operation-secret";
        newest.ProviderCheckoutReference = "checkout-secret";
        newest.ProviderSubscriptionReference = "subscription-secret";
        newest.ProviderCustomerReference = "customer-secret";
        newest.CreatedOn = DateTimeOffset.Parse("2026-07-03T00:00:00Z");
        newest.Lines[0].ProviderOfferReference = "offer-secret";
        var older = Subscription(701, 1, 10, 101, 411, "paypal");
        older.CreatedOn = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        var otherSite = Subscription(702, 1, 11, 100, 412, "stripe");
        var otherTenant = Subscription(703, 2, 10, 100, 413, "stripe");
        harness.Session.Store(newest); harness.Session.Store(older); harness.Session.Store(otherSite); harness.Session.Store(otherTenant);
        harness.Session.Store(Cycle(710, newest));
        await harness.Session.SaveChangesAsync();

        var visibility = new SubscriptionVisibilityService(harness.Session);
        var page = (await visibility.ListForManagerAsync(1, 10, 1, 1)).ShouldBeOfType<Result<ManagerSubscriptionPage, AeroError>.Ok>().Value;
        page.TotalCount.ShouldBe(2);
        page.Items.Single().SubscriptionId.ShouldBe(701);
        page.Items.Single().Provider.ShouldBe("PayPal");

        var receipt = (await visibility.GetForManagerAsync(1, 10, 700)).ShouldBeOfType<Result<ManagerSubscriptionReceipt?, AeroError>.Ok>().Value;
        receipt.ShouldNotBeNull();
        var serialized = JsonSerializer.Serialize(receipt);
        serialized.ShouldNotContain("stripe-account-secret");
        serialized.ShouldNotContain("operation-secret");
        serialized.ShouldNotContain("checkout-secret");
        serialized.ShouldNotContain("subscription-secret");
        serialized.ShouldNotContain("customer-secret");
        serialized.ShouldNotContain("offer-secret");
        receipt!.Lines.Single().ProductName.ShouldBe("Recurring service");
        receipt.Cycles.Single().Amount.ShouldBe(24m);

        (await visibility.GetForManagerAsync(1, 10, 702)).ShouldBeOfType<Result<ManagerSubscriptionReceipt?, AeroError>.Ok>().Value.ShouldBeNull();
        (await visibility.GetForManagerAsync(2, 10, 700)).ShouldBeOfType<Result<ManagerSubscriptionReceipt?, AeroError>.Ok>().Value.ShouldBeNull();
    }

    [Test]
    public async Task Authenticated_member_and_manager_visibility_endpoints_are_never_stored()
    {
        var visibility = Substitute.For<ISubscriptionVisibilityService>();
        visibility.ListForMemberAsync(1, 10, 100, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<IReadOnlyList<MemberSubscriptionSummary>, AeroError>>(Prelude.Ok<IReadOnlyList<MemberSubscriptionSummary>, AeroError>([])));
        visibility.GetForMemberOrderAsync(1, 10, 100, 400, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<MemberSubscriptionReceipt?, AeroError>>(Prelude.Ok<MemberSubscriptionReceipt?, AeroError>(null)));
        visibility.ListForManagerAsync(1, 10, 0, 20, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ManagerSubscriptionPage, AeroError>>(Prelude.Ok<ManagerSubscriptionPage, AeroError>(new([], 0))));
        visibility.GetForManagerAsync(1, 10, 700, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ManagerSubscriptionReceipt?, AeroError>>(Prelude.Ok<ManagerSubscriptionReceipt?, AeroError>(null)));

        await using var app = await CreateEndpointAppAsync(visibility);
        foreach (var (path, statusCode) in new[]
                 {
                     ("/api/commerce/subscriptions", System.Net.HttpStatusCode.OK),
                     ("/api/commerce/subscriptions/orders/400", System.Net.HttpStatusCode.NotFound),
                     ("/api/v1/admin/commerce/subscriptions", System.Net.HttpStatusCode.OK),
                     ("/api/v1/admin/commerce/subscriptions/700", System.Net.HttpStatusCode.NotFound)
                 })
        {
            using var request = TestAuthentication.WithTestUser(new HttpRequestMessage(HttpMethod.Get, path), 100);
            using var response = await app.GetTestClient().SendAsync(request);
            response.StatusCode.ShouldBe(statusCode);
            response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        }
    }

    private static SubscriptionDocument Subscription(long id, long tenantId, long siteId, long memberId, long orderId, string provider) => new()
    {
        Id = id, TenantId = tenantId, SiteId = siteId, ExternalMemberId = memberId, OrderId = orderId,
        Provider = provider, ProviderAccountKey = $"{provider}-account", ProviderOperationKey = $"commerce-subscription-order-{orderId}",
        Lines = [new SubscriptionLineSnapshot { ProductId = 1, ListingId = 2, ProductName = "Recurring service", ListingName = "Recurring service", Sku = "RECUR-1", Quantity = 2, UnitAmount = 12m, ProviderOfferReference = "price_monthly" }],
        Currency = "USD", IntervalDays = 30, State = SubscriptionState.ManualReview, RequiresManualReview = true,
        ManualReviewReason = "Mismatch", ManualReviewRequestedOn = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
        CurrentPeriodStartsOn = DateTimeOffset.Parse("2026-07-01T00:00:00Z"), CurrentPeriodEndsOn = DateTimeOffset.Parse("2026-07-31T00:00:00Z"),
        CreatedOn = DateTimeOffset.Parse("2026-07-02T00:00:00Z")
    };

    private static SubscriptionCycleDocument Cycle(long id, SubscriptionDocument subscription) => new()
    {
        Id = id, TenantId = subscription.TenantId, SiteId = subscription.SiteId, ExternalMemberId = subscription.ExternalMemberId,
        SubscriptionId = subscription.Id, CycleNumber = 1, Provider = subscription.Provider, ProviderAccountKey = subscription.ProviderAccountKey,
        ProviderCycleReference = $"cycle-{id}", ProviderPaymentReference = $"payment-{id}", Lines = subscription.Lines,
        AmountSnapshot = subscription.TotalAmount, Currency = subscription.Currency, PeriodStartsOn = subscription.CurrentPeriodStartsOn!.Value,
        PeriodEndsOn = subscription.CurrentPeriodEndsOn!.Value, State = SubscriptionCycleState.ManualReview, RequiresManualReview = true,
        ManualReviewReason = "Mismatch", ManualReviewRequestedOn = DateTimeOffset.Parse("2026-07-01T00:00:00Z")
    };

    private static async Task<SableTestHarness> HarnessAsync()
    {
        var harness = new SableTestHarness().WithConfiguration(new CommerceModule().Configure);
        await harness.InitializeAsync();
        return harness;
    }

    private static async Task<WebApplication> CreateEndpointAppAsync(ISubscriptionVisibilityService visibility)
    {
        var principal = Substitute.For<ICurrentPrincipal>();
        principal.PrincipalId.Returns(100L);
        var site = Substitute.For<ISiteContext>();
        site.TenantId.Returns(1L);
        site.SiteId.Returns(10L);
        var managerScope = Substitute.For<ICommerceManagerScopeResolver>();
        managerScope.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<CommerceManagerScope, AeroError>>(Prelude.Ok<CommerceManagerScope, AeroError>(new(1, 10))));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuthentication();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(ExternalMemberAuthenticationDefaults.Policy, policy => policy
                .AddAuthenticationSchemes(TestAuthentication.Scheme)
                .RequireAuthenticatedUser());
            options.AddPolicy(ExternalMemberAuthenticationDefaults.SitePolicy, policy => policy
                .AddAuthenticationSchemes(TestAuthentication.Scheme)
                .RequireAuthenticatedUser());
        });
        builder.Services.AddSingleton(visibility);
        builder.Services.AddSingleton(principal);
        builder.Services.AddSingleton(site);
        builder.Services.AddSingleton(managerScope);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSubscriptionVisibilityApi();
        await app.StartAsync();
        return app;
    }
}
