
> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). Marten
> was migrated out in [`surrealdb-marten-port.md`](surrealdb-marten-port.md).
> This document is a historical implementation record; its Marten/PostgreSQL
> persistence details do not reflect the current stack.

# Seed Refactor: Pexels Integration + Commerce Seed + 404 Page

## Objective

Replace the placeholder `static.photos` image service with real Pexels images, seed commerce products, add a `/oops` 404 page with alias, and create a `/shop` landing page.

---

## 1. New Files

### `src/Aero.Services/Images/IPexelsService.cs`
Interface for Pexels API:
```csharp
Task<IReadOnlyList<PexelsPhoto>> SearchPhotosAsync(string query, int count = 5);
Task<PexelsPhoto?> GetPhotoByIdAsync(int id);
Task<IReadOnlyList<PexelsVideo>> SearchVideosAsync(string query, int count = 1);
Task<string> DownloadPhotoAsync(PexelsPhoto photo, string subfolder, string filename);
Task<string> DownloadVideoAsync(PexelsVideo video, string subfolder, string filename);
```

### `src/Aero.Services/Images/PexelsService.cs`
- HttpClient-based, reads `PEXELS_API_KEY` from environment
- Base URL: `https://api.pexels.com/v1/` (photos) / `https://api.pexels.com/videos/` (videos)
- Logs each request (query, count, latency, result count) via `ILogger<PexelsService>`
- Handles rate limiting (200 req/hr on free tier — batch downloads with delay)

### `src/Aero.Services/Images/PexelsMediaDownloader.cs`
- Downloads image/video bytes from a URL
- Saves to `wwwroot/media/{subfolder}/{filename}`
- Creates subfolder if it doesn't exist
- Returns the relative path (`/media/{subfolder}/{filename}`)

### `src/Aero.Services/Images/PexelsModels.cs`
Response DTOs:
```csharp
public record PexelsSearchResult(IReadOnlyList<PexelsPhoto> Photos, int TotalResults, int Page);
public record PexelsPhoto(int Id, string Url, PexelsSrc Src, string Alt);
public record PexelsSrc(string Original, string Large, string Medium, string Small, string Tiny);
public record PexelsVideoSearchResult(IReadOnlyList<PexelsVideo> Videos);
public record PexelsVideo(int Id, string Url, string Image, IReadOnlyList<PexelsVideoFile> VideoFiles);
public record PexelsVideoFile(int Id, string Quality, string Link, int Width, int Height);
```

### `src/Aero.Cms.Modules.Commerce/Data/CommerceSeedService.cs`
- Inject: `IPexelsService`, `IDocumentSession`, `IWebHostEnvironment`, `ILogger<CommerceSeedService>`, `IPageContentService`
- Method: `SeedAsync(long siteId, SeedDatabaseRequest request, CancellationToken ct)`
- Steps:
  1. Fetch Pexels images for each product category (Clothing, Equipment, Accessories, Footwear)
  2. Generate 12 products via Bogus with varying prices/descriptions
  3. Download Pexels photo for each product to `wwwroot/media/products/product-{id}.jpg`
  4. Register each as `MediaAsset` in Marten
  5. Store all `ProductDocument`s in Marten
  6. Download skateboard video from Pexels (ID 10118302) → `wwwroot/media/videos/skateboard-promo.mp4`
  7. Register video as `MediaAsset`
  8. Build + save `/shop` landing page via `pageContentService`

### `src/Aero.Cms.Modules.Commerce/Areas/Commerce/Pages/ShopHome.cshtml` + `.cshtml.cs`
- Route: `/shop`
- View: Welcome hero, 2-3 paragraphs, featured products grid (3 items), image gallery (6 Pexels photos)
- Page model injects `IProductService` to load featured products

---

## 2. Modified Files

### `src/Aero.Cms.Modules.Setup/SeedDataService.cs`
Changes:
- Constructor: add `IPexelsService pexelsService` parameter
- `SeedStarterContentAsync`: accept `long siteId`, pass to sub-methods
- `BuildStarterBlogContent`: accept `IPexelsService`, replace `staticPhotosClient.GetPhotoUrl(...)` with Pexels-fetched images:
  ```csharp
  // Instead of: staticPhotosClient.GetPhotoUrl("technology")
  // Do: download Pexels photo for each post
  ```
- After existing page/blog seeding, add:
  1. Build + save `/oops` page (standard PageDocument with friendly 404 message)
  2. Store `AliasDocument` mapping `/404` → `/oops`
  3. Call `CommerceSeedService.SeedAsync(siteId, request, ct)`

### `src/Aero.Cms.Modules.Commerce/CommerceModule.cs`
- `ConfigureServices`: register `CommerceSeedService`
- Route registration: add `/shop` → `ShopHome` page

### `src/Aero.Cms.Services/Aero.Cms.Services.csproj`
- No changes needed — `PexelsService` lives in `Aero.Services.Images` alongside existing `IStaticPhotosClient`

---

## 3. Implementation Order

| Step | File | Depends On |
|------|------|-----------|
| 1 | Pexels models | Nothing |
| 2 | Pexels interface + service | Step 1 |
| 3 | PexelsMediaDownloader | Step 2 |
| 4 | CommerceSeedService | Steps 2-3 |
| 5 | ShopHome page | Step 4 (for ProductService) |
| 6 | SeedDataService updates | Steps 4-5 |
| 7 | CommerceModule route + registration | Step 5 |
| 8 | Build + verify | All above |

---

## 4. 12 Seed Products (Bogus-generated)

| Category | Count | Pexels Search Query |
|----------|-------|---------------------|
| Clothing | 3 | "outdoor clothing hiking apparel" |
| Equipment | 3 | "camping hiking equipment gear" |
| Accessories | 3 | "outdoor accessories gear" |
| Footwear | 3 | "hiking boots trail shoes" |

Each product gets: Bogus `Commerce.ProductName()`, `Commerce.ProductDescription()`, `Random.Decimal(9.99, 299.99)`, `Random.Int(0, 500)` for stock.

---

## 5. `/oops` Page Content

Title: "Page Not Found"
Slug: `/oops`
Summary: "The page you're looking for doesn't exist or has been moved."
Body: Friendly message with a link back to the homepage, using the same block structure as other seeded pages (BoringHeroBlock + RichTextBlock).

---

## 6. Pexels Stats Logging

Each Pexels API call logs:
- Timestamp
- Endpoint (search, photo by id, video, download)
- Query parameters (search term, count)
- Response time (ms)
- Result count
- Warnings (empty results, rate limit approaching)

This satisfies the "request statistics" requirement for future use in the manager area.

---

## 7. Risks

| Risk | Mitigation |
|------|-----------|
| Pexels API rate limit (200 req/hr) | Cache search results; blog posts share images; batch with delays |
| Pexels API key missing at seed time | Log warning and skip Pexels downloads; fall back to placeholder images |
| Large video download (skateboard) | Download async; stream to disk |
| Media assets already exist on re-seed | Check if file exists before downloading; skip if present |
