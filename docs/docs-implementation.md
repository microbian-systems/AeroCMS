# Aero CMS — Docs Module Implementation Plan

> **Status:** Draft v2 — council-reviewed (gamma: minimax-m2.7)  
> **Module:** `Aero.Cms.Modules.Docs`  
> **Pattern:** Vertical slice, GitBook-style knowledge base with block composition  
> **Last Updated:** 2026-05-19

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
| `Aero.Cms.Core.Entities/DocsPage.cs` | ✅ Complete | 41 lines; `Entity : ISiteOwned` with all fields |
| `Aero.Cms.Abstractions/Models/DocViewModel.cs` | ✅ Complete | Orleans-serializable view model |
| `Aero.Cms.Abstractions/Events/AeroEvents.cs` | ✅ Complete | `DocsPageContentUpdatedEvent`, DocViewModel CUD events |
| `Aero.Cms.Modules.Docs/DocsModule.cs` | ✅ Complete | Module registration, Marten indexes, DI |
| `Aero.Cms.Modules.Docs/IDocsService.cs` | ✅ Complete | Interface with CRUD + tree methods |
| `Aero.Cms.Modules.Docs/DocsService.cs` | ⚠️ Partial | 263 lines; uses `IDocumentSession` directly (not repository) |
| `Aero.Cms.Modules.Docs/Caching/DocsCacheTags.cs` | ✅ Complete | Cache tag constants |
| `Aero.Cms.Modules.Docs/DocsMartenConfiguration.cs` | ⚠️ Redundant | Duplicates `DocsModule.Configure()` |
| `Aero.Cms.Modules.Docs/Areas/Docs/Pages/DocsIndex.cshtml` | ⚠️ Partial | Works but bypasses `IDocsService` |
| `Aero.Cms.Modules.Docs/Areas/Docs/Pages/DocsIndex.cshtml.cs` | ⚠️ Partial | Uses `IQuerySession` directly |
| `Aero.Cms.Modules.Docs/Areas/Docs/Pages/Doc.cshtml` | ⚠️ Partial | Works but has hardcoded slug prefix |
| `Aero.Cms.Modules.Docs/Areas/Docs/Pages/Doc.cshtml.cs` | ⚠️ Partial | Uses `IQuerySession` directly |
| `Aero.Cms.Modules.OutputCache/OutputCacheModule.cs` | ✅ Complete | `DocsPolicy` + `DocsIndexPolicy` registered |
| `Aero.Cms.Modules.Docs/wwwroot/css/docs.css` | ✅ Complete | Tailwind prose styles |

### 2.2 What is Missing

| Feature | Priority | Effort |
|---------|----------|--------|
| **DocsEditor.razor** (admin block editor with tree) | HIGH | Large |
| **BlockBase adapters** for Docs block types | HIGH | Medium |
| **Block render cache** (N+1 prevention, same as Pages) | HIGH | Small |
| **Drag-and-drop tree management** (reorder, reparent) | HIGH | Large |
| **Right-click context menu** for tree CRUD | HIGH | Medium |
| **DocsRepository** wrapping `GenericMartenRepository<DocsPage>` | HIGH | Small |
| **IDocsTreeService** for sidebar hierarchy + breadcrumbs | HIGH | Medium |
| **Search service** (Ctrl+K overlay) | MEDIUM | Medium |
| **FluentValidation validator** (`DocsPageValidator`) | HIGH | Small |
| **Refactor PageModels** to use `IDocsService` not `IQuerySession` | HIGH | Small |
| **Sidebar CSHTML partial** with tree nav | HIGH | Medium |
| **Breadcrumb CSHTML partial** | MEDIUM | Small |
| **"On this page" scroll spy** (headings in content) | MEDIUM | Medium |
| **Seed data** (starter docs structure) | MEDIUM | Small |
| **Search indexing** (Marten GIN/Ngram) | MEDIUM | Medium |
| **Version selector** (skeleton pattern) | LOW | Small |
| **Cache eviction handler** for OutputCache tags | LOW | Already in place via Wolverine FX |
| **Remove `DocsMartenConfiguration.cs`** (redundant) | LOW | Trivial |

---

## 3. Architecture Overview

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
  • Aero.Marten (GenericMartenRepository<T>)
  • Aero.Cms.Abstractions.Blocks (BlockBase, IBlockRenderer)
  • Wolverine (IMessageBus for event publishing)
  • ZiggyCreatures.FusionCache (data cache)
  • NeoUI (Blazor component library for block renders)
```

### Dependency Graph

```
Aero.Cms.Modules.Docs
  ├── Aero.Core                       (IEntity, ROP types)
  ├── Aero.Marten                     (GenericMartenRepository)
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
    public bool IsPubliclyVisible => PublicationState == ContentPublicationState.Published;

    // Presentation
    public bool ShowHeaderNavigation { get; set; } = true;
    public string? HeaderImageUrl { get; set; }

    // Tree
    public long? ParentId { get; set; }
    public int Order { get; set; }

    // Block schema versioning (added per council review — C3)
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
opts.Schema.For<DocsEditorState>().Index(x => x.DocId);
opts.Schema.For<DocsEditorState>().Index(x => x.SiteId);
```

**Tree design:** Materialized path pattern (parent pointer). Each `DocsPage` stores its `ParentId`. The tree is assembled in-memory by the `DocsTreeService`, not via recursive CTEs or nested sets.

### 4.3 Spaces Concept

"Spaces" are top-level docs pages — `DocsPage` records where `ParentId` is `null` (or where the parent is a virtual "root"). The docs home page (`DocsIndex.cshtml`) displays these as feature cards matching the GitBook skeleton's card grid.

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

Each `DocsPage` stores a `LayoutManifest` (mirroring `PageDocument.LayoutRegions`). The manifest is a serialized collection of `LayoutRegion` objects stored as a JSONB column in Marten:

```csharp
// Conceptual: what gets added to DocsPage (or stored separately)
public sealed class DocsPage : Entity, ISiteOwned
{
    // ... existing fields ...

