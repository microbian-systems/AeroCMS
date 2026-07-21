# Page Editor: Typed Content Composition and Extensibility

## Status: Accepted Additive Design; Implementation In Progress

This document extends the implemented [HTML Living Standard page editor](page-editor-html-living-standard.md).
It does not replace its HTML tree, validation pipeline, static renderer, sortable
interop, inspector, rich-text editor, or draft/published behavior.

The first two implementation slices are complete. The selected-element action
toolbar and full element-properties dialog are additive to the existing editor.
The right sidebar now composes the existing outline, palette, and inspector as
Document, Elements, and Inspector tabs, and a Content tab reads content types
and field definitions through `IContentTypesHttpClient` in
`Aero.Cms.Abstractions`. Content scopes and fields are intentionally shown as
read-only until authoring commands attach them to the composition sidecar.

The persistence foundation is also implemented. `PageCompositionDocument` in
`Aero.Cms.Abstractions` defines content-list scopes, content-item scopes,
bounded list queries, and explicit field-binding targets. `PageDocument` now
stores independent `DraftComposition` and `PublishedComposition` snapshots;
draft replacement, publication, culture forks, Orleans view-model transport,
HTTP DTOs, and PageEditor save/load mapping keep those snapshots paired with
their corresponding HTML trees. Pages validates node ownership, scope
uniqueness, lookup shape, paging bounds, and binding containment. Content-type,
content-item, and field existence validation remains owned by the Content
module and is the next cross-module contract slice.

## Goals

- Compose pages from ordinary HTML plus typed content without merging the Pages
  and Content modules.
- Add pageable `ContentTypeList` and detail `ContentTypeItem` scopes.
- Let authors drag fields from a selected content type into a compatible scope.
- Add Markdown, Custom HTML, and Scriban palette items with explicit rendering
  and security policies.
- Organize the right sidebar into Document, Elements, Content, and Inspector
  tabs while retaining all existing editor behavior.
- Make registered partials, views, and slots available through an explicit
  registry rather than filesystem reflection.
- Add a manager-wide AI assistant only after the MCP module exposes an
  authenticated transport and stable tool contract.

## Non-Destructive Invariant

`PageDocument.DraftContent` and `PageDocument.PublishedContent` remain the
authoritative layout snapshots. Existing `HtmlPageContent` and `HtmlNode`
serialization stays unchanged. New capabilities are optional sidecars attached
to stable `HtmlNode.NodeId` values.

Pages that do not use bindings or fragments must save, publish, render, import,
export, undo, redo, and reorder exactly as they do today. No compatibility
upcaster, legacy cleanup, or second page tree is introduced.

## Module Boundary

The Pages module owns:

- the HTML layout tree and page lifecycle;
- placement of scopes, bindings, and fragments;
- draft/published sidecar snapshots;
- the render orchestration order.

The Content module owns:

- content-type definitions and field descriptors;
- content-item identity, slug routing, publication state, and queries;
- validation of type/field references;
- pageable content results and item projections.

Cross-boundary calls use contracts in `Aero.Cms.Abstractions` and implementations
registered by the Content module. Orleans grains may implement the contracts,
but PageEditor and public rendering consume the abstractions rather than taking
a project reference on `Aero.Cms.Modules.Content`.

The initial contract should expose read-only authoring and rendering operations:

- list available content types for the current site;
- describe the fields for one content type;
- resolve one published item by stable item ID, with slug as routing metadata;
- query a published, pageable list for one content type;
- validate that a saved type, item, and field reference still exists.

## Persistence Sidecar

Add optional `DraftBindings` and `PublishedBindings` snapshots beside the
existing HTML snapshots. Each sidecar entry targets one stable `NodeId` and is
one of the following concepts:

### Content scope

- `ContentTypeList`: content type ID, paging definition, sort/filter definition,
  page-size limit, empty-state behavior, and template-root node ID.
- `ContentTypeItem`: content type ID plus item selection. Persist the stable
  content item ID and an optional last-known slug. Slug-only lookup is an
  explicit routing mode, not the default identity model.

### Field binding

A binding maps a node property to a field within its nearest content scope. A
target is explicit, for example text content, `href`, `src`, `alt`, title, or a
supported structured-style token. The editor filters offered targets by the
selected HTML element and field data type.

Bindings do not write Scriban expressions into HTML attributes and do not turn
the saved HTML tree into a template language.

### Rendered fragment

- Markdown stores Markdown source and renders through Markdig on the public
  side. Authoring uses the existing Tiptap integration adapted for Markdown
  interchange.
- Custom HTML stores an HTML fragment and passes it through the same import,
  attribute, URL, content-model, and render validation boundaries as normal page
  HTML. It is not a sanitization bypass.
- Scriban stores template source plus an explicit, allowlisted context contract.
  Authoring uses the existing Blazor Monaco integration already used by Content
  Types. Template execution is time/size bounded and exposes no arbitrary .NET
  object access.
- Registered partial/view/slot entries store a registry key and typed
  parameters. Discovery is generated or explicitly registered; it does not scan
  folders at runtime and does not use reflection-based module discovery.

## Rendering Pipeline

Public rendering remains deterministic and fail-closed:

1. Load the published `HtmlPageContent` and published composition sidecar.
2. Clone the HTML tree; never mutate the persisted snapshot during rendering.
3. Resolve content scopes through the Content abstraction using site and
   publication context.
4. For a list, clone its template subtree once per returned item and apply field
   bindings within that item context.
5. Resolve Markdown, Custom HTML, Scriban, and registered fragment nodes through
   their dedicated renderer strategies.
6. Validate the expanded candidate tree and compile structured styles.
7. Pass the result to the existing static HTML renderer.

Missing types, items, fields, registry keys, or rejected fragment output produce
an observable render error or an explicitly configured empty state. They never
fall back to executing raw input.

## PageEditor Experience

The right sidebar becomes a tab set while retaining the existing components:

- **Document** contains `HtmlPageEditorOutline`.
- **Elements** contains the existing element/layout/component palette plus
  Markdown, Custom HTML, Scriban, and registered fragments.
- **Content** contains a site-scoped content-type selector. Selecting a type
  shows `ContentTypeList`, `ContentTypeItem`, and compatible field draggables.
- **Inspector** contains the existing `HtmlElementPropertyPanel` for the current
  selection and later adds binding/scope configuration for that node.

Dragging a field outside a compatible content scope is rejected with an
explanation. Dropping `ContentTypeList` creates an ordinary validated container
plus list-scope metadata; dropping `ContentTypeItem` creates an ordinary
container plus item-scope metadata. The canvas therefore remains an HTML tree,
not a parallel component tree.

Selected canvas elements expose an additive floating toolbar:

- drag handle (existing behavior);
- move before and move after sibling;
- duplicate;
- edit all currently supported element properties;
- delete.

Double-click opens the full properties dialog. The right-side Inspector remains
available, and eligible text nodes retain the dedicated Tiptap “Edit text” flow.
All mutations continue through `HtmlPageEditorSession`, content validation, and
Memento history.

## AI Assistant Boundary

The manager navbar may open a persistent assistant drawer, but the manager UI
must not depend directly on the MCP module implementation. A client contract in
`Aero.Cms.Abstractions` should own conversation creation, REST request/response,
SSE streaming, cancellation, authentication failures, and correlation IDs.

The current MCP module must first gain authenticated endpoints, an SSE transport,
tool registration, site/user authorization context, and tests. Until those exist,
the assistant button may be designed but must not imply a functioning backend.
Assistant tools use the same application services and authorization rules as the
manager UI; MCP is a transport boundary, not an authorization bypass.

## Industry Alignment

This model follows the common separation used by mature CMS products:

- Umbraco composes pages with Block Grid while content pickers reference content.
- Orchard Core Flow composes widgets while Content Picker fields reference
  content items.
- Wix datasets connect repeaters and dynamic list/item pages to collection data.
- WordPress Query Loop repeats a nested block template over query results.
- Sanity and Contentful use references for reusable content and embedded
  components for composed presentation.

The shared lesson is to keep structured content independently reusable, store
stable references, and let the page builder own presentation and query context.
Copying complete content items into the page tree would couple lifecycle,
localization, permissions, and publication state and is therefore rejected.

## Delivery Slices

1. **Editor interaction:** additive selected-element toolbar, sibling movement,
   duplicate/edit/delete actions, and full property dialog.
2. **Sidebar organization (complete):** Document, Elements, Content, and
   Inspector tabs composed from the existing outline, palette, property panel,
   and content-type read client.
3. **Contracts and sidecars (in progress):** source-defined composition models,
   structural validation, persistence, publication/culture cloning, Orleans
   transport, and HTTP/PageEditor mapping are complete. Content-module read
   contracts and referenced type/item/field validation remain.
4. **Typed content authoring:** type selector, list/item scopes, compatible field
   draggables, paging configuration, undo/redo, and preview resolution.
5. **Fragment strategies:** Markdown/Markdig, validated Custom HTML,
   Scriban/Monaco, and explicit registered partial/view/slot registry.
6. **Public rendering:** published resolution pipeline, cache keys, pagination,
   observability, failure policy, and integration tests.
7. **Manager assistant:** MCP endpoints and tools first, then the authenticated
   REST/SSE client and manager-wide assistant drawer.

Each slice must preserve the existing PageEditor and add focused TUnit and, when
the browser interaction changes, Microsoft Playwright coverage before proceeding.

## References

- [Umbraco Block Grid Editor](https://docs.umbraco.com/umbraco-cms/13.latest/fundamentals/backoffice/property-editors/built-in-umbraco-property-editors/block-editor/block-grid-editor)
- [Umbraco Content Picker](https://docs.umbraco.com/umbraco-cms/13.latest/fundamentals/backoffice/property-editors/built-in-umbraco-property-editors/content-picker)
- [Orchard Core Flow](https://docs.orchardcore.net/en/latest/reference/modules/Flow/)
- [Orchard Core Content Picker Field](https://docs.orchardcore.net/en/latest/reference/modules/ContentFields/)
- [Wix dynamic list pages](https://support.wix.com/en/article/cms-formerly-content-manager-about-dynamic-list-pages)
- [WordPress Query Loop block](https://wordpress.org/documentation/article/query-loop-block/)
- [Sanity references](https://www.sanity.io/docs/studio/reference-type)
- [Contentful references and entries](https://www.contentful.com/help/references/)
