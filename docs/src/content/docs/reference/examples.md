---
title: Runnable examples
description: Published query, HTMX, Scriban, SharpTS, and extension examples with prerequisites and expected output.
---

## Query published pages

Prerequisites: AeroCMS running at `https://localhost:333`, a site mapped to `localhost`, and at least one published page in the active culture.

```powershell
Invoke-RestMethod 'https://localhost:333/api/v1/query/pages?skip=0&take=10'
```

Expected output:

```json
{
  "items": [
    {
      "id": "190000000000000001",
      "title": "Home",
      "slug": "home",
      "path": "/",
      "summary": null,
      "culture": "en-US",
      "publishedOn": "2026-07-26T00:00:00+00:00"
    }
  ],
  "totalItems": 1,
  "skip": 0,
  "take": 10
}
```

IDs and timestamps differ.

## Search published content

Prerequisites: a published `speaker` type with full-text indexed data.

```bash
curl --fail --show-error \
  "https://localhost:333/api/v1/query/content/speaker/search?q=distributed&mode=fulltext&skip=0&take=20"
```

Expected output: an `items` array plus `skip`, `take`, and `hasMore`. Use `mode=semantic` only after configuring the experimental embedding path.

## Request an HTMX representation

```bash
curl --fail --show-error \
  --header "HX-Request: true" \
  --header "Accept: text/html" \
  "https://localhost:333/api/v1/query/posts?skip=0&take=5"
```

Expected output: encoded semantic HTML, with `Vary: HX-Request, Accept` and `Cache-Control: private, no-store`.

## Scriban content tree

Illustrative page fragment. Prerequisite: the page declares a content query named `topics`.

```liquid
<nav aria-label="Topics">
  <ul>
  {{ for item in content.topics.roots }}
    <li><a href="/topics/{{ item.slug | html.url_encode }}">{{ item.title | html.escape }}</a></li>
  {{ end }}
  </ul>
</nav>
```

Expected output: one safe link per eager root. The exact URL helper availability must be verified in the current safe built-in set before publishing; if unavailable, precompute the URL in a trusted binding.

## SharpTS content import

Illustrative trusted-author source:

```typescript
import { topics } from "aero:content";

export function render(context: AeroRenderContext): string {
  const items = topics.roots
    .map(item => `<li>${item.title}</li>`)
    .join("");
  return `<section><h1>${context.page.title}</h1><ul>${items}</ul></section>`;
}
```

Expected output: a validated fragment or a railway validation failure. Aero's HTML importer remains responsible for accepting/rejecting the returned markup; do not regard string interpolation as safe by itself.

## Extension service result

Illustrative C# expected-failure handling:

```csharp
return await repository.FindAsync(id, cancellationToken) switch
{
    Option<Article>.Some found => found.Value,
    Option<Article>.None => AeroError.NotFoundError($"Article {id} was not found."),
    _ => AeroError.CreateError("Unexpected article lookup state.")
};
```

Prerequisite: use the current Aero railway `Option<T>`/`Result<T>` types and repository contract. Expected output: success for a scoped article or a typed failure that the endpoint maps to 404/500 without leaking internal details.
