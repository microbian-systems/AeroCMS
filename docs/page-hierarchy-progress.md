# Page Hierarchy Implementation — Progress Tracker

**Spec:** `docs/page-hierarchy-implementation.md`  
**Version:** 2.1  
**Last Updated:** 2026-05-11

---

## Phase 1: Core Infrastructure (Sprint 1) — 5 days

| ID | Task | Status | Notes |
|----|------|--------|-------|
| 1.1 | Add hierarchy fields to `PageDocument` (`ParentId`, `Path`, `Depth`, `Order`, `IsHidden`) | ✅ Complete | `src/Aero.Cms.Core.Entities/PageDocument.cs` |
| 1.2 | Remove `ParentSlug` from `PageDocument` | ✅ Complete | Not present in existing code; no removal needed |
| 1.3 | Add `ISoftDeleted` interface to `PageDocument` | ✅ Complete | Uses `Marten.Metadata.ISoftDeleted` |
| 1.4 | Add `IAuditableEntity` marker to `PageDocument` | ✅ Complete | `src/Aero.Cms.Abstractions/Interfaces/IAuditableEntity.cs` |
| 1.5 | Update `ContentPublicationState` enum (append new values) | ✅ Complete | Added `Archived=2, InReview=3, Scheduled=4` |
| 1.6 | Configure Marten indexes on `PageDocument` in `PagesModule.Configure()` | ✅ Complete | Computed: Path, ParentId; NgramIndex: Path; SoftDeleted; DuplicateField: PublishedOn |
| 1.7 | Replace old `UniqueIndex(SiteId, Slug)` with `UniqueIndex(SiteId, ParentId, Slug)` | ✅ Complete | Marten defaults to computed index type |
| 1.8 | Expand FluentValidation `PageDocumentValidator` | ✅ Complete | Slug pattern, Path, Depth, Order, ParentId, PublicationState rules |
| 1.9 | Implement `IPageTreeService` + `PageTreeService` | ✅ Complete | `PageTreeService.cs` (310 lines): GetTree, GetChildren, GetAncestors, Move, ComputePath, GetNextSiblingOrder, UpdateDescendantPaths |
| 1.10 | Implement `INavigationService` + `NavigationService` | ✅ Complete | `NavigationService.cs` (295 lines): GetNavigationTree, GetBreadcrumb, SetHidden, MarkHiddenDescendants |
| 1.11 | Optimize breadcrumb query (single query, not N+1) | ✅ Complete | Uses materialized path with `Contains` query |
| 1.12 | Register services in `PagesModule.ConfigureServices()` | ✅ Complete | IPageTreeService, INavigationService, IValidator<PageDocument>, IHttpContextAccessor |
| 1.13 | Write unit tests (TUnit) for `PageTreeService` | ✅ Complete | `tests/Aero.Cms.Core.Tests/Services/PageTreeServiceTests.cs` — 529 lines, 18 tests (GetTree, GetChildren, GetAncestors, ComputePath, GetNextSiblingOrder, Move, UpdateDescendantPaths) |
| 1.14 | Create migration script for existing pages | ✅ Complete | `PageHierarchyMigration.cs` — tree-aware migration with orphan handling |
| 1.15 | Create `IAuditableEntity` marker interface in `Aero.Cms.Abstractions` | ✅ Complete | `src/Aero.Cms.Abstractions/Interfaces/IAuditableEntity.cs` |

**Phase 1 Progress: 15/15 complete ✅**

---

## Phase 2: Blazor UI (Sprint 2) — 5 days

| ID | Task | Status | Notes |
|----|------|--------|-------|
| 2.0 | Create tree API endpoints in Headless module | ✅ Complete | `PagesTreeApi.cs` — 8 endpoints |
| 2.1 | Create `PageTreeSelect` component | ✅ Complete | `PageTreeSelect.razor` + `.razor.cs` in Shared — hierarchical RadzenDropDown with circular-ref exclusion |
| 2.2 | Create `PathPreview` component | ✅ Complete | `PathPreview.razor` + `.razor.cs` in Shared — live path preview with validity badge |
| 2.3 | Update `PageEditor` to support parent selection | ✅ Complete | `ParentId` added to CreatePageRequest/UpdatePageRequest; PageEditor passes it |
| 2.4 | Create page tree manager using **Radzen DataGrid** | ✅ Complete | `PageTreeGrid.razor` in Pages module — flat list with depth indentation, hide toggle, delete confirm |
| 2.5 | Add breadcrumb component | ✅ Complete | `BreadcrumbNav.razor` + `.razor.cs` in Shared — full breadcrumb nav with links |
| 2.6 | Write Playwright UI integration tests | ⬜ Pending | |

