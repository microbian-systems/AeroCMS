# Aero CMS OSS Multi-Site Specification

## Document Purpose

This specification defines the implementation plan for adding **multi-site support** to the open-source Aero CMS product using **MartenDB** and **host/domain-based site resolution**.

This is **not** the SaaS multi-tenant architecture.  
This is the OSS, single-database, multi-site architecture.

The goal is:

- one Aero CMS deployment
- one database
- multiple hosted sites
- the active site resolved by request host/domain
- all site-owned content scoped by a `long SiteId`

This specification is written for an AI coding agent and should be followed carefully. This refactor touches abstractions, modules, events, validation, persistence, routing, and tests.

---

## Scope

### In scope

Implement explicit site scoping for the following modules:

- Pages
- Posts / Blogs
- Media
- Categories
- Tags
- Aliases
- Docs

Implement:

- `SiteId` ownership on site-owned data
- host/domain-based site resolution
- request-scoped current site access
- site-aware queries and writes
- site-aware validation
- site-aware events
- Marten indexes / uniqueness rules for site-scoped data
- migration/backfill path for existing single-site data

### Out of scope

Do **not** implement:

- SaaS tenant isolation
- database-per-tenant routing
- YARP integration
- premium/dedicated hosting logic
- Marten built-in document tenancy for this feature

---

## Architectural Decision

### Chosen model

Use **explicit site ownership** with `long SiteId` on all site-owned documents/entities.

### Why

This is the best fit for the OSS Aero CMS product because:

- Aero OSS is a single logical product deployment
- multiple hosted sites exist within one CMS instance
- `Site` is a content ownership boundary, not a SaaS tenant
- explicit `SiteId` is simple, visible, testable, and maintainable
- explicit `SiteId` allows straightforward querying, indexing, validation, and eventing

### Rejected approach

Do **not** use Marten built-in multi-tenanted documents for this feature.

Reason:

- Marten multi-tenancy is aimed at tenant isolation concerns
- Aero OSS multi-site is a domain scoping concern inside one database
- using explicit `SiteId` keeps the open-source CMS clean and avoids coupling OSS design to future SaaS tenancy

---

## Repository Structure Notes

Shared contracts, requests, validators, view models, and events are in:

- `src/Aero.Cms.Abstractions/Aero.Cms.Abstractions.csproj`

Each site-owned pillar exists in its own module project, following a structure like:

- `Aero.Cms.Modules.Pages`
- `Aero.Cms.Modules.Posts`
- `Aero.Cms.Modules.Media`
- `Aero.Cms.Modules.Categories`
- `Aero.Cms.Modules.Tags`
- `Aero.Cms.Modules.Aliases`
- `Aero.Cms.Modules.Docs`

This refactor must preserve module boundaries as much as possible.

---

## High-Level Rules

1. `Site.Id` is of type `long`.
2. All site-owned content must contain `long SiteId`.
3. `Site` itself must **not** contain `SiteId`.
4. Normal create/update/delete requests must **not** trust client-supplied `SiteId`.
5. The active site must be resolved from the incoming host/domain.
6. All site-owned reads must be filtered by `SiteId`.
7. All site-owned writes must stamp `SiteId` from the resolved current site.
8. All create/update/delete events for site-owned modules must include `SiteId`.
9. All uniqueness rules must be reviewed and converted from global scope to site scope where appropriate.
10. All relationships between site-owned records must enforce same-site ownership.

---

## Domain Model

### Site

`Site` is the parent container for hosted CMS content.

Minimum required fields:

```csharp
public class Site
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string PrimaryHost { get; set; } = null!;
    public List<string> Hosts { get; set; } = [];
    public bool IsEnabled { get; set; } = true;
    public string? DefaultCulture { get; set; }
}
```

Notes:

- `PrimaryHost` is the canonical host/domain.
- `Hosts` contains secondary domains, aliases, or local development hostnames if needed.
- `IsEnabled` allows a site to be disabled without deleting it.
- `Site` is **not** site-owned and must not contain `SiteId`.

### Site-owned modules

The following models/entities must gain `SiteId: long`:

- Page
- Post / Blog
- Media
- Category
- Tag
- Alias
- Doc

