# Sitemap Module Implementation Plan

## Goal
Implement a Sitemap module (`Aero.Cms.Modules.SiteMap`) that dynamically generates an XML sitemap from Pages, Blog posts, and Docs content, cached via FusionCache and invalidated through Wolverine events.

## Architecture

### Module: `Aero.Cms.Modules.SiteMap`

**Current state**: Stub module with `SiteMapModule : AeroModuleBase` and an empty `.csproj` referencing only `Aero.Cms.Core`.

**Target state**: Fully functioning module with:
- A `SiteMapModule : AeroWebModule` that registers services and maps endpoints
- A `SiteMapService` that gathers content from all 3 sources
- FusionCache-backed caching (L1 memory + optional L2 Garnet/Redis)
- Wolverine event handlers that invalidate the cached sitemap when content changes

---

## Step-by-Step Plan

### 1. Update `Aero.Cms.Modules.SiteMap.csproj`

Add project references to:
- `Aero.Cms.Modules.Pages` — for `IPageContentService` / `PageDocument` queries
- `Aero.Cms.Modules.Blog` — for `IBlogPostContentService` / `BlogPostDocument` queries
- `Aero.Cms.Modules.Docs` — for `IDocsService` / `DocsPage` queries
- `Aero.Cms.Abstractions` — for Wolverine event types (`PageCreated`, `PageUpdated`, `PageDeleted`, `PostCreated`, `PostUpdated`, `PostDeleted`, `DocCreated`, `DocUpdated`, `DocDeleted`)
- `Aero.Caching` — for `FusionCacheClient` / `ICacheService` (or inject `IFusionCache` directly)
- `Aero.Cms.Core` — already present; keep for shared infrastructure

### 2. Change `SiteMapModule` to `AeroWebModule`

- Change base class from `AeroModuleBase` to `AeroWebModule`
- Register `ISiteMapService` → `SiteMapService` in `ConfigureServices`
- Register `SitemapInvalidationHandler` with Wolverine
- Call `builder.MapSitemapApi()` in `RunAsync(IEndpointRouteBuilder)`

**Dependencies**: depends on Pages, Blog, Docs modules (they must load first).

### 3. Create `ISiteMapService` + `SiteMapService`

```csharp
public interface ISiteMapService
{
    Task<Result<string, AeroError>> BuildSitemapAsync(CancellationToken ct);
}
```

`SiteMapService` implementation:

1. **Check cache**: `IFusionCache.TryGetAsync<string>("sitemap:xml")` — return cached XML if found
2. **On cache miss**, query all 3 sources in parallel:
   - `IPageContentService.GetAllPagesAsync()` → filter `PublicationState == Published` → map to sitemap entries
   - Direct Marten query on `BlogPostDocument` → filter `Published`
   - `IDocsService.GetAllAsync()` → filter `IsPubliclyVisible`
3. **Merge** into unified list sorted by priority (Pages > Blog > Docs)
4. **Render** XML string
5. **Cache**: `IFusionCache.SetAsync("sitemap:xml", xml, opts with 5-min TTL)`
6. **Return** XML string

### 4. Create `SitemapApi.cs` — Minimal API Endpoint

```csharp
public static class SitemapApi
{
    public static void MapSitemapApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap.xml", GetSitemap)
            .WithName("GetSitemap")
            .WithTags("SEO");
    }

    private static async Task<IResult> GetSitemap(
        [FromServices] ISiteMapService sitemapService,
        CancellationToken ct)
    {
        var result = await sitemapService.BuildSitemapAsync(ct);
        if (result is Result<string, AeroError>.Ok ok)
            return Results.Content(ok.Value, "application/xml", Encoding.UTF8);
        return Results.Problem("Failed to generate sitemap");
    }
}
```

### 5. Create `SitemapInvalidationHandler` — Wolverine Event Handler

```csharp
public class SitemapInvalidationHandler(IFusionCache cache) : IWolverineHandler
{
    public Task Handle(PageCreated _) => Invalidate();
    public Task Handle(PageUpdated _) => Invalidate();
    public Task Handle(PageDeleted _) => Invalidate();
    public Task Handle(PostCreated _) => Invalidate();
    public Task Handle(PostUpdated _) => Invalidate();
    public Task Handle(PostDeleted _) => Invalidate();
    public Task Handle(DocCreated _) => Invalidate();
    public Task Handle(DocUpdated _) => Invalidate();
    public Task Handle(DocDeleted _) => Invalidate();

    private Task Invalidate() => cache.RemoveAsync("sitemap:xml");
}
```

