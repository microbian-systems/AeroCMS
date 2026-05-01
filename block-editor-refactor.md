# Block Editor Refactor — Architecture Analysis & Migration Plan

> **Status:** Tactical fix (Option B) applied (verified). Strategic refactor (Option C) planned, council review incorporated.
> **Author:** Staff-level architecture review + Council review
> **Date:** 2026-04-27 (Council review: 2026-04-27)

---

## Table of Contents

1. [Current Architecture](#1-current-architecture)
2. [Root Cause: Why Seed Data Fails](#2-root-cause-why-seed-data-fails)
3. [Tactical Fix (Option B)](#3-tactical-fix-option-b)
4. [Architecture Critique](#4-architecture-critique)
5. [Strategic Fix (Option C)](#5-strategic-fix-option-c)
6. [Migration Script Strategy](#6-migration-script-strategy)
7. [Risk Assessment](#7-risk-assessment)
8. [Decision Log](#8-decision-log)

---

## 1. Current Architecture

### 1.1 Dual Storage Model

AeroCMS currently maintains **two independent block storage systems** inside a single `PageDocument` entity:

```
┌─────────────────────────────────────────────────────────────────────┐
│                        PageDocument                                  │
│  (single JSON row in mt_doc_pagedocument via Marten)                │
├────────────────────────────────┬────────────────────────────────────┤
│  Blocks                         │  LayoutRegions                    │
│  (List<EditorBlock>)           │  (List<LayoutRegion>)              │
├────────────────────────────────┼────────────────────────────────────┤
│  Inline JSON in document       │  Inline JSON in document           │
│                                 │  Contains BlockPlacement[]        │
│  Flat bag-of-properties:       │  Each has BlockId + BlockType      │
│    Type: "aero_hero"           │  (references SEPARATE Marten doc)  │
│    MainText: "..."             │                                    │
│    SubText: "..."              └───────────┬────────────────────────┘
│    FeatureItems: [...]                     │
│    ...30 properties            ┌───────────▼────────────────────────┐
│                                │  BlockBase (polymorphic)           │
│  Used by: PageEditor.razor    │  Stored as individual Marten docs  │
│  (read/write)                  │  via session.Store(block)          │
│                                │                                    │
│                                │  ├── HeadingBlock                  │
│                                │  ├── RichTextBlock                 │
│                                │  ├── CtaBlock                      │
│                                │  ├── QuoteBlock                    │
│                                │  ├── ImageBlock                    │
│                                │  ├── EmbedBlock                    │
│                                │  ├── AeroHeroBlock                 │
│                                │  ├── AeroFeaturesBlock             │
│                                │  ├── ... 12 more Aero types        │
│                                │  └── ... 7 legacy types            │
│                                │                                    │
│                                │  Used by: Render pipeline          │
│                                │  (IBlockVisitor / BlockSliceRegistry)│
└────────────────────────────────┴────────────────────────────────────┘
```

### 1.2 File Inventory

#### Core entities

| File | Role |
|---|---|
| `src/Aero.Cms.Core.Entities/PageDocument.cs:22-28` | Defines `LayoutRegions` + `Blocks` dual storage |
| `src/Aero.Cms.Abstractions/Blocks/EditorBlock.cs:1-98` | Flat `EditorBlock` class with 30+ properties |
| `src/Aero.Cms.Abstractions/Blocks/BlockBase.cs:1-69` | Polymorphic `BlockBase` abstract class |
| `src/Aero.Cms.Abstractions/Blocks/ConcreteBlocks.cs:1-193` | Legacy blocks: `HeadingBlock`, `RichTextBlock`, `CtaBlock`, `QuoteBlock`, `ImageBlock`, `EmbedBlock`, `NavigationBlock` |
| `src/Aero.Cms.Abstractions/Blocks/Layout/LayoutRegion.cs:1-22` | Layout region model |
| `src/Aero.Cms.Abstractions/Blocks/Layout/LayoutColumn.cs:1-24` | Layout column model |
| `src/Aero.Cms.Abstractions/Blocks/Layout/BlockPlacement.cs:1-23` | Block placement (reference by ID) |
| `src/Aero.Cms.Core.Entities/BlogPostDocument.cs:21` | Blog posts ALSO use `List<BlockBase>` |
| `src/Aero.Cms.Abstractions/Blocks/IBlockVisitor.cs:1-16` | Visitor pattern interface |
| `src/Aero.Cms.Web.Core/Blocks/Rendering/BlockSliceRegistry.cs:1-108` | Visitor dispatcher |
| `src/Aero.Cms.Web.Core/Blocks/Rendering/IBlockSliceRenderer.cs:1-26` | Renderer interface |
| `src/Aero.Cms.Core/Blocks/MartenBlockService.cs:1-29` | Loads individual BlockBase from DB |
| `src/Aero.Cms.Abstractions/Blocks/IBlockService.cs:1-25` | Service interface |

#### API layer

| File | Role |
|---|---|
| `src/Aero.Cms.Modules.Headless/Areas/Api/v1/PagesApi.cs:247-264` | `MapToDetail()` — only reads `p.Blocks`, ignores `LayoutRegions` |
| `src/Aero.Cms.Modules.Pages/PageContentService.cs:105-133` | `CreateAsync/UpdateAsync` — stores BOTH `Blocks` and converts to `LayoutRegions` |
| `src/Aero.Cms.Modules.Pages/PageContentService.cs:231-413` | `MapEditorBlocksToLayoutRegions()` + `MapEditorBlock()` — one-way converter |

#### UI layer

| File | Role |
|---|---|
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor` | **Active editor** — works with `EditorBlock` |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor.cs:783-854` | `SavePage()` — sends `EditorBlocks` list to API |
| `src/Aero.Cms.Shared/Components/BlockEditor.razor` | **UNUSED** — old editor that works with `BlockBase` directly |
| `src/Aero.Cms.Shared/Components/BlockPicker.razor` | **UNUSED** — old block type picker |

### 1.3 Save Flow (creating "mytestpage")

```
PageEditor.razor.cs:SavePage()
  │
  │   Blocks = [
  │     EditorBlock{ Type="text", Content="hello" },
  │     EditorBlock{ Type="content", Content="<p>world</p>" }
  │   ]
  │
  ├─► PageEditor.razor.cs:783
  │   CreatePageRequest(..., EditorBlocks = Blocks)
  │
  ├─► PagesHttpClient: src/Aero.Cms.Abstractions/Http/Clients/PagesClient.cs:128
  │   POST /api/admin/pages → serialized as JSON
  │
  ├─► PagesApi.CreatePage: src/Aero.Cms.Modules.Headless/Areas/Api/v1/PagesApi.cs:123
  │   [FromBody] CreatePageRequest → forwards to service
  │
  ├─► MartenPageContentService.CreateAsync:
  │   src/Aero.Cms.Modules.Pages/PageContentService.cs:105-133
  │   │
  │   │   Creates PageDocument instance:
  │   │   ├── page.Blocks = request.EditorBlocks.ToList()
  │   │   │   (stored inline in JSON column — fast round-trip)
  │   │   │
  │   │   └── page.LayoutRegions = MapEditorBlocksToLayoutRegions(...)
  │   │       │  src/Aero.Cms.Modules.Pages/PageContentService.cs:231-265
  │   │       │
  │   │       │   For each EditorBlock (e.g. "content"):
  │   │       │     block = MapEditorBlock(eb)
  │   │       │     │  src/Aero.Cms.Modules.Pages/PageContentService.cs:267-413
  │   │       │     │  Switch on eb.Type:
  │   │       │     │    "content"  → new RichTextBlock { Content = eb.Content }
  │   │       │     │    "aero_cta" → new AeroCtaBlock { Title = eb.MainText, ... }
  │   │       │     │    "text"     → new HeadingBlock { Text = eb.Content }
  │   │       │     │    ...20+ cases
  │   │       │     │
  │   │       │     blockService.SaveAsync(block)
  │   │       │     │  src/Aero.Cms.Core/Blocks/MartenBlockService.cs:23-28
  │   │       │     │  session.Store(block)    ◄── SEPARATE Marten row
  │   │       │     │
  │   │       │     placements.Add(BlockPlacement{ BlockId = block.Id })
  │   │       │
  │   │       └── Returns LayoutRegion[1] → Column[1] → placements[N]
  │   │
  │   └── session.Store(pageDocument)
  │       session.SaveChangesAsync()
  │       ◄── NOW stored in DB:
  │           mt_doc_pagedocument: { Id, Title, Blocks: [...], LayoutRegions: [...], ... }
  │           mt_doc_blockbase:      { Id, BlockType, Content, ... }  ← N separate rows
  │
  └──► Returns PageDetail with Blocks (editor can reload)
```

### 1.4 Load Flow (editor opens a page)

```
PageEditor.razor.cs:OnInitializedAsync()
  │  src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor.cs:110-132
  │
  └─► LoadPageAsync(Id)
      │  src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor.cs:134-164
      │
      └─► PagesClient.GetByIdAsync(id)
          │  src/Aero.Cms.Abstractions/Http/Clients/PagesClient.cs:110
          │
          └─► PagesApi.GetPageById
              │  src/Aero.Cms.Modules.Headless/Areas/Api/v1/PagesApi.cs:75-97
              │
              └─► pageService.LoadAsync(id)  → returns PageDocument
              │   │
              │   └─► MapToDetail(p)
              │       │  src/Aero.Cms.Modules.Headless/Areas/Api/v1/PagesApi.cs:247-264
              │       │
              │       │   return new PageDetail(
              │       │     ...,
              │       │     p.Blocks.Count,    ◄── BlockCount
              │       │     p.Blocks           ◄── IReadOnlyList<EditorBlock>?
              │       │   )
              │       │
              │       │   FOR SEED DATA: p.Blocks is EMPTY → BlockCount = 0
              │       │   FOR EDITOR-SAVED: p.Blocks has items → works
              │       │
              └─► PageEditor.razor.cs:152-155
                  if (page.Blocks != null)
                      Blocks = page.Blocks.ToList();  ◄── EMPTY → shows empty canvas
```

### 1.5 Render Flow (public visitor views a page)

```
HTTP GET /en/about
  │
  └─► PageService.GetPageAsync()
      │
      └─► PageReadPipeline (Chain of Responsibility)
          │  src/Aero.Cms.Web.Core/Pipelines/
          │
          └─► Razor Slice Rendering
              │  PageSlice<PageModel>
              │  → RegionSlice (iterates LayoutRegions)
              │    → BlockSliceRegistry
              │      → Resolve(block.GetType())       ◄── CLR type dispatch
              │        → IBlockSliceRenderer.Render()  ◄── casts to BlockBase
              │
              │  BLOCK LOADING:
              │  LayoutRegions[0].Columns[0].Blocks[0].BlockId
              │    → blockService.GetByIdAsync(blockId)
              │      → _session.LoadAsync<BlockBase>(id)   ◄── N+1 per block!
```

---

## 2. Root Cause: Why Seed Data Fails

### 2.1 The Asymmetry

The `SeedDataService.cs` (`src/Aero.Cms.Modules.Setup/SeedDataService.cs:287-563`) creates pages using the **old system only**:

```csharp
// SeedDataService.cs:358-393 — BuildHomepage()
return (
    new PageDocument
    {
        // ...
        LayoutRegions = [ /* populated with BlockPlacement references */ ],
        // ⚠ Blocks is NOT set — defaults to empty list
    },
    new List<BlockBase> { headingBlock, bodyBlock }
);
```

But the **editor reads only `Blocks`** (`PageDocument.Blocks`, a `List<EditorBlock>`). Since `Blocks` is empty for seeded pages:

1. `PagesApi.MapToDetail()` at `src/Aero.Cms.Modules.Headless/Areas/Api/v1/PagesApi.cs:262` returns `p.Blocks` → **empty list**
2. `PageEditor.razor.cs:152-155` assigns `Blocks = page.Blocks.ToList()` → **empty**
3. `PageEditor.razor:124` checks `Blocks.Count == 0` → **shows empty state**

### 2.2 Why New Pages Work

When a user creates a page through the editor and saves:
- `MartenPageContentService.CreateAsync()` at `src/Aero.Cms.Modules.Pages/PageContentService.cs:121-124`:
  ```csharp
  page.Blocks = request.EditorBlocks.ToList();  // ← populates Blocks
  page.LayoutRegions = await MapEditorBlocksToLayoutRegions(...);  // ← also populates LayoutRegions
  ```
- Both are stored. When loaded back, `Blocks` has data → editor renders it.

### 2.3 Summary

| Scenario | `page.Blocks` (EditorBlocks) | `page.LayoutRegions` (BlockBase refs) | Editor works? |
|---|---|---|---|
| Seeded page | Empty | Populated | ❌ No blocks shown |
| Editor-created | Populated | Populated | ✅ Full editing |
| Editor-edit + save | Updated | Re-generated | ✅ Works |

---

## 3. Tactical Fix (Option B)

### 3.1 What We Changed

**Single file modified:** `src/Aero.Cms.Modules.Setup/SeedDataService.cs`

Added `Blocks = new List<EditorBlock> { ... }` to all 4 page builders, mapping each old-style block to its `EditorBlock` equivalent:

#### BuildHomepage() — line 358-399

| Old block | `EditorBlock` |
|---|---|
| `HeadingBlock(Level=1, Text=homepageTitle)` | `Type="text", Content=homepageTitle` |
| `RichTextBlock(Content=html)` | `Type="content", Content=body.Content` |

```csharp
Blocks = new List<EditorBlock>
{
    new() { Type = "text", Content = Normalize(request.HomepageTitle) },
    new() { Type = "content", Content = bodyBlock.Content }
},
```

#### BuildBlogListingPage() — line 417-457

```csharp
Blocks = new List<EditorBlock>
{
    new() { Type = "text", Content = Normalize(request.BlogName) },
    new() { Type = "content", Content = bodyBlock.Content }
},
```

#### BuildAboutPage() — line 476-516

```csharp
Blocks = new List<EditorBlock>
{
    new() { Type = "text", Content = "About Us" },
    new() { Type = "content", Content = bodyBlock.Content }
},
```

#### BuildContactPage() — line 542-578

```csharp
Blocks = new List<EditorBlock>
{
    new() { Type = "text", Content = "Contact Us" },
    new() { Type = "content", Content = bodyBlock.Content },
    new() { Type = "aero_cta", MainText = ctaBlock.Text, CtaText = ctaBlock.Text, CtaUrl = ctaBlock.Url }
},
```

### 3.2 What Stays the Same

- The old `session.Store(block)` calls still persist `BlockBase` documnts (the renderer needs them)
- `LayoutRegions` with `BlockPlacement` references remains (for renderer)
- The tuple return signature `(PageDocument, List<BlockBase>)` is unchanged

### 3.3 What Changed

Each `PageDocument` now has `Blocks` populated with matching `EditorBlock` objects. The editor will load these on open, and on first user save, the existing `MapEditorBlocksToLayoutRegions` pipeline will regenerate fresh rendering blocks from them.

### 3.4 Verification

Build passes: `dotnet build src/Aero.Cms.Modules.Setup/` — **0 errors, 0 new warnings** (4 `Blocks` entries confirmed via `rg -c`).

**⚠ Note:** The initial edit attempt did NOT persist to disk due to a tool issue on Windows. The fix was re-applied and verified via actual file content search (`rg "Blocks = new List<EditorBlock>"` returns 4 matches across the 4 page builders). Always verify persisted edits with a filesystem read after "Edit applied successfully."

To verify end-to-end:
1. Reset the database (delete or recreate)
2. Run the setup/seed flow
3. Navigate to Pages manager → click any seeded page
4. Editor should show the page's blocks in the canvas

---

## 4. Architecture Critique

### 4.1 Staff-Level Analysis

The dual-storage architecture has **fundamental design flaws** that would be flagged in any senior/staff-level code review:

#### Issue 1: Dual Source of Truth

```
            ┌──► EditorBlock (inline in PageDocument)
            │       │
Save path   │       ├── Editor reads this
            │       └── Serialized as flat JSON
            │
Write───────┤
            │
            │       ┌──► BlockBase (separate Marten docs)
            │       │
            └──► LayoutRegions ──references──► BlockBase.BlockId
                    │
                    ├── Render pipeline reads this
                    └── Requires separate DB query per block (N+1)
```

Every save mutates two storage systems that can diverge. There is no referential integrity enforcement — a `BlockPlacement` can reference a `BlockId` that was deleted, or vice versa.

#### Issue 2: N+1 Database Reads on Render

The render pipeline loads one `PageDocument` from Marten (1 query), then for each `BlockPlacement` calls `_session.LoadAsync<BlockBase>(id)` (N queries). For a page with 5 blocks: **6 database round-trips**. Umbraco/Orchard Core do this in **1**.

#### Issue 3: String-Typed Discriminator vs Schema-Typed

`EditorBlock.Type` uses magic strings (`"aero_hero"`, `"content"`, `"text"`). There's no compile-time validation — a typo silently produces a blank block or a fallback renderer. The `RenderBlock` switch at `PageEditor.razor:606` has a `_ => RenderReferenceBlock(block)` fallback that silently swallows unknown types.

#### Issue 4: Dead Code

| File | Status | Lines |
|---|---|---|
| `src/Aero.Cms.Shared/Components/BlockEditor.razor` | **Unused** — 499 lines of old block editor UI | Obsolete |
| `src/Aero.Cms.Shared/Components/BlockPicker.razor` | **Unused** — 133 lines of old block type picker | Obsolete |
| `src/Aero.Cms.Abstractions/Blocks/ConcreteBlocks.cs` | 6 of 7 types only used by seed data + renderer | Semi-obsolete |
| `src/Aero.Cms.Abstractions/Blocks/IBlockVisitor.cs` | Single implementation (`BlockSliceRegistry`) | Coupling point |
| `src/Aero.Cms.Abstractions/Blocks/IBlockService.cs` | Only used by `MapEditorBlock` → `blockService.SaveAsync` | Obsolete if Option C done |

These represent **~800 lines of dead or nearly-dead code** that still need maintenance, still appear in searches, and still confuse new developers.

### 4.2 Comparison: AeroCMS vs Umbraco vs Orchard Core

| Dimension | AeroCMS (current) | Umbraco v13+ | Orchard Core |
|---|---|---|---|
| **Storage model** | 2 models: `EditorBlock` (flat) + `BlockBase` (polymorphic) | 1 model: Element type (`PublishedElement`) | 1 model: ContentItem (content parts) |
| **Editor data** | `List<EditorBlock>` inline in document | Element types stored in `ElementValue` JSON | Content parts stored in `ContentItem` JSON |
| **Render data** | `List<LayoutRegion>` → `BlockPlacement` → separate `BlockBase` docs | Same element types (no separation) | Same content parts (no separation) |
| **Type safety** | String discriminator (`Type: "aero_hero"`) — no compile-time check | Schema-defined element types with C# strongly-typed models | Strongly-typed content part C# classes |
| **DB reads per render** | 1 (page) + N (blocks) = **N+1** | 1 (document with inline elements) | 1 (document with inline content items) |
| **Conversion needed** | Yes: `EditorBlock` ↔ `BlockBase` via `MapEditorBlock()` | No — editor and renderer share the same model | No — content parts are the single model |
| **Migration path** | Complex — 40+ files touch `BlockBase` | N/A (single model from start) | N/A (single model from start) |
| **Dead code** | ~800 LOC across 5 files | Minimal | Minimal |
| **Editor UI** | Custom Blazor with string-switched renderers | Angular-based backoffice | Razor + Admin theme |

### 4.3 Code Path Counts

```
rg "BlockBase" --type cs src/ | wc -l       → ~40 files reference BlockBase
rg "EditorBlock" --type cs src/ | wc -l     → ~10 files reference EditorBlock
rg "BlockEditor.razor" src/ --type razor    → 0 references (unused)
rg "BlockPicker.razor" src/ --type razor    → 0 references (unused)
```

The old system has ~4× the surface area of the new system, yet the new system is what users interact with daily.

---

## 5. Strategic Fix (Option C)

### 5.1 Vision: Single Source of Truth

```
┌─────────────────────────────────────────────────────────┐
│                    PageDocument                          │
├─────────────────────────────────────────────────────────┤
│  Blocks: List<EditorBlock>                              │
│  [                                                      │
│    { Type: "aero_hero", MainText: "...", ... },         │
│    { Type: "content", Content: "<p>...</p>" },          │
│    { Type: "aero_cta", MainText: "...", CtaUrl: "..." } │
│  ]                                                      │
│                                                          │
│  (NO LayoutRegions — EditorBlocks define the layout)    │
│  (NO separate BlockBase docs — all inline)              │
└──────────────────────────────────────────────────────────┘
         │                                │
         │  Editor reads/writes           │  Renderer reads directly
         ▼                                ▼
  PageEditor.razor                   IBlockSliceRenderer
  (no conversion needed)             (dispatch by Type string,
                                     not CLR type)
```

### 5.2 Phase 1: Make Renderers Consume EditorBlock Directly

The editor already proves this is possible. At `PageEditor.razor:689-704`:

```csharp
RenderFragment RenderAeroHeroBlock(EditorBlock b, bool sel) => __builder =>
{
    var model = new AeroHeroBlock  // ← creates BlockBase in-memory for preview
    {
        Title = b.MainText,
        Description = b.SubText,
        Layout = Enum.TryParse<AeroHeroLayout>(b.AeroLayout, ...),
        ...
    };
    <AeroHeroRenderer Block="model" />
```

The renderer (`AeroHeroRenderer`) takes a `BlockBase` parameter. Phase 1 changes renderers to accept `EditorBlock` instead, eliminating the in-memory conversion layer.

**⚠ Council finding:** A search for `: IBlockSliceRenderer` across the entire codebase returned **zero results**. The `BlockSliceRegistry` infrastructure exists but no concrete renderers are registered into it. The visitor pattern rendering pipeline described in Section 1.5 is **built infrastructure without consumers**. This means:

1. The actual page rendering may use a completely different code path (Blazor server renderers at `AeroHeroRenderer`, `AeroFeaturesRenderer`, etc.)
2. The N+1 database concern may be less impactful than stated if the visitor pipeline isn't used for hot-path rendering
3. Before Phase 1 begins, trace the actual render path for public page views to confirm whether it uses `BlockBase` → visitor → renderer, or a different mechanism

**Files to modify (preliminary — verify actual render path first):**
- `src/Aero.Cms.Web.Core/Blocks/Rendering/IBlockSliceRenderer.cs` — Change `Render(BlockBase block)` to `Render(EditorBlock block)`
- `src/Aero.Cms.Web.Core/Blocks/Rendering/BlockSliceRegistry.cs` — Change dispatch to key on `EditorBlock.Type` string instead of `block.GetType()` CLR type
- `src/Aero.Cms.Abstractions/Blocks/IBlockVisitor.cs` — Change `Visit(BlockBase block)` to `Visit(EditorBlock block)` (required to update the visitor pattern interface that all 32+ block types implement via `Accept()`)
- All blocks with `Accept(IBlockVisitor visitor) => visitor.Visit(this)` — The `this` type changes from `BlockBase` to `EditorBlock`, which means the `Visit()` overload resolution needs updating. Each `Accept()` changes to pass `this` as the appropriate type.
- All `IBlockSliceRenderer` implementations (unknown count — search `: IBlockSliceRenderer`) — Change parameter type
- `src/Aero.Cms.Shared/Blocks/Rendering/AeroHeroRenderer.cs`, `AeroFeaturesRenderer`, etc. — Change parameter from `AeroHeroBlock` to `EditorBlock`

**Effort:** 3-4 days (may be lower if visitor pipeline is unused)

### 5.3 Phase 2: Flatten PageDocument

Remove `LayoutRegions` from `PageDocument`. The `EditorBlock` list IS the layout — blocks are rendered in order, which is how the editor already displays them.

**Files to modify:**
- `src/Aero.Cms.Core.Entities/PageDocument.cs:22-28` — Remove `LayoutRegions` property, keep `Blocks` only
- `src/Aero.Cms.Modules.Pages/PageContentService.cs:121-131` — Remove `MapEditorBlocksToLayoutRegions` call, just store `Blocks`
- `src/Aero.Cms.Modules.Pages/PageContentService.cs:231-265` — Remove `MapEditorBlocksToLayoutRegions` method entirely
- `src/Aero.Cms.Modules.Headless/Areas/Api/v1/PagesApi.cs:247-264` — `MapToDetail` just returns `Blocks`, no fallback needed

**Effort:** 1 day

### 5.4 Phase 3: Migrate Blog Posts

`BlogPostDocument.Content` currently stores `List<BlockBase>` (`src/Aero.Cms.Core.Entities/BlogPostDocument.cs:21`). Blog posts created by seed data also use old-style blocks.

**Change:** `List<BlockBase>` → `List<EditorBlock>`.

**Files to modify:**
- `src/Aero.Cms.Core.Entities/BlogPostDocument.cs:21` — Change `List<BlockBase> Content` to `List<EditorBlock> Content`
- `src/Aero.Cms.Modules.Blog/BlogPostContentService.cs` — Update any BlockBase references
- `src/Aero.Cms.Modules.Setup/SeedDataService.cs:565-766` — `BuildStarterBlogContent()` — Change `CreatePost()` helper to use `EditorBlock` instead of `HeadingBlock`/`RichTextBlock`/`QuoteBlock`

**Effort:** 1-2 days

### 5.5 Phase 5: Update Marten Block Configurations (BEFORE BlockBase Removal)

Remove polymorphic block registration from Marten configuration. The `BlockMartenConfiguration` class registers `BlockBase` subtypes for polymorphic storage. With `EditorBlock` being a single concrete class (no polymorphism needed), this entire configuration layer disappears.

**⚠ CRITICAL (council finding):** This phase MUST run BEFORE Phase 4 (BlockBase removal). Marten's `AddSubClassHierarchy` maps the `$blockType` discriminator to concrete types. If Phase 4 deletes the hierarchy before Phase 5 updates the config, any existing `BlockBase` data in the database becomes **un-deserializable** — Marten will not know how to map discriminator values to concrete types. Migration scripts must run while `BlockBase` types still exist.

**Files to modify/remove:**
- `src/Aero.Cms.Core/Blocks/BlockMartenConfiguration.cs` — Remove entire file
- `src/Aero.Cms.Abstractions/Blocks/Serialization/BlockJsonContext.cs` — Remove `[JsonSerializable(typeof(List<BlockBase>))]` — no longer needed since EditorBlock doesn't use polymorphic serialization
- `src/Aero.Cms.Web/Program.cs` — Remove `AddCmsBlocks()` / `BlockSliceRegistry` registrations
- `src/Aero.Cms.Abstractions/Blocks/Editing/BlockMetadataProvider.cs` — Remove `DiscoverBlocks()` which scans for `BlockBase` subclasses
- `src/Aero.Cms.Abstractions/Blocks/Editing/BlockEditorMetadata.cs` — Remove or mark deprecated

**Effort:** 1 day

### 5.6 Phase 4: Remove BlockBase Hierarchy (AFTER Phase 5 + Migration)

Remove the entire `BlockBase` abstract class and all 20+ concrete subtypes. This eliminates the `[JsonDerivedType]` polymorphism, the `Accept()` visitor method, and the `IBlockVisitor` interface.

**Files to remove:**
- `src/Aero.Cms.Abstractions/Blocks/BlockBase.cs`
- `src/Aero.Cms.Abstractions/Blocks/ConcreteBlocks.cs` (all 7 types)
- `src/Aero.Cms.Abstractions/Blocks/Common/AeroHeroBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/AeroFeaturesBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/AeroCtaBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/AeroBlogBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/AeroPricingBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/AeroTeamsBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/AeroTestimonialsBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/AeroFaqBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/AeroPortfolioBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/AeroContactBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/AeroTableBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/AeroAuthBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/HtmlBlocks.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/MediaBlocks.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/VideoBlocks.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/TextBlocks.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/ScrollingBlocks.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/LayoutBlocks.cs`
- `src/Aero.Cms.Abstractions/Blocks/Common/FormBlocks.cs`
- `src/Aero.Cms.Abstractions/Blocks/IBlockVisitor.cs`
- `src/Aero.Cms.Abstractions/Blocks/IBlock.cs`
- `src/Aero.Cms.Abstractions/Blocks/IBlockService.cs`
- `src/Aero.Cms.Core/Blocks/MartenBlockService.cs`
- `src/Aero.Cms.Web.Core/Blocks/Rendering/BlockSliceRegistry.cs`
- `src/Aero.Cms.Web.Core/Blocks/Rendering/IBlockSliceRenderer.cs`
- `src/Aero.Cms.Abstractions/Blocks/Serialization/BlockJsonContext.cs`
- `src/Aero.Cms.Abstractions/Blocks/Editing/BlockEditorMetadata.cs`
- `src/Aero.Cms.Abstractions/Blocks/Editing/IBlockMetadataProvider.cs`
- `src/Aero.Cms.Abstractions/Blocks/Editing/BlockMetadataProvider.cs`
- `src/Aero.Cms.Abstractions/Blocks/Editing/BlockEditingService.cs`

**Files to modify (remove BlockBase imports/usage):**
- `src/Aero.Cms.Abstractions/Blocks/Layout/BlockPlacement.cs` — Remove `BlockType` property (no longer needed)
- `src/Aero.Cms.Abstractions/Blocks/Layout/LayoutRegion.cs` — If Phase 2 already removed LayoutRegion, skip
- `src/Aero.Cms.Abstractions/Blocks/Layout/LayoutColumn.cs` — Same as above
- `src/Aero.Cms.Modules.Headless/Areas/Api/v1/BlocksApi.cs` — Remove entire blocks API endpoint
- `src/Aero.Cms.Modules.Pages/PageContentService.cs:267-413` — Remove `MapEditorBlock()` method
- `src/Aero.Cms.Shared/Services/HttpBlockService.cs` — Remove
- `src/Aero.Cms.Core/Blocks/BlockMartenConfiguration.cs` — Remove Marten config for BlockBase
- `src/Aero.Cms.Abstractions/Http/Clients/BlocksHttpClient.cs` — Remove client

**Files to remove (dead UI):**
- `src/Aero.Cms.Shared/Components/BlockEditor.razor`
- `src/Aero.Cms.Shared/Components/BlockPicker.razor`

**Effort:** 2-3 days (lots of file deletions, but each removal is straightforward)

### 5.7 Phase 6: Remove LayoutRegion/Column/Placement Classes

If the rendering pipeline no longer needs the hierarchical layout model, remove these entirely. The editor already renders blocks as a flat, ordered list. If visual hierarchy is needed (e.g., side-by-side columns), that should be encoded in `EditorBlock` properties (as it already is — `EditorBlock.EditorColumns` exists at `EditorBlock.cs:45`).

**Files to remove:**
- `src/Aero.Cms.Abstractions/Blocks/Layout/LayoutRegion.cs`
- `src/Aero.Cms.Abstractions/Blocks/Layout/LayoutColumn.cs`
- `src/Aero.Cms.Abstractions/Blocks/Layout/BlockPlacement.cs`

**Effort:** 0.5 day

### 5.8 Module Audit (Required Before Phase 4/5)

Before removing the `BlockBase` hierarchy, audit ALL modules for direct `BlockBase` references:

```bash
# Check each module
for dir in src/Aero.Cms.Modules.*/; do
  echo "=== $dir ==="
  rg "BlockBase|HeadingBlock|RichTextBlock|CtaBlock|QuoteBlock" "$dir" --type cs -l
done
```

Modules known to reference `BlockBase` based on file inventory:
- `Aero.Cms.Modules.Setup` — Seed data (will be updated in Phase 3)
- `Aero.Cms.Modules.Pages` — `PageContentService.cs` (has `MapEditorBlock` → remove)
- `Aero.Cms.Modules.Blog` — Blog post content (migrated in Phase 3)
- `Aero.Cms.Modules.Headless` — `BlocksApi.cs`, `PagesApi.cs`
- `Aero.Cms.Banners` — Unknown, check
- `Aero.Cms.Modules.ContentCreator` — Unknown, check

**Effort:** 0.5 day for audit

### 5.9 Option C Summary

| Phase | What | Files touched | Effort | Dependencies |
|---|---|---|---|---|
| 0 | Module audit | ~8 module dirs | 0.5 day | None |
| 1 | Renderers consume EditorBlock | ~15 files | 3-4 days | Verify actual render path first |
| 2 | Flatten PageDocument | 3 files | 1 day | Phase 1 |
| 3 | Migrate blog posts | 3 files | 1-2 days | None (can run alongside 1-2) |
| 5 | Update Marten config | 3-4 files | 1 day | **MUST precede Phase 4** |
| 4 | Remove BlockBase hierarchy | ~35 files deleted, ~10 modified | 2-3 days | Phase 5 (config), Phase 3 (blog migration), Phase 0 (audit) |
| 6 | Remove Layout classes | 3 files | 0.5 day | Phase 3 |
| **Total** | | **~60 files changed** | **~4 weeks** (including testing) | See above |

**Phase ordering note (council finding):** Initial plan placed Phase 5 (Marten config) after Phase 4 (BlockBase removal). This is incorrect — Marten's polymorphic deserialization would break on any existing data. Corrected ordering: Phase 5 BEFORE Phase 4. Migration scripts must run while BlockBase types still exist.

**Time estimate note (council finding):** Initial 2-3 week estimate did not account for module audit, testing, or regression verification. With proper coverage: **3-4 weeks realistic.**

---

## 6. Migration Script Strategy

When switching from old to new storage, existing databases will have pages with `LayoutRegions` pointing to `BlockBase` docs but no `EditorBlock` data. The migration script handles this.

### 6.1 Migration Direction: Backfill EditorBlocks from BlockBase

```csharp
// Run once during app startup or as a Wolverine command
public class BackfillEditorBlocksCommand
{
    public async Task ExecuteAsync(IDocumentSession session)
    {
        var pages = await session.Query<PageDocument>()
            .Where(p => p.Blocks.Count == 0 && p.LayoutRegions.Count > 0)
            .ToListAsync();

        foreach (var page in pages)
        {
            var blockIds = page.LayoutRegions
                .SelectMany(r => r.Columns)
                .SelectMany(c => c.Blocks)
                .OrderBy(b => b.Order)
                .Select(b => b.BlockId)
                .ToList();

            var blocks = await session.LoadManyAsync<BlockBase>(blockIds);

            page.Blocks = blocks.Select(b => BlockBaseToEditorBlock(b)).ToList();
        }

        await session.SaveChangesAsync();
    }
}
```

### 6.2 Conversion Function

```csharp
private static EditorBlock BlockBaseToEditorBlock(BlockBase block) => block switch
{
    RichTextBlock r     => new() { Type = "content",   Content = r.Content },
    HeadingBlock h      => new() { Type = "text",      Content = h.Text },
    QuoteBlock q        => new() { Type = "quote",     Content = q.Content, Author = q.Author ?? "" },
    CtaBlock c          => new() { Type = "aero_cta",  MainText = c.Text, CtaText = c.Text, CtaUrl = c.Url },
    AeroHeroBlock ah    => new() { Type = "aero_hero", MainText = ah.Title, SubText = ah.Description, ... },
    // ... 15+ more cases
    _                   => new() { Type = "content",   Content = $"<!-- Unknown block: {block.GetType().Name} -->" }
};
```

### 6.3 When to Run

- **Option B only** (current): Migration not needed — seed data now has `Blocks` populated for new installs. Existing seeded databases need re-seeding.
- **Option C Phase 2** (flatten `PageDocument`): Migration run before `LayoutRegions` column is removed.

---

## 7. Risk Assessment

### 7.1 Option B Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Seed data has typo in EditorBlock mapping | Low | Medium | Build verifies. Manual test: seed → open page → verify blocks render |
| HeadingBlock text too short for "text" type editor | Low | Low | "text" type shows a textarea — content is editable regardless |
| CtaBlock doesn't map cleanly to aero_cta (no AeroLayout in seed) | Low | Low | `aero_cta` renderer defaults to `Card` layout, still renders fine |

### 7.2 Option C Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Missing an `IBlockSliceRenderer` implementation during Phase 1 | Medium | High — render breaks | Comprehensive grep: `rg ": IBlockSliceRenderer" src/ --type cs` before starting Phase 1 |
| Blog post editor still uses old `BlockEditor.razor` | Low | Medium — can't edit blog posts | Check if blog post editor references it. If so, delay Phase 4 until blog editor is migrated |
| Marten polymorphic deserialization breaks if BlockBase types removed but data exists | High (for existing DB) | High — page load fails | Run migration script (Section 6) BEFORE deploying Phase 4 code. The migration converts all BlockBase to EditorBlock while old code can still deserialize BlockBase |
| Third-party modules reference BlockBase | Unknown | High | Audit all modules in `src/Aero.Cms.Modules.*/` for BlockBase references before Phase 4 |
| Render path doesn't use visitor pattern — removing it breaks public page rendering | High | Critical | Trace actual render path BEFORE Phase 1. Search for `Accept()` calls, visitor invocations, and direct renderer references before assuming the visitor pipeline is the live render path |
| Migration leaves orphaned BlockBase data in DB | Medium | Medium (until cleanup) | Run migration script BEFORE Phase 5. Keep backup of `mt_doc_block_base`. Revert migration script should convert editorBlocks back to BlockBase |

### 7.3 Testing Strategy

Option B requires:
- Manual verification: Run setup flow, open each seeded page in editor, verify blocks render
- Build verification: `dotnet build` passes

Option C requires:
- **Unit tests**: `MapEditorBlock()` conversion completeness (every EditorBlock.Type → every BlockBase subtype) — ensure no silent property drops
- **Integration tests**: Seed data → render path → verify all 4 seeded pages produce correct HTML
- **Regression tests**: Create page via editor → save → reload → blocks match
- **Migration script tests**: Backfill EditorBlocks from BlockBase → verify all properties survive round-trip
- **Marten deserialization tests**: `LoadManyAsync<BlockBase>` with mixed subtypes → verify correct concrete types
- **Performance benchmarks**: N+1 vs LoadManyAsync on render path

### 7.4 Rollback Plan

**Option B:** Revert `SeedDataService.cs` — single file diff, trivial rollback.

**Option C:** Keep each phase as a separate merge commit. Any phase can be reverted independently. The migration script (Section 6) converts data to new format — a reverse migration would need to be written for rollback (EditorBlock → BlockBase).

---

## 8. Decision Log

| Date | Decision | Rationale |
|---|---|---|
| 2026-04-27 | Option B selected for immediate fix | Code not in production yet. Seed-only fix is ~40 lines vs ~200+ for full reverse mapper. Unblocks editor usage for seeded pages. |
| 2026-04-27 | Option C deferred | 2-3 week refactor not appropriate for a tactical bug fix. Will revisit as a dedicated sprint. |
| 2026-04-27 | Write this document | Capture architectural analysis, comparison data (Umbraco/Orchard), diagrams, and phased migration plan for future reference. |

---

## Appendices

### A. Key Source Locations

| Component | Path | Key lines |
|---|---|---|
| PageDocument entity | `src/Aero.Cms.Core.Entities/PageDocument.cs` | 22-28 (dual storage) |
| EditorBlock class | `src/Aero.Cms.Abstractions/Blocks/EditorBlock.cs` | 1-98 |
| BlockBase class | `src/Aero.Cms.Abstractions/Blocks/BlockBase.cs` | 1-69 |
| Seed data service | `src/Aero.Cms.Modules.Setup/SeedDataService.cs` | 287-563 (4 page builders) |
| Editor save method | `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor.cs` | 783-854 |
| API MapToDetail | `src/Aero.Cms.Modules.Headless/Areas/Api/v1/PagesApi.cs` | 247-264 |
| Converter (EditorBlock→BlockBase) | `src/Aero.Cms.Modules.Pages/PageContentService.cs` | 231-413 |
| Block service | `src/Aero.Cms.Core/Blocks/MartenBlockService.cs` | 1-29 |
| Render pipeline | `src/Aero.Cms.Web.Core/Blocks/Rendering/BlockSliceRegistry.cs` | 1-108 |
| Old (unused) BlockEditor | `src/Aero.Cms.Shared/Components/BlockEditor.razor` | 1-499 |
| Blog post (also uses BlockBase) | `src/Aero.Cms.Core.Entities/BlogPostDocument.cs` | 21 |

### B. Comparison File: Umbraco vs Orchard vs AeroCMS

For full context, the comparison table in Section 4.2 is the condensed version.

**Council correction on comparison accuracy:** The initial comparison overstated the simplicity of Umbraco/Orchard Core models:

1. **Umbraco v13+** stores block data in a separate `cmsContent` table with a JSON `data` column — blocks are their own content nodes with separate rows, not truly "inline." The "single model" claim conflates document JSON representation with actual denormalized storage.

2. **Orchard Core** also has a form of dual representation: `ContentItem` stores JSON `Data`, but on render the `ContentManager` hydrates into in-memory `ContentPart` objects. There's one storage row but conceptually two models (stored JSON + hydrated runtime objects).

3. **Hierarchy loss**: The current `LayoutRegions` → `LayoutColumn` → `BlockPlacement` model provides multi-region, multi-column layout. A flat `List<EditorBlock>` loses this structure. For example, a two-column layout is encoded in `PageEditor` as `EditorBlock.EditorColumns` — this would need to be preserved when flattening.

**Corrected takeaway:** Both systems prove that a **single storage model** works at scale. AeroCMS should converge to a single storage model as well — but the comparison doesn't imply the feature gap is zero. The hierarchical layout (regions, columns) that `LayoutRegions` provides must be preserved in the converged model, not lost.
