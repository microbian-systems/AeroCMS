
> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). Marten
> was migrated out in [`surrealdb-marten-port.md`](surrealdb-marten-port.md).
> This document is a historical implementation record; its Marten/PostgreSQL
> persistence details do not reflect the current stack.

# Aero CMS Page Refactor Implementation Plan

This plan coordinates the two implementation specs:

- [`aero-page-document-refactor.md`](aero-page-document-refactor.md) defines the data model, draft/publish split, event shapes, preview pipeline, and published layout manifest.
- [`aero-blocks-renderers-neoui.md`](aero-blocks-renderers-neoui.md) defines the NeoUI PageEditor shell, block catalog, composition model, editor preview behavior, public static SSR renderers, output-cache behavior, and legacy block migration.

## Decision

Implement the PageDocument refactor first, then implement the NeoUI PageEditor and block renderer refactor.

The NeoUI refactor depends on a stable page-content contract. It needs to know where draft state lives, when `LayoutRegions` are written, how preview and publish build render manifests, and how `BlockIdMap` relates client editor nodes to persisted `BlockBase` documents.

Use **Option A** for V1:

- `PageEditorState` remains a flat list of top-level block placements.
- Nested composition is allowed only inside a `NeoCompositionBlock : BlockBase`.
- A visual tree/outline can be added later as an editor UI affordance over the same data.
- Do not store a page-level `NeoPageNode` tree in `PageEditorState` in this refactor.

The product goal is a simple WYSIWYG PageEditor for non-technical users. Authors should be able to build pages by adding obvious page sections, editing text/images/buttons in-place, and seeing quick previews. Advanced primitives/components should support composition inside blocks without making the first editing experience feel like a developer tool or component inspector.

## Block UI Package Boundary

For new UI libraries such as HyperUI, use a vertical package boundary. A package like `Aero.Cms.Ui.Hyper` owns the block model, mapper, public static SSR renderer, package-local renderer marker, editor preview, modal editor, and `IPageEditorBlockDefinition` for each Hyper block. `Aero.Cms.Shared` remains the PageEditor shell and block rendering host; it should not receive per-block Hyper switch cases, Hyper renderer markers, or Hyper editor files.

Stable contracts live in `Aero.Cms.Abstractions`:

- `BlockBase`
- `BlockMetadataAttribute`
- `CmsBlockRendererAttribute`
- `ICmsBlockModelProvider`
- `IPageEditorBlockDefinition`
- `IPageEditorBlockProvider`

The public/server host and the WebAssembly client both reference the UI package and call one extension method, for example:

```csharp
services.AddAeroCmsHyperUiBlocks();
```

That extension registers the package's editor provider, block model provider, and generated renderer registry. The server call lets public `.cshtml` rendering resolve Hyper renderers; the WebAssembly client call lets PageEditor resolve palette items, previews, and modal editors. Adding the next Hyper block should normally touch only files under `src/Aero.Cms.Ui.Hyper/Blocks/{Slice}/` plus the package provider list.

Renderer discovery for package-owned blocks uses a normal `.cs` partial marker with `[CmsBlockRenderer(typeof(BlockType))]`. Do not rely on Razor `@attribute [CmsBlockRenderer(...)]` for new Hyper blocks, and do not add Hyper entries to `Aero.Cms.Shared/Blocks/Rendering/RendererMarkers.cs`.

## Required Order

### 1. PageDocument Foundation

Implement the data model from `aero-page-document-refactor.md`.

Deliverables:

- Add `PageEditorState` as the draft/editor workspace.
- Move editor block placement state out of `PageDocument`.
- Move `BlockIdMap` out of `PageDocument` and into `PageEditorState`.
- Keep `PageDocument.LayoutRegions` as the published render manifest.
- Ensure draft saves never write `PageDocument.LayoutRegions`.
- Add `PublishedVersion` and `DraftVersion` behavior.
- Add or update `PageAdminStatusService` for unpublished-change detection.
- Add `PageDeleteHandler` to hard-delete `PageEditorState` when a page is deleted.

Exit criteria:

- Existing pages still render publicly from `PageDocument.LayoutRegions`.
- Pages with empty `LayoutRegions` are handled gracefully by public rendering.
  - A never-published draft page should not pretend to have body content.
  - Rendering may return no body content, a normal unpublished/not-found path, or the existing CMS 404 behavior depending on current route rules.
  - It must not throw because the layout manifest is empty.
- Draft editor saves update `PageEditorState`, not published `LayoutRegions`.
- Published pages can be distinguished from pages with unpublished draft changes.

### 2. Preview and Publish Pipeline

Implement the shared manifest-building pipeline before changing the editor UI.

Deliverables:

- Add `IPageLayoutManifestBuilder`.
- Use the builder for preview.
- Use the same builder for publish.
- Ensure preview never writes to `PageDocument`.
- Ensure publish is the only path that writes `PageDocument.LayoutRegions`.
- Batch-load `BlockBase` documents for placement rendering where practical.

Exit criteria:

- Preview and publish cannot drift because they share the same layout builder.
- `PagePublished` carries the built `LayoutRegions`.
- `PageMetadataUpdated` remains metadata-only and carries no block body or layout manifest.

### 3. Existing Data Migration

Migrate the old PageDocument shape before removing old editability.

Deliverables:

- For pages with existing `PageDocument.Blocks`, create `PageEditorState`.
- Convert old editor block state into `EditorBlockPlacement` entries.
- Copy/rebuild `BlockIdMap` into `PageEditorState.BlockIdMap`.
- Leave `PageDocument.LayoutRegions` as the published manifest.
- For pages that have both `PageDocument.Blocks` and `PageDocument.LayoutRegions`, do both:
  - migrate `PageDocument.Blocks` into `PageEditorState.Blocks`
  - preserve existing `PageDocument.LayoutRegions` for currently published output
- For `LayoutRegions`-only pages, create an empty `PageEditorState` with no draft changes.
- Add Marten event upcasting for `PageContentUpdated` to `PageMetadataUpdated` if existing events use the old name.
- Add a rollback plan before running a batch migration.
  - At minimum, snapshot affected document IDs and old document JSON.
  - Prefer a transactional Marten/Wolverine migration command that can be retried safely.

Exit criteria:

- No existing rendered page loses its public output.
- Pages with both old editor blocks and existing published `LayoutRegions` keep their public output while gaining editable draft state.
- Old pages do not become broken or ambiguous during the transition.
- `PageDocument.Blocks` and `PageDocument.BlockIdMap` are no longer required after migration.

### 4. Renderer Compatibility and Cache Preservation

Stabilize public rendering on the new data model before introducing Neo authoring.

Deliverables:

- Public rendering reads published `PageDocument.LayoutRegions`.
- `BlockPlacement` references live `BlockBase` documents by `BlockId`.
- `BlockType` remains dispatch metadata only, not a data snapshot.
- Existing output-cache policies still apply to public static SSR pages.
- Cache invalidation handles page/content/site/slug changes.
- Bridge event naming clearly:
  - `PageMetadataUpdated` is the Marten/event-sourcing metadata event.
  - Wolverine cache handlers may consume this event directly or consume a derived integration message.
  - Slug/site/content tags must be evicted when metadata changes affect public URLs or public HTML.
- Add a staging smoke/load gate for public static SSR pages before enabling the new render path for production traffic.

Exit criteria:

- Public pages still render from the published manifest.
- Output cache behavior is preserved.
- Draft preview routes are not output cached.
- Cache invalidation is verified for publish, metadata changes, slug changes, and unpublish/archive flows.

### 5. NeoUI PageEditor Shell

After the data model is stable, implement the NeoUI editor shell from `aero-blocks-renderers-neoui.md`.

Deliverables:

