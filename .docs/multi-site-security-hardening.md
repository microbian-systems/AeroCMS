# Multi-Site Security Hardening

Status: Phase 1 completed; Phase 2 in progress (Pages, Posts, Docs, Aliases, Content, Navigation, Footer, and the Commerce ownership/pricing/payment foundation completed); Phases 3-6 pending
Created: 2026-07-19
Current work unit: Commerce payment foundation and Sable transaction hardening completed; Commerce production WU-1 is catalog manager contract and persistence safety

Focused Commerce production task:
[commerce-production-vertical-slice.md](commerce-production-vertical-slice.md).

## Outcome

Make AeroCMS multi-site administration safe by enforcing authentication, proving
that the selected manager site is authorized, and then incrementally enforcing
site ownership throughout content, media, host, and site-lifecycle operations.

This task deliberately separates the emergency authentication boundary from
record-level authorization. Completing Phase 1 does not mean the broader
multi-site implementation is production-ready.

## Audit verdict

The public site-resolution foundation is structurally sound: normalized hosts
resolve through unique `SiteHost` records, disabled or missing sites are
rejected, public content services generally filter by `SiteId`, aliases are
site/culture/path scoped, and output caching varies by origin and culture.

The administration implementation is not yet production-sound. The audit found:

1. Critical: audited admin APIs did not carry authorization metadata.
2. Critical: the manager site cookie was trusted without validating the user's
   site assignment.
3. Critical: identifier-based actor and API operations can load or mutate a
   record without proving it belongs to the authorized site.
4. High: media records and physical paths are effectively global.
5. High: host replacement is non-atomic and does not enforce exactly one
   canonical host.
6. High: site deletion removes only the site and host records, leaving
   assignments and site-owned content orphaned.
7. Medium: current tests primarily prove service-level filtering and do not
   cover the HTTP authorization boundary, cookie forgery, record-level IDOR,
   media isolation, or complete deletion.

## Scope and non-goals

Phase 1 is limited to:

- requiring authentication or an explicit anonymous designation on every known
  active `/api/v1/admin/*` endpoint;
- inventorying the AI, content-item, and content-type endpoint mappers as part
  of that explicit access-intent boundary;
- requiring `AeroAdmin` for the Audit, Dashboard, Settings, Themes, Modules,
  Localization Import, Users, and global AI settings endpoints because they
  expose or mutate store-wide state;
- preserving ordinary authenticated editor access to AI content enhancement,
  translation, and provider options;
- requiring authentication for the current-user Profile group;
- explicitly allowing anonymous access to authentication configuration, local
  login, and logout while requiring authentication for `/auth/me`;
- validating both requested and cookie-selected sites for non-admin users with
  `IUserSiteService.HasPermissionAsync(userId, siteId, "read")`;
- preserving the existing exact admin bypass: `Admin` role or
  `is_admin=true`;
- returning `403` without a cookie, site payload, or audit event when site
  selection is unauthorized;
- adding focused TUnit coverage for metadata and HTTP behavior.

Phase 1 does not:

- attach the existing `site:read`, `site:create`, `site:update`, or
  `site:delete` policies to content or site-management operations;
- repair identifier-based cross-site access;
- change media persistence or URL layout;
- change host schemas or transaction boundaries;
- implement site deletion cascading or retention;
- change whether disabled sites can be selected by an otherwise authorized
  caller.

## Implementation phases

### Phase 1 — Authentication boundary and safe site selection

Status: Completed

- [x] Require authentication on Sites and client-error endpoints.
- [x] Require authentication on Pages, page tree, and page preview endpoints.
- [x] Require authentication on Posts, taxonomies, series, and post preview
      endpoints.
- [x] Require authentication on Docs, Media, Files, Aliases, Navigation, and
      Footer admin endpoints.
- [x] Require authentication on Content Items and Content Types endpoints.
- [x] Require authentication on AI content enhancement, translation, and
      provider-options endpoints.
- [x] Require `AeroAdmin` on global AI settings reads and writes.
- [x] Require `AeroAdmin` on Audit, Dashboard, Settings, Themes, Modules, and
      Localization Import admin groups.
- [x] Require `AeroAdmin` on the Users admin group.
- [x] Require authentication on Profile endpoints.
- [x] Mark auth configuration, local login, and logout explicitly anonymous;
      require authentication on `/auth/me`.
