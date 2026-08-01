# Page Editor: HTML Living Standard Redesign

## Status: First-Release Vertical Slice Implemented

The Living Standard page path is implemented end to end: one recursive HTML
tree, tracked draft/published snapshots, static public rendering, the Blazor
WASM editor, owned sortable interop, Tiptap rich-text editing, framework-neutral
style intent, seeded pages, and browser-tested create/save/reload/publish.
Remaining catalog expansion and advanced capabilities are explicitly deferred
below; they are not blockers for the first usable page builder.

Typed content scopes, field bindings, rendered fragments, sidebar tabs, canvas
element actions, and the manager assistant boundary are specified separately in
[Page Editor: Typed Content Composition and Extensibility](page-editor-content-composition.md).

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
  LivingStandard/HtmlSortableInterop.cs
  LivingStandard/TiptapEditorInterop.cs
  LivingStandard/HtmlRichTextEditorDialog.razor
  LivingStandard/HtmlPageEditorSession.cs
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

`IStyleCompiler` has the native implementation `NativeCssStyleCompiler` and
the adapter decorator `FrameworkStyleCompiler`, which accepts either
`TailwindStyleFrameworkAdapter` or `BootstrapStyleFrameworkAdapter`.
Compilation is a page-level pass, not an independent per-node side effect:

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

`FrameworkStyleCompiler` now decorates the native compiler through an
`IStyleFrameworkAdapter` Strategy. The built-in Tailwind and Bootstrap
adapters emit only documented, exact utility mappings. They never emit
arbitrary-value utilities or approximate a framework concept. Any unmatched
intent stays on a cloned residual `HtmlStyle` and is compiled to deterministic
scoped native CSS. Responsive stacking remains native because the site-owned
breakpoint cannot be assumed to equal a framework breakpoint. Native CSS
remains the active default until a site explicitly selects and supplies the
corresponding framework stylesheet/class safelist.

The property panel exposes semantic controls to ordinary users and reserves
raw classes/custom declarations for an administrator-focused Advanced panel.
For example, a selected section can set background media, overlay, padding,
margin, minimum height, layout mode, columns, gap, and alignment. A selected
heading can set text, typography, color/gradient, width, spacing, and position.

Normal positioning uses the parent container's flex or grid layout. An
advanced free-positioning mode may use a positioned parent plus validated,
responsive absolute offsets and z-index for hero overlays. It is not the
default because normal flow/flex/grid layouts are more robust across devices.

### Site-Owned Style Profiles

The native-CSS profile is owned by `SitesModel`, not registered as a process-
wide singleton. Each site persists a strict-schema-safe `StyleProfileSettings`
value containing:

- A monotonically increasing revision used as the profile version.
- The small-screen breakpoint used by responsive style compilation.
- A normalized list of named color tokens.

The manager's Site editor exposes a curated design-system surface for primary
and secondary brand colors, page and card surfaces, primary and muted text,
and the responsive breakpoint. It does not expose raw CSS or require the user
to understand Tailwind, Bootstrap, or framework-specific class names.
Additional advanced tokens are preserved when the simplified form is saved.

`ISiteStyleProfileResolver` validates and resolves the profile for the owning
site at every server compilation boundary: draft validation, publishing,
preview fragments, and public SSR. The WASM PageEditor receives the same
settings through `SiteViewModel` and constructs the same native profile
locally. A dedicated revision-checked endpoint updates the profile; normalized
no-op updates do not increment its revision.

Public output-cache entries vary by origin and carry a
`site-pages-{siteId}` tag. A persisted profile change publishes a non-replayable
integration notification that evicts only the rendered pages for that site.
Semantic page-document cache entries are not evicted because their content
does not contain compiled profile CSS.

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

Manifest version `2026.3` contains 82 elements:

- Structural: `section`, `div`, `main`, `header`, `footer`, `article`, `nav`,
  `aside`.
- Content and semantic text: headings, paragraphs, inline semantics, links,
  buttons, quotations, code/preformatted text, edits, dates, and data values.
- Media: `img`, `picture`, `audio`, `video`, `source`, `track`, `figure`, and
  `figcaption`.
- Lists, disclosure, and dialogs: ordered/unordered/description lists,
  `details`, `summary`, and `dialog`.
- Tables: `table`, `caption`, column groups, header/body/footer row groups,
  rows, and header/data cells.
- Static forms: `form`, `label`, `input`, `textarea`, `fieldset`, `legend`,
  `datalist`, `select`, `optgroup`, `option`, and `output`. The CMS renders
  form controls but does not process submissions in the first release.
- Data indicators: `progress` and `meter`.
- Layout starters: one column, two columns, three columns, split layout, and
  card grid.