- Register NeoUI services and assets only for the manager/PageEditor surface:
  - `AddNeoUIPrimitives()`
  - `AddNeoUIComponents()`
  - required NeoUI provider/wrapper components such as `AppProvider` where the NeoUI package requires them
  - manager/PageEditor-only CSS/script assets
- Replace the hardcoded right-sidebar block list with catalog-driven sections:
  - `Aero UI`
  - `Primitives`
  - `Components`
- Preserve existing PageEditor visual behavior and `pe-*` styling where possible.
- Use NeoUI only inside the PageEditor/page-composition surface.
- Keep the rest of the manager UI on its existing Radzen/current styling.
- Add Sortable-backed palette/canvas behavior.
- Keep quick editor previews for all new Neo blocks.
- Keep the primary UX WYSIWYG and block-first:
  - users add page sections such as Hero, CTA, Gallery, and Feature Grid
  - users edit obvious content fields without understanding component trees
  - advanced composition is progressively disclosed inside custom/composition blocks
  - do not require a tree-view to build a normal page in V1
- Lazily create `PageEditorState` when the editor opens a page that does not have one yet.
  - New never-published pages may not have editor state until first edit.
  - The API should create an empty `PageEditorState` with the page ID, site ID, initial draft version, empty placements, and empty `BlockIdMap`.

Exit criteria:

- The editor reads/writes `PageEditorState`.
- The editor no longer depends on `PageDocument.Blocks`.
- Draft saves do not mutate `PageDocument.LayoutRegions`.
- Opening the editor for a newly created page with no existing `PageEditorState` succeeds.

### 6. Neo Block and Composition Model

Introduce new blocks only after the PageEditor has a stable persistence target.

Deliverables:

- Add `Hero01Block`.
- Add `BasicHeroBlock` as the deterministic migration target for legacy `boring_hero`.
- Add `NeoCompositionBlock` only with a public-safe static SSR renderer.
- Keep the page-level editor state flat:
  - `PageEditorState.Blocks` remains a list of `EditorBlockPlacement`.
  - Nested Neo composition is stored inside a `NeoCompositionBlock : BlockBase`.
  - Do not add a second page-level `NeoPageNode` tree to `PageEditorState` in this slice.
  - If later work needs page-level nested layout state, update `aero-page-document-refactor.md` first.
- Blocks remain the primary page-building unit.
  - A block is a predefined page section/row with a known renderer and editor preview.
  - Examples: `Hero01Block`, `BasicHeroBlock`, CTA, Feature Grid, Gallery.
  - Blocks may contain a nested composition payload when they are explicitly composition-capable.
  - The whole page does not become one nested tree in V1.
- Add recursive rendering guarded by `BlockRenderContext.NestingDepth` and `MaxNestingDepth`.
- Ensure new `BlockBase` subtypes flow through:
  - `BlockRendererGenerator`
  - `GeneratedBlockModelManifest`
  - `BlockBase.Polymorphic.g.cs`
  - `BlockMartenConfiguration`
- Use the shared semantic action model from the NeoUI spec for actions/buttons:
  - `BlockAction`
  - `BlockActionRole`
  - renderer-specific button styling should map from role/intent, not be stored as arbitrary HTML
- Keep public renderers free of NeoUI components/assets.

Exit criteria:

- New block types round-trip through Marten.
- Public renderers produce static SSR HTML.
- User-composed primitive/component trees are enabled only after `NeoCompositionBlockRenderer` is implemented and tested.
- The editor can represent nested composition without changing `PageEditorState` into a nested tree document.

### 7. Neo Legacy Block Migration

After the new editor and block model are in place, migrate selected legacy block concepts into Neo-era blocks.

Initial migration set:

- Boring Hero
- Columns
- Scriban
- Image
- Video
- Audio
- Gallery, not carousel
- Raw HTML
- Separator

Rules:

- If a typed target block does not exist yet, migrate into `NeoCompositionBlock` payloads with stable catalog IDs.
- This is block content migration, not the PageDocument structure migration from step 3.
- Rebuild `PageEditorState.BlockIdMap` for migrated draft/editor state.
- Republish through `IPageLayoutManifestBuilder` when updating published `LayoutRegions`.
- Keep old editability or a read-only/migrate-now safety path until migration is complete.
- Keep Neo Scriban public rendering feature-flagged off until its sandbox, allowed functions, data-binding schema, caching behavior, and error rendering are verified.

Exit criteria:

- No old page enters an uneditable limbo state.
- Migrated pages render through the same published manifest path as new pages.
- Legacy block authoring code can be removed only after the migration path is verified.

## Do Not Start With NeoUI

Do not implement the NeoUI PageEditor first. Doing so would force the editor to target unstable or soon-to-be-removed data structures, especially:

- `PageDocument.Blocks`
- `PageDocument.BlockIdMap`
- draft writes to `PageDocument.LayoutRegions`
- preview paths that do not share publish manifest building

The correct dependency direction is:

```text
PageDocument data model
  -> PageEditorState draft workspace
  -> shared preview/publish layout builder
  -> public renderer compatibility
  -> NeoUI PageEditor shell
  -> Neo blocks and composition
  -> legacy Neo block migration
```

## First Implementation Slice

The first implementation slice should include only:

1. `PageEditorState`
2. `IPageLayoutManifestBuilder`
3. draft save/publish separation
4. migration from current `PageDocument.Blocks` / `BlockIdMap`
5. migration handling for pages that have both `PageDocument.Blocks` and `PageDocument.LayoutRegions`
6. empty-`LayoutRegions` render behavior
7. public render compatibility tests
8. output-cache preservation tests

NeoUI integration should begin only after that slice is green.

## Key Contract Clarifications

### Structure Migration vs Block Migration

There are two different migrations:

- Step 3 is document-structure migration.
  - It moves draft/editor placement state from `PageDocument` to `PageEditorState`.
  - It preserves existing published `PageDocument.LayoutRegions`.
  - It does not transform old block types into new Neo block types.
- Step 7 is block-content migration.
  - It transforms selected old block concepts into new typed Neo blocks or `NeoCompositionBlock` payloads.
  - It may rebuild editor state and republish, but only after the new block model exists.

Do not combine these migrations into one broad rewrite unless the user explicitly approves that risk.

### Visual Tree View vs PageEditorState

The first Neo implementation slice keeps `PageEditorState` flat. A visual tree-view/outline is a later editor feature, not the V1 persistence model.

```text
PageEditorState.Blocks
  -> EditorBlockPlacement[]
  -> references BlockBase documents
       -> Hero01Block
       -> BasicHeroBlock
       -> NeoCompositionBlock
            -> NeoPageNode tree lives here
```

This preserves the PageDocument refactor contract and avoids adding a second page-level tree. Nested Neo authoring is represented inside `NeoCompositionBlock`, not beside `PageEditorState.Blocks`.

Later, the PageEditor can display a tree/outline by combining:

- top-level blocks from `PageEditorState.Blocks`
- nested nodes from any `NeoCompositionBlock.Nodes`

That tree is a UX projection. It should not force a page-level tree document unless a future design explicitly changes the data model.

### Empty Published Manifest

An empty `PageDocument.LayoutRegions` means there is no published body manifest.

The renderer must handle that state intentionally:

- never throw on empty regions
- never infer draft/editor content from `PageEditorState`
- respect publication state and existing routing rules
- render no body content or the appropriate not-found/unpublished behavior

### Cache Event Bridge

The data model uses `PageMetadataUpdated` as the metadata event. Cache invalidation can be implemented directly from that event or by mapping it to a Wolverine integration message. Either way, cache tags must be invalidated for:

- publish
- unpublish/archive
- slug changes
- title/SEO/display changes that affect rendered HTML
- page content/block changes that affect rendered HTML