    /// <summary>
    /// Published layout manifest: regions → columns → block placements.
    /// Rendered SSR by LayoutRegionRenderer components during public page rendering.
    /// </summary>
    public IReadOnlyList<LayoutRegion> LayoutRegions { get; set; } = [];
}
```

> **Note:** Per council review (Q7 resolution): Docs will use the **two-document model** — `DocsEditorState` for scratch/wip blocks and `DocsPage.LayoutRegions` for committed blocks. This is consistent with Pages' `PageEditorState` + `PageDocument` pattern. The `DocsEditorState` entity must be defined in Phase 1 before building the editor.

**`DocsEditorState` entity (conceptual):**
```csharp
// D:\proj\microbians\AeroCMS\src\Aero.Cms.Core.Entities\DocsEditorState.cs
public sealed class DocsEditorState : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long DocId { get; set; }                          // FK to DocsPage
    public IReadOnlyList<LayoutRegion> DraftRegions { get; set; } = [];  // WIP blocks
    public int DraftSchemaVersion { get; set; }              // Block schema version used in editor
    public DateTimeOffset? DraftedAt { get; set; }           // Last edit timestamp
}
```

### 4b.2 Block Types

Docs-specific block types extend `BlockBase` from `Aero.Cms.Abstractions.Blocks`:

```csharp
// Blocks available in the Docs editor block menu:

// Markdown content block — renders via Markdig
public sealed class DocsMarkdownBlock : BlockBase
{
    public string Content { get; set; } = string.Empty;  // Markdown source
}

// Code block with syntax highlighting
public sealed class DocsCodeBlock : BlockBase
{
    public string Code { get; set; } = string.Empty;
    public string Language { get; set; } = "csharp";
}

// Callout / info box
public sealed class DocsCalloutBlock : BlockBase
{
    public CalloutType Type { get; set; } = CalloutType.Info;  // Info, Warning, Tip, Danger
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;       // Markdown
}

// Image with caption
public sealed class DocsImageBlock : BlockBase
{
    public string ImageUrl { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public string? Caption { get; set; }
}

// Table block
public sealed class DocsTableBlock : BlockBase
{
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; set; } = [];
    public bool HasHeader { get; set; } = true;
}

// Child pages list (auto-generated from tree)
public sealed class DocsChildPagesBlock : BlockBase
{
    public int MaxItems { get; set; } = 10;
    public bool ShowSummaries { get; set; } = true;
}
```

### 4b.3 Block Renderers

Each block type has a corresponding `IBlockRenderer` implementation that outputs NeoUI SSR components:

```
DocsMarkdownBlockRenderer   → <DocsMarkdownView Content="@block.Content" />
DocsCodeBlockRenderer       → <DocsCodeView Code="@block.Code" Language="@block.Language" />
DocsCalloutBlockRenderer    → <DocsCalloutView Type="@block.Type" Title="@block.Title" Content="@block.Content" />
DocsImageBlockRenderer      → <DocsImageView Url="@block.ImageUrl" Alt="@block.AltText" Caption="@block.Caption" />
DocsTableBlockRenderer      → <DocsTableView Rows="@block.Rows" HasHeader="@block.HasHeader" />
DocsChildPagesBlockRenderer → <DocsChildPagesView DocId="@block.OwnerId" MaxItems="@block.MaxItems" />
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

## 5. DocsEditor.razor — Admin Block Editor

The admin editing experience is a Blazor component (`DocsEditor.razor`) that parallels the Pages block editor. It provides a two-panel layout: a tree sidebar on the left for document hierarchy management, and a blocks canvas on the right for block composition.

### 5.1 Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  DocsEditor.razor                                                 │
│  ┌──────────────────┐  ┌───────────────────────────────────────┐ │
│  │  Tree Panel      │  │  Top Toolbar                           │ │
│  │  (300px)         │  │  [Save] [Preview] [Publish] [Delete]   │ │
│  │                  │  ├───────────────────────────────────────┤ │
│  │  📁 Space 1      │  │                                       │ │
│  │   📄 Chapter A   │  │  ┌─────────────────────────────────┐  │ │
│  │   📄 Chapter B ◉ │  │  │  Block Canvas                   │  │ │
│  │     📄 Section 1  │  │  │                                 │  │ │
│  │     📄 Section 2  │  │  │  Region 1: [Block A] [Block B] │  │ │
│  │  📁 Space 2       │  │  │                                 │  │ │
│  │   📄 Chapter C   │  │  │  Region 2: [Block C]            │  │ │
│  │                  │  │  │                                 │  │ │
│  │  [+ New Page]    │  │  └─────────────────────────────────┘  │ │
│  │                  │  │                                       │ │
│  └──────────────────┘  │  ┌────────────────────────────────────┐│ │
│                        │  │ Block Menu Tray (draggable types)  ││ │
│                        │  │ [Markdown] [Code] [Callout] ...    ││ │
│                        │  └────────────────────────────────────┘│ │
│                        └───────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

### 5.2 Component Model