Example shape:

```csharp
public class Page : ISiteOwned
{
    public long Id { get; set; }
    public long SiteId { get; set; }
    public string Slug { get; set; } = null!;
    public string Title { get; set; } = null!;
}
```

---

## Abstractions Changes

## 1. Add ISiteOwned

Add a shared contract in `Aero.Cms.Abstractions`.

Recommended location:

- `src/Aero.Cms.Abstractions/Interfaces/ISiteOwned.cs`
- or merge into existing `Interfaces/Common.cs` if that better fits the project

Definition:

```csharp
namespace Aero.Cms.Abstractions.Interfaces;

public interface ISiteOwned
{
    long SiteId { get; set; }
}
```

Use an interface unless there is a strong existing base-class strategy that already governs all persisted models.

## 2. Add current site runtime contracts

Add:

```csharp
public interface ICurrentSiteAccessor
{
    SiteViewModel? CurrentSite { get; }
    long? SiteId { get; }
    bool HasSite { get; }
}
```

Add:

```csharp
public interface ISiteResolver
{
    Task<SiteViewModel?> ResolveAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default);
}
```

If the abstractions library must remain ASP.NET-free, move the `HttpContext`-based interface into the web host layer and keep only a transport-neutral site lookup abstraction in abstractions.

Example transport-neutral alternative:

```csharp
public interface ISiteLookupService
{
    Task<SiteViewModel?> ResolveByHostAsync(
        string host,
        CancellationToken cancellationToken = default);
}
```

Preferred split:

- abstractions: `ICurrentSiteAccessor`, `ISiteLookupService`, `ISiteOwned`
- web host: `ISiteResolver`, middleware, host normalization

## 3. Update view models

Update these view models in `src/Aero.Cms.Abstractions/Models` to include `SiteId`:

- `AliasViewModel`
- `CategoryViewModel`
- `DocViewModel`
- `MediaViewModel`
- `PageViewModel`
- `PostViewModel`
- `TagViewModel`

Do **not** add `SiteId` to `SiteViewModel`, because the site's primary key is already `Id`.

### View model guidance

Add `SiteId` where the view model represents a persisted site-owned entity or where the admin UI needs ownership visibility.

If a view model is purely presentational and never crosses module boundaries, the module may keep `SiteId` internal. However, for the listed abstractions models, include it for clarity and downstream usage.

## 4. Requests

Requests in `src/Aero.Cms.Abstractions/Requests` should generally **not** expose `SiteId` for standard site-bound operations.

This includes:

- `CreateAliasRequest`
- `CreateCategoryRequest`
- `CreateDocRequest`
- `CreateMediaRequest`
- `CreatePageRequest`
- `CreatePostRequest`
- `CreateTagRequest`

Default rule:

- request payload does not include `SiteId`
- handler/service stamps `SiteId` from current site context

This prevents cross-site writes by forged input.

If there is a future super-admin API that must manage multiple sites centrally, create dedicated admin-specific request contracts rather than overloading the normal CMS requests.

## 5. Validators

Update all validators so uniqueness and integrity checks are site-scoped.

Examples:

- page slug unique within site
- post slug unique within site
- category name/slug unique within site
- tag name/slug unique within site
- alias source path unique within site
- doc path unique within site

Do not assume global uniqueness unless that is explicitly intended.

---

## Event Contract Changes

All create/update/delete events for site-owned modules in `Aero.Cms.Abstractions/Events` must include `SiteId`.

Examples of likely affected events:

- page created / updated / deleted
- post created / updated / deleted
- media created / updated / deleted
- category created / updated / deleted
- tag created / updated / deleted
- alias created / updated / deleted
- doc created / updated / deleted

Example event shape:

```csharp
public sealed record PageCreated(
    long Id,
    long SiteId,
    string Slug,
    string Title);
```

Rules:

1. Every site-owned CRUD event must contain `SiteId`.
2. Event publishers must populate `SiteId` from the persisted record or current site context.
3. Event consumers must use `SiteId` when rebuilding projections, invalidating caches, recomputing routes, or updating search/sitemaps.

---

## Host Resolution Design

## Site resolution source

