
> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). Marten
> was migrated out in [`surrealdb-marten-port.md`](surrealdb-marten-port.md).
> This document is a historical implementation record; its Marten/PostgreSQL
> persistence details do not reflect the current stack.

# Aero CMS — Docs Module Implementation Plan

> **Status:** Draft v4 — foundation complete, UI redesign planned
> **Module:** `Aero.Cms.Modules.Docs`  
> **Pattern:** Vertical slice, GitBook-style knowledge base with block composition (reusing Pages block types)
> **Last Updated:** 2026-05-24

---

## 1. Purpose & Vision

The Docs module powers a **GitBook-style knowledge base / documentation repository** within Aero CMS. It mirrors the Pages module architecture — SSR block rendering with NeoUI components, a tree hierarchy for content organisation, and a rich admin editor with drag-and-drop tree management.

### Key Design Decisions

- **Block renderer** — NeoUI blocks rendered as SSR (Static render mode), same as Pages. Markdig is available for in-block Markdown rendering when needed, but the page composition uses blocks, not a single Markdown content area.
- **Tree hierarchy** — `ParentId` + `Order` on `DocsPage` enables arbitrary-depth nesting with drag-and-drop reordering in the admin
- **Separate files** — follows SRP and vertical slice architecture; does NOT reuse Pages module files even where patterns overlap
- **Caching** — dual-layer: OutputCache (HTTP) + FusionCache (data) with Wolverine-driven eviction
- **Markdig** — available for individual Markdown blocks within the block composition (not the sole rendering engine)
- **CSS** — Tailwind v4 CDN with `type="text/tailwindcss"` processing
- **Editor UX** — `DocsEditor.razor` with tree sidebar (drag-and-drop, right-click context menu) + blocks canvas, mirroring the Pages editor experience

### GitBook Skeleton Reference

The UI/UX skeleton lives at `docs-skeleton/` with four HTML pages:

| Skeleton File | Purpose | Maps To |
|---|---|---|
| `index.html` | Docs overview / landing page | `DocsIndex.cshtml` |
| `cms.html` | Category/card grid page | `DocsIndex.cshtml` (database-driven) |
| `setup.html` | Sidebar tree + content + "On this page" | `Doc.cshtml` |
| `developing.html` | Nested content page with active sidebar | `Doc.cshtml` |
| `js/app.js` | Search overlay, sidebar toggle, scroll spy | Alpine.js in CSHTML |

All "Umbraco" references in the skeleton are replaced with "Aero" in the implementation.

---

## 2. Current State Assessment

### 2.1 What Exists (Complete / Partially Complete)

| File | Status | Notes |
|------|--------|-------|
| `Aero.Cms.Core.Entities/DocsPage.cs` | ✅ Complete | Has PublishedVersion, LayoutRegions, BlockSchemaVersion, ToViewModel() |
| `Aero.Cms.Core.Entities/DocsEditorState.cs` | ✅ Complete | New entity mirroring `PageEditorState` |
| `Aero.Cms.Abstractions/Models/DocViewModel.cs` | ✅ Complete | Orleans-serializable view model |
| `Aero.Cms.Abstractions/Events/AeroEvents.cs` | ✅ Complete | `DocsPageContentUpdatedEvent`, DocViewModel CUD events |
| `Aero.Cms.Modules.Docs/DocsModule.cs` | ✅ Complete | Module registration, Marten indexes + Ngram + UseOptimisticConcurrency, DI factory |
| `Aero.Cms.Modules.Docs/IDocsService.cs` | ✅ Complete | 14 methods (CRUD + published + paging + tree + request-based) |
| `Aero.Cms.Modules.Docs/DocsService.cs` | ✅ Complete | Renamed to `DocsContentService`; mirrors `MartenPageContentService` with explicit site scoping, Wolverine events, FusionCache, compiled queries |
| `Aero.Cms.Modules.Docs/Queries/DocsQueries.cs` | ✅ Complete | 4 `AeroCompiledQuery` classes (published, published-paged, count, by-slug) |
| `Aero.Cms.Modules.Docs/Grains/AeroDocsGrain.cs` | ✅ Complete | Thin delegation pattern; per-operation sessions + `CreateDocsService(session, siteId)` |
| `Aero.Cms.Modules.Docs/DocsOperationContext.cs` | ✅ Complete | Record (SiteId, Actor = "system") |
| `Aero.Cms.Modules.Pages/PageOperationContext.cs` | ✅ Complete | Record (SiteId, Actor = "system") |
| `Aero.Cms.Modules.Docs/Caching/DocsCacheTags.cs` | ✅ Complete | Cache tag constants (auto-invalidated via Wolverine) |
| `Aero.Cms.Modules.Docs/DocsMartenConfiguration.cs` | 🗑️ Removed | Redundant — folded into `DocsModule.Configure()` |
| `Aero.Cms.Modules.Docs/Areas/Docs/Pages/DocsIndex.cshtml` | ✅ Complete | Data-driven cards from `IDocsService.GetPublishedAsync()` |
| `Aero.Cms.Modules.Docs/Areas/Docs/Pages/DocsIndex.cshtml.cs` | ✅ Complete | Injects `IDocsService`, calls `GetPublishedAsync()`, unwraps `Result` |
| `Aero.Cms.Modules.Docs/Areas/Docs/Pages/Doc.cshtml` | ⚠️ Partial | Uses `IDocsService.GetPublishedBySlugAsync()`; needs three-column layout redesign (planned) |
| `Aero.Cms.Modules.Docs/Areas/Docs/Pages/Doc.cshtml.cs` | ✅ Complete | Injects `IDocsService`, calls `GetPublishedBySlugAsync()`, unwraps `Result` |
| `Aero.Cms.Modules.OutputCache/OutputCacheModule.cs` | ✅ Complete | `DocsPolicy` + `DocsIndexPolicy` registered |
| `Aero.Cms.Modules.Docs/wwwroot/css/docs.css` | ✅ Complete | Tailwind prose styles |
| `Aero.Cms.Modules.Cache/Handlers/ContentUpdatedHandler.cs` | ✅ Complete | Handles `DocsPageContentUpdatedEvent` → cache invalidation |
| `Aero.Cms.Modules.Cache/Services/FusionCacheInvalidationService.cs` | ✅ Complete | Evicts `"docs-index"` tag on docs content updates |

> **Update 2026-05-24:** `DocsPage` is complete for the current Markdown-based implementation, but the block editor slice must add the same published/draft fields used by Pages: published `LayoutRegions`, `PublishedVersion`, and `BlockSchemaVersion`, plus a separate `DocsEditorState` that follows `PageEditorState`.

### 2.2 What is Missing

| Feature | Priority | Effort | Status |
|---------|----------|--------|--------|
| **Docs Spaces listing page** (Manager Radzen Grid) | HIGH | Small | ✅ Initial complete |
| **Docs Space editor** (Manager-hosted, mirrors public Doc layout + left explorer panel) | HIGH | Large | 🟡 Initial editor complete; block-state integration still pending |
| **Neo Tree explorer** (left panel — space outline with sections/sub-sections, multi-select) | HIGH | Medium | 🟡 Native outline + multi-select/search implemented; Neo Tree integration still pending |
| **Neo Context Menu** on tree nodes (add, update, delete, rename, duplicate) | HIGH | Medium | 🟡 Initial Neo Context Menu actions implemented |
| **Neo Sortable drag-and-drop** (Full item drag within tree for reorder/reparent) | HIGH | Medium | 🟡 Native move/reparent controls implemented; Neo Sortable drag/drop still pending |
| **Block render cache** (N+1 prevention, reuse Pages pattern) | HIGH | Small | ⬜ Planned |
| **BlockBase adapters** for Docs (reuse Pages block types and renderers) | HIGH | Medium | 🟡 Reusing Pages blocks |
| **Docs content service alignment** (mirror `AeroPageGrain` + `PageContentService`) | HIGH | Medium | ✅ Complete |
| **Refactor PageModels** to use `IDocsService` not `IQuerySession` | HIGH | Small | ✅ Complete |
| **FluentValidation validator** (`DocsPageValidator`) | HIGH | Small | ✅ Complete (inline in DocsContentService) |
| **Remove `DocsMartenConfiguration.cs`** (redundant) | LOW | Trivial | ✅ Complete |
| **Search indexing** (Marten GIN/Ngram) | HIGH | Medium | ✅ Complete |
| **Cache eviction handler** for OutputCache tags | LOW | Small | ✅ Already in place via Wolverine FX |
| **IDocsTreeService** for sidebar hierarchy + breadcrumbs | HIGH | Medium | ✅ Admin + public sidebar/breadcrumb operations implemented |
| **Search service** (Ctrl+K overlay) | MEDIUM | Medium | ⬜ Planned |
| **Sidebar CSHTML partial** with tree nav | HIGH | Medium | ✅ Complete (service-backed nested layout) |
| **Breadcrumb CSHTML partial** | MEDIUM | Small | 🟡 Service-backed inline breadcrumb implemented; partial extraction pending |
| **"On this page" scroll spy** (headings in content) | MEDIUM | Medium | 🟡 Markdig heading extraction implemented; scroll spy behavior pending |
| **Seed data** (starter docs structure) | MEDIUM | Small | ⬜ Planned |
| **Version selector** (skeleton pattern) | LOW | Small | ⬜ Deferred |
| **Doc.cshtml redesign** (three-column layout matching skeleton) | HIGH | Large | ⬜ Planned (council reviewed) |
| **"On this page" Markdig heading extraction** | HIGH | Medium | ⬜ Planned |
| **_DocsLayout + _ViewStart** (nested layout for sidebar/right-panel) | HIGH | Medium | ⬜ Planned |
| **DocsOperationContext + PageOperationContext** records | HIGH | Small | ✅ Complete |
| **Compiled queries** (DocsQueries.cs) | HIGH | Small | ✅ Complete |
| **IHttpContextAccessor removal** (transport-leak cleanup) | HIGH | Small | ✅ Complete |

---

## 3. Architecture Overview

### 3.0 Request Flow (System Architecture)

```
┌──────────────┐     ┌─────────────────┐     ┌──────────────────┐
│ HTTP Request │ ──▶ │ Page / WebApi   │ ──▶ │ AeroDocsGrain    │
│ (browser)    │     │ Minimal API or  │     │ (Orleans Actor)  │
│              │     │ Razor Page      │     │                  │
└──────────────┘     └─────────────────┘     └────────┬─────────┘
                                                        │
                                                        ▼
                                      ┌────────────────────────────┐
                                      │ DocsContentService         │
                                      │ constructed per operation  │
                                      │ with IDocumentSession +    │
                                      │ FixedSiteContext           │
                                      └─────────────┬──────────────┘
                                                    │
                                                    ▼
                                      ┌────────────────────────────┐
                                      │ IDocumentStore/PostgreSQL  │
                                      └────────────────────────────┘

Pattern: Thin HTTP → Grain → per-operation content service → Marten
- Follow the working Pages design: `AeroPageGrain` opens `LightweightSession()` / `QuerySession()`, manually constructs `MartenPageContentService` with a fixed site context, and delegates business logic to that service.
- Docs should mirror that shape with `AeroDocsGrain` + `DocsContentService` / refactored `DocsService`.
- Do not inject a scoped repository or scoped `IDocumentSession` into the grain. The grain owns `IDocumentStore`, creates a session per operation, and passes an explicit `SiteId`.
- `DocsContentService` owns Marten queries, validation, FusionCache, Wolverine event publishing, and all business rules.
- Public Razor pages can use the scoped service directly when they are already inside the ASP.NET Core request scope, but every public and admin read/write path must resolve through a `SiteId`.
- Exception: `SeedDataService` may use a service/session directly because Orleans is not running during setup.
```

