# Page Editor: Typed Content Composition and Extensibility

## Status: Accepted Additive Design; Implementation In Progress

This document extends the implemented [HTML Living Standard page editor](page-editor-html-living-standard.md).
It does not replace its HTML tree, validation pipeline, static renderer, sortable
interop, inspector, rich-text editor, or draft/published behavior.

The implemented slices now establish the editor interaction, sidebar,
persistence, validation, typed-content authoring, and first public rendering
path. The
selected-element action toolbar and full element-properties dialog are additive
to the existing editor.
The right sidebar now composes the existing outline, palette, and inspector as
Document, Elements, and Inspector tabs, and a Content tab reads content types
and field definitions through `IContentTypesHttpClient` in
`Aero.Cms.Abstractions`. Selecting a content type now enables draggable and
click-to-add list scopes, selected-item scopes, and field bindings.

The persistence foundation is also implemented. `PageCompositionDocument` in
`Aero.Cms.Abstractions` defines content-list scopes, content-item scopes,
bounded list queries, explicit field-binding targets, and bounded
`PageRenderedFragment` sidecars for Markdown, Custom HTML, and Scriban.
`PageDocument` now
stores independent `DraftComposition` and `PublishedComposition` snapshots;
draft replacement, publication, culture forks, Orleans view-model transport,
HTTP DTOs, and PageEditor save/load mapping keep those snapshots paired with
their corresponding HTML trees. Pages validates node ownership, scope
uniqueness, lookup shape, paging bounds, and binding containment.

The Content-owned reference boundary is now implemented through
`IContentCompositionReferenceValidator` in `Aero.Cms.Abstractions`. The Content
module resolves content types by stable ID and validates item ownership,
slug-and-culture lookup, list sort/filter fields, and field bindings. Draft
saves use authoring validation; publication additionally requires explicitly
selected content items to be published. Pages consumes only the abstraction and
fails closed when structured content exists but the Content implementation is
unavailable.

Published typed-content resolution is also implemented without merging the
modules. `IContentCompositionResolver` is owned by `Aero.Cms.Abstractions` and
implemented by Content using its existing type, item, and query services. It
returns copied published projections after enforcing site, culture, type, and
publication boundaries. Pages owns `PageCompositionExpander`, which clones the
saved HTML, expands list templates with fresh node IDs, applies allowlisted field
targets, validates the expanded tree, and then hands it to the existing style
compiler and static renderer. The same expander is used by public Razor Pages,
saved draft previews, and the unsaved PageEditor fragment-preview endpoint.

The three source-backed fragment strategies are now implemented additively.
Each palette item creates an ordinary `<section>` in `HtmlPageContent` and stores
its authoring source beside that node ID. Markdown uses block-capable Tiptap,
persists Markdown, disables raw HTML in Markdig, and imports generated HTML
through the existing strict importer. Custom HTML retains its source but is not
a bypass: scripts, event handlers, unsafe URLs, unsupported elements, invalid
nesting, and parser recovery fail closed. Scriban uses Monaco and the existing
`SecureScribanRenderer`, exposing only explicit `page` and `site` metadata before
sanitization and strict HTML import. Fragment insertion, source edits, removal,
duplication, and orphan reconciliation share the aggregate HTML/composition
undo-redo history. Public pages and preview endpoints resolve all three through
`PageCompositionExpander` without mutating the saved tree.

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

The implemented validation path uses a scoped Content-module implementation so
Pages does not open Content persistence or depend on the Content assembly.
Existing HTTP and Orleans surfaces remain the transport boundaries for manager
UI and distributed callers; the domain validation contract is transport-neutral.

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
- Registered application-fragment entries use a distinct `PageRegisteredFragment`
  sidecar. Persisted pages store only a normalized provider key and typed scalar
  JSON parameters; they never store view paths, CLR type names, or rendered markup.
  Providers are added explicitly with `AddPageRegisteredFragment<TProvider>()`.
  Registration rejects invalid or duplicate normalized keys and never scans
  assemblies or folders at runtime.
- The implemented schema supports string, integer, decimal, boolean, and enum
  parameters with required/default, length, range, and choice constraints. Both
  save/publication validation and rendering fail closed when a provider is
  missing or its parameter schema no longer matches.
- Provider output is size-bounded, imported through the existing strict HTML
  fragment importer, and checked again by final page validation. The initial
  vertical includes the code-backed `core.site-notice` slot. A fixed Razor
  partial/view adapter is intentionally deferred until it can preserve the same
  explicit registration and bounded-output contract cleanly.

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

