using System.Globalization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Commerce.Catalog.Models;
using Aero.Cms.Modules.Commerce.PageEditor;
using Aero.Cms.Modules.Pages;
using Aero.Services.Images;
using AeroDB.Sable;
using Bogus;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Commerce.Data;

public interface ICommerceSeedService { Task SeedAsync(long siteId, CancellationToken ct = default); }

/// <summary>Seeds tenant-owned catalog data and PageEditor-native public Commerce pages for one storefront site.</summary>
public sealed class CommerceSeedService(
    IPexelsService pexels,
    IDocumentSession session,
    IPageContentService pageContentService,
    IPagePublishingWorkflowService pagePublishingWorkflowService,
    ILogger<CommerceSeedService> log) : ICommerceSeedService
{
    private static readonly string[] Categories = ["Clothing", "Equipment", "Accessories", "Footwear"];

    public async Task SeedAsync(long siteId, CancellationToken ct = default)
    {
        var site = await session.LoadAsync<SitesModel>(siteId, ct)
            ?? throw new InvalidOperationException("Seed site was not found.");
        var defaultCulture = NormalizeCulture(site.DefaultCulture);
        await EnsureCatalogAsync(site, defaultCulture, ct);

        foreach (var culture in GetSupportedCultures(site, defaultCulture))
        {
            var listings = await GetSiteCultureListingsAsync(siteId, culture, ct);
            var shop = await SaveAndPublishAsync(
                CreatePage(siteId, culture, "shop", "/shop", null, 0, "Shop", "Browse featured products from this storefront.",
                    CommerceSeedPageFactory.CreateCatalog("Shop", "Browse featured products from this storefront.", featuredOnly: true, culture, defaultCulture)),
                ct);
            await SaveAndPublishAsync(
                CreatePage(siteId, culture, "search", "/shop/search", shop.Id, 1, "Search the shop", "Find published products available for this storefront.",
                    CommerceSeedPageFactory.CreateSearch(culture, defaultCulture)),
                ct);
            var products = await SaveAndPublishAsync(
                CreatePage(siteId, culture, "products", "/shop/products", shop.Id, 1, "Products", "Browse all published products for this storefront.",
                    CommerceSeedPageFactory.CreateCatalog("Products", "Browse all published products for this storefront.", culture: culture, defaultCulture: defaultCulture)),
                ct);

            foreach (var listing in listings.Where(listing => listing.IsPublished))
            {
                var composition = CommerceSeedPageFactory.CreateProduct(listing.Slug, culture, defaultCulture);
                await SaveAndPublishAsync(
                    CreatePage(siteId, culture, listing.Slug, $"/shop/products/{listing.Slug}", products.Id, 2,
                        "Storefront product", "Product availability and details are shown from the current storefront catalog.", composition),
                    ct);
            }

            log.LogInformation(
                "Commerce seed ensured {Count} published listings and PageEditor pages for site {SiteId} culture {Culture}",
                listings.Count,
                siteId,
                culture);
        }
    }

    private async Task<IReadOnlyList<ProductListingDocument>> EnsureCatalogAsync(
        SitesModel site,
        string culture,
        CancellationToken ct)
    {
        var existing = await session.Query<ProductListingDocument>()
            .Where(listing => listing.SiteId == site.Id && listing.Culture == culture)
            .ToListAsync(ct);
        if (existing.Count > 0)
        {
            return existing;
        }

        var faker = new Faker();
        var listings = new List<ProductListingDocument>();
        for (var index = 0; index < 12; index++)
        {
            var category = Categories[index % Categories.Length];
            var product = new ProductDocument
            {
                Id = Snowflake.NewId(),
                TenantId = site.TenantId,
                Name = faker.Commerce.ProductName(),
                Description = faker.Commerce.ProductDescription(),
                Sku = faker.Commerce.Ean13(),
                StockQuantity = faker.Random.Int(0, 500),
                IsActive = true,
                CreatedBy = "seed"
            };
            string? imageUrl = null;
            var photos = await pexels.SearchPhotosAsync($"product photography {category}", count: 1, ct: ct);
            if (photos.Count > 0)
            {
                imageUrl = await pexels.DownloadPhotoAsync(photos[0], "products", $"product-{product.Id}", ct);
            }

            var listing = new ProductListingDocument
            {
                Id = Snowflake.NewId(),
                TenantId = site.TenantId,
                SiteId = site.Id,
                ProductId = product.Id,
                Culture = culture,
                Slug = $"{CatalogSlug.Normalize(product.Name)}-{product.Id}",
                Name = product.Name,
                ShortDescription = product.Description,
                Description = product.Description,
                Category = category,
                ImageUrl = imageUrl,
                Price = Math.Round(faker.Random.Decimal(9.99m, 299.99m), 2),
                Currency = "USD",
                IsPublished = true,
                IsFeatured = index < 4,
                CreatedBy = "seed"
            };
            session.Store(product);
            session.Store(listing);
            listings.Add(listing);
        }

        await session.SaveChangesAsync(ct);
        return listings;
    }

    private async Task<IReadOnlyList<ProductListingDocument>> GetSiteCultureListingsAsync(
        long siteId,
        string culture,
        CancellationToken ct)
        => await session.Query<ProductListingDocument>()
            .Where(listing => listing.SiteId == siteId && listing.Culture == culture)
            .ToListAsync(ct);

    private async Task<PageDocument> SaveAndPublishAsync(PageDocument candidate, CancellationToken ct)
    {
        var existing = await FindPageAsync(candidate.SiteId, candidate.Culture, candidate.Path, ct);
        if (existing is not null)
        {
            // A page is merchant-owned as soon as it exists. Ordinary reseeding may
            // add missing starter routes, but it must never rewrite or republish an
            // existing draft or its public snapshot.
            return existing;
        }

        var saved = await pageContentService.SaveAsync(candidate, candidate.SiteId, ct);
        if (saved is Result<PageDocument, AeroError>.Failure saveFailure)
        {
            throw new InvalidOperationException($"Could not save seeded Commerce page '{candidate.Path}': {saveFailure.Error}");
        }

        var persisted = ((Result<PageDocument, AeroError>.Ok)saved).Value;
        var published = await pagePublishingWorkflowService.PublishNowAsync(persisted.Id, persisted.SiteId, ct);
        if (published is Result<bool, AeroError>.Failure publishFailure)
        {
            throw new InvalidOperationException($"Could not publish seeded Commerce page '{candidate.Path}': {publishFailure.Error}");
        }

        return persisted;
    }

    private async Task<PageDocument?> FindPageAsync(long siteId, string culture, string path, CancellationToken ct)
    {
        var pages = await session.Query<PageDocument>()
            .Where(page => page.SiteId == siteId && page.Culture == culture)
            .ToListAsync(ct);
        return pages.FirstOrDefault(page => string.Equals(page.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    private static PageDocument CreatePage(
        long siteId,
        string culture,
        string slug,
        string path,
        long? parentId,
        int depth,
        string title,
        string summary,
        (Aero.Cms.Html.HtmlPageContent Content, Aero.Cms.Abstractions.Pages.Composition.PageCompositionDocument Composition) document)
        => new()
        {
            Id = Snowflake.NewId(),
            SiteId = siteId,
            TranslationGroupId = null,
            Culture = culture,
            Kind = PageKind.Standard,
            RendererId = PageRendererIds.AeroComposition,
            Slug = slug,
            Path = path,
            ParentId = parentId,
            Depth = depth,
            Order = 0,
            Title = title,
            Summary = summary,
            SeoTitle = title,
            SeoDescription = summary,
            ShowInNavMenu = false,
            ShowHeaderNavigation = true,
            DraftContent = document.Content,
            DraftComposition = document.Composition
        };

    private static string NormalizeCulture(string? culture)
    {
        try
        {
            return CultureInfo.GetCultureInfo(culture ?? SitesModel.DefaultCultureName).Name;
        }
        catch (CultureNotFoundException)
        {
            return SitesModel.DefaultCultureName;
        }
    }

    private static IReadOnlyList<string> GetSupportedCultures(SitesModel site, string defaultCulture)
        => (site.SupportedCultures ?? [])
            .Append(defaultCulture)
            .Select(NormalizeCulture)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
