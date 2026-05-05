# Aero CMS Manager Site Tasks

> **Status**: Council-vetted plan. Updated 2026-05-04 with findings from multi-LLM architecture review.

## Multi-Tenancy and Site Scope

The anticipated multi-tenancy model does not combine tenants into a shared host.

- One Docker container maps to one tenant.
- A tenant container can serve multiple sites.
- Site resolution happens inside the CMS by hostname/domain lookup.
- Each tenant has its own non-shared Postgres database.
- That database can contain data for multiple sites.
- Every site-owned table/document/entity must have a `long SiteId` foreign key.

Multi-tenancy is handled at the proxy level. After load balancers hit the proxies, the proxy will resolve the incoming domain name to a tenant id, find the right service/container, and forward the request there. This is future work and is listed here for informational purposes.

---

## Architecture Decisions From Review

### Modules Are Per Instance

Do not implement per-site or per-tenant module enablement.

- Modules are compiled into and registered for the container instance.
- The module catalog, dependency graph, DI registration, and module lifecycle are per-instance.
- A site must not rebuild DI or run module startup separately.
- Remove/ignore tenant-level module enablement decisions from runtime module flow.
- Keep environment-level module policy if needed, such as `DisabledInProduction`; that is not the same as tenant/site module enablement.

### Site-Owned Content

Site ownership is still required for content/data.

- Pages, posts/blogs, docs, aliases, banners, taxonomy, navigation, media, and other site-owned models must have `long SiteId`.
- All site-owned types must implement `ISiteOwned { long SiteId { get; set; } }` — **does not exist yet, must be added** at `src/Aero.Cms.Abstractions/Interfaces/ISiteOwned.cs`.
- Site-owned reads must filter by `SiteId`.
- Site-owned writes must stamp `SiteId` from the resolved current site context.
- Normal create/update/delete requests should not trust client-supplied `SiteId`.
- Site-owned events should include `SiteId`.

### Setup Bootstrap

Setup should continue creating the tenant and default site as it does now.

- Tenants are generally managed outside the CMS in the future hosting/provisioning model.
- The setup/bootstrap flow creates a local/default tenant record and a default site via `SeedDataService.cs` (already implemented).
- Retain the created tenant id and site id in setup state.
- Seed starter pages, posts, docs, media, navigation, taxonomy, and other site-owned data with the default site's `SiteId`.

### Site Domains (Multi-Domain / CNAME Support)

The Site model must support multiple domains/CNAMEs. The current entity `SitesModel` had only a single `string? Hostname` and later `Hosts: List<string>`. Two options evaluated:

| Option | Structure | Pros | Cons |
|--------|-----------|------|------|
| **Separate `SiteHost` document** ✅ (implemented) | `SiteHost { long SiteId, string Host, bool IsPrimary }` — unique index on `Host` | Marten can unique-index each row; fast host lookup; prevents domain collision across sites | Extra document type; one more type in cascade delete |
| Inline list on `SitesModel` | `SitesModel.Hosts: List<string>` | Simpler API | Marten cannot unique-index list elements — **real risk of non-deterministic host resolution** |

**Decision**: Use **separate `SiteHost` document** for domain storage — ✅ **implemented 2026-05-04**.
- Entity `SiteHost` created at `src/Aero.Cms.Core.Entities/SiteHost.cs` with `SiteId`, `Host` (unique-indexed), `IsPrimary`.
- `SitesModel.PrimaryHost` and `SitesModel.Hosts` removed — host resolution now goes through `SiteHost`.
- `SiteLookupService` queries `SiteHost` first via `SiteHost.Host` btree match, then loads parent `SitesModel`.
- `SiteService` provides `AddHostAsync`, `RemoveHostAsync`, `GetHostsAsync`, `ReplaceHostsAsync` for host CRUD.
- Site Settings UI (future) lists/edits `SiteHost` entries for the current site.
- Display immutable tenant id and site id in Site Settings.

### Two Independent Site Resolution Paths

The system uses **two separate resolution paths** for the current site:

| Path | Used By | Mechanism | Data Source |
|------|---------|-----------|-------------|
| **Hostname resolution** | Public frontend (content rendering) | `SiteResolutionMiddleware` → `ISiteLookupService.ResolveByHostAsync()` | `SiteHost.Host` match |
| **Explicit selection** | Admin manager UI | `ICurrentSiteAccessor` backed by cookie `AeroCms.SiteId` | User's selected site in manager |

**Critical rule**: `SiteResolutionMiddleware` skips `/manager/*` routes. The manager resolves the site from user selection, not hostname.

### Current Site Context Contracts

Two separate abstractions serve different purposes:

| Contract | Location | Purpose |
|----------|----------|---------|
| `ISiteContext { long SiteId, long TenantId }` | `Aero\src\Aero.Core\Http\ISiteContext.cs` | Low-level content queries — used by all content services (Pages, Posts, Docs, Media) |
| `ICurrentSiteAccessor` | `Aero.Cms.Abstractions/Interfaces/ICurrentSiteAccessor.cs` | Manager-side state — reads/writes current selected site from cookie |

### DefaultSiteContext — Dual Resolution

`DefaultSiteContext` (web host, `src/Aero.Cms.Web/Infrastructure/DefaultSiteContext.cs`) implements `ISiteContext` with a **fallback chain**:

```csharp
public long SiteId
{
    get
    {
        // 1. Try features (set by SiteResolutionMiddleware for public routes)
        var slice = _httpContextAccessor.HttpContext?.Features.Get<IAeroSiteSlice>();
        if (slice is not null) return slice.SiteId;

        // 2. Fallback: read from manager cookie (for /manager/* API calls)
        var cookie = _httpContextAccessor.HttpContext?.Request.Cookies["AeroCms.SiteId"];
        if (long.TryParse(cookie, out var siteId)) return siteId;

        return 0;
    }
}
```

### How Content Scoping Works

All content services (MartenPageContentService, BlogPostContentService, DocsService, etc.) filter queries by `ISiteContext.SiteId`:

```csharp
// Example: MartenPageContentService.GetAllPagesAsync
var query = session.Query<PageDocument>().Where(x => x.SiteId == _siteContext.SiteId);
```

Because `DefaultSiteContext` now resolves the SiteId from:
- **Public routes**: features set by `SiteResolutionMiddleware` (hostname-based)
- **Manager routes**: `AeroCms.SiteId` cookie (user selection)

...the existing services automatically scope content to the current site **without any code changes**. The data flow for a manager API call is:

```
User selects site → cookie set → manager API call
  → DefaultSiteContext reads cookie → returns SiteId
  → Content service filters by SiteId → returns only this site's content
```

This means all existing manager pages (Pages.razor, Posts.razor, Docs.razor, Media.razor) automatically filter by the selected site site.

### User-Site Assignment Model (New)

Users must be assigned to sites with per-site permissions. This is a **new entity** — does not exist yet.

```csharp
// src/Aero.Cms.Core.Entities/UserSiteAssignment.cs
public class UserSiteAssignment : Entity
{
    public long UserId { get; set; }
    public long SiteId { get; set; }
    public List<string> Permissions { get; set; } = []; // "create", "read", "update", "delete"
}
```

**Why Marten document, not Identity claims**: Claims per site-path would create claim bloat for users on many sites. Marten documents support efficient querying, batch updates, and batch revocation. Claims remain for coarse identity (UserName, Email, Roles).

**Service**: `IUserSiteService` — Create, Update permissions, Remove assignment, GetAccessibleSites.

### RBAC — Per-Site Claims-Based Authorization (New)

Authorization is dual-layer:

1. **Coarse role**: `ClaimTypes.Role` — `Admin`, `Editor`, `Contributor`, `ViewOnly`
2. **Fine per-site claims**: From `UserSiteAssignment.Permissions[]` — `"create"`, `"read"`, `"update"`, `"delete"`

**Admin bypass rule**: Users with `ClaimTypes.Role == "Admin"` bypass `UserSiteAssignment` checks and see ALL sites in the site picker. However, site scoping still applies — Admin is still restricted to operating within the currently selected site. This avoids the complexity of a separate "SuperAdmin" role while maintaining data isolation.