- [x] Check `read` permission before resolving a selected site or writing its
      cookie.
- [x] Preserve `Admin` role and exact `is_admin=true` bypass behavior.
- [x] Add and pass focused endpoint metadata and HTTP integration tests.
- [x] Build the web host successfully.

### Phase 2 — Site policies and record-level isolation

Status: In progress — Pages, Posts, Docs, Aliases, Content, Navigation, and Footer vertical slices completed; remaining modules pending

- Define the endpoint-to-policy matrix for `site:read`, `site:create`,
  `site:update`, and `site:delete`.
- Bind authorization to an authoritative site instead of trusting a raw cookie
  or target record.
- Pass the authorized `SiteId` into identifier-based actor/service operations.
- Enforce `Id && SiteId` on reads, updates, deletes, translations, publication,
  previews, route-impact, and bulk operations.
- Add cross-site IDOR tests for Pages, Posts, Docs, Aliases, Navigation, and
  Footer.

Pages vertical slice:

- [x] Apply `site:read`, `site:create`, `site:update`, and `site:delete` to
      Pages, page-tree, admin preview, and draft-preview routes.
- [x] Pass the authorized selected `SiteId` through Pages API, actor, content,
      hierarchy, and publishing boundaries.
- [x] Make inherited identifier-only page actor methods fail closed.
- [x] Enforce site ownership for page reads, updates, deletes, publication,
      translations, route impact, bulk deletion, previews, and parent IDs.
- [x] Fail mixed-site bulk deletion atomically and return actual distinct
      deletion counts.
- [x] Conceal missing and cross-site page, parent, and translation-group IDs
      with `404`.
- [x] Protect only the draft-preview Razor selector while keeping public page
      selectors anonymous.
- [x] Add two-site service tests, real `SitePermissionHandler` HTTP tests,
      fail-closed actor tests, policy metadata tests, and draft-preview tests.
Posts vertical slice:

- [x] Apply the complete `site:read`, `site:create`, `site:update`, and
      `site:delete` matrix to all 18 Posts administration and preview endpoints.
- [x] Pass the authorized selected `SiteId` through post API, actor, content,
      publication, translation, preview, and import boundaries.
- [x] Make inherited identifier-only post actor CRUD methods fail closed.
- [x] Prevent cross-site post-ID re-homing and validate referenced series, tags,
      and categories against the selected site.
- [x] Ignore imported payload `SiteId`, require the explicit authorized site in
      the import service, and require `site:update` for overwrite imports.
- [x] Conceal missing and cross-site posts and translation groups with `404`;
      reject route/body update-ID mismatches with `400`.
- [x] Commit translation-group publication and deletion in one selected-site
      session, including staged post and slug-reservation deletion.
- [x] Protect only the post draft-preview Razor selector while keeping all
      public blog selectors anonymous.
- [x] Add complete policy metadata, real `SitePermissionHandler`, fail-closed
      actor, two-site persistence, import-scope, and preview-selector tests.
Docs vertical slice:

- [x] Apply the complete `site:read`, `site:create`, `site:update`, and
      `site:delete` matrix to all 15 Docs administration endpoints.
- [x] Split create and update into `POST` and `PUT` routes so create-only users
      cannot update existing documents.
- [x] Pass the authorized selected `SiteId` through identifier-based Docs API,
      actor, content, translation, publication, delete, and hierarchy
      boundaries.
- [x] Make inherited identifier-only Docs actor CRUD methods fail closed
      without opening storage.
- [x] Prevent cross-site document re-homing and reject foreign parent and
      translation-group relationships before mutating tracked records.
- [x] Force ordinary creates to Draft with a self translation group and no
      publication timestamp.
- [x] Conceal missing and cross-site document, translation-source, space,
      parent, section, and reorder identifiers before hierarchy mutation.
- [x] Keep public Docs selectors anonymous and enforce selected-site,
      published-state, and culture filtering, including cache-hit validation.
- [x] Validate empty and duplicate reorder requests and preserve atomic
      selected-site hierarchy changes.
- [x] Add exact policy metadata, real `SitePermissionHandler`, API guard,
      fail-closed actor, two-site persistence, and hierarchy atomicity tests.
- [x] Verify the complete Core test suite, Docs module, and web host.
Aliases vertical slice:

- [x] Apply `site:read`, `site:create`, and `site:delete` to the three Aliases
      administration endpoints.
