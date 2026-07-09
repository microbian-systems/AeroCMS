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
using Aero.Cms.Modules.Commerce.Orders.Data;
using Aero.Cms.Modules.Commerce.Orders.Domain;
using Aero.Cms.Modules.Commerce.Orders.Services;
using Aero.Cms.Modules.Commerce.Data;
using Aero.Cms.Modules.Commerce.Orders.Validation;
using Aero.Cms.Modules.Commerce.Payments.Api;
using Aero.Services.Images;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

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
        services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, CommerceStartupFilter>());

        // Catalog (Marten)
        services.AddScoped<IProductService, ProductService>();

        // Basket (Marten)
        services.AddScoped<IBasketService, BasketService>();

        // Orders (EF Core)
        var connString = config?.GetConnectionString("aero")
                         ?? throw new InvalidOperationException("Connection string 'aero' is required for CommerceDbContext.");
        services.AddDbContext<CommerceDbContext>(o => o.UseNpgsql(connString,
            x => x.MigrationsHistoryTable(Aero.Core.Data.Schemas.MigrationTableName, Schemas.Database)));
        services.AddScoped<IOrderService, OrderService>();

        // Validation
        services.AddScoped<IValidator<ProductDocument>, ProductValidator>();
        services.AddScoped<IValidator<BasketItem>, BasketItemValidator>();
        services.AddScoped<IValidator<OrderEntity>, CreateOrderValidator>();

        // HTTP context accessor (for anonymous cart cookie)
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
        });
    }

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        // Product document — Marten schema
        opts.DatabaseSchemaName = Schemas.Database;
        opts.Schema.For<ProductDocument>()
            .DocumentAlias(Schemas.Tables.Products)
            .Identity(x => x.Id)
            .Index(x => x.Slug)
            .Index(x => x.Sku)
            .Index(x => x.Category)
            .Index(x => x.Price)
            .FullTextIndex(x => x.Name)
            .FullTextIndex(x => x.Description);

        opts.Schema.For<ProductTranslation>().Index(x => x.ProductId);
        opts.Schema.For<ProductTranslation>().Index(x => x.Culture);
        opts.Schema.For<ProductTranslation>().UniqueIndex(x => x.ProductId, x => x.Culture);

        // Basket document — Marten schema
        opts.Schema.For<BasketDocument>()
            .DocumentAlias(Schemas.Tables.Baskets)
            .Identity(x => x.Id)
            .Index(x => x.CustomerId);
    }

    public override void Run(IEndpointRouteBuilder builder)
    {
        builder.MapCatalogApi();
        builder.MapBasketApi();
        builder.MapOrderApi();
        builder.MapPaymentApi();

        base.Run(builder);
    }
}
