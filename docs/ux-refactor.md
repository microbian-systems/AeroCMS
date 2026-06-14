# WYSIWYG Page Editor UX Refactor

**Status:** In progress  
**Last modified:** 2026-06-14 (policy note added)  
**Author:** AI-assisted planning session

---

## Implementation Progress

**Last updated:** June 14, 2026

**Estimated core-refactor completion:** 96%

This estimate covers the required core refactor through mixed persistence,
site-owned Custom components, and browser verification. Later named slots,
advanced WYSIWYG polish, and component portability are not included.

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
- The palette has no global search.
- Shared color, spacing, gradient, icon, media, border, and responsive controls are missing.
- `EditorBlock` is a large property bag.
- `PageEditor.razor.cs` owns too many responsibilities.
- Inline editing, context menus, responsive overrides, and polished pointer interactions are incomplete.

---

## Composition Rules

### `IEmbeddable`

`IEmbeddable` may be introduced as a small semantic marker for elements that can participate in a composition. It must not contain the complete nesting policy and must not be the only validation mechanism.

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

The existing interface mixes catalog metadata, `EditorBlock` creation, node conversion, and persistence conversion. Move toward small purpose-specific contracts:

```csharp
public interface IPageEditorCatalogDefinition
{
    string CatalogId { get; }
    string DisplayName { get; }
    string Category { get; }
    NeoPageNodeKind Kind { get; }
    Type? PreviewComponentType { get; }
    Type? PropertyEditorComponentType { get; }
    ICompositionCapabilities Composition { get; }
    EditorCapabilitySet EditorCapabilities { get; }
}

public interface INeoNodeFactory
{
    NeoPageNode CreateDefaultNode();
}

public interface INeoNodeBlockMapper
{
    BlockBase ToBlock(NeoPageNode node);
    NeoPageNode ToNode(BlockBase block);
}
```

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
- [ ] New primitives do not require properties added to `EditorBlock`.
- [ ] Invalid nesting and cycles are rejected centrally.
- [ ] All composition mappings are bidirectional.
- [ ] Responsive styles are typed and validated.
- [ ] Spacing and alignment use logical block/inline concepts.
- [ ] Direction and active culture are explicit editor state.
- [ ] Renderers sanitize generated style output.

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
| 80+ property editors dominate development time | Prove one vertical slice, then group shared editor families |
| Collection drag events conflict with canvas drag events | Separate Sortable groups, dedicated handles, and event propagation tests |
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