**Authorization flow**:
1. Is user authenticated? → No → 401
2. Does user have `Admin` role? → Yes → Allow (within selected site scope)
3. Does `UserSiteAssignment` exist for `(UserId, currentSiteId)` with required permission? → Yes → Allow
4. Otherwise → 403

**Integration point**: Leverage the `IPermissionService` pattern from `docs/02_permissions_and_rbac.md` with `SitePermissionRequirement` for ASP.NET Core policy-based authorization.

### Cascade Site Delete (New)

Site deletion is destructive — all site-owned data across ALL tables must be removed. Two-phase approach:

1. **Soft-delete**: Set `SitesModel.IsEnabled = false`, record deletion timestamp. Site content becomes inaccessible but recoverable.
2. **Background deletion** (TickerQ job `SiteDeletionJob`):
   - Source-generator discovers all types implementing `ISiteOwned`
   - For each type: `session.DeleteWhere<T>(x => x.SiteId == siteId)` within a Marten `IDocumentSession` transaction
   - Delete `SiteHost` records, `UserSiteAssignment` records
   - Finally, hard-delete `SitesModel` itself

**Source-generator approach**: Extend the existing module catalog source generator to emit a `SiteOwnedTypes` static class listing all `ISiteOwned` implementations. No manual enumeration. No reflection.

### Blazor Dual-Rendering Concerns (New)

The manager uses **InteractiveServer + InteractiveWebAssembly** render modes. This creates state sync challenges:

**`ICurrentSiteAccessor` needs two implementations**:
- **Server**: Reads cookie via `IHttpContextAccessor.HttpContext.Request.Cookies["AeroCms.SiteId"]`
- **WASM**: Reads cookie via `IJSRuntime.InvokeAsync<string>("eval", "document.cookie")` (JavaScript interop)

**Circuit reconnection**: Blazor Server circuits recreate scoped DI on reconnect. The cookie ensures the site selection survives disconnection. Implement `ICircuitHandler` to re-read the cookie on reconnect.

**`ServerAuthenticationStateProvider` must be extended**: Currently returns only `UserName`, `Email`, `Roles`. Must also include `AccessibleSiteIds` and `IsAdmin` in the `/api/v1/admin/auth/me` response so the WASM client knows which sites to show in the site picker.

### ✅ Verified — No Longer Blockers

The following concerns were investigated and found to be **already resolved** in the current codebase:

| Claim | Resolution |
|-------|------------|
| `AeroSiteMiddleware` uses `Snowflake.NewId()` for TenantId | **Resolved** — Old middleware replaced by `SiteResolutionMiddleware` which correctly reads `site.TenantId` from the resolved site |
| `DefaultSiteContext` reads `X-Site-Id`/`X-Tenant-Id` headers | **Resolved** — Reads from `HttpContext.Features.Get<IAeroSiteSlice>()` set by middleware, not headers |
| `SiteViewModel` missing `TenantId` | **Resolved** — `SiteViewModel` already has `TenantId` field (line 18) |

### Alias Indexing

Aliases are site-owned.

- Use unique `(SiteId, OldPath)` or `(SiteId, SourcePath)` for redirect lookup.
- Do not make `(SiteId, NewPath)` unique.
- A non-unique `(SiteId, NewPath)` index is acceptable if reverse lookup/search needs it.
- Multiple old paths must be allowed to redirect to the same new path.
- Alias resolution must always be `SiteId + old path`, never path alone.

### Docs Module Fix

`Aero.Cms.Modules.Docs` currently has an incomplete `SiteId` implementation.

- `CreateDocRequest` has `long SiteId`.
- The validator requires `SiteId > 0`.
- `DocsService` ignores that value.
- `DocsPage` has no `SiteId` field.

Required work:

- Add `long SiteId` to `DocsPage`.
- Update docs create/save paths so `SiteId` is stamped from the resolved current site context.
- Update docs list, slug, child, category, and page queries to filter by `SiteId`.
- Update docs view models/events as needed so `SiteId` is preserved.
- Convert docs slug/path uniqueness to site-scoped uniqueness, such as `(SiteId, Slug)` or `(SiteId, Path)` depending on the final docs routing model.
- Update starter docs seeding to stamp the default site id.