- [x] Make the selected `ISiteContext.SiteId` authoritative for list, create,
      lookup, and delete operations.
- [x] Ignore caller-supplied query and request-body site identifiers.
- [x] Add explicit site-scoped actor operations and make inherited unscoped
      identifier and mutation methods fail closed without opening storage.
- [x] Enforce `Id && SiteId` for lookup and deletion and conceal foreign aliases
      as `404`.
- [x] Remove obsolete unscoped wrapper-service members and the HTTP client's
      caller-selected `siteId` query parameter.
- [x] Preserve public rewrite isolation by site, culture, and normalized path.
- [x] Add exact policy metadata, real `SitePermissionHandler`, payload-override,
      fail-closed actor, and two-site persistence tests.
- [x] Verify the complete Core test suite, Aliases module, and web host.
- [x] Complete independent Oracle review with no actionable tenant-isolation
      finding.
Content Types and Content Items vertical slice:

- [x] Apply the exact `site:read`, `site:create`, `site:update`, and
      `site:delete` matrix to all 14 Content administration endpoints.
- [x] Make `ISiteContext.SiteId` authoritative and pass it through Content
      Items and Content Types API, actor, service, and persistence boundaries.
- [x] Make inherited unscoped Content Item actor CRUD fail closed without
      opening storage.
- [x] Prevent foreign nonzero Content Item and Content Type identifiers from
      being re-homed; reserve `Id = 0` for clean creates.
- [x] Require same-site content types, references, sources, and translation
      groups before storing, with mixed-site reference sets failing atomically.
- [x] Scope item-ID cache keys by site and reject mismatched ID, site, slug,
      type, culture, and mixed-site list snapshots before reloading storage.
- [x] Keep the public Content selector explicitly anonymous while requiring the
      host site, `AllowPublicUrl`, matching type/culture/slug, and Published
      state.
- [x] Conceal missing, foreign, and same-site wrong-ID preflight results with
      `404`; return generic `400` for post-preflight mutation failures.
- [x] Add exact policy metadata, real `SitePermissionHandler`, fail-closed
      actor, two-site persistence, relationship, poisoned-cache, wrong-ID API,
      and public-renderer tests.
- [x] Verify the complete Core test suite, Content module, and web host.
- [x] Complete independent Oracle correction review with no actionable
      tenant-isolation or correctness finding.
Navigation and Footer vertical slice:

- [x] Apply the exact `site:read`, `site:create`, `site:update`, and
      `site:delete` matrix to the 12 canonical administration routes in each
      module.
- [x] Require both `site:create` and `site:update` for AI translation.
- [x] Make the ambient `ISiteContext.SiteId` authoritative for authoring
      operations and conceal missing or foreign identifiers as `404`.
- [x] Preflight event-history identifiers through the site-scoped service before
      fetching deterministic event streams.
- [x] Require explicit `(siteId, id)` public snapshot resolution and make culture
      selection fail closed when a base document is missing or foreign.
- [x] Render no output for invalid or foreign overrides and corrupt defaults;
      preserve Footer's no-default fallback only within the requested site.
- [x] Remove the redundant `PUT /{id}` compatibility routes and matching typed
      client `UpdateAsync` methods.
- [x] Add exact route-policy, real `SitePermissionHandler`, event-history, and
      two-site public-resolution tests.
- [x] Verify the full Core test suite, both focused modules, and the web host.
- [x] Complete independent deep Oracle review with no blocking tenant-isolation
      or correctness finding; correct the resulting stale-comment advisories.

### Commerce multi-site workstream

Status: Tenant/site ownership, authoritative pricing, and the first Stripe/PayPal
payment orchestration foundation completed; durable messaging, scheduled
reconciliation, refunds/compensation, and provider integration testing remain pending

Completed prerequisite:

- [x] Keep ASP.NET Core Identity and `.AeroCms.Auth` internal to CMS
      administrators/managers.
- [x] Add a non-default `AeroCms.ExternalMember` scheme and
      `.AeroCms.Member` cookie for storefront customers.
- [x] Add local external member, revocable session, and tenant/site-membership
      documents with strict scheme/claim validation.
- [x] Validate member/session state on every member-cookie request and fail
      closed on datastore failure.
- [x] Authorize storefront membership against the host-resolved site/tenant,
      never the manager selected-site cookie.