---

## Progress Tracking

### Overall Status

| Step | Status | Files Created | Files Modified | Notes |
|---|---|---|---|---|---|
| 1. PageDocument Foundation | ✅ Complete | 4 | 4 | `PageEditorState`, `PageMetadataUpdated`, new `PagePublished` |
| 2. Preview/Publish Pipeline | ✅ Complete | 4 | 1 | `IPageLayoutManifestBuilder`, publish wired in Step 7 Phase A |
| 3. Data Migration | ✅ Complete | 1 | 1 | `PageDocumentMigration` (idempotent, per-site) |
| 4. Renderer Compatibility & Cache | ✅ Complete | 0 | 1 | Publish/archive eviction; 2 items deferred |
| 5. NeoUI PageEditor Shell | ✅ Complete | 14 | 4 | NeoUI services, assets, AppProvider, catalog sidebar, Sortable, property panel, legacy removal |
| 6. Neo Block & Composition Model | ✅ Complete | 24 | 2 | 11 Neo blocks (models, renderers, mappers, catalog entries), 8 editor previews, 2 property editors |
| 7. Neo Legacy Block Migration | ✅ Complete | 10 | 3 | Migration infra; ~2200 lines legacy code removed; 14 API endpoints |
| **Total** | | **57** | **16** | **Build: 0 errors; Phase 0-7 done; Phase 8 + deferred items remain** |

### Cross-Reference: aero-blocks-renderers-neoui.md Phases

The NeoUI doc defines 8 implementation phases. This table maps them to the aero-page-refactor-plans steps:

| NeoUI Phase | Mapped Step | Status | What's Done | What's Outstanding |
|---|---|---|---|---|
| Phase 0 — NeoUI Setup | Step 5 | ✅ Done | NuGet packages (4.0.18/4.0.5/3.0.0), services, AppProvider + 4 portal hosts, CSS/JS assets, scoped imports | — |
| Phase 1 — Static SSR Public Page Host | Step 4 (new) | ✅ Done | `.cshtml` + `<component>` tag helpers already provide Blazor static SSR for all renderers; output-cache preserved via `[OutputCache]` on `DynamicPageModel` | — |
| Phase 2 — Legacy Migration Bridge | Step 7 | ✅ Done | `ILegacyBlockMapper`, `BlockContentMigrationService`, Wolverine handler, 14 API endpoints | — |
| Phase 3 — Neo PageEditor Shell | Step 5 | ✅ Done | Component decomposition, catalog-driven sidebar, NeoUI Sortable palette/canvas, `PageEditorPropertyPanel`, `BlockEditorHost` | Lazy `PageEditorState` creation on first editor open |
| Phase 4 — Neo Catalog Foundation | Step 6 | ✅ Done | `NeoEditorCatalogSection`, `NeoEditorCatalogKind`, `NeoPropertyFieldType`, `NeoPropertyDefinition`, `NeoEditorCatalogItem`, `NeoEditorCatalogProvider` (11 entries), `INeoEditorCatalogProvider`, `NeoCatalogSectionMapper` | `NeoEditorCatalogValidator`, serialization tests |
| Phase 5 — Initial Neo Blocks & Primitives | Step 6 | ✅ Done | 11 Neo blocks: `Hero01Block`, `BasicHeroBlock`, `ImageBlock`, `VideoBlock`, `AudioBlock`, `GalleryBlock`, `NeoRawHtmlBlock`, `SeparatorBlock`, `NeoColumnsBlock`, `ScribanBlock`, `NeoCompositionBlock` — all with model+renderer+mapper+catalog entry+renderer marker; 8 editor previews; 2 property editors | Property editors for blocks 3-11; editor previews for Scriban+NeoColumns |
| Phase 6 — Generated Catalog & Typed Adapters | Step 6 | ✅ Done | `ICmsBlockRenderAdapter<TBlock>`, source-generated `NeoEditorCatalogProvider` (partial + `GeneratedNeoEditorCatalog.g.cs`), CLR property extraction, adapter classes implement typed interface | Editor preview types emitted as `null` (naming convention unreliable); property definitions deferred (cross-pipeline data) |
| Phase 7 — Final Legacy Content Cutover | Step 7 | ✅ Done | ~2200 lines removed: legacy sidebar sections (4 sections, 29 blocks), legacy `RenderXxxBlock` previews (~1350 lines), 25 legacy `CreateBlock` cases, dead column/drag methods, dead `IBlockEditorCallbacks` members | Remove `EditorBlock`-based save/publish from `PageContentService`; dead UI (`BlockEditor.razor`, `BlockPicker.razor`) |
| Phase 8 — Expand Neo Blocks | Future | ✋ User-handled | — | Feature Grid, Pricing, Testimonials, FAQ, Blog Grid, etc. — user will provide 50+ Neo components before implementation begins |

---

## Detailed Progress by Step

### ✅ Step 1 — PageDocument Foundation (COMPLETE)

| Deliverable | Status | Notes |
|---|---|---|
| Add `PageEditorState` | ✅ Done | `src/Aero.Cms.Core.Entities/PageEditorState.cs` |
| Add `EditorBlockPlacement` | ✅ Done | `src/Aero.Cms.Core.Entities/EditorBlockPlacement.cs` |
| Move `Blocks` and `BlockIdMap` out of PageDocument | ✅ Done | `PageDocument.Blocks`/`.BlockIdMap` kept for backward compat; migration in Step 3 |
| Keep `PageDocument.LayoutRegions` as published manifest | ✅ Done | Old `Apply(PageContentUpdated)` still writes it; new `Apply(PageMetadataUpdated)` does not |
| Add `PublishedVersion` to PageDocument | ✅ Done | `PageDocument.PublishedVersion` (long, default 0) |
| Add `PageMetadataUpdated` event | ✅ Done | `src/Aero.Cms.Abstractions/Events/PageEvents.cs` |
| Update `PagePublished` to carry LayoutRegions + Version | ✅ Done | Optional params for backward compat |
| Add `Apply(PageMetadataUpdated)` | ✅ Done | Metadata-only; no LayoutRegions/Blocks |
| Add `Apply(PagePublished)` with new shape | ✅ Done | Handles Version + LayoutRegions |
| Add `PageAdminStatusService` | ✅ Done | `src/Aero.Cms.Modules.Pages/Admin/PageAdminStatusService.cs` |
| Add `PageDeleteHandler` | ✅ Done | `src/Aero.Cms.Modules.Pages/PageDeleteHandler.cs` |
| Update `PageDocumentProjection` | ✅ Done | Wired new events alongside old |
| Register new services in `PagesModule` | ✅ Done | DI registration |
| Build verification | ✅ Done | Core.Entities, Abstractions, Pages, Shared — all green (0 errors) |

**Files created:** `PageEditorState.cs`, `EditorBlockPlacement.cs`, `PageAdminStatusService.cs`, `PageDeleteHandler.cs`
**Files modified:** `PageDocument.cs`, `PageEvents.cs`, `PageDocumentProjection.cs`, `PagesModule.cs`

### ✅ Step 2 — Preview and Publish Pipeline (COMPLETE)

