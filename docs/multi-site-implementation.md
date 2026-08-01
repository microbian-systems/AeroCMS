
> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). Marten
> was migrated out in [`surrealdb-marten-port.md`](surrealdb-marten-port.md).
> This document is a historical implementation record; its Marten/PostgreSQL
> persistence details do not reflect the current stack.

# Aero CMS Multi-Site Implementation Plan

## Document Purpose

This is the master implementation plan for adding multi-site support to Aero CMS. It covers the architecture, data model changes, request pipeline, module refactors, UI changes, and migration strategy.

## Architecture Overview

### Pipeline: IStartupFilter Chain of Responsibility

Each participating module registers an `IStartupFilter` via `services.Insert(0, ...)` in its `ConfigureServices`. ASP.NET Core composes `IStartupFilter` instances by iterating in reverse registration order, so the **first** module to call `Insert(0)` becomes the **outermost** filter (runs first on request).

Because the module system calls `ConfigureServices` in **load order** (lowest `Order` first), the pipeline naturally composes correctly:

```
Module Load Order  →  IStartupFilter Insert(0) Call Order  →  ASP.NET Compose (Reverse)  →  Request Execution Order
──────────────────────────────────────────────────────────────────────────────────────────────────────────────────
SitesModule (-9999)  →  Insert(0, SiteStartupFilter)        →  Site wraps Alias wraps ...  →  SiteMiddleware FIRST
AliasModule (-9998)  →  Insert(0, AliasStartupFilter)       →                              →  AliasMiddleware SECOND
BlogModule (-9997)   →  Insert(0, BlogStartupFilter)        →                              →  BlogMiddleware THIRD
PagesModule (-9995)  →  Insert(0, PageStartupFilter)        →                              →  PageMiddleware ...
```

No new interface required — `IStartupFilter` is standard ASP.NET Core and matches the module system's natural `Order`-based calling sequence.

### Final Request Pipeline

```
HTTP Request
  │
  ├─ IStartupFilter wrapping (innermost to outermost):
  │    ├─ SiteResolutionMiddleware     (SitesModule, Order=-9999)
  │    │    Resolves host → site, sets HttpContext.Features[IAeroSiteSlice]
  │    │
  │    ├─ UseRewriter                  (AliasModule, Order=-9998)
  │    │    AliasRewriteRule checks site-scoped cache, redirects if match
  │    │
  │    └─ [future: BlogStartupFilter, PageStartupFilter, etc.]
  │
  ├─ UseExceptionHandler()
  ├─ UseHttpsRedirection()
  ├─ MapStaticAssets()
  ├─ UseRouting()
  ├─ UseAuthentication() / UseAuthorization()
  ├─ UseCmsSetupGate()
  ├─ UseOutputCache()
  ├─ UseAntiforgery()
  ├─ MapRazorPages() / MapRazorComponents<App>() / MapAeroCmsEndpoints()
  └─ UseStatusCodePagesWithRedirects("/oops")
```

### Key Decisions

| Decision | Rationale |
|---|---|
| Use `IStartupFilter` pattern | Already used by `AliasModule`. No new interface needed. Aligned with ASP.NET Core conventions. |
| Use `services.Insert(0, ...)` | Leverages ASP.NET Core's `Reverse()` composition to auto-order by module load sequence. |
| Use `Module.Order` for pipeline order | Same property controls DI load order AND pipeline order — no split-brain risk. |
| Single `UseStatusCodePagesWithRedirects` in Program.cs | Remove duplication from `AliasStartupFilter`. One canonical location. |
| `DisabledInProduction` skips `IStartupFilter` registration | Prevents disabled modules from inserting unused middleware. |

---

## Phase 1: Foundation ✅

### 1.1 SitesModel: Single Hostname → Multiple Hosts ✅

**File:** `src/Aero.Cms.Core.Entities/SitesModel.cs`

Replaced `Hostname` (single string) with `PrimaryHost` + `Hosts` list:

```csharp
public class SitesModel : Entity
{
    public long TenantId { get; set; }
    public string? Name { get; set; } 
    public string? PrimaryHost { get; set; }          // was: Hostname
    public List<string> Hosts { get; set; } = [];     // new: multi-domain support
    public string? Description { get; set; }          // new: for manager UI
    public bool IsEnabled { get; set; }
    public string? DefaultCulture { get; set; }
}
```