- Guided semantic controls: display, gap, padding, margin, alignment,
  background, typography, and element-specific attributes/actions.

Iframes, SVG, MathML, custom elements, and other embedded or high-risk elements
remain later catalog phases.

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

### Persistence Lifecycle: Document Saves, Not Event Sourcing

Page content is not event sourced. The editor does not require replayable page
events, event projections, or separate composition aggregates. Normal Sable
document saves and optimistic concurrency are the persistence mechanism.

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
- Sable's identity-map snapshots are in-memory dirty-checking state and are
  cleared after a successful save. Enabling Sable `ChangeTracking()` would
  create generic audit/changefeed records, but Pages does not currently enable
  or expose that facility as author-facing revision history.
- If product requirements later need user-visible history, write immutable
  `PageRevisionDocument` snapshots on explicit save or publish actions. Do not
  introduce replayable content events for that purpose. Alternatively, first
  evaluate whether a revision browser can be built over Sable's persisted
  changefeed; do not assume its internal session snapshots are durable
  revisions.

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

AngleSharp is the implemented import/conversion tool for approved static HTML
fragments. The import boundary rejects full documents, unsupported syntax,
unsafe attributes, invalid nesting, and content that exceeds the configured
depth/node limits. A successful import converts the fragment into the
manifest-validated node model and commits it as one undoable editor mutation.
AngleSharp is not the persisted model, renderer, or editor state.

### Rich-Text Editing with Tiptap

The visible rich-text UI is an Aero-owned `RichTextEditor` component in
`Aero.Cms.Shared`, not a stock Tiptap toolbar or a separate editor product.
The browser editor uses Tiptap Core directly through the owned
`aero-tiptap-editor.ts` adapter. Tiptap owns ProseMirror DOM concerns such as
selection/ranges, composition/IME, clipboard, and `contenteditable`; Blazor/C#
owns the dialog, command boundary, conversion to `HtmlNode`, content-model
validation, undo/redo integration, and persistence.

```text
Selected text-capable HtmlNode subtree
  → HtmlNode-to-Tiptap JSON bridge
  → transient ProseMirror document/editor session
  → Tiptap browser editing and schema validation
  → Tiptap JSON-to-HtmlNode bridge
  → manifest/content-model validation
  → one coalesced PageContentCommand + Memento
```

`HtmlPageContent`/`HtmlNode` remains the only persisted page-content model.
Tiptap JSON is transient editor state, never a second persisted document or
parallel source of truth. The editor is deliberately an inline/phrasing-content
surface for one selected HTML element. It supports hard breaks, links, bold,
italic, strikethrough, and inline code, which convert to ordinary `br`, `a`,
`strong`, `em`, `s`, and `code` nodes. Headings, lists, blockquotes, and code
blocks remain structural PageEditor nodes rather than a second block tree
inside Tiptap. Native browser undo is scoped to an active text-editing session;
PageEditor Memento records one coalesced completed edit. The editor rejects
scripts, event attributes, arbitrary raw styles, and unsupported nodes before
the tree is saved or rendered.

An HTML `<textarea>` is a literal form-control element: its `value`,
`placeholder`, and related attributes are edited through the normal property
panel, not with a rich-text editor inside the control.

Markdown import/export is implemented as explicit interchange, never as another
persisted page format. Import uses one reusable immutable Markdig pipeline with
raw HTML parsing disabled, converts Markdown to HTML, and then passes the output
through the existing AngleSharp importer and the manifest, attribute, URL,
nesting, and size policies. Markdig conversion is not a sanitization or
validation boundary. A successful import is inserted as one undoable editor
mutation.

Export uses a fail-closed visitor over the canonical `HtmlNode` tree. It emits
only the semantic subset Markdown can preserve losslessly: headings,
paragraphs, emphasis, links, images, quotes, lists, rules, line breaks, and
inline or fenced code. Presentation styles, theme classes, unsupported
attributes, and non-representable elements cause a visible validation failure
instead of being silently discarded. The PageEditor exposes separate Import
Markdown and Export Markdown commands; exported text is an interchange copy
and never replaces the stored HTML tree.

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
  ts/aero-html-sortable.ts
  ts/aero-tiptap-editor.ts
  wwwroot/js/aero-html-sortable.js
  wwwroot/js/aero-tiptap-editor.js
  Pages/Manager/PageEditor/LivingStandard/
    HtmlSortableInterop.cs
    TiptapEditorInterop.cs
    HtmlRichTextEditorDialog.razor
    HtmlRichTextEditorDialog.razor.cs