- [x] Add isolated member `/me` and logout endpoints; always clear the member
      cookie even if revocation persistence fails.
- [x] Prove manager/member scheme isolation, invalid session rejection,
      host-site isolation, logout behavior, and assignment uniqueness with
      executable tests.
- [x] Complete independent deep Oracle correction review with no remaining
      blocking or high-severity finding.

Completed ownership/pricing slice:

- [x] Split tenant-owned canonical products/SKUs and pooled inventory from
      site/culture-owned listings, publication, merchandising, and USD pricing.
- [x] Key baskets by tenant, site, and immutable external-member Snowflake ID.
- [x] Remove caller-selected customer identifiers and caller-supplied price,
      title, and SKU from basket and checkout inputs.
- [x] Scope anonymous catalog reads to published listings for the host-resolved
      tenant/site/culture and return narrow public DTOs.
- [x] Apply exact `site:*` policies and tenant/site ownership predicates to
      manager catalog operations.
- [ ] Move the current `/api/commerce/catalog/manager/*` routes beneath
      `/api/v1/admin/commerce/*`; the legacy prefix is not recognized as a
      manager request by `DefaultSiteContext`, so selected-site derivation is not
      reliable until this WU-1 correction lands.
- [x] Require the isolated external-member scheme plus host-site membership for
      basket, checkout, customer order, cancellation, and payment-status routes.
- [x] Explicitly authenticate the non-default member cookie on mixed public
      Razor pages; public badge reads are non-mutating.
- [x] Re-resolve listing, product, price, SKU, publication, currency, and stock
      on basket mutation and again at checkout.
- [x] Commit order creation, pooled-stock reservation, and basket clearing in
      one Sable save transaction with effective optimistic concurrency.
- [x] Limit customer cancellation to owned `Submitted` or
      `AwaitingValidation` orders and atomically release reserved stock.
- [x] Preserve tenant/site/member scope in active order events and grace-period
      processing; remove the unsafe simulated stock/payment pipeline.
- [x] Seed canonical products and listings from the requested site's tenant and
      site ownership.
- [x] Treat the durable Sable commit as success even when immediate Wolverine
      publication fails; log the delivery failure for later reconciliation.
- [x] Complete deep Oracle review and correction loops with no remaining P0-P2
      finding.

Completed payment and transaction foundation:

- [x] Add provider-neutral Strategy/Adapter/registry boundaries for Stripe and
      PayPal without leaking provider DTOs into Commerce domain contracts.
- [x] Persist Snowflake-keyed payment attempts and webhook receipts; keep
      provider references and idempotency values as opaque strings.
- [x] Bind payment initiation/status to tenant, site, external member, and order;
      reject provider/key changes on replay.
- [x] Use stable provider-operation idempotency keys, signed size-limited
      callbacks, duplicate receipt handling, amount/currency verification, and
      fail-closed manual-review transitions.
- [x] Route Sable document writes, deferred patches, queued SQL/storage work,
      save listeners, and save-pipeline event writes through the same resolved
      auto or explicit SurrealDB transaction.
- [x] Correct embedded-provider transaction GUID transport to RFC byte order
      while retaining Snowflake `long` IDs for every Commerce aggregate.
- [x] Make repo-local Sable, in-memory tests, and AppServer SurrealKV builds
      consume the checked-in fixed provider source rather than the unfixed
      public 0.10.2 embedded binaries.
- [x] Prove transaction rollback, explicit lifecycle/listener timing, competing
      versioned writers, payment replay/webhook behavior, and full AeroDB/Aero CMS
      regressions with executable tests.

Accepted first-release decisions:

1. Canonical products/SKUs are tenant-owned and listings are site-owned.
2. Inventory is tenant-pooled on the canonical product for the first release.
3. `ExternalMember.PrincipalId` (`long`) is the immutable customer identity.
4. Checkout is authenticated; guest checkout is not included.
5. Presentment is USD-only.
6. Customers may cancel only `Submitted` and `AwaitingValidation`; later states
   require manager review/refund handling.
7. Stripe and PayPal are the first payment providers. Link, Google Pay, and
   Apple Pay are provider-reported payment methods/capabilities unless a future
   direct integration owns a distinct payment lifecycle.

Remaining Commerce work:

- scheduled provider reconciliation plus refunds, voids/capture management,
  compensation automation, and manual-review UI;