**Phase 2 Progress: 6/7 complete (1 remaining: Playwright tests)**

---

## Phase 3: Event Sourcing (Sprint 3) — 5 days

| ID | Task | Status | Notes |
|----|------|--------|-------|
| 3.1 | Create event record types in `Aero.Cms.Abstractions/Events/` | ✅ Complete | 8 events: PageCreated, PageContentUpdated, PagePublished, PageArchived, PageDeleted, PageRestored, PageMoved, PageVisibilityChanged |
| 3.2 | Add `Create()` / `Apply()` methods to `PageDocument` | ✅ Complete | Self-aggregating snapshot pattern |
| 3.3 | Configure Marten event store in `PagesModule.Configure()` | ⚠️ Partial | `StreamIdentity.AsString` in global config. **Snapshot projection NOT registered** — events + `session.Store()` dual-write pattern in use. See 3.12. |
| 3.4 | Append events in `PageContentService` (Create/Update/Delete) | ✅ Complete | CreateAsync → StartStream + Store; UpdateAsync → Append + Store; DeleteAsync → Append + Delete |
| 3.5 | Append events in `PageTreeService.MoveAsync()` | ✅ Complete | AppendOne(PageMoved); descendants updated directly (pragmatic hybrid) |
| 3.6 | Append events in `NavigationService.SetHiddenAsync()` | ✅ Complete | AppendOne(PageVisibilityChanged); cascade via direct update |
| 3.7 | Bootstrap event streams for existing pages | ✅ Complete | `EventStreamBootstrapMigration.cs` |
| 3.8 | Implement `IPagePublishingWorkflowService` | ✅ Complete | `PagePublishingWorkflowService.cs` — uses `PageStateChanged` event |
| 3.9 | Create TickerQ event archiving job | ✅ Complete | `PageEventArchiveJob.cs` — `[TickerFunction("pages.archive-events")]` |
| 3.10 | Global Marten event store config (StreamIdentity.AsString) | ✅ Complete | `AeroAppServerExtensions.cs` |
| 3.11 | Create UI: version history panel | ✅ Complete | `PageVersionHistory.razor` + `.razor.cs` in Shared — modal timeline with event icons. API endpoint `GET /admin/pages/{id}/events` in `PagesApi.cs`. HTTP client method `GetEventHistoryAsync` on `IPagesHttpClient`. Integrated into `PageEditor` metadata tab via "Version History" button. |
| 3.12 | Register `Snapshot<PageDocument>(SnapshotLifecycle.Inline)` + remove `session.Store()` | ⬜ Pending | Full snapshot event sourcing. Remove dual-write; let projection persist documents. Modules own their projection: PagesModule, BlogModule, etc. Soft-delete + descendant paths keep hybrid approach. |

**Phase 3 Progress: 11/12 complete (1 remaining: 3.12 snapshot projection)**

### Removed from Spec (replaced by event sourcing)

- ❌ `IPageVersioningService` + `PageVersioningService`
- ❌ `PageVersion` entity + Marten mapping
- ❌ `PageAuditEntry` + `PageAuditListener : DocumentSessionListenerBase`
- ❌ `IsContentChanged()` predicate
- ❌ `PageVersionCleanupJob` (old TickerQ job)

---

## Phase 4: Audit & Observability (Sprint 4) — 3 days

> **Revised 2026-05-11:** The `mt_events` table IS the audit log. No separate `PageAuditEntry` document or `IDocumentSessionListener` needed. Every create/update/delete/publish/move already appends an event with full metadata (timestamp, version, causation ID). See ADR #18.

| ID | Task | Status | Notes |
|----|------|--------|-------|
| 4.1 | Create global audit API endpoint (`GET /admin/audit`) | ⬜ Pending | `QueryAllRawEvents()` across all streams with type/date/stream filters. Returns unified activity feed from `mt_events`. |
| 4.2 | Create manager audit dashboard (Blazor) | ⬜ Pending | Global activity feed component with filters (entity type: Page/BlogPost, date range, event type). Manager sidebar menu entry. |
| 4.3 | Per-doc version history (pages) | ✅ Complete | Built in 3.11 — `PageVersionHistory` component queries `session.Events.FetchStreamAsync($"page-{id}")` |
| 4.4 | Per-doc version history (blog posts) | ⬜ Pending | Same pattern as 3.11 — event types `BlogPostCreated`, `BlogPostContentUpdated`, etc. Will be done during blog event sourcing extraction. |
| 4.5 | Event archiving cleanup (TickerQ) | ✅ Complete | `PageEventArchiveJob.cs` already handles pruning old events. Applies to all streams. |