```

`Microsoft.TypeScript.MSBuild` compiles the project TypeScript outside
`wwwroot` into `wwwroot/js`. The sortable module has no third-party runtime.
The rich-text module dynamically imports pinned Tiptap ESM packages from
`esm.sh`, following the project's CDN-first policy. No npm or pnpm install/build
step is required. If offline/self-hosted operation becomes a requirement, the
same adapter boundary can switch to vendored ESM assets without changing the
page model or Blazor component. The RCL static-web-assets manifest makes the
owned modules available to every consuming ASP.NET Core app at:

```text
/_content/Aero.Cms.Shared/js/aero-html-sortable.js
/_content/Aero.Cms.Shared/js/aero-tiptap-editor.js
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
| Browser integration | Adapters (`HtmlSortableInterop`, `TiptapEditorInterop`) |
| Rich-text document/schema/DOM editing | Tiptap Core JS behind the owned TypeScript and Blazor adapters |
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

## Delivery Status

1. **Complete:** `Aero.Cms.Html` foundation, versioned manifest,
   catalog/policy, tree operations, style contracts, and conformance tests.
2. **Complete:** `PageDocument` draft/published `HtmlPageContent` snapshots,
   Sable flexible-schema persistence, and deeply nested integration tests.
3. **Complete:** tracked draft/published lifecycle and optimistic concurrency;
   page content no longer depends on composition documents or replayable page
   content events.
4. **Complete:** static encoded renderer and style output, public
   `Page.cshtml` integration, and Living Standard seed templates.
5. **Complete:** editor commands, bounded Memento undo/redo, manifest palette,
   27 curated ordinary-HTML component templates, and 7 responsive layout
   starters. Templates are grouped by authoring purpose across Start here,
   Content, Conversion, Trust, Navigation, and Structure. The palette initially
   shows six choices, expands on demand, and searches components, layouts, and
   HTML primitives through one field.
6. **Complete:** Living Standard canvas and property panel inside the retained
   PageEditor shell.
7. **Complete:** owned TypeScript sortable and Tiptap adapters packaged as RCL
   static assets and exercised from the Blazor WASM manager.
8. **Complete:** first catalog, including the table and static-form phases (82
   elements in manifest `2026.3`). Ordered lists support the Living Standard
   `start` attribute with signed-integer validation.
9. **Complete for Pages, Posts, and Docs:** PageEditor/page persistence/public
   rendering no longer reference Neo or legacy blocks. Posts persist and
   transport their Markdown body directly; Docs retain their direct Markdown
   model and no longer register an unused block service or block-editor state.
10. **Complete:** the compiled Neo page-tree/editor layer has been removed. This
    includes `NeoPageNode`, `NeoCompositionBlock`, the Neo composition policy and
    history types, old PageEditor catalog contracts, Neo tree renderers/mappers,
    editor-block transport metadata, and their obsolete tests. The archived
    Marten source under `Aero.Cms.Db.Marten/Legacy` remains non-buildable reference
    material and is not part of the runtime.
11. **Complete:** fail-closed AngleSharp fragment import through the palette.
    Imported HTML is converted into validated `HtmlNode` trees and enters
    history as one mutation; AngleSharp state is never persisted.
12. **Complete:** document outline and ancestor breadcrumbs derived directly
    from the active `HtmlPageContent` tree, with synchronized canvas/inspector
    selection and no parallel navigation model.
13. **Content Types/Scriban cutover complete:** `ContentItemRenderer` now renders
    a content type's normalized, validated Scriban template directly from its
    `ContentItem.Fields`. Content-type saves no longer create duplicate
    `DynamicBlockDefinition` documents, and the former
    `IContentTypeRenderingBridge`/`DynamicTemplateBlock` conversion has been
    removed. The secure Scriban engine accepts only a content-owned
    `ScribanRenderDefinition`; there is no dynamic-block compatibility overload
    or render-mode switch. Content field bags use a content-specific
    source-generated JSON context, and the generic `BlockBase`,
    `ContentEmbedBlock`, block persistence, HTTP, preview, rendering, source
    generator, and legacy Neo style infrastructure have been removed from the
    compiled product.
    Strict embedded-SurrealDB coverage now verifies that content items and
    content-type definitions round-trip nested objects, arrays, scalar values,
    field settings, and subsequent updates. Both field bags and settings use
    source-generated `System.Text.Json` metadata; no reflection-based fallback
    or legacy block serializer participates in persistence.
14. **Complete:** site-owned native style profiles, strict-safe persisted color
    tokens, revision-checked updates, per-site server and WASM resolution, a
    curated Site editor design-system panel, and site-scoped rendered-output
    cache invalidation.
15. **Complete:** Markdig-based Markdown import/export as non-persisted,
    fail-closed interchange. Import reuses the AngleSharp policy boundary and
    export refuses to drop page structure or presentation information.