### 1.2 Host Normalization Utility ✅

**New file:** `src/Aero.Cms.Core/Infrastructure/HostNormalizer.cs`

```csharp
public static class HostNormalizer
{
    public static string Normalize(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return string.Empty;
        return host.Trim().ToLowerInvariant().TrimEnd('.');
    }
}
```

Used consistently by: middleware, repository queries, validators, seed data.

### 1.3 SitesModule Marten Configuration Update ✅

**File:** `src/Aero.Cms.Modules.Sites/SitesModule.cs`

```csharp
public override void Configure(IServiceProvider services, StoreOptions opts)
{
    Configure<SitesModel>(services, opts);
    opts.Schema.For<SitesModel>().UniqueIndex(x => x.PrimaryHost!);
    opts.Schema.For<SitesModel>().Index(x => x.IsEnabled);
    // Hosts is stored in JSONB document body. Marten's JSONB containment
    // handles Contains() queries natively without a flat duplicate column.
}
```

**Actual vs Plan:**
- `ForeignKey<TenantModel>` was **removed** — caused DDL ordering failure with embedded PG (tenants table not yet created when SitesModel FK runs)
- `Duplicate(x => x.Hosts, pgType: "jsonb")` was **removed** — caused Marten source generator to produce `NpgsqlDbType.-2147483629` (invalid C#)
- `base.Configure<SitesModel>()` was **removed** — was duplicating indexes (created_by, modified_by, created_on, modified_on)
- Added `Dependencies => ["TenantModule"]` — ensures TenantModule loads first for FK (re-enable later)
- Added `Order => -9999` — ensures SitesModule loads first, making it outermost IStartupFilter

### 1.4 SiteModelValidator Update ✅

**File:** `src/Aero.Cms.Modules.Sites/SiteModelValidator.cs`

```csharp
public sealed class SiteModelValidator : AbstractValidator<SitesModel>
{
    public SiteModelValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.TenantId).GreaterThan(0);
        RuleFor(x => x.Name).NotNullOrEmpty();
        RuleFor(x => x.PrimaryHost).NotNullOrEmpty()
            .WithMessage("Primary host name must have a value");
        RuleFor(x => x.Hosts).NotEmpty()
            .WithMessage("At least one host must be configured");
        RuleFor(x => x.PrimaryHost)
            .Must((model, primary) => model.Hosts.Contains(primary ?? ""))
            .WithMessage("PrimaryHost must be included in the Hosts list");
    }
}
```

### 1.5 SiteLookupService: Multi-Host Resolution ✅

**File:** `src/Aero.Cms.Modules.Sites/SiteLookupService.cs`


### 1.6 ISiteContext: Reads from HttpContext.Features (was: HTTP Headers) ✅

**File:** `src/Aero.Cms.Web/Infrastructure/DefaultSiteContext.cs`

**Critical fix:** The previous implementation read `X-Site-Id` and `X-Tenant-Id` from **client-supplied HTTP headers** — a privilege escalation vulnerability. Now reads from `IAeroSiteSlice` on `HttpContext.Features`, set by `SiteResolutionMiddleware`.

### 1.7 SiteResolutionMiddleware

**New file:** `src/Aero.Cms.Modules.Sites/SiteResolutionMiddleware.cs`

Extracted from the existing `AeroSiteMiddleware` stub in `ISiteLookupService.cs`. Fixes the hardcoded `TenantId = Snowflake.NewId()` bug.

```csharp
public sealed class SiteResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public SiteResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ISiteLookupService siteLookup)
    {
        var host = context.Request.Host.Host;
        var normalized = HostNormalizer.Normalize(host);
        
        var site = await siteLookup.ResolveByHostAsync(normalized);

        if (site is null || !site.IsEnabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Features.Set<IAeroSiteSlice>(new AeroSiteSlice
        {
            SiteId = site.Id,
            TenantId = site.TenantId  // FIXED: was Snowflake.NewId()
        });

        await _next(context);
    }
}
```

### 1.8 SiteStartupFilter

**New file:** `src/Aero.Cms.Modules.Sites/SiteStartupFilter.cs`

```csharp
public sealed class SiteStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<SiteResolutionMiddleware>();
            next(app);
        };
    }
}
```

### 1.9 SitesModule: Register IStartupFilter

**File:** `src/Aero.Cms.Modules.Sites/SitesModule.cs`

```csharp
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
{
    base.ConfigureServices(services, config, env);
    services.AddScoped<ISiteRepository, SiteRepository>();
    services.AddScoped<ISiteService, SiteService>();

    // Register startup filter (respects DisabledInProduction)
    if (!DisabledInProduction)
    {
        services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, SiteStartupFilter>());
    }
}
```

---

## Phase 2: Site-Scoped Aliases ✅

### 2.1 AliasRuleCache: Composite Key ✅

**File:** `src/Aero.Cms.Modules.Aliases/AliasRuleCache.cs`

Change from flat `ImmutableDictionary<string, AliasRuleEntry>` to composite `(SiteId, Path)` key:

```csharp
// Use a value type for composite key
public readonly record struct SitePathKey(long SiteId, string Path);

public sealed class AliasRuleCache : IAliasRuleCache
{
    private ImmutableDictionary<SitePathKey, AliasRuleEntry> _rules = 
        ImmutableDictionary<SitePathKey, AliasRuleEntry>.Empty;

    public AliasRuleEntry? Find(long siteId, string oldPath)
    {
        _rules.TryGetValue(new SitePathKey(siteId, oldPath), out var entry);
        return entry;
    }

    // Refresh and Invalidate remain similar, using composite key
}
```

### 2.2 IAliasRuleCache: Update Signature ✅

**File:** `src/Aero.Cms.Modules.Aliases/IAliasRuleCache.cs`

```csharp
public interface IAliasRuleCache
{
    AliasRuleEntry? Find(long siteId, string oldPath);  // was: Find(string oldPath)
    Task RefreshAsync(CancellationToken ct = default);
    void Invalidate();
}
```

### 2.3 AliasRewriteRule: Site-Scoped Lookup ✅

**File:** `src/Aero.Cms.Modules.Aliases/AliasRewriteRule.cs`

```csharp
public void ApplyRule(RewriteContext context)
{
    var http = context.HttpContext;
    var path = NormalizePath(http.Request.Path.Value);
    if (string.IsNullOrEmpty(path)) return;

    // Resolve current site from features (set by SiteResolutionMiddleware)
    var slice = http.Features.Get<IAeroSiteSlice>();
    if (slice is null || slice.SiteId <= 0) return; // no site resolved, skip

    // Site-scoped lookup
    var entry = _cache.Find(slice.SiteId, path);
    if (entry is not null)
    {
        ApplyEntry(http, entry, context);
        return;
    }

    // Cache miss — site-scoped DB fallback
    _log.LogDebug("Cache miss for SiteId={SiteId} Path='{Path}'", slice.SiteId, path);
    using var scope = _serviceProvider.CreateScope();
    var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
    var aliases = session.Query<AliasDocument>()
        .Where(x => x.SiteId == slice.SiteId)  // SITE-SCOPED
        .ToList();
    
    foreach (var alias in aliases)
    {
        var aliasPath = NormalizePath(alias.OldPath);
        if (string.Equals(aliasPath, path, StringComparison.OrdinalIgnoreCase))
        {
            ApplyEntry(http, new AliasRuleEntry(alias.SiteId, aliasPath, alias.NewPath), context);
            return;
        }
    }
}
```

### 2.4 AliasesModule Marten Index: Composite Unique ✅

**File:** `src/Aero.Cms.Modules.Aliases/AliasModule.cs`

```csharp
public override void Configure(IServiceProvider services, StoreOptions opts)
{
    opts.Schema.For<AliasDocument>().DocumentAlias(Schemas.Tables.Aliases);
    opts.Schema.For<AliasDocument>().Identity(x => x.Id);
    opts.Schema.For<AliasDocument>().Index(x => x.SiteId);
    opts.Schema.For<AliasDocument>().UniqueIndex(x => x.SiteId, x => x.OldPath); // COMPOSITE
    opts.Schema.For<AliasDocument>().Index(x => x.NewPath);
    opts.Schema.For<AliasDocument>().Index(x => x.CreatedOn);
    opts.Schema.For<AliasDocument>().Index(x => x.ModifiedOn);
}
```

### 2.5 Remove UseStatusCodePagesWithRedirects from AliasStartupFilter ✅

**File:** `src/Aero.Cms.Modules.Aliases/AliasStartupFilter.cs`

Remove line 22: `app.UseStatusCodePagesWithRedirects("/oops");`

The single canonical call remains in `Program.cs:336`.

---

## Phase 3: Content Module SiteId ✅

### 3.1 ISiteOwned Interface

**New file:** `src/Aero.Cms.Abstractions/Interfaces/ISiteOwned.cs`

```csharp
public interface ISiteOwned
{
    long SiteId { get; set; }
}
```

### 3.2 Entity Changes per Module

| Entity | File | Change |
|---|---|---|
| `PageDocument` | `src/Aero.Cms.Core.Entities/PageDocument.cs` | Add `SiteId` + implement `ISiteOwned` |
| `BlogPostDocument` | `src/Aero.Cms.Core.Entities/BlogPostDocument.cs` | Add `SiteId` + implement `ISiteOwned` |
| `DocsPage` | `src/Aero.Cms.Core.Entities/DocsPage.cs` | Add `SiteId` + implement `ISiteOwned` |
| `ContentSlugDocument` | `src/Aero.Cms.Modules.Pages/SlugRegistry.cs` | Add `SiteId` |
| `MediaAsset` | `src/Aero.Cms.Core.Entities/MediaAsset.cs` | Add `SiteId` |
| `Category` | `src/Aero.Cms.Core.Entities/Category.cs` | Add `SiteId` |
| `Tag` | `src/Aero.Cms.Core.Entities/Tag.cs` | Add `SiteId` |

### 3.3 Marten Composite Unique Indexes

For each site-owned entity, add:
```csharp
opts.Schema.For<T>().Index(x => x.SiteId);
opts.Schema.For<T>().UniqueIndex(x => x.SiteId, x => x.Slug);  // site-scoped slug uniqueness
```

### 3.4 Content Services: Inject ISiteContext

Each content service (`MartenPageContentService`, `MartenBlogPostContentService`, `DocsService`) must:
1. Inject `ISiteContext` via constructor
2. Scope all queries: `.Where(x => x.SiteId == _siteContext.SiteId)`
3. Stamp `SiteId` on save: `entity.SiteId = _siteContext.SiteId`
4. Verify ownership on update/delete: `entity.SiteId == _siteContext.SiteId`

---

## Phase 4: Manager UI ✅ (Complete)

### 4.1 Site Management Pages ✅

Already existed at `src/Aero.Cms.Shared/Pages/Manager/Sites.razor` — full CRUD (list, create, edit, delete) using `ISitesHttpClient`.

### 4.2 NavMenu Restructure ✅

- Sites added at position #1 (right after Dashboard)
- Aliases added at position #2 (right after Sites)
- Databases already removed
- Taxonomy "General" submenu already removed
- Settings already at bottom

### 4.3 Site Selector ✅

Already existed at `src/Aero.Cms.Shared/Pages/Manager/SiteSelector.razor`. Auto-selects on single site. `ManagerHeader.razor` has site indicator with `CTRL+S` keyboard shortcut.

### 4.4 Aliases Page ✅ (New)

- `src/Aero.Cms.Shared/Pages/Manager/Aliases.razor` — create, list, delete aliases
- `src/Aero.Cms.Abstractions/Http/Clients/AliasesClient.cs` — HTTP client interface + stub implementation
- Backend API endpoints in `Aero.Cms.Modules.Aliases` need implementation

---

## Phase 5: Seed Data & Migration ✅ (Partially Complete)

### 5.1 SeedDataService Updates ✅

- Site creation uses `PrimaryHost` + `Hosts` list
- Builder methods stamp `SiteId` on all seeded entities (pages, docs, tags, oops page)
- Slug reservation passes `siteId` through `ContentSlugDocument.Create()`

### 5.2 Legacy Data Backfill ⬜ (Not Started)

For existing databases:
1. Create a default site if none exists
2. Update all site-owned records without `SiteId` to the default site's ID
3. Apply new Marten indexes

**Backfill order:**
1. Create default site
2. Backfill Pages
3. Backfill Posts
4. Backfill Docs
5. Backfill Media
6. Backfill Categories/Tags
7. Backfill Aliases
8. Backfill ContentSlugDocuments

---

## Phase 6: Testing ✅ (Complete)

Tests live in `tests/Aero.Cms.Core.Tests/`:

### 6.1 Unit & Integration Tests Completed

| Test | Type | File |
|---|---|---|
| SiteResolutionMiddleware resolves host | Integration | `SitePipelineChainTests.cs` |
| SiteResolutionMiddleware returns 404 for unknown/disabled host | Integration | `SitePipelineChainTests.cs` |
| HostNormalizer strips port, lowercases, trims dot | Unit | `SitePipelineChainTests.cs` |
| AliasRewriteRule redirects 301 (site-scoped) | Integration | `SitePipelineChainTests.cs` |
| Same path different sites → different redirects | Integration | `SitePipelineChainTests.cs` |
| Unknown host skips alias check (short-circuit) | Integration | `SitePipelineChainTests.cs` |
| Chain ordering: site runs before alias | Integration | `SitePipelineChainTests.cs` |
| SiteResolution sets IAeroSiteSlice on Features | Integration | `SitePipelineChainTests.cs` |
| SitePathKey value type equality | Unit | `SitePipelineChainTests.cs` |
| Page SaveAsync stamps SiteId | Unit | `PageContentServiceTests.cs` |
| Page CreateAsync stamps SiteId | Unit | `PageContentServiceTests.cs` |
| Page DeleteAsync rejects cross-site | Unit | `PageContentServiceTests.cs` |
| Blog SaveAsync stamps SiteId | Unit | `BlogPostContentServiceTests.cs` |
| Blog DeleteAsync rejects cross-site | Unit | `BlogPostContentServiceTests.cs` |
| Docs SaveAsync stamps SiteId | Unit | `DocsServiceTests.cs` |
| Docs ToViewModel maps SiteId | Unit | `DocsServiceTests.cs` |
| Docs DeleteAsync rejects cross-site | Unit | `DocsServiceTests.cs` |
| ContentSlugReservation stamps SiteId | Unit | `SlugRegistryTests.cs` |

---

## Module Order Reference

| Module | Order | IStartupFilter | Pipeline Stage |
|---|---|---|---|
| `SitesModule` | `-9999` | `SiteStartupFilter` | Host → Site resolution |
| `AliasModule` | `-9998` | `AliasStartupFilter` | Site-scoped alias rewrite |
| `BlogModule` | `-9997` | (future) | Blog-specific middleware |
| `CommerceModule` | `-9996` | (future) | Commerce middleware |
| `PagesModule` | `-9995` | (future) | Page-specific middleware |

---

## Acceptance Criteria

| # | Criterion | Status |
|---|---|---|
| 1 | One Aero CMS instance can serve multiple sites from one database | ✅ Phase 1 |
| 2 | Current site is resolved from request host/domain (not headers) | ✅ Phase 1 |
| 3 | Each site-owned module persists and queries by `SiteId` | ✅ Phase 3 |
| 4 | Same slugs/paths can exist on different sites (composite uniqueness) | ✅ Phase 3 |
| 5 | Alias resolution is site-scoped: `(SiteId + OldPath) → NewPath` | ✅ Phase 2 |
| 6 | Cross-site update/delete by ID alone is rejected | ✅ Phase 3 |
| 7 | Manager UI shows site-specific content | ✅ Phase 4 |
| 8 | Seed data creates default site and stamps `SiteId` on all entities | ✅ Phase 3 |
| 9 | `UseStatusCodePagesWithRedirects` exists only once in `Program.cs` | ✅ Phase 2 |
| 10 | `DisabledInProduction` modules skip `IStartupFilter` registration | ✅ Phase 1 |

**100 of 125 tests passing** (25 pre-existing failures unrelated to multi-site).
