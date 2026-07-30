---
title: Posts and documentation
description: Blog posts and hierarchical documentation authoring, preview, localization, publishing, and caching.
---

Posts and documentation entries are separate AeroCMS content domains. Both are site/culture scoped, have draft and published states, expose manager APIs, provide authenticated preview, and render public routes.

## Posts

The manager uses `/manager/posts` and `/manager/post/editor/{id}`. APIs under `/api/v1/admin/blogs` cover list, retrieve by ID/slug, translation groups, create, update, publish, unpublish, archive/delete, and preview. Taxonomy endpoints manage categories, tags, and series.

Post bodies are Markdown. The public blog routes render only published variants. Index/detail output caching uses the `BlogPolicy` and `BlogPartialPolicy` names with five-minute expiration and variation for pagination/slug plus site-aware policy dimensions.

Preview endpoints require `site:read` and return no-store output. A translated post is a separate culture variant; publish it independently.

## Documentation content

The manager uses `/manager/docs` for spaces and `/manager/docs/{spaceId}/sections/{sectionId}` for sections. Admin APIs under `/api/v1/admin/docs` cover list, ID/slug/category lookup, children, translations, create, update, create child, move, reorder, publish, unpublish, and delete.

Documentation entries form a validated hierarchy inside a space. Their public routes are `/docs` and `/docs/{*slug}`. Draft iframe preview uses `/_cms/preview/docs/drafts/{draftId}` with authentication, `site:read`, site-scoped lookup, and no-store headers.

Docs output-cache policies expire after ten minutes and use the `docs-index` tag. Cache eviction is coarse; it is not a cross-cache transaction.

## Authoring safety

- Keep site and culture selected before editing.
- Treat Markdown as authored content; rendered raw HTML remains constrained by the active Markdown/render pipeline.
- Review slugs and parent moves because public paths and aliases can change.
- Preview the exact variant before publishing.
- Do not assume a published source culture implies that translations are published.
- Use the public query API when another page needs metadata; do not expose manager clients to anonymous code.