Resolve the current site from the HTTP request host/domain.

Matching order:

1. exact normalized match on `PrimaryHost`
2. exact normalized match on any entry in `Hosts`
3. optional development fallback if explicitly configured
4. fail with site-not-found behavior

## Host normalization rules

Create a single reusable host normalization utility.

Behavior:

- lowercase
- trim whitespace
- strip port
- trim trailing `.`
- preserve exact hostname semantics after normalization
- optionally support punycode normalization later

Example inputs:

- `Example.COM` -> `example.com`
- `example.com:5001` -> `example.com`
- `example.com.` -> `example.com`

This normalization logic must be used consistently by:

- middleware
- site creation/update validation
- repository lookup logic
- seed/migration utilities

---

## Runtime Request Pipeline

## SiteResolutionMiddleware

Add middleware in the web host layer to resolve the current site before content routing executes.

Responsibilities:

1. read request host
2. normalize host
3. resolve `Site`
4. set request-scoped current site accessor
5. short-circuit with not-found behavior if site does not exist or is disabled

Suggested names:

- `SiteResolutionMiddleware`
- `CurrentSiteMiddleware`

## CurrentSiteAccessor implementation

Example:

```csharp
public sealed class CurrentSiteAccessor : ICurrentSiteAccessor
{
    public SiteViewModel? CurrentSite { get; internal set; }
    public long? SiteId => CurrentSite?.Id;
    public bool HasSite => CurrentSite is not null;
}
```

Register as scoped.

Middleware should set it once per request.

## Site lookup service

Implement a service to resolve a site by normalized host.

Behavior:

- look up by `PrimaryHost`
- if not found, search `Hosts`
- enforce `IsEnabled`
- return `SiteViewModel` or domain model as appropriate

Start simple. Add caching later only if needed.

---

## Persistence and Marten Strategy

## Marten usage

Keep Marten in regular single-database mode for the OSS product.

Do **not** use Marten document tenancy for this feature.

## SiteId indexing

For each site-owned document/entity, configure Marten-managed indexing for `SiteId`.

Rationale:

- nearly every query will filter by `SiteId`
- performance will degrade if `SiteId` is not indexed

Use Marten-managed indexes, not unmanaged manual drift where avoidable.

## Composite uniqueness rules

Add composite uniqueness/index rules for site-scoped uniqueness.

Examples:

### Pages

- unique `(SiteId, Slug)`

### Posts

- unique `(SiteId, Slug)`

### Categories

- unique `(SiteId, Slug)` or `(SiteId, Name)` depending on business rule

### Tags

- unique `(SiteId, Slug)` or `(SiteId, Name)` depending on business rule

### Aliases

- unique `(SiteId, SourcePath)`

### Docs

- unique `(SiteId, Slug)` or `(SiteId, Path)` depending on routing model

If there are route-like uniqueness rules already defined globally, convert them to site-scoped composites.

## Query guidance

Every site-owned query must include `SiteId`.

Examples:

Bad:

```csharp
session.Query<Page>().Where(x => x.Slug == slug)
```

Good:

```csharp
session.Query<Page>().Where(x => x.SiteId == siteId && x.Slug == slug)
```

---

## Per-Module Implementation Requirements

## Pages

### Additions

- add `SiteId`
- update mappings/indexes
- update CRUD handlers/services
- update list/search/read queries
- update slug lookup
- update validators
- update events

### Rules

- slug unique within site
- page lookup by slug must include site
- homepage uniqueness, if applicable, must be per site

## Posts / Blogs

### Additions

- add `SiteId`
- update CRUD handlers/services
- update archive/published queries
- update slug lookup
- update validators
- update events

### Rules

- slug unique within site
- featured and published queries must be site-scoped
- post/category/tag joins must remain inside same site

## Media

### Additions

- add `SiteId`
- update upload/create/update/delete logic
- update library browsing/search
- update validators
- update events

### Rules

- current site should only see current site media unless a future shared-media feature is explicitly added

## Categories

### Additions

- add `SiteId`
- update CRUD/read logic
- update validators
- update events

### Rules

- category slug or name uniqueness must be per site
- category relationships must be same-site only

## Tags

### Additions