### Wolverine Handler Discovery

The source-generator/Wolverine decision remains unchanged.

- Generated handler discovery should be attribute based.
- Every generated-discovery Wolverine handler should use `[WolverineHandler]`.
- Keep analyzer coverage for intended handlers missing the attribute.
- Do not use broad interface scanning as the source-generator discovery mechanism.

---

## Implementation Phases

### Phase 0 — Verification (no code changes needed)

The old `AeroSiteMiddleware` with the random TenantId bug and header-based `DefaultSiteContext` have been resolved. Verify:

- [x] `SiteResolutionMiddleware` resolves TenantId correctly from `site.TenantId`
- [x] `DefaultSiteContext` reads from `HttpContext.Features`, not from headers
- [x] `SiteViewModel.TenantId` is populated in all queries

### Phase 1 — Foundation (new abstractions + data model)

- [x] Add `ISiteOwned` interface at `src/Aero.Cms.Abstractions/Interfaces/ISiteOwned.cs`
- [x] Create `UserSiteAssignment` entity at `src/Aero.Cms.Core.Entities/UserSiteAssignment.cs`
- [x] Create `SiteHost` entity for multi-domain support at `src/Aero.Cms.Core.Entities/SiteHost.cs`
- [x] Update `SitesModel` — add `Description` field; remove `PrimaryHost` and `Hosts` (moved to SiteHost)
- [x] Create `ICurrentSiteAccessor` interface at `Aero.Cms.Abstractions/Interfaces/ICurrentSiteAccessor.cs`
- [x] Implement `CurrentSiteAccessor` (Blazor-friendly, uses HttpClient → server API) at `src/Aero.Cms.Shared/Services`
- [ ] Implement `WasmCurrentSiteAccessor` (WASM, via JS interop) — **deferred; the HttpClient-based `CurrentSiteAccessor` works for both render modes**
- [x] Update Marten config in `SitesModule`:
  - [x] Removed unique index on `Hostname` (field removed from `SitesModel`)
  - [x] Added unique index on `SiteHost.Host`
  - [x] Added indexes on `UserSiteAssignment.UserId` and `SiteId`
- [ ] Extend source generator to emit `SiteOwnedTypes` static class — **deferred to Phase 4 (cascade delete)**
- [x] Extend `ServerAuthenticationStateProvider` to include `IsAdmin` in `/me` response
- [x] Add current-site API endpoints: `GET/POST/DELETE /api/v1/admin/sites/current`
- [x] Add `SitePermissionRequirement` + `SitePermissionHandler` for ASP.NET Core policy-based auth

### Phase 2 — RBAC & Authorization

- [x] Create `IUserSiteService` with CRUD for assignments and permission checking
- [x] Implement `UserSiteService` — resolves site access from `UserSiteAssignment` or admin bypass
- [x] Add `SitePermissionRequirement` for ASP.NET Core policy-based auth
- [x] Add site authorization policies: `site:read`, `site:create`, `site:update`, `site:delete`
- [x] **Manager middleware skip**: Updated `SiteResolutionMiddleware` to skip `/manager/*` routes
- [ ] Wire authorization into existing manager API endpoints — **deferred to Phase 3 (when endpoints are built)**

### Phase 3 — Manager UX

- [x] **Site Selection Gate**: New page at `/manager/select-site` — card grid, auto-select single site, admin manage link
- [x] **Site CRUD pages**: `/manager/sites` — RadzenDataGrid with inline create/edit forms, delete confirmation, "Set as Current" action
- [x] **User-Site Assignment UI**: `/manager/users/{id}/sites` — real working page with per-site permission checkboxes (read/create/update/delete), Select All/Clear All, Save
- [x] **Header indicator**: Site name badge in `ManagerHeader.razor`, click opens `/manager/select-site`
- [x] **Header keyboard shortcut**: `CTRL+S` to open site picker — wired via `@onkeydown` on header element
- [x] **NavMenu updates**: Sites section added below Dashboard, Databases removed, Taxonomy simplified to Categories + Tags, Global Settings anchored at bottom
- [x] **Content scoping**: `DefaultSiteContext` falls back to `AeroCms.SiteId` cookie for `/manager/*` routes — all content services automatically filter by selected site
- [ ] Wire `[Authorize(Policy = "site:read")]` etc. onto content API endpoints — **deferred**

