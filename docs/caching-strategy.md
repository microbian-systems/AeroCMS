# Caching Strategy: Content Update Invalidation Pipeline

> Architecture for standardized cache invalidation using Wolverine events, FusionCache (L1+L2), and ASP.NET Core response/output caching where appropriate.

## Cache Surface Profiles

| Surface | Cache Layers | Invalidation |
|---------|--------------|--------------|
| Public-facing CMS pages, blog, docs, sitemap, and public headless reads | Response caching, Output caching, FusionCache | Wolverine content events evict FusionCache keys/tags and ASP.NET OutputCache tags. Response cache behavior follows HTTP cache headers and generated response metadata. |
| Manager/admin UI and manager API data | FusionCache only | Wolverine manager/content events evict FusionCache keys/tags. Do not apply ASP.NET response caching or output caching to manager routes. |

Public request flow:

```text
HTTP client/proxy response cache
  -> ASP.NET Core OutputCache
    -> FusionCache data/object cache
      -> Marten DB
```

Manager/admin data flow:

```text
Manager UI / admin API
  -> FusionCache data/object cache
    -> Marten DB
```

## Architecture

```
ContentService.CRUD
  → bus.PublishAsync(ContentUpdatedEvent)
    → Wolverine In-Memory Bus
      → ContentUpdatedHandler (polymorphic, base event type)
        → CacheInvalidationService (single implementation)
          1. FusionCache.RemoveAsync(key)        // evict old slug + new slug
          2. FusionCache.RemoveByTagAsync(tag)   // evict FusionCache tag group
          3. IOutputCacheStore.EvictByTagAsync   // public surfaces only: evict ASP.NET OutputCache group
```

## Event Hierarchy

All events live in `Aero.Cms.Modules.Cache/Events/`.

```
AeroEventMessageBase (Aero.Events)
  └── ContentUpdatedEvent (abstract)
        ├── long ContentId
        ├── long SiteId
        ├── string NewSlug
        ├── string? OldSlug
        ├── abstract string ContentType  // "page" | "blog" | "docs"
        │
        ├── PageContentUpdatedEvent       → ContentType = "page"
        ├── BlogPostContentUpdatedEvent   → ContentType = "blog"
        └── DocsPageContentUpdatedEvent   → ContentType = "docs"
```

Each event is a `sealed record` inheriting from `ContentUpdatedEvent`. Adding a new content type means creating one event record and one config entry.

## Invalidation Service

`ICacheInvalidationService` → `FusionCacheInvalidationService`

Located in `Aero.Cms.Modules.Cache/Services/`.

### Invalidation Logic

```csharp
public async Task InvalidateAsync(ContentUpdatedEvent evt, CancellationToken ct)
{
    var cfg = Configs[evt.ContentType]; // Map: page→pages-list, blog→blog-index, docs→docs-index

    // 1. Evict old slug (slug changed or content deleted)
    if (evt.OldSlug is not null
        && !string.Equals(evt.OldSlug, evt.NewSlug, StringComparison.OrdinalIgnoreCase))
        await fusionCache.RemoveAsync($"cms:{evt.ContentType}:{evt.SiteId}:{evt.OldSlug}", token: ct);

    // 2. Evict new slug (may have been cached from prior request)
    await fusionCache.RemoveAsync($"cms:{evt.ContentType}:{evt.SiteId}:{evt.NewSlug}", token: ct);

    // 3. Evict all FusionCache entries tagged with the output cache tag
    await fusionCache.RemoveByTagAsync(cfg.OutputCacheTag, ct);

    // 4. Evict ASP.NET OutputCache entries for this content type.
    // Manager/admin routes do not use output caching; this is for public surfaces only.
    await outputCacheStore.EvictByTagAsync(cfg.OutputCacheTag, ct);
}
```

### Config Map

```csharp
private static readonly Dictionary<string, CacheConfig> Configs = new(StringComparer.OrdinalIgnoreCase)
{
    ["page"] = new("cms:pages", "pages-list"),
    ["blog"] = new("cms:blog",  "blog-index"),
    ["docs"] = new("cms:docs",  "docs-index"),
};
```

### Key Conventions

