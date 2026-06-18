# WYSIWYG Page Editor UX Refactor

**Status:** In progress  
**Last modified:** 2026-06-16 (page editor registry-first mapper path verified)  
**Author:** AI-assisted planning session

---

## Implementation Progress

**Last updated:** June 16, 2026

**Estimated core-refactor completion:** 92%

This estimate now accounts for the discovered transition debt between the old
`EditorBlock` property-bag canvas and the catalog/capability-driven Neo node
composition model. The feature work is substantially present, but the
interaction architecture still needs consolidation before the editor should be
called complete. The next architecture slice should be additive catalog
consolidation, not an immediate wholesale editor-state replacement.

### Completed

- Composition capability contracts, named drop zones, and central nesting policy.
- Policy-validated immutable tree insert, move, reorder, and remove operations.
- Cycle, parent-kind, child-kind, drop-zone, and maximum-child enforcement.
- Typed responsive styles with logical spacing and explicit direction support.
- Registry contracts for catalog metadata, node factories, and block mappers.
- Legacy definition adapter so existing canned blocks remain available.
- Node editor Memento/session foundation.
- Bounded composition undo/redo history with coalescing, redo invalidation,
  canvas mutation records, and page-editor controls.
- Bounded root-canvas mementos with identity-preserving undo/redo for Custom
  component insertion, integrated with the existing composition controls.
- Root-canvas history coverage for canned and Custom insertion, toolbar moves,
  sortable reorder, duplicate, and delete, reset at page-load boundaries.
- Web and MAUI keyboard commands for undo, redo, copy, cut, paste, duplicate,
  and delete, with editable-field suppression and collision-free pasted node
  identities.
- Policy-driven invalid-drop feedback propagated through nested composition
  surfaces into an error toast and dismissible canvas alert.
- Nested subtree copy/paste controls with fresh recursive node identities,
  policy validation, rejection feedback, and composition-history integration.
- Content/Design/Advanced modal tabs with native primitive content editors and
  typed responsive spacing, dimensions, opacity, and direction controls.
- Desktop, Tablet, and Mobile style editing with inherited-value display and
  explicit breakpoint reset.
- Nested node selection, double-click Edit, right-click actions, and undoable
  Duplicate/Delete operations for composition children.
- Sanitized typed-style CSS rendering for public nodes and editor composition
  children, including responsive inheritance and logical spacing.
- Native composition persistence bridge through `EditorBlock.CompositionNodes`.
- Primitives palette registration.
- Container, Text, Button, Image, Pill, Icon, and Separator primitive
  definitions, previews, editing, and public rendering.
- Card preset factory composed from standard editable primitives.
- Nested NeoUI Sortable transfer wiring for palette insertion and re-parenting.
- Shared sortable composition surface used by Container and Card definitions.
- Native preview roots are initialized into persisted editor state rather than
  rendered as transient factory instances.
- Source-generated mixed-page transport preserves nested composition,
  responsive styles, direction, and primitive property data.
- Real TestServer coverage proves the Pages update API preserves nested
  composition, RTL direction, responsive overrides, and localized text through
  HTTP binding, Orleans-safe JSON transport, and grain rehydration.
- Pages API endpoint registration now uses explicit service binding for site,
  Marten session, preview renderer, and page-service dependencies.
- Preview workflow coverage proves draft placements batch-load the persisted
  Neo composition block, produce a transient layout manifest, preserve
  localized RTL metadata, and never mutate the published page layout.
- Embedded-Postgres/Marten publish coverage proves draft Neo composition is
  resolved into a layout manifest, projected through the real inline page
  event projection, persisted as the next published version, and followed by
  the expected page/cache notification messages.
- Static public-region rendering coverage proves a published Neo composition
  resolves through the request block cache and generated renderer pipeline,
  preserving localized content, RTL direction, logical spacing, background
  styling, node metadata, and mobile breakpoint overrides.
- Fixed public Neo SSR wrappers so static rendering emits valid elements with
  node IDs, catalog IDs, and typed styles instead of attribute-less `<>`
  fragments.
- Culture and detached-page persistence paths isolate editor-block composition
  trees through deep clones.
- Site-owned Custom component entity and service foundation with validation,
  tenant-scoped CRUD, isolated template capture, catalog dependency tracking,
  and fresh-ID insertion.
- Typed Custom component HTTP client and minimal API endpoints for list, create,
  rename/update, fresh-instance creation, and delete.
- Editor Save as Custom modal, live site-owned Custom palette, and drag/drop
  insertion through fresh independent composition instances.
- Custom palette rename/edit and confirmed delete actions; deleting a template
  does not mutate composition instances already placed on pages.
- Palette search with clear/no-results states and multi-term matching across
  names, descriptions, catalog IDs, sections, kinds, and Custom tags.
- Reusable palette-search matching policy with focused coverage for empty,
  case-insensitive, multi-term, catalog-field, and Custom keyword queries.
- Focused TUnit coverage for composition policy, tree mutation, styles, mapping,
  legacy adapters, editor sessions, and reusable component templates.
- Database-backed Custom component service coverage for site-scoped name
  conflicts, cross-site name reuse, and tenant-isolated read, instance,
  update, and delete operations.
- Full Shared project compilation restored by completing the missing page-tree
  translation, public-view, edit, publish, unpublish, and delete handlers.
- Setup now seeds a mixed canned/native homepage with a responsive bilingual
  primitive composition, explicit LTR/RTL child direction, logical spacing,
  mobile overrides, and fresh node identities.
- Universal native-node controls now include sanitized responsive foreground,
  background, border color, border width/radius, and breakpoint visibility,
  gated by each definition's declared editor capabilities.
- Universal native-node controls now include typed font size, font weight, line
  height, letter spacing, and logical text alignment with responsive
  inheritance and LTR/RTL-safe `start`/`end` values.
- Background-capable native nodes now support validated two-stop linear
  gradients with responsive angle/color editing and explicit breakpoint
  disabling.
- Effects-capable native nodes now support validated responsive box shadows
  with bounded offsets, blur, spread, color, and explicit breakpoint
  disabling.
- Background-capable native nodes now reuse the shared media library for
  responsive background-image selection, persist the media ID and safe URL,
  and support typed cover/contain sizing and breakpoint removal.
- Background images now support nine typed focal positions using logical
  inline start/end labels that render direction-aware physical positions for
  LTR and RTL pages.

- The native Image primitive now opens the shared media library from its
  content editor and writes the selected URL into its existing persisted
  `url` property while retaining manual external-URL entry.
- Live browser smoke coverage now confirms the right palette expands with
  search and all catalog groups, the Primitives group exposes the expected
  native inventory, primitive drag/drop inserts into the root canvas, and the
  insertion enables Undo.
- Root-canvas blocks now expose a localized right-click Edit menu that opens
  the same complete modal used by double-click.
- Root block-frame rendering now has focused component coverage for context
  menu suppression and selected-block canvas actions.
- Fixed the universal modal's tab binding so pre-existing canned blocks render
  their existing Content property editors instead of always falling through to
  the shared-controls placeholder.
- Pre-existing canned blocks now persist typed responsive styles, expose the
  applicable shared Design/Advanced controls, update their canvas preview, and
  render through a sanitized responsive public wrapper.
- Focused canned-block coverage proves responsive style mapping is isolated by
  deep clone and public rendering emits Desktop, Tablet, and Mobile styles.
- Phase 0.5 registry foundation is in place: `IPageEditorDefinitionRegistry`,
  an immutable DI-backed `PageEditorDefinitionRegistry`, compatibility shim
  wiring for the old static registry, and initial editor/palette lookups through
  the injected registry.
- Page editor canvas, root block frame actions, block preview host, property
  panel, modal editor host, preview fragment rendering, publish/save block
  mapping, and grain-backed page service construction now consume the injected
  `IPageEditorDefinitionRegistry` path instead of direct static registry
  lookups.
- Built-in Neo and Hyper package extensions now register editor definitions
  through DI providers only. They no longer create providers manually for
  `PageEditorBlockRegistry`, leaving the static registry as a deprecated
  compatibility bridge rather than the extensibility boundary.
- `EditorBlockMapper` now has an `IEditorBlockMapper` service boundary so
  page preview and publish paths depend on abstractions and can be tested with
  registry substitutes.
- Legacy alias block IDs that previously existed only inside the publish/save
  mapper switch are now registered through `LegacyPageEditorBlockProvider`,
  giving them the same `IPageEditorBlockProvider` -> `IPageEditorBlockDefinition`
  traceability as package-provided blocks.
- `EditorBlockMapper` no longer contains the legacy alias block switch. It now
  relies on the injected registry for known block mappings and returns `null`
  for truly unknown editor block types.
