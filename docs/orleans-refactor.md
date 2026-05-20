# AeroCMS Refactoring Plan: Startup Encapsulation, API Decoupling, and Orleans Grain Migration

**Status**: Planning — Awaiting implementation  
**Date**: 2026-05-20  
**Effort**: Medium (3 phases, ~8-12 days)  
**Reviewed by**: @council (multi-model consensus)

---

## Table of Contents

1. [Objective](#objective)
2. [Current Architecture](#current-architecture)
3. [Phase 1: Startup Encapsulation](#phase-1-startup-encapsulation)
4. [Phase 2: API Migration from HeadlessModule](#phase-2-api-migration-from-headlessmodule)
5. [Phase 3: Orleans Grain Migration](#phase-3-orleans-grain-migration)
6. [Risk Register](#risk-register)
7. [Implementation Sequencing](#implementation-sequencing)
8. [Validation Strategy](#validation-strategy)
9. [Open Items / TODOs](#open-items--todos)

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
| `MapPreviewApi` (block fragment) | `Modules.Content` | `Areas/Api/v1/PreviewBlockFragmentApi.cs` | **Content-agnostic** endpoint |

> **Note on `PreviewBlockFragment`**: The `PreviewBlockFragment` endpoint accepts raw `BlockBase` — it is content-type agnostic (works for any block in any content type). Placing it in Pages or Blog would be wrong. It belongs in `Modules.Content` which owns the block system.

### Prerequisite: Auth & Antiforgery Audit

**Before any API migration**, audit all 22 API groups for auth requirements. Currently only `ContentItemsApi` and `ContentTypesApi` use `.RequireAuthorization()`. All `admin/` endpoints must require authorization after migration.

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

**Contract**: Grains use `IDocumentSession` as a **per-method-call factory pattern**. The grain does not store `IDocumentSession` as grain state. On each method invocation, it obtains a fresh session from the DI container. Grain state on `AeroActor` is used for caching/coordination only, not as the source of truth.

```csharp
public class AeroPageGrain : AeroActor, IAeroPageActor
{
    private readonly IServiceProvider _services;
    
    public AeroPageGrain(ILogger<AeroActor> log, IServiceProvider services) 
        : base(log) 
    {
        _services = services;
    }

    public async Task<AeroRequestResponse<PageViewModel>> CreateAsync(IRequest request, CancellationToken ct)
    {
        // Obtain fresh session per method call — not stored as grain state
        await using var scope = _services.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        
        // Business logic...
        // Marten is the sole source of truth.
    }
}
```

**Result**: Zero state drift risk. Marten remains the authoritative data store.

#### 3.3 Grain Registration Strategy

**Module-level registration**: Each module registers its grains in `ConfigureServices` via the silo builder:

```csharp
// In PagesModule.ConfigureServices:
public override void ConfigureServices(IServiceCollection services, IConfiguration? config, IHostEnvironment? env)
{
    services.AddScoped<IPageContentService, MartenPageContentService>();
    // ... existing registrations ...
    
    // Register grain on the silo
    services.AddSingleton<IAeroPageActor, AeroPageGrain>();  // via Orleans conventions
}
```

The existing `builder.AddAeroApplicationServer()` call in `Program.cs` bootstraps the Orleans silo; grains are discovered via the source-generated catalog.

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
    Task<AeroRequestResponse<SettingViewModel>> GetByKeyAsync(string key, CancellationToken ct);
    Task<AeroRequestResponse<SettingViewModel>> SetAsync(string key, string value, CancellationToken ct);
    Task<AeroRequestResponse<bool>> DeleteByKeyAsync(string key, CancellationToken ct);
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
    [FromServices] IAeroPostService postService,  // ← grain-backed service
    CancellationToken ct)
{
    var result = await postService.CreateAsync(request, ct);
    return result.Match(
        ok => TypedResults.Created($"/api/admin/blogs/{ok.Id}", ok),
        error => TypedResults.BadRequest(error));
}
```

---

## Risk Register

| # | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R1 | **Circular dependency in Web.Core** | Medium | High | RESOLVED: Use new `Aero.Cms.Web.Bootstrap` project instead |
| R2 | **PreviewBlockFragment orphaned after split** | Low (caught early) | Medium | RESOLVED: Move to `Modules.Content` |
| R3 | **Auth/antiforgery regression** | Medium | High | **Prerequisite**: Audit all 22 API groups before migration |
| R4 | **Module API route ordering regression** | Low | Medium | Use conformance test to verify route resolution |
| R5 | **Marten + Orleans lifecycle mismatch (NRE)** | High | High | RESOLVED: Use per-method-call `IServiceProvider` factory pattern |
| R6 | **Orleans grain state vs Marten document state drift** | High (if misconfigured) | High | RESOLVED: Marten is sole source of truth; grains use short-lived sessions |
| R7 | **Unproven grain implementation pattern** | Medium | Medium | Mitigated: Start with Aliases (simplest) to prove pattern |
| R8 | **Pages grain complexity (event sourcing)** | High | High | Mitigated: Do last, after pattern proven |

---

## Implementation Sequencing

```
Phase 1 (1-2 days): Startup Encapsulation
├── Create src/Aero.Cms.Web.Bootstrap/ project
├── Move static methods from Program.cs → AeroStartupPipeline.cs
├── Program.cs → ~30-line delegator
├── Update solution file (.slnx) to include new project
└── Verify: dotnet run starts both setup and main app correctly

Phase 2 (4-6 days): API Migration
├── Step 1: Auth/Antiforgery Audit (all 22 API groups)
│   └── Add RequireAuthorization() to all admin/ groups
│   └── Preserve DisableAntiforgery() on upload endpoints
├── Step 2: Split PreviewApi (page→Pages, blog→Blog, block→Content)
├── Step 3: Batch 1 — Low-risk modules
│   └── Aliases, Tags, Categories, Settings, Themes, Modules, JWT, Dashboard
├── Step 4: Batch 2 — Service-backed modules
│   └── Pages, Blog, Docs, Content, Media, Files, Audit, Users, Profile
├── Step 5: Cleanup — HeadlessModule.RunAsync emptied
└── Step 6: Conformance test — enumerate all registered routes, verify resolution

Phase 3 (4-7 days): Orleans Grain Migration
├── Step 1: Prove pattern with Aliases (simplest grain)
│   └── Implement AeroAliasGrain : AeroActor
│   └── Wire through existing AeroAliasService
│   └── Verify: API → Service → Grain → Marten → Response
├── Step 2: Batch — Simple CRUD grains (Tags, Categories, Settings)
├── Step 3: Batch — Media grain (proves binary/file interaction)
├── Step 4: Pages grain (highest complexity — event sourcing, drafts, publish)
├── Step 5: Remaining grains (Blog, Docs, Content)
└── Step 6: Remove direct IDocumentSession from all migrated API handlers

Phase 4 (1 day): Cleanup & Verification
├── Remove empty HeadlessModule (or mark deprecated)
├── Verify: dotnet build (no warnings)
├── Verify: dotnet test (unit + integration)
└── Verify: dotnet run (full startup, Setup + Main app)
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
- [ ] Create `src/Aero.Cms.Web.Bootstrap/` project with correct project references
- [ ] Audit all 22 API groups for auth/antiforgery requirements (see table in Phase 2)
- [ ] Move `PreviewBlockFragment` to `Modules.Content`

### Future
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