> **Decision (2026-05-24):** The grain now mirrors `AeroPageGrain` exactly — owns `IDocumentStore`, opens per-operation sessions, constructs `DocsContentService` with `FixedSiteContext(siteId)` and explicit `actor: "system"`. The service uses compiled queries (`AeroCompiledQueryList`, `AeroCompiledQuery<T, TOut>`) for published docs lookups and delegates all business logic to `DocsContentService`. `IHttpContextAccessor` was removed from the service constructor (transport leak cleanup); actor is now an explicit `string? actor` parameter.

### 3.0b Admin Flow (Manager)

```
┌──────────────┐     ┌─────────────────┐     ┌──────────────────┐
│  Manager Nav  │ ──▶ │  Spaces Listing  │ ──▶ │  Space Editor    │
│  "Docs" link  │     │  (Radzen Grid)  │     │  (Manager-hosted)│
└──────────────┘     └─────────────────┘     └──────────────────┘
                              │                       │
                              ▼                       ▼
                     ┌─────────────────┐     ┌──────────────────┐
                     │  AeroDocsGrain  │     │  Neo Tree        │
                     │  (CRUD + query) │     │  (explorer left) │
                     └─────────────────┘     └──────────────────┘
                                                     │
                                              ┌──────┴──────┐
                                              │ Neo Context  │
                                              │ Menu (CRUD)  │
                                              └──────────────┘
                                                     │
                                              ┌──────┴──────┐
                                              │ Neo Sortable │
                                              │ (drag-drop)  │
                                              └──────────────┘
```