- `PageEditor.CreateBlock`, `GetBlockBaseForEditor`, and
  `MapEditorBlockToNeoNode` now resolve through the injected definition
  registry first, with legacy UI switches retained only as parity bridges while
  canned blocks move to native composition definitions.
- Block-rendering test scaffolding now registers the same definition registry
  and action-provider services as the app, and mapper tests cover legacy alias
  mapping through `LegacyPageEditorBlockProvider`.

### Architecture Transition In Progress

- Native primitive definitions use catalog/capability contracts.
- Page-editor definition lookup now flows through a DI-backed registry for the
  primary editor, preview, publish/save paths, and legacy alias block mapping.
  The static compatibility shim remains only as a transitional bridge for older
  external consumers and should be removed after registry parity tests cover all
  canned blocks.
- Composition policy exists and is used by nested mutation paths.
- Universal context-menu behavior is documented but not yet centralized.
- Root blocks, nested primitives, Columns rows, and custom components still
  have separate UI action paths.
- Editor UI and preview switches still preserve existing legacy editors until
  each canned block has registry-backed parity.
- `EditorBlock` is still the dominant page-editor storage and transport bridge.
- `SeedDataService.cs` still needs a direct Neo node-tree seed model.

### Adopted Target: HTML-Adjacent Composition Model

The proposal in `docs/proposed-cms-wysiwig-editor.md` is adopted as the target
conceptual model for the final editor vocabulary, with several important
AeroCMS-specific constraints.

The editor should move toward a semantic, HTML-adjacent node tree rather than a
large opaque block taxonomy. Authors should be able to reason in familiar page
structures:

- `SectionBlock` -> `<section>`
- `ArticleBlock` -> `<article>`
- `NavBlock` -> `<nav>`
- `FormBlock` -> `<form>`
- `GridBlock` -> layout container
- `GridRow` -> row container
- `GridCell` -> droppable cell container
- `TextBlock` -> `<p>` or rich text
- `HeadingBlock` -> `<h1>` through `<h6>`
- `ImageBlock` -> `<figure>` / `<img>`
- `ButtonBlock` -> `<button>` or link-button
- `PillBlock` -> `<span>` badge
- `DividerBlock` -> `<hr>`
- `CodeBlock` -> `<pre>` / `<code>`
- `EmbedBlock` -> safe `<iframe>` widget

This model should improve SSR output, accessibility, SEO, author intuition, and
the long-term ability to make AeroCMS feel like a real visual GUI composer
rather than a list of unrelated content widgets.

The adopted shape is:

```text
Persisted node tree
  -> typed catalog definition
    -> composition capabilities
    -> editor capabilities
    -> renderer strategy/component
    -> property editor strategy/component
```

The proposed `IPageElement`, `IContainer`, `IEmbeddable`, `ISlotted`, and
`IConfigurable` concepts are valid, but they should be expressed through the
existing registry/definition system rather than as persisted Blazor rendering
objects. Persisted/domain models must not directly expose
`RenderFragment Render(...)`; rendering remains a separate strategy owned by
registered renderers or Blazor components.

Final storage should also avoid `Dictionary<string, object>` for arbitrary
property bags. Use typed normalized values, typed descriptors, or controlled
`JsonElement` payloads that can be validated, serialized with
`System.Text.Json`, localized, diffed, and rendered safely.

Discovery remains DI-provider based:

```text
Package/provider
  -> IPageEditorBlockProvider / IPageEditorDefinitionProvider
    -> IPageEditorDefinitionRegistry
      -> palette / canvas / editor / preview / public renderer
```

Do not use Scrutor or reflection scanning as the block-discovery boundary.
Source generators may emit first-party providers, renderer adapters, metadata,
and serialization registrations, but external package extensibility flows
through DI providers.

`GridBlock -> GridRow -> GridCell` becomes the preferred replacement direction
for the current Columns block. `GridCell` is the real droppable container, and
row add/remove/delete operations should be first-class editor commands.

`ISlotted` is the controlled composition answer for canned blocks. A slotted
hero or card may expose named regions such as `media`, `content`, `actions`,
`header`, `body`, and `footer`; users can compose inside those regions only
according to the slot's declared constraints.

`EmbedBlock` is accepted as a future primitive, but only with:

- URL normalization through `IEmbedUrlResolver` implementations.
- Provider-specific resolvers for YouTube, Vimeo, Google Maps, Calendly,
  Typeform, Loom, and similar services.
- A strict HTTPS fallback resolver.
- Site/operator allow-list policy.
- Safe sandbox and permissions-policy defaults.
- Required iframe title for accessibility.
- Lazy loading and fixed aspect-ratio rendering.
- Inert editor-mode placeholder instead of live third-party iframe execution.

### Architecture Cleanup Remaining

- [ ] Add XML/doc comments to the catalog, composition, command, memento,
  adapter, and interaction contracts that explain their responsibility and
  where they sit in the composition pipeline.
- [ ] Make each catalog family traceable from interface -> abstract base class
  -> concrete implementation. Existing contracts should be reused where they
  already express the role; do not introduce a parallel hierarchy just to
  rename working abstractions.
- [ ] Add `EditorInteractionCapabilities` as a separate interaction contract,
  not mixed into `EditorCapabilitySet`, which currently describes property
  editor groups.
- [ ] Add `IEditorInteractionProvider` that exposes
  `EditorInteractionCapabilities Interaction { get; }` for a given node
  definition. `PageEditorCatalogDefinitionBase` implements this by default.
- [ ] Add centralized `IEditorNodeActionProvider` that consumes `Interaction`
  flags plus editor session state (selection, clipboard, undo stack) and
  returns the currently available context menu actions.
- [ ] Move root blocks, primitives, rows/columns, containers, and custom
  components to one canvas node rendering path.
- [ ] Reframe the native node catalog around the adopted HTML-adjacent element
  vocabulary and map each supported element to semantic public HTML.
- [ ] Replace the current Columns implementation with a typed
  `GridBlock -> GridRow -> GridCell` model, including add/delete row commands.
- [ ] Add controlled `ISlotted`-style named regions for selected canned blocks
  after the base grid/container model is stable.
- [ ] Add a secure `EmbedBlock` primitive with resolver pipeline, allow-list,
  sandbox policy, and editor placeholder behavior.
- [ ] Route all node mutations through commands plus `ICompositionPolicy`.
- [ ] Port existing canned blocks into full node/composition definitions.
- [ ] Remove legacy switch-based preview/editor/action paths after parity. The
  publish/save mapper switch has been removed for registered legacy aliases;
  editor UI, preview selection, and action execution switches still need parity
  work.
- [ ] Replace legacy flat `EditorBlock` storage with direct node-tree editor
  state only after catalog definitions and `CompositionNodes` migration make
  the flat block property bag dead-code-eligible.
- [ ] Update `SeedDataService.cs` to seed the new node-tree architecture.

### In Progress

- Phase B primitive inventory and vertical slice.
- Browser verification of nested sorting, cancellation, rapid pointer movement,
  drag indicators, and flicker-free re-parenting.
- Responsive card styling and full composition interaction verification.
- Shared property editing and universal modal behavior.
- Manual browser verification of persisted Design/Advanced styling for
  pre-existing canned blocks.
- Custom component browser coverage.

### Known Blockers

- No known compile blocker in the editor's Shared project. Repository package
  vulnerability and dependency-version warnings remain visible during builds
  and should be handled separately from this editor refactor.
- Aspire and the editor now run from the canonical
  `D:\proj\microbians\AeroCMS` workspace. The earlier `C:\Users\bbqch\proj`
  path came from the symbolic-link launch path rather than a project defect.
- The bundled Playwright Chromium image is unavailable locally, but the
  browser smoke gate can run against the installed Chrome executable.

### Policy Notes

- The `NeoUI/` and `hyperui/` directories are git submodules pulled for
  reference only. Do not modify files inside them. All editor-related changes
  belong in the AeroCMS `src/` tree.

### Manual Test Gate

Do not schedule the full manual acceptance walkthrough until all core features
are implemented and automated checks are green:

- Mixed canned/native save, reload, preview, publish, and public rendering.
- Primitive and nested-node edit, duplicate, delete, move, copy, and paste.
- Undo/redo across supported canvas mutations.
- Responsive Desktop/Tablet/Mobile editing and LTR/RTL rendering.
- Media and icon selection.
- Site-owned Custom component save, insert, rename, and delete.
- Palette search and clear invalid-drop feedback.
- Playwright smoke coverage for the critical editor journey.
- Full Shared/Web builds pass without unrelated blockers.

An earlier targeted smoke test is appropriate only after the complete primitive
card journey works end to end. It is not the final acceptance test.

### Next

1. Finish browser-testing the complete Custom component and LTR/RTL vertical
   slices; palette expansion and root primitive insertion are verified.
2. Add Playwright coverage for composition editing, history, responsive
   behavior, and persistence.
