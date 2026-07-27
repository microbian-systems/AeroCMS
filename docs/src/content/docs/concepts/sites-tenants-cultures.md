---
title: Sites, tenants, and cultures
description: AeroCMS resolution, isolation, selection, authorization, and localization boundaries.
---

A tenant is the ownership boundary; a site is the host/content boundary inside a tenant; culture selects a localized variant inside a site. Treat all three as authoritative inputs, not presentation metadata.

## Public request resolution

`SiteResolutionMiddleware` resolves a site from the request host and establishes site context before public content is loaded. Culture selection uses the Aero request-culture provider followed by cookie, query-string, and `Accept-Language` providers. Public pages, posts, docs, and content queries filter by the resolved site and active culture and return only published records.

Unknown or ambiguous hosts must fail closed. Do not fall back to another tenant's site merely because it is marked default.

## Manager selection

The manager stores the selected site ID in the `AeroCms.SiteId` cookie. `DefaultSiteContext` reads the selected site for manager operations. The cookie is not an authorization grant: endpoints must also require authentication and an appropriate `site:*` policy, and the policy must validate the user's assignment to the selected site.

The standard policies are:

| Policy | Typical use |
| --- | --- |
| `site:read` | list, retrieve, preview, and use read-only tools |
| `site:create` | create a site-scoped draft or definition |
| `site:update` | update, move, publish, unpublish, or configure |
| `site:delete` | archive or delete where the feature supports it |

Administrator-only screens add role or `AeroAdmin` requirements. An endpoint under `/api/v1/admin` is not safe merely because of its URL; authorization metadata is the boundary.

## Culture variants

Pages, posts, docs, content items, navigation, and footers can have culture-specific records or translation groups. Forking to a culture creates a new variant rather than changing the source culture. Publication state belongs to the variant. Preview URLs carry the selected site's host and the target culture/version.

## Isolation checklist

- Resolve the site before loading any public record.
- Require authenticated manager identity and site policy on every manager/admin endpoint.
- Revalidate selected-site membership on sensitive calls, including MCP tools.
- Include site and culture in queries, unique constraints, route lookups, and cache variation.
- Keep tenant ID available for authorization/audit even when site ID is the normal query scope.
- Use Snowflake `long` IDs internally; validate decimal strings before conversion at JSON/script boundaries.
- Test a foreign site, foreign tenant, wrong culture, draft record, and missing site selection for every new feature.

See [security hardening](/operations/security/) for production controls and [identity and access](/guides/identity-and-access/) for authentication providers.