- Stripe/PayPal sandbox/live integration coverage and operational secret setup;
- durable request idempotency plus transactional outbox/inbox delivery;
- endpoint/browser coverage for real external cookies, policy matrices,
  post-commit bus failure, basket-create races, DTO serialization, and
  pagination totals.

### Phase 3 — Media isolation and file safety

Status: Not started

- Stamp all media records from the authorized site context.
- Filter media listing, detail, mutation, parent, and delete operations by
  `SiteId`.
- Choose and implement a site-specific physical and public path layout.
- Prevent traversal and filename collisions with canonical containment checks
  and generated storage names.
- Add cross-site metadata and file-access tests.

### Phase 4 — Host lifecycle integrity

Status: Not started

- Enforce exactly one primary host per site.
- Normalize and deduplicate replacement input before persistence.
- Validate site existence and global host ownership.
- Make site and host creation/update atomic or return an explicit partial
  failure that cannot be reported as success.
- Add conflict, rollback, and concurrent update tests.

### Phase 5 — Site deletion and retention

Status: Not started

- Decide between soft deletion, retention/archival, and hard cascade.
- Inventory every site-owned document and assignment.
- Implement an observable, retryable deletion workflow.
- Prevent orphan access during and after deletion.
- Add complete lifecycle and recovery tests.

### Phase 6 — Production assurance

Status: Not started

- Add end-to-end multi-host tests across two sites and two non-admin users.
- Verify cache and alias isolation under authenticated and public traffic.
- Add security logging and alerts for forbidden site access.
- Document rollout, rollback, data repair, and operational checks.
- Complete an independent security review before production enablement.

## Phase 1 acceptance criteria

- Every known active `/api/v1/admin/*` endpoint has either `IAuthorizeData` or
  explicit `IAllowAnonymous` metadata; AI, Content Items, Content Types, Pages,
  and Posts preview endpoints are included.
- Audit, Dashboard, Settings, Themes, Modules, Localization Import, and Users
  specify the `AeroAdmin` policy.
- AI settings GET and POST specify `AeroAdmin`; AI content enhancement,
  translation, and provider options retain the default authenticated boundary.
- Profile specifies authentication without requiring global administration.
- `/auth/config`, `/auth/local/login`, and `/auth/logout` are explicitly
  anonymous; `/auth/me` explicitly requires authentication.
- An anonymous request to a protected admin endpoint returns `401`.
- A test-authenticated request can reach an ordinary protected endpoint.
- A non-admin user with assigned `read` permission can select a site and
  receives `200` plus `Set-Cookie`.
- A non-admin user without permission receives `403`; no cookie is written and
  no `SiteSelectionChanged` event is published.
- A forged selected-site cookie returns `403` without a site payload.
- `Admin` role and exact `is_admin=true` principals bypass assignment lookup.
- Existing disabled-site behavior is unchanged.
- Focused TUnit tests pass and the web project builds with zero errors.

## Verification log

