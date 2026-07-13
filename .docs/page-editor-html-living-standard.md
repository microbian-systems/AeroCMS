# Page Editor: HTML Living Standard Redesign

## Status: Accepted Architecture

## Context

The existing PageEditor is based on NeoUI blocks, `NeoPageNode`, and
`ResponsiveNodeStyle`. Those types attempt to persist the CSS box model,
breakpoints, pseudo-states, renderer choices, and editor state as one object
graph. That is more machinery than a static, Wix/Squarespace-style page
builder needs, and it has made the document difficult to persist through
Sable/SurrealDB.

This redesign is a clean pre-production break. Existing Neo page content is
not a compatibility target.

## Current Coupling Findings

The current persistence issue is architectural rather than a SurrealDB nesting
limit. The embedded-SurrealDB regression test proves that one Sable root
document with deeply nested, plain HTML-node POCOs round-trips under
`SchemaMode.Flexible`.

The existing page feature instead persists and processes Neo content through
several overlapping paths:

- `PageDocument.RootNodes` stores `List<NeoPageNode>`, while
  `PageCompositionDocument.RootNodes` stores a second persisted composition
  tree.
- `LayoutRegions`, `BlockIdMap`, draft/published composition IDs, and block
  schema/version state preserve additional legacy and composition paths.
- Page save logic branches between `RootNodes` and `LayoutRegions`, deep-clones
  nodes through `EditorNodeMemento`, and creates composition events.
- Separate page and composition projections process overlapping content events.
- Public rendering flows through Neo renderers, composition bridges, block
  render caches, and layout-region fallbacks.
- The editor uses Neo catalog, block-provider, composition-policy, and Sortable
  abstractions across a large PageEditor component family.

The refactor removes those parallel content paths in favor of one HTML tree,
one tracked save path, one rendering path, and one undo/redo history. Before
deleting any shared block type, renderer, or service, perform a reference
inventory: only PageEditor-specific Neo code is in this replacement scope.

## Decision

Pages are stored as one simple, recursive HTML fragment tree. Structure,
styling, rendering, validation, editor interaction, and browser drag handling
have separate responsibilities.

```text
PageDocument
  ├─ DraftContent: HtmlPageContent
  └─ PublishedContent: HtmlPageContent?
      └─ HtmlNode (root fragment)
          ├─ NodeId              editor identity, never emitted as HTML by default
          ├─ Kind                Fragment, Element, or Text
          ├─ Tag                 section, div, button, ...
          ├─ Attributes          allowed HTML attributes
          ├─ Style               framework-neutral style intent
          ├─ ThemeClasses        optional advanced theme-specific override
          ├─ Text                literal text when Kind is Text
          └─ Children[]          ordered recursive children
```

`PageDocument` remains the page aggregate and continues to own page metadata,
routing, publication, auditing, and site ownership. Its Neo layout,
composition, block-map, and Neo-version fields will be removed as the new
content property takes their place.

## Project Placement

Create a small CMS-specific `src/Aero.Cms.Html/` class library. It has one
intentional one-way dependency on `Aero/Aero.Core` for the existing
`Result<T>` and `Option<T>` railway-oriented types, and is referenced by
`Aero.Cms.Core.Entities`, `Aero.Cms.Modules.Pages`, and `Aero.Cms.Shared`.
This prevents a circular dependency: Pages already references Shared, so the
PageEditor cannot consume implementations placed only in Pages.

The HTML foundation remains CMS-specific and does not belong in the generic
`Aero` submodule or in `Aero.Cms.Abstractions`: the manifest, tree operations,
content-model policy, and style contracts are concrete CMS behavior. It is
deliberately separate from the Pages module because both the browser editor and
the server page module require the same model, catalog, tree operations,
content rules, and style contracts.

```text
Aero.Cms.Html/
  HtmlPageContent.cs
  HtmlNode.cs
  HtmlNodeKind.cs
  HtmlStyle.cs
  HtmlTag.cs
  HtmlElementDefinition.cs
  IHtmlContentValidator.cs
  IHtmlContentModelPolicy.cs
  HtmlTreeIndex.cs
  HtmlTreeOperations.cs
  IStyleCompiler.cs
  IStyleProfile.cs
  HtmlElementManifest.json

Aero.Cms.Modules.Pages/
  HtmlRenderer.cs
  Publishing, persistence, and server-side validation adapters
  Public-page render host and page style output integration

Aero.Cms.Shared/Pages/Manager/PageEditor/
  Canvas and property-panel components
  Commands, mementos, and editor history
  IPageEditorSortable.cs
  PageEditorSortableInterop.cs
```