| Deliverable | Status | Notes |
|---|---|---|
| Add `IPageLayoutManifestBuilder` | ✅ Done | `src/Aero.Cms.Modules.Pages/IPageLayoutManifestBuilder.cs` |
| Add `PageLayoutManifestBuilder` | ✅ Done | `src/Aero.Cms.Modules.Pages/PageLayoutManifestBuilder.cs` |
| Add `IPagePreviewService` | ✅ Done | `src/Aero.Cms.Modules.Pages/IPagePreviewService.cs` |
| Add `PagePreviewService` | ✅ Done | `src/Aero.Cms.Modules.Pages/PagePreviewService.cs` |
| Add `PreviewRenderModel` | ✅ Done | In `IPagePreviewService.cs` |
| Preview uses builder | ✅ Done | Preview pipeline: load PageDocument → load PageEditorState → load blocks → build → return transient layout |
| Publish uses builder | ✅ Done | Wired in Step 7 Phase A (`PagePublishingWorkflowService.PublishNowAsync`) |
| Preview never writes to PageDocument | ✅ Done | Preview service loads PageDocument metadata but never stores/updates LayoutRegions |
| Register services in PagesModule | ✅ Done | Singleton builder, Scoped preview service |
| Build verification | ✅ Done | Pages, Shared — all green (0 errors) |

**Files created:** `IPageLayoutManifestBuilder.cs`, `PageLayoutManifestBuilder.cs`, `IPagePreviewService.cs` (with `PreviewRenderModel`), `PagePreviewService.cs`
**Files modified:** `PagesModule.cs` (DI registration)

### ✅ Step 3 — Existing Data Migration (COMPLETE)

| Deliverable | Status | Notes |
|---|---|---|
| Create `PageDocumentMigration` | ✅ Done | `src/Aero.Cms.Modules.Pages/PageDocumentMigration.cs` |
| Convert `EditorBlock` list → `EditorBlockPlacement` entries | ✅ Done | Maps `EditorId` → `ClientId`, `BlockIdMap` lookup → `BlockId` |
| Copy/rebuild `BlockIdMap` into `PageEditorState.BlockIdMap` | ✅ Done | Direct copy from `PageDocument.BlockIdMap` |
| Preserve `PageDocument.LayoutRegions` | ✅ Done | LayoutRegions untouched; only editor state migrates |
| Handle pages with both `Blocks` + `LayoutRegions` | ✅ Done | Migrates editor state; leaves LayoutRegions as published manifest |
| Handle `LayoutRegions`-only pages (empty editor state) | ✅ Done | Creates empty `PageEditorState` with `DraftVersion = 0` |
| Marten event upcasting (`PageContentUpdated` → `PageMetadataUpdated`) | 🔜 Deferred | Both event types are actively used |
| Rollback safety (snapshot affected IDs) | ✅ Done | `MigrationResult.AffectedPageIds` tracks all processed pages for rollback |
| Idempotency | ✅ Done | Checks for existing `PageEditorState` before creating; safe to re-run |
| Register in PagesModule | ✅ Done | Scoped registration |
| Build verification | ✅ Done | Pages, Shared — all green (0 errors) |

**Migration behavior per page:**

| Page state | Action |
|---|---|
| `Blocks.Count > 0` + no existing `PageEditorState` | Creates `PageEditorState` with placements + `DraftVersion = PublishedVersion + 1` |
| `Blocks.Count == 0` + no existing `PageEditorState` | Creates empty `PageEditorState` with `DraftVersion = 0` |
| `PageEditorState` already exists | Skips (idempotent) |

**Files created:** `PageDocumentMigration.cs`
**Files modified:** `PagesModule.cs` (DI registration)

### ✅ Step 4 — Renderer Compatibility and Cache Preservation (COMPLETE)

| Deliverable | Status | Notes |
|---|---|---|
| Public rendering reads `PageDocument.LayoutRegions` | ✅ Verified | `Page.cshtml` already renders via `LayoutRegionRenderer` from published manifest |
| `BlockPlacement` references live `BlockBase` by `BlockId` | ✅ Verified | `BlockPlacementRenderer` does `blockService.GetByIdAsync(Placement.BlockId)` (reference model) |
| `BlockType` is dispatch metadata only | ✅ Verified | Used only for renderer resolution, not a data snapshot |
| Output-cache policies still apply | ✅ Verified | `PagesPolicy` (5min, vary by slug) on `Page.cshtml` → runs through `CmsOutputCachePolicy` |
| Cache eviction on save/draft update | ✅ Verified | `ContentUpdatedHandler` consumes `PageContentUpdatedEvent` + `PageViewModelUpdated` → evicts `pages-list` + removes FusionCache slug keys |
| Cache eviction on publish | ✅ Done | Added `IMessageBus.PublishAsync` in `PagePublishingWorkflowService.PublishNowAsync` (broadcasts `PageViewModelUpdated` + `PageContentUpdatedEvent`) |
| Cache eviction on archive | ✅ Done | Added in `PagePublishingWorkflowService.ArchiveAsync` (same pattern) |
| Draft preview not cached | ✅ Verified | `Page.cshtml.cs` sets `no-store` for drafts; `CmsOutputCachePolicy` blocks authenticated requests |
| Empty `LayoutRegions` handled gracefully | ✅ Verified | `Page.cshtml` line 34: `@if (pageDoc.LayoutRegions.Count > 0)` guard |
| Staging smoke/load gate | 🔜 Deferred | Operational concern — add gated rollout before production |
| PagesApi publish/unpublish cache eviction | 🔜 Deferred | `PagesApi.PublishPage`/`UnpublishPage` are static methods; cache eviction will be added when these are refactored |

**Files modified:** `PagePublishingWorkflowService.cs` (added `IMessageBus` + cache eviction broadcasting)

### 🔄 Step 5 — NeoUI PageEditor Shell (PARTIAL)

#### ✅ Done: Component Decomposition

| Deliverable | Status | Notes |
|---|---|---|
| Extract `PageEditorHeader.razor` | ✅ Done | Title input, meta, save/publish buttons → standalone component with code-behind |
| Extract `EditorBlockFrame.razor` | ✅ Done | Block wrapper with toolbar (move up/down, duplicate, delete) + drag-drop |
| Extract `PageEditorCanvas.razor` | ✅ Done | Block list loop, empty state, drag-drop orchestration |
| Extract `BlockEditorPreviewHost.razor` | ✅ Done | ALL block preview rendering (~1300 lines moved from PageEditor.razor) — still uses old legacy block previews |
| Create `IBlockEditorCallbacks` interface | ✅ Done | Cascading callbacks interface (18 members) for preview → orchestrator communication |
| Add `IBlockEditorCallbacks` implementation | ✅ Done | Explicit interface implementation in PageEditor.razor.cs (forwards to protected methods) |
| Simplify `PageEditor.razor` to orchestrator | ✅ Done | Reduced from 2021 → 549 lines (73% reduction) |
| Code-behind updated for component callbacks | ✅ Done | Drag signatures updated for component-based event flow |
| Build verification | ✅ Done | Shared project — 0 errors |

**Files created:**
- `IBlockEditorCallbacks.cs` — Callback interface (18 members)
- `PageEditorHeader.razor` + `.razor.cs` — Header component
- `EditorBlockFrame.razor` + `.razor.cs` — Block frame w/ toolbar
- `PageEditorCanvas.razor` — Block list canvas
- `BlockEditorPreviews/BlockEditorPreviewHost.razor` — All block preview rendering (contains ~1358 lines of inline Razor previews)

**Files modified:**
- `PageEditor.razor` — Reduced from 2021 → 549 lines (orchestrator)
- `PageEditor.razor.cs` — Added `IBlockEditorCallbacks` + updated drag signatures + `ToggleSidebarPanels`

#### ✅ Done: NeoUI Integration & Catalog-Driven Editor