```razor
@* DocsEditor.razor — Admin page for editing a docs page *@
@page "/docs/admin/edit/{DocId:long}"

<DocsEditorLayout>
    <LeftPanel>
        <DocsTreePanel @ref="treePanel"
                       DocId="@DocId"
                       OnPageSelected="OnPageSelectedAsync"
                       OnPageMoved="OnPageMovedAsync"
                       OnPageCreated="OnPageCreatedAsync"
                       OnPageDeleted="OnPageDeletedAsync" />
    </LeftPanel>
    
    <CenterPanel>
        <EditorToolbar Page="@currentPage"
                       OnSave="SaveAsync"
                       OnPublish="PublishAsync"
                       OnPreview="PreviewAsync" />
        
        <BlockCanvas Regions="@editorState.Regions"
                     SelectedBlockId="@selectedBlockId"
                     OnBlockSelected="OnBlockSelected"
                     OnBlockMoved="OnBlockMovedAsync" />
    </CenterPanel>
    
    <BlockMenuTray AvailableBlocks="@availableBlockTypes"
                   OnBlockDragStart="OnBlockDragStart" />
</DocsEditorLayout>
```

**Code-behind (`DocsEditor.razor.cs`):**
```csharp
public partial class DocsEditor : ComponentBase
{
    [Parameter] public long DocId { get; set; }
    
    [Inject] private IDocsService DocsService { get; set; } = null!;
    [Inject] private IDocsTreeService TreeService { get; set; } = null!;
    [Inject] private IBlockService BlockService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    
    private DocsPage? currentPage;
    private DocsEditorState? editorState;
    private DocsTreeViewModel tree;
    private IReadOnlyList<BlockTypeDescriptor> availableBlockTypes = [];
    private long? selectedBlockId;
    
    protected override async Task OnInitializedAsync()
    {
        var result = await DocsService.GetByIdAsync(DocId);
        currentPage = result.Match(ok => ok, _ => null);
        
        editorState = await LoadEditorStateAsync(DocId);
        tree = (await TreeService.GetTreeAsync(currentPage!.SiteId)).Match(ok => ok, _ => null);
        availableBlockTypes = BlockRegistry.GetAvailableTypes("docs");
    }
    
    private async Task OnPageSelectedAsync(long pageId)
    {
        Navigation.NavigateTo($"/docs/admin/edit/{pageId}");
    }
    
    private async Task OnPageMovedAsync(long pageId, long? newParentId, int newOrder)
    {
        await TreeService.MovePageAsync(pageId, newParentId, newOrder);
    }
}
```

### 5.3 Tree Panel — Drag, Drop, and Context Menu

The tree panel uses NeoUI components with drag-and-drop and right-click context menus:

```csharp
// DocsTreePanel.razor features:

// 1. Drag-and-drop reordering
//    • NeoUI TreeView with DragDropMode="Reorder"
//    • Drop indicators between items for insertion position
//    • Drag over parent item to reparent (with highlight)
//    • Debounced save to Marten (batch update Order values)

// 2. Right-click context menu
//    • Edit (opens doc in editor)
//    • Add Child Page (creates new doc under this parent)
//    • Rename (inline edit Title)
//    • Duplicate (clone with " (copy)" suffix)
//    • Move To... (modal to select new parent)
//    • Delete (with confirmation, cascading delete of children)
//    • View Published (opens /docs/{slug} in new tab)
//    • Toggle Published/Draft state
```

**Tree data operations:**
```csharp
public interface IDocsTreeService
{
    // ... existing methods ...
    
    /// <summary>
    /// Moves a page to a new parent and position. Updates Order values
    /// for all affected siblings. Single Marten transaction.
    /// </summary>
    Task<Result<bool, AeroError>> MovePageAsync(
        long pageId, long? newParentId, int newOrder, CancellationToken ct = default);

    /// <summary>
    /// Creates a new child page under the specified parent.
    /// </summary>
    Task<Result<DocsPage, AeroError>> CreateChildPageAsync(
        long parentId, string title, CancellationToken ct = default);
}
```

### 5.4 Block Canvas

The block canvas renders the current page's layout regions as a WYSIWYG preview using the same SSR NeoUI components used on the public page. Blocks are displayed in their final rendered form, not as grey placeholder rectangles.

- **Add blocks:** Drag from the block menu tray into a region
- **Reorder blocks:** Drag within a region or across regions
- **Edit blocks:** Click a block to open its property editor sidebar
- **Delete blocks:** Select a block and press Delete, or use the block's context menu

### 5.5 Block Menu Tray

A horizontal tray at the bottom (or vertical sidebar on the right) listing draggable block types:

```
[📝 Markdown] [💻 Code] [💡 Callout] [🖼️ Image] [📊 Table] [📚 Child Pages]
```

Each item is a draggable element that creates a new `BlockBase` instance when dropped onto a region.

### 5.6 Render Mode

Per the user's directive: **all SSR, no dynamic blocks.** NeoUI blocks render in `Static` render mode — just like Pages module public pages. The editor canvas renders blocks with `render-mode="Static"` for a faithful WYSIWYG preview. Block property editing opens a side panel (not inline editing) to avoid dynamic rendering in the canvas.

---

### 5.1 Interface: `IDocsRepository`