| Date | Command | Result |
| --- | --- | --- |
| 2026-07-19 | `dotnet build-server shutdown` | Succeeded; MSBuild and compiler servers stopped |
| 2026-07-19 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-restore --treenode-filter "/*/*/SitesApiAuthorizationTests/*" --verbosity minimal` | Passed: 6 |
| 2026-07-19 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-restore --treenode-filter "/*/*/AdminEndpointAuthorizationMetadataTests/*" --verbosity minimal` | Passed: 2; complete metadata inventory and anonymous Modules HTTP 401 |
| 2026-07-19 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/PagesApiTests/*" --verbosity minimal` | Passed: 2 |
| 2026-07-19 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/CategoriesApiTests/*" --verbosity minimal` | Passed: 2 |
| 2026-07-19 | `dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj --no-restore --verbosity minimal` | Succeeded: 0 errors; 338 existing warnings remain |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-restore --treenode-filter "/*/*/AdminEndpointAuthorizationMetadataTests/*" --verbosity minimal` | Passed: 2; inventory includes AI, Content Items, and Content Types, with `AeroAdmin` limited to global AI settings |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/SitesApiAuthorizationTests/*" --verbosity minimal` | Passed: 6 |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/PagesApiTests/*" --verbosity minimal` | Passed: 2 |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/CategoriesApiTests/*" --verbosity minimal` | Passed: 2 |
| 2026-07-20 | `dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj --no-restore --verbosity minimal` | Succeeded: 0 errors; 336 existing warnings remain |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-restore -p:UseSharedCompilation=false --treenode-filter "/*/*/PagesApiTests/*" --verbosity minimal` | Passed: 9; Pages and page-tree policy matrix, scoped ID behavior, parent preflight, bulk 404, draft-selector metadata, and tree child isolation |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/PageContentServiceTests/*" --verbosity minimal` | Passed: 20; cross-site load/save/parent/group guards and atomic mixed-site bulk deletion |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/PagePublishingWorkflowHtmlTests/*" --verbosity minimal` | Passed: 4; includes same-session cross-site publication rejection |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/PagesSitePermissionAuthorizationTests/*" --verbosity minimal` | Passed: 2; real assignment handler permits assigned cookie and rejects forged cookie before actor invocation |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/AeroPageGrainScopeTests/*" --verbosity minimal` | Passed: 1; inherited identifier-only CRUD methods fail closed without opening a store session |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/DynamicPageModelStatusCodeTests/*" --verbosity minimal` | Passed: 4; draft preview uses scoped actor lookup and rejects direct cross-site documents |
| 2026-07-20 | `dotnet build src/Aero.Cms.Modules.Pages/Aero.Cms.Modules.Pages.csproj --no-restore -p:UseSharedCompilation=false --verbosity minimal` | Succeeded: 0 errors; 140 existing warnings in the focused module graph |
| 2026-07-20 | `dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj --no-restore -p:UseSharedCompilation=false --verbosity minimal` | Succeeded: 0 errors; 424 existing warnings in the full rebuilt host graph |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-restore -p:UseSharedCompilation=false --treenode-filter "/*/*/PostsAuthorizationMetadataTests/*" --verbosity minimal` | Passed: 19; all 18 Posts endpoints plus draft-only Razor selector metadata |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/PostsSitePermissionAuthorizationTests/*" --verbosity minimal` | Passed: 3; assigned/forged selected-site authorization and route/body ID mismatch before actor access |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/AeroPostGrainScopeTests/*" --verbosity minimal` | Passed: 1; inherited identifier-only post CRUD fails closed |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/BlogPostContentServiceTests/*" --verbosity minimal` | Passed: 9; post re-homing and relationship guards, scoped publication, and atomic own-site group deletion preserving foreign rows |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --treenode-filter "/*/*/PostImportServiceScopeTests/*" --verbosity minimal` | Passed: 1; payload site ignored in favor of explicit authorized site |
| 2026-07-20 | `dotnet build src/Aero.Cms.Modules.Posts/Aero.Cms.Modules.Posts.csproj --no-restore -p:UseSharedCompilation=false --verbosity minimal` | Succeeded: 0 errors; 43 existing warnings in the focused module graph |
| 2026-07-20 | Full `Aero.Cms.Core.Tests` Oracle run | Passed: 431 |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --maximum-parallel-tests 1` | Passed: 450; includes Docs policy, API guard, permission-handler, actor-scope, service-isolation, and hierarchy tests |
| 2026-07-20 | `dotnet build src/Aero.Cms.Modules.Docs/Aero.Cms.Modules.Docs.csproj --no-restore` | Succeeded: 0 errors; 30 existing warnings in the focused module graph |
| 2026-07-20 | `dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj --no-restore` | Succeeded: 0 errors; 446 existing warnings in the full host graph |
| 2026-07-20 | Docs deep Oracle review | Accepted with advisories; no reachable cross-site Docs escape found |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --maximum-parallel-tests 1` | Passed: 461; includes Aliases policy, selected-site override, permission-handler, actor fail-closed, and two-site persistence tests |
| 2026-07-20 | `dotnet build src/Aero.Cms.Modules.Aliases/Aero.Cms.Modules.Aliases.csproj --no-restore -p:UseSharedCompilation=false --verbosity minimal` | Succeeded: 0 errors; 33 existing dependency warnings |
| 2026-07-20 | `dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj --no-restore -p:UseSharedCompilation=false --verbosity minimal` | Succeeded: 0 errors; 336 existing warnings in the full host graph |
| 2026-07-20 | Aliases deep Oracle review and correction recheck | Accepted with one residual post-commit event-publication advisory; no actionable tenant-isolation finding |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --no-ansi --maximum-parallel-tests 1` | Passed: 495; includes Content policy, permission-handler, actor, persistence, reference, poisoned-cache, wrong-ID, mutation-status, and public-renderer tests |
| 2026-07-20 | `dotnet build src/Aero.Cms.Modules.Content/Aero.Cms.Modules.Content.csproj --no-restore` | Succeeded: 0 errors; 40 existing dependency warnings |
| 2026-07-20 | `dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj --no-restore` | Succeeded: 0 errors; 337 existing warnings in the final writer run and 338 in the independent pre-correction run |
| 2026-07-20 | Content deep Oracle review, correction pass, and recheck | Accepted; no actionable tenant-isolation or correctness finding; residual post-commit notification-durability advisory only |
| 2026-07-20 | Focused Navigation/Footer authorization and site-isolation tests | Passed: 29; exact route policies, real permission handler, event-history preflight, and public resolution |
| 2026-07-20 | Existing Navigation test suite | Passed: 10 |
| 2026-07-20 | Existing Footer test suite | Passed: 10 |
| 2026-07-20 | `dotnet test --project tests/Aero.Cms.Core.Tests/Aero.Cms.Core.Tests.csproj --no-build --no-ansi --maximum-parallel-tests 1 --verbosity minimal` | Passed: 524; 0 failed; 0 skipped |
| 2026-07-20 | `dotnet build src/Aero.Cms.Modules.Navigation/Aero.Cms.Modules.Navigation.csproj --no-restore -p:UseSharedCompilation=false --verbosity minimal` | Succeeded: 0 errors |
| 2026-07-20 | `dotnet build src/Aero.Cms.Modules.Footer/Aero.Cms.Modules.Footer.csproj --no-restore -p:UseSharedCompilation=false --verbosity minimal` | Succeeded: 0 errors |
| 2026-07-20 | `dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj --no-restore -p:UseSharedCompilation=false --verbosity minimal` | Succeeded: 0 errors; 424 existing warnings in the rebuilt host graph |
| 2026-07-20 | Navigation/Footer deep Oracle review and advisory correction | Accepted with no blocking tenant-isolation or correctness finding; stale security comments corrected |
| 2026-07-20 | `dotnet --version` at repository and `src` roots | `10.0.301` at both roots |
| 2026-07-20 | External-member focused Microsoft Testing Platform run with `--treenode-filter "/*/*/*ExternalMember*/*"` | Passed: 31; strict claims/scheme provenance, validator failures, host-site authorization, scheme isolation, logout success/failure, internal-cookie preservation, and unique assignment constraint |
| 2026-07-20 | External-member correction full `Aero.Cms.Core.Tests` run, serialized | Passed: 555; 0 failed; 0 skipped |
| 2026-07-20 | `dotnet build src/Aero.Cms.Modules.Identity/Aero.Cms.Modules.Identity.csproj --no-restore -p:UseSharedCompilation=false --disable-build-servers --verbosity minimal` | Succeeded: 0 errors; 30 existing dependency warnings |
| 2026-07-20 | `dotnet build src/Aero.Cms.Web/Aero.Cms.Web.csproj --no-restore -p:UseSharedCompilation=false --disable-build-servers --verbosity minimal` | Succeeded: 0 errors; 338 existing dependency/generated-code warnings |
| 2026-07-20 | External-member deep Oracle review, correction pass, and recheck | Accepted; no remaining blocking or high-severity finding |
| 2026-07-20 | Focused Commerce ownership/pricing TUnit run with `--treenode-filter "/*/*/CommerceCheckoutOwnershipTests/*"` | Passed: 5; authoritative checkout/update pricing, scoped cancellation, validation, and non-mutating public basket lookup |
| 2026-07-20 | Full serialized `Aero.Cms.Core.Tests` run after Commerce corrections | Passed: 558; 0 failed; 0 skipped |
| 2026-07-20 | `dotnet build src/Aero.Cms.Modules.Commerce/Aero.Cms.Modules.Commerce.csproj --no-restore --disable-build-servers` | Succeeded: 0 errors; existing dependency warnings only |
| 2026-07-20 | Sequential `dotnet build src/Aero.Cms.Modules.Commerce.Client/Aero.Cms.Modules.Commerce.Client.csproj --no-restore --disable-build-servers` | Succeeded: 0 errors; existing dependency warnings only |
| 2026-07-20 | Commerce deep Oracle review and two correction rechecks | Accepted; no remaining P0-P2 ownership/pricing or storefront-authentication finding |

