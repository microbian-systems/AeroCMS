---
title: End-to-end tutorial
description: Build, configure, author, query, preview, and publish a first AeroCMS site.
---

This tutorial is the shortest evidence-based path through the current AeroCMS alpha. It uses the manager UI for authenticated authoring and the anonymous read-only API for integration.

## 1. Decide whether AeroCMS fits

Use AeroCMS when the host is a .NET 10 application, content is scoped by site and culture, authors need runtime-managed pages and structured content, and server rendering is preferred. Do not treat this alpha as production-ready without completing the [production checklist](/operations/security/).

## 2. Install prerequisites

Required:

- Git with recursive submodule support.
- The .NET SDK pinned by `global.json` (`10.0.301` at this baseline).
- A trusted ASP.NET Core development certificate for the HTTPS launch profiles.
- Node.js and pnpm only when building this documentation site; AeroCMS runtime assets do not use npm.

Optional production dependencies include a remote SurrealDB endpoint, a Redis-compatible cache, a certificate-backed data-protection key ring, and external identity or AI providers.

## 3. Clone, build, and run

Run from a clean parent directory:

```powershell
git clone --recurse-submodules git@github.com:microbian-systems/AeroCMS.git
Set-Location AeroCMS
dotnet restore src/Aero.Cms.slnx
dotnet build src/Aero.Cms.slnx --no-restore
dotnet run --project src/Aero.Cms.AppHost/Aero.Cms.AppHost.csproj
```

Expected result: the Aspire dashboard starts on the URL printed by the AppHost and launches the manager and public web resources. To run only the public web host, use:

```powershell
dotnet run --project src/Aero.Cms.Web/Aero.Cms.Web.csproj --launch-profile https
```

The checked-in launch profile uses `https://localhost:333`. Treat console output as authoritative when a local override changes the port.

## 4. Complete first-run setup

Open `/setup` on the public web host. The bootstrap pipeline uses setup, configured, and running states; it does not serve the normal application before the selected infrastructure is ready.

Choose the database and cache modes, create the recovery administrator, select manager and member authentication providers, and finish the wizard. Setup protects the pending handoff, initializes required records, creates the initial tenant and site, seeds starter content, and then restarts into running mode.

Expected result: `/setup/status` reports the bootstrap state as ready and the browser transitions to the normal application. Do not place raw secrets into tracked `appsettings` files.

## 5. Create and select a site

Sign in at `/manager/login`, then open `/manager/sites`. Create the site with its tenant, primary host, default culture, and supported cultures. Select the site at `/manager/select-site`.

The selected site is stored in the `AeroCms.SiteId` cookie. Manager APIs still require authentication and the matching `site:read`, `site:create`, `site:update`, or `site:delete` authorization policy. Host resolution alone is not manager authorization.

Expected result: the manager header shows the selected site, and site-scoped screens load only its data.

## 6. Create and publish an Aero page

Open `/manager/pages`, choose **New page**, keep the **Aero composition** renderer, enter a title and slug, and save the draft. Add semantic HTML elements in the visual editor, preview the exact draft, then publish.

Expected result: the public route resolves by the site's host, culture, and page path. Draft preview requires an authenticated manager with `site:read`; the public route serves only a published version.

## 7. Create a post

Open `/manager/posts`, create a draft, enter Markdown content and optional taxonomy/media metadata, preview, and publish.

Expected result: the post appears under the site's blog route for the active culture. Publication invalidates the relevant blog cache tags; it does not make another site's post visible.

## 8. Create a flat content type

Open `/manager/content-types`, create `speaker` as a collection, and add `name` (text), `bio` (rich text), and `photo` (image) fields. Save the definition, then open `/manager/content/speaker` and add an item.

Expected result: the item remains a draft until explicitly published. Its Snowflake identifier is a `long` in .NET and a decimal string at JSON/script boundaries.

## 9. Add richer fields

The built-in aliases are `text`, `richtext`, `image`, `number`, `boolean`, `url`, `date`, `reference`, `list`, `gallery`, `dictionary`, `range`, and `color`. List and dictionary fields are bounded; range fields use inclusive integer endpoints; references can target flat or hierarchical content.

Use indexing/search settings only on fields supported by the current content definition. Semantic search is a separate, maturity-limited path; do not assume an embedding provider is configured.

## 10. Create a hierarchy

Create `topic` with structure **Hierarchical**, maximum depth `4`, root items allowed, and same-type parents required. Create a root called “Engineering”, add “.NET” beneath it, and add “AeroCMS” beneath “.NET”. Use the hierarchy manager to reorder or move a node.

The service rejects cycles, invalid parent types, cross-site/culture parents, and moves beyond the depth bound. Sibling order is normalized in the same Sable unit of work.

## 11. Query published content

Publish the three topic items, then run:

```powershell
$headers = @{ Accept = 'application/json' }
Invoke-RestMethod `
  -Uri 'https://localhost:333/api/v1/query/content/topic?traversal=RootsWithDescendants&maximumDepth=4&maximumItems=50&fields=title' `
  -Headers $headers
```

Expected shape:

```json
{
  "name": "public",
  "contentTypeAlias": "topic",
  "roots": [
    {
      "id": "190000000000000001",
      "contentType": "topic",
      "title": "Engineering",
      "slug": "engineering",
      "fields": {},
      "children": []
    }
  ],
  "totalItems": 3,
  "wasTruncated": false
}
```

The exact IDs differ. The API is anonymous, read-only, published-only, site/culture scoped, bounded, and returns JSON unless `HX-Request: true` or `Accept: text/html` requests an encoded HTML fragment.

## 12. Add a Scriban fragment

In an Aero-composition page, add a rendered fragment and choose **Scriban**. The following is illustrative; available bindings depend on the content queries declared on the page:

```liquid
<section class="topic-list">
  {{ for topic in content.topics.roots }}
    <h2>{{ topic.title | html.escape }}</h2>
  {{ end }}
</section>
```

Expected output: one encoded heading per materialized root. The runtime receives a closed, eager data scope—not a database session or arbitrary .NET object graph.

## 13. Add an HTMX fragment

This fragment is illustrative and assumes the public query route:

```html
<section
  hx-get="/api/v1/query/content/topic?traversal=Roots&maximumItems=10"
  hx-trigger="load"
  hx-swap="innerHTML">
  <p>Loading topics…</p>
</section>
```

Expected output: the server returns fixed, encoded HTML when HTMX sends `HX-Request: true`. Never interpolate untrusted values into `hx-*` URLs or headers.

## 14. Add a SharpTS fragment

SharpTS is alpha and intended for trusted authors. This source is illustrative:

```typescript
export function render(context: AeroRenderContext): string {
  return `<p>${context.page.title}</p>`;
}
```

Expected output: a validated HTML fragment. The interpret-only host rejects arbitrary imports and decorator-based .NET access, exposes only `aero:content` plus an explicit .NET allowlist, serializes execution through an in-process gate, and caps output size. It is not an operating-system sandbox.

## 15. Preview and publish

Pages, posts, and documentation use authenticated preview boundaries. Preview resolves a draft by exact ID/source version, marks the response no-store, and keeps site/culture scope. Publishing creates the public version and triggers feature-specific cache invalidation. Aliases and redirects are separate records; review route-impact warnings before moving a page.

## 16. Continue safely

Next:

- learn the [page rendering boundaries](/guides/pages-and-rendering/);
- model [content and hierarchy](/guides/content-modeling/);
- integrate the [public query API](/guides/public-query-api/);
- configure [identity and site permissions](/guides/identity-and-access/);
- complete [deployment](/operations/deployment/) and [security](/operations/security/) checks.