- **Spaces Listing** — Radzen Grid showing all spaces (like Posts/Pages admin tables). Each row links to the space editor.
- **Space Editor** — mirrors public `Doc.cshtml` layout + **explorer panel** on the left using the **Neo Tree** component.
- **Neo Tree** — displays the space outline (sections, sub-sections). Supports multi-select for batch operations.
- **Neo Context Menu** — right-click on any tree node for add/update/delete/rename/duplicate.
- **Neo Sortable** — Full item drag within the tree for reordering and reparenting (see [Neo Sortable docs](https://demos.neoui.io/primitives/sortable)).

```
┌─────────────────────────────────────────────────────────────────────┐
│  Aero.Cms.Modules.Docs  (Vertical Slice)                            │
│                                                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │ DocsModule   │  │ DocsRepos..  │  │ DocsTreeSvc  │              │
│  │ (Registration)│  │ (Marten)     │  │ (Hierarchy)  │              │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘              │
│         │                 │                  │                      │
│  ┌──────┴─────────────────┴──────────────────┴───────────────────┐  │
│  │                       DocsService                             │  │
│  │  (CRUD + Search + Cache + Events + Tree Operations)           │  │
│  └────┬──────────────────────────────────────────────────┬──────┘  │
│       │                                                  │         │
│  ┌────┴───────────┐  ┌──────────────┐  ┌───────────────┐│         │
│  │ Validation     │  │ Razor Pages  │  │ Caching       ││         │
│  │ (Fluent)       │  │ (Public)     │  │ (Tags+Events) ││         │
│  └────────────────┘  └──────────────┘  └───────────────┘│         │
│                                                          │         │
│  ┌───────────────────────────────────────────────────────┘         │
│  │                                                                 │
│  │  ┌──────────────────────────────────────────────────────┐      │
│  │  │  DocsEditor.razor  (Admin Blazor Component)          │      │
│  │  │  ┌──────────────┐  ┌────────────────────────────┐   │      │
│  │  │  │ Tree Panel   │  │ Blocks Canvas              │   │      │
│  │  │  │ (Drag/Drop,  │  │ (SSR NeoUI Block Renders)  │   │      │
│  │  │  │  Context CRUD)│  │                            │   │      │
│  │  │  └──────────────┘  └────────────────────────────┘   │      │
│  │  │  ┌────────────────────────────────────────────┐     │      │
│  │  │  │ Block Menu (Draggable block types)         │     │      │
│  │  │  └────────────────────────────────────────────┘     │      │
│  │  └──────────────────────────────────────────────────────┘      │
│  └─────────────────────────────────────────────────────────────────┘
└─────────────────────────────────────────────────────────────────────┘

Cross-cutting:
  • Aero.Core.Entities (ISnowflakeEntity, Entity)
  • Aero.Core.Railway (Result<T>, Option<T>, Bind, Map)
  • IDocumentStore / IDocumentSession per operation
  • Aero.Cms.Abstractions.Blocks (BlockBase, IBlockRenderer)
  • Wolverine (IMessageBus for event publishing)
  • ZiggyCreatures.FusionCache (data cache)
  • NeoUI (Blazor component library for block renders)
```

### Dependency Graph

```
Aero.Cms.Modules.Docs
  ├── Aero.Core                       (IEntity, ROP types)
  ├── Aero.Marten                     (IDocumentStore / IDocumentSession)
  ├── Aero.Modular                    (IAeroModule, AeroModuleBase)
  ├── Aero.Cms.Core.Entities          (DocsPage)
  ├── Aero.Cms.Core                   (AeroConstants)
  ├── Aero.Cms.Abstractions           (DocViewModel, events, BlockBase, IBlockRenderer)
  ├── Aero.Cms.Web.Core               (AeroWebModule, IAeroPipelineModule)
  ├── Aero.Cms.Shared                 (LayoutRegionRenderer, block components)
  ├── Aero.Cms.Modules.OutputCache    (DocsPolicy / DocsIndexPolicy)
  ├── NeoUI                           (Blazor SSR UI components)
  ├── Markdig                         (Markdown → HTML for MarkdownBlock)
  ├── Marten                          (IDocumentSession)
  ├── WolverineFx                     (IMessageBus events)
  ├── ZiggyCreatures.FusionCache      (data caching)
  └── FluentValidation                (validator)
```

---

## 4. Entity Model

### 4.1 `DocsPage` (Aero.Cms.Core.Entities)

```csharp
// D:\proj\microbians\AeroCMS\src\Aero.Cms.Core.Entities\DocsPage.cs
public sealed class DocsPage : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? MarkdownContent { get; set; }

    // SEO
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }

    // Publication
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    public DateTimeOffset? PublishedOn { get; set; }
    public long PublishedVersion { get; set; }
    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;

    // Presentation
    public bool ShowHeaderNavigation { get; set; } = true;
    public string? HeaderImageUrl { get; set; }

    // Tree
    public long? ParentId { get; set; }
    public int Order { get; set; }

    // Published block layout, built from DocsEditorState on publish.
    public List<LayoutRegion> LayoutRegions { get; set; } = [];

    // Block schema versioning
    /// <summary>
    /// Incremented when the block schema changes. Used for idempotent migrations
    /// of LayoutRegions to the latest block type definitions.
    /// Mirroring PageDocument.BlockSchemaVersion.
    /// </summary>
    public int BlockSchemaVersion { get; set; }
}
```

**Base class chain:** `DocsPage` → `Entity` → `Entity<long>` → `EntityBase<long>` → `IEntity<long>`
**Inherited fields:** `Id` (long, Snowflake), `CreatedOn`, `ModifiedOn`, `CreatedBy`, `ModifiedBy`

### 4.2 Marten Indexes (defined in `DocsModule.Configure()`)

```csharp
opts.Schema.For<DocsPage>().DocumentAlias("docs");
opts.Schema.For<DocsPage>().UseOptimisticConcurrency(true);   // NEW: prevent lost updates on tree reorder
opts.Schema.For<DocsPage>().Index(x => x.SiteId);              // multi-tenant filter
opts.Schema.For<DocsPage>().UniqueIndex(x => x.SiteId, x => x.Slug);  // slug uniqueness per site
opts.Schema.For<DocsPage>().Index(x => x.ParentId);            // tree queries
opts.Schema.For<DocsPage>().Index(x => x.Order);               // sibling sort
opts.Schema.For<DocsPage>().Index(x => x.PublishedOn);         // date range
opts.Schema.For<DocsPage>().Index(x => x.CreatedOn);
opts.Schema.For<DocsPage>().Index(x => x.ModifiedOn);

// Search (NEW — Phase 1 per council review C5)
opts.Schema.For<DocsPage>().NgramIndex(x => x.Title);
opts.Schema.For<DocsPage>().NgramIndex(x => x.MarkdownContent);

// Editor state (NEW — Phase 1 per council review C1)
opts.Schema.For<DocsEditorState>().Identity(x => x.Id); // same Id as DocsPage
opts.Schema.For<DocsEditorState>().Index(x => x.SiteId);
```

**Tree design:** Materialized path pattern (parent pointer). Each `DocsPage` stores its `ParentId`. The tree is assembled in-memory by the `DocsTreeService`, not via recursive CTEs or nested sets.

### 4.3 Spaces Concept

"Spaces" are the top-level children under the virtual docs root. The root page has `Slug == "docs"` and `ParentId == null`. A space is a child of that root and acts as the container for its parent/child docs tree, matching the existing seeded docs shape and the way Pages models parent/child content. The public docs home page (`DocsIndex.cshtml`) displays those root children as feature cards.

**Seeding pattern** (for dev/staging):
```
/docs               (landing page, virtual root)
  ├── fundamentals  (space)
  │   ├── setup     (chapter)
  │   │   ├── requirements  (section)
  │   │   └── installation  (section)
  │   └── architecture      (chapter)
  └── api-reference  (space)
      ├── rest-api    (chapter)
      └── graphql     (chapter)
```

---

## 4b. Block Architecture

The Docs module uses the same block composition model as the Pages module. Doc pages are composed of `BlockBase` instances arranged in `LayoutRegion → LayoutColumn → BlockPlacement` structures — mirroring Pages with NeoUI SSR rendering.

### 4b.1 Relationship to `DocsPage`

Each `DocsPage` stores the published render artifact, mirroring `PageDocument.LayoutRegions`. The manifest is a serialized collection of `LayoutRegion` objects stored as JSONB in Marten:

```csharp
// Conceptual: what gets added to DocsPage (or stored separately)
public sealed class DocsPage : Entity, ISiteOwned
{
    // ... existing fields ...

    /// <summary>
    /// Published layout manifest: regions → columns → block placements.
    /// Rendered SSR by LayoutRegionRenderer components during public page rendering.
    /// </summary>
    public List<LayoutRegion> LayoutRegions { get; set; } = [];
}
```

> **Decision (2026-05-24):** Use the PageEditor style because it already works in AeroCMS. `LayoutRegions` are still the better public render artifact, but they should be built on publish from editor block state. The draft workspace must follow `PageEditorState`: editor placements + `BlockIdMap` + draft versioning, not a separate `DraftRegions` layout.

**`DocsEditorState` entity (conceptual):**
```csharp
// D:\proj\microbians\AeroCMS\src\Aero.Cms.Core.Entities\DocsEditorState.cs
public sealed class DocsEditorState : ISiteOwned
{
    // Same Id as the corresponding DocsPage.
    public long Id { get; set; }
    public long SiteId { get; set; }
    public long DraftVersion { get; set; }
    public List<EditorBlockPlacement> Blocks { get; set; } = [];
    public Dictionary<string, long> BlockIdMap { get; set; } = [];
    public DateTimeOffset LastModified { get; set; }
}
```

### 4b.2 Block Types (Reuse Pages Block Types)

> **Decision (2026-05-24):** Do NOT create Doc-specific block types. The existing Pages module block types — `MarkdownBlock`, `RichTextBlock`, `ImageBlock`, `CodeBlock`, `CalloutBlock`, `TableBlock`, `CarouselBlock`, etc. — serve the same role for docs. All block types and their `IBlockRenderer` implementations are reused directly from `Aero.Cms.Modules.Pages`. The Docs module registers no new block types. This avoids code duplication and ensures consistent rendering between Pages and Docs content.

**Referenced block types (from Pages):**

- `MarkdownBlock` — Markdown content via Markdig
- `RichTextBlock` — Rich text/HTML content
- `CodeBlock` — Syntax-highlighted code
- `ImageBlock` — Image with caption
- `CalloutBlock` — Info/warning/tip/danger boxes
- `TableBlock` — Data tables
- `CarouselBlock` — Image galleries/carousels
- `ChildPagesBlock` — Auto-generated child page links

### 4b.3 Block Renderers

Block renderers from the Pages module are reused directly. No new doc-specific renderers are needed. The existing Pages `IBlockRenderer` implementations handle all block types:

```
MarkdownBlockRenderer   → <MarkdownBlockView Content="@block.Content" />
CodeBlockRenderer       → <CodeBlockView Code="@block.Code" Language="@block.Language" />
CalloutBlockRenderer    → <CalloutBlockView Type="@block.Type" Title="@block.Title" Content="@block.Content" />
ImageBlockRenderer      → <ImageBlockView Url="@block.ImageUrl" Alt="@block.AltText" Caption="@block.Caption" />
CarouselBlockRenderer   → <CarouselBlockView Items="@block.Items" Layout="@block.Layout" />
TableBlockRenderer      → <TableBlockView Rows="@block.Rows" HasHeader="@block.HasHeader" />
ChildPagesBlockRenderer → <ChildPagesBlockView ParentId="@block.OwnerId" MaxItems="@block.MaxItems" />
```

### 4b.4 Source-Generated Discovery

Per AGENTS.md ("Avoid using reflection; prefer source generators for code discovery and generation"), block types and renderers are discovered via source generators — the same mechanism used by the Pages module. Each block type is annotated with `[BlockRenderer]` or discovered via the existing source generator pipeline.

### 4b.5 Rendering Pipeline (Public Page)

The public `Doc.cshtml` rendering pipeline mirrors Pages:

1. `DocModel.OnGetAsync()` loads the `DocsPage` and its `LayoutRegions`
2. `BlockRenderCache.PreloadAsync()` batch-loads all referenced `BlockBase` IDs in a single Marten query (N+1 prevention)
3. `Doc.cshtml` iterates `LayoutRegions` and renders `<component type="typeof(LayoutRegionRenderer)" render-mode="Static" param-Region="@region" />`
4. Each `LayoutRegionRenderer` resolves block types via source-generated adapters and renders NeoUI components in `Static` render mode

```html
<!-- Doc.cshtml (simplified) -->
@if (pageDoc.LayoutRegions.Count > 0)
{
    foreach (var region in pageDoc.LayoutRegions.OrderBy(r => r.Order))
    {
        <component type="typeof(LayoutRegionRenderer)" 
                   render-mode="Static" 
                   param-Region="@region" />
    }
}
else
{
    <!-- Empty state / fallback to plain Markdig if LayoutRegions is empty -->
    <div class="prose prose-slate max-w-none">
        @Html.Raw(Markdown.ToHtml(pageDoc.MarkdownContent ?? ""))
    </div>
}
```

**Fallback behaviour:** If a `DocsPage` has no layout regions (legacy or newly created), the page falls back to rendering `MarkdownContent` directly via Markdig. This provides backward compatibility with existing docs content.

### 4b.6 N+1 Prevention: `BlockRenderCache`

The same `BlockRenderCache` pattern from Pages is reused:

```csharp
// In DocModel.OnGetAsync():
if (DocsPage.LayoutRegions.Count > 0)
{
    var blockIds = DocsPage.LayoutRegions
        .SelectMany(r => r.Columns)
        .SelectMany(c => c.Blocks)
        .Where(p => p.BlockId > 0)
        .Select(p => p.BlockId)
        .Distinct()
        .ToList();

    if (blockIds.Count > 0)
        await blockCache.PreloadAsync(blockIds, blockService, ct);
}
```

---

## 5. Manager Admin — Spaces & Space Editor

The Docs admin experience lives in the **Manager module** (same as Pages and Posts editors). It has two views:

1. **Spaces Listing** — Radzen Grid listing all doc spaces (index page). Accessed via the Manager's "Docs" nav item (left sidebar + header dropdown).
2. **Space Editor** — Individual space/space-editor page with an explorer panel (Neo Tree) on the left and the page preview on the right.

> **Update 2026-05-25:** The Manager flow now follows this split: `/manager/docs` is the Spaces listing, and the Space editor lives under `src/Aero.Cms.Shared/Pages/Manager/DocsEditor/` with routes `/manager/docs/{spaceId:long}` and `/manager/docs/{spaceId:long}/sections/{sectionId:long}`. The current editor is space-scoped and includes a native hierarchical outline, outline search, multi-select batch publish/unpublish/delete, Neo Context Menu actions, metadata/SEO/header attributes, Markdown preview, and publish/unpublish API wiring. Full Neo Tree replacement, Neo Sortable drag/drop, and `DocsEditorState` block-canvas persistence remain the next slices.

### 5.1 Spaces Listing (Radzen Grid)

The landing page for the Manager's "Docs" nav item. Displays all spaces (`DocsPage` records whose parent is the virtual root page with `Slug == "docs"`) in a Radzen DataGrid — same pattern as the Posts and Pages admin tables.

- **Columns**: Title, Slug, Sections (child count), Published, PublishedOn, ModifiedOn
- **Actions**: Edit (opens Space Editor), Delete, Publish/Unpublish toggle
- **Create**: "+ New Space" button opens a dialog or inline row for title + slug

**Routes**:
- Manager UI: `/manager/docs`, `/manager/docs/{spaceId:long}`, `/manager/docs/{spaceId:long}/sections/{sectionId:long}`
- Admin API: `/api/admin/docs` via `DocsApi.MapDocsApi()`
- Creation flow: `POST /api/admin/docs` → `AeroDocsGrain.CreateAsync` → per-operation `DocsContentService`

### 5.2 Space Editor Layout

The space editor mirrors the **public `Doc.cshtml` layout** with the addition of a left **explorer panel** using the **Neo Tree** component (`./Neo` submodule). This gives editors a live preview of the rendered page while managing the doc hierarchy.

```
┌──────────────────────────────────────────────────────────────────┐
│  Space Editor (Manager-hosted)                                     │
│  ┌──────────────────┐  ┌───────────────────────────────────────┐ │
│  │  Explorer Panel  │  │  Top Toolbar                           │ │
│  │  (Neo Tree)      │  │  [Save] [Preview] [Publish] [Delete]   │ │
│  │  320px           │  ├───────────────────────────────────────┤ │
│  │                  │  │                                       │ │
│  │  📁 Space Root   │  │  ┌─────────────────────────────────┐  │ │
│  │   📄 Section A   │  │  │  Page Preview                    │  │ │
│  │   📄 Section B ◉ │  │  │  (mirrors public Doc.cshtml      │  │ │
│  │     📄 Sub 1     │  │  │   layout + block rendering)      │  │ │
│  │     📄 Sub 2     │  │  │                                 │  │ │
│  │  📁 Another Space│  │  │  Region 1: [Block A] [Block B] │  │ │
│  │   📄 Section C   │  │  │  Region 2: [Block C]            │  │ │
│  │                  │  │  │                                 │  │ │
│  │  [+ Add Section] │  │  └─────────────────────────────────┘  │ │
│  │                  │  │                                       │ │
│  └──────────────────┘  │  ┌────────────────────────────────────┐│ │
│                        │  │ Block Menu Tray (draggable types)  ││ │
│                        │  │ [Markdown] [Code] [Callout] ...    ││ │
│                        │  └────────────────────────────────────┘│ │
│                        └───────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

**Key difference from public layout**: The explorer panel replaces the public sidebar. The page preview (right side) renders blocks exactly as they appear on the public `Doc.cshtml` page — SSR NeoUI components in `Static` render mode.

### 5.3 Neo Tree Explorer

The explorer panel uses the **Neo TreeView** component ([docs](https://demos.neoui.io/components/tree-view)) to display the space outline — all sections and sub-sections within the current space. This is NOT a page-level tree of all docs; it's scoped to the space being edited.

**Features:**
- **Hierarchical display** — root space at top, children indented by level
- **Multi-select** — checkboxes or Ctrl+click to select multiple nodes for batch delete or move
- **Drag-and-drop** — powered by **Neo Sortable** ([docs](https://demos.neoui.io/primitives/sortable)) with **Full item drag** within the tree for reordering and reparenting
- **Selection indicator** — currently-edited section is highlighted
- **Inline rename** — click an already-selected node to rename

### 5.4 Neo Context Menu

Right-click on any tree node opens a **Neo Context Menu** with actions:

| Action | Behaviour |
|---|---|
| **Edit** | Loads the section into the page preview for editing |
| **Add Child Section** | Creates a new `DocsPage` as a child of the selected node |
| **Rename** | Inline edit of the section title |
| **Duplicate** | Clones the section with " (copy)" suffix |
| **Move To…** | Opens a modal to select a new parent section |
| **Delete** | With confirmation dialog; cascading delete of children |
| **View Published** | Opens the public `/docs/{slug}` in a new tab |
| **Toggle Published/Draft** | Switches `PublicationState` |

### 5.5 Drag-and-Drop (Neo Sortable)

Tree reordering and reparenting uses the **Neo Sortable primitive** with **Full item drag** mode:

- **Reorder siblings** — drag a section between other sections at the same level (drop indicator between items)
- **Reparent** — drag a section onto another to make it a child (highlight on hover over potential parent)
- **Batch save** — all reorder/reparent operations are batched into a single Marten transaction on "Save" (not auto-save on each drop). This prevents silent commits and concurrency issues.
- **Via**: `Neo Sortable` → `AeroDocsGrain.SaveAsync` / `MoveSectionAsync` → per-operation `DocsContentService`

### 5.6 Space Editor Component Model

```razor
@* SpaceEditor.razor — Manager page for editing a docs space *@

<SpaceEditorLayout>
    <ExplorerPanel>
        <NeoTreeView Data="@treeData"
                     SelectedNodeId="@selectedNodeId"
                     OnNodeSelected="OnNodeSelectedAsync"
                     MultiSelect="true"
                     OnSelectionChanged="OnSelectionChanged">
            <NeoContextMenu Items="@contextMenuItems"
                           OnAction="OnContextMenuActionAsync" />
            <NeoSortable Mode="SortableMode.FullItemDrag"
                        Group="@sortableGroup"
                        OnDragEnd="OnDragEndAsync" />
        </NeoTreeView>
    </ExplorerPanel>
    
    <PagePreview>
        <EditorToolbar Page="@currentPage"
                       OnSave="SaveAsync"
                       OnPublish="PublishAsync"
                       OnPreview="PreviewAsync" />

        <BlockCanvas Blocks="@editorState.Blocks"
                      SelectedBlockId="@selectedBlockId"
                      OnBlockSelected="OnBlockSelected"
                      OnBlockMoved="OnBlockMovedAsync" />
    </PagePreview>
    
    <BlockMenuTray AvailableBlocks="@availableBlockTypes"
                   OnBlockDragStart="OnBlockDragStart" />
</SpaceEditorLayout>
```

**Code-behind (`SpaceEditor.razor.cs`):**
```csharp
public partial class SpaceEditor : ComponentBase
{
    [Parameter] public long SpaceId { get; set; }
    
    [Inject] private IAeroDocsActor DocsActor { get; set; } = null!;
    [Inject] private IDocsTreeService DocsTreeService { get; set; } = null!;
    [Inject] private IBlockService BlockService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    
    private DocsPage? currentPage;
    private DocsEditorState? editorState;
    private List<TreeNode> treeData = [];
    private long? selectedNodeId;
    private List<ContextMenuItem> contextMenuItems = BuildContextMenu();
    private string sortableGroup = "space-tree";
    private IReadOnlyList<BlockTypeDescriptor> availableBlockTypes = [];
    
    protected override async Task OnInitializedAsync()
    {
        // Load space root and its children for the tree
        var spaceResult = await DocsActor.GetByIdAsync(SpaceId);
        currentPage = spaceResult.Match(ok => MapFromViewModel(ok), _ => null);

        var children = await DocsActor.GetChildrenAsync(SpaceId, currentPage!.SiteId);
        treeData = BuildTree(currentPage, children);

        availableBlockTypes = BlockRegistry.GetAvailableTypes("docs");
    }
    
    private async Task OnNodeSelectedAsync(long nodeId)
    {
        Navigation.NavigateTo($"/manager/docs/{SpaceId}/sections/{nodeId}");
    }
    
    private async Task OnDragEndAsync(DragEndEventArgs args)
    {
        // args.FromIndex, args.ToIndex, args.NewParentId
        await DocsActor.SaveAsync(/* updated order */);
    }
}
```

### 5.7 Tree Data Operations

```csharp
public interface IDocsTreeService
{
    /// <summary>
    /// Moves a section to a new parent and position. Updates Order values
    /// for all affected siblings in a single Marten transaction.
    /// </summary>
    Task<Result<bool, AeroError>> MoveSectionAsync(
        long sectionId, long? newParentId, int newOrder, CancellationToken ct = default);

    /// <summary>
    /// Creates a new child section under the specified parent.
    /// </summary>
    Task<Result<DocsPage, AeroError>> CreateChildSectionAsync(
        long parentId, string title, CancellationToken ct = default);
}
```

### 5.8 Block Canvas

The block canvas (right side of the space editor) renders the current section's layout regions as a WYSIWYG preview using the same SSR NeoUI components used on the public `Doc.cshtml` page. Blocks are displayed in their final rendered form.

- **Add blocks:** Drag from the block menu tray into a region
- **Reorder blocks:** Drag within a region or across regions
- **Edit blocks:** Click a block to open its property editor sidebar
- **Delete blocks:** Select a block and press Delete, or use the block's context menu

### 5.9 Block Menu Tray

A draggable tray listing available block types for the space editor:

```
[📝 Markdown] [💻 Code] [💡 Callout] [🖼️ Image] [🎠 Carousel] [📊 Table] [📚 Child Pages]
```

Each item is a draggable element that creates a new `BlockBase` instance when dropped onto a region.

### 5.6 Render Mode

Per the user's directive: **all SSR, no dynamic blocks.** NeoUI blocks render in `Static` render mode — just like Pages module public pages. The editor canvas renders blocks with `render-mode="Static"` for a faithful WYSIWYG preview. Block property editing opens a side panel (not inline editing) to avoid dynamic rendering in the canvas.

---

### 5.10 Content Service Alignment

Docs should follow the working Pages service boundary instead of introducing a separate repository wrapper for the first slice.

```csharp
// D:\proj\microbians\AeroCMS\src\Aero.Cms.Modules.Docs\Grains\AeroDocsGrain.cs
private DocsContentService CreateDocsService(IDocumentSession session, long siteId)
{
    var blockService = _services.GetRequiredService<IBlockService>();
    var bus = _services.GetRequiredService<IMessageBus>();
    var logger = _services.GetRequiredService<ILogger<DocsContentService>>();
    var cache = _services.GetService<IFusionCache>();

    return new DocsContentService(
        session,
        blockService,
        bus,
        new FixedSiteContext(siteId),
        logger,
        actor: "system",
        cache);
}
```

**Decision:** `AeroDocsGrain` owns `IDocumentStore`, opens a `QuerySession()` for reads and `LightweightSession()` for mutations, and constructs `DocsContentService` per operation. This copies the `AeroPageGrain` / `MartenPageContentService` design. `IHttpContextAccessor` was removed from the service constructor (transport leak cleanup); actor is now an explicit `string? actor` parameter defaulting to `null` (resolved to `"system"` at the grain boundary).

---

## 6. Service Layer

### 6.1 Interface: `IDocsService` (Refined)

The existing `IDocsService` already defines CRUD + tree methods. Add search and breadcrumb methods:

```csharp
public interface IDocsService
{
    // ── CRUD ─────────────────────────────────────
    Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetAllAsync(CancellationToken ct = default);
    Task<Result<DocsPage?, AeroError>> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Result<DocsPage?, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<DocsPage, AeroError>> SaveAsync(DocsPage page, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);

    // ── Tree ─────────────────────────────────────
    Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetChildrenAsync(long parentId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetTopLevelCategoriesAsync(CancellationToken ct = default);

    // ── NEW: Search ──────────────────────────────
    Task<Result<IReadOnlyList<DocsPage>, AeroError>> SearchAsync(string query, int limit = 20, CancellationToken ct = default);

    // ── NEW: Navigation ──────────────────────────
    Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetBreadcrumbsAsync(long docId, CancellationToken ct = default);
    Task<Result<DocsTreeViewModel, AeroError>> GetTreeAsync(CancellationToken ct = default);
}
```

### 6.2 Implementation: `DocsService`

The existing `DocsService` will be refactored into a Pages-style content service. It should keep session-based Marten access, but the session is supplied per operation by the caller:

```csharp
// BEFORE (current):
public sealed class DocsService(
    IDocumentSession session,
    IMessageBus bus,
    ISiteContext siteContext,
    IHttpContextAccessor? httpContextAccessor = null,
    IFusionCache? cache = null) : IDocsService
{
    // ... uses session.Query<DocsPage>() directly but lacks the full Pages-style publish/draft shape
}

// AFTER (target):
public sealed class DocsContentService(
    IDocumentSession session,
    IBlockService blockService,
    IMessageBus bus,
    ISiteContext siteContext,
    ILogger<DocsContentService> logger,
    IHttpContextAccessor? httpContextAccessor = null,
    IFusionCache? cache = null,
    IDocsTreeService? treeService = null) : IDocsService
{
    // ... mirrors MartenPageContentService and requires SiteId on every operation
}
```

The FusionCache pattern, Wolverine event publishing, `ModifiedBy` stamping, validation, and block publishing workflow are preserved. The important change is not "repository vs direct session"; it is making `SiteId` explicit everywhere and copying the working Pages content-service boundary.

### 6.3 Breadcrumb Algorithm

```
GetBreadcrumbsAsync(docId):
  1. Load doc by ID
  2. Walk up via ParentId → load each ancestor
  3. Collect into ordered list [root → ... → current]
  4. Cache per-doc (FusionCache key: cms:docs:{siteId}:breadcrumb:{docId})
```

---

## 7. Tree & Navigation Service

### 7.1 `IDocsTreeService`

```csharp
// D:\proj\microbians\AeroCMS\src\Aero.Cms.Modules.Docs\Services\IDocsTreeService.cs
namespace Aero.Cms.Modules.Docs.Services;

public interface IDocsTreeService
{
    /// <summary>
    /// Builds the full published docs tree for sidebar rendering.
    /// </summary>
    Task<Result<DocsTreeViewModel, AeroError>> GetTreeAsync(long siteId, CancellationToken ct = default);

    /// <summary>
    /// Gets breadcrumbs (ancestor chain) for a doc page.
    /// </summary>
    Task<Result<IReadOnlyList<DocsPage>, AeroError>> GetBreadcrumbsAsync(long siteId, long docId, CancellationToken ct = default);

    /// <summary>
    /// Gets the "On this page" section links from a doc's rendered HTML headings.
    /// </summary>
    IReadOnlyList<HeadingAnchor> ExtractHeadings(string markdownContent);
}
```

### 7.2 View Models

```csharp
public sealed record DocsTreeViewModel
{
    public IReadOnlyList<TreeNode> Roots { get; init; } = [];
}

public sealed record TreeNode
{
    public long Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public long? ParentId { get; init; }
    public int Order { get; init; }
    public IReadOnlyList<TreeNode> Children { get; init; } = [];
    public bool IsExpanded { get; init; }
}

public sealed record HeadingAnchor
{
    public string Id { get; init; } = string.Empty;       // #heading-id
    public string Text { get; init; } = string.Empty;     // display text
    public int Level { get; init; }                       // h2=2, h3=3
}
```

### 7.3 Tree Construction Algorithm

```
GetTreeAsync(siteId):
  1. Load all published DocsPage records for site (cached via FusionCache)
  2. Group by ParentId
  3. Find the virtual root (`Slug == "docs"`, `ParentId == null`)
  4. Build tree recursively starting from the virtual root's children
  5. Return DocsTreeViewModel with nested TreeNode hierarchy
```

The sidebar CSHTML partial renders this tree with Alpine.js for expand/collapse behaviour, matching the skeleton's collapsible groups.

---

## 8. Search

### 8.1 Approach

The skeleton's search overlay (Ctrl+K) searches across docs titles, summaries, and content. 

**Phase 1 implementation:** Add Marten `NgramIndex(x => x.Title)` and `NgramIndex(x => x.MarkdownContent)` immediately, then implement the search method against those indexed fields. A simple `Contains()` fallback can remain only as a defensive compatibility path while the migration is being applied; it is not the planned steady-state search strategy.

### 8.2 Search Endpoint

```csharp
// Minimal API endpoint (registered in DocsModule or Aero.Cms.Modules.Headless)
app.MapGet("/docs/api/search", async (
    [FromQuery] string q,
    [FromQuery] int limit = 10,
    IDocsService docsService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        return Results.Ok(Array.Empty<object>());

    var result = await docsService.SearchAsync(q.Trim(), limit, ct);
    return result.Match(
        ok => Results.Ok(ok.Select(d => new { d.Id, d.Title, d.Slug, d.Summary })),
        failure => Results.Problem(failure.Message)
    );
});
```

### 8.3 Search UI (CSHTML)

The search overlay is a partial view included in the Docs layout. It uses Alpine.js to:
- Listen for Ctrl+K to open
- Debounce input (300ms) and fetch from `/docs/api/search`
- Render results as clickable links
- Close on ESC or backdrop click

```html
<!-- Areas/Docs/Partials/_SearchOverlay.cshtml -->
<div x-data="searchOverlay()" @@keydown.window.escape="close()" @@keydown.window.ctrl.k.prevent="open()">
    <div x-show="open" class="fixed inset-0 bg-black/30 backdrop-blur-sm z-[60]"
         @@click.self="close()">
        <div class="flex items-start justify-center pt-[15vh]">
            <div class="w-full max-w-2xl mx-4 bg-white rounded-xl shadow-2xl overflow-hidden">
                <!-- Search input -->
                <div class="flex items-center gap-3 px-5 py-4 border-b">
                    <i class="ph ph-magnifying-glass text-xl text-gray-400"></i>
                    <input x-ref="searchInput" type="text" placeholder="Search docs..."
                           class="flex-1 text-base outline-none"
                           x-model="query"
                           @@input.debounce.300ms="search()">
                </div>
                <!-- Results -->
                <div class="py-2">
                    <template x-for="result in results" :key="result.id">
                        <a :href="`/docs/${result.slug}`"
                           class="flex items-center gap-3 px-5 py-3 text-sm hover:bg-gray-50 transition-colors">
                            <i class="ph ph-file-text text-gray-400"></i>
                            <div>
                                <div class="font-medium text-gray-900" x-text="result.title"></div>
                                <div class="text-xs text-gray-500" x-text="result.summary?.substring(0, 80)"></div>
                            </div>
                        </a>
                    </template>
                </div>
            </div>
        </div>
    </div>
</div>
```

---

## 9. Validation

### 9.1 `DocsPageValidator`

```csharp
// D:\proj\microbians\AeroCMS\src\Aero.Cms.Modules.Docs\Validators\DocsPageValidator.cs
namespace Aero.Cms.Modules.Docs.Validators;

public sealed class DocsPageValidator : AbstractValidator<DocsPage>
{
    public DocsPageValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(256).WithMessage("Title must be 256 characters or fewer");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required")
            .MaximumLength(512).WithMessage("Slug must be 512 characters or fewer")
            .Matches(@"^[a-z0-9]+(?:[-/][a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase alphanumeric with hyphens or forward slashes");

        RuleFor(x => x.Summary)
            .MaximumLength(1024).WithMessage("Summary must be 1024 characters or fewer");

        RuleFor(x => x.SeoTitle)
            .MaximumLength(120).WithMessage("SEO title must be 120 characters or fewer");

        RuleFor(x => x.SeoDescription)
            .MaximumLength(320).WithMessage("SEO description must be 320 characters or fewer");

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0).WithMessage("Order must be non-negative");

        RuleFor(x => x.PublishedOn)
            .Must(date => date <= DateTimeOffset.UtcNow)
            .When(x => x.PublishedOn.HasValue)
            .WithMessage("Published date cannot be in the future");
    }
}
```

**Registration in `DocsModule.ConfigureServices()`:**
```csharp
services.AddScoped<IValidator<DocsPage>, DocsPageValidator>();
```

---

## 10. Caching Strategy

### 10.1 Layers

| Layer | Technology | Scope | Tags |
|-------|-----------|-------|------|
| **Output Cache** | ASP.NET Core `[OutputCache]` | HTTP responses | `docs-index` |
| **Data Cache** | `ZiggyCreatures.FusionCache` | Service-layer objects | `docs-index` (shared tag for eviction) |
| **Response Cache** | `[ResponseCache]` attributes | Browser/CDN hints | N/A |

### 10.2 Cache Keys (FusionCache)

```
cms:docs:{siteId}:all              → GetAllAsync results
cms:docs:{siteId}:slug:{slug}      → GetBySlugAsync single doc
cms:docs:{siteId}:id:{id}          → GetByIdAsync single doc
cms:docs:{siteId}:children:{pid}   → GetChildrenAsync
cms:docs:{siteId}:top-level        → GetTopLevelCategoriesAsync
cms:docs:{siteId}:breadcrumb:{id}  → GetBreadcrumbsAsync
cms:docs:{siteId}:tree             → GetTreeAsync
cms:docs:{siteId}:search:{query}   → SearchAsync (short TTL)
```

### 10.3 Eviction via Wolverine

When `DocsService.SaveAsync()` or `DeleteAsync()` fires, it publishes `DocsPageContentUpdatedEvent`. A Wolverine handler (already exists in the project's caching infrastructure) listens for these events and calls `IFusionCache.RemoveByTagAsync("docs-index")` + `IOutputCacheStore.EvictByTagAsync("docs-index")`.

**Event flow:**
```
DocsService.SaveAsync()
  → session.Store(page)
  → bus.PublishAsync(new DocsPageContentUpdatedEvent(...))
  → bus.PublishAsync(new DocViewModelCreated(ToViewModel(page)))
      ↓
  [Wolverine handler]
      ↓
  fusionCache.RemoveByTagAsync("docs-index")
  outputCacheStore.EvictByTagAsync("docs-index")
```

### 10.4 Output Cache Policy

```csharp
// Already registered in OutputCacheModule.cs (lines 84-94):
options.AddPolicy("DocsPolicy", builder =>
    builder.AddPolicy<CmsOutputCachePolicy>()
           .Expire(TimeSpan.FromMinutes(10))    // docs change infrequently
           .Tag("docs-index"),
    excludeDefaultPolicy: true);

options.AddPolicy("DocsIndexPolicy", builder =>
    builder.AddPolicy<CmsOutputCachePolicy>()
           .Expire(TimeSpan.FromMinutes(10))
           .Tag("docs-index"),
    excludeDefaultPolicy: true);
```

**Fine-grained eviction:** Add real docs tags end to end in Phase 1. `DocModel` sets `HttpContext.Items["AeroCms.DocId"]` and `HttpContext.Items["AeroCms.DocSlug"]`. `CmsOutputCachePolicy` must read those keys and add `doc-id-{id}` and `doc-slug-{slug}` tags. `FusionCacheInvalidationService` must evict those docs-specific tags for `ContentType == "docs"` instead of reusing page tags. Keep the coarse `docs-index` tag as the fallback for operational safety.

---

## 11. Razor Pages & UI

### 11.1 Page Route Structure

| Route | Page | Purpose |
|-------|------|---------|
| `/docs` | `DocsIndex.cshtml` | Landing page — space cards grid |
| `/docs/{*path}` | `Doc.cshtml` | Individual doc page (planned: three-column layout with sidebar) |
| `/manager/docs/{spaceId:long}` | `SpaceEditor.razor` | Manager-hosted space editor (planned) |
| `/manager/docs/{spaceId:long}/sections/{sectionId:long}` | `SpaceEditor.razor` | Manager-hosted section editor (planned) |
| `/api/admin/docs` | `DocsApi` | Admin API endpoints |
| `/docs/api/search` | Minimal API | Search endpoint (planned) |

### 11.2 `DocsIndex.cshtml` (Landing Page)

Matches the skeleton's `index.html` + `cms.html`.

**Data flow:**
1. `DocsIndexModel.OnGetAsync()` → calls `IDocsService.GetPublishedAsync()`
2. Loads all published docs for the current `SiteId` (cached via FusionCache + `docs-index` tag)
3. Finds the virtual root doc (`Slug == "docs"`) to identify top-level children (spaces)
4. Groups children by `ParentId` to build the chapter→sections tree in-memory
5. Rendered as Tailwind cards in a responsive grid (lg:grid-cols-3)

**Key design:**
- Hero section with "Knowledge Base" heading
- Card grid (2-3 columns) for spaces
- Each card shows: title, summary, nested child links
- Empty state with "No documentation found" message
- Attributes: `[OutputCache(PolicyName = "DocsIndexPolicy")]`
- Service-layer FusionCache: cache key `cms:docs:{siteId}:published`, tag `docs-index`

### 11.3 `Doc.cshtml` (Content Page)

Matches the skeleton's `setup.html` / `developing.html`.

**Layout:**
```
┌──────────────────────────────────────────────────────┐
│  Header (shared Aero layout)                          │
├──────────┬──────────────────────────┬────────────────┤
│ Sidebar  │ Main Content             │ On This Page   │
│ (300px)  │ (max 900px)              │ (240px, xl+)   │
│          │                          │                │
│ Spaces ▸ │ # Title                  │ • Section 1    │
│  Chap1 ▸ │ Summary                  │ • Section 2    │
│   Sec1   │                          │ • Section 3    │
│   Sec2 ◉ │ ┌────────────────────┐   │                │
│  Chap2   │ │ Markdown Content   │   │ [emoji feedback]│
│          │ │ (Markdig rendered) │   │                │
│          │ └────────────────────┘   │                │
├──────────┴──────────────────────────┴────────────────┤
│ Footer (shared Aero layout)                          │
└──────────────────────────────────────────────────────┘
```

**Data flow:**
1. `DocModel.OnGetAsync()` → resolves slug from `{*path}`
2. Calls `IDocsService.GetBySlugAsync(path)` to load the `DocsPage`
3. Calls `IDocsTreeService.GetTreeAsync()` for the sidebar
4. Calls `IDocsTreeService.GetBreadcrumbsAsync()` for breadcrumbs
5. Calls `IDocsTreeService.ExtractHeadings(content)` for "On This Page"
6. Returns `Page()` with the model

**Code-behind (current — `Doc.cshtml.cs`):**
```csharp
[OutputCache(PolicyName = "DocsPolicy")]
public class DocModel(IDocsService docsService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Slug { get; set; }

    public DocsPage? MarkdownPage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        var pageSlug = string.IsNullOrWhiteSpace(Slug) ? "docs" : $"docs/{Slug.TrimStart('/')}";

        var result = await docsService.GetPublishedBySlugAsync(pageSlug, ct);
        if (result is Result<DocsPage?, AeroError>.Ok ok)
        {
            MarkdownPage = ok.Value;
            return MarkdownPage is not null ? Page() : NotFound();
        }
        return NotFound();
    }
}
```

**Code-behind (planned — after layout redesign):**
```csharp
[OutputCache(PolicyName = "DocsPolicy")]
public class DocModel(
    IDocsService docsService,
    IDocsTreeService? treeService = null) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Path { get; set; }

    public DocsPage? DocsPage { get; private set; }
    public DocsTreeViewModel? Tree { get; private set; }
    public IReadOnlyList<DocsPage> Breadcrumbs { get; private set; } = [];
    public IReadOnlyList<HeadingItem> OnThisPage { get; private set; } = [];
    public IReadOnlyList<DocsPage> ChildPages { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var slug = string.IsNullOrWhiteSpace(Path) ? "docs" : $"docs/{Path.TrimStart('/')}";

        var result = await docsService.GetPublishedBySlugAsync(slug, ct);
        if (result is not Result<DocsPage?, AeroError>.Ok ok || ok.Value is null)
            return NotFound();

        DocsPage = ok.Value;

        // Sidebar tree (planned)
        if (treeService is not null)
        {
            var treeResult = await treeService.GetTreeAsync(DocsPage.SiteId, ct);
            Tree = treeResult is Result<DocsTreeViewModel, AeroError>.Ok treeOk ? treeOk.Value : null;
        }

        // Child pages for feature cards (space overview mode)
        if (DocsPage.ParentId is null)
        {
            var childrenResult = await docsService.GetChildrenAsync(DocsPage.Id, ct);
            ChildPages = childrenResult is Result<IReadOnlyList<DocsPage>, AeroError>.Ok childrenOk ? childrenOk.Value.ToList() : [];
        }

        // "On This Page" headings via Markdig AST (planned)
        if (!string.IsNullOrWhiteSpace(DocsPage.MarkdownContent))
            OnThisPage = HeadingExtractor.Extract(DocsPage.MarkdownContent);

        // Fine-grained cache tags (planned)
        HttpContext.Items["AeroCms.DocId"] = DocsPage.Id;
        HttpContext.Items["AeroCms.DocSlug"] = DocsPage.Slug;

        return Page();
    }
}
```

### 11.4 Sidebar Partial (`_DocsSidebar.cshtml`)

Renders the tree from `DocsTreeViewModel` using Alpine.js for expand/collapse:

```html
<!-- Areas/Docs/Partials/_DocsSidebar.cshtml -->
<aside class="hidden lg:block w-[300px] min-w-[300px] bg-gray-50 border-r border-gray-200 
              fixed top-16 left-0 bottom-0 overflow-y-auto sidebar-scroll z-30">
    <nav class="px-2 pb-4" x-data="docsSidebar(@Json.Serialize(Model.Tree))">
        <template x-for="space in spaces" :key="space.id">
            <div class="mb-4">
                <p class="px-3 py-2 text-xs font-medium text-gray-500 uppercase tracking-wider"
                   x-text="space.title"></p>
                <template x-for="chapter in space.children" :key="chapter.id">
                    <div>
                        <button @@click="toggle(chapter.id)"
                                class="w-full flex items-center justify-between px-3 py-1.5 text-sm font-medium 
                                       text-gray-600 hover:bg-white hover:text-gray-900 rounded transition-colors">
                            <span x-text="chapter.title"></span>
                            <i class="ph ph-caret-right text-xs transition-transform"
                               :class="{ 'rotate-90': chapter.expanded }"></i>
                        </button>
                        <div x-show="chapter.expanded" class="pl-6 mt-1 space-y-1">
                            <template x-for="section in chapter.children" :key="section.id">
                                <a :href="'/docs/' + section.slug"
                                   class="flex items-center px-3 py-1.5 text-sm rounded transition-colors"
                                   :class="section.id === activeDocId 
                                       ? 'text-indigo-600 border-l-2 border-indigo-600 bg-white' 
                                       : 'text-gray-600 hover:bg-white hover:text-gray-900'">
                                    <span x-text="section.title"></span>
                                </a>
                            </template>
                        </div>
                    </div>
                </template>
            </div>
        </template>
    </nav>
