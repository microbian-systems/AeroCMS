using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Services.Images;
using AeroDB.Sable;
using Bogus;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Commerce.Data;

public interface ICommerceSeedService { Task SeedAsync(long siteId, CancellationToken ct = default); }

/// <summary>Seeds tenant-owned products and listings for one requested storefront site.</summary>
public sealed class CommerceSeedService(IPexelsService pexels, IDocumentSession session, ILogger<CommerceSeedService> log) : ICommerceSeedService
{
    private static readonly string[] Categories = ["Clothing", "Equipment", "Accessories", "Footwear"];
    public async Task SeedAsync(long siteId, CancellationToken ct = default)
    {
        var site = await session.LoadAsync<SitesModel>(siteId, ct) ?? throw new InvalidOperationException("Seed site was not found.");
        var faker = new Faker();
        var products = Enumerable.Range(0, 12).Select(_ => new ProductDocument { Id = Snowflake.NewId(), TenantId = site.TenantId, Name = faker.Commerce.ProductName(), Description = faker.Commerce.ProductDescription(), Sku = faker.Commerce.Ean13(), StockQuantity = faker.Random.Int(0, 500), IsActive = true, CreatedBy = "seed" }).ToList();
        foreach (var product in products)
        {
            var category = new Faker().PickRandom(Categories);
            string? imageUrl = null;
            var photos = await pexels.SearchPhotosAsync($"product photography {category}", count: 1, ct: ct);
            if (photos.Count > 0) imageUrl = await pexels.DownloadPhotoAsync(photos[0], "products", $"product-{product.Id}", ct);
            session.Store(product);
            session.Store(new ProductListingDocument { Id = Snowflake.NewId(), TenantId = site.TenantId, SiteId = site.Id, ProductId = product.Id, Culture = site.DefaultCulture ?? SitesModel.DefaultCultureName, Slug = $"{product.Name.ToLowerInvariant().Replace(' ', '-')}-{product.Id}", Name = product.Name, Description = product.Description, Category = category, ImageUrl = imageUrl, Price = Math.Round(new Faker().Random.Decimal(9.99m, 299.99m), 2), Currency = "USD", IsPublished = true, CreatedBy = "seed" });
        }
        await session.SaveChangesAsync(ct);
        log.LogInformation("Commerce seed created {Count} tenant products and listings for site {SiteId}", products.Count, siteId);
    }
}