## Model Rules

### Structure

- Persist one concrete `HtmlNode` type, not a polymorphic hierarchy of 115 C#
  element classes.
- The page root has `Kind = Fragment`, no tag or text, and ordered children.
- A text node has `Kind = Text`, `Text`, and no tag or children.
- An element node has `Kind = Element`, a lower-case tag name, and ordered
  children when the element is not void.
- `NodeId` is a stable editor-only identity for selection, move commands, and
  undo/redo. It is distinct from the optional HTML `id` attribute.
- Void-element and parent/child rules are enforced by validation and catalog
  metadata, not C# generic child interfaces.

### Tree Semantics and Operations

The HTML structure is a rooted, ordered tree, not a DAG. Every node has one
structural parent and an ordered `Children` collection. Attributes such as
`href`, `for`, ARIA references, and HTML `id` values can refer to other
content, but they are not structural edges.

The recursive `Children` collection is the persisted adjacency list. It is
sufficient for all editor operations:

- Depth-first traversal renders HTML and locates a selected `NodeId`.
- A transient `HtmlTreeIndex` maps `NodeId` to its parent and child index for
  efficient selection, move, and delete operations. Parent pointers and paths
  are not persisted redundantly.
- Move removes a node from one parent collection and inserts it into a validated
  target parent collection at an explicit index.
- Delete removes a subtree; duplicate deep-clones it and assigns new `NodeId`
  values.
- Commands capture a complete `HtmlPageContent` memento before each meaningful
  mutation. Full-tree snapshots are the initial undo/redo implementation;
  inverse-operation optimization is deferred until profiling demonstrates a
  need. Text and slider edits are debounced/coalesced into one history entry,
  and history has explicit entry and memory limits.

### Attributes and Styling

Styling is separate from HTML structure and is not Tailwind-only. The content
model stores a deliberately constrained, framework-neutral `HtmlStyle` intent.
The active site style profile compiles that intent into framework classes or
scoped native CSS.

```text
HtmlNode structure
  → HtmlStyle intent
  → site theme/design tokens
  → IStyleCompiler
  → Tailwind classes, Bootstrap classes, or scoped native CSS
```

- Tailwind remains useful for the Aero CMS manager and may be one public-site
  profile, but it is not a core page-content dependency.
- Style intent covers the editor's useful concepts: background image/overlay,
  spacing, typography, surface, display, flex/grid layout, columns, gap, and
  alignment.
- The model must not become another exhaustive CSS object graph. It does not
  persist every CSS property, pseudo-state, or breakpoint override as C# types.
- The style compiler uses a framework class only for an exact mapping;
  otherwise it emits a deterministic scoped native-CSS rule.
- A user-selected framework class is an optional advanced override in
  `ThemeClasses`, persisted with the profile ID and version it targets. It is
  not the normal editor experience.
- Advanced custom declarations use a property/value allow list, safe value and
  URL checks, and generated scoped CSS. If introduced, they live in a scoped
  `PageStyleSheet` value with validation status, not an unrestricted
  `CustomCss` string on `PageDocument`. Do not persist an unrestricted inline
  `style` string. This panel remains unavailable until that validator exists.
- Allow global safe attributes, element-specific safe attributes, `data-*`,
  and `aria-*`. Do not allow event-handler attributes (`onclick`, etc.),
  scripts, custom elements, arbitrary attribute bags, or data binding in v1.

### CSS-Neutral Style Architecture

```text
HTML element model      HtmlNode
CSS/style model         HtmlStyle (small semantic intent)
Theme/design system     IStyleProfile and design tokens
CSS framework adapter   IStyleCompiler strategy
Rendered output         HTML classes plus generated scoped CSS when needed
```

`IStyleCompiler` has implementations such as `NativeCssStyleCompiler`,
`TailwindStyleCompiler`, and `BootstrapStyleCompiler`. Compilation is a
page-level pass, not an independent per-node side effect:

```text
Compile(HtmlPageContent, IStyleProfile)
  → CompiledPageStyles
      NodeClasses
      CssText
      ContentHash
      ProfileId and ProfileVersion

Render(HtmlPageContent, CompiledPageStyles)
  → RenderedHtmlPage
      Markup
      CssText
```