</aside>
```

### 11.5 Admin Editor (Manager-hosted Space Editor)

The admin editing experience is hosted in the **Manager module** (same as Pages and Posts editors). It provides two views:
1. **Spaces Listing** — Radzen Grid (like Posts/Pages admin tables) at the Manager's Docs nav target
2. **Space Editor** — individual space editing at `/manager/docs/{spaceId}` with a **Neo Tree** explorer on the left and a **WYSIWYG page preview** on the right that mirrors the public `Doc.cshtml` layout.

See **Section 5 — Manager Admin** for the full specification including Neo TreeView, Neo Context Menu, Neo Sortable drag-and-drop, block canvas, and block menu tray.

**Key features:**
- Left explorer panel with **Neo Tree** — space outline (sections, sub-sections), multi-select, context menu CRUD
- **Neo Sortable** with Full item drag for reordering and reparenting within the tree
- **Neo Context Menu** on tree nodes: Edit, Add Child Section, Rename, Duplicate, Move To…, Delete, View Published, Toggle Published/Draft
- Center page preview rendering blocks as SSR NeoUI components (WYSIWYG, mirrors public Doc.cshtml)
- Bottom block menu tray with draggable block types
- Toolbar with Save, Preview, Publish actions
- **Batch save** — all reorder operations are committed in a single Marten transaction (not auto-save on each drop)

---

## 12. Routing

### 12.1 Route Registration

Registered in `DocsModule.ConfigureServices()`:

```csharp
services.Configure<RazorPagesOptions>(options =>
{
    options.Conventions.AddAreaPageRoute("Docs", "/Docs/DocsIndex", "/docs");
    options.Conventions.AddAreaPageRoute("Docs", "/Docs/Doc", "/docs/{*path}");
});
```

### 12.2 Slug Resolution

The `{*path}` catch-all route parameter captures everything after `/docs/`. The `DocModel` code-behind normalizes it back into the stored slug shape by prefixing `docs/`, then passes that site-scoped slug to `IDocsService.GetBySlugAsync()`.

**Examples:**
- `/docs` → DocsIndex
- `/docs/fundamentals/setup` → Doc page with stored slug `"docs/fundamentals/setup"`
- `/docs/api-reference/rest/endpoints` → Doc page with stored slug `"docs/api-reference/rest/endpoints"`
- Manager routes stay under `/manager/docs`; there is no `/docs/admin` admin surface.

### 12.3 URL Design

Following the skeleton pattern:
- **Spaces** are the first path segment: `/docs/fundamentals`, `/docs/api-reference`
- **Deep pages** nest with slashes: `/docs/fundamentals/setup/requirements`
- The slug stored in `DocsPage.Slug` includes the `docs/` prefix and full path (e.g., `"docs/fundamentals/setup/requirements"`)

---

## 13. Wolverine Events

### 13.1 Event Types (Already Defined)

```csharp
// D:\proj\microbians\AeroCMS\src\Aero.Cms.Abstractions\Events\AeroEvents.cs

