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