### Phase 3b — Page Editor Enhancements

- [x] **Slug auto-population**: Title → slug on new pages; lock on manual edit or DB load
- [x] **Unicode slug support**: Diacritics normalized (café → cafe) via `NormalizationForm.FormD`
- [x] **Dirty state tracking**: `PageState { Clean, Dirty }` — auto-save only fires when dirty
- [x] **New page auto-save**: Auto-creates on first save if content exists (blocks or title)
- [x] **Draft storage**: `PageDraft` entity — auto-save writes to draft, manual save/publish promotes to `PageDocument` + deletes draft
- [x] **Draft recovery on reload**: `LoadPageAsync` checks for draft — uses draft data if one exists
- [x] **Draft API**: `GET/PUT/DELETE /api/v1/admin/pages/{id}/draft`
- [x] **`ModifiedBy` stamping**: Pages, Docs, and Blog services now stamp `ModifiedBy` from `IHttpContextAccessor`

### Phase 3c — Post Editor Enhancements

- [x] **Dirty state tracking**: `PostState { Clean, Dirty }` — auto-save only fires when dirty
- [x] **3-second auto-save interval**: Reduced from 30s to 3s, gated by dirty flag
- [x] **New post auto-save**: Auto-creates on first save if content exists
- [x] **All inputs wired**: Title, slug, content, excerpt, featured image, category, tags all call `MarkDirty()`

### Phase 4 — Cascade Delete Implementation

- [ ] Create `SiteDeletionJob` (TickerQ background job)
- [ ] Soft-delete flow: `SiteService.DeleteSiteAsync()` sets `IsEnabled = false`
- [ ] Job deletes all `ISiteOwned` data via source-generated type list
- [ ] Transactional safety: use `IDocumentSession` transaction per site
- [ ] Audit logging: log every entity type deletion count
- [ ] UI: show deletion progress / completion timestamp

### Phase 5 — Multi-Domain & Hardening

- [x] `SitesModel.PrimaryHost` and `Hosts` removed — host resolution now goes through `SiteHost`
- [x] `SiteLookupService` queries `SiteHost` via unique-indexed `Host` btree match
- [ ] Add `HostNormalizer` validation to site domain editing
- [ ] Integration tests: hostname resolution, cross-site isolation, cascade delete
- [ ] Security audit: verify IDOR protection (every read/write checks `entity.SiteId == currentSiteId`)

---

## Remaining Tasks for the Aero CMS Manager

### Manager

- Dashboard UI makeover
    - UI: needs to get the UI from the dashboard path `D:\html-templates\mosaic`
        - Don't need to make it functional but the UI replacing the current UI page would be great (markup - the `.html` file has to be a `.razor` or `.cshtml`)
    - ~~Remove Settings as a submenu and put it as an anchor at the bottom of the left-side menu.~~ ✅ Done — Global Settings already anchored at bottom in `NavMenu.razor` lines 86-88.

### Sites Feature (Detailed)

- Site should have a `TenantId` for reference only; tenants are managed outside the CMS long term. ✅ Done.
- Site should support multiple domains/CNAMEs via `SiteHost` document. ✅ Done.
- After the last top nav menu item, display the currently selected site. ✅ Done.
- Clicking the current site opens the site selection menu. ✅ Done (navigates to `/manager/select-site`).
- `CTRL + S` should open the site selection menu. ✅ Done (wired via `@onkeydown` on header).
- Site Settings should be positioned just under the Dashboard menu item. ❌ Not done (no dedicated Settings page yet).
- Site Settings is site-specific, not global. ❌ Not done.
- User can edit site name. ✅ Done (via `/manager/sites` CRUD).
- User can edit site domains/CNAMEs. ✅ Done (via `/manager/sites` CRUD — hosts managed via `SiteHost`).
- User can edit site description. ✅ Done.
- Display immutable tenant id. ❌ Not done.
- Display immutable site id. ❌ Not done.
- **New**: User-site assignment via `UserSiteAssignment` entity. ✅ Entity exists. ✅ Working UI at `/manager/users/{id}/sites`.
- **New**: Per-site RBAC claims (create/read/update/delete). ✅ Policies exist. ❌ Not wired to endpoints.
- **New**: Cascade site delete (soft-delete + background job). ❌ Not implemented.