// High-level content change event (used for cache eviction)
public sealed record DocsPageContentUpdatedEvent(
    long ContentId, long SiteId, string NewSlug, string? OldSlug = null)
    : ContentUpdatedEvent(ContentId, SiteId, NewSlug, OldSlug, "docs");

// View-model events (used for Orleans grain notifications, admin UI updates)
public sealed record DocViewModelCreated(DocViewModel doc, string? msg = null);
public sealed record DocViewModelUpdated(DocViewModel doc, string? msg = null);
public sealed record DocViewModelDeleted(DocViewModel doc, string? msg = null);
```

### 13.2 Publishing (in `DocsService`)

```csharp
// Save
var isNew = existing is null;
session.Store(page);
await session.SaveChangesAsync(ct);

if (isNew)
    await bus.PublishAsync(new DocViewModelCreated(ToViewModel(page)));
else
    await bus.PublishAsync(new DocViewModelUpdated(ToViewModel(page)));

await bus.PublishAsync(new DocsPageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, oldSlug));

// Delete
await bus.PublishAsync(new DocViewModelDeleted(ToViewModel(page)));
await bus.PublishAsync(new DocsPageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, page.Slug));
```

---

## 14. CSS / Tailwind

### 14.1 Existing `docs.css`

The existing `wwwroot/css/docs.css` (80 lines) provides prose styles for the markdown content area using Tailwind `@apply` directives. This file is processed by `@tailwindcss/browser` via the `type="text/tailwindcss"` attribute.

### 14.2 New Styles Needed

Additional styles matching the skeleton:
- **Sidebar scrollbar** — custom thin scrollbar
- **Active nav item** — indigo left border + white background
- **Chevron rotation** — expand/collapse animation
- **Card hover** — lift + shadow effect for space cards
- **Search overlay** — backdrop blur
- **"On this page" active** — scroll spy highlight
- **Chat panel** — slide-in from right (future)
- **Emoji feedback** — scale on hover/select

These can be added to `docs.css` or kept inline in `_DocsLayout.cshtml` via a `<style type="text/tailwindcss">` block.

### 14.3 CDN Dependencies

All loaded via CDN (matching the skeleton):
```
- Tailwind CSS v4:        <script src="https://cdn.tailwindcss.com"></script>
- Google Fonts:           Inter (sans), JetBrains Mono (mono)
- Phosphor Icons:         https://unpkg.com/@phosphor-icons/web@2.1.1
```

---

## 15. Migration & Seeding

### 15.1 Seed Data

A seeding method in the Setup module already references `Aero.Cms.Modules.Docs`. The seed should create a minimal starter structure:

```csharp
// Conceptual seed data
var docs = new DocsPage
{
    Id = Snowflake.NewId(),
    SiteId = defaultSiteId,
    Slug = "docs",
    Title = "Aero Documentation",
    Summary = "Everything you need to build with Aero CMS.",
    PublicationState = ContentPublicationState.Published,
    PublishedOn = DateTimeOffset.UtcNow,
    ParentId = null,
    Order = 0
};

