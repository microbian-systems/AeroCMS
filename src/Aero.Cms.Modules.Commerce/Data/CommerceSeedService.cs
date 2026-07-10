using Aero.Cms.Core.Models;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Services.Images;
using Bogus;
using AeroDB.Sable;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Commerce.Data;

public interface ICommerceSeedService
{
    Task SeedAsync(long siteId, CancellationToken ct = default);
}

/// <summary>
/// Seeds commerce module with starter products, images, and a landing page.
/// Uses Pexels API for real product images and Bogus for varied product data.
/// </summary>
public sealed class CommerceSeedService(
    IPexelsService pexels,
    IDocumentSession session,
    ILogger<CommerceSeedService> log) : ICommerceSeedService
{
    private static readonly string[] Categories = ["Clothing", "Equipment", "Accessories", "Footwear"];

    public async Task SeedAsync(long siteId, CancellationToken ct = default)
    {
        log.LogInformation("Commerce seed: starting product seeding with Pexels images...");

        // Fetch Pexels images per category
        var categoryPhotos = new Dictionary<string, IReadOnlyList<PexelsPhoto>>();
        foreach (var cat in Categories)
        {
            var photos = await pexels.SearchPhotosAsync($"product photography {cat}", count: 5, ct: ct);
            categoryPhotos[cat] = photos;
            await Task.Delay(200, ct); // Be kind to Pexels rate limits
        }

        // Generate products with Bogus
        var productFaker = new Faker<ProductDocument>()
            .RuleFor(p => p.Id, _ => Snowflake.NewId())
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Slug, (f, p) => f.Internet.UserName().ToLowerInvariant() + "-" + f.Random.AlphaNumeric(4))
            .RuleFor(p => p.Sku, f => f.Commerce.Ean13())
            .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
            .RuleFor(p => p.ShortDescription, f => f.Lorem.Sentence(10))
            .RuleFor(p => p.Price, f => Math.Round(f.Random.Decimal(9.99m, 299.99m), 2))
            .RuleFor(p => p.CompareAtPrice, (f, p) => f.Random.Bool(0.3f) ? Math.Round(p.Price * f.Random.Decimal(1.1m, 1.5m), 2) : null)
            .RuleFor(p => p.StockQuantity, f => f.Random.Int(0, 500))
            .RuleFor(p => p.Currency, _ => "USD")
            .RuleFor(p => p.Category, f => f.PickRandom(Categories))
            .RuleFor(p => p.IsPublished, _ => true)
            .RuleFor(p => p.ImageUrl, _ => null!) // Set below via Pexels
            .RuleFor(p => p.CreatedOn, _ => DateTimeOffset.UtcNow)
            .RuleFor(p => p.CreatedBy, _ => "seed")
            .RuleFor(p => p.ModifiedBy, _ => "seed")
            .RuleFor(p => p.Tags, f => [f.Commerce.Categories(1)[0]]);

        var products = productFaker.Generate(12);

        // Download Pexels images for each product and register media
        foreach (var product in products)
        {
            if (categoryPhotos.TryGetValue(product.Category, out var photos) && photos.Count > 0)
            {
                var photo = photos[new Random().Next(photos.Count)];
                var localPath = await pexels.DownloadPhotoAsync(photo, "products", $"product-{product.Id}", ct);

                if (!string.IsNullOrEmpty(localPath))
                {
                    product.ImageUrl = localPath;

                    var filePath = Path.Combine(
                        Directory.GetCurrentDirectory(), "wwwroot", "media", "products", $"product-{product.Id}.jpg");

                    session.Store(new MediaAsset
                    {
                        Id = Snowflake.NewId(),
                        FileName = $"product-{product.Id}.jpg",
                        Url = localPath,
                        MimeType = "image/jpeg",
                        FileSize = File.Exists(filePath) ? new FileInfo(filePath).Length : 0,
                        AltText = product.Name,
                        IsFolder = false
                    });
                }
            }

            session.Store(product);
        }

        await session.SaveChangesAsync(ct);

        // Download skateboard promo video
        log.LogInformation("Commerce seed: downloading skateboard promo video...");
        var video = await pexels.GetVideoByIdAsync(10118302, ct);
        if (video is not null)
        {
            var videoPath = await pexels.DownloadVideoAsync(video, "videos", "skateboard-promo", ct);
            if (!string.IsNullOrEmpty(videoPath))
            {
                session.Store(new MediaAsset
                {
                    Id = Snowflake.NewId(),
                    FileName = "skateboard-promo.mp4",
                    Url = videoPath,
                    MimeType = "video/mp4",
                    AltText = "Skateboard promo video",
                    IsFolder = false
                });
                await session.SaveChangesAsync(ct);
            }
        }

        log.LogInformation("Commerce seed: seeded {Count} products and media assets", products.Count);
    }
}
