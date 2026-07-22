using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Commerce.Basket.Api;
using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Basket.Validation;
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
using Aero.Services.Images;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using FluentValidation;
using AeroDB.Sable;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
public override IReadOnlyList<string> Dependencies => [];
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

        // Validation
        services.AddScoped<IValidator<ProductDocument>, ProductValidator>();
        services.AddScoped<IValidator<ProductListingDocument>, ProductListingValidator>();
        services.AddScoped<IValidator<BasketItem>, BasketItemValidator>();
        services.AddScoped<IValidator<OrderEntity>, CreateOrderValidator>();
        services.AddScoped<IValidator<InitiatePaymentRequest>, InitiatePaymentRequestValidator>();

        services.AddHttpContextAccessor();

        // Pexels image service (also used by Setup module's SeedDatabaseService).
        // MediaModule also registers this via TryAddScoped — whichever loads first wins.
        services.TryAddScoped<IPexelsService, PexelsService>();

        // Commerce seed service
        services.AddScoped<ICommerceSeedService, CommerceSeedService>();

        // Razor Pages — register this module's Areas for public commerce pages
        services.AddRazorPages()
            .AddApplicationPart(typeof(CommerceModule).Assembly);

        services.Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AddAreaPageRoute("Commerce", "/ShopHome", "/shop");
            options.Conventions.AddAreaPageRoute("Commerce", "/Catalog", "/shop/products");
            options.Conventions.AddAreaPageRoute("Commerce", "/ProductDetail", "/shop/products/{slug}");
            options.Conventions.AddAreaPageRoute("Commerce", "/Cart", "/shop/cart");
            options.Conventions.AddAreaPageRoute("Commerce", "/Checkout", "/shop/checkout");
            options.Conventions.AddAreaPageRoute("Commerce", "/Orders", "/shop/orders");
            options.Conventions.AddAreaPageRoute("Commerce", "/OrderDetail", "/shop/orders/{id}");
            options.Conventions.AddAreaPageRoute("Commerce", "/Account", "/shop/account");
        });
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

        var products = opts.Schema.For<ProductDocument>();
        products.Identity(x => x.Id);
        products.UseOptimisticConcurrency = true;
        products.Index(x => x.TenantId);
        products.UniqueIndex(x => new { x.TenantId, x.Sku });

        var listings = opts.Schema.For<ProductListingDocument>();
        listings.Identity(x => x.Id);
        listings.UseOptimisticConcurrency = true;
        listings.Index(x => x.TenantId);
        listings.Index(x => x.SiteId);
        listings.Index(x => x.ProductId);
        listings.UniqueIndex(x => new { x.SiteId, x.Culture, x.Slug });
        listings.UniqueIndex(x => new { x.SiteId, x.Culture, x.ProductId });

        var baskets = opts.Schema.For<BasketDocument>();
        baskets.Identity(x => x.Id);
        baskets.UseOptimisticConcurrency = true;
        baskets.UniqueIndex(x => new { x.TenantId, x.SiteId, x.ExternalMemberId });

        var orders = opts.Schema.For<OrderEntity>();
        orders.Identity(x => x.Id);
        orders.UseOptimisticConcurrency = true;
        orders.Index(x => x.TenantId);
        orders.Index(x => x.SiteId);
        orders.Index(x => x.ExternalMemberId);
        orders.Index(x => x.Status);
        orders.Index(x => x.CreatedOn);
        var attempts = opts.Schema.For<PaymentAttemptDocument>();
        attempts.Identity(x => x.Id);
        attempts.UseOptimisticConcurrency = true;
        attempts.Index(x => x.TenantId);
        attempts.Index(x => x.SiteId);
        attempts.Index(x => x.OrderId);
        attempts.UniqueIndex(x => new { x.TenantId, x.SiteId, x.OrderId });
        var receipts = opts.Schema.For<PaymentWebhookReceiptDocument>();
        receipts.Identity(x => x.Id);
        receipts.UseOptimisticConcurrency = true;
        receipts.UniqueIndex(x => new { x.Provider, x.ProviderAccountKey, x.ProviderEventId });
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
        builder.MapBasketApi();
        builder.MapOrderApi();
        builder.MapPaymentApi();

        base.Run(builder);
    }
}