3. Run the full Web build and manual acceptance gate once browser tooling is
   available.

### Remaining Work

#### Required for the Core Refactor

- [x] Add a persisted typed responsive style contract to canned block
  instances, expose the same applicable Design/Advanced controls used by native
  nodes, and render those styles through a sanitized public block wrapper.
- [x] Extend the shared Content/Design/Advanced modal with typed, responsive
  background repeat behavior (`no-repeat`, repeat, horizontal, and vertical).
- Add additional shared visual effects where they materially improve the
  editor without exposing unsafe arbitrary CSS.
- [x] Add searchable visual Lucide icon selection while retaining manual icon-name entry and accessible labels.
- [x] Add rendered component coverage for palette search empty-query,
  clear-control, and accessible no-results behavior. The reusable matching
  policy remains covered for name, description, catalog ID, section/category,
  tag, kind, and multi-term queries.
- Add database-backed integration coverage proving mixed canned/composition
  save, reload, preview, publish, and public rendering through the full
  API/Orleans path. The transport and renderer contracts are implemented.
  Detached persistence-harness coverage now proves mixed canned/composition
  snapshots preserve culture, RTL direction, responsive overrides, publication
  metadata, and isolation. Real embedded-Postgres/Marten coverage now proves
  mixed page documents round-trip nested composition, responsive styles,
  background repeat, culture, and RTL state. Real TestServer coverage now also
  proves the API-to-Orleans transport and grain rehydration seam. Focused
  preview-service coverage proves draft layout generation and published-layout
  isolation. Embedded-Postgres/Marten coverage now proves the publish workflow,
  inline event projection, version/state transition, layout persistence, and
  notification messages. Static public-region coverage now proves the same
  persisted Neo block shape renders through the public cache/region/block
  pipeline with localized RTL content and responsive typed styles.
- [x] Add real TestServer API/client contract coverage for Custom component
  create, list, update, instance creation, and delete routes.
- Add browser-level Custom component coverage. Persistence, tenant isolation,
  name-conflict behavior, Save as Custom, live palette registration, isolated
  template capture, dependency tracking, rename/update, confirmed delete,
  fresh-ID insertion, and root-canvas insertion undo/redo are implemented.
- Add component and Playwright tests for nested drag/drop, cancellation, rapid
  movement, modal editing, history, persistence, responsive breakpoints, and
  bidirectional behavior.
- Run the full Web build and editor Playwright release gate after the remaining
  browser-facing features are implemented.

#### Later Phases

- Controlled named slots for selected canned blocks.
- Inline text editing, resize handles, refined drag ghosts/drop indicators,
  inherited-value indicators, accessibility polish, and large-canvas profiling.
- Versioned custom component export/import with schema and dependency checks.

---

## Objective

Transform the AeroCMS page editor from a functional block builder into a best-in-class WYSIWYG SaaS CMS editor. The target experience should be comparable to Wix, Webflow, or GrapeJS in capability and polish, while remaining an Aero-owned solution built around Blazor, NeoUI, plain TypeScript, and Alpine.js.

Users should be able to:

- Build pages from existing canned blocks such as heroes, cards, pricing, and features.
- Build custom composite elements from primitives such as containers, text, buttons, pills, icons, and images.
- Nest elements according to explicit composition rules.
- Edit content through a dedicated editor for every block and primitive.
- Edit responsive design properties through shared, capability-driven controls.
- Open the same complete editor through double-click or a right-click Edit action.
- Edit directly on the canvas where inline interaction is appropriate.
- Save any custom composition under a site-owned **Custom** palette.
- Work responsively across desktop, tablet, and mobile from the beginning.
- Author and preview localized content in both left-to-right and right-to-left directions.

Existing canned blocks remain important. Users may use a canned card or build their own card from primitives.

The editor does not embed GrapeJS, React, or another page-builder runtime. Plain TypeScript and Alpine.js may assist with drag/drop, pointer interactions, inline editing, resize handles, and other browser-heavy interactions.

---

## Product Decisions

1. Keep the current editor shell, palette approach, canned blocks, renderer pipeline, and package-owned definitions while evolving the canvas.
2. Add a **Primitives** palette using existing NeoUI components where suitable.
3. Custom composition is capability-driven, not arbitrary.
4. Canned blocks remain atomic initially and gain complete modal property editors.
5. Explicit named slots inside canned blocks are a later phase.
6. New custom compositions use `NeoCompositionBlock` containing a `NeoPageNode` tree.
7. Existing canned blocks and custom composition blocks coexist on the page canvas during the transition.
8. Database backward compatibility is not required because AeroCMS is not in production.
9. Development seed data and sample pages must be updated for the selected persistence model.
10. Shared styles use typed normalized values. Renderers translate them into sanitized CSS or approved Tailwind utilities.
11. Responsive values use cascading inheritance: Base/Desktop -> Tablet -> Mobile.
12. Custom components are site-owned.
13. Export/import of custom components is deferred until the editor is stable.
14. Undo/redo is foundational and uses Command plus Memento.
15. Localization, globalization, and bidirectional layout are foundational requirements, not final-polish work.
16. Shared layout styles use logical block/inline properties rather than hard-coded top/right/bottom/left assumptions.

---

## Current Architecture

The current primary block path is:

```text
EditorBlock -> EditorBlockMapper -> BlockBase -> generated renderer registry
```

The composition path already has useful foundations:

```text
EditorBlock -> ToNeoPageNode() -> NeoCompositionBlock -> Neo composition renderer
```

### Keep

- Existing canned blocks and public renderers.
- Source-generated block discovery and renderer registration.
- `IPageEditorBlockDefinition` and `PageEditorBlockRegistry`.
- Package ownership of block models, previews, editors, and renderers.
- NeoUI Sortable as the initial drag/drop foundation.
- `EditorBlockFrame` selection and event approach.
- Existing modal editing behavior where it already works.
- `NeoPageNode.Children` as the custom composition tree.

### Current Problems

- The canvas is a flat `List<EditorBlock>`.
- Columns and nested sorting are incomplete.
- The editor uses multiple type switches and two property-editor paths.
- Many Aero UX and Hyper blocks expose only generic title/description/button fields.
- Palette search exists with multi-term matching but only on the new palette path.
- Shared color, spacing, gradient, icon, media, border, and responsive controls are missing.
- `EditorBlock` is a large property bag.
- `PageEditor.razor.cs` owns too many responsibilities.
- Inline editing, context menus, responsive overrides, and polished pointer interactions are incomplete.

---

## SOLID Composition Architecture

### Architectural Goal

The editor should use a modern composable-GUI architecture: every selectable
thing on the canvas is an editor node with declared capabilities. Primitives,
containers, layout rows/columns, custom components, and slot-enabled canned
blocks should not each require special interaction code.

The intended model is:

```text
Catalog definition -> capabilities -> node instance -> renderer/editor/action services
```

In other words, behavior is discovered from definitions and capabilities, not
from `switch` statements scattered through the editor.

### Current Transition Problem

The current implementation is in a transition state:

- New primitives already use `IPageEditorCatalogDefinition`,
  `INeoNodeFactory`, `ICompositionCapabilities`, `ICompositionPolicy`, and
  `IEmbeddable`.
- Legacy/canned blocks still mostly flow through `EditorBlock` and
  `IPageEditorBlockDefinition`.
- Root canvas actions, nested primitive actions, Columns rows, media actions,
  and modal editing still contain bridge code in Razor components.
- This makes simple UX requests feel harder than they should be because the
  editor has multiple interaction paths for concepts that should be one
  abstraction.

This is expected during an incremental refactor, but it should not become the
final architecture. AeroCMS is not in production, so the refactor does **not**
need to preserve the old database/editor storage model forever. However, an
immediate replacement of `List<EditorBlock>` with a root `NeoPageNode` tree is
too risky while root canvas state, undo/redo, clipboard, media selection,
preview, custom components, auto-save, and tests still depend on the flat
editor shape.

The safer path is Phase 0.5 catalog consolidation:

1. Register every remaining legacy switch-case block in the catalog.
2. Add separate interaction capabilities for menu/action behavior.
3. Migrate blocks one at a time to populate `CompositionNodes`.
4. Delete switch fallbacks as each block reaches parity.
5. Replace flat editor state only after the flat property bag is proven dead.

### SOLID Principles Applied

- **Single Responsibility:** catalog definitions describe nodes; policy
  validates composition; action services build menus; renderers render; editors
  edit. `PageEditor.razor.cs` should orchestrate, not own all behavior.
- **Open/Closed:** adding a new primitive or block should add a concrete
  definition and renderer/editor, not modify central switch statements.
- **Liskov Substitution:** all embeddable node definitions should be usable
  through the same catalog/capability contracts. A text primitive, container,
  custom component, and slot-enabled canned block should all be valid canvas
  participants where their capabilities allow.