## Residual risks after the completed Phase 2 slices

- Authentication alone does not prevent an authenticated user from exploiting
  identifier-based cross-site operations outside the completed Pages, Posts,
  Docs, Aliases, Content, Navigation, and Footer slices.
- Commerce ownership, public catalog, basket, checkout, customer orders,
  cancellation, active events, jobs, and seed data are now tenant/site/member
  scoped, and the Stripe/PayPal provider-neutral payment orchestration foundation
  is complete. The manager catalog route still needs to move beneath
  `/api/v1/admin` so `DefaultSiteContext` resolves selected-site scope reliably.
  Production external-member login, provider sandbox continuation,
  durable request idempotency, transactional outbox/inbox delivery, scheduled
  reconciliation, refund/void/capture workflows, and direct concurrent-conflict
  coverage remain pending.
- The external-member foundation does not yet issue production member cookies:
  provider identity links, tenant/organization bindings, invitations, Entra and
  WorkOS callbacks, upstream logout, webhooks, and reconciliation remain
  deferred. Commerce must consume only the local member principal/session
  boundary when those adapters are added.
- Site CRUD remains authenticated but is not yet mapped to `site:*` policies.
- The Users group is administrator-only, but tenant scoping and assignment
  validation remain open.
- AI settings are administrator-only, but authenticated editor AI operations
  still require Phase 2 site-policy and tenant-bound authorization.