The page-level pass deduplicates generated rules and produces stable scoped
class names. Public rendering and manager preview use the same validated
catalog and style compiler semantics. Public SSR uses an encoded static HTML
writer; the manager preview uses a small recursive Blazor renderer. Both are
covered by shared rendering conformance tests so that they cannot diverge on
tags, attributes, void elements, or compiled styles. The render host owns safe
stylesheet placement and CSP integration.

The property panel exposes semantic controls to ordinary users and reserves
raw classes/custom declarations for an administrator-focused Advanced panel.
For example, a selected section can set background media, overlay, padding,
margin, minimum height, layout mode, columns, gap, and alignment. A selected
heading can set text, typography, color/gradient, width, spacing, and position.

Normal positioning uses the parent container's flex or grid layout. An
advanced free-positioning mode may use a positioned parent plus validated,
responsive absolute offsets and z-index for hero overlays. It is not the
default because normal flow/flex/grid layouts are more robust across devices.

### HTML Scope

Page content is an HTML fragment rendered inside the CMS page shell. It is not
a complete user-authored `<html>`, `<head>`, or `<body>` document. Page title,
SEO metadata, document language, and head content remain PageDocument/CMS-shell
responsibilities.

## HTML Catalog

The HTML Living Standard informs the catalog, but it is metadata rather than a
generated inheritance hierarchy. Each `HtmlElementDefinition` describes the
tag, display label, palette category, allowed attributes, whether it is void,
and allowed child tags. A source generator may later generate this *catalog*
from maintained spec data; it must not generate persisted concrete node types
or custom JSON polymorphism.

### Canonical Element Manifest

Maintain a versioned element manifest as the canonical source for catalog and
validation metadata. The first manifest covers only the first shippable
catalog; it does not attempt full WHATWG coverage before the editor foundation
is proven.

```json
{
  "tag": "section",
  "namespace": "html",
  "paletteCategory": "Structural",
  "isVoid": false,
  "contentModel": "Flow",
  "allowedChildModel": "Flow",
  "attributes": ["global.id", "global.title", "global.aria-*", "global.data-*"],
  "styleCapabilities": ["layout", "spacing", "surface", "typography"]
}
```

The manifest drives `HtmlElementDefinition` entries, palette labels and
categories, node-factory defaults, valid drop zones, child-nesting validation,
element-specific property panels, and generated conformance tests. It is the
place to compare the supported catalog with the Living Standard over time.

Global and element-specific attributes are separately declared descriptors,
not merely strings. A descriptor supplies value type, editor control, required
status, allowed values, numeric limits, and URL policy. This lets property
panels, import validation, and rendering share one contract.

`HtmlTag` is a validated string value/static-constant catalog, not a closed
enum, so later manifests and approved extensions do not require changing the
persisted node representation.

The manifest does **not** generate `ArticleElement`/`ImgElement` classes,
category interfaces, generic child containers, or element-specific JSON
serialization. Every persisted page still uses the one concrete recursive
`HtmlNode` model.

After the manifest schema and first catalog stabilize, a source generator may
produce the static `HtmlElementCatalog` and repetitive tests. Until then, a
small manifest loader and direct tests are preferable to prematurely adding
generator complexity. The editor property panel reads the manifest directly;
it does not depend on source generation. SVG and MathML are separate later
manifest namespaces.

### First Shippable Catalog

- Structural: `section`, `div`, `main`, `header`, `footer`, `article`, `nav`,
  `aside`.
- Content: `h1` through `h6`, `p`, `span`, `strong`, `em`, `a`, `button`, and
  text.
- Media: `img`, `figure`, `figcaption`.
- Lists: `ul`, `ol`, `li`, `hr`.
- Layout starters: one column, two columns, three columns, split layout, and
  card grid.
- Guided semantic controls: display, gap, padding, margin, alignment,
  background, and typography.

Tables follow once basic primitives, canvas interaction, persistence, and
rendering are stable. Forms follow as static HTML only; the CMS does not
process submissions in the first release. Iframes, SVG, MathML, dialog,
audio/video, and other embedded or high-risk elements are later catalog phases.

### Layout Is Behavior, Not an Element Category

Any appropriate rendered container can receive flex or grid layout intent.
Grid and flex are neither node kinds nor marker interfaces. The active style
compiler decides whether that intent becomes Tailwind/Bootstrap classes or
native scoped CSS.