| Scope | Pattern | Example |
|-------|---------|---------|
| FusionCache entry key | `cms:{type}:{siteId}:{slug}` | `cms:pages:1:about-us` |
| FusionCache tag | Same as output cache tag | `pages-list` |
| OutputCache tag | Defined in `OutputCacheModule` for public routes only | `pages-list`, `blog-index`, `docs-index` |
| FusionCache backplane | Redis pub/sub (managed by FusionCache) | Auto |

## Handler

Single polymorphic Wolverine handler in `Aero.Cms.Modules.Cache/Handlers/`.

```csharp
[WolverineHandler]
public sealed class ContentUpdatedHandler(ICacheInvalidationService cacheInvalidation)
{
    public async Task Handle(ContentUpdatedEvent evt, CancellationToken ct)
    {
        await cacheInvalidation.InvalidateAsync(evt, ct);
    }
}
```

Wolverine dispatches all three event types to this handler because they inherit from `ContentUpdatedEvent`.

## Publishing Events from Services

### PageContentService — `SaveAsync()` (already has `IMessageBus bus`)

```csharp
// After session.SaveChangesAsync(), alongside existing SlugUpdated:
await bus.PublishAsync(new PageContentUpdatedEvent
{
    ContentId = page.Id,
    SiteId = page.SiteId,
    NewSlug = page.Slug,
    OldSlug = existingPage?.Slug,
});
```

Also add to `DeleteAsync()`:

```csharp
await bus.PublishAsync(new PageContentUpdatedEvent
{
    ContentId = page.Id,
    SiteId = page.SiteId,
    NewSlug = page.Slug,
    OldSlug = page.Slug,  // same = "this key is now gone"
});
```

### BlogPostContentService — `SaveAsync()` + `DeleteAsync()`

Same pattern — inject `IMessageBus` and publish `BlogPostContentUpdatedEvent`.

### DocsService — `SaveAsync()` + `DeleteAsync()`

Same pattern — inject `IMessageBus` and publish `DocsPageContentUpdatedEvent`.

## Module Registration

All cache invalidation logic lives in the existing `Aero.Cms.Modules.Cache` module.

### CacheModule.cs — Additions

```csharp
// 1. Add .WithTags() to the FusionCache builder (required for RemoveByTagAsync)
var cacheBuilder = services.AddFusionCache()
    .WithDefaultEntryOptions(...)
    .WithSystemTextJsonSerializer()
    .WithRegisteredDistributedCache()
    .WithTags();   // ← NEW — enables RemoveByTagAsync()

// 2. Register the invalidation service
services.AddSingleton<ICacheInvalidationService, FusionCacheInvalidationService>();

// 3. Register the events and handler (Wolverine auto-discovers [WolverineHandler])
// No explicit registration needed — source generator discovers the handler.
```

### Module Dependencies

`CacheModule` already depends on nothing for FusionCache. The handler needs `IOutputCacheStore` which is registered by `AddOutputCache()` in `OutputCacheModule`. Since modules are loaded in dependency order, `CacheModule` should declare `OutputCacheModule` as a dependency so it loads after output cache is registered:

```csharp
public override IReadOnlyList<string> Dependencies => [nameof(OutputCacheModule)];
```

## FusionCache + IDistributedCache (Redis/Garnet)

Current `CacheModule.cs` already sets up the full stack:

```csharp
services.AddDistributedMemoryCache();  // fallback L2 when no external cache
// OR in production:
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = config!.GetConnectionString("Cache") ?? "localhost:6379";
    options.InstanceName = "AeroCms:";
});

services.AddFusionCache()
    .WithDefaultEntryOptions(new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(5) })
    .WithSystemTextJsonSerializer()
    .WithRegisteredDistributedCache(ignoreMemoryDistributedCache: false)
    .WithTags();  // required for RemoveByTagAsync
```

**Redis/Garnet compatibility:** Both speak the Redis wire protocol. Garnet uses the same `StackExchange.Redis` library. Just point `AddStackExchangeRedisCache()` at the Garnet endpoint and it works.

**Current `CacheModule.cs` reads cache config from:**
- `AeroCms:Bootstrap:CacheMode` — `"Memory"` (default, no external cache) or `"Embedded"` (uses local Garnet/Redis on port 33333)
- Connection string from config when in production

## Middleware Pipeline Order

