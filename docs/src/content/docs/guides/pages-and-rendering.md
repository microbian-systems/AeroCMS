---
title: Pages and rendering
description: Page hierarchy, renderer selection, fragments, preview, publishing, aliases, and security boundaries.
---

Pages are site- and culture-scoped documents with hierarchy, path, draft/published state, and an explicit renderer ID. The manager exposes `/manager/pages` and `/manager/page/editor/{id}`; authenticated minimal APIs under `/api/v1/admin/pages` own create, update, move, preview, publish, unpublish, archive, delete, translation, and route-impact operations.

## Hierarchy and routing

A page can have a parent. The service computes the public path from the parent path and slug, validates cycles and depth, and records route impact before a move. Tree APIs expose roots, children, breadcrumbs, ancestors, navigation projections, and next-order calculation. Published route changes can require aliases; review them before committing a move.

The public Razor page route resolves the current host/site, culture, and normalized path. Drafts do not fall through to this boundary.

## Renderer choices

| ID | Authoring model | Maturity | Boundary |
| --- | --- | --- | --- |
| `aero.composition` | visual semantic HTML tree | implemented | validates typed elements, expands declared fragments and content queries, compiles style metadata |
| `aero.scriban` | exact full-page source | experimental | closed globals, validated Scriban AST/data, resource limits, sanitized output |
| `aero.htmx` | exact full-page HTML/HTMX source | experimental | imports source through the same validated HTML pipeline; HTMX requests run later in the browser |
| `aero.sharpts` | exact TypeScript source | alpha | trusted-author in-process interpreter with import/type allowlists and validated HTML output |

Renderer source versions are immutable snapshots. Publishing records the exact renderer, source, and hash; a mismatched hash is rejected.

## Aero composition

The Living Standard editor stores semantic page content rather than arbitrary component instances. Content queries are declared separately, resolved eagerly, and passed into the render. This keeps page composition, structured-content validation, and persistence query execution as separate boundaries.

Supported rendered fragment kinds are Markdown, custom HTML, Scriban, SharpTS, and HTMX. Registered fragments are developer-owned providers with source-generated registration. Every fragment is expanded into validated `HtmlPageContent` before final rendering.

### Markdown

Raw HTML is disabled. The rendered Markdown is imported through the strict HTML fragment importer.

### Custom HTML

Custom HTML passes through the importer and composition validator. Treat the allowed element/attribute set as the contract; do not assume arbitrary script, inline event handler, or unsafe URL support.

### Scriban

The secure renderer defaults to a 50,000-byte template limit, 1,000 loop iterations, recursion depth 50, strict variables, a two-second regex timeout, a 30-second render deadline, input depth 10, and 1,048,576 output characters. Includes, template loading, dynamic evaluation, relaxed CLR member access, and undeclared object traversal are disabled. Output is sanitized.

The scope contains detached page, site, preview, and declared content-query values. It does not contain a Sable session.

### HTMX

A pure HTMX page is authored as HTML with `hx-*` attributes and then imported through Aero's validated markup pipeline. Browser requests must target explicit endpoints. Prefer the anonymous published-only query API for public fragments and authenticated, antiforgery-aware manager endpoints for authoring UI.

Do not accept untrusted strings as `hx-get`, `hx-post`, `hx-headers`, selectors, or swap targets. Validate server input and return encoded fragments. The public query API varies on `HX-Request`/`Accept` and responds `private, no-store`.

### SharpTS

SharpTS executes trusted-author TypeScript in-process and serializes execution through one gate. The only virtual content import is `aero:content`; a small explicit set of collections, LINQ/expression, and task types is allowed. Other imports and .NET decorators are rejected. Context data is JSON-shaped and output is capped and re-imported as HTML.

This is defense in depth, not isolation from the host process. Disable SharpTS for untrusted authors and do not use it as a multi-tenant code-execution sandbox.

## Preview and publishing

Preview routes require an authenticated principal and `site:read`, load the exact draft/source version within the selected site, and send no-store cache headers. Publishing validates the renderer, source, content references, route, and current concurrency state before replacing the public version.

Aliases and redirects are explicit. Publishing does not grant authorization, change culture ownership, or copy content across sites.