The palette exposes layout *starters* that insert ordinary nodes, for example:

| Starter | Inserted structure |
| --- | --- |
| One column | `section > div`, with one-column grid/spacing intent |
| Two columns | `section > div > div + div`, with responsive two-column grid intent |
| Three columns | `section > div > div × 3`, with responsive three-column grid intent |
| Split layout | `section > div > div + div`, with responsive flex-row intent |
| Card grid | `section > div > article × 3`, with responsive grid intent |

Once inserted, every node is a normal editable node. A user can alter its
classes, replace it, add children, or move children within validated nesting
rules.

## Persistence and Validation

`HtmlPageContent` is persisted as part of the single Sable `PageDocument`
record using `SchemaMode.Flexible` (SurrealDB SCHEMALESS). The model is
open-ended by design; strict Surreal schema declarations for every nested
attribute are neither necessary nor desirable.

Application-side validation is the safety boundary:

1. `IHtmlContentValidator` and `IHtmlContentModelPolicy` return the existing
   Aero.Core `Result<T>`/validation report types. They validate tags, allowed
   attributes, void elements, nesting, URL policies, and maximum depth/node
   limits. The server boundary integrates these rules through FluentValidation.
2. The renderer HTML-encodes text and attribute values and emits only validated
   attributes.
3. `HtmlRenderer` is the sole path from the persisted model to public markup.

The Sable regression suite must cover a deeply nested page with sections,
divs, buttons, ordered lists, unordered lists, attributes, and style intents
using the embedded in-memory SurrealDB engine.

### Persistence Lifecycle: Change Tracking, Not Event Sourcing

Page content is not event sourced. The editor does not require replayable page
events, event projections, or separate composition aggregates. Sable
`ChangeTracking()` and optimistic concurrency are the persistence mechanism.

`PageDocument` owns the content lifecycle:

```text
PageDocument
  ├─ DraftContent: HtmlPageContent
  ├─ PublishedContent: HtmlPageContent?
  ├─ ContentRevision
  ├─ PublishedVersion
  └─ PublicationState and audit metadata
```

- Editor saves update `DraftContent` through the normal tracked document save.
- Publish deep-copies `DraftContent` to `PublishedContent`, increments the
  content/published versions, and updates publication and audit metadata.
- Manager rendering uses `DraftContent`; public rendering uses
  `PublishedContent`.
- Optimistic concurrency prevents one editor from silently overwriting another
  editor's changes.
- Client-side Command/Memento history is transient editor state, not an event
  store.
- If product requirements later need user-visible history, write immutable
  `PageRevisionDocument` snapshots on explicit save or publish actions. Do not
  introduce replayable content events for that purpose.

The page builder introduces no replacement page-content event stream. A small
non-replayable integration notification such as `PagePublished` may be added
later if another module needs it, but it must not contain or project content.

Publish is one optimistic-concurrency-protected document mutation: validate the
current draft, deep-copy it to `PublishedContent`, increment versions, and
update publication/audit metadata. Unpublish clears public availability without
destroying the draft or the last published snapshot.

## Editor and Drag/Drop Architecture

Keep the PageEditor shell, toolbar, preview, selection affordances, and modal
workflow. Replace Neo catalog, canvas, block frames, and property panels with
HTML-node equivalents.

### Block-Based UX Through HTML Templates

Blocks remain a user-facing authoring concept, not persisted `BlockBase` or
Neo objects. A curated hero, feature section, FAQ, layout, or future
HyperUI-like component is an `HtmlPageContent`/`HtmlNode` template. Insertion
deep-clones that template, assigns fresh `NodeId` values, and leaves the user
with ordinary editable HTML nodes.

The normal palette begins with sections, layout starters, content, and media.
An Advanced palette exposes individual primitives. Double-click/right-click
editing remains a universal affordance: common semantic controls are shared,
while catalog attribute descriptors select element-specific fields. Existing
PageEditor shell, toolbar, preview overlay, and modal workflow are preserved.

AngleSharp is a later import/conversion tool for approved static HTML
fragments. It parses a fragment into the manifest-validated node model; it is
not the persisted model, renderer, or editor state.

### Rich-Text Editing with Tiptap.Core

The visible rich-text UI is an Aero-owned `RichTextEditor` component in
`Aero.Cms.Shared`, not a stock Tiptap toolbar or a separate editor product. The
local `tiptap-dotnet/` submodule (`Tiptap.Core`) supplies the ProseMirror
document model, schema, parsing, serialization, and sanitization underneath
that UI.