The implemented typed-content portion resolves nested scopes deepest-first,
defaults every list to page 1, accepts `contentPage` for a page with one list,
and accepts `contentPage-{scopeNodeId}` for independent list paging. Output-cache
entries vary by all query keys and carry every authoritative content-type tag
reported by the expansion, so existing Content invalidation evicts composed
pages when an item of a referenced type changes.

Dynamic JSON sort and filter expressions currently run in Content over a
maximum of 1,000 site/type candidates. A larger candidate set fails closed with
an observable validation error. This is an explicit initial bound until the
Sable query provider offers safe, allowlisted dynamic JSON predicate pushdown.

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

The initial list command creates a validated `<section>` with an `<article>`
template and a bounded query whose default page size is 10. Selecting the list
scope exposes additive Inspector controls for a public page size from 1 through
100, an optional sort field and direction, up to ten AND filters, and empty
results behavior. These settings update only the composition sidecar and share
the aggregate undo/redo history; they do not rewrite the HTML template.

The content-item picker loads authoring summaries through
`IContentItemsHttpClient` in searchable ten-item pages rather than loading an
unbounded selector. The saved item scope uses the stable content-item ID and
retains the slug as routing metadata. Field buttons create a compatible
ordinary HTML node inside the nearest matching scope and add an explicit field
binding. Fields are rejected when there is no matching scope or when a list
drop is outside its template subtree. Palette drag payloads and query settings
are treated as untrusted hints and revalidated against the currently loaded
content type, fields, and item summaries before mutation.

The composite editor Memento gate is complete. `HtmlPageEditorSession` owns both
`HtmlPageContent` and `PageCompositionDocument`, captures them as one atomic
undo/redo snapshot, and removes structurally orphaned scopes and bindings after
successful HTML mutations. `PageEditor` now loads and saves the sidecar through
that session. List, item, and field commands update HTML and composition as one
atomic history entry. Bound-value preview and public resolution now use the
shared server-side expansion pipeline; the persisted editor HTML remains the
unchanged authoring template.

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

The manager navbar opens a shell-owned assistant drawer without coupling the
manager UI to the MCP module implementation. Contracts and the typed client in
`Aero.Cms.Abstractions` own bounded conversations, REST responses, POST-SSE
streaming, cancellation, authentication failures, correlation IDs, and the
capability-only REST fallback policy.

`Aero.Cms.Modules.AiAssistant` owns stateless provider conversation
orchestration and reuses the existing AI settings and provider factory.
`Aero.Cms.Modules.Mcp` owns the authenticated manager endpoints and the
standards Streamable HTTP endpoint at `/mcp`. Its initial read-only tools expose
the current site, bounded page listings, and bounded page detail. Every tool
independently rebuilds its user/site context and reauthorizes `site:read` for the
exact selected site before reading data; MCP is a transport boundary, not an
authorization bypass.

The drawer keeps history only in its manager-shell scope, clears it when the
authenticated user or selected site changes, streams incremental output, and
cancels in-flight work. Model-driven invocation of the MCP tools from within the
provider conversation loop is intentionally deferred; provider chat and direct
authenticated MCP tool calls are available in this slice.

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
3. **Contracts and sidecars (complete):** source-defined composition models,
   structural validation, persistence, publication/culture cloning, Orleans
   transport, HTTP/PageEditor mapping, stable content-type lookup, and
   Content-owned type/item/query/field reference validation.
4. **Typed content authoring (complete):** composite
   HTML/composition Memento, orphan reconciliation, validated drag tokens,
   list/item scope creation, compatible field-binding commands, searchable
   item-picker pagination, bounded list-query configuration, and shared
   bound-value preview.
5. **Fragment strategies (complete for source-backed and registered code-backed
   providers):** bounded fragment
   sidecars, Markdown/Markdig with block-capable Tiptap, validated Custom HTML,
   Scriban/Monaco through the secure runtime, public/preview expansion, and
   aggregate history. Registered application fragments add an authenticated
   catalog, explicit generic provider registration, schema-driven scalar editing,
   and one code-backed slot. A Razor partial/view adapter remains deferred.
6. **Public rendering (typed-content and fragment portions complete):** Content-owned
   published projections, Pages-owned cloned-tree expansion, public and editor
   preview integration, independent list pagination, content dependency cache
   tags, bounded query failure policy, source-backed fragment expansion, and
   focused integration tests. Registered application fragments share the same
   cloned-tree public and unsaved-preview expansion path and fail closed.
7. **Manager assistant (complete for provider chat and MCP transport):** bounded
   stateless provider chat, authenticated REST/POST-SSE endpoints, explicit
   read-only site/page tools, standards Streamable HTTP at `/mcp`, and a
   manager-wide streaming/cancellable drawer. Model-driven tool invocation from
   the provider conversation loop remains deferred.

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