```csharp
// D:\proj\microbians\AeroCMS\src\Aero.Cms.Modules.Docs\Repositories\IDocsRepository.cs
namespace Aero.Cms.Modules.Docs.Repositories;

public interface IDocsRepository : IGenericMartenRepository<DocsPage>
{
    /// <summary>
    /// Queries all docs for the current site, ordered by sibling sort order.
    /// </summary>
    Task<IReadOnlyList<DocsPage>> GetAllBySiteAsync(long siteId, CancellationToken ct = default);

    /// <summary>
    /// Finds a doc by site-scoped slug.
    /// </summary>
    Task<DocsPage?> FindBySlugAsync(long siteId, string slug, CancellationToken ct = default);

    /// <summary>
    /// Finds immediate children of a parent doc.
    /// </summary>
    Task<IReadOnlyList<DocsPage>> GetChildrenAsync(long siteId, long parentId, CancellationToken ct = default);

    /// <summary>
    /// Finds top-level docs (ParentId == null or parent is virtual root) for the landing page.
    /// </summary>
    Task<IReadOnlyList<DocsPage>> GetTopLevelAsync(long siteId, CancellationToken ct = default);

    /// <summary>
    /// Full-text search across title, summary, and markdown content.
    /// </summary>
    Task<IReadOnlyList<DocsPage>> SearchAsync(long siteId, string query, int limit = 20, CancellationToken ct = default);
}
```

### 5.2 Implementation: `DocsRepository`

```csharp
// D:\proj\microbians\AeroCMS\src\Aero.Cms.Modules.Docs\Repositories\DocsRepository.cs
namespace Aero.Cms.Modules.Docs.Repositories;

public sealed class DocsRepository : GenericMartenRepository<DocsPage>, IDocsRepository
{
    public DocsRepository(IDocumentSession session, ILogger<GenericMartenRepository<DocsPage>> log)
        : base(session, log) { }

    public async Task<IReadOnlyList<DocsPage>> GetAllBySiteAsync(long siteId, CancellationToken ct = default)
    {
        var results = await session.Query<DocsPage>()
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.Order)
            .ToListAsync(ct);
        return results;
    }

    public async Task<DocsPage?> FindBySlugAsync(long siteId, string slug, CancellationToken ct = default)
    {
        return await session.Query<DocsPage>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Slug == slug, ct);
    }

    public async Task<IReadOnlyList<DocsPage>> GetChildrenAsync(long siteId, long parentId, CancellationToken ct = default)
    {
        var results = await session.Query<DocsPage>()
            .Where(x => x.SiteId == siteId && x.ParentId == parentId)
            .OrderBy(x => x.Order)
            .ToListAsync(ct);
        return results;
    }

    public async Task<IReadOnlyList<DocsPage>> GetTopLevelAsync(long siteId, CancellationToken ct = default)
    {
        // Top-level docs have ParentId == null (spaces)
        var results = await session.Query<DocsPage>()
            .Where(x => x.SiteId == siteId && x.ParentId == null)
            .OrderBy(x => x.Order)
            .ToListAsync(ct);
        return results;
    }

    public async Task<IReadOnlyList<DocsPage>> SearchAsync(long siteId, string query, int limit = 20, CancellationToken ct = default)
    {
        // Marten full-text search (requires GIN index on the doc table)
        // Falls back to LIKE query if GIN not configured
        return await session.Query<DocsPage>()
            .Where(x => x.SiteId == siteId 
                && (x.Title.Contains(query) 
                    || (x.Summary != null && x.Summary.Contains(query))
                    || (x.MarkdownContent != null && x.MarkdownContent.Contains(query))))
            .Take(limit)
            .ToListAsync(ct);
    }
}
```

**Why `GenericMartenRepository<DocsPage>`?** Per AGENTS.md: "Prefer MartenDB for persistence using `GenericMartenRepository`." This gives us `InsertAsync`, `UpdateAsync`, `UpsertAsync`, `DeleteAsync`, `FindByIdAsync`, and `SaveChangesAsync` from the base class without reinventing them.

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

The existing `DocsService` will be refactored to inject `IDocsRepository` instead of using `IDocumentSession` directly. This is a clean swap:

```csharp
// BEFORE (current):
public sealed class DocsService(
    IDocumentSession session,
    IMessageBus bus,
    ISiteContext siteContext,
    IHttpContextAccessor? httpContextAccessor = null,
    IFusionCache? cache = null) : IDocsService
{
    // ... uses session.Query<DocsPage>() directly
}

// AFTER (target):
public sealed class DocsService(
    IDocsRepository repository,        // <-- NEW: replaces IDocumentSession
    IMessageBus bus,
    ISiteContext siteContext,
    IHttpContextAccessor? httpContextAccessor = null,
    IFusionCache? cache = null) : IDocsService
{
    // ... uses repository.GetAllBySiteAsync(), repository.FindBySlugAsync(), etc.
}
```

The FusionCache pattern, Wolverine event publishing, and `ModifiedBy` stamping from the current `DocsService` are all preserved — only the data access path changes.

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
  3. Build tree recursively starting from ParentId == null
  4. Return DocsTreeViewModel with nested TreeNode hierarchy
