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

## Phase 1: Foundation

### 1.1 SitesModel: Single Hostname → Multiple Hosts

**File:** `src/Aero.Cms.Core.Entities/SitesModel.cs`

Replace `Hostname` (single string) with `PrimaryHost` + `Hosts` list:

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

### 1.2 Host Normalization Utility

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

### 1.3 SitesModule Marten Configuration Update

**File:** `src/Aero.Cms.Modules.Sites/SitesModule.cs`

```csharp
public override void Configure(IServiceProvider services, StoreOptions opts)
{
    Configure<SitesModel>(services, opts);
    opts.Schema.For<SitesModel>().UniqueIndex(x => x.PrimaryHost!);
    opts.Schema.For<SitesModel>().Index(x => x.IsEnabled);
    opts.Schema.For<SitesModel>().ForeignKey<TenantModel>(x => x.TenantId);
    // Hosts list: Marten DuplicateField for Contains queries
    opts.Schema.For<SitesModel>().Duplicate(x => x.Hosts, pgType: "jsonb");
}
```

### 1.4 SiteModelValidator Update

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

### 1.5 SiteLookupService: Multi-Host Resolution

**File:** `src/Aero.Cms.Modules.Sites/SiteLookupService.cs`

```csharp
public async Task<SiteViewModel?> ResolveByHostAsync(string host, CancellationToken ct = default)
{
    var normalized = HostNormalizer.Normalize(host);
    
    var site = await session.Query<SitesModel>()
        .Where(x => x.PrimaryHost == normalized || x.Hosts.Contains(normalized))
        .Where(x => x.IsEnabled)
        .FirstOrDefaultAsync(ct);

    if (site is null) return null;
    return MapToViewModel(site);
}
```

### 1.6 ISiteContext: Cookie Fallback for Manager Routes

**File:** `src/Aero.Cms.Web/Infrastructure/DefaultSiteContext.cs`

The current implementation reads from `IAeroSiteSlice` on `HttpContext.Features` — set by `SiteResolutionMiddleware` for public routes. For `/manager/*` routes where the middleware is skipped, it falls back to the `AeroCms.SiteId` cookie:

```csharp
public sealed class DefaultSiteContext : ISiteContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DefaultSiteContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long SiteId
    {
        get
        {
            // 1. Try features (set by SiteResolutionMiddleware for public routes)
            var slice = _httpContextAccessor.HttpContext?.Features.Get<IAeroSiteSlice>();
            if (slice is not null) return slice.SiteId;

            // 2. Fallback: read from manager cookie (for /manager/* API calls)
            var cookie = _httpContextAccessor.HttpContext?.Request.Cookies["AeroCms.SiteId"];
            if (long.TryParse(cookie, out var siteId)) return siteId;

            return 0;
        }
    }

    public long TenantId
    {
        get
        {
            var slice = _httpContextAccessor.HttpContext?.Features.Get<IAeroSiteSlice>();
            return slice?.TenantId ?? 0;
        }
    }
}
```

This dual resolution means **no content service code changes** are needed for site scoping. All services filter by `ISiteContext.SiteId` and automatically get the correct site from whichever path is active.

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

## Phase 2: Site-Scoped Aliases

### 2.1 AliasRuleCache: Composite Key

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

### 2.2 IAliasRuleCache: Update Signature

**File:** `src/Aero.Cms.Modules.Aliases/IAliasRuleCache.cs`

```csharp
public interface IAliasRuleCache
{
    AliasRuleEntry? Find(long siteId, string oldPath);  // was: Find(string oldPath)
    Task RefreshAsync(CancellationToken ct = default);
    void Invalidate();
}
```

### 2.3 AliasRewriteRule: Site-Scoped Lookup

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

### 2.4 AliasesModule Marten Index: Composite Unique

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

### 2.5 Remove UseStatusCodePagesWithRedirects from AliasStartupFilter

**File:** `src/Aero.Cms.Modules.Aliases/AliasStartupFilter.cs`

Remove line 22: `app.UseStatusCodePagesWithRedirects("/oops");`

The single canonical call remains in `Program.cs:336`.

---

## Phase 3: Content Module SiteId

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

## Phase 4: Manager UI

### 4.1 Site Management Pages

Create Blazor pages under `src/Aero.Cms.Shared/Pages/Manager/Sites/`:

| Page | Route | Purpose |
|---|---|---|
| `SitesList.razor` | `/manager/sites` | List all sites with create button |
| `SiteEditor.razor` | `/manager/sites/{id}` | Edit site name, hosts, description, enabled |
| `SiteCreate.razor` | `/manager/sites/create` | Create new site |