var fundamentals = new DocsPage
{
    Id = Snowflake.NewId(),
    SiteId = defaultSiteId,
    Slug = "docs/fundamentals",
    Title = "Fundamentals",
    Summary = "Core concepts and getting started guides.",
    PublicationState = ContentPublicationState.Published,
    PublishedOn = DateTimeOffset.UtcNow,
    ParentId = docs.Id,
    Order = 0
};

var setup = new DocsPage
{
    Id = Snowflake.NewId(),
    SiteId = defaultSiteId,
    Slug = "docs/fundamentals/setup",
    Title = "Setup",
    Summary = "Information on the requirements to setup, install & upgrade Aero CMS.",
    MarkdownContent = "# Setup\n\n## Requirements\n\n...",
    PublicationState = ContentPublicationState.Published,
    PublishedOn = DateTimeOffset.UtcNow,
    ParentId = fundamentals.Id,
    Order = 0
};
```

### 15.2 Search Index Migration

When implementing full-text search, add a Marten migration to create the GIN trigram index:

```csharp
// Phase 1: In DocsModule.Configure():
opts.Schema.For<DocsPage>().NgramIndex(x => x.Title);
opts.Schema.For<DocsPage>().NgramIndex(x => x.MarkdownContent);
```

---

## 16. File Manifest

### 16.1 Complete file list for `Aero.Cms.Modules.Docs`

```
Aero.Cms.Modules.Docs/
├── Aero.Cms.Modules.Docs.csproj          ✅ existing
├── DocsModule.cs                         ✅ complete (Marten indexes + DI factory)
├── IDocsService.cs                       ✅ 14 methods (CRUD + published + paging + tree)
├── DocsService.cs                        ✅ renamed to DocsContentService (mirrors MartenPageContentService)
├── DocsOperationContext.cs               ✅ Record (SiteId, Actor)
├── Queries/
│   └── DocsQueries.cs                    ✅ 4 AeroCompiledQuery classes
├── Grains/
│   └── AeroDocsGrain.cs                  ✅ thin delegation pattern
├── Services/
│   ├── IDocsTreeService.cs               🆕 planned
│   └── DocsTreeService.cs                🆕 planned
├── Blocks/                               🟡 Reuse Pages block types (MarkdownBlock, CodeBlock, etc.)
│   └── Rendering/                        🟡 Reuse Pages IBlockRenderer implementations
├── Components/                           (Manager + Neo components — planned)
│   ├── SpacesListing.razor               🆕 planned
│   ├── SpaceEditor.razor                 🆕 planned
│   ├── Neo TreeView (explorer)           ← Neo submodule (./Neo)
│   ├── Neo Context Menu                  ← Neo submodule (./Neo)
│   └── Neo Sortable                      ← Neo submodule (./Neo)
├── Caching/
│   └── DocsCacheTags.cs                  ✅ existing
├── Validators/
│   └── DocsPageValidator.cs              ✅ inline validation in DocsContentService
├── Areas/
│   └── Docs/
│       ├── Pages/
│       │   ├── DocsIndex.cshtml          ✅ data-driven cards
│       │   ├── DocsIndex.cshtml.cs       ✅ injects IDocsService
│       │   ├── Doc.cshtml                ⚠️ uses IDocsService; needs three-column layout
│       │   └── Doc.cshtml.cs             ✅ injects IDocsService
│       ├── Partials/                     🆕 planned
│       │   ├── _DocsSidebar.cshtml       🆕 planned (Alpine.js tree)
│       │   ├── _DocsBreadcrumbs.cshtml   🆕 planned
│       │   ├── _OnThisPage.cshtml        🆕 planned (Markdig AST)
│       │   └── _SearchOverlay.cshtml     🆕 planned
│       ├── Models/                       🆕 planned
│       │   ├── HeadingItem.cs            🆕 planned (Markdig AST extraction)
│       │   └── DocsSidebarNode.cs        🆕 planned (tree node model)
│       ├── _DocsLayout.cshtml            🆕 planned (nested layout: sidebar | content | right-panel)
│       ├── _ViewImports.cshtml           🆕 planned
│       └── _ViewStart.cshtml             🆕 planned
├── wwwroot/
│   ├── css/
│   │   └── docs.css                      ✅ existing (extend for skeleton styles)
│   └── js/
│       └── docs.js                       🆕 planned (Alpine.js components)
└── DocsMartenConfiguration.cs            🗑️ REMOVED (redundant)
```

### 16.2 Files Outside the Module (In Shared Projects)

| File | Project | Status |
|------|---------|--------|
| `DocsPage.cs` | `Aero.Cms.Core.Entities` | ⚠️ add LayoutRegions, PublishedVersion, BlockSchemaVersion |
| `DocViewModel.cs` | `Aero.Cms.Abstractions.Models` | ✅ Complete |
| `AeroEvents.cs` (Docs events) | `Aero.Cms.Abstractions.Events` | ✅ Complete |
| `OutputCacheModule.cs` (policies) | `Aero.Cms.Modules.OutputCache` | ✅ Complete |
| `CmsOutputCachePolicy.cs` | `Aero.Cms.Modules.OutputCache` | ✅ Complete |

---

## 17. Implementation Order

### Phase 1 — Foundation (HIGH) — Mostly Complete

1. ✅ Add PageEditor-style published/draft fields: `DocsPage.LayoutRegions`, `DocsPage.PublishedVersion`, `DocsPage.BlockSchemaVersion`, and `DocsEditorState`
2. ✅ Add Marten configuration: optimistic concurrency, `DocsEditorState` indexes, Ngram search indexes
3. ✅ Refactor `AeroDocsGrain` to mirror `AeroPageGrain`: own `IDocumentStore`, open sessions per operation, construct `DocsContentService` with `FixedSiteContext(siteId)`
4. ✅ Refactor `DocsService` into `DocsContentService` / Pages-style service methods with mandatory `SiteId` scoping
5. ✅ Create `DocsPageValidator` (inline validation in `DocsContentService`)
6. ✅ Refactor `DocsIndexModel` and `DocModel` to use `IDocsService` (not `IQuerySession`)
7. ✅ Add compiled queries (`DocsQueries.cs`): `GetPublishedAsync`, `GetPublishedBySlugAsync`, `GetPagedAsync`
8. ✅ Remove `IHttpContextAccessor` from service constructors (transport leak cleanup; explicit `actor` parameter)
9. ✅ Remove `DocsMartenConfiguration.cs` (redundant)
10. ✅ Create `DocsOperationContext` + `PageOperationContext` records
11. ⬜ Add docs-specific fine-grained cache tags end to end: `doc-id-{id}` and `doc-slug-{slug}`
12. 🟡 Block types — **reuse Pages block types and renderers** (no new Doc-specific blocks needed)
13. ⬜ Register block types and renderers via source generators in `DocsModule` for docs pages

### Phase 2 — UI Redesign (HIGH — Next)

14. ✅ Create `_ViewStart.cshtml` (layout pointer)
15. ⬜ Create `_DocsLayout.cshtml` (nested layout: `_CmsLayout` → three-column flex)
16. ⬜ Add FusionCache to `GetPublishedBySlugAsync` (H2 fix from council)
17. ⬜ Create `HeadingItem` model + Markdig AST heading extractor
18. ⬜ Redesign `Doc.cshtml`: three-column layout, breadcrumb, feature cards or markdown, right panel
19. ⬜ Create `_DocsSidebar.cshtml` (Alpine.js collapsible tree)
20. ⬜ Create `_OnThisPage.cshtml` partial
21. ⬜ Create `_DocsBreadcrumbs.cshtml` partial
22. ⬜ Extend `docs.css` with skeleton-matching styles
23. ⬜ Add Alpine.js components (`docs.js`)

### Phase 3 — Admin Editor (HIGH)

24. ✅ Implement **Spaces Listing** page (Radzen Grid) — Manager's Docs nav target
25. ✅ Implement **Space Editor** layout (Manager-hosted, mirrors public Doc.cshtml + left explorer)
26. 🟡 Implement **Neo Tree** explorer with Neo TreeView component (hierarchy, multi-select) — native outline/search/multi-select in place; Neo Tree replacement pending
27. 🟡 Implement **Neo Context Menu** on tree nodes (CRUD actions) — initial context actions in place
28. 🟡 Implement **Neo Sortable** drag-and-drop (Full item drag for reorder and reparent) — native move/reparent controls in place; drag/drop pending
29. ✅ Create `IDocsTreeService` / `DocsTreeService` (MoveSection, CreateChildSection, sidebar tree, breadcrumbs, headings)
30. ✅ Wire Space Editor route `/manager/docs/{spaceId}` in Manager module

### Phase 4 — Search (MEDIUM)

31. ⬜ Add `SearchAsync` to `IDocsService` + `DocsContentService`
32. ⬜ Create `/docs/api/search` minimal API endpoint
33. ⬜ Create `_SearchOverlay.cshtml` partial with Alpine.js
34. ⬜ Wire search overlay into the docs layout

### Phase 5 — Polish (MEDIUM)

35. ⬜ Add seed data for starter docs
36. ⬜ Add emoji feedback buttons
37. ⬜ Add "On this page" scroll spy highlighting

### Phase 6 — Future (LOW)

38. ⬜ Version selector dropdown
39. ⬜ Dedicated search page with pagination
40. ⬜ Doc analytics (view counts)
41. ⬜ AI chat assistant integration
42. ⬜ Theme toggle / dark mode (when theme system is implemented)

---

## 18. Testing Strategy

| Test Type | Tool | Scope |
|-----------|------|-------|
| **Unit** | TUnit + NSubstitute | `DocsContentService`, `AeroDocsGrain`, `DocsTreeService`, `DocsPageValidator` |
| **Integration** | Alba + Embedded Postgres | Razor Page rendering, search API, cache behaviour |
| **End-to-End** | Playwright | Sidebar navigation, search overlay, admin CRUD flow |

---

## 19. Open Questions for Council Review

1. **Arbitrary-depth nesting vs. fixed depth**: The current `DocsPage.ParentId` supports arbitrary depth. The skeleton suggests a 3-level structure (spaces → chapters → sections). Should we enforce a depth limit, or keep it flexible?

2. **Slug strategy**: Should slugs be flat (`"fundamentals/setup/requirements"`) or hierarchical (concatenated from parent slugs)? The current `DocsPage.Slug` stores the full path. Should the slug auto-populate from the tree path when a page is moved via drag-and-drop?

3. **Search fallback**: ✅ RESOLVED: Ngram indexes are Phase 1. `Contains()` may exist only as a temporary defensive fallback during migration.

4. **Admin auth**: Should the Manager-hosted Space Editor routes (`/manager/docs/*`) require `[Authorize(Policy = "CMSAdmin")]`? ✅ RESOLVED: Yes — the Manager module already enforces authorization for all admin routes (same as Pages and Posts editors).

5. **Share layout with Pages?** The `_DocsLayout.cshtml` could potentially share structural elements with the Pages layout (header/footer). SG the `ViewBag` approach used by Pages (`ViewBag.ShowHeaderNavigation`, `ViewBag.HideFooter`). Should Docs maintain its own layout file or inherit from the CMS layout?

6. **~Markdown editor~** ✅ RESOLVED: The Docs module now uses block composition (like Pages) with NeoUI SSR renderers. The `DocsMarkdownBlock` handles Markdown content as one of several block types. Radzen WYSIWYG is not needed — block editing uses NeoUI property editors in the block sidebar panel.

7. **Editor state separation**: ✅ RESOLVED: Follow the PageEditor pattern. `DocsEditorState` stores draft editor placements and `BlockIdMap`; `DocsPage.LayoutRegions` stores the published render manifest built on publish.

8. **Block type scope**: Are the 6 proposed block types (Markdown, Code, Callout, Image, Table, ChildPages) sufficient for the MVP, or should additional types (e.g., Video, Tabs, Accordion, API Reference) be added?

9. **~Drag-and-drop persistence~** ✅ RESOLVED: **Batch save**. Neo Sortable captures all reorder/reparent operations. On "Save", all changes are committed in a single Marten transaction. This prevents silent commits and concurrency issues (same pattern as Pages module).

10. **Public sidebar tree depth**: Should the public-facing sidebar show the full tree (all levels) or only expand to a configurable depth (e.g., 3 levels)?

### Neo Component References

| Component | Docs URL |
|---|---|
| **Neo TreeView** | [https://demos.neoui.io/components/tree-view](https://demos.neoui.io/components/tree-view) |
| **Neo Context Menu** | Neo submodule (`./Neo`) — right-click context menu on tree nodes |
| **Neo Sortable** | [https://demos.neoui.io/primitives/sortable](https://demos.neoui.io/primitives/sortable) |

---

## 20. Council Review — Architecture (v3 Findings & Resolutions)

> **Reviewer:** gamma (minimax-m2.7) — 1/3 councillors responded (alpha/beta timed out)  
> **Verdict:** Plan was **solid in direction**. All critical findings (C1-C7) have been implemented in Phase 1. ✅
> **See §21 for the current UI redesign council review.**

### 20.1 Critical Issues (Must Fix Before Phase 1)

| # | Issue | Resolution |
|---|-------|------------|
| **C1** | `DocsEditorState` entity not defined in the codebase, but referenced throughout Phase 2 | **Add `DocsEditorState` entity** in Phase 1. Follow the actual `PageEditorState` shape: same id as the content document, `Blocks`, `BlockIdMap`, `DraftVersion`, and `LastModified`. Do not store draft `LayoutRegions` directly. |
| **C2** | `DocsService.SaveAsync` currently owns direct persistence but does not match the working Pages service boundary | **Refactor to Pages-style `AeroDocsGrain` + `DocsContentService`.** The grain opens `IDocumentStore` sessions per operation and constructs the service with `FixedSiteContext(siteId)`. |
| **C3** | Missing `BlockSchemaVersion` on `DocsPage` for migration idempotency | **Add `BlockSchemaVersion` (int)** to `DocsPage` mirroring `PageDocument` (line 89). |
| **C4** | `GetTreeAsync` loads ALL published docs into memory | **Add `GetSubtreeAsync(long parentId, int maxDepth)`** for context-aware loading. Use full tree only for small sites with FusionCache. |
| **C5** | Search uses `Contains()` (LIKE) — slow at scale | **Add NgramIndex in Phase 1**, not Phase 6. One-line Marten config: `opts.Schema.For<DocsPage>().NgramIndex(x => x.Title)`. |
| **C6** | Docs public/admin paths are not consistently site-scoped | **Every grain/service method must accept or derive `SiteId`, and every Marten query must filter by `SiteId`.** |
| **C7** | Fine-grained cache tags are documented but not implemented end to end | **Add real docs tags**: `doc-id-{id}` and `doc-slug-{slug}` in the page model, output-cache policy, and invalidation service. |

### 20.2 Open Questions — Resolved

| Q | Topic | Resolution | Reasoning |
|---|-------|------------|-----------|
| **Q1** | Depth limit | **Arbitrary depth, max 10** | Enforce `MaxDepth=10` in `MovePageAsync`. Flexible UX without pathological trees. |
| **Q2** | Slug strategy | **Stored full docs path + independent tree position** | Current seed/code stores `docs/...` slugs. Tree position comes from `ParentId`; moving a node does not silently rewrite slugs unless the user explicitly chooses that behavior. |
| **Q3** | Search index | **NgramIndex immediately** | Phase 1 (addressed in C5 above). |
| **Q4** | Admin auth | **Yes — manager routes stay under `/manager/docs/*` and require manager authorization** | Follow the current Pages/Posts manager route paradigm. |
| **Q5** | Layout sharing | **Separate `_DocsLayout` with composition** | Docs has distinct UX (sidebar + content + on-this-page). Compose from base layout, don't share with Pages. |
| **Q6** | Markdown editor | **RESOLVED** — block composition | v2 document. |
| **Q7** | Editor state separation | **PageEditor-style two-document model** (`DocsEditorState` + `DocsPage`) | `DocsEditorState` stores draft editor blocks; `DocsPage.LayoutRegions` stores the published render manifest. |
| **Q8** | Block type count | **6 types sufficient, but clarify `DocsChildPagesBlock`** | Consider making it a sidebar component, not a stored block — it's a computed query, not stored content. |
| **Q9** | Drag-drop save | **Batched save** | "Save Tree Changes" button batches all reorders into a single Marten transaction. Avoids silent commits and concurrency issues. |
| **Q10** | Sidebar depth | **Configurable, default 3** | `SidebarMaxDepth` setting. Truncate tree at `currentDepth + maxDepth` in `GetTreeAsync`. |

### 20.3 Missing Considerations (Added to Plan)

| # | Missing | Impact | Mitigation Added |
|---|---------|--------|------------------|
| **M1** | No version/comparison for blocks | Can't detect "unpublished changes" | Add `PublishedVersion` to `DocsPage` and compare it with `DocsEditorState.DraftVersion` |
| **M2** | "On This Page" assumes `MarkdownContent` | Breaks with block-only pages | Extract headings from rendered HTML of `LayoutRegions`, not raw MarkdownContent |
| **M3** | Slug uniqueness conflict on move | Possible data corruption | Check uniqueness in `MovePageAsync`; reject or auto-suffix if collision |
| **M4** | No migration path for legacy MarkdownContent → blocks | Existing docs stay in legacy state | When legacy doc is opened in block editor, auto-migrate `MarkdownContent` into a `DocsMarkdownBlock` |
| **M5** | Concurrent tree edits (lost updates) | Two admins reorder same tree | Add **Marten optimistic concurrency** on `DocsPage` (already partially configured: `UseOptimisticConcurrency` exists in Pages but not Docs) |
| **M6** | `DocsChildPagesBlock` computed at render time | Potential N+1 for deep pages | Pre-fetch child page IDs in `BlockRenderCache.PreloadAsync`; or move to sidebar component |
| **M7** | "Spaces" could be mistaken for separate aggregate roots | Implementation drift from current seed/routes | Treat a space as a direct child of the virtual root page `Slug == "docs"`; it is a term for the docs container UX, not a separate model. |

### 20.4 Risk Matrix

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `DocsEditorState` undefined → Phase 2 delay | **High** | Medium | Define entity in Phase 1 |
| Circular reference on tree reparent | **Medium** | High | Ancestor-chain validation in `MovePageAsync` |
| LIKE search degrades at 500+ docs | **High** | Medium | NgramIndex in Phase 1 |
| Missing `SiteId` in any docs query | **High** | High | Add `SiteId` as a required service/grain invariant and test public/admin paths |
| Block editor can't handle legacy MarkdownContent | **High** | Medium | Auto-migration on editor open |
| Concurrent tree reorders → lost updates | **Medium** | Medium | Marten optimistic concurrency |
| No authorization on admin routes | **Low** | High | `[Authorize]` in Phase 2 |
| Slug uniqueness violation on page move | **Low** | Medium | Check and reject/auto-suffix |

### 20.5 Implementation Priority Adjustments

Based on council findings, reorder Phase 1 to include the critical issues:

**Adjusted Phase 1 — Foundation (HIGH):**
1. **Define `DocsEditorState` entity** (C1) — BEFORE building the editor.
2. Add `LayoutRegions`, `PublishedVersion`, and `BlockSchemaVersion` to `DocsPage` (C3).
3. Add `NgramIndex` to Marten config (C5).
4. Refactor `AeroDocsGrain` to match `AeroPageGrain`: `IDocumentStore` + per-operation session + manually constructed `DocsContentService` (C2).
5. Refactor `DocsService` into a Pages-style `DocsContentService` with mandatory `SiteId` scoping on every operation (C6).
6. Add docs-specific fine-grained cache tags end to end (C7).
7. Create `DocsPageValidator` + register in `DocsModule`.
8. Refactor `DocsIndexModel` and `DocModel` to use `IDocsService`, virtual-root spaces, block rendering, and docs-specific cache items.
9. Remove `DocsMartenConfiguration.cs` (redundant).
10. Define block types (`DocsMarkdownBlock`, etc.) in `Blocks/`.
11. Implement `IBlockRenderer` for each block type.
12. Add `GetSubtreeAsync()` to `IDocsTreeService` (C4).
13. Implement `IDocsTreeService` methods with proper tree query scoping.

---

## 21. Council Review — UI Redesign (2026-05-24)

> **Reviewer:** gamma (minimax-m2.7) — 1/3 councillors responded  
> **Scope:** `Doc.cshtml` redesign, `_DocsLayout`, sidebar, "On This Page", heading extraction  
> **Verdict:** Architecture is sound. Three blockers identified.

### Critical Issues

| # | Issue | Resolution |
|---|-------|------------|
| **H1** | Fixed header mismatch — `AeroNavBar` in `_CmsLayout` is normal document flow, skeleton uses `position: fixed` | Use sticky sidebar within `<main>` flex container. Three-column layout sits below `AeroNavBar` in normal document flow. No `position: fixed` needed. |
| **H2** | `GetPublishedBySlugAsync` has no FusionCache | Add per-slug cache with `DocsCacheTag` (mirror `GetBySlugAsync` pattern). Key: `cms:docs:{siteId}:slug:{slug}`. |
| **H3** | Missing `_ViewStart.cshtml` in `Areas/Docs/Pages/` | Must create to activate `_DocsLayout.cshtml`. |

### Architecture Decisions

| Decision | Rationale |
|----------|-----------|
| **Nested layout** `_DocsLayout` → `_CmsLayout` | Sections pass through correctly via `@await RenderSectionAsync` chaining. `_DocsLayout` wraps `RenderBody()` in three-column flex container. |
| **Sidebar tree** via `IDocsService.GetPublishedAsync()` | Already cached via FusionCache + `docs-index` tag. Auto-invalidated on any docs save/delete via Wolverine. |
| **Heading extraction** via Markdig AST | `Markdown.Parse()` + `Descendants<HeadingBlock>()` with `.UseAutoIdentifiers()` pipeline. Clean, reliable. Overhead negligible with OutputCache. |
| **Hybrid rendering mode** for space overview pages | Show intro MarkdownContent above feature cards for pages with children. Not binary (cards-only vs content-only). |
| **Alpine.js for sidebar collapsible** | `x-data` for expanded/collapsed state, CSS `max-height` + transition. No custom JS needed. |
| **Mobile sidebar** | `lg:hidden` by default, Alpine.js `x-show` overlay toggle. |
| **Right panel** | Column always present (layout consistency). "On This Page" TOC only on leaf pages. "Was this helpful?" on all. |
| **Phosphor Icons CDN** | Added in `_DocsLayout.headPartial` section (not `cssPartial` — avoids render-block). |
| **Theme toggle** | ⬜ Deferred to future theme system. |

### Risks

| Risk | Mitigation |
|------|------------|
| `GetPublishedBySlugAsync` uncached → N+1 DB hits per page view | Add FusionCache in Phase 1 (H2 above) |
| Three-column layout breaks on mobile without responsive design | Sidebar hidden below `lg:`, right panel hidden below `xl:` |
| Markdig heading extraction fails on empty/null MarkdownContent | Guard with null check; return empty list |

### Implementation Sequence

1. **Create `_ViewStart.cshtml`** — point to `_DocsLayout`
2. **Create `_DocsLayout.cshtml`** — nested layout with three-column flex
3. **Add FusionCache to `GetPublishedBySlugAsync`** (H2 fix)
4. **Create heading extraction** — `HeadingItem` model + Markdig AST helper
5. **Redesign `Doc.cshtml`** — three-column, breadcrumb, feature cards or markdown, right panel
6. **Add sidebar partial** — `_DocsSidebar.cshtml` with Alpine.js tree
7. **Add `_OnThisPage.cshtml` partial** — "On This Page" TOC
8. **Extend `docs.css`** — skeleton-matching styles (sidebar scrollbar, card hover, active nav)

---

<!--
  ═══════════════════════════════════════════════════════════
  EDITOR NOTES:
  - This document is a living spec. Update as implementation progresses.
  - All "Umbraco" strings in docs-skeleton/ are replaced with "Aero".
  - Follow AGENTS.md constraints: no reflection, source generators, FluentValidation, ROP.
  ═══════════════════════════════════════════════════════════
-->