### 6. Add Wolverine Events to Docs Service

**`DocsService.cs`**: Inject `IMessageBus`, publish events in `SaveAsync` and `DeleteAsync`:

- `SaveAsync` — detect if new or existing → publish `DocCreated` or `DocUpdated`
- `DeleteAsync` — publish `DocDeleted`

This mirrors the existing pattern in `PageContentService.cs` (which publishes `SlugUpdated`).

**`DocsModule.cs`**: Register `IMessageBus` for `DocsService` (already available via DI — the `DocsService` constructor just needs the parameter).

### 7. Sitemap Entry Model

```csharp
public sealed record SitemapEntry
{
    public string Loc { get; init; }       // e.g. "/about-us"
    public DateTimeOffset? LastMod { get; init; }
    public ChangeFrequency ChangeFreq { get; init; } = ChangeFrequency.Weekly;
    public double Priority { get; init; } = 0.5;
}

public enum ChangeFrequency
{
    Always, Hourly, Daily, Weekly, Monthly, Yearly, Never
}
```

### 8. XML Rendering

Standard `System.Xml.Linq` or manual string building with `XElement`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>https://{host}/{slug}</loc>
    <lastmod>2026-04-29</lastmod>
    <changefreq>weekly</changefreq>
    <priority>0.8</priority>
  </url>
</urlset>
```

Priority mapping:
| Content Type | Priority | Change Frequency |
|---|---|---|
| Homepage (`/`) | 1.0 | Daily |
| Standard Pages | 0.8 | Weekly |
| Blog Posts | 0.6 | Weekly |
| Docs Pages | 0.5 | Monthly |

---

## Caching Strategy

| Concern | Decision |
|---|---|
| **Cache key** | `"sitemap:xml"` |
| **TTL** | 5 minutes (configurable) |
| **Cache service** | `IFusionCache` (injected directly) — already configured in `AeroAppServerExtensions.cs` with `.WithRegisteredDistributedCache()` |
| **L1** | In-memory (always on via FusionCache) |
| **L2** | Garnet/Redis if configured, else `MemoryDistributedCache` fallback |
| **Invalidation** | Wolverine events → remove cache key → next request rebuilds |
| **Cache stampede protection** | FusionCache handles this natively (factory soft/hard timeouts, fail-safe) |

---

## Files to Create

| File | Purpose |
|---|---|
| `src/Aero.Cms.Modules.SiteMap/ISiteMapService.cs` | Interface for sitemap service |
| `src/Aero.Cms.Modules.SiteMap/SiteMapService.cs` | Implementation: query, cache, render XML |
| `src/Aero.Cms.Modules.SiteMap/SitemapApi.cs` | Minimal API endpoint `GET /sitemap.xml` |
| `src/Aero.Cms.Modules.SiteMap/SitemapEntry.cs` | Sitemap entry model + enum |
| `src/Aero.Cms.Modules.SiteMap/SitemapInvalidationHandler.cs` | Wolverine event handler |

## Files to Modify

| File | Change |
|---|---|
| `src/Aero.Cms.Modules.SiteMap/Aero.Cms.Modules.SiteMap.csproj` | Add project references |
| `src/Aero.Cms.Modules.SiteMap/SiteMapModule.cs` | Extend `AeroWebModule`, register services + endpoints |
| `src/Aero.Cms.Modules.Docs/DocsService.cs` | Inject `IMessageBus`, publish DocCreated/Updated/Deleted |
| `src/Aero.Cms.Modules.Docs/DocsModule.cs` | Register `IMessageBus` in DocsService constructor |

---

## Execution Order

1. Update `.csproj` with project references
2. Create model files (`SitemapEntry.cs`)
3. Create `ISiteMapService` + `SiteMapService`
4. Create `SitemapApi.cs`
5. Create `SitemapInvalidationHandler.cs`
6. Update `SiteMapModule.cs`
7. Update `DocsService.cs` to publish events
8. Build and verify