### Removed from Audit Spec (replaced by event-store-native approach)

- ❌ `PageAuditEntry` document — `mt_events` IS the audit log; no separate document needed
- ❌ `PageAuditListener : DocumentSessionListenerBase` — events are already written on every state change; no listener needed
- ❌ Separate `Aero.Cms.Modules.Audit` module scaffold — audit is a cross-cutting query, not a separate persistence layer
- ❌ TickerQ cleanup for `PageAuditEntry` — event archiving already handled by `PageEventArchiveJob`

---

## Phase 5: Polish & Performance (Sprint 5) — 3 days

| ID | Task | Status | Notes |
|----|------|--------|-------|
| 5.1 | Add output caching for navigation queries | ⬜ Pending | |
| 5.2 | Optimize descendant update queries | ⬜ Pending | |
| 5.3 | Create `ToMinimalApiResult()` extension in `Aero.Core` | ⬜ Pending | Maps `Result<T, AeroError>` → `IResult` |
| 5.4 | Integration testing with Alba + embedded Postgres | ⬜ Pending | |
| 5.5 | Performance testing with 10k+ pages | ⬜ Pending | |
| 5.6 | Documentation and training materials | ⬜ Pending | |

---

## Architectural Decisions Log

| # | Decision | Rationale | Date |
|---|----------|-----------|------|
| 1 | Adjacency list + materialized path | Industry standard (Umbraco, Contentful, Sanity) | 2026-05-10 |
| 2 | `long` IDs (Snowflake) | Project standard | 2026-05-10 |
| 3 | `Result<T, AeroError>` return types | Railway Oriented Programming (project standard) | 2026-05-10 |
| 4 | Marten `ISoftDeleted` for page deletion | Native Marten support, auto-filters queries | 2026-05-10 |
| 5 | Computed indexes over DuplicateField | Recommended by Marten docs, no extra columns | 2026-05-10 |
| 6 | `NgramIndex` on `Path` for prefix matching | Better performance than btree for `StartsWith` queries | 2026-05-10 |
| 7 | FluentValidation over standalone `SlugValidator` | Project standard | 2026-05-10 |
| 8 | TUnit for testing | Project standard (not Xunit) | 2026-05-10 |
| 9 | Wolverine outbox for `PageSlugChanged` | Transactional consistency with Marten | 2026-05-10 |
| 10 | Cascade hidden parent navigation | Industry standard (Umbraco, Orchard) | 2026-05-10 |
| 11 | Unlimited page versions + TickerQ cleanup | Flexibility, automated maintenance | 2026-05-10 |
| 12 | `IAuditableEntity` marker + module-specific listeners | Loose coupling, no cross-module dependency | 2026-05-10 |
| 13 | Radzen DataGrid self-ref hierarchy for tree UI | Already a project dependency, avoids new package | 2026-05-10 |
| 14 | Skip separate `PageTree` document for v1 | Complexity not justified for expected scale | 2026-05-10 |
| 15 | Inline Marten config in `PagesModule` for v1 | Simpler than `MartenRegistry` subclass; can refactor later | 2026-05-10 |
| 16 | Snapshot projections per-module, not global | Each module owns its document projection (e.g., `PagesModule` registers `Snapshot<PageDocument>(Inline)`, `BlogModule` registers `Snapshot<BlogPostDocument>(Inline)`) | 2026-05-11 |
| 17 | Pragmatic hybrid for descendant paths + soft-delete | Descendant path updates and soft-delete metadata kept as direct writes; the event stream is authoritative for content state changes | 2026-05-11 |
| 18 | Event-store-native audit (no separate audit module) | `mt_events` IS the audit log. Every state change is an immutable event with metadata. Per-doc history via `FetchStreamAsync()`, global audit via `QueryAllRawEvents()`. No `PageAuditEntry`, no `IDocumentSessionListener`, no separate audit storage. | 2026-05-11 |

---

## Status Key

- ⬜ Pending
- 🔄 In Progress
- ✅ Complete
- ❌ Blocked
- ⏭️ Skipped