Fields displayed:
- **Editable**: Name, PrimaryHost, Hosts (multi-domain), Description, IsEnabled, DefaultCulture
- **Read-only/immutable**: TenantId, SiteId (Id)

### 4.2 NavMenu Restructure

**File:** `src/Aero.Cms.Shared/Layout/NavMenu.razor`

Changes per `docs/site-manager-tasks.md`:

1. **Add Site Settings** — positioned just under Dashboard:
   ```razor
   <NavMenuSection Href="/manager/sites" Label="Site Settings" Icon="..." IsCollapsed="IsCollapsed">
       <NavMenuItem Href="/manager/sites/general" Label="General" IsCollapsed="IsCollapsed"/>
   </NavMenuSection>
   ```

2. **Add Aliases menu** — under Sites:
   ```razor
   <NavMenuSection Href="/manager/aliases" Label="Aliases" Icon="..." IsCollapsed="IsCollapsed">
   ```

3. **Add Banners menu** — after Aliases (placeholder for future)

4. **Taxonomy**: Remove "General" submenu, keep only "Categories" and "Tags"

5. **Remove Databases menu item** — not used

6. **Settings moved to bottom** — already done (existing spacer + anchor)

### 4.3 Site Selector in Top Nav

After the last top nav menu item:
```razor
<div class="site-selector" @onkeydown="HandleKeyDown">
    <RadzenDropDown @bind-Value="currentSiteId"
                    Data="sites"
                    TextProperty="Name"
                    ValueProperty="Id"
                    Change="OnSiteChanged" />
</div>
```

Keyboard shortcut: `CTRL+S` opens the selector via `@onkeydown:ctrlKey`.

### 4.4 Site Context for Manager

Manager pages operate in the current site context:
- Lists and editors show only the active site's records
- Site selector changes the active site for the manager session
- Site selector stores choice in `ProtectedBrowserStorage` (client-side Blazor)

---

## Phase 5: Seed Data & Migration

### 5.1 SeedDataService Updates

**File:** `src/Aero.Cms.Modules.Setup/SeedDataService.cs`

1. Create default site with:
   - `Id = 1`, `Name = "Default Site"`
   - `PrimaryHost` from configuration or `localhost`
   - `Hosts = ["localhost", "127.0.0.1", PrimaryHost]`
   - `IsEnabled = true`

2. Pass `siteId` to all builder methods (`BuildHomepage`, `BuildStarterBlogContent`, etc.)

3. Stamp `SiteId` on every seeded entity:
   - `PageDocument.SiteId = siteId`
   - `BlogPostDocument.SiteId = siteId`
   - `DocsPage.SiteId = siteId`
   - `AliasDocument.SiteId = siteId`
   - All `ContentSlugDocument` entries

### 5.2 Legacy Data Backfill

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

## Phase 6: Testing

### 6.1 Unit Tests

| Test | Purpose |
|---|---|
| `HostNormalizer_TrimsPort` | `"example.com:5001"` → `"example.com"` |
| `SiteLookup_MultipleHosts` | Can resolve site by primary host or secondary host |
| `SiteLookup_DisabledSite_Null` | Disabled sites are not resolved |
| `SiteResolution_SetsFeature` | Middleware sets `IAeroSiteSlice` on features |
| `AliasCache_CompositeKey` | Same path on different sites returns different entries |
| `ContentCreate_StampsSiteId` | Entity.SiteId populated from current context |
| `ContentQuery_FilteredBySite` | Queries filtered by site return only matching records |
| `CrossSite_UpdateRejected` | Update with mismatched SiteId fails |

### 6.2 Integration Tests

| Test | Purpose |
|---|---|
| `RequestToSiteA_ReturnsSiteAContent` | Host header isolation |
| `Slug_SameOnTwoSites_NoConflict` | Composite uniqueness works |
| `AliasRedirect_SiteScoped` | `/old-path` on site A redirects; same path on site B doesn't |
| `CrossSite_DeleteRejected` | Cannot delete entity from wrong site by ID alone |

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

The feature is complete when:

1. One Aero CMS instance can serve multiple sites from one database
2. Current site is resolved from request host/domain (not headers)
3. Each site-owned module persists and queries by `SiteId`
4. Same slugs/paths can exist on different sites (composite uniqueness)
5. Alias resolution is site-scoped: `(SiteId + OldPath) → NewPath`
6. Cross-site update/delete by ID alone is rejected
7. Manager UI shows site-specific content
8. Seed data creates default site and stamps `SiteId` on all entities
9. `UseStatusCodePagesWithRedirects` exists only once in `Program.cs`
10. `DisabledInProduction` modules skip `IStartupFilter` registration