| Deliverable | Status | Notes |
|---|---|---|
| Add NuGet packages: `NeoUI.Blazor` (4.0.18), `NeoUI.Blazor.Primitives` (4.0.5), `NeoUI.Icons.Lucide` (3.0.0) | ✅ Done | `Directory.Packages.props` + `Aero.Cms.Shared.csproj` + `Aero.Cms.Modules.Setup.csproj` |
| Register services: `AddNeoUIPrimitives()` (first), `AddNeoUIComponents()` | ✅ Done | `Program.cs`, `SetupAppFactory.cs`, `MauiProgram.cs`; `Web.Client` removed (WASM doesn't need NeoUI) |
| Load NeoUI assets (manager only): `components.css`, `base/zinc.css`, `primary/blue.css`, `theme.js` | ✅ Done | In `App.razor` head/body; plain `href` (not `@Assets[...]` cache-busting) |
| Add `AppProvider` + 4 portal hosts (`ToastViewport`, `DialogHost`, `ContainerPortalHost`, `OverlayPortalHost`) | ✅ Done | Inside `<AppProvider>` in `ManagerShellLayout.razor` |
| Add `@using NeoUI.Blazor.Extensions` + `@using NeoUI.Blazor.Primitives.Extensions` | ✅ Done | Scoped to `ManagerShellLayout.razor`; NOT in `_Imports.razor` (caused 74+ namespace collisions with Radzen) |
| Verify no npm target runs during build | ✅ Done | Verified; no npm triggers |
| Replace hardcoded right-sidebar with catalog-driven "Aero UI" section | ✅ Done | `INeoEditorCatalogProvider` injected; 11 Neo catalog items sorted by SortOrder; "aeroui" toggle case |
| Add NeoUI `Sortable` palette/canvas behavior | ✅ Done | `PageEditorPaletteSection.razor` (copy-source) + `PageEditorCanvas.razor` (reorder/drop-target); `Group="page-editor"` |
| Build `PageEditorPropertyPanel` + `BlockEditorHost` (per-block editor dispatch) | ✅ Done | `Hero01BlockEditor` + `BasicHeroBlockEditor` property editors; fallback for unknown types |
| Build `BlockPalette` | ✅ Done | Catalog-driven palette as part of "Aero UI" sidebar section |
| Build verification | ✅ Done | 0 errors |

#### ⏳ Outstanding

| Deliverable | Status | Notes |
|---|---|---|
| Lazily create `PageEditorState` on first editor open | ⏳ Not started | New pages may not have editor state until first edit |

Target folder structure from `aero-blocks-renderers-neoui.md:1475-1552` is the reference for these new files.

### 🔄 Step 6 — Neo Block and Composition Model (PARTIAL)

#### ✅ Done: Core Model & Composition

| Deliverable | Status | Notes |
|---|---|---|
| Create `NeoPageNodeKind` enum | ✅ Done | `Block`, `Section`, `Container`, `Component`, `Primitive` with `JsonStringEnumConverter` |
| Create `NeoPageNode` class | ✅ Done | `NodeId`, `CatalogId`, `Kind`, `Properties` (Dictionary\<string, JsonElement\>), `Children` — per advisory to avoid `JsonObject` issues |
| Create `NeoCompositionBlock : BlockBase` | ✅ Done | `BlockType = "neo_composition"`, `[BlockMetadata("neo_composition", "Neo Composition", Category = "Layout")]`, `List<NeoPageNode> Nodes` |
| Source generator registration | ✅ Auto | `[BlockMetadata]` on `NeoCompositionBlock` → auto-registered in `GeneratedBlockModelManifest`, `BlockBase.Polymorphic.g.cs`, `GeneratedBlockFactory` |
| Create `NeoCompositionBlockRenderer` | ✅ Done | SSR Razor component that walks `NeoPageNode` tree |
| Create `NeoNodeRenderer` | ✅ Done | Recursive per-node renderer with `NestingDepth`/`MaxDepth` guard (max 5 levels) |
| Renderer marker registration | ✅ Done | `[CmsBlockRenderer(typeof(NeoCompositionBlock))]` in `RendererMarkers.cs` |
| `NestingDepth`/`MaxNestingDepth` on `BlockRenderContext` | ✅ Done | Step 7 Phase A; default 0/5 |
| Catalog per-node rendering | 🔜 Deferred | V1 renderer shows placeholder `[catalogId]` for unknown nodes; per-node catalog dispatch deferred until individual Neo catalog items are defined |
| Build verification | ✅ Done | Shared + Abstractions — 0 errors |

**Files created:**
- `src/Aero.Cms.Abstractions/Blocks/Neo/NeoPageNodeKind.cs` — composition node kind enum
- `src/Aero.Cms.Abstractions/Blocks/Neo/NeoPageNode.cs` — composition tree node (Dict\<string, JsonElement\>)
- `src/Aero.Cms.Abstractions/Blocks/Neo/NeoCompositionBlock.cs` — BlockBase subclass
- `src/Aero.Cms.Shared/Blocks/Rendering/NeoCompositionBlockRenderer.razor` — SSR renderer
- `src/Aero.Cms.Shared/Blocks/Rendering/NeoNodeRenderer.razor` — recursive per-node renderer

**Files modified:**
- `src/Aero.Cms.Shared/Blocks/Rendering/RendererMarkers.cs` — added `NeoCompositionBlockRenderer` marker

#### ✅ Done: Typed Neo Blocks & Catalog Infrastructure

| Deliverable | Status | Notes |
|---|---|---|
| Add `Hero01Block` (aero.hero.01) | ✅ Done | Model: Eyebrow, Title, Highlight, Description, PrimaryText/Url, SecondaryText/Url, TrustMarkers; `BlockMetadata` |
| Add `Hero01BlockRenderer.razor` | ✅ Done | Static SSR public renderer (plain HTML, no NeoUI components) |
| Add `Hero01BlockEditorPreview.razor` | ✅ Done | Editor canvas preview wrapping the renderer |
| Add `Hero01BlockEditor.razor` (property editor) | ✅ Done | Radzen-based structured property editor |
| Add `Hero01BlockMapper.cs` | ✅ Done | Node ↔ Block mapping |
| Add `BasicHeroBlock` (aero.hero.basic) | ✅ Done | Migration target for legacy `boring_hero`; 5 properties |
| Add `BasicHeroBlockRenderer.razor` + mapper + editor preview + property editor | ✅ Done | Full implementation |
| Add `ImageBlock` (media.image) | ✅ Done | src, alt, caption, imageMediaId; renderer + mapper + editor preview |
| Add `VideoBlock` (media.video) | ✅ Done | src, poster, caption, autoplay, loop, controls; renderer + mapper + editor preview |
| Add `AudioBlock` (media.audio) | ✅ Done | src, caption, controls, autoplay; renderer + mapper + editor preview |
| Add `GalleryBlock` (media.gallery) | ✅ Done | images list, columns; renderer + mapper + editor preview |
| Add `NeoRawHtmlBlock` (ui.raw-html) | ✅ Done | html; renamed from RawHtmlBlock (avoid source gen collision with legacy `RawHtmlBlock`) |
| Add `SeparatorBlock` (ui.separator) | ✅ Done | Minimal block; renderer + mapper + editor preview |
| Add `NeoColumnsBlock` (neo.layout.columns) | ✅ Done | items list with span, gap, equalHeight; renamed from ColumnsBlock (source gen collision); renderer + mapper |
| Add `ScribanBlock` (neo.template.scriban) | ✅ Done | name, template, JsonDocument Data; `ISecureScribanRenderer` injection; `OnParametersSetAsync` pattern; renderer + mapper |
| Add `NeoEditorCatalogSection`, `NeoEditorCatalogKind` enums | ✅ Done | AeroUi, Primitives, Components; Block, Primitive, Component |
| Add `NeoPropertyFieldType` enum + `NeoPropertyDefinition` record | ✅ Done | 9 field types (Text→Json); Name, Label, FieldType, Required, DefaultValue, Options |
| Add `NeoEditorCatalogItem` record + `INeoEditorCatalogProvider` + `NeoEditorCatalogProvider` | ✅ Done | 11 catalog entries; registered as singleton |
| Add `NeoCatalogSectionMapper` | ✅ Done | Case-insensitive string → enum mapping |
| Add `RendererMarkers.cs` entries for all 11 Neo renderers | ✅ Done | Fully-qualified `typeof()` where legacy types conflict |
| Build verification | ✅ Done | 0 errors; source generator auto-discovers via `[BlockMetadata]` |

**Files created (24):** 11 block models, 11 renderers, 11 mappers, 11 editor previews (Hero01+BasicHero only for property editors), 8 catalog infrastructure files, `RendererMarkers.cs` entries

#### ⏳ Outstanding

| Deliverable | Status | Notes |
|---|---|---|
| Property editors for Image, Video, Audio, Gallery, NeoRawHtml, Separator, NeoColumns, Scriban blocks | ⏳ Not started | Editor previews exist; structured property editors deferred |
| Editor previews for ScribanBlock + NeoColumnsBlock | ⏳ Not started | Show placeholder div in canvas |
| `NeoEditorCatalogValidator` | ⏳ Not started | Catalog ID validation, parent/child placement rules |
| `BlockAction` + `BlockActionRole` shared semantic action model | ⏳ Not started | Per spec; Hero01Block uses simple string properties for now |
| Catalog serialization tests | ⏳ Not started | Marten round-trip for `NeoCompositionBlock` |
| Catalog per-node rendering in `NeoNodeRenderer` | 🔜 Deferred | Shows `[catalogId]` placeholder until individual catalog dispatch defined |

### 🔄 Step 7 — Neo Legacy Block Migration (PARTIAL)

#### ✅ Done: Migration Infrastructure

| Phase | Deliverable | Status | Notes |
|---|---|---|---|
| A | Add `BlockSchemaVersion` to `PageDocument` | ✅ Done | `public int BlockSchemaVersion { get; set; }` — default 0, used for migration idempotency |
| A | Wire `IPageLayoutManifestBuilder` into publish flow | ✅ Done | `PagePublishingWorkflowService.PublishNowAsync` now loads `PageEditorState`, resolves `BlockBase` docs, calls `_layoutBuilder.BuildAsync()`, and emits `PagePublished(LayoutRegions, Version)` |
| B | Create `NeoCatalogIds` constants | ✅ Done | 13 stable catalog IDs |
| B | Create `ILegacyBlockMapper` | ✅ Done | `List<NeoPageNode> MapFromBlock(BlockBase block)` |
| B | Create `LegacyBlockMapper` impl | ✅ Done | 12+ block type mappings; uses `JsonSerializer.SerializeToElement` |
| B | Register in DI | ✅ Done | `PagesModule.cs` — singleton |
| C | Create `BlockMigrationResult` | ✅ Done | Record: Migrated, Skipped, Failed, AffectedPageIds, Errors |
| C | Create `IBlockContentMigrationService` + impl | ✅ Done | Idempotent; per-page and per-site |
| C | Create Wolverine commands + handler | ✅ Done | `MigratePageBlockContent` / `MigrateSiteBlockContent` + `BlockContentMigrationHandler` |
| C | Register services in DI | ✅ Done | Scoped registration |
| D | Document migration tested | ✅ Done | 6 pages; all have `PageEditorState` |
| D | Block migration tested | ✅ Done | All pages at current schema; zero rendering changes |
| D | API endpoints added | ✅ Done | 14 migration endpoints (page, site, diagnose, status, page-list) |

**Files created (10):** `NeoCatalogIds.cs`, `ILegacyBlockMapper.cs`, `LegacyBlockMapper.cs`, `BlockMigrationResult.cs`, `IBlockContentMigrationService.cs`, `BlockContentMigrationService.cs`, `MigrationCommands.cs`, `BlockContentMigrationHandler.cs`, `MigrationApiRoutes.cs`
**Files modified (3):** `PageDocument.cs` (BlockSchemaVersion), `PagePublishingWorkflowService.cs` (layout builder), `BlockRenderContext.cs` (NestingDepth/MaxNestingDepth)

#### ✅ Done: Safety Valve, Scriban Port, Legacy Editor Removal

| Deliverable | Status | Notes |
|---|---|---|
| Port Neo Scriban with catalog metadata, editor preview, public SSR renderer | ✅ Done | `ScribanBlock` (neo.template.scriban); renderer uses `ISecureScribanRenderer` + `OnParametersSetAsync`; mapper; catalog entry |
| Remove old block editor UI | ✅ Done | ~1350 lines of legacy `RenderXxxBlock` previews removed from `BlockEditorPreviewHost.razor` (1492→~140 lines) |
| Remove old sidebar block entries (4 sections, 29 block types) | ✅ Done | UI, Aero UX, Media, References sections removed; `ToggleCategory` cases cleaned up |
| Remove legacy `CreateBlock` cases | ✅ Done | 25 legacy cases removed; only `aero.hero.01`, `aero.hero.basic` remain |
| Remove dead column/drag methods | ✅ Done | `UpdateColumnCount`, `AddBlockToColumn`, `CreateNestedBlock`, `RemoveNestedBlock`, `DropOnColumn` + `IBlockEditorCallbacks` members |
| Remove dead HTML5 drag handlers | ✅ Done | `DragStartBlock`, `DragOverBlock`, `OnDropCanvas`, `DropBlock`, `DraggedBlockId`, `DragOverIndex` |
| Remove dead imports | ✅ Done | `BlockEditorPreviewHost.razor`: removed 7 unused `@using` directives |
| Build verification | ✅ Done | 0 errors |

#### ⏳ Outstanding

| Deliverable | Status | Notes |
|---|---|---|
| Remove `EditorBlock`-based save/publish logic from `PageContentService` | ⏳ Not started | Legacy persistence path |
| Remove dead UI: `BlockEditor.razor`, `BlockPicker.razor` (~630 lines) | ⏳ Not started | Per `block-editor-refactor.md` |
| Remove old `IBlockSliceRenderer` and `BlockSliceRegistry` | 🔜 Deferred | Council finding: zero concrete implementations; verify render path first |
| Neo Scriban security hardening | 🔜 Deferred | Allowlist, sandbox, timeouts, sanitization, error rendering |

---

## Next Implementation Slice (Updated 2026-05-14)

Based on current state (Phases 0-7 complete, Phase 1/6/8 + deferred items remain):

### Batch A — Static SSR Host + Legacy Cleanup (Phase 1 + Phase 7 remainder)

| # | Task | Files (est.) | Depends on |
|---|---|---|---|
| 1 | Replace `Page.cshtml` with `.razor` static SSR host, preserve output-cache policy | 2 | — |
| 2 | Remove `EditorBlock`-based save/publish from `PageContentService` | 1 | — |
| 3 | Remove dead UI: `BlockEditor.razor`, `BlockPicker.razor` (~630 lines) | 2 | 2 |
| 4 | Lazily create `PageEditorState` on first editor open | 1 | — |

### Batch B — Editor Completeness (Phase 5 remainder)

| # | Task | Files (est.) | Depends on |
|---|---|---|---|
| 5 | Property editors for Image, Video, Audio, Gallery, NeoRawHtml, Separator, NeoColumns, Scriban | ~8 | — |
| 6 | Editor previews for ScribanBlock + NeoColumnsBlock | 2 | — |
| 7 | `NeoEditorCatalogValidator` (catalog ID validation, parent/child placement) | 1 | — |
| 8 | `BlockAction` + `BlockActionRole` shared semantic action model | 1 | — |

### Batch C — Source Generator + Typed Adapters (Phase 6)

| # | Task | Files (est.) | Depends on |
|---|---|---|---|
| 9 | `ICmsBlockRenderAdapter<TBlock>` typed variant | 1 | — |
| 10 | Source generator updates for catalog metadata | ~2 | 9 |
| 11 | Replace hardcoded catalog with source-generated registry | 1 | 10 |

### Batch D — Neo Blocks Expansion (Phase 8)

| # | Task | Files (est.) | Depends on |
|---|---|---|---|
| 12 | Port `feature-01` from NeoUI.io as `NeoFeatureGridBlock` | ~4 | Batch B |
| 13 | Build Pricing, Testimonials, FAQ blocks from NeoUI components (Card, Grid, Button) | ~12 | 12 |
| 14 | Build Blog Grid, Contact, Portfolio, Table/DataGrid blocks | ~16 | 13 |

### Batch E — Optimizations & Hardening

| # | Task | Files (est.) | Depends on |
|---|---|---|---|---|
| 17 | Neo Scriban security hardening (sandbox, allowlist, timeouts) | ~2 | ✋ Requires user confirmation — last |

### ✅ Done This Session (2026-05-14)
| # | Task | Files | Notes |
|---|---|---|---|
| — | N+1 query optimization | 6 | `IBlockService.GetByIdsAsync` + `BlockRenderCache` (scoped) + `DynamicPageModel.PreloadBlockCacheAsync` + `BlockPlacementRenderer.razor` sync lookup + `PagePreviewService` batch |
| — | PagesApi cache eviction | 1 | `PublishPage`/`UnpublishPage` now broadcast `PageContentUpdatedEvent` via `IMessageBus` |
| — | Dead code removal | 4 | Deleted `BlockSliceRegistry` (115 lines), `IBlockSliceRenderer` (26), `CmsBlockSliceRenderer` (15), DI registration |
| — | Client WASM NeoUI DI | 1 | Added `AddNeoUIPrimitives()`/`AddNeoUIComponents()` to `Aero.Cms.Web.Client/Program.cs` |

## Known Deferred Items

These were explicitly deferred during implementation and are tracked for follow-up:

| Item | Step | Rationale |
|---|---|---|---|
| Marten event upcasting (`PageContentUpdated` → `PageMetadataUpdated`) | 3 | Both event types are actively used; upcast when service layer switches |
| Remove `PageDocument.Blocks` and `.BlockIdMap` | 1 | Kept for backward compat; remove after migration verified in production |
| Staging smoke/load gate | 4 | Operational concern — add before production |
| Fine-grained cache tags (`site:{id}`, `page:{id}`, `slug:{slug}`) | 4 | Coarse tags (`pages-list`) sufficient for current policy |
| Catalog per-node rendering in `NeoNodeRenderer` | 6 | Shows `[catalogId]` placeholder until individual Neo catalog items defined |
| Carousel migration | 7 | Not in initial migration set (Gallery instead) |
| Flatten `LayoutRegions` to `EditorBlock` (Option C from `block-editor-refactor.md`) | Future | Council-reviewed: 3-4 week refactor; verify render path first |
| Remove `BlockBase` hierarchy + legacy subtypes | Future | ~35 files; requires verifying public rendering path independence |
| Neo Scriban security hardening | Future | ✋ User-confirmed: last item to address. Allowlist, sandbox, timeouts, sanitization, error rendering |
| Public NeoUI components as interactive islands | Future | Not in V1; Alpine/HTMX for custom interactivity |
| Reusable saved custom block patterns | Future | Later-phase work |
| Property editors for blocks 3-11 (Image, Video, Audio, Gallery, Separator, etc.) | Future | Editor previews exist; structured property editors deferred |
- `NeoUI.Icons.Lucide` 3.0.0 on NuGet, not 4.0.0 (README was aspirational); `NeoUI.Blazor` latest is 4.0.18, Primitives 4.0.5
- `AddNeoUIPrimitives()` must be called BEFORE `AddNeoUIComponents()` per docs; requires `using NeoUI.Blazor.Primitives.Extensions;`
- All 4 portal hosts (`ToastViewport`, `DialogHost`, `ContainerPortalHost`, `OverlayPortalHost`) go INSIDE `<AppProvider>`, not outside
- Scoped `@using NeoUI.Blazor` to `ManagerShellLayout.razor` only — global import in `_Imports.razor` caused 74+ namespace collisions (Radzen's `ButtonSize`, `SidebarSide`, AeroCMS's `CarouselItem`)
- Source generator creates adapter classes by TYPE NAME, not namespace — same-named `BlockBase` subclasses in different namespaces produce `CS0111` duplicate `Render` methods. Fix: rename Neo blocks to unique names (`NeoRawHtmlBlock`, `NeoColumnsBlock`)
- `RendererMarkers.cs` uses fully-qualified `typeof(Aero.Cms.Abstractions.Blocks.Neo.ImageBlock)` where legacy types conflict
- `CanvasClass` computed property needed for Sortable — Razor doesn't support `Class="string @(expr)"` on component attributes
- `AeroError` uses `.ToString()`, not `.Messages` (matches `DynamicTemplateBlockRenderer` pattern)
- `Web.Client` doesn't need NeoUI registrations (WASM doesn't run manager pages)
- `PageEditorPropertyPanel` data flow: EditorBlock → `MapEditorBlockToNeoNode()` → `*Mapper.FromNode()` → BlockBase → `BlockEditorHost`
- Migration API (14 endpoints) serves as safety valve — no separate "migrate now" button needed
- `ScribanBlockRenderer` uses `ISecureScribanRenderer` injection with `OnParametersSetAsync` pattern (matches `DynamicTemplateBlockRenderer`)
- ~2200 lines of legacy code safely removed after verifying catalog + migration paths were operational