- **Interface Segregation:** definitions should implement small contracts such
  as catalog metadata, node factory, renderer/editor metadata, persistence
  mapper, composition capabilities, and interaction capabilities. Do not force
  every block to implement persistence or children if it does not need them.
- **Dependency Inversion:** UI components depend on abstractions such as
  `IEditorNodeActionProvider`, `ICompositionPolicy`, and
  `IPageEditorDefinitionRegistry`, not concrete primitive/block classes.

### Interface -> Base Class -> Concrete Class

Use interfaces for contracts, abstract base classes for shared behavior, and
concrete classes for actual catalog items.

The current contracts are already close to the desired interface layer:

- `IPageEditorCatalogDefinition` describes catalog metadata, component types,
  editor capability groups, and composition capability.
- `INeoNodeFactory` creates default node instances.
- `INeoNodeBlockMapper` maps between node trees and public block models when a
  block needs persistence/rendering translation.
- `IPageEditorBlockDefinition` is the transitional legacy adapter target.
- `ICompositionCapabilities` and `ICompositionPolicy` own nesting rules.

Do **not** introduce a parallel `IEditorNodeDefinition` hierarchy just to rename
these contracts. If a new name is still desirable, treat it as a later
consolidating rename after catalog parity. For Phase 0.5, add thin base classes
under the existing contracts so the trail is obvious and searchable.

```csharp
/// <summary>
/// Catalog metadata and editor behavior shared by blocks, primitives, and components.
/// Implementations declare identity, categorization, Kind, composition rules, and
/// property editor capabilities. They do not perform rendering or mutate editor state.
/// </summary>
public interface IPageEditorCatalogDefinition
{
    string CatalogId { get; }
    string DisplayName { get; }
    string? Description { get; }
    string Category { get; }
    NeoPageNodeKind Kind { get; }
    string IconName { get; }
    int SortOrder { get; }
    bool PublicStaticSsrSafe { get; }
    Type? PreviewComponentType { get; }
    Type? PropertyEditorComponentType { get; }
    ICompositionCapabilities Composition { get; }
    EditorCapabilitySet EditorCapabilities { get; }
}

/// <summary>
/// Creates a default persisted node for a catalog item. Factories must produce
/// fresh node IDs and safe default properties.
/// </summary>
public interface INeoNodeFactory
{
    NeoPageNode CreateDefaultNode();
}

/// <summary>
/// Declares which canvas interactions are available for a node definition.
/// This is intentionally separate from EditorCapabilitySet, which describes
/// property editor groups such as Typography, Background, Media, and Direction.
/// </summary>
[Flags]
public enum EditorInteractionCapabilities
{
    None = 0,
    Selectable = 1 << 0,
    Editable = 1 << 1,
    Draggable = 1 << 2,
    Duplicatable = 1 << 3,
    Deletable = 1 << 4,
    Copyable = 1 << 5,
    PasteTarget = 1 << 6,
    SaveAsCustom = 1 << 7,
    MediaSelectable = 1 << 8
}

/// <summary>
/// Base class for catalog definitions that removes repeated metadata defaults
/// while keeping behavior in policies, commands, renderers, and editors.
/// Members with virtual defaults (Description, IconName, SortOrder, etc.) should
/// be overridden only when the concrete definition has a meaningful value.
/// Composition, EditorCapabilities, and Interaction are abstract — every
/// concrete definition must explicitly declare them.
/// </summary>
public abstract class PageEditorCatalogDefinitionBase :
    IPageEditorCatalogDefinition,
    INeoNodeFactory
{
    public abstract string CatalogId { get; }
    public abstract string DisplayName { get; }
    public virtual string? Description => null;
    public abstract string Category { get; }
    public abstract NeoPageNodeKind Kind { get; }
    public virtual string IconName => "unknown";
    public virtual int SortOrder => 0;
    public virtual bool PublicStaticSsrSafe => true;
    public virtual Type? PreviewComponentType => null;
    public virtual Type? PropertyEditorComponentType => null;
    public abstract ICompositionCapabilities Composition { get; }
    public abstract EditorCapabilitySet EditorCapabilities { get; }

    /// <summary>
    /// Declares which canvas interactions are available.
    /// Abstract — each concrete definition must explicitly declare its
    /// capabilities. This prevents accidental grants from inherited defaults.
    /// </summary>
    public abstract EditorInteractionCapabilities Interaction { get; }

    public abstract NeoPageNode CreateDefaultNode();
}

/// <summary>
/// Base class for embeddable leaf items such as Text, Button, Pill, Icon, and
/// Image primitives. Kind is fixed to Primitive. Interaction is deliberately
/// left abstract so each concrete leaf definition declares its own capabilities.
/// </summary>
public abstract class PrimitiveDefinitionBase : PageEditorCatalogDefinitionBase, IEmbeddable
{
    public override NeoPageNodeKind Kind => NeoPageNodeKind.Primitive;
}

/// <summary>
/// Base class for nodes that may contain child nodes through declared drop
/// zones and policy validation. Extends PrimitiveDefinitionBase — containers
/// are still embeddable — with additional editor capabilities such as layout,
/// alignment, borders, and backgrounds. Interaction is abstract: concrete
/// container definitions must declare PasteTarget alongside other flags.
/// </summary>
public abstract class ContainerDefinitionBase : PrimitiveDefinitionBase
{
}

/// <summary>
/// Concrete text primitive definition. Every member is explicitly declared.
/// This class contains only Text-specific defaults and capability declarations;
/// domain behavior (rendering, editing, validation) resides in policies,
/// commands, renderers, and editors.
/// </summary>
public sealed class TextPrimitiveDefinition : PrimitiveDefinitionBase
{
    public override string CatalogId => "primitive.text";
    public override string DisplayName => "Text";
    public override string? Description => "Responsive body text.";
    public override string Category => "Primitives";
    public override string IconName => "type";
    public override int SortOrder => 10;
    public override bool PublicStaticSsrSafe => true;
    public override Type? PreviewComponentType => typeof(TextPrimitivePreview);
    public override Type? PropertyEditorComponentType => typeof(TextPrimitiveEditor);
    public override ICompositionCapabilities Composition =>
        CompositionCapabilities.Leaf(
            NeoPageNodeKind.Section,
            NeoPageNodeKind.Container,
            NeoPageNodeKind.Component);
    public override EditorCapabilitySet EditorCapabilities =>
        EditorCapabilitySet.Content |
        EditorCapabilitySet.Typography |
        EditorCapabilitySet.Spacing |
        EditorCapabilitySet.Dimensions |
        EditorCapabilitySet.Foreground |
        EditorCapabilitySet.Background |
        EditorCapabilitySet.Direction |
        EditorCapabilitySet.Visibility;
    public override EditorInteractionCapabilities Interaction =>
        EditorInteractionCapabilities.Selectable |
        EditorInteractionCapabilities.Editable |
        EditorInteractionCapabilities.Draggable |
        EditorInteractionCapabilities.Duplicatable |
        EditorInteractionCapabilities.Deletable |
        EditorInteractionCapabilities.Copyable;
    public override NeoPageNode CreateDefaultNode() =>
        new()
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = CatalogId,
            Kind = Kind,
            Properties = new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement("Enter your text here...")
            }
        };
}
```

The base classes should stay thin. They remove repeated defaults but must not
become a second property bag. Domain-specific behavior still belongs to
capability objects, policies, commands, renderers, and editors.

#### Context Menu Actions: Not on the Definition

Do **not** add a static `ContexMenuItems` or `Actions` property to
`IPageEditorCatalogDefinition` or its base classes. Context menu items depend
on runtime state (selection context, clipboard content, tree position),
which a static definition cannot model.

Instead, use a three-layer separation:

1. **`EditorInteractionCapabilities`** (flags enum, on the definition) —
   declares what interactions are *possible* for this node type:
   Selectable, Editable, Draggable, Duplicatable, Deletable, Copyable,
   PasteTarget, SaveAsCustom, MediaSelectable.
2. **`IEditorNodeActionProvider`** (DI service) — consumes the definition's
   `Interaction` flags plus the current editor session state (selected node,
   clipboard, undo stack, active breakpoint) and returns the set of
   *currently available* actions.
3. **Razor component** — renders the action list returned by the provider
   with no business logic.

This mirrors how `EditorCapabilitySet` already works: the definition says
which property editor groups a node supports, and the `BlockEditorModal`
service resolves which controls to show at runtime.

Every interface and base class added for this refactor must include comments
that explain:

- The one responsibility of the contract/class.
- Whether it is final architecture or a transitional adapter.
- Which GoF pattern it participates in when applicable.
- Which concrete classes are expected to inherit from it.
- Which service owns mutation, rendering, editing, or persistence behavior.