A small TypeScript DOM adapter handles browser-only concerns—selection/ranges,
composition/IME, clipboard, and `contenteditable` events—while C# owns the
toolbar, dialogs, editor commands, schema, validation, and conversion. The
adapter is not a direct dependency on the Tiptap JavaScript editor.

```text
Selected text-capable HtmlNode subtree
  → HtmlNode-to-Tiptap.Core bridge
  → transient ProseMirror document/editor session
  → browser selection/input intent
  → C# editor command + Tiptap.Core schema validation
  → Tiptap.Core-to-HtmlNode bridge
  → manifest/content-model validation
  → one coalesced PageContentCommand + Memento
```

`HtmlPageContent`/`HtmlNode` remains the only persisted page-content model.
Tiptap JSON is transient editor state, never a second persisted document or
parallel source of truth. The initial schema is deliberately small:
paragraphs, headings, lists, links, and basic inline marks. Native browser undo
is scoped to an active text-editing session; PageEditor Memento records one
coalesced completed edit. The editor rejects scripts, event attributes,
arbitrary raw styles, and unsupported nodes before the tree is saved or
rendered.

An HTML `<textarea>` is a literal form-control element: its `value`,
`placeholder`, and related attributes are edited through the normal property
panel, not with a rich-text editor inside the control. Markdown import/export
is a later explicit conversion feature; it must not become another persisted
page format.

### Localization, Direction, and Accessibility

Localization, RTL/LTR, and accessibility are foundation requirements. Page
content retains normalized values, while editor controls perform
culture-appropriate formatting. Style intent and property panels use logical
concepts such as `inline-start`, `inline-end`, `block-start`, and `block-end`,
not hard-coded left/right. The page preview direction is independent of the
manager shell direction. The catalog/validator requires appropriate text
alternatives, labels, heading use, and ARIA attributes where applicable.

### Owned Sortable Boundary

The project owns the sortable abstraction; it does not retain the NeoUI
Sortable dependency and does not adopt Sortable.js. This does **not** mean
reimplementing browser pointer and drag mechanics in C#.

```text
Browser drag
  → PageEditorSortableInterop (small TypeScript module)
  → IPageEditorSortable.Move(drop intent)
  → PageContentCommand
  → IHtmlContentValidator
  → HtmlNode tree mutation
  → Memento snapshot and Blazor rerender
```

- TypeScript determines drag start, drag-over target, insertion position, and
  keyboard-accessible drag intent.
- C# owns all mutation, validation, selection, undo/redo, and persistence.
- The browser reports an intent (`source node`, `target parent`, `index`); it
  never mutates the persisted tree directly.

### Valid Child-Element Policy

The editor prevents invalid HTML nesting through a policy, not through a
persisted inheritance hierarchy. Every `HtmlNode` retains `Children` for a
simple recursive serialization shape; text and void-element nodes must have an
empty collection.

```text
Palette drag/drop
  → IHtmlContentModelPolicy.CanContain(parent, child)
  → permitted drop zone or clear denial reason
  → PageContentCommand validates and mutates
  → HtmlTreeValidator validates again before save/render
```

`IHtmlContentModelPolicy` is a Strategy backed by catalog metadata and focused
rules. It rejects examples such as a `div` or `section` inside `span`, permits
only `li` as direct children of `ul`/`ol`, rejects children of `img`/`br`/
`input`, and applies context-sensitive rules such as preventing nested anchors.
The UI uses the same policy to show only valid drop zones.

### RCL Packaging of Browser Editor Modules

`Aero.Cms.Shared` is already a Razor Class Library and owns the PageEditor, so
no new RCL is needed. The sortable and rich-text DOM adapters are packaged as
RCL static web assets:

```text
src/Aero.Cms.Shared/
  ts/PageEditor/page-editor-sortable.ts
  ts/PageEditor/page-editor-rich-text.ts
  wwwroot/js/page-editor-sortable.js
  wwwroot/js/page-editor-rich-text.js
  Pages/Manager/PageEditor/
    IPageEditorSortable.cs
    PageEditorSortableInterop.cs
    RichTextEditor.razor
    RichTextEditor.razor.cs
```