- add `SiteId`
- update CRUD/read logic
- update validators
- update events

### Rules

- tag slug or name uniqueness must be per site
- tag relationships must be same-site only

## Aliases

### Additions

- add `SiteId`
- update redirect lookup logic
- update CRUD/read logic
- update validators
- update events

### Rules

- alias/source path uniqueness is per site
- alias resolution must be `(SiteId + path)`, not path alone

## Docs

### Additions

- add `SiteId`
- update CRUD/read/tree logic
- update validators
- update events

### Rules

- document hierarchy must remain inside same site
- parent/child relationships cannot cross sites
- doc path uniqueness is per site

## Sites

### Notes

`Site` does not receive `SiteId`.

Update only as needed for:

- host resolution
- CRUD for sites
- host uniqueness validation
- seed data
- admin management UI

---

## Application Layer Rules

## Reads

All site-owned reads must accept or derive `siteId` and filter on it.

Preferred repository/query signatures:

```csharp
Task<Page?> GetBySlugAsync(long siteId, string slug, CancellationToken ct);
Task<IReadOnlyList<Post>> ListPublishedAsync(long siteId, CancellationToken ct);
Task DeleteAsync(long siteId, long id, CancellationToken ct);
```

## Writes

All site-owned writes must:

1. resolve current site
2. stamp `SiteId`
3. persist only within that site boundary

## Update/delete safety

All update/delete logic must verify the current site owns the record.

Example rule:

- load entity by id
- ensure `entity.SiteId == currentSiteId`
- reject otherwise

Do not permit cross-site mutation by ID alone.

## Relationships

Every relation between site-owned data must enforce same-site membership.

Examples:

- post -> category
- post -> tag
- doc -> parent doc
- alias -> target resource if applicable

No cross-site linking unless explicitly designed and documented later.

---

## Migration and Upgrade Plan

## Goal

Support upgrade from a legacy single-site Aero installation to the new multi-site schema.

## Required steps

1. create a default site
2. backfill all existing site-owned records to that default site
3. update schema/indexes
4. make `SiteId` required going forward

## Default site

Create a default site such as:

- `Id = 1`
- `Name = "Default Site"`
- `PrimaryHost` from configuration, known host, or localhost fallback
- `IsEnabled = true`

## Backfill order

1. create default site
2. backfill pages
3. backfill posts
4. backfill media
5. backfill categories
6. backfill tags
7. backfill aliases
8. backfill docs
9. backfill any join/relationship documents or tables

## Post-backfill rule

After migration completes:

- all site-owned records must have non-null, valid `SiteId`
- all CRUD paths must stamp `SiteId`
- all indexes/uniqueness rules must assume `SiteId` exists

---

## Admin and Manager UI Considerations

This specification prioritizes correctness of content isolation over complete multi-site management UX.

Minimum expected behavior:

- admin/manager screens operate in a current site context
- site-owned lists and editors show only the active site's records
- site-specific routes resolve against current host

Future enhancements may add:

- explicit site selector in admin
- super-admin site management
- central cross-site dashboards

Do not over-design these in this refactor.

---

## Testing Strategy

## Unit tests

Add tests for:

- host normalization
- site lookup by primary host
- site lookup by secondary host
- disabled site rejection
- `SiteId` stamping on create
- same slug allowed on different sites
- uniqueness enforcement inside same site
- prevention of cross-site update/delete
- same-site relationship enforcement

## Integration tests

Add Marten-backed integration tests for:

- request to site A returns only site A content
- request to site B returns only site B content
- page/post/doc lookup by slug is site-scoped
- alias resolution is site-scoped
- update/delete by wrong site is rejected
- category/tag joins cannot cross site boundary
- events emitted on CRUD contain correct `SiteId`

## Migration tests

Add upgrade-path tests from a legacy single-site schema/data set to the new multi-site schema.

Verify:

- default site created
- all legacy content assigned to default site
- existing content remains accessible under default site host
- new site-scoped uniqueness rules do not corrupt migrated data

---

## Implementation Phases

## Phase 1 - Foundations

1. add `ISiteOwned`
2. add current site contracts
3. add host normalization utility
4. add site lookup service
5. add site resolution middleware
6. wire scoped `ICurrentSiteAccessor`