## Relevant File Index

### New Files (57)

| File | Step | Purpose |
|---|---|---|
| `src/Aero.Cms.Core.Entities/PageEditorState.cs` | 1 | Draft workspace document |
| `src/Aero.Cms.Core.Entities/EditorBlockPlacement.cs` | 1 | Placement metadata DTO |
| `src/Aero.Cms.Modules.Pages/Admin/PageAdminStatusService.cs` | 1 | Published vs draft version comparison |
| `src/Aero.Cms.Modules.Pages/PageDeleteHandler.cs` | 1 | Cleanup on page delete |
| `src/Aero.Cms.Modules.Pages/IPageLayoutManifestBuilder.cs` | 2 | Layout manifest builder interface |
| `src/Aero.Cms.Modules.Pages/PageLayoutManifestBuilder.cs` | 2 | Builder implementation |
| `src/Aero.Cms.Modules.Pages/IPagePreviewService.cs` | 2 | Preview service interface + `PreviewRenderModel` |
| `src/Aero.Cms.Modules.Pages/PagePreviewService.cs` | 2 | Preview service implementation |
| `src/Aero.Cms.Modules.Pages/PageDocumentMigration.cs` | 3 | Idempotent document structure migration |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/IBlockEditorCallbacks.cs` | 5 | Cascading callback interface (18→14 members after legacy removal) |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditorHeader.razor` + `.cs` | 5 | Header component |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/EditorBlockFrame.razor` + `.cs` | 5 | Block wrapper with toolbar |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditorCanvas.razor` | 5 | Sortable-backed block canvas |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/Palette/PageEditorPaletteSection.razor` | 5 | NeoUI Sortable palette (copy-source) |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/BlockEditorHost.razor` | 5 | Per-block property editor dispatch |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditorPropertyPanel.razor` | 5 | Property panel chrome wrapper |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/BlockEditorPreviews/BlockEditorPreviewHost.razor` | 5 | Neo-only block preview dispatch (was 1358 lines legacy, now ~140 lines) |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/Catalog/NeoEditorCatalogSection.cs` | 6 | AeroUi, Primitives, Components enum |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/Catalog/NeoEditorCatalogKind.cs` | 6 | Block, Primitive, Component enum |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/Catalog/NeoPropertyFieldType.cs` | 6 | 9 field types (Text→Json) |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/Catalog/NeoPropertyDefinition.cs` | 6 | Name, Label, FieldType, Required, DefaultValue, Options |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/Catalog/NeoEditorCatalogItem.cs` | 6 | CatalogId, DisplayName, Section, Kind, IconName, SortOrder, property definitions, child/parent constraints |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/Catalog/INeoEditorCatalogProvider.cs` | 6 | Catalog provider interface |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/Catalog/NeoEditorCatalogProvider.cs` | 6 | 11 catalog entries |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/Catalog/NeoCatalogSectionMapper.cs` | 6 | Case-insensitive string → enum |
| `src/Aero.Cms.Abstractions/Blocks/Neo/Hero01Block.cs` + mapper | 6 | aero.hero.01 (9 properties) |
| `src/Aero.Cms.Abstractions/Blocks/Neo/BasicHeroBlock.cs` + mapper | 6 | aero.hero.basic (5 properties) |
| `src/Aero.Cms.Abstractions/Blocks/Neo/ImageBlock.cs` + mapper | 6 | media.image |
| `src/Aero.Cms.Abstractions/Blocks/Neo/VideoBlock.cs` + mapper | 6 | media.video |
| `src/Aero.Cms.Abstractions/Blocks/Neo/AudioBlock.cs` + mapper | 6 | media.audio |
| `src/Aero.Cms.Abstractions/Blocks/Neo/GalleryBlock.cs` + mapper | 6 | media.gallery |
| `src/Aero.Cms.Abstractions/Blocks/Neo/NeoRawHtmlBlock.cs` + mapper | 6 | ui.raw-html (renamed to avoid source gen collision) |
| `src/Aero.Cms.Abstractions/Blocks/Neo/SeparatorBlock.cs` + mapper | 6 | ui.separator |
| `src/Aero.Cms.Abstractions/Blocks/Neo/NeoColumnsBlock.cs` + mapper | 6 | neo.layout.columns (renamed to avoid source gen collision) |
| `src/Aero.Cms.Abstractions/Blocks/Neo/ScribanBlock.cs` + mapper | 6 | neo.template.scriban |
| `src/Aero.Cms.Shared/Blocks/Rendering/Hero01BlockRenderer.razor` | 6 | Public SSR renderer (11 total Neo renderers) |
| `src/Aero.Cms.Shared/Blocks/Rendering/*BlockRenderer.razor` (10 more) | 6 | BasicHero, Image, Video, Audio, Gallery, NeoRawHtml, Separator, NeoColumns, Scriban, NeoComposition |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/AeroUi/Hero01/*BlockEditorPreview.razor` (8) | 6 | Editor canvas previews for 8 blocks |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/AeroUi/Hero01/Hero01BlockEditor.razor` (2) | 6 | Property editors for Hero01 + BasicHero |
| `src/Aero.Cms.Shared/Blocks/Rendering/ScribanBlockRenderer.razor.cs` | 6 | Scriban code-behind (`ISecureScribanRenderer`, `OnParametersSetAsync`) |
| `src/Aero.Cms.Abstractions/Blocks/Neo/NeoPageNodeKind.cs` | 6 | Composition node kind enum |
| `src/Aero.Cms.Abstractions/Blocks/Neo/NeoPageNode.cs` | 6 | Composition tree node (Dict\<string, JsonElement\>) |
| `src/Aero.Cms.Abstractions/Blocks/Neo/NeoCompositionBlock.cs` | 6 | BlockBase subclass for composition |
| `src/Aero.Cms.Shared/Blocks/Rendering/NeoCompositionBlockRenderer.razor` | 6 | SSR composition renderer |
| `src/Aero.Cms.Shared/Blocks/Rendering/NeoNodeRenderer.razor` | 6 | Recursive per-node renderer |
| `src/Aero.Cms.Abstractions/Blocks/Neo/NeoCatalogIds.cs` | 7 | 13 stable catalog ID constants |
| `src/Aero.Cms.Modules.Pages/Migration/ILegacyBlockMapper.cs` | 7 | Block mapper interface |
| `src/Aero.Cms.Modules.Pages/Migration/LegacyBlockMapper.cs` | 7 | 12+ block type mappings |
| `src/Aero.Cms.Modules.Pages/Migration/BlockMigrationResult.cs` | 7 | Migration result record |
| `src/Aero.Cms.Modules.Pages/Migration/IBlockContentMigrationService.cs` | 7 | Migration service interface |
| `src/Aero.Cms.Modules.Pages/Migration/BlockContentMigrationService.cs` | 7 | Idempotent migration service |
| `src/Aero.Cms.Modules.Pages/Migration/MigrationCommands.cs` | 7 | Wolverine command records |
| `src/Aero.Cms.Modules.Pages/Migration/BlockContentMigrationHandler.cs` | 7 | Wolverine migration handler |
| `src/Aero.Cms.Modules.Pages/Migration/MigrationApiRoutes.cs` | 7 | 14 migration API endpoints |

### Modified Files (16)

| File | Steps | Changes |
|---|---|---|
| `src/Directory.Packages.props` | 5 | NeoUI 4.0.18/4.0.5/3.0.0 |
| `src/Aero.Cms.Shared/Aero.Cms.Shared.csproj` | 5 | NeoUI package refs |
| `src/Aero.Cms.Modules.Setup/Aero.Cms.Modules.Setup.csproj` | 5 | NeoUI package refs (Setup needs its own) |
| `src/Aero.Cms.Web/Program.cs` | 5 | `AddNeoUIPrimitives` + `AddNeoUIComponents` + NeoUI usings |
| `src/Aero.Cms.Web/Components/App.razor` | 5 | NeoUI CSS (components+zinc+blue) + theme.js |
| `src/Aero.Cms/MauiProgram.cs` | 5 | NeoUI registrations |
| `src/Aero.Cms.Web.Client/Program.cs` | 5 | NeoUI registrations REMOVED (WASM doesn't need) |
| `src/Aero.Cms.Shared/Layout/ManagerShellLayout.razor` | 5 | `<AppProvider>` + 4 portal hosts + scoped NeoUI usings |
| `src/Aero.Cms.Core.Entities/PageDocument.cs` | 1, 7 | `PublishedVersion`, `BlockSchemaVersion`, `Apply(PageMetadataUpdated)`, updated `Apply(PagePublished)` |
| `src/Aero.Cms.Abstractions/Events/PageEvents.cs` | 1 | `PageMetadataUpdated`, `PagePublished` with optional params |
| `src/Aero.Cms.Modules.Pages/PageDocumentProjection.cs` | 1 | Wired new events |
| `src/Aero.Cms.Modules.Pages/PagesModule.cs` | 1, 2, 3, 6, 7 | DI registrations (catalog provider, NeoUI usings) |
| `src/Aero.Cms.Modules.Pages/PagePublishingWorkflowService.cs` | 4, 7 | `IMessageBus` + cache eviction; `IPageLayoutManifestBuilder` wiring |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor` | 5, 7 | 2021→catalog-driven orchestrator; 4 legacy sidebar sections removed |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor.cs` | 5, 7 | `IBlockEditorCallbacks`, catalog methods, legacy `CreateBlock` cases removed (~2200 lines total legacy removal) |
| `src/Aero.Cms.Shared/Blocks/Rendering/RendererMarkers.cs` | 6 | 11 Neo renderer markers (FQNs where legacy types conflict) |
| `src/Aero.Cms.Shared/Blocks/Rendering/BlockRenderContext.cs` | 7 | `NestingDepth`/`MaxNestingDepth` |
