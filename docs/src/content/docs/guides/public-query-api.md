---
title: Public content query API
description: Anonymous, read-only, published CMS queries for JSON clients and HTMX.
---

The public facade is mapped at `/api/v1/query` and allows anonymous requests. It returns fresh, published-only, site/culture-scoped projections and never exposes a Sable session or lazy query object.

## Endpoints

| Route | Query |
| --- | --- |
| `GET /pages` | published page metadata with `skip` and `take` |
| `GET /posts` | published post metadata with `skip` and `take` |
| `GET /docs` | published docs metadata with `skip` and `take` |
| `GET /content/{contentTypeAlias}` | bounded hierarchy traversal and field projection |
| `GET /content/{contentTypeAlias}/search` | full-text or experimental semantic search |

Content query parameters are `traversal`, `rootId`, `maximumDepth`, `maximumItems`, and comma-separated `fields`. Search parameters are `q`, `mode`, `skip`, and `take`. Invalid modes return a validation problem; missing resources return 404; cancellation uses status 499.

Only structured-content search at `/content/{contentTypeAlias}/search` consults the content type's `IncludeInSearch` flag and returns an empty result when it is disabled. The flag does not hide published page, post, docs, list, or hierarchy results and is not an authorization or confidentiality control.

## JSON example

Prerequisite: a published `topic` content type on the site resolved by `localhost`.

```bash
curl --fail --show-error \
  --header "Accept: application/json" \
  "https://localhost:333/api/v1/query/content/topic?traversal=RootsWithDescendants&maximumDepth=3&maximumItems=25"
```

Expected output is a `ContentQueryResult` with `roots`, `totalItems`, and `wasTruncated`. Snowflake IDs are decimal strings.

## HTMX example

Prerequisite: HTMX loaded on a published page.

```html
<div
  hx-get="/api/v1/query/posts?skip=0&take=5"
  hx-trigger="load"
  hx-swap="innerHTML">
  Loading…
</div>
```

Expected output is fixed semantic HTML with encoded text. The server selects HTML when `HX-Request: true` or `Accept: text/html`; otherwise it returns JSON.

## Response and cache behavior

Responses set `Vary: HX-Request, Accept` and `Cache-Control: private, no-store`. This prevents a shared cache from serving an HTML fragment as JSON or carrying a site/culture response across callers. If you add a public cache, its key must also include host/site, culture, path, query, and representation.

## Client error handling

```csharp
public static async Task<Result<JsonDocument>> LoadTopicsAsync(
    HttpClient client,
    CancellationToken cancellationToken)
{
    using var response = await client.GetAsync(
        "api/v1/query/content/topic?maximumItems=25",
        cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        return AeroError.InvalidRequestError(
            $"AeroCMS query failed with HTTP {(int)response.StatusCode}.");
    }

    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
    return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
}
```

Prerequisite: reference the Aero railway result/error types used by the host. Expected result: `Result<JsonDocument>.Ok` for a successful JSON response, otherwise a failure without throwing for an expected HTTP error.

See the [public API reference](/api/index.html) for supported .NET contracts.