16. **Complete:** exact Tailwind and Bootstrap style-adapter Strategies with
    native scoped-CSS fallback. Adapter output participates in the deterministic
    content hash; native remains the default profile.

Browser acceptance covers palette insertion, owned pointer drag, undo/redo,
rich-text editing, and the create → save → reload → publish → public-render
lifecycle. The component lifecycle scenario drags a split hero onto an empty
canvas, edits its heading through the Aero Tiptap surface, changes its image
source and alternative text through the property panel, reloads the stored
draft, publishes it, and verifies the resulting public HTML.

Nested-composition browser acceptance builds a `section > h2 + p` tree by
dragging palette primitives into the container, edits the paragraph through
the Aero Tiptap surface, reorders siblings with the owned pointer adapter,
outdents and indents with the accessible ArrowLeft/ArrowRight commands, verifies
Memento undo/redo, and confirms that the nested text and ordering survive save
and reload. Pointer placement remains intentionally unambiguous: dragging over
a child targets that child, while explicit keyboard commands change nesting
depth.

Keyboard-command acceptance verifies Control/Command+D duplication,
Delete/Backspace removal, Control/Command+Z undo, redo variants, and protection
for inputs and editable text surfaces. Command buttons expose
`aria-keyshortcuts`; successful commands are announced through the editor's
polite status channel, invalid nesting is reported as an alert, and focus
returns to the selected element's drag handle. Removal selects the next sibling,
then the previous sibling, then the non-fragment parent so keyboard users retain
their editing context; an empty canvas receives focus when no selection remains.

Rich-text acceptance applies strikethrough and inline code to distinct text
ranges, exposes active formatting through `aria-pressed`, converts the result to
ordinary semantic `s` and `code` nodes, and verifies that one PageEditor Undo
reverts the complete applied edit while Redo restores it.

Responsive browser acceptance composes representative split-hero, feature,
image-backed call-to-action, gallery, and contact-form templates in the actual
WASM editor preview. It verifies desktop and mobile grid collapse, descendant
overflow, and the framework-neutral Pages baseline used by public rendering.
Curated media templates use lightweight placeholder SVGs packaged by the Shared
RCL, so a fresh installation has a complete visual preview before the user
chooses site media.

Visual-style authoring acceptance customizes an ordinary hero section through
the property panel with grid layout, mobile stacking, alignment, gap, logical
spacing, minimum height, background image and overlay, image fit/repeat, and
corner radius. It also applies responsive gradient typography to the heading.
The browser verifies computed desktop and mobile styles, then saves, reloads,
publishes, and verifies the same framework-neutral intent in public SSR output.
Solid text color and text gradient remain mutually exclusive compiler intents;
the editor enforces that policy by omitting the retained solid color while
gradient mode is active and restoring it when gradient mode is disabled.

The PageEditor sidebar also exposes a document outline derived directly from
the same `HtmlPageContent` tree used by the canvas. It shows the nested element
structure, keeps selection synchronized with the canvas and inspector, and
provides clickable element breadcrumbs for moving from a deeply nested child
to an ancestor. It is navigation state only: it owns no second tree, command
history, or persistence path.

### Remaining Backlog

There are no required Living Standard cutover items left for the first usable
PageEditor. Remaining work is optional expansion:

1. Expand element attribute descriptors so future catalog phases can render
   property controls from manifest metadata instead of adding tag-specific UI.
2. Continue curated component/template expansion when concrete site patterns
   justify it.
3. Add an explicit site-level framework selection only when a consuming theme
   supplies the corresponding Tailwind or Bootstrap stylesheet contract. Exact
   adapters and native scoped-CSS fallback are complete; native remains the
   default.
4. Add immutable `PageRevisionDocument` snapshots on save/publish only if
   visible revision history becomes a product requirement.
5. Continue later catalog phases for `iframe`, SVG, MathML, and custom
   elements. Each phase requires namespace, URL, child-content, rendering, and
   import-policy review before it becomes palette-visible.
Source-generated catalog code and offline-vendored Tiptap modules remain
optional engineering improvements, not PageEditor feature blockers.

6. **TODO (future): align the AngleSharp dependency graph.** The current
   solution build resolves both the legacy `AngleSharp` 0.17.x family (through
   existing sanitizer/CSS dependencies) and `AngleSharp` 1.x for Living
   Standard HTML import. Consolidate these packages onto a compatible version
   family, remove the assembly-resolution warnings, and rerun the approved
   static-fragment import and browser acceptance coverage before treating HTML
   import as production-hardened.
7. Implement the additive typed-content and fragment sidecar described in
   [Page Editor: Typed Content Composition and Extensibility](page-editor-content-composition.md)
   without changing the `HtmlPageContent` persistence or rendering contract.

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