### Aliases Menu

- Place directly under the new Sites menu item. ❌ Not done.
- Main module is `Aero.Cms.Modules.Aliases`.
- Add UI/API support for creating aliases with old URL and new URL.
- Automatically create/update aliases when a URL/slug changes for blog, page, or doc rename.
- Add/confirm `AliasViewModel` in `Aero.Cms.Abstractions`.
- Create the alias API in `Aero.Cms.Modules.Headless`.
- Add an HTTP client for the new alias API.
- Add/confirm FluentValidation validators for the alias model/request.
- Change alias uniqueness from global old path to unique `(SiteId, OldPath)`.
- Keep `NewPath` non-unique; optionally add non-unique `(SiteId, NewPath)` index for reverse lookup.

### Banners Menu

- Add a Banners item after Aliases. ❌ Not done.
- Banners allow sites to display sitewide banners.
- Banner feature code lives in `Aero.Cms.Modules.Banners`.
- Banners are site-owned and need `SiteId`.
- Create the banner API in `Aero.Cms.Modules.Headless`.
- Add an API client for the banners API.
- Add a FluentValidation validator for the banner model/request.

### Navigation (NavMenu) Module

- Add a new NavMenu block registered with source generators. ❌ Not done.
- Navigation/menu records are site-owned and need `SiteId`.
- API and supporting code should follow the Aero CMS module creation skill.

### Global Settings

- Under the main left-side menu, add a Settings button anchored at the bottom. ✅ Done.
- Global settings items are TBD.
- Global settings are instance-level, not site-level, unless a setting is explicitly moved into Site Settings.

### Databases Menu

- Remove the left-hand Databases menu item. ✅ Done (removed from `NavMenu.razor`).

### Taxonomy Menu Item

- Keep only two submenu items: Categories, Tags. ✅ Done (General removed).
- Implement APIs for categories and tags in `Aero.Cms.Modules.Taxonomy`.
- Categories and tags are site-owned and need `SiteId`.
- Category/tag uniqueness should be site-scoped, such as `(SiteId, Slug)` or `(SiteId, Name)` depending on the final business rule.

---

## Cross-Module SiteId Work

After adding `SiteId` to Pages, Posts, Docs, and other site-owned features:

- [x] Add `ISiteOwned` interface — all site-owned entity types must implement it. ✅ Done at `src/Aero.Cms.Abstractions/Interfaces/ISiteOwned.cs`.
- [x] Ensure `SeedDataService.cs` creates and retains a tenant and a default site. ✅ Done.
- [ ] Ensure seeded records use the created default site id. ✅ Done (already stamps `siteId` on all seeded content).
- [ ] Update dependent modules and models with `long SiteId` where data is owned by a site.
- [ ] Update list/search/query endpoints to filter by `SiteId`.
- [ ] Update create/update/delete endpoints to derive `SiteId` from the resolved current site context.
- [ ] Update events, cache invalidation, sitemap/search projections, and alias generation to include `SiteId`.
- [ ] Review existing global uniqueness rules and convert them to site-scoped composite uniqueness where appropriate.

---

## Test Strategy

| Layer | Tool | What to test |
|-------|------|-------------|
| **Unit** | TUnit | `SiteService` CRUD, `IUserSiteService` assignment logic, `SitePermissionHandler` authorization, `UserSiteAssignment` validation |
| **Integration** | Alba + embedded Postgres (mysticmind-postgresembed) | Full request pipeline with site middleware, scoped content queries, hostname resolution, cross-site IDOR prevention |
| **E2E** | Playwright | Manager flow: login → site selection → CRUD content within site → verify content invisible on different site |
| **Security** | TUnit | Unassigned user access denied, cross-site entity access by ID rejected, admin bypass verified, cookie tampering validation |
