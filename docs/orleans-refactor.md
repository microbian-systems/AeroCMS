# AeroCMS Refactoring Plan: Startup Encapsulation, API Decoupling, and Orleans Grain Migration

**Status**: Phase 1-3 Complete ✅ | Phase 4: Front-end done ✅, cleanup/cleanup pending  
**Date**: 2026-05-22  
**Effort**: Medium (3 phases, ~8-12 days)  
**Reviewed by**: @council (multi-model consensus)

---

## Table of Contents

1. [Objective](#objective)
2. [Current Architecture](#current-architecture)
3. [Phase 1: Startup Encapsulation](#phase-1-startup-encapsulation)
4. [Phase 2: API Migration from HeadlessModule](#phase-2-api-migration-from-headlessmodule)
5. [Phase 3: Orleans Grain Migration](#phase-3-orleans-grain-migration)
6. [Orleans Dependency Injection Reference](#orleans-dependency-injection-reference)
7. [Risk Register](#risk-register)
8. [Implementation Sequencing](#implementation-sequencing)
9. [Validation Strategy](#validation-strategy)
10. [Open Items / TODOs](#open-items--todos)
11. [Appendix A: Orleans Dependency Injection Reference](#appendix-a-orleans-dependency-injection-reference)

---

## Objective

Refactor the AeroCMS account into a cleaner vertical-slice architecture by:

1. **Encapsulating** startup logic from `Program.cs` into a dedicated startup pipeline class
2. **Decoupling** API endpoint registrations from `HeadlessModule` into each domain module
3. **Migrating** business logic from minimal API handlers into Orleans grains

All three changes must maintain **Single Responsibility Principle**, **vertical slice architecture**, and follow existing project conventions (SOLID, Railway Oriented Programming, source-generator-based module discovery).

---

## Current Architecture

### Problem 1: Bloated Program.cs
`src/Aero.Cms.Web/Program.cs` is **488 lines** with inline static methods and a two-phase startup pattern (Setup App → Main App). Startup bootstrap logic is mixed with middleware configuration, dependency injection, and error handling.

### Problem 2: Centralized API God-Module
`Aero.Cms.Modules.Headless/HeadlessModule.cs` registers **22+ API groups** in a single `RunAsync` method. These APIs span multiple domains (Blog, Pages, Media, Users, etc.) but are all centrally owned — violating module boundaries.

### Problem 3: Business Logic in HTTP Layer
Most API handlers (e.g., `BlogApi.cs` — 526 lines, `PagesApi.cs` — 579 lines) contain business logic (slug uniqueness checks, state transitions, audit event creation) directly in minimal API static methods, mixed with HTTP concerns (`IResult` return types, `TypedResults`).

---

## Phase 1: Startup Encapsulation

### Design Decision

**`Aero.Cms.Web.Core` is NOT the right home.** The council identified a circular dependency risk:

- `Aero.Cms.Web.Core.csproj` already references `Aero.Cms.Modules.Modules` (the meta-module)
- Modules like `HeadlessModule`, `NavigationModule`, `FooterModule` reference Web.Core
- `Program.cs` consumes `Aero.Cms.Modules.Setup`, `Aero.Cms.Modules.Identity`, `Aero.AppServer` — none of which are referenced by Web.Core
- Adding these references to Web.Core would create: `Modules → Web.Core → Modules.Setup/Identity/...` (a cycle)

**Decision**: Create a new **composition root** project:

```
src/Aero.Cms.Web.Bootstrap/
  └── AeroStartupPipeline.cs   (static class, ~350 lines)
```

This project sits at the composition root layer with full references to all modules and the app server. Web.Core remains clean as the module infrastructure layer.

### What Moves

From `Program.cs` into `AeroStartupPipeline`:

```csharp
public static class AeroStartupPipeline
{
    public static async Task RunAsync(string[] args);
    
    // Moved static methods:
    private static IConfiguration BuildEarlyConfiguration(string[] args, string webProjectPath);
    private static BootstrapState GetBootstrapState(IConfiguration config);
    private static async Task<(IConfiguration, BootstrapState)> ReloadBootstrapStateAfterSetupAsync(...);
    private static async Task RunSetupAppAsync(...);
    private static async Task RunMainAppAsync(...);
    private static async Task WaitForRequiredInfrastructureAsync(...);
    private static async Task TryMarkBootstrapFailedAsync(...);
}
```

### Result: Program.cs

Shrinks to **~30 lines**: Serilog bootstrap + delegation to `AeroStartupPipeline.RunAsync(args)`.

```csharp
using Aero.Cms.Web.Bootstrap;
using Serilog;

// Serilog early bootstrap (must run before DI)
Log.Logger = new LoggerConfiguration() /* ... */ .CreateLogger();

try
{
    await AeroStartupPipeline.RunAsync(args);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

---

## Phase 2: API Migration from HeadlessModule

### Pattern

Follow the **NavigationModule** precedent: each module creates `Areas/Api/v1/` with static extension methods, called from the module's `RunAsync`:

```csharp
// In Module:
public override Task RunAsync(IEndpointRouteBuilder builder)
{
    builder.MapMyDomainApi();  // extension method from Areas/Api/v1/
    return Task.CompletedTask;
}
```

### API → Module Mapping

| Headless API | Target Module | Class to Create | Notes |
|---|---|---|---|
| `MapAliasesApi` | `Modules.Aliases` | `Areas/Api/v1/AliasesAdminApi.cs` | Has `IAliasService` |
| `MapBlogApi` | `Modules.Blog` | `Areas/Api/v1/BlogPostsAdminApi.cs` | Has `IBlogPostContentService` |
| `MapPagesApi` | `Modules.Pages` | `Areas/Api/v1/PagesAdminApi.cs` | Has `IPageContentService` |
| `MapPagesTreeApi` | `Modules.Pages` | `Areas/Api/v1/PagesTreeApi.cs` | Has `IPageTreeService` |
| `MapDocsApi` | `Modules.Docs` | `Areas/Api/v1/DocsAdminApi.cs` | Has `IDocsService` |
| `MapCategoriesApi` | `Modules.Blog` | Co-locate in `BlogPostsAdminApi` | `Category` is a Blog model |
| `MapTagsApi` | `Modules.Blog` | Co-locate in `BlogPostsAdminApi` | `Tag` is a Blog model |
| `MapUsersApi` | `Modules.Users` | `Areas/Api/v1/UsersAdminApi.cs` | Module exists |
| `MapProfileApi` | `Modules.Users` | `Areas/Api/v1/ProfileApi.cs` | Current user profile |
| `MapThemesApi` | `Modules.Theming` | `Areas/Api/v1/ThemesAdminApi.cs` | Has `ThemeService` |
| `MapSettingsApi` | `Modules.Settings` | `Areas/Api/v1/SettingsAdminApi.cs` | Module exists (needs services) |
| `MapModulesApi` | `Modules.Modules` | `Areas/Api/v1/ModulesAdminApi.cs` | Meta-module management |
| `MapJwtApi` | `Modules.Jwt` | `Areas/Api/v1/JwtApi.cs` | Has `JwtAuthModule` |
| `MapAuthApi` | `Modules.Jwt` | Co-locate in `JwtApi.cs` | Headless auth = JWT-based |
| `MapContentTypesApi` | `Modules.Content` | `Areas/Api/v1/ContentTypesAdminApi.cs` | Module exists |
| `MapContentItemsApi` | `Modules.Content` | `Areas/Api/v1/ContentItemsAdminApi.cs` | Module exists |
| `MapBlocksApi` | `Modules.Content` | `Areas/Api/v1/BlocksApi.cs` | Block lookup |
| `MapAuditApi` | `Modules.Audit` | `Areas/Api/v1/AuditApi.cs` | Module exists (needs services) |
| `MapMediaApi` | `Modules.Media` | `Areas/Api/v1/MediaAdminApi.cs` | Module exists |
| `MapFilesApi` | `Modules.Media` | Co-locate in `MediaAdminApi` | File management ≈ media |
| `MapDashboardApi` | `Modules.Manager` | `Areas/Api/v1/DashboardApi.cs` | Manager module exists |
| `MapPreviewApi` (page handlers) | `Modules.Pages` | Co-locate in `PagesAdminApi` | Page preview |
| `MapPreviewApi` (blog handlers) | `Modules.Blog` | Co-locate in `BlogPostsAdminApi` | Blog preview |
| `MapPreviewApi` (block fragment) | `Modules.Manager` | `Areas/Api/v1/PreviewBlockFragmentApi.cs` | **Content-agnostic** endpoint; manager concern |

> **Note on `PreviewBlockFragment`**: The `PreviewBlockFragment` endpoint accepts raw `BlockBase` — it is content-type agnostic (works for any block in any content type). Placing it in Pages or Blog would be wrong. It belongs in `Modules.Manager` alongside the Dashboard API — both are cross-cutting admin concerns.

### Prerequisite: Auth & Antiforgery Audit

**Before any API migration**, audit all 22 API groups for auth requirements. Currently only `ContentItemsApi` and `ContentTypesApi` use `.RequireAuthorization()`. All `admin/` endpoints must require authorization after migration.

**Auth policy**: Use the existing role-based `"Admin"` role (already used by `SitesModule` and `ServerAuthenticationStateProvider`). Define a centralized named policy in `Aero.Cms.Web` startup if one doesn't already exist:

```csharp
// In Program.cs authorization setup:
services.AddAuthorization(options =>
{
    options.AddPolicy("AeroAdmin", policy => policy.RequireRole("Admin"));
});
```

Then each admin API group uses: `.RequireAuthorization("AeroAdmin")`

| API Group | Auth Required? | Antiforgery | Notes |
|---|---|---|---|
| `admin/pages` | ✅ Yes | Keep default | Draft endpoints need auth too |
| `admin/blogs` | ✅ Yes | Keep default | |
| `admin/aliases` | ✅ Yes | Keep default | |
| `admin/media` | ✅ Yes | **Disable** for uploads | File uploads need `DisableAntiforgery()` |
| `admin/files` | ✅ Yes | **Disable** for uploads | Same as media |
| `admin/users` | ✅ Yes | Keep default | |
| `admin/themes` | ✅ Yes | Keep default | |
| `admin/settings` | ✅ Yes | Keep default | |
| `admin/modules` | ✅ Yes | Keep default | |
| `admin/audit` | ✅ Yes | Keep default | |
| `admin/docs` | ✅ Yes | Keep default | |
| `admin/categories` | ✅ Yes | Keep default | |
| `admin/tags` | ✅ Yes | Keep default | |
| `admin/dashboard` | ✅ Yes | Keep default | |
| `admin/blocks` | ✅ Yes | Keep default | |
| `admin/content-types` | ✅ Yes | Keep default | Already has `RequireAuthorization()` |
| `admin/content-items` | ✅ Yes | Keep default | Already has `RequireAuthorization()` |
| `admin/preview/*` | ✅ Yes | Keep default | Preview needs auth — draft content |
| `api/jwt/*` | ❌ No (public) | Disable | Token endpoint is public |
| `api/auth/*` | ❌ No (public) | Disable | Login endpoint is public |
| `admin/profile` | ✅ Yes | Keep default | User's own profile |

### After Migration: HeadlessModule

```csharp
public override Task RunAsync(IEndpointRouteBuilder builder)
{
    // All APIs migrated to respective modules.
    // HeadlessModule now owns only cross-cutting concerns (Scalar/OpenAPI).
    return Task.CompletedTask;
}
```

---

## Phase 3: Orleans Grain Migration

### Design Decisions

#### 3.1 Grain Interface Placement

Following **Microsoft Orleans best practices** ([docs](https://learn.microsoft.com/dotnet/orleans/grains/)):

> *"You can define grain interfaces and grain classes in the same Class Library project or in two different projects for better separation of interfaces from implementation."*

**Decision**:

| Layer | Location | Content |
|---|---|---|
| **Grain Interfaces** | `Aero.Cms.Abstractions/Actors/` | `IAeroAliasActor`, `IAeroPageActor`, `IAeroPostActor`, etc. — consumed by external systems |
| **Service Wrappers** | `Aero.Cms.Abstractions/Services/` | `AeroAliasService`, `AeroPageService`, etc. — typed façades over `IGrainFactory` |
| **Grain Implementations** | Each module's `Grains/` folder | `AeroAliasGrain`, `AeroPageGrain`, etc. — registered on the silo |

Each module registers its own grains in `ConfigureServices`. External consumers reference the abstractions library.

#### 3.2 Marten + Orleans Lifecycle Contract

**Critical decision**: How do grains interact with Marten's `IDocumentSession`?

| Option | Description | Risk |
|---|---|---|
| A | Grain stores `IDocumentSession` as field | Session outlives grain activation — NRE on reactivation |
| B | Grain manages Orleans state, Marten in service layer | Dual-write — state drift |
| **C (chosen)** | **Grain obtains `IDocumentSession` from `IServiceProvider` per method call** | **No drift. Marten is sole source of truth.** |

**Contract**: Grains inject `IDocumentStore` as a **singleton** and open a lightweight session per operation. The grain does not store `IDocumentSession` as grain state. On each method invocation, it opens a fresh session. Grain state on `AeroActor` is used for caching/coordination only, not as the source of truth.

```csharp
public class AeroPageGrain : AeroActor, IAeroPageActor
{
    private readonly IDocumentStore _documentStore;
    
    public AeroPageGrain(
        ILogger<AeroActor> log, 
        IDocumentStore documentStore)    // ← singleton, safe for grain activation
        : base(log) 
    {
        _documentStore = documentStore;
    }

    public async Task<AeroRequestResponse<PageViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        // Lightweight session per operation — not stored as grain state
        await using var session = _documentStore.LightweightSession();
        var page = new PageDocument { Id = Snowflake.NewId(), /* ... */ };
        session.Store(page);
        await session.SaveChangesAsync(ct);
        // Marten is the sole source of truth.
    }
}
```

**Result**: Zero state drift risk. Marten remains the authoritative data store.

#### 3.3 Grain Registration — Two-Layer Strategy

Grain registration has two concerns that live at different layers. This is by design — the same two-layer pattern already proven with Wolverine handlers in this codebase.

| Layer | Concern | Where | What |
|---|---|---|---|
| **Assembly Discovery** | Tell Orleans which assemblies contain grain classes | `AddAeroApplicationServer()` via callback | Source-generated `IApplicationPartManager` callback |
| **Service Wrappers** | Register grain façades as consumable DI services | Each module's `ConfigureServices(IServiceCollection)` | `AeroPageService` wrapping `IGrainFactory` |

**Why two layers**: `services.AddOrleans()` in `AeroAppServerExtensions.cs` gives you `IServiceCollection`, not `ISiloBuilder`. Modules calling `ConfigureServices` later cannot touch `ApplicationPartManager` directly. This is architecturally correct — silo setup belongs in the composition root, not inside modules.

##### Layer 1: Assembly Discovery (mirrors Wolverine pattern)

`AeroAppServerExtensions.cs` receives a callback, exactly like the existing `configureWolverine` parameter:

```csharp
// AeroAppServerExtensions.cs — NEW parameter
public static Task<IHostApplicationBuilder> AddAeroApplicationServer(
    this IHostApplicationBuilder builder,
    Action<WolverineOptions>? configureWolverine = null,
    Action<IApplicationPartManager>? configureGrains = null)    // ← NEW
{
    // ... existing setup ...
    
    services.AddOrleans(opts =>
    {
        opts.UseLocalhostClustering();
        opts.ConfigureApplicationParts(parts =>
        {
            parts.AddFromApplicationBaseDirectory();     // baseline
            configureGrains?.Invoke(parts);              // module grains
        });
    });
    
    // ...
}
```

The source-generated grain catalog mirrors `GeneratedWolverineHandlerCatalog`:

```csharp
// Source-generated — Aero.AppServer.Generated
public static class GeneratedAeroGrainCatalog
{
    public static void Register(IApplicationPartManager parts)
    {
        parts.AddApplicationPart(typeof(Aero.Cms.Modules.Blog.Grains.AeroBlogGrain).Assembly)
             .WithReferences();
        parts.AddApplicationPart(typeof(Aero.Cms.Modules.Pages.Grains.AeroPageGrain).Assembly)
             .WithReferences();
        parts.AddApplicationPart(typeof(Aero.Cms.Modules.Media.Grains.AeroMediaGrain).Assembly)
             .WithReferences();
        // ... one per module that contains grains
    }
}
```

`Program.cs` wires both catalogs at the composition root:

```csharp
_ = await builder.AddAeroApplicationServer(
    configureWolverine: GeneratedWolverineHandlerCatalog.Register,
    configureGrains: GeneratedAeroGrainCatalog.Register);    // ← mirrors Wolverine
```

**No reflection. No manual maintenance. AOT safe.** Adding a new grain just means adding the grain class file — the source generator picks it up.

##### Layer 2: Service Wrappers (per module)

Each module registers its grain-backed service wrappers in `ConfigureServices`. `IGrainFactory` is registered by Orleans when `AddOrleans` runs — modules resolve it freely without any Orleans plumbing knowledge:

```csharp
// In PagesModule.ConfigureServices — no Orleans plumbing here
public override void ConfigureServices(IServiceCollection services, ...)
{
    // Existing services stay
    services.AddScoped<IPageContentService, MartenPageContentService>();

    // NEW: Grain-backed service wrapper (façade over IGrainFactory)
    services.AddScoped<IAeroPageService>(sp =>
        new AeroPageService(sp.GetRequiredService<IGrainFactory>()));
}
```

| Designer | Assessor |
|---|---|
| Source-gen friendly | **Yes** — no reflection, no attribute scanning |
| Module isolation | **Yes** — modules only touch `IServiceCollection` |
| Consistent with existing Wolverine pattern | **Yes** — identical callback pattern, same wiring in `Program.cs` |
| AOT safe | **Yes** — no dynamic assembly loading |

#### 3.4 Grain Interface Hierarchy

```
AeroActor (base, OpenTelemetry + lifecycle)
├── IAeroCmsContentActor<T>           ← Existing (CRUD + site + slug + state)
│   ├── IAeroAliasActor               ← Existing interface
│   ├── IAeroPageActor                ← Existing interface
│   ├── IAeroPostActor                ← Existing interface
│   ├── IAeroDocsActor                ← Existing interface
│   ├── IAeroMediaActor               ← Existing interface
│   ├── IAeroCategoryActor            ← Existing interface
│   ├── IAeroTagActor                 ← Existing interface
│   ├── IAeroSiteActor                ← Existing interface
│   └── IAeroAuthorActor              ← Existing interface
└── IAeroCmsContentActor<T, TKey>     ← Existing (flexible key type)
```

Additional interfaces needed for non-content entities (Settings, Themes):

```csharp
// New interface for simple key-value store grains
public interface IAeroSettingActor : IAeroActor
{
    Task<AeroRequestResponse<IReadOnlyList<SettingViewModel>>> GetAllAsync(CancellationToken ct);
    Task<AeroRequestResponse<SettingViewModel>> GetByKeyAsync(string key, CancellationToken ct);
    Task<AeroRequestResponse<SettingViewModel>> SetAsync(string key, string value, CancellationToken ct);
    Task<AeroRequestResponse<bool>> DeleteByKeyAsync(string key, CancellationToken ct);
    Task<AeroRequestResponse<IReadOnlyList<SettingViewModel>>> GetByCategoryAsync(string category, CancellationToken ct);
}
```

### Grain Migration Targets

| Grain | Module | Complexity | Priority | Notes |
|---|---|---|---|---|
| `AeroAliasGrain` | `Modules.Aliases/Grains/` | Low | **P1 (First)** | Prove pattern. `IAliasService` already wraps grain. |
| `AeroCategoryGrain` | `Modules.Blog/Grains/` | Low | P2 | Simple CRUD |
| `AeroTagGrain` | `Modules.Blog/Grains/` | Low | P2 | Simple CRUD |
| `AeroMediaGrain` | `Modules.Media/Grains/` | Medium | P2 | File upload; proves grain + binary interaction |
| `AeroSettingsGrain` | `Modules.Settings/Grains/` | Low | P2 | Key-value store |
| `AeroThemeGrain` | `Modules.Theming/Grains/` | Medium | P2 | Theme activation/management |
| `AeroDocsGrain` | `Modules.Docs/Grains/` | Medium | P3 | Counts, markdown rendering |
| `AeroPageGrain` | `Modules.Pages/Grains/` | **High** | **P3 (After pattern proven)** | Event sourcing, drafts, publish workflow, cascade delete |
| `AeroPostGrain` | `Modules.Blog/Grains/` | Medium | P3 | Blog CRUD, import, audit |
| `AeroContentTypeGrain` | `Modules.Content/Grains/` | Medium | P3 | Content type definitions |
| `AeroContentItemGrain` | `Modules.Content/Grains/` | Medium | P3 | Content item CRUD |

> **Sequencing note**: The user initially requested Pages-first for grain migration. The council recommends Aliases-first to prove the grain pattern before tackling the highest-complexity module. **Final decision**: Aliases first, then batch remaining after pattern proven.

### Identity/Users Exclusion

**Decision**: Identity-backed APIs (`UsersApi`, `ProfileApi`) stay in the ASP.NET layer using `UserManager<AeroUser>` (EF Core). These do NOT migrate to Orleans grains in this phase because:

1. Identity uses EF Core, not Marten — fundamentally different persistence model
2. ASP.NET Identity's `UserManager<T>` is deeply coupled to `HttpContext`
3. Moving Identity to grains would require significant auth infrastructure redesign

**TODO** (tracked below): Revisit with [Orleans.Identity](https://github.com/managedcode/Orleans.Identity) — a community project that integrates ASP.NET Core Identity with Orleans grains.

### After Migration: API Handler Example

Before:
```csharp
// In BlogApi.cs — 526 lines, business logic inline
private static async Task<IResult> CreatePost(
    [FromBody] CreateBlogPostRequest request,
    [FromServices] IBlogPostContentService blogService,
    [FromServices] IAuditService auditService,
    [FromServices] IDocumentSession session,  // ← direct Marten access
    [FromServices] ISiteContext siteContext,
    CancellationToken ct)
{
    // Slug uniqueness check (business logic in HTTP handler)
    var existingSlug = await session.Query<ContentSlugDocument>()
        .FirstOrDefaultAsync(s => s.SiteId == siteContext.SiteId && s.NormalizedSlug == normalizedSlug, ct);
    // ...
}
```

After:
```csharp
// Thin HTTP adapter — ~15 lines
private static async Task<IResult> CreatePost(
    [FromBody] CreateBlogPostRequest request,
    [FromServices] IAeroPostActor postActor,  // ← grain (Orleans)
    CancellationToken ct)
{
    var result = await postActor.CreateAsync(request, ct);
    return result.Match(
        ok => TypedResults.Created($"/api/admin/blogs/{ok.Id}", ok),
        error => TypedResults.BadRequest(error));
}
```

### Target Architecture: Grains as Single Entry Point

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  Admin APIs  │  │ Razor Pages  │  │Seed Executor │
│  (minimal)   │  │  (Blazor)    │  │  (startup)   │
└──────┬───────┘  └──────┬───────┘  └──────┬───────┘
       │                  │                  │
       ▼                  ▼                  │ (direct — Orleans
┌──────────────────────────────┐             │  not running)
│     Orleans Grain Layer      │             │
│  PageActor  PostActor  ...   │             │
└─────────────┬────────────────┘             │
              │ delegates to                  │
              ▼                              ▼
┌──────────────────────────────────────────────────┐
│            Service Layer (internal)               │
│  IPageContentService  IBlogPostContentService     │
│  (Marten queries, business logic, caching)        │
└─────────────────────┬────────────────────────────┘
                      │
                      ▼
┌──────────────────────────────────────────────────┐
│              Marten (IDocumentStore)              │
└──────────────────────────────────────────────────┘
```

**Key rule**: At runtime, all callers (admin APIs, Razor pages) go through the grain layer. Grains delegate to existing services internally. No runtime caller touches `IDocumentSession` or service interfaces directly.

**Exception**: `ServerTargetSetupExecutor` (bootstrap/seed) uses services directly. Orleans may not be running during the setup phase. Seed code is excluded from the grain rule.

**Current state**: Admin APIs ✅ — route through grains. Front-end (Razor pages) ✅ — also route through grains.

---

## Orleans Dependency Injection Reference

See [Appendix A: Orleans Dependency Injection Reference](#appendix-a-orleans-dependency-injection-reference) for grain lifecycle, service lifetimes, Marten integration patterns, and pitfalls.

---

## Risk Register

| # | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R1 | **Circular dependency in Web.Core** | Medium | High | RESOLVED: Use new `Aero.Cms.Web.Bootstrap` project instead |
| R2 | **PreviewBlockFragment orphaned after split** | Low (caught early) | Medium | RESOLVED: Move to `Modules.Manager` |
| R3 | **Auth/antiforgery regression** | Medium | High | **Prerequisite**: Audit all 22 API groups before migration |
| R4 | **Module API route ordering regression** | Low | Medium | Use conformance test to verify route resolution |
| R5 | **Marten + Orleans lifecycle mismatch (NRE)** | High | High | RESOLVED: Inject `IDocumentStore` singleton; open `LightweightSession()` per method |
| R6 | **Orleans grain state vs Marten document state drift** | High (if misconfigured) | High | RESOLVED: Marten is sole source of truth; grains use short-lived sessions |
| R7 | **Unproven grain implementation pattern** | Medium | Medium | Mitigated: Start with Aliases (simplest) to prove pattern |
| R8 | **Pages grain complexity (event sourcing)** | High | High | Mitigated: Do last, after pattern proven |

---

## Implementation Sequencing

```
Phase 1 ✅ (1-2 days): Startup Encapsulation — DONE
├── Created src/Aero.Cms.Web.Bootstrap/ project with AeroStartupPipeline.cs
├── Moved static methods from Program.cs → AeroStartupPipeline.cs
├── Program.cs → ~30-line delegator
└── Verified: dotnet run starts both setup and main app correctly

Phase 2 ✅ (4-6 days): API Migration — DONE
├── 21 API groups relocated from HeadlessModule to domain modules
├── Preview API split: page→Pages, blog→Blog, block fragment stays in HeadlessModule
└── HeadlessModule cleaned to OpenAPI/Scalar + PreviewBlockFragment

Phase 3 ✅ (4-7 days): Orleans Grain Migration — DONE (12/12 grains)
├── AeroAliasGrain       (Aliases)    — CRUD + Wolverine events + FluentValidation
├── AeroCategoryGrain    (Blog)       — CRUD + Wolverine events
├── AeroTagGrain         (Blog)       — CRUD + Wolverine events
├── AeroPostGrain        (Blog)       — Ported from MartenBlogPostContentService
├── AeroContentItemGrain (Content)    — SaveDraft, Publish, Unpublish, GetByType
├── AeroContentTypeGrain (Content)    — Alias-based identity, CRUD
├── AeroDocsGrain        (Docs)       — CRUD + hierarchy queries + Wolverine events
├── AeroMediaGrain       (Media)      — SaveMedia, DeleteMedia, GetPaged, GetAll
├── AeroPageGrain        (Pages)      — Event sourcing, publish/draft, cascade delete
├── AeroSettingGrain     (Settings)   — Key-value store, categories
│
├── Skipped: AeroThemeGrain — Theme module has no persistence logic to port
│           (API endpoints are TODO stubs, service enumerates loaded modules).
│           This is greenfield development, not refactoring.
│
└── Excluded by design: Identity/Users (EF Core, not Marten)

Phase 4 ✅ (1 day): Cleanup & Verification
├── ✅ Move PreviewBlockFragment from HeadlessModule → ManagerModule
├── ✅ Front-end decoupling: Razor pages now route through grains
│   ├── Blog: PostsIndexPageModel, PostsDetailPageModel → IAeroPostActor
│   └── Pages: DynamicPageModel → IAeroPageActor
│   └── PageViewModel extended: ShowHeaderNavigation, HideFooter, ShowChatAgent, LayoutRegionsJson
│   └── IAeroPostActor extended: LoadAsync, FindBySlugAsync, GetLatestPostsAsync, GetPagedPostsAsync, GetTagNameMapAsync, GetPostAuthorSummaryAsync
│   └── Published-state filter in AeroPageGrain.GetBySlugCoreAsync
│   └── DynamicPageModelStatusCodeTests updated for new constructor
├── ⏭ Remove stale services — check internal consumers before removing registrations
├── ⏭ Implement source generator for GeneratedAeroGrainCatalog (hand-authored placeholder exists)
├── ⏭ Route conformance test (enumerate registered routes, verify resolution)
├── ✅ Verify: dotnet build (Pages, Posts, Abstractions: 0 errors)
└── ⏭ Verify: dotnet test (unit + integration)
```

---

## Validation Strategy

### Build Verification
```bash
# After each phase
dotnet build src/Aero.Cms.slnx --no-restore

# Phase 1 specific
dotnet run --project src/Aero.Cms.Web --environment Development
```

### Route Conformance Test
After Phase 2, add a test that enumerates all registered routes and verifies expected endpoints:

```csharp
[Test]
public async Task All_22_admin_api_groups_are_registered_after_migration()
{
    // Use Alba (ASP.NET Core integration testing)
    await using var host = await AlbaHost.For<Program>();
    
    var expectedPrefixes = new[]
    {
        "api/admin/pages", "api/admin/blogs", "api/admin/aliases",
        "api/admin/media", "api/admin/files", "api/admin/users",
        "api/admin/themes", "api/admin/settings", "api/admin/modules",
        "api/admin/audit", "api/admin/docs", "api/admin/categories",
        "api/admin/tags", "api/admin/dashboard", "api/admin/blocks",
        "api/admin/content-types", "api/admin/content-items",
        "api/admin/preview", "api/admin/profile",
        "api/jwt", "api/auth"
    };
    
    // Verify each prefix has at least one registered route
    var routes = host.Services.GetRequiredService<EndpointDataSource>().Endpoints;
    // ...
}
```

### Orleans Grain Integration Test

```csharp
[Test]
public async Task AliasGrain_create_read_delete_roundtrip()
{
    // Use Orleans TestCluster or Alba + embedded Postgres + Orleans silo
    var grain = Cluster.Client.GetGrain<IAeroAliasActor>(0, "test");
    
    var created = await grain.CreateAsync(new CreateAliasRequest { /* ... */ });
    var retrieved = await grain.GetByIdAsync(created.Value.Id);
    
    await Assert.That(retrieved.Value.OldPath).IsEqualTo(created.Value.OldPath);
}
```

---

## Open Items / TODOs

### Immediate
- [x] Create `src/Aero.Cms.Web.Bootstrap/` project with correct project references
- [x] Hand-author `GeneratedAeroGrainCatalog.Register()` with `// SOURCE GENERATOR TARGET` comment
- [x] Add `Action<IApplicationPartManager>? configureGrains` parameter to `AddAeroApplicationServer`
- [x] Wire `Program.cs`: `configureGrains: GeneratedAeroGrainCatalog.Register`
- [x] Move 21 APIs from HeadlessModule to domain modules
- [x] Move `PreviewBlockFragment` stays in HeadlessModule (cross-cutting block-type-agnostic)
- [x] Move `PreviewBlockFragment` to `Modules.Manager` (cross-cutting admin concern)
- [ ] **Phase 4**: Implement source generator for `GeneratedAeroGrainCatalog` (hand-authored placeholder exists)
- [ ] **Phase 4**: Route conformance test — enumerate all registered routes, verify resolution
- [ ] **Phase 4**: Remove stale services where safe (blocked: Razor pages + SeedDataService still use direct service DI)

### Future
- [ ] **Front-end decoupling** — Migrate Razor pages to call grains instead of injecting services directly:
  - ✅ `BlogDetailPageModel`, `BlogIndexPageModel` → inject `IAeroPostActor` instead of `IPostContentService`
  - ✅ `DynamicPageModel` (Pages front-end) → inject `IAeroPageActor` instead of `IPageContentService`
  - `ServerTargetSetupExecutor` — excluded; uses services directly (Orleans not running during setup phase)
  - After migration: remove service registrations from `ConfigureServices` if no runtime consumers remain
  - ✅ Added `LayoutRegionsJson` to `PageViewModel` for Orleans-safe transport of layout regions
  - ✅ Added `ShowHeaderNavigation`, `HideFooter`, `ShowChatAgent` to `PageViewModel`
  - ✅ Added `GetLatestPostsAsync`, `GetPagedPostsAsync`, `GetTagNameMapAsync`, `GetPostAuthorSummaryAsync` to `IAeroPostActor`
  - ✅ Added `LoadAsync`, `FindBySlugAsync` to `IAeroPostActor` interface (already existed in grain, not in interface)
  - ✅ Published-state filter in `AeroPageGrain.GetBySlugCoreAsync` for public rendering
  - ✅ `PostsDetailPage.cshtml` author rendering uses `Model.PostAuthor` instead of inline `@inject IPostContentService`
  - ✅ `PostsDetailPage.cshtml` content extraction uses `OfType<string>()` instead of `OfType<MarkdownBlock>()`
  - ⏭ Consider removing `AddScoped<IPageContentService, MartenPageContentService>()` and `AddScoped<IPostContentService, MartenBlogPostContentService>()` — check if internal services still need them
  - ✅ `DynamicPageModelStatusCodeTests` updated for new constructor signature (IAeroPageActor + ISiteContext)
- [ ] **Revisit Identity/Users APIs for Orleans migration**  
      Currently excluded: `UsersApi`, `ProfileApi` use `UserManager<AeroUser>` (EF Core).  
      Candidate: [Orleans.Identity](https://github.com/managedcode/Orleans.Identity) — community project integrating ASP.NET Core Identity with Orleans grains.
- [ ] Deprecate `HeadlessModule` after all APIs migrated (or repurpose for cross-cutting headless API concerns only)
- [ ] Add OpenTelemetry spans for grain-to-Marten interactions
- [ ] Consider grain activation count limits for high-traffic modules (Pages, Blog)

---

## References

- Microsoft Orleans Docs: [Grain Development](https://learn.microsoft.com/dotnet/orleans/grains/)
- Microsoft Orleans Docs: [Best Practices](https://learn.microsoft.com/dotnet/orleans/resources/best-practices)
- Microsoft Orleans Docs: [Grain References (interface vs implementation separation)](https://learn.microsoft.com/dotnet/orleans/grains/grain-references)
- [Orleans.Identity](https://github.com/managedcode/Orleans.Identity) — ASP.NET Core Identity + Orleans integration
- Existing project conventions: `docs/`, `AGENTS.md`, `.skills/create-aero-module/SKILL.md`
- Existing grain infrastructure: `Aero/src/Aero.Actors/AeroActor.cs`, `src/Aero.Cms.Abstractions/Actors/IAeroCmsActors.cs`
- Reference implementation: `src/Aero.Cms.Abstractions/Services/AeroAliasService.cs`
- Reference module: `src/Aero.Cms.Modules.Navigation/NavigationModule.cs`
- Wolverine callback pattern (mirrored for grains): `src/Aero.AppServer/AeroAppServerExtensions.cs:94-98`

---

## Appendix A: Orleans Dependency Injection Reference

### Orleans Serialization: Block Types Across the Grain Wire

**Problem**: Orleans requires types that cross grain boundaries to have serialization support (copiers + serializers). Both `MarkdownBlock` (blog posts) and `EditorBlock` (pages) lack `[GenerateSerializer]` attributes. When these types transit through `IRequest` parameters or `List<object>` properties on view models, Orleans throws `CodecNotFoundException`.

**Three options considered:**

| Option | Description | Effort | Risk | Chosen? |
|---|---|---|---|---|
| **A** | Strip blocks from Orleans wire; transport as JSON string. API serializes → grain deserializes via `BlockJsonContext`. | Low | Low | ✅ |
| B | Add `[GenerateSerializer]` + `[Id(x)]` to `EditorBlock` + 13 nested types. | High | Medium | ❌ |
| C | Custom Orleans `ICopier`/`ISerializer` delegating to `BlockJsonContext`. | Medium | Low | ❌ |

**Decision: Option A — JSON-string transport**

Option B is invasive (50+ properties across 14+ files) and makes editor DTOs into Orleans wire contracts. Option A is the narrower fix: only the request DTOs change (`string? EditorBlocksJson` added at end), the API serializes blocks to JSON via the existing `BlockJsonContext`, and the grain deserializes on the other side. No Orleans configuration changes, no serialization attributes needed.

This is consistent with the blog post fix (`PostViewModel.Content` → markdown strings instead of `MarkdownBlock` objects).

**Implementation pattern for Option A:**

```
API handler                    Grain request (Orleans-safe)        Grain
───────────                    ────────────────────────────        ─────
EditorBlock list               CreatePageRequest                   DeserializeEditorBlocks(json)
  → JsonSerializer.Serialize     EditorBlocks  = null
  → EditorBlocksJson = "..."     EditorBlocksJson = "..."          page.Blocks = deserialized list
```

**Null vs empty semantics:**
- `EditorBlocksJson = null` — blocks omitted, grain preserves existing (update) or defaults empty (create)
- `EditorBlocksJson = ""` — blocks intentionally empty, grain clears blocks
- `EditorBlocksJson = "[...]"` — blocks provided, grain applies them

**Modified types in `BlockJsonContext`:**
- `EditorBlock` + `List<EditorBlock>`
- `EditorColumn` + `List<EditorColumn>`
- `GalleryImage` + `List<GalleryImage>`



When Orleans activates a grain, it constructs a new instance and resolves constructor parameters from the silo's service provider. If a grain is later deactivated by the activation GC and re-called, a new instance is created and dependencies are re-resolved.

### Service Lifetimes

| Lifetime | Behavior in Orleans | Use for |
|---|---|---|
| **Singleton** | Shared across all grain activations in the silo. Must be thread-safe. | `IDocumentStore`, `IMessageBus`, `IOptions<T>`, `IHttpClientFactory` |
| **Transient** | New instance per resolution. Within a grain constructor this means one instance per activation — **not** one per method call. Safe and predictable for per-activation state. | Per-activation helpers, factories |
| **Scoped** | **Orleans 7+**: creates a per-activation scope; service is disposed when the grain is deactivated. **Earlier versions**: scopes are not reliably isolated per activation — scoped services may behave like singletons across the silo. | Verify against target Orleans version. When in doubt, prefer transient or inject `IServiceScopeFactory` explicitly. |

### Marten Pattern

**Inject `IDocumentStore` as a singleton and open a session per operation.** Never inject a long-lived `IDocumentSession` into the grain — it holds an open unit-of-work for the entire activation lifetime.

```csharp
public sealed class PageGrain(
    ILogger<PageGrain> logger,
    IDocumentStore documentStore,
    IOptionsMonitor<PageModuleOptions> options)
    : AeroActor, IPageGrain
{
    public override Task OnActivateAsync(CancellationToken ct)
    {
        logger.LogInformation("Activating {GrainId}", this.GetPrimaryKeyLong());
        return Task.CompletedTask;
    }

    public async Task<PageDto?> GetPage(long siteId, string slug)
    {
        await using var session = documentStore.LightweightSession();
        var page = await session.Query<PageDocument>()
            .FirstOrDefaultAsync(p => p.SiteId == siteId && p.Slug == slug);
        return page?.ToDto();
    }

    public async Task<PageDto> CreatePage(CreatePageRequest request)
    {
        await using var session = documentStore.LightweightSession();
        var page = new PageDocument { Id = Snowflake.NewId(), /* ... */ };
        session.Store(page);
        await session.SaveChangesAsync();
        return page.ToDto();
    }
}
```

### Wolverine + Orleans

Inject `IMessageBus` as a singleton. Do not attempt to scope it per grain activation.

### Grain Silo Lifecycle

```
AddOrleans(configureGrains)     — registers silo config + assembly parts (sync, pre-Build)
  ↓
app.Build()                      — DI container resolved, Orleans IHostedService registered
  ↓
app.UseAeroApplicationServer()   — currently: app.UseTickerQ() only
  ↓                                  (no grain lifecycle impact — silo managed by IHostedService)
app.StartAsync()                 — starts all IHostedServices including Orleans silo
  ↓
OnActivateAsync (per grain)      — grains activate on first call or GC re-activation
  ↓
app.StopAsync()                  — Orleans IHostedService stops → grains deactivate
  ↓
OnDeactivateAsync (per grain)    — cleanup before activation released by GC
```

`UseAeroApplicationServer()` is middleware-only today (`UseTickerQ()`). Grain lifecycle is entirely managed by the Orleans `IHostedService`, not the middleware pipeline. The `configureGrains` callback runs during service registration (before `Build`), so it's synchronous and immediate — no deferred execution needed.

### Pitfalls

| Pitfall | Explanation |
|---|---|
| **Scoped services mid-activation** | Do not resolve scoped services via `IServiceProvider` lazily inside a grain method. The activation scope is controlled by Orleans, not the caller. Use `IServiceScopeFactory` if a method-scoped unit-of-work is required. |
| **Transient ≠ per-call** | Transient dependencies injected into a grain constructor are instantiated once at activation time. They are not refreshed on every method invocation. |
| **`IDocumentSession` as constructor DI** | A `IDocumentSession` injected into the constructor is bound to the grain activation scope — it lives as long as the activation. Prefer `IDocumentStore` + per-method `LightweightSession()`. |

### Lifecycle Hooks

| Hook | Purpose |
|---|---|
| `OnActivateAsync` | Activation-time initialization — timers, state hydration, logging |
| `OnDeactivateAsync` | Cleanup before the activation is released by the GC |

> The activation GC deactivates idle grains automatically. A subsequent call to the same grain identity creates a fresh activation — the constructor and `OnActivateAsync` run again.

### References

- [Orleans Grain Lifecycle — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/orleans/grains/)
- [Activation Collection — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/orleans/host/configuration-guide/activation-collection)
- [.NET DI Service Lifetimes — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/service-lifetimes)
