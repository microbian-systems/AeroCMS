# Specification: Multi-Site Support (OSS) - Track multisite_20260331

## Overview
This track implements multi-site support for the open-source Aero CMS using a single database and host/domain-based resolution. Content across key modules will be scoped using an explicit `long SiteId`.

## Objectives
- Implement `long SiteId` ownership for Pages, Blogs, Media, Categories, Tags, Aliases, and Docs.
- Develop a host-based site resolution service and request-scoped accessor.
- Update Marten configuration with site-aware indexes and composite uniqueness rules.
- Integrate multi-site selection into the single Manager UI.
- Provide a migration path for existing single-site installations, generating the default `SiteId` using `Snowflake.NewId()`.

## Functional Requirements
### 1. Site Resolution & Access
- Resolve `SiteId` from the incoming HTTP request host.
- Provide `ICurrentSiteAccessor` for shared access to the current site context.
- Implement host normalization (lowercase, trim, strip port).
- Setup wizard (`@setup.cshtml`) to capture the initial site name.

### 2. Module Scoping
- Add `ISiteOwned` interface to all site-bound entities.
- Ensure CRUD operations stamp the `SiteId` from the current context.
- Update all site-bound events to include `SiteId`.
- Media storage to follow site-scoped folders: `/media/{SiteId}/...`.

### 3. Manager UI
- Single Manager interface with a site switcher for administrators.
- Filter all content lists (Pages, Posts, etc.) by the active `SiteId`.

### 4. Persistence
- Use Marten composite unique indexes: e.g., `(SiteId, Slug)`.
- Global uniqueness rules to be converted to site-scoped where applicable.

## Non-Functional Requirements
- **Performance:** `SiteId` must be indexed for all site-owned documents.
- **Security:** Standard requests must not trust client-supplied `SiteId`; it must be derived server-side.
- **Maintainability:** Preserve existing module boundaries.

## Acceptance Criteria
- [ ] Multiple sites can be served from a single deployment.
- [ ] `SiteId` is correctly resolved and stamped on all new content.
- [ ] Content is isolated by site (e.g., site A cannot see site B's pages).
- [ ] Media is stored in site-specific subfolders.
- [ ] Migration utility successfully assigns legacy content to a new default `SiteId`.

## Out of Scope
- SaaS-style database-per-tenant isolation.
- Marten's built-in multi-tenancy.
- Premium/dedicated hosting logic.