`Microsoft.TypeScript.MSBuild` compiles the project TypeScript outside
`wwwroot` into `wwwroot/js`. No runtime CDN, npm dependency, or direct Tiptap
JavaScript bundle is required. The RCL static-web-assets manifest makes the
modules available to every consuming ASP.NET Core app at:

```text
/_content/Aero.Cms.Shared/js/page-editor-sortable.js
/_content/Aero.Cms.Shared/js/page-editor-rich-text.js
```

The PageEditor dynamically imports these modules through `IJSRuntime`;
consumers do not copy them or add script tags. `dotnet pack` includes RCL
`wwwroot` assets in the package. The consuming host needs its normal
static-assets/static-files pipeline enabled.

## GoF and SOLID Boundaries

| Concern | Pattern / boundary |
| --- | --- |
| Palette insertion | Factory backed by `HtmlElementDefinition` defaults |
| Move, insert, delete, attribute edits | Command objects |
| Undo/redo | One Memento history for the complete HTML tree |
| Rendering, validation, and child rules | Strategy interfaces (`IHtmlRenderer`, `IHtmlContentValidator`, `IHtmlContentModelPolicy`) |
| CSS/profile translation | Strategy interface (`IStyleCompiler`) |
| Browser integration | Adapters (`PageEditorSortableInterop`, `RichTextEditor` interop) |
| Rich-text document/schema/sanitization | `Tiptap.Core` behind the owned `RichTextEditor` |
| Editor orchestration | Facade over selection, commands, history, and preview refresh |

No pattern should be added merely to represent a single HTML tag. The model is
kept concrete and small so it is straightforward to test and serialize.

## Replacement Scope

Remove from the PageEditor persistence and rendering path:

- `LayoutRegions`
- `RootNodes` / `NeoPageNode`
- block ID maps and composition document pointers
- page-content event streams, composition events, and their projections
- Neo block catalog/definition/mapper/renderer flow
- `ResponsiveNodeStyle` and Neo style validators/renderers
- duplicated Neo composition histories
- NeoUI Sortable components

The complete Neo implementation can be deleted only after no other CMS feature
references it. Do not preserve an adapter or migration path for old page data;
this checkout is pre-production.

## Delivery Sequence

1. Add the `Aero.Cms.Html` foundation, manifest, catalog/policy, tree
   operations, style contracts, and direct unit tests.
2. Replace PageDocument's Neo content fields with draft/published
   `HtmlPageContent`, then add Sable persistence and deeply nested integration
   tests.
3. Implement the tracked draft/published lifecycle with optimistic concurrency,
   then remove page-content events, composition documents, and projections.
4. Implement the page-level renderer/style output, rework `Page.cshtml` and its
   page model to render only published content, and recreate seeded pages using
   the same HTML-template factories.
5. Add editor commands, bounded/coalesced memento history, the palette catalog,
   curated component templates, and layout-starter factories.
6. Replace the PageEditor canvas and property panel while retaining its shell,
   preview, toolbar, and modal layout.
7. Add the owned TypeScript sortable and rich-text DOM adapters in
   `Aero.Cms.Shared`; add the owned `RichTextEditor` over `Tiptap.Core` and
   verify RCL static-asset consumption from the web host.
8. Deliver the first catalog and layout starters; then tables, static forms,
   and later catalog phases.
9. Remove Neo dependencies, registrations, tests, legacy seed data, and unused page
   composition infrastructure.

### Cutover Strategy

Work occurs on `feature/html-page-builder`. Build and verify the new vertical
slice alongside the legacy implementation on that branch, but do not create
compatibility adapters, upcasters, or old-data migration paths. Once the new
save, render, and editor path is complete, remove the PageEditor-specific Neo,
composition, block, and event-projection infrastructure in the same cutover.
The branch may use incremental commits for review and verification; the
product does not carry two page architectures after the cutover.

## Non-Goals for the First Release

- Script execution, event handlers, models, templates, or CMS-side form
  processing.
- Arbitrary inline CSS or a reconstructed CSS object model.
- Full WHATWG element parity on day one.
- Import/migration of existing Neo page documents.
- A third-party page-builder runtime or a NeoUI compatibility layer.

## References

- [WHATWG HTML Living Standard element index](https://html.spec.whatwg.org/multipage/indices.html)
- [Microsoft Learn: RCL static assets and TypeScript](https://learn.microsoft.com/aspnet/core/razor-pages/ui-class?view=aspnetcore-10.0#create-an-rcl-with-static-assets)
