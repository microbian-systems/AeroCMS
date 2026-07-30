using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Commerce.Basket.Api;
using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Basket.Validation;
using Aero.Cms.Modules.Commerce.A2A.Api;
using Aero.Cms.Modules.Commerce.A2A.Models;
using Aero.Cms.Modules.Commerce.A2A.Services;
using Aero.Cms.Modules.Commerce.A2A.Validation;
using Aero.Cms.Modules.Commerce.Catalog.Api;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Commerce.Catalog.Validation;
using Aero.Cms.Modules.Commerce.Orders.Api;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Aero.Cms.Modules.Commerce.Data;
using Aero.Cms.Modules.Commerce.Orders.Validation;
using Aero.Cms.Modules.Commerce.Payments.Api;
using Aero.Cms.Modules.Commerce.Payments;
using Aero.Cms.Modules.Commerce.Storefront;
using Aero.Cms.Modules.Commerce.PageEditor;
using Aero.Cms.Modules.Commerce.Subscriptions;
using Aero.Cms.Modules.Commerce.Subscriptions.Api;
using Aero.Cms.Modules.Commerce.Subscriptions.Webhooks;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Services.Images;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using FluentValidation;
using AeroDB.Sable;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;

namespace Aero.Cms.Modules.Commerce;

/// <summary>
/// Represents a class for CommerceModule.
/// </summary>
[Module(nameof(CommerceModule))]
public sealed class CommerceModule : AeroWebModule, IConfigureAeroDB
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(CommerceModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => ["PagesModule"];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["commerce"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["commerce", "catalog", "orders", "basket", "payments"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // Catalog (AeroDB Sable)
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICommerceManagerScopeResolver, CommerceManagerScopeResolver>();
        services.AddScoped<IA2ASettingsRepository, A2ASettingsRepository>();
        services.AddScoped<IA2ASettingsService, A2ASettingsService>();

        // Basket (AeroDB Sable)
        services.AddScoped<IBasketService, BasketService>();

        // Orders (AeroDB Sable — ported from EF Core Npgsql)
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IStorefrontMemberAccessor, StorefrontMemberAccessor>();
        if (config is not null) services.AddOptions<CommercePaymentOptions>().Bind(config.GetSection(CommercePaymentOptions.SectionName)).ValidateOnStart();
        else services.AddOptions<CommercePaymentOptions>().ValidateOnStart();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<CommercePaymentOptions>, CommercePaymentOptionsValidator>();
        services.AddHttpClient(StripePaymentProviderAdapter.HttpClientName);
        services.AddHttpClient(PayPalPaymentProviderAdapter.HttpClientName);
        services.AddScoped<IPaymentProviderAdapter, StripePaymentProviderAdapter>();
        services.AddScoped<IPaymentProviderAdapter, PayPalPaymentProviderAdapter>();
        services.AddScoped<IPaymentProviderRegistry, PaymentProviderRegistry>();
        services.AddScoped<IPaymentApplicationService, PaymentApplicationService>();
        services.AddScoped<ISubscriptionCheckoutProviderAdapter, StripeSubscriptionCheckoutProviderAdapter>();
        services.AddScoped<ISubscriptionCheckoutProviderAdapter, PayPalSubscriptionCheckoutProviderAdapter>();
        services.AddScoped<ISubscriptionCheckoutService, SubscriptionCheckoutService>();
        services.AddScoped<ISubscriptionVisibilityService, SubscriptionVisibilityService>();
        services.AddScoped<ISubscriptionWebhookProviderAdapter, StripePaymentProviderAdapter>();
        services.AddScoped<ISubscriptionWebhookProviderAdapter, PayPalPaymentProviderAdapter>();
        services.AddScoped<ISubscriptionReconciliationService, SubscriptionReconciliationService>();

        // Validation
        services.AddScoped<IValidator<ProductDocument>, ProductValidator>();
        services.AddScoped<IValidator<ProductListingDocument>, ProductListingValidator>();
        services.AddScoped<IValidator<SubscriptionOffer>, SubscriptionOfferValidator>();
        services.AddScoped<IValidator<SubscriptionLineSnapshot>, SubscriptionLineSnapshotValidator>();
        services.AddScoped<IValidator<SubscriptionDocument>, SubscriptionDocumentValidator>();
        services.AddScoped<IValidator<SubscriptionCycleDocument>, SubscriptionCycleDocumentValidator>();
        services.AddScoped<IValidator<SubscriptionWebhookReceiptDocument>, SubscriptionWebhookReceiptDocumentValidator>();
        services.AddScoped<IValidator<BasketItem>, BasketItemValidator>();
        services.AddScoped<IValidator<BasketDocument>, BasketDocumentValidator>();
        services.AddScoped<IValidator<OrderEntity>, CreateOrderValidator>();
        services.AddScoped<IValidator<InitiatePaymentRequest>, InitiatePaymentRequestValidator>();
        services.AddScoped<IValidator<UpdateA2ASettingsRequest>, UpdateA2ASettingsRequestValidator>();

        services.AddHttpContextAccessor();

        // Pexels image service (also used by Setup module's SeedDatabaseService).
        // MediaModule also registers this via TryAddScoped — whichever loads first wins.
        services.TryAddScoped<IPexelsService, PexelsService>();

        // Commerce seed service
        services.AddScoped<ICommerceSeedService, CommerceSeedService>();
        services.AddPageRegisteredFragment<CommerceCatalogPageRegisteredFragmentProvider>();
        services.AddPageRegisteredFragment<CommerceSearchPageRegisteredFragmentProvider>();
        services.AddPageRegisteredFragment<CommerceProductPageRegisteredFragmentProvider>();

        // Razor Pages — private, stateful storefront flows are declared directly by
        // their Razor Page directives. Public catalog routes are deliberately owned
        // by the CMS PageDocument catch-all and rendered through registered fragments.
        services.AddRazorPages()
            .AddApplicationPart(typeof(CommerceModule).Assembly);
    }

        /// <summary>
    /// Configure method.
    /// </summary>
    public void Configure(StoreOptions opts)
    {
        // Current Sable sessions consult this store-level flag; mapping flags alone do not activate checks.
        opts.UseOptimisticConcurrency = true;
        opts.Schema.Analyzers.DefineAnalyzer(
            Search.Analyzer.English,
            filters:
            [
                Search.Filter.Lowercase,
                Search.Filter.SnowballEnglish
            ]);

        var products = opts.Schema.For<ProductDocument>()
            .TableName(Schemas.Tables.Products);
        products.Identity(x => x.Id);
        products.UseOptimisticConcurrency = true;
        products.Index(x => x.TenantId);
        products.UniqueIndex(x => new { x.TenantId, x.Sku });

        var listings = opts.Schema.For<ProductListingDocument>()
            .TableName(Schemas.Tables.ProductListings);
        listings.Identity(x => x.Id);
        listings.UseOptimisticConcurrency = true;
        listings.Index(x => x.TenantId);
        listings.Index(x => x.SiteId);
        listings.Index(x => x.ProductId);
        listings.UniqueIndex(x => new { x.SiteId, x.Culture, x.Slug });
        listings.UniqueIndex(x => new { x.SiteId, x.Culture, x.ProductId });

        var a2aSettings = opts.Schema.For<A2ASettingsDocument>()
            .TableName(Schemas.Tables.A2ASettings);
        a2aSettings.Identity(x => x.Id);
        a2aSettings.UseOptimisticConcurrency = true;
        a2aSettings.Index(x => x.TenantId);
        a2aSettings.Index(x => x.SiteId);
        a2aSettings.UniqueIndex(x => new { x.TenantId, x.SiteId });

        var baskets = opts.Schema.For<BasketDocument>()
            .TableName(Schemas.Tables.Baskets);
        baskets.Identity(x => x.Id);
        baskets.UseOptimisticConcurrency = true;
        baskets.UniqueIndex(x => new { x.TenantId, x.SiteId, x.ExternalMemberId });

        var orders = opts.Schema.For<OrderEntity>()
            .TableName(Schemas.Tables.Orders);
        orders.Identity(x => x.Id);
        orders.UseOptimisticConcurrency = true;
        orders.Index(x => x.TenantId);
        orders.Index(x => x.SiteId);
        orders.Index(x => x.ExternalMemberId);
        orders.Index(x => x.Status);
        orders.Index(x => x.CreatedOn);
        var attempts = opts.Schema.For<PaymentAttemptDocument>()
            .TableName(Schemas.Tables.PaymentAttempts);
        attempts.Identity(x => x.Id);
        attempts.UseOptimisticConcurrency = true;
        attempts.Index(x => x.TenantId);
        attempts.Index(x => x.SiteId);
        attempts.Index(x => x.OrderId);
        attempts.UniqueIndex(x => new { x.TenantId, x.SiteId, x.OrderId });
        var receipts = opts.Schema.For<PaymentWebhookReceiptDocument>()
            .TableName(Schemas.Tables.PaymentWebhookReceipts);
        receipts.Identity(x => x.Id);
        receipts.UseOptimisticConcurrency = true;
        receipts.UniqueIndex(x => new { x.Provider, x.ProviderAccountKey, x.ProviderEventId });

        var subscriptions = opts.Schema.For<SubscriptionDocument>()
            .TableName(Schemas.Tables.Subscriptions);
        subscriptions.Identity(x => x.Id);
        subscriptions.UseOptimisticConcurrency = true;
        subscriptions.Index(x => x.TenantId);
        subscriptions.Index(x => x.SiteId);
        subscriptions.Index(x => x.ExternalMemberId);
        subscriptions.Index(x => x.OrderId);
        subscriptions.Index(x => x.State);
        subscriptions.Index(x => x.ProviderSubscriptionReference);
        subscriptions.Index(x => x.ProviderCheckoutReference);
        subscriptions.UniqueIndex(x => new { x.Provider, x.ProviderAccountKey, x.ProviderOperationKey });
        subscriptions.UniqueIndex(x => new { x.TenantId, x.SiteId, x.OrderId });

        var cycles = opts.Schema.For<SubscriptionCycleDocument>()
            .TableName(Schemas.Tables.SubscriptionCycles);
        cycles.Identity(x => x.Id);
        cycles.UseOptimisticConcurrency = true;
        cycles.Index(x => x.TenantId);
        cycles.Index(x => x.SiteId);
        cycles.Index(x => x.ExternalMemberId);
        cycles.Index(x => x.SubscriptionId);
        cycles.Index(x => x.PaymentAttemptId);
        cycles.Index(x => x.ProviderPaymentReference);
        cycles.UniqueIndex(x => new { x.SubscriptionId, x.CycleNumber });
        cycles.UniqueIndex(x => new { x.Provider, x.ProviderAccountKey, x.ProviderCycleReference });
        cycles.UniqueIndex(x => new { x.Provider, x.ProviderAccountKey, x.ProviderPaymentReference });

        var subscriptionReceipts = opts.Schema.For<SubscriptionWebhookReceiptDocument>()
            .TableName(Schemas.Tables.SubscriptionWebhookReceipts);
        subscriptionReceipts.Identity(x => x.Id);
        subscriptionReceipts.UseOptimisticConcurrency = true;
        subscriptionReceipts.Index(x => x.TenantId);
        subscriptionReceipts.Index(x => x.SiteId);
        subscriptionReceipts.Index(x => x.ExternalMemberId);
        subscriptionReceipts.Index(x => x.SubscriptionId);
        subscriptionReceipts.Index(x => x.SubscriptionCycleId);
        subscriptionReceipts.Index(x => x.ProviderSubscriptionReference);
        subscriptionReceipts.Index(x => x.ProviderPaymentReference);
        subscriptionReceipts.UniqueIndex(x => new { x.Provider, x.ProviderAccountKey, x.ProviderEventId });
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

        /// <summary>
    /// Run method.
    /// </summary>
public override void Run(IEndpointRouteBuilder builder)
    {
        builder.MapCatalogApi();
        builder.MapA2ASettingsApi();
        builder.MapA2ACommerceApi();
        builder.MapBasketApi();
        builder.MapOrderApi();
        builder.MapPaymentApi();
        builder.MapSubscriptionVisibilityApi();
        builder.MapSubscriptionWebhookApi();

        base.Run(builder);
    }
}