## Phase 2 - Model and abstraction refactor

7. add `SiteId` to all site-owned persisted models
8. add `SiteId` to relevant abstractions view models
9. update events to include `SiteId`
10. update validators for site scope

## Phase 3 - Marten mapping and migration

11. add Marten indexes for `SiteId`
12. add composite uniqueness rules
13. create default site seed/migration
14. add legacy data backfill logic

## Phase 4 - Module refactor

15. refactor Pages
16. refactor Posts
17. refactor Media
18. refactor Categories
19. refactor Tags
20. refactor Aliases
21. refactor Docs

For each module update:

- model
- repository/query logic
- handlers/services
- validation
- events
- tests

## Phase 5 - Hardening

22. enforce ownership checks on update/delete
23. enforce same-site relationship integrity
24. review every list/search/query endpoint for missing `SiteId`
25. add integration and migration coverage

## Phase 6 - Documentation

26. add upgrade notes
27. add developer notes on current site resolution
28. add module authoring guidance for future modules to implement `ISiteOwned` when appropriate

---

## Acceptance Criteria

The feature is complete when all of the following are true:

1. One Aero CMS instance can serve multiple sites from one database.
2. Current site is resolved from request host/domain.
3. Each site-owned module persists and queries by `SiteId`.
4. `SiteId` is a `long`.
5. The `Site` entity does not contain `SiteId`.
6. Same slugs/paths/names can exist on different sites where business rules allow.
7. Same slugs/paths/names cannot collide within the same site where uniqueness applies.
8. CRUD events for site-owned modules include `SiteId`.
9. Cross-site updates/deletes by ID alone are rejected.
10. Existing single-site databases can be upgraded via migration/backfill.

---

## Agent Execution Rules

1. Do not trust client-supplied `SiteId`.
2. Be explicit with `SiteId` in application logic.
3. Do not use hidden magic/global filters as the only safety mechanism.
4. Preserve existing module boundaries unless correctness requires change.
5. Keep all CRUD event contracts site-aware.
6. Keep `Site` as the parent container only.
7. Favor clarity and correctness over clever abstractions.
8. Treat this as a large refactor and update tests alongside code changes.

---

## Suggested Agent Prompt

```text
Implement multi-site support for Aero CMS OSS using explicit SiteId scoping with MartenDB.

Requirements:
- Single deployment, single database, multiple hosted sites
- Resolve current site by request host/domain
- Site.Id type is long
- Add long SiteId to all site-owned modules: pages, posts/blogs, media, categories, tags, aliases, docs
- Do NOT add SiteId to Site itself
- Do NOT use Marten built-in multi-tenanted documents for this feature
- All reads/writes/validators/events for site-owned modules must be site-aware
- All create/update/delete events in Aero.Cms.Abstractions must include SiteId
- Do not trust client-supplied SiteId; derive it from the resolved current site
- Enforce same-site relationship integrity across linked entities
- Backfill legacy records into a default site during migration
- Add integration tests for host-based site isolation

Repository structure notes:
- Shared contracts/events/view models/requests/validators are in src/Aero.Cms.Abstractions/Aero.Cms.Abstractions.csproj
- Each module has its own csproj under aero.cms.modules.{name}
- This is a large refactor; preserve existing module boundaries as much as possible

Implementation expectations:
1. Add shared interfaces/contracts for ISiteOwned and current-site access
2. Add host normalization and site resolution middleware/services
3. Update persisted models and relevant view models with SiteId
4. Refactor handlers/services/repositories/queries so all site-owned access is filtered by SiteId
5. Update validators to enforce uniqueness within site scope
6. Update events to include SiteId and ensure handlers publish it correctly
7. Add Marten-managed indexes/unique constraints for SiteId-scoped queries
8. Create migration/backfill for existing data into a default site
9. Add unit and integration tests for cross-site isolation and same-slug-different-site scenarios

Important rules:
- Be explicit with SiteId in application logic
- Prevent cross-site updates/deletes by ID alone
- Keep Site as the parent container, not a site-owned entity
- Preserve abstractions and module boundaries unless a change is necessary for correctness
```
