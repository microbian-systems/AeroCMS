using Aero.Cms.Core;
using Aero.Cms.Modules.Commerce.Basket.Api;
using Aero.Cms.Modules.Commerce.Basket.Models;
using Aero.Cms.Modules.Commerce.Basket.Services;
using Aero.Cms.Modules.Commerce.Basket.Validation;
using Aero.Cms.Modules.Commerce.Catalog.Api;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.Catalog.Services;
using Aero.Cms.Modules.Commerce.Catalog.Validation;
using Aero.Cms.Modules.Commerce.Orders.Api;
using Aero.Cms.Modules.Commerce.Orders.Data;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Aero.Cms.Modules.Commerce.Orders.Validation;
using Aero.Cms.Modules.Commerce.Payments.Api;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Commerce;

[Module(nameof(CommerceModule))]
public sealed class CommerceModule : AeroWebModule, IConfigureMarten
{
    public override string Name => nameof(CommerceModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["commerce"];
    public override IReadOnlyList<string> Tags => ["commerce", "catalog", "orders", "basket", "payments"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // Catalog (Marten)
        services.AddScoped<IProductService, ProductService>();

        // Basket (Marten)
        services.AddScoped<IBasketService, BasketService>();

        // Orders (EF Core)
        services.AddDbContext<CommerceDbContext>();
        services.AddScoped<IOrderService, OrderService>();

        // Validation
        services.AddScoped<IValidator<ProductDocument>, ProductValidator>();
        services.AddScoped<IValidator<BasketItem>, BasketItemValidator>();
        services.AddScoped<IValidator<OrderEntity>, CreateOrderValidator>();
    }

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        // Product document — Marten schema
        opts.Schema.For<ProductDocument>()
            .DocumentAlias("commerce_products")
            .Identity(x => x.Id)
            .Index(x => x.Slug)
            .Index(x => x.Sku)
            .Index(x => x.Category)
            .Index(x => x.Price)
            .FullTextIndex(x => x.Name)
            .FullTextIndex(x => x.Description);

        // Basket document — Marten schema
        opts.Schema.For<BasketDocument>()
            .DocumentAlias("commerce_baskets")
            .Identity(x => x.Id)
            .Index(x => x.CustomerId);
    }

    public override void Run(IEndpointRouteBuilder builder)
    {
        CatalogEndpoints.MapCatalogApi(builder);
        BasketEndpoints.MapBasketApi(builder);
        OrderEndpoints.MapOrderApi(builder);
        PaymentEndpoints.MapPaymentApi(builder);

        base.Run(builder);
    }
}
