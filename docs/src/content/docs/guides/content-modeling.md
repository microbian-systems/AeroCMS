---
title: Content model and hierarchy
description: Content types, fields, validation, hierarchy, traversal, indexing, search, and public rendering.
---

Structured content separates reusable data from page layout. A content type belongs to a site, has a stable alias, cardinality, structure, fields, rendering/search settings, and optional hierarchy rules. A content item belongs to a type, site, culture, draft/published state, and optional parent.

## Type choices

- **Singleton**: at most one item per site and culture.
- **Collection**: multiple items.
- **Flat**: parent IDs are forbidden.
- **Hierarchical**: parent/child placement is validated.

Hierarchical rules control whether roots are allowed, whether parents must use the same type, the maximum depth (default `8`), allowed cross-type parent IDs, and default ordering (`sortOrder,title`).

## Built-in field aliases

| Alias | Stored/editor intent | Important settings |
| --- | --- | --- |
| `text`, `richtext` | text or editor-authored rich text | required, length, indexing as defined by the field contract |
| `image`, `gallery` | media reference or bounded media list | gallery item limits |
| `number`, `boolean`, `date`, `url` | scalar values | type validation |
| `reference` | one or many content IDs | target type, hierarchy mode, leaf-only, ancestor display, dependent filter |
| `list` | bounded text/number values | item type, allowed values, min/max items |
| `dictionary` | bounded key/value entries | value type, min/max entries |
| `range` | inclusive integer range | start, end, negative-value policy |
| `color` | color value | editor and value validation |

Unknown fields are rejected or omitted according to the active validation/projection boundary; they are not silently persisted as an unbounded object bag.

## Create and publish

Manager endpoints are under:

- `/api/v1/admin/content-types`
- `/api/v1/admin/content-items`

Read operations require `site:read`; creation uses `site:create`; updates, moves, publishing, and unpublishing use `site:update`; deletion uses `site:delete`. Every operation must use the selected site and culture rather than trusting a site ID supplied in a request body.

Content types and items use Sable document sessions. Command services stage validation, content search projections, and document changes in a unit of work. Do not describe the obsolete Marten provider as current persistence.

## Hierarchy operations

The manager hierarchy endpoint can return a tree, move one item, and reorder siblings. The service:

1. loads the target and candidate parent in the current site/culture;
2. validates flat/hierarchical structure, root rules, parent type, allowed type IDs, cycle, and depth;
3. updates `ParentId`;
4. normalizes old and new sibling `SortOrder`;
5. commits once through the Sable session.

Breadcrumbs and ancestors are root-first. Bounded render/query traversal supports `Roots`, `Children`, `Descendants`, `Ancestors`, and `RootsWithDescendants`. `Children`, `Descendants`, and `Ancestors` require a root ID; root traversals reject one.

## Projection and search

Renderers receive immutable `ContentNode` trees with decimal-string IDs, alias, title, slug, projected JSON fields, and eager children. Requests bound maximum depth, maximum items, projection fields, and whether a trusted preview can include drafts. `WasTruncated` signals that a candidate, depth, item, or output-size bound was reached.

Search projection documents are written alongside content changes. Full-text search is the baseline mode. Semantic mode is exposed by contract but depends on a configured embedding path and remains experimental. Always paginate and keep the site/culture/published filters intact.

## Public URL rendering

A content type can define a public URL/template. The public renderer loads only the current site's published item and renders its Scriban template through the secure pipeline. This is separate from the JSON/HTMX [public query API](/guides/public-query-api/).