### GoF Patterns To Use

- **Composite:** `NeoPageNode` trees model GUI composition. Containers,
  sections, rows, columns, custom components, and primitives are all nodes in a
  tree.
- **Command:** every user mutation is a typed command: add, move, re-parent,
  delete, duplicate, paste, edit property, resize, change breakpoint value.
- **Memento:** command history stores bounded snapshots for undo/redo.
- **Strategy:** renderers, property editors, validators, and action builders
  are selected by catalog definition/capability.
- **Factory Method / Abstract Factory:** node definitions create default nodes,
  templates, and custom component instances.
- **Visitor:** optional traversal for validation, rendering preparation,
  localization extraction, dependency collection, and seed verification.
- **Adapter:** temporary bridge from legacy canned blocks into the new
  definition model. Because this is pre-production, adapters are transitional
  only and should be removed when parity is reached.

### Common Modern UI Composition Design

```text
IPageEditorCatalogDefinition + INeoNodeFactory
  -> PageEditorCatalogDefinitionBase
    -> PrimitiveDefinitionBase
      -> TextPrimitiveDefinition
      -> ImagePrimitiveDefinition
      -> ButtonPrimitiveDefinition
    -> ContainerDefinitionBase
      -> ContainerPrimitiveDefinition
      -> ColumnsDefinition
      -> GridDefinition
    -> ComponentDefinitionBase (deferred — Card and CustomComponent
       currently implement the contracts directly without a shared base)
      -> CardDefinition
      -> CustomComponentDefinition
    -> CannedBlockDefinitionBase
      -> HeroBlockDefinition
      -> PricingBlockDefinition

NeoPageNode
  -> primitive node
  -> container node
  -> component node
  -> block node

Editor services
  -> Definition registry
  -> Composition policy
  -> Context menu/action provider
  -> Command dispatcher
  -> History service
  -> Clipboard service
  -> Property editor resolver
  -> Renderer resolver
```

The UI should ask services questions:

- What actions are available for this selected node?
- Can this node be dropped into this drop zone?
- Which property editor should open?
- Which renderer should render this node?
- Which shared design controls are supported?

The UI should not ask:

- Is this exact catalog ID `primitive.text`?
- Is this exact block type `columns`?
- Which component branch am I in?

Catalog IDs are still useful for lookup and persistence, but not for
hardcoding behavior in canvas components.

#### CanvasNode and CanvasContainer

`CanvasNode` and `CanvasContainer` are the unified rendering primitives that
replace the current separate paths for root blocks, nested primitives,
columns/rows, containers, and custom components:

- **CanvasTree** — renders the full canvas tree from the editor session's root
  `NeoPageNode`. Handles tree-level concerns: selection tracking, drop-zone
  registration, and undoable command dispatch.
- **CanvasNode** — renders any single node. Receives a `NeoPageNode`, resolves
  its preview/renderer component from the catalog definition, and delegates
  rendering. Owns the node's selection highlight, click/double-click handlers,
  and drag handle.
- **CanvasContainer** — renders a node that `CanContainChildren`. Wraps
  `CanvasNode` for the parent frame, then iterates `node.Children` and renders
  `CanvasNode` for each child, separated by drop-zone indicators. The
  `CanvasTree` renders the root as a `CanvasContainer`.

All three use the registry (`IPageEditorDefinitionRegistry`) to resolve
catalog definitions, interaction capabilities, and property editors. The
rendering code never checks concrete catalog IDs.

### New Architecture Migration Plan

Because the product is not in production, we should move intentionally toward
the new architecture rather than preserving the legacy storage shape forever.
The migration should still be staged so that already-working editor behavior is
not destabilized by a ground-up state rewrite.

1. **Consolidate the catalog contracts.** Reuse
   `IPageEditorCatalogDefinition`, `INeoNodeFactory`,
   `ICompositionCapabilities`, and `INeoNodeBlockMapper` as the current
   interface layer. Add comments and thin base classes so each concrete
   primitive/container/block has a traceable interface -> base -> concrete
   path.
2. **Register remaining legacy blocks.** Each switch fallback in
   `CreateBlock`, `MapEditorBlockToNeoNode`, `EditorBlockMapper`, or preview
   selection should become a catalog definition or transitional adapter entry.
   The `EditorBlockMapper` alias cases are now covered by
   `LegacyPageEditorBlockProvider`; UI/editor switches remain migration targets.
3. **Add interaction capabilities.** Introduce
   `EditorInteractionCapabilities` for selectable/editable/draggable/delete
   menu behavior without mixing action flags into `EditorCapabilitySet`.
4. **Create a single canvas node renderer.** Root blocks, primitives,
   containers, rows, columns, and custom components should all render through
   one `CanvasNode`/`CanvasContainer` path.
5. **Centralize context menus and actions.** Add an
   `IEditorNodeActionProvider` that returns capability-aware actions for any
   selected node. Double-click Edit and context-menu Edit must call the same
   command/modal path.
6. **Route mutations through commands and policy.** Add/move/delete/duplicate,
   row/column changes, custom insertion, media selection, and property changes
   should dispatch commands validated by `ICompositionPolicy`.
7. **Migrate to `CompositionNodes` incrementally.** Port one canned block at a
   time so it uses a node tree internally while the outer editor can still
   carry an `EditorBlock` shell.
8. **Remove the legacy bridge last.** Replace flat `EditorBlock` page storage
   with direct node-tree editor state only after catalog registration,
   composition migration, preview rendering, save/load, undo/redo, clipboard,
   media selection, custom components, and browser tests are green.

### Seed Data Impact

`src/Aero.Cms.Modules.Setup/SeedDataService.cs` must be updated as part of the
new architecture move:

- Seed pages should create `NeoPageNode` trees directly instead of relying on
  flat `EditorBlock` property bags.
- Seeded examples should include a representative custom composition, a
  Columns/Grid layout, a Container with nested primitives, and at least one
  canned block with named slots when slots are introduced.
- Seeded examples must include LTR and RTL culture variants.
- Seed reset should produce deterministic node IDs only where tests need stable
  fixtures; runtime-created page content should still use fresh IDs.

---

## Composition Rules

### `IEmbeddable`

`IEmbeddable` is a small semantic marker for elements that can participate in a composition. It must not contain the complete nesting policy and must not be the only validation mechanism.

Every embeddable canvas participant shares the same interaction contract, whether it is a primitive, container, layout block, composed card, custom component, or a canned block with named slots:

- It can be selected as a first-class node.
- It exposes the same right-click context-menu entry point.
- Context-menu actions are capability-aware, not type-hardcoded.
- Edit, duplicate, copy, paste, delete, save-as-custom, and media actions appear only when the node definition supports them.
- Double-click Edit and context-menu Edit open the same modal for the same target node.
- Parent containers and child nodes both remain individually addressable; selecting a child must not require editing the parent first.

Placement rules belong to catalog definition metadata:

```csharp
public interface ICompositionCapabilities
{
    bool IsEmbeddable { get; }
    bool CanContainChildren { get; }
    IReadOnlySet<NeoPageNodeKind> AllowedChildKinds { get; }
    IReadOnlySet<NeoPageNodeKind> AllowedParentKinds { get; }
    int? MaximumChildren { get; }
    IReadOnlyList<NeoDropZoneDefinition> SupportedDropZones { get; }
}
```

Capabilities belong to immutable catalog definitions, not individual node instances. The registry caches them by `CatalogId`.

For responsive pointer feedback, precompute compatibility by source catalog ID, target catalog ID, and drop-zone ID. TypeScript may use that snapshot for provisional highlighting, but C# `ICompositionPolicy` remains authoritative when an operation is committed.

Examples:

- Text, button, icon, pill, image, and separator are leaf nodes.
- Container, stack, grid, columns, and composed card may contain children.
- A button cannot contain a hero.
- A node cannot be moved into itself or one of its descendants.
- A single-content media slot may set `MaximumChildren = 1`.
- Columns expose a named drop zone for each column.

### Central Policy

Add an `ICompositionPolicy` that validates:

- Add
- Move
- Re-parent
- Paste
- Template insertion
- Load
- Save

The UI may provide immediate visual feedback, but the same policy must validate the final operation in C#.

```csharp
public interface ICompositionPolicy
{
    Result ValidatePlacement(
        NeoPageNode child,
        NeoPageNode? parent,
        string dropZone,
        CompositionTreeContext context);
}
```

Use Aero.Core railway-oriented results and FluentValidation for persisted models.

---

## Editor Definition Proposal

The existing interface layer already uses small purpose-specific contracts. The
canonical versions are in the SOLID Composition Architecture section above:

- **`IPageEditorCatalogDefinition`** — catalog metadata, composition rules, and
  property editor capability groups. See the 12-member canonical version in the
  ["Interface -> Base Class -> Concrete Class"](#interface---base-class---concrete-class)
  section above. Do not add `EditorInteractionCapabilities` or action-related
  members to this interface — keep them in the separate interaction contract.
- **`INeoNodeFactory`** — creates default node instances.
- **`INeoNodeBlockMapper`** — maps between node trees and public block models
  when a block needs persistence/rendering translation.
- **`IPageEditorBlockDefinition`** — transitional legacy adapter target for
  existing canned blocks. New primitives and custom compositions should not
  require round-tripping through the `EditorBlock` property bag.
- **`ICompositionCapabilities`** and **`ICompositionPolicy`** — nesting rules
  and central validation.
- **`IEditorInteractionProvider`** (new, Phase 0.5) — exposes
  `EditorInteractionCapabilities Interaction { get; }` for canvas-level
  interaction behavior. `PageEditorCatalogDefinitionBase` implements this by
  default; legacy adapters can wrap it independently.

Mapper invariant:

```text
ToBlock(ToNode(block)) must preserve every persisted, user-editable property.
```

Generated IDs, timestamps, transient editor state, and documented default normalization are excluded from equality. Every mapper requires a round-trip contract test and must document intentional normalization.

`IPageEditorBlockDefinition` can temporarily adapt existing canned blocks to these contracts. New primitives and custom compositions should not require round-tripping through the `EditorBlock` property bag.

The registry remains the single lookup path for:

- Display/catalog metadata
- Preview component
- Property editor component
- Composition capabilities
- Editor capabilities
- Node factory
- Persistence mapper

`EditorCapabilitySet` initially declares capability groups rather than individual UI controls:

```csharp
[Flags]
public enum EditorCapabilitySet
{
    None = 0,
    Content = 1 << 0,
    Typography = 1 << 1,
    Spacing = 1 << 2,
    Dimensions = 1 << 3,
    Layout = 1 << 4,
    Alignment = 1 << 5,
    Foreground = 1 << 6,
    Background = 1 << 7,
    Border = 1 << 8,
    Effects = 1 << 9,
    Media = 1 << 10,
    Icon = 1 << 11,
    Link = 1 << 12,
    Visibility = 1 << 13,
    Direction = 1 << 14,
    Collection = 1 << 15
}
```

---

## Responsive Style Model

Shared style data must not be stored as arbitrary Tailwind class strings.

```csharp
public sealed class ResponsiveNodeStyle
{
    public NodeStyle Base { get; set; } = new();
    public NodeStyleOverride? Tablet { get; set; }
    public NodeStyleOverride? Mobile { get; set; }
}
```

`NodeStyle` should cover:

- Logical margin and padding using block-start, block-end, inline-start, and inline-end
- Width, height, min/max dimensions
- Display and visibility
- Flex/grid layout and alignment
- Typography
- Text and background color
- Linear/radial gradients and stops
- Background image and positioning
- Border width/style/color/radius
- Box shadow
- Opacity
- Responsive grid span

Lengths use a validated value object:

```csharp
public enum CssLengthUnit
{
    Pixels,
    Percent,
    Rem,
    Em,
    ViewportWidth,
    ViewportHeight,
    Auto
}

public readonly record struct CssLength(decimal? Value, CssLengthUnit Unit);
```

Arbitrary `calc()` expressions are excluded from the first implementation. They complicate validation, responsive editing, and safe rendering and can be added later through a constrained expression model.

Inheritance:

1. Base/Desktop supplies defaults.
2. Tablet inherits Base and stores only explicit overrides.
3. Mobile inherits Tablet, then Base, and stores only explicit overrides.
4. The UI shows whether a value is inherited or overridden.
5. Users can reset an override to inherited.

An `INodeStyleRenderer` Strategy converts normalized styles into sanitized CSS or an approved Tailwind utility set. Renderers must not accept unvalidated arbitrary CSS or class strings.

---

## Localization, Globalization, and Direction

The editor and rendered content must support localized sites and both LTR and RTL layouts from the first implementation phase.

### Editor Localization

- All editor labels, palette categories, dialogs, context menus, validation messages, empty states, tooltips, and accessibility labels use `IStringLocalizer`.
- Package-owned block definitions provide localizable display names and descriptions rather than treating English strings as permanent identifiers.
- Catalog IDs, property keys, and persisted enum values remain culture-invariant.
- User-authored custom component names and descriptions are site content and may be localized according to the site's localization model.
- Do not build UI text through string concatenation where word order may differ between languages.

### Culture-Aware Values

Editor controls must parse and format values using the active authoring culture:

- Numbers and decimal separators
- Dates and times
- Currency
- Percentages
- Measurement display

Persist normalized invariant values. Culture-specific formatting belongs to editor controls and renderers, not persisted numeric strings.

### Direction Model

```csharp
public enum ContentDirection
{
    Inherit,
    LeftToRight,
    RightToLeft
}
```

Direction may be inherited from the current site/culture and explicitly overridden where a mixed-direction content region requires it.

The canvas preview direction is independent from the manager shell direction. An editor using an LTR manager UI must still be able to preview an Arabic page accurately, and the reverse must also work.

### Logical Layout

Use logical layout concepts in normalized styles:

```csharp
public sealed class LogicalSpacing
{
    public CssLength? BlockStart { get; set; }
    public CssLength? BlockEnd { get; set; }
    public CssLength? InlineStart { get; set; }
    public CssLength? InlineEnd { get; set; }
}
```

Prefer:

- `inline-start` / `inline-end` over left/right
- `block-start` / `block-end` over top/bottom where direction or writing mode matters
- `start` / `end` alignment over left/right alignment
- Mirrored directional icons where their meaning depends on direction

The style renderer emits logical CSS properties whenever possible. Tailwind mappings must use logical utilities or generated CSS that preserves RTL behavior.

### Bidirectional Interaction

The following must work in both LTR and RTL:

- Palette and canvas layout
- Nested Sortable behavior
- Drop indicators
- Columns and grid ordering
- Context menus and toolbars
- Resize handles
- Inline editing toolbars
- Keyboard navigation and focus order
- Start/end alignment controls
- Directional icons and chevrons
- Gradient angle editing and background positioning

Drag calculations must use physical pointer coordinates internally while applying semantic start/end placement according to the active canvas direction.

### Localized Page Content

The composition model must fit AeroCMS's existing localized page/culture ownership. A composition edited for one culture must not silently overwrite another culture's content.

Shared custom component templates are site-owned. Whether their text content is:

- copied into each culture,
- localized per culture,
- or treated as culture-invariant

must follow the site's existing content localization workflow. The first implementation should clone template content into the active culture and avoid automatic cross-culture synchronization.

The existing page culture-variant and AI translation workflows must understand the new node schema. Definitions identify which properties are:

- Translatable text
- Culture-invariant style/layout
- Culture-invariant catalog metadata
- URLs or media references requiring an explicit translation policy

Commands and mementos operate only on the active culture's editor session. Translation must never send style values, catalog IDs, enum values, or internal node IDs as natural-language fields.

---

## Editor Session Ownership

`EditorCanvasState` is an explicit, disposable page-editor session object. It is not an Orleans grain and is not an unconstrained application-wide mutable scoped service.

- Created when a page/culture editor opens
- Owned by `PageEditor` or a dedicated `IEditorSession` facade
- Passed explicitly to commands
- Replaced when navigating to another page or culture
- Disposed when the editor closes
- Never shared across browser tabs or MAUI WebViews

This ownership model behaves consistently in Interactive Server, WebAssembly, and MAUI Hybrid, where dependency-injection scope lifetimes differ.

The session owns:

- Active page and culture identity
- Composition/canned-block canvas state
- Selection and active breakpoint
- Undo/redo history
- Clipboard scope
- Cached compatibility/drop-zone snapshot

---

## TypeScript and Blazor Boundary

Blazor owns durable editor state. TypeScript/Alpine.js owns transient DOM and pointer interaction.

Use an editor-local TypeScript module imported through `IJSObjectReference`. The boundary is operation-based:

1. Blazor sends a serializable interaction snapshot containing node IDs, rectangles, direction, and compatible drop zones.
2. TypeScript handles pointer movement, drag ghost, hover highlighting, and resize previews without per-frame .NET calls.
3. TypeScript commits one typed operation such as `MoveNode`, `ResizeNode`, or `CommitInlineEdit`.
4. Blazor validates the operation through `ICompositionPolicy`, executes an `IEditorCommand`, rerenders, and returns the updated snapshot.

Do not use a global browser event bus or call .NET once per pointer-move event.

---

## Property Editor UX

Every block and primitive uses one modal shell with:

- **Content:** definition-owned fields and collection editors.
- **Design:** shared controls selected from `EditorCapabilitySet`.
- **Advanced:** optional definition-owned controls.

Double-click opens this modal. Right-click shows a context menu with Edit plus the relevant canvas actions.

The right-click context menu is part of the embeddable-node contract, not a legacy block-only feature. It must work consistently for:

- Root canvas blocks.
- Nested primitives.
- Containers, rows, columns, grids, and named slots.
- Custom component instances.
- Canned blocks that opt into composition through named slots.

The menu contents are computed from catalog capabilities and current selection state. Invalid actions are hidden or disabled with clear feedback, rather than silently doing nothing.

The Design tab does not show every possible field for every node. Definitions declare supported capabilities. For example:

- Text supports typography but not media playback.
- Image supports media, dimensions, border, and object fit.
- Container supports layout, spacing, backgrounds, borders, and dimensions.
- Button supports typography, icon, link, spacing, colors, borders, and states.

Reusable editor controls:

- `ResponsiveValueEditor<T>`
- `SpacingEditor`
- `ColorPicker`
- `GradientBuilder`
- `IconPicker`
- `MediaField`
- `UrlPicker`
- `SelectField`
- `BorderEditor`
- `ShadowEditor`
- `CollectionEditor<T>`
- `DirectionEditor`
- Culture-aware numeric, date, currency, and percentage fields

`CollectionEditor<T>` supports add, remove, collapse/expand, and drag reorder.

Collection drag handles must use a distinct Sortable group and stop propagation so modal collection reordering cannot trigger canvas dragging.

Do not remove switch-based fallbacks until every currently supported block has registry-based editor and preview coverage.

---

## Undo/Redo Design

Use **Command + Memento**.

Memento alone would require frequent full-canvas snapshots and would not represent user intent. Command alone makes complex inverse operations such as re-parenting and multi-property responsive edits fragile.

### Command

Each committed editor action is an `IEditorCommand`:

- Add node
- Remove node
- Move/reorder node
- Re-parent node
- Duplicate node
- Edit property
- Apply style
- Resize
- Paste subtree
- Insert custom component

```csharp
public interface IEditorCommand
{
    string Description { get; }
    Result Execute(EditorCanvasState state);
    Result Undo(EditorCanvasState state);
}
```

### Memento

Commands store before/after mementos for the affected subtree and relevant UI state:

- Affected parent/subtree
- Selection
- Active breakpoint
- Expanded container state where needed

Pointer moves, typing, resize events, and inline editing are coalesced into one history entry per user gesture or editing session.

History is bounded by:

- Maximum entry count
- Estimated serialized size
- Optional subtree compression

The undo stack is editor-session state and is not persisted with the page.

---

## Custom Components

Any valid selected custom composition subtree may be saved from the context menu using **Save as Custom Component**.

The user supplies:

- Name
- Optional description
- Optional category/tags
- Optional preview image later

The saved template is site-owned and stores:

- Site ID
- Snowflake ID
- Name and metadata
- Schema version
- Root `NeoPageNode` subtree
- Responsive style data
- Referenced catalog IDs

Inserting from the Custom palette deep-clones the tree and generates new node IDs. Editing the inserted instance does not mutate the saved template.

Updating all existing instances from a template is not part of this refactor.

Export/import is deferred to the final portability phase.

---

## Implementation Phases

### Phase 0.5 - Catalog Consolidation Before State Replacement

This phase is required before adding more WYSIWYG surface area. AeroCMS is not
in production, so the editor can still move to the final node-tree architecture,
but the next step should be additive and test-friendly. Replacing
`List<EditorBlock>` with a root `NeoPageNode` tree immediately would break too
many save/load, undo/redo, clipboard, custom component, media, preview, and
browser-test paths at once.

1. Document the existing contracts with XML comments:
   `IPageEditorCatalogDefinition`, `INeoNodeFactory`,
   `INeoNodeBlockMapper`, `ICompositionCapabilities`, `ICompositionPolicy`,
   `CompositionMutation`, `EditorNodeMemento`,
   `LegacyPageEditorDefinitionAdapter`, and the registry/descriptor types.
2. Add thin abstract bases that make each family traceable:
   interface -> base class -> concrete implementation. Start with primitives
   and containers, then port canned blocks.
3. Register every remaining legacy switch-case block in the catalog. Each
   fallback in `CreateBlock`, `MapEditorBlockToNeoNode`,
   `EditorBlockMapper`, and preview/editor selection is a migration target.
   The save/publish mapper alias set is now registered through the transitional
   `LegacyPageEditorBlockProvider`.
4. Add `EditorInteractionCapabilities` and a centralized action-provider
   contract. Keep it separate from `EditorCapabilitySet`, which remains about
   property editor groups.
5. Replace separate root/nested/row/custom context-menu code with one
   capability-aware action pipeline.
6. Move root blocks, primitives, containers, rows/columns, and custom
   components toward one `CanvasNode`/`CanvasContainer` renderer path.
7. Migrate legacy/canned blocks to populate `CompositionNodes` one block at a
   time. When a block is fully migrated and tested, remove its switch fallback.
8. Replace flat `EditorBlock` page editor state with direct `NeoPageNode`
   trees only after the catalog path, composition path, and automated tests
   prove the old property bag is dead-code-eligible.
9. Update `SeedDataService.cs` after the state model is settled so seeded pages
   are authored in the same architecture as the editor.

**Bridge note:** Phases A through G were defined before Phase 0.5 was inserted.
Many Phase A items (composition policy, responsive styles, direction, editor
session, undo/redo, history) have been substantially implemented during the
earlier phases. Phase 0.5 completes the catalog consolidation; Phases A–G
should be re-assessed against the current codebase after Phase 0.5 stabilizes.

### Phase A - Contracts, Styles, and History

1. Add composition capability types and `ICompositionPolicy`.
2. Add typed responsive style models and validation.
3. Add `ContentDirection`, logical spacing/alignment types, and direction inheritance.
4. Define localization contracts for catalog metadata, editor labels, and custom component metadata.
5. Add registry-owned node factory and bidirectional mapper contracts.
6. Add `EditorCanvasState`, including active culture and preview direction.
7. Add Command plus Memento history with coalescing and bounded storage.
8. Add unit tests for cycles, invalid parents, drop-zone cardinality, style inheritance, direction inheritance, culture-invariant persistence, mapping, and undo/redo.
9. Define the editor-session lifetime and TypeScript operation protocol.
10. Add translatable-property metadata for the existing culture-variant and AI translation workflows.
11. Define deterministic editor test fixtures and reset APIs for later Playwright tests.

### Phase B - Sortable Proof and Primitive Vertical Slice

1. Audit NeoUI Sortable for root reorder, cross-container transfer, nested sorting, cancellation, and rapid pointer movement.
2. Fix columns add/remove/resize and child drop zones.
3. Add the Primitives palette.
4. Implement the initial primitives:
   - Container/Stack
   - Text/Heading
   - Button
   - Image
   - Pill/Badge
   - Icon
   - Separator
   - Card/container preset
5. Add global palette search by name, category, tag, and kind.
6. Build a custom card from primitives.
7. Verify the card in representative LTR and RTL cultures.
8. Verify edit, nest, reorder, duplicate, delete, undo/redo, save, reload, preview, and public rendering.

Discover the exact NeoUI primitive inventory during this phase. Wrap existing NeoUI components where suitable. Create thin Aero-owned primitive renderers where NeoUI has no appropriate component.

### Phase C - Property Editor System

1. Build the Content/Design/Advanced modal shell.
2. Build responsive shared design controls.
3. Add culture-aware value editors and direction controls.
4. Localize all shared editor UI and require package-owned editor localization.
5. Wire double-click and right-click Edit universally.
6. Add context actions: Edit, Duplicate, Delete, Move, Copy, Paste, Save as Custom Component.
7. Complete one canned hero editor as the reference implementation.
8. Complete the primitive vertical-slice editors.
9. Scale registered content editors across Aero UX and Hyper families.
10. Remove editor/preview switches only after registry parity is verified.
11. Prototype named slots on one non-production hero fixture to validate the contracts without making slots part of the production vertical slice.

### Phase D - Mixed Canvas and Persistence

1. Render existing canned blocks and `NeoCompositionBlock` entries together.
2. Render custom composition internals recursively.
3. Route all mutations through commands and `ICompositionPolicy`.
4. Persist the chosen pre-production model directly.
5. Update `SeedDataService.cs` and sample pages.
6. Reset development data rather than building a production migration.
7. Verify seeded pages can be edited, previewed, saved, published, and publicly rendered.
8. Add site-owned Custom palette persistence and CRUD.
9. Seed at least one LTR and one RTL localized page/composition for repeatable testing.

### Phase E - Canned Block Slots

Canned blocks remain atomic through Phases A-D.

Add controlled composition to selected canned blocks using named slots:

- `actions`
- `media`
- `header`
- `body`
- `footer`

Each slot declares:

- Accepted child kinds/catalog IDs
- Minimum/maximum children
- Ordering rules
- Fallback content

The canned block renderer owns slot placement. Users cannot insert children outside declared slots.

Prove slots with one hero and one canned card before expanding.

### Phase F - WYSIWYG Polish

1. Alpine.js/TypeScript inline text editing with a floating toolbar.
2. Resize handles that update the active responsive breakpoint.
3. High-quality drag ghost and drop indicators.
4. Desktop/tablet/mobile canvas controls.
5. Inherited/overridden style indicators.
6. Keyboard shortcuts for undo/redo, delete, duplicate, copy/paste, and palette search.
7. Collection-item drag reorder.
8. Accessibility review for keyboard navigation, focus management, context menus, dialogs, and screen-reader labels.
9. Large-canvas profiling and lazy rendering where necessary.
10. Bidirectional visual review for toolbars, handles, icons, gradients, menus, and nested drag/drop.

### Phase G - Custom Component Portability

After the refactor is stable:

1. Export site-owned custom components as versioned JSON.
2. Import with schema validation and dependency checks.
3. Report missing/unsupported catalog definitions.
4. Generate new IDs and assign ownership to the target site.
5. Never import executable scripts or unsafe raw HTML silently.

---

## Suggested Component Map

```text
PageEditor/
├── PageEditor.razor
├── PageEditor.razor.cs
├── Canvas/
│   ├── CanvasTree.razor
│   ├── CanvasNode.razor
│   ├── CanvasContainer.razor
│   ├── CanvasDropZone.razor
│   └── CanvasDropIndicator.razor
├── Palette/
│   ├── PageEditorPalette.razor
│   ├── PageEditorPaletteSection.razor
│   └── SidebarBlockItem.razor
├── Properties/
│   ├── BlockEditorModal.razor
│   ├── StyleEditorComponent.razor
│   ├── ResponsiveValueEditor.razor
│   ├── SpacingEditor.razor
│   ├── ColorPicker.razor
│   ├── GradientBuilder.razor
│   ├── IconPicker.razor
│   ├── CollectionEditor.razor
│   └── MediaField.razor
├── ContextMenu/
│   └── EditorNodeContextMenu.razor
├── Services/
│   ├── EditorCanvasService.cs
│   ├── CompositionPolicy.cs
│   ├── EditorHistoryService.cs
│   ├── EditorClipboardService.cs
│   ├── CustomComponentService.cs
│   └── NodeStyleRenderer.cs
└── History/
    ├── IEditorCommand.cs
    ├── EditorMemento.cs
    └── Commands/
```

Keep block-specific editors, previews, renderers, and definitions in the package that owns the block.

---

## Verification

### Architecture

- [ ] Registry is the single definition lookup path.
- [ ] Definitions follow the interface -> base class -> concrete class model.
- [ ] `IEmbeddable`, container/component definitions, and canned
  block definitions share the same interaction contract.
- [ ] Context-menu actions are produced by one capability-aware action provider.
- [ ] Root blocks, nested primitives, rows/columns, containers, and custom
  components render through one canvas-node path.
- [ ] New primitives do not require properties added to `EditorBlock`.
- [ ] Flat `EditorBlock` storage is removed after the new node-tree editor
  state is in place.
- [ ] Invalid nesting and cycles are rejected centrally.
- [ ] All composition mappings are bidirectional.
- [ ] Responsive styles are typed and validated.
- [ ] Spacing and alignment use logical block/inline concepts.
- [ ] Direction and active culture are explicit editor state.
- [ ] Renderers sanitize generated style output.
- [ ] Every interface and base class has XML doc comments explaining
  responsibility, final-or-transitional status, GoF pattern, expected
  concrete classes, and owning service.

### Vertical Slice

- [ ] Build a responsive card from primitives.
- [ ] Nest text, image, button, icon, and pill inside valid containers.
- [ ] Reject illegal nesting with clear feedback.
- [ ] Reorder and re-parent without flicker or lost state.
- [ ] Undo and redo every operation.
- [ ] Save, reload, preview, publish, and publicly render the same composition.
- [ ] Repeat the vertical slice in representative LTR and RTL cultures.

### Editors

- [ ] Double-click and context-menu Edit open the same modal.
- [ ] Content/Design/Advanced tabs show appropriate controls.
- [ ] Desktop, tablet, and mobile values inherit and reset correctly.
- [ ] Direction inherits from the active site culture and supports explicit overrides.
- [ ] Culture-aware values display locally and persist invariantly.
- [ ] Canned block editors expose their real collections and properties.
- [ ] Registry fallbacks are removed only after parity tests pass.

### Custom Components

- [ ] Save any valid composed subtree with a name.
- [ ] Saved items appear under the site-owned Custom palette.
- [ ] Inserting a custom item deep-clones it with new IDs.
- [ ] One site's custom items are not visible to another site.
- [ ] Saving/inserting a custom item does not overwrite another culture's page content.
- [ ] Every embeddable node has a right-click context menu with capability-aware actions.
- [ ] Nested primitive, container, row/column, custom component, and root block context-menu Edit all open the same modal path.

### Seed and Persistence

- [ ] `SeedDataService.cs` uses the new model where appropriate.
- [ ] Seed data includes representative LTR and RTL localized compositions.
- [ ] Seeded pages open correctly in the editor.
- [ ] Development data can be reset and reseeded cleanly.
- [ ] Orleans/API transport includes the new composition/style contracts.
- [ ] System.Text.Json source-generation registrations cover all persisted types.

### Build and Tests

```powershell
dotnet build src/Aero.Cms.Abstractions
dotnet build src/Aero.Cms.Ui.Neo
dotnet build src/Aero.Cms.Shared
dotnet build src/Aero.Cms.Modules.Pages
dotnet build src/Aero.Cms.Modules.Setup
dotnet build src/Aero.Cms.Web
```

Use TUnit for unit tests, Alba for ASP.NET Core integration tests, and Microsoft Playwright for editor interaction tests.

Playwright coverage must include at least:

- An LTR culture such as `en-US`
- An RTL culture such as `ar-SA`
- Desktop, tablet, and mobile viewport modes
- Nested drag/drop, context menus, dialogs, inline editing, resize handles, and keyboard navigation in both directions

Playwright tests require deterministic setup:

- A test-only seed/reset path creates a known site, cultures, page, and composition.
- Each test starts from a known persisted page or imports a fixed editor fixture.
- Tests must not depend on execution order or content left by a previous test.
- Generated IDs are returned to the test rather than discovered through timing-sensitive UI searches.

---

## Risks

| Risk | Mitigation |
|---|---|
| NeoUI Sortable cannot support nested named drop zones smoothly | Prove the vertical slice before scaling; extend the local wrapper if required |
| Capability checks enter the pointer-event hot path | Cache immutable definition capabilities and send precomputed compatibility snapshots to TypeScript |
| Illegal trees or cycles | Central composition policy on every mutation, load, and save |
| Blazor and TypeScript state compete | Blazor owns durable state; TS/Alpine owns transient pointer/DOM interaction and commits explicit operations |
| Editor state leaks between pages, cultures, or hosting models | Use an explicit disposable editor session owned by the page editor |
| Bidirectional mapping loses block data | Enforce round-trip invariants with mapper contract tests |
| History consumes WebView memory | Bounded entries, subtree mementos, command coalescing, and size accounting |
| Responsive controls become inconsistent | One typed style model and one renderer strategy |
| RTL is added after physical left/right styles spread | Logical style properties and direction-aware controls are required in Phase A |
| Localized editor UI falls back to embedded English | `IStringLocalizer` for shared and package-owned UI plus localization verification |
| Culture-formatted strings corrupt persisted values | Persist normalized invariant values and format only at UI/render boundaries |
| Style output permits unsafe values | FluentValidation, allowlists, sanitization, and no arbitrary class/script input |
| Number of property editors grows with each new block | Prove one vertical slice, then group shared editor families. Delay definition-owned editors until catalog parity is reached. |
| Collection drag events conflict with canvas drag events | Separate Sortable groups, dedicated handles, and event propagation tests |
| Orleans grain state versioning during flat→tree migration | Add a version field to grain state; deserialize with fallback to the old flat `EditorBlock` shape during the transition window. Remove fallback after migration is certified. |
| New persistence model breaks sample content | Update `SeedDataService.cs`, reset development data, and test seeded edit/publish flows |
| Playwright editor tests become flaky | Deterministic test reset/seed APIs and fixed composition fixtures |
| Large canvases become slow | Measure in MAUI WebView; lazy render and reduce unnecessary Blazor rerenders |

---

## Deferred

- Updating existing instances when a custom template changes
- Custom component export/import until Phase G
- Collaborative editing
- Marketplace/shared templates
- AI-generated compositions
- A/B testing
- Cross-site custom component sharing