```

The sidebar CSHTML partial renders this tree with Alpine.js for expand/collapse behaviour, matching the skeleton's collapsible groups.

---

## 8. Search

### 8.1 Approach

The skeleton's search overlay (Ctrl+K) searches across docs titles, summaries, and content. 

**MVP implementation:** Use Marten LINQ `Contains()` (translated to PostgreSQL `LIKE '%query%'`). Adequate for moderate doc counts.

**Future upgrade:** Add Marten `NgramIndex(x => x.Title)` and `NgramIndex(x => x.MarkdownContent)` for fast prefix/infix search. This requires a migration to add the GIN trigram indexes.

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

**Fine-grained eviction** (future enhancement): Single-doc cache tags (`doc-id-{id}`, `doc-slug-{slug}`) from `DocsCacheTags` can be used to evict individual cached responses without invalidating the entire index. The `CmsOutputCachePolicy` reads `HttpContext.Items["AeroCms.DocId"]` and `HttpContext.Items["AeroCms.DocSlug"]` set by the PageModels to tag responses with these fine-grained identifiers.

---

## 11. Razor Pages & UI

### 11.1 Page Route Structure

| Route | Page | Purpose |
|-------|------|---------|
| `/docs` | `DocsIndex.cshtml` | Landing page — space cards grid |
| `/docs/{*path}` | `Doc.cshtml` | Individual doc page with sidebar |
| `/docs/admin/edit/{id:long}` | `DocsEditor.razor` | Admin — block editor with tree panel |
| `/docs/api/search` | Minimal API | Search endpoint |

### 11.2 `DocsIndex.cshtml` (Landing Page)

Matches the skeleton's `index.html` + `cms.html`.

**Data flow:**
1. `DocsIndexModel.OnGetAsync()` → calls `IDocsService.GetTopLevelCategoriesAsync()`
2. Returns a list of top-level "spaces" (`DocsPage` where `ParentId == null`)
3. For each space, also loads its immediate children for the "nested sections" links
4. Rendered as Tailwind cards in a responsive grid

**Key design:**
- Hero section with "Knowledge Base" heading
- Card grid (2-3 columns) for spaces
- Each card shows: title, summary, nested child links
- Empty state with "No documentation found" message
- Attributes: `[OutputCache(PolicyName = "DocsIndexPolicy")]`

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

**Code-behind (`DocModel`):**
```csharp
[OutputCache(PolicyName = "DocsPolicy")]
public class DocModel(
    IDocsService docsService,
    IDocsTreeService treeService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Path { get; set; }    // {*path} from route

    public DocsPage? DocsPage { get; private set; }
    public DocsTreeViewModel? Tree { get; private set; }
    public IReadOnlyList<DocsPage> Breadcrumbs { get; private set; } = [];
    public IReadOnlyList<HeadingAnchor> Headings { get; private set; } = [];
    public string? RenderedHtml { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var slug = string.IsNullOrWhiteSpace(Path) ? "docs" : Path.TrimStart('/');

        var docResult = await docsService.GetBySlugAsync(slug, ct);
        if (docResult is Result<DocsPage?, AeroError>.Failure || 
            docResult is Result<DocsPage?, AeroError>.Ok(var d) && d is null)
            return NotFound();

        DocsPage = (docResult as Result<DocsPage?, AeroError>.Ok)!.Value!;

        // Sidebar tree
        var treeResult = await treeService.GetTreeAsync(DocsPage.SiteId, ct);
        Tree = treeResult.Match(ok => ok, _ => null);

        // Breadcrumbs
        var breadcrumbResult = await treeService.GetBreadcrumbsAsync(DocsPage.SiteId, DocsPage.Id, ct);
        Breadcrumbs = breadcrumbResult.Match(ok => ok, _ => []);

        // "On This Page" headings
        if (!string.IsNullOrWhiteSpace(DocsPage.MarkdownContent))
        {
            RenderedHtml = Markdown.ToHtml(DocsPage.MarkdownContent);
            Headings = treeService.ExtractHeadings(DocsPage.MarkdownContent);
        }

        // Store for fine-grained cache tagging
        HttpContext.Items["AeroCms.DocId"] = DocsPage.Id;
        HttpContext.Items["AeroCms.DocSlug"] = DocsPage.Slug;

        ApplyResponseCacheHeaders();
        return Page();
    }

    private void ApplyResponseCacheHeaders()
    {
        Response.Headers.CacheControl = "public,max-age=600";
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

### 11.5 Admin Editor (`DocsEditor.razor`)

The admin editing experience is a Blazor component at `/docs/admin/edit/{id:long}`. See **Section 5 — DocsEditor.razor** for the full component specification including tree panel, block canvas, and block menu tray.

**Key features:**
- Left tree panel with drag-and-drop reordering and right-click context menu (CRUD)
- Center block canvas rendering blocks as SSR NeoUI components (WYSIWYG)
- Bottom block menu tray with draggable block types
- Toolbar with Save, Preview, Publish actions
- Property editor side panel appears when a block is selected

**Tree operations:**
- **Right-click context menu** on any tree node provides: Edit, Add Child Page, Rename, Duplicate, Move To..., Delete, View Published, Toggle Published/Draft
- **Drag-and-drop** to reorder siblings (between-items indicator) or reparent (highlight on hover)
- **Auto-save or batched save** (configurable per Phase 2 discussion)

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

The `{*path}` catch-all route parameter captures everything after `/docs/`. The `DocModel` code-behind trims leading slashes and passes the path to `IDocsService.GetBySlugAsync()`.

**Examples:**
- `/docs` → DocsIndex
- `/docs/fundamentals/setup` → Doc page with slug `"fundamentals/setup"`
- `/docs/api-reference/rest/endpoints` → Doc page with slug `"api-reference/rest/endpoints"`
- `/docs/admin` → Admin list page (exact match takes priority over catch-all)

### 12.3 URL Design

Following the skeleton pattern:
- **Spaces** are the first path segment: `/docs/fundamentals`, `/docs/api-reference`
- **Deep pages** nest with slashes: `/docs/fundamentals/setup/requirements`
- The slug stored in `DocsPage.Slug` includes the full path (e.g., `"fundamentals/setup/requirements"`)

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
// Future: In DocsModule.Configure():
opts.Schema.For<DocsPage>().NgramIndex(x => x.Title);
opts.Schema.For<DocsPage>().NgramIndex(x => x.MarkdownContent);
```

---

## 16. File Manifest

### 16.1 Complete file list for `Aero.Cms.Modules.Docs`

```
Aero.Cms.Modules.Docs/
├── Aero.Cms.Modules.Docs.csproj          ✅ existing (add BlockBase refs)
├── DocsModule.cs                         ✅ existing (register block services)
├── IDocsService.cs                       ⚠️ add SearchAsync, GetBreadcrumbsAsync, GetTreeAsync
├── DocsService.cs                        ⚠️ refactor to use IDocsRepository
├── Repositories/
│   ├── IDocsRepository.cs                🆕
│   └── DocsRepository.cs                 🆕
├── Services/
│   ├── IDocsTreeService.cs               🆕 (add MovePageAsync, CreateChildPageAsync)
│   └── DocsTreeService.cs                🆕
├── Blocks/
│   ├── DocsMarkdownBlock.cs              🆕
│   ├── DocsCodeBlock.cs                  🆕
│   ├── DocsCalloutBlock.cs               🆕
│   ├── DocsImageBlock.cs                 🆕
│   ├── DocsTableBlock.cs                 🆕
│   ├── DocsChildPagesBlock.cs            🆕
│   └── Rendering/
│       ├── DocsMarkdownBlockRenderer.cs  🆕
│       ├── DocsCodeBlockRenderer.cs      🆕
│       ├── DocsCalloutBlockRenderer.cs   🆕
│       ├── DocsImageBlockRenderer.cs     🆕
│       ├── DocsTableBlockRenderer.cs     🆕
│       └── DocsChildPagesBlockRenderer.cs🆕
├── Components/                           (NeoUI SSR components)
│   ├── DocsMarkdownView.razor            🆕
│   ├── DocsCodeView.razor                🆕
│   ├── DocsCalloutView.razor             🆕
│   ├── DocsImageView.razor               🆕
│   ├── DocsTableView.razor               🆕
│   ├── DocsChildPagesView.razor          🆕
│   ├── DocsEditor.razor                  🆕 (admin block editor)
│   ├── DocsEditor.razor.cs               🆕
│   ├── DocsTreePanel.razor               🆕 (drag-drop tree)
│   ├── DocsTreePanel.razor.cs            🆕
│   ├── DocsBlockCanvas.razor             🆕 (blocks WYSIWYG)
│   └── DocsBlockMenuTray.razor           🆕 (draggable block types)
├── Caching/
│   └── DocsCacheTags.cs                  ✅ existing
├── Validators/
│   └── DocsPageValidator.cs              🆕
├── Areas/
│   └── Docs/
│       ├── Pages/
│       │   ├── DocsIndex.cshtml          ⚠️ refactor to use IDocsService
│       │   ├── DocsIndex.cshtml.cs       ⚠️ refactor to use IDocsService
│       │   ├── Doc.cshtml                ⚠️ refactor to render LayoutRegions + sidebar
│       │   └── Doc.cshtml.cs             ⚠️ refactor to use IDocsService + BlockRenderCache
│       ├── Partials/
│       │   ├── _DocsSidebar.cshtml       🆕
│       │   ├── _DocsBreadcrumbs.cshtml   🆕
│       │   ├── _OnThisPage.cshtml        🆕
│       │   └── _SearchOverlay.cshtml     🆕
│       ├── _DocsLayout.cshtml            🆕 (shared layout for docs pages)
│       ├── _ViewImports.cshtml           🆕
│       └── _ViewStart.cshtml             🆕
├── wwwroot/
│   ├── css/
│   │   └── docs.css                      ✅ existing (extend)
│   └── js/
│       └── docs.js                       🆕 (Alpine.js components)
└── DocsMartenConfiguration.cs            🗑️ REMOVE (redundant)
```

### 16.2 Files Outside the Module (In Shared Projects)

| File | Project | Status |
|------|---------|--------|
| `DocsPage.cs` | `Aero.Cms.Core.Entities` | ✅ Complete |
| `DocViewModel.cs` | `Aero.Cms.Abstractions.Models` | ✅ Complete |
| `AeroEvents.cs` (Docs events) | `Aero.Cms.Abstractions.Events` | ✅ Complete |
| `OutputCacheModule.cs` (policies) | `Aero.Cms.Modules.OutputCache` | ✅ Complete |
| `CmsOutputCachePolicy.cs` | `Aero.Cms.Modules.OutputCache` | ✅ Complete |

---

## 17. Implementation Order

### Phase 1 — Foundation (HIGH)
1. Create `IDocsRepository` + `DocsRepository`
2. Refactor `DocsService` to inject `IDocsRepository`
3. Create `DocsPageValidator` + register in `DocsModule`
4. Refactor `DocsIndexModel` and `DocModel` to use `IDocsService`
5. Remove `DocsMartenConfiguration.cs` (redundant)
6. Define block types (`DocsMarkdownBlock`, `DocsCodeBlock`, etc.) in `Blocks/`
7. Implement `IBlockRenderer` for each block type
8. Register block types and renderers via source generators in `DocsModule`

### Phase 2 — Admin Editor (HIGH)
9. Implement `DocsEditor.razor` + `DocsEditor.razor.cs` (layout with tree + canvas)
10. Implement `DocsTreePanel.razor` with NeoUI TreeView + drag-and-drop
11. Implement right-click context menu on tree nodes (CRUD actions)
12. Implement `DocsBlockCanvas.razor` (SSR block preview)
13. Implement `DocsBlockMenuTray.razor` (draggable block types)
14. Add `MovePageAsync` and `CreateChildPageAsync` to `IDocsTreeService`
15. Wire admin route `/docs/admin/edit/{id:long}` in `DocsModule`

### Phase 3 — Navigation (HIGH)
16. Create `IDocsTreeService.GetTreeAsync()` for public sidebar
17. Create `_DocsSidebar.cshtml` partial with Alpine.js tree
18. Create `_DocsBreadcrumbs.cshtml` partial
19. Add `GetBreadcrumbsAsync` to `IDocsService`
20. Update `Doc.cshtml` to render LayoutRegions (blocks) with sidebar + breadcrumbs
21. Add `BlockRenderCache` preloading to `DocModel.OnGetAsync()`
22. Create `_DocsLayout.cshtml`, `_ViewImports.cshtml`, `_ViewStart.cshtml`

### Phase 4 — Search (MEDIUM)
23. Add `SearchAsync` to `IDocsService` + `DocsService`
24. Create `/docs/api/search` minimal API endpoint
25. Create `_SearchOverlay.cshtml` partial with Alpine.js
26. Wire search overlay into the docs layout

### Phase 5 — Polish (MEDIUM)
27. Create `_OnThisPage.cshtml` with scroll spy
28. Implement NeoUI renderer components (`DocsMarkdownView.razor`, etc.)
29. Add Alpine.js components in `wwwroot/js/docs.js`
30. Extend `docs.css` with skeleton-matching styles
31. Add seed data for starter docs
32. Add emoji feedback buttons

### Phase 6 — Future (LOW)
33. Marten NgramIndex for fast search
34. Version selector dropdown
35. Dedicated search page with pagination
36. Doc analytics (view counts)
37. AI chat assistant integration

---

## 18. Testing Strategy

| Test Type | Tool | Scope |
|-----------|------|-------|
| **Unit** | TUnit + NSubstitute | `DocsService`, `DocsRepository`, `DocsTreeService`, `DocsPageValidator` |
| **Integration** | Alba + Embedded Postgres | Razor Page rendering, search API, cache behaviour |
| **End-to-End** | Playwright | Sidebar navigation, search overlay, admin CRUD flow |

---

## 19. Open Questions for Council Review

1. **Arbitrary-depth nesting vs. fixed depth**: The current `DocsPage.ParentId` supports arbitrary depth. The skeleton suggests a 3-level structure (spaces → chapters → sections). Should we enforce a depth limit, or keep it flexible?

2. **Slug strategy**: Should slugs be flat (`"fundamentals/setup/requirements"`) or hierarchical (concatenated from parent slugs)? The current `DocsPage.Slug` stores the full path. Should the slug auto-populate from the tree path when a page is moved via drag-and-drop?

3. **Search fallback**: For the MVP, `Contains()` (LIKE) is used. Should we add NgramIndex immediately, or defer to Phase 6?

4. **Admin auth**: Should the `/docs/admin/*` routes require `[Authorize]` attribute with a specific policy, or is the existing middleware sufficient?

5. **Share layout with Pages?** The `_DocsLayout.cshtml` could potentially share structural elements with the Pages layout (header/footer). SG the `ViewBag` approach used by Pages (`ViewBag.ShowHeaderNavigation`, `ViewBag.HideFooter`). Should Docs maintain its own layout file or inherit from the CMS layout?

6. **~Markdown editor~** ✅ RESOLVED: The Docs module now uses block composition (like Pages) with NeoUI SSR renderers. The `DocsMarkdownBlock` handles Markdown content as one of several block types. Radzen WYSIWYG is not needed — block editing uses NeoUI property editors in the block sidebar panel.

7. **Editor state separation**: Should the Docs editor follow Pages' pattern of separating editor state (`DocsEditorState` — scratch/wip blocks) from the published document (`DocsPage.LayoutRegions` — committed blocks), or should it use a simpler single-document model?

8. **Block type scope**: Are the 6 proposed block types (Markdown, Code, Callout, Image, Table, ChildPages) sufficient for the MVP, or should additional types (e.g., Video, Tabs, Accordion, API Reference) be added?

9. **Drag-and-drop persistence strategy**: Should tree reorder operations save immediately (auto-save on drop) or batch into a single "Save Changes" action alongside block edits?

10. **Public sidebar tree depth**: Should the public-facing sidebar show the full tree (all levels) or only expand to a configurable depth (e.g., 3 levels)?

---

## 20. Council Review — Findings & Resolutions

> **Reviewer:** gamma (minimax-m2.7) — 1/3 councillors responded (alpha/beta timed out)  
> **Verdict:** Plan is **solid in direction** — vertical slice, repository pattern, ROP, source generators, block composition matching Pages. Key risks identified and addressed below.

### 20.1 Critical Issues (Must Fix Before Phase 1)

| # | Issue | Resolution |
|---|-------|------------|
| **C1** | `DocsEditorState` entity not defined in the codebase, but referenced throughout Phase 2 | **Add `DocsEditorState` entity** to the file manifest and define it in Phase 1. Follow Pages' `PageEditorState` pattern. |
| **C2** | `DocsService.SaveAsync` calls `session.Store(page)` directly — bypasses repository | **Use `repository.UpsertAsync(page)`** in the refactored service. Same for `DeleteAsync`. |
| **C3** | Missing `BlockSchemaVersion` on `DocsPage` for migration idempotency | **Add `BlockSchemaVersion` (int)** to `DocsPage` mirroring `PageDocument` (line 89). |
| **C4** | `GetTreeAsync` loads ALL published docs into memory | **Add `GetSubtreeAsync(long parentId, int maxDepth)`** for context-aware loading. Use full tree only for small sites with FusionCache. |
| **C5** | Search uses `Contains()` (LIKE) — slow at scale | **Add NgramIndex in Phase 1**, not Phase 6. One-line Marten config: `opts.Schema.For<DocsPage>().NgramIndex(x => x.Title)`. |

### 20.2 Open Questions — Resolved

| Q | Topic | Resolution | Reasoning |
|---|-------|------------|-----------|
| **Q1** | Depth limit | **Arbitrary depth, max 10** | Enforce `MaxDepth=10` in `MovePageAsync`. Flexible UX without pathological trees. |
| **Q2** | Slug strategy | **Independent slug + tree path** | Like Pages: `DocsPage.Slug` is the URL segment; tree position from `ParentId`. No slug recalculation on move. |
| **Q3** | Search index | **NgramIndex immediately** | Phase 1 (addressed in C5 above). |
| **Q4** | Admin auth | **Yes — `[Authorize(Policy = "CMSAdmin")]`** | Security gap. Add to admin route registration. |
| **Q5** | Layout sharing | **Separate `_DocsLayout` with composition** | Docs has distinct UX (sidebar + content + on-this-page). Compose from base layout, don't share with Pages. |
| **Q6** | Markdown editor | **RESOLVED** — block composition | v2 document. |
| **Q7** | Editor state separation | **Two-document model** (`DocsEditorState` + `DocsPage`) | Consistent with Pages. Enables draft/publish workflows. |
| **Q8** | Block type count | **6 types sufficient, but clarify `DocsChildPagesBlock`** | Consider making it a sidebar component, not a stored block — it's a computed query, not stored content. |
| **Q9** | Drag-drop save | **Batched save** | "Save Tree Changes" button batches all reorders into a single Marten transaction. Avoids silent commits and concurrency issues. |
| **Q10** | Sidebar depth | **Configurable, default 3** | `SidebarMaxDepth` setting. Truncate tree at `currentDepth + maxDepth` in `GetTreeAsync`. |

### 20.3 Missing Considerations (Added to Plan)

| # | Missing | Impact | Mitigation Added |
|---|---------|--------|------------------|
| **M1** | No version/comparison for blocks | Can't detect "unpublished changes" | Add `PublishedVersion` to `DocsPage` (or `DocsEditorState`) |
| **M2** | "On This Page" assumes `MarkdownContent` | Breaks with block-only pages | Extract headings from rendered HTML of `LayoutRegions`, not raw MarkdownContent |
| **M3** | Slug uniqueness conflict on move | Possible data corruption | Check uniqueness in `MovePageAsync`; reject or auto-suffix if collision |
| **M4** | No migration path for legacy MarkdownContent → blocks | Existing docs stay in legacy state | When legacy doc is opened in block editor, auto-migrate `MarkdownContent` into a `DocsMarkdownBlock` |
| **M5** | Concurrent tree edits (lost updates) | Two admins reorder same tree | Add **Marten optimistic concurrency** on `DocsPage` (already partially configured: `UseOptimisticConcurrency` exists in Pages but not Docs) |
| **M6** | `DocsChildPagesBlock` computed at render time | Potential N+1 for deep pages | Pre-fetch child page IDs in `BlockRenderCache.PreloadAsync`; or move to sidebar component |

### 20.4 Risk Matrix

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `DocsEditorState` undefined → Phase 2 delay | **High** | Medium | Define entity in Phase 1 |
| Circular reference on tree reparent | **Medium** | High | Ancestor-chain validation in `MovePageAsync` |
| LIKE search degrades at 500+ docs | **High** | Medium | NgramIndex in Phase 1 |
| Block editor can't handle legacy MarkdownContent | **High** | Medium | Auto-migration on editor open |
| Concurrent tree reorders → lost updates | **Medium** | Medium | Marten optimistic concurrency |
| No authorization on admin routes | **Low** | High | `[Authorize]` in Phase 2 |
| Slug uniqueness violation on page move | **Low** | Medium | Check and reject/auto-suffix |

### 20.5 Implementation Priority Adjustments

Based on council findings, reorder Phase 1 to include the critical issues:

**Adjusted Phase 1 — Foundation (HIGH):**
1. **Define `DocsEditorState` entity** (C1) — BEFORE building the editor
2. Add `BlockSchemaVersion` to `DocsPage` (C3)
3. Add `NgramIndex` to Marten config (C5)
4. Create `IDocsRepository` + `DocsRepository` with proper save/delete via repository methods (C2)
5. Refactor `DocsService` to inject `IDocsRepository` and use `repository.UpsertAsync` / `repository.DeleteAsync`
6. Create `DocsPageValidator` + register in `DocsModule`
7. Refactor `DocsIndexModel` and `DocModel` to use `IDocsService`
8. Remove `DocsMartenConfiguration.cs` (redundant)
9. Define block types (`DocsMarkdownBlock`, etc.) in `Blocks/`
10. Implement `IBlockRenderer` for each block type
11. Add `AddSubtreeAsync()` to `IDocsTreeService` (C4)
12. Implement `IDocsTreeService` methods with proper tree query scoping

---

<!--
  ═══════════════════════════════════════════════════════════
  EDITOR NOTES:
  - This document is a living spec. Update as implementation progresses.
  - All "Umbraco" strings in docs-skeleton/ are replaced with "Aero".
  - Follow AGENTS.md constraints: no reflection, source generators, FluentValidation, ROP.
  ═══════════════════════════════════════════════════════════
-->