```csharp
// Program.cs
var app = builder.Build();

app.UseExceptionHandler();
app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseResponseCaching();       // Public HTTP/client/proxy Cache-Control behavior
app.UseOutputCache();           // Public server-side full-response cache
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
// FusionCache is NOT middleware — it is a DI service consumed by handlers
```

- `UseResponseCaching()` participates in public HTTP cache semantics through response headers.
- `UseOutputCache()` caches full public HTTP responses server-side.
- FusionCache operates at the DI/service level, not the middleware level.
- Manager/admin routes must be excluded from response and output cache policies; manager caching belongs in FusionCache-backed services and typed clients.

## Module File Structure

```
src/Aero.Cms.Modules.Cache/                         (EXISTING, UPDATED)
├── Aero.Cms.Modules.Cache.csproj
├── CacheModule.cs                                   ← .WithTags() + invalidation service registration
├── PageCacheHooks.cs                               (unchanged)
├── Events/                                          ← NEW
│   ├── ContentUpdatedEvent.cs                       (abstract base)
│   ├── PageContentUpdatedEvent.cs
│   ├── BlogPostContentUpdatedEvent.cs
│   └── DocsPageContentUpdatedEvent.cs
├── Handlers/                                        ← NEW
│   └── ContentUpdatedHandler.cs                     (polymorphic Wolverine handler)
└── Services/                                        ← NEW
    ├── ICacheInvalidationService.cs
    └── FusionCacheInvalidationService.cs             (implementation)
```

## Services That Need Updates

| Service | Change |
|---------|--------|
| `PageContentService.SaveAsync()` | Publish `PageContentUpdatedEvent` + `DeleteAsync()` |
| `BlogPostContentService.SaveAsync()` | Publish `BlogPostContentUpdatedEvent` + `DeleteAsync()` |
| `DocsService.SaveAsync()` | Publish `DocsPageContentUpdatedEvent` + `DeleteAsync()` |
| `Program.cs` | Add `app.UseResponseCaching()` before `app.UseOutputCache()` if not present |

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| **Slug change** | Both old + new slug keys evicted from FusionCache + output cache tag evicted |
| **Delete** | `NewSlug == OldSlug` → single slug key evicted + tag evicted |
| **Unpublish** | No cache action needed — draft pages return 404 publicly via `PublicationState` filter |
| **Cold Redis / first start** | `RemoveAsync`/`EvictByTagAsync` are no-ops on missing keys |
| **Garnet vs Redis** | Same wire protocol — swap connection string, same code |
| **Multi-site** | All keys include `SiteId` — tenant-scoped eviction |
| **Tag cross-site bleed** | Tags are shared across sites. If this is a concern, qualify with site: `pages-list:site1` |
| **Manager/admin routes** | FusionCache only. Do not apply response caching or output caching policies. |
| **Concurrent saves** | Wolverine processes sequentially per message. FusionCache `RemoveAsync` is atomic. Race window handled by FusionCache fail-safe |
| **Missing `.WithTags()`** | `RemoveByTagAsync` throws. The FusionCache builder MUST include `.WithTags()` |

## Package Dependencies

The `Aero.Cms.Modules.Cache.csproj` already has `ZiggyCreatures.FusionCache` and related packages. No new packages needed for the invalidation logic — it uses `Microsoft.AspNetCore.OutputCaching` (already referenced transitively) and `Aero.Events` (already referenced via `Aero.Cms.Abstractions`).

## Implementation Order

1. **Create event types** — `ContentUpdatedEvent` base + 3 sealed records in `Cache/Events/`
2. **Create invalidation service** — `ICacheInvalidationService` + `FusionCacheInvalidationService` in `Cache/Services/`
3. **Create handler** — `ContentUpdatedHandler` in `Cache/Handlers/`
4. **Update CacheModule.cs** — add `.WithTags()` to FusionCache builder, register invalidation service, add `OutputCacheModule` dependency
5. **Publish from PageContentService** — add `bus.PublishAsync(new PageContentUpdatedEvent {...})` in SaveAsync + DeleteAsync
6. **Publish from BlogPostContentService** — same pattern
7. **Publish from DocsService** — same pattern
8. **Add ResponseCaching middleware** — `app.UseResponseCaching()` in Program.cs if not present
9. **Verify build** — `dotnet build` passes with no errors