- Media remains globally queried and stored until Phase 3.
- Host replacement and site deletion remain non-atomic/incomplete.
- The selected-site cookie is still client-controlled state; Phase 1 validates
  it on the current-site endpoints. Pages, Posts, Docs, Aliases, Content,
  Navigation, and Footer now validate it through `site:*` policies on their
  administrative operations. The remaining Phase 2 modules still need the same
  treatment.
- Navigation `PageId` remains an opaque, currently non-dereferenced value. Any
  future page lookup or link generation from it must validate page ownership
  against the resolved navigation site before use.
- Post mutation events and audit records are emitted after persistence commits;
  a bus or audit failure can therefore be reported after a durable mutation.
- The Posts permission-handler test substitutes `ISiteContext`; production
  cookie-to-`DefaultSiteContext` integration remains covered indirectly.
- The low-level `DocsContentService.SaveAsync(DocsPage)` remains a trusted
  internal primitive that can accept caller-selected publication and
  translation values. Current remote writes use the hardened view-model path,
  but a future refactor should split or restrict this primitive.
- Foreign or missing Docs parent relationships are safely rejected but ordinary
  create/update and malformed-parent fork responses currently use `400` rather
  than the planned `404` taxonomy.
- Single Docs deletion can orphan descendants despite current manager wording;
  manager multi-delete is non-atomic. Cascade, rejection, or explicit orphaning
  requires a separate content-lifecycle decision.
- Docs mutation events and cache invalidation occur after persistence, so event
  delivery failure can be reported after a durable mutation.
- Any selected-site Docs page can currently act as a space root; explicit
  persisted space semantics remain a product-model decision.
- Markdown fragment preview remains an existing HTML-sanitization trust-boundary
  risk, separate from multi-site isolation.
- Alias create/delete persistence commits before Wolverine event publication;
  an event failure can therefore surface after the durable mutation.
- Content notifications are best-effort after persistence; an outbox is needed
  if downstream notification delivery becomes correctness-critical.

## Open decisions

1. Should `AeroAdmin` remain globally privileged across every tenant, or should
   tenant-level administration be introduced?
2. Should disabled sites remain selectable by assigned users, or only by global
   administrators?
3. What canonical media URL and storage layout should be used:
   `/media/{siteId}/...`, tenant/site segments, or opaque storage keys?
4. Should site removal use soft delete plus retention, asynchronous hard
   deletion, or an explicit export/archive workflow?
5. Which documents are authoritatively site-owned for deletion and generated
   ownership discovery?
6. Which settlement currencies and merchant-account ownership model are needed
   after the USD-only first release?
7. What manager review, refund, void, and manual-compensation workflow applies
   after an order leaves `AwaitingValidation`?
8. When guest checkout is introduced, what protected guest-token ownership and
   account-merge rules should replace the authenticated-only boundary?
