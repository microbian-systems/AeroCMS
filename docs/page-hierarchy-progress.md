# Page Hierarchy Implementation — Progress Tracker

**Spec:** `docs/page-hierarchy-implementation.md`  
**Version:** 2.0  
**Last Updated:** 2026-05-10

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
| 1.13 | Write unit tests (TUnit) for `PageTreeService` | ⬜ Pending | |
| 1.14 | Create migration script for existing pages | ✅ Complete | `PageHierarchyMigration.cs` — tree-aware migration with orphan handling |
| 1.15 | Create `IAuditableEntity` marker interface in `Aero.Cms.Abstractions` | ✅ Complete | `src/Aero.Cms.Abstractions/Interfaces/IAuditableEntity.cs` |

**Phase 1 Progress: 12/13 complete (1 remaining: tests)**

---

## Phase 2: Blazor UI (Sprint 2) — 5 days

| ID | Task | Status | Notes |
|----|------|--------|-------|
| 2.0 | Create tree API endpoints in Headless module | ✅ Complete | `PagesTreeApi.cs` — 8 endpoints |
| 2.1 | Create `PageTreeSelect` component | ⬜ Pending | Needs PageTree HTTP client integration in Shared |
| 2.2 | Create `PathPreview` component | ⬜ Pending | Needs PageTree HTTP client integration in Shared |
| 2.3 | Update `PageEditor` to support parent selection | ✅ Complete | `ParentId` added to CreatePageRequest/UpdatePageRequest; PageEditor passes it |
| 2.4 | Create page tree manager using **Radzen DataGrid** | ✅ Complete | `PageTreeGrid.razor` in Pages module — flat list with depth indentation, hide toggle, delete confirm |
| 2.5 | Add breadcrumb component | ⬜ Pending | Needs Nav HTTP client in Shared |
| 2.6 | Write Playwright UI integration tests | ⬜ Pending | |

**Phase 2 Progress: 3/7 complete** (+ tree API + client DTOs)

---

## Phase 3: Event Sourcing (Sprint 3) — 5 days

| ID | Task | Status | Notes |
|----|------|--------|-------|
| 3.1 | Create event record types in `Aero.Cms.Abstractions/Events/` | ✅ Complete | 8 events: PageCreated, PageContentUpdated, PagePublished, PageArchived, PageDeleted, PageRestored, PageMoved, PageVisibilityChanged |
| 3.2 | Add `Create()` / `Apply()` methods to `PageDocument` | ✅ Complete | Self-aggregating snapshot pattern |
| 3.3 | Configure Marten event store in `PagesModule.Configure()` | ✅ Complete | `StreamIdentity.AsString`, `Snapshot<PageDocument>(Inline)` |
| 3.4 | Rewrite `PageContentService` for event sourcing | ✅ Complete | CreateAsync → StartStream; UpdateAsync → FetchForWriting+AppendOne; DeleteAsync → AppendOne(PageDeleted) |
| 3.5 | Rewrite `PageTreeService.MoveAsync()` for event sourcing | ✅ Complete | FetchForWriting + AppendOne(PageMoved); descendants updated directly |
| 3.6 | Rewrite `NavigationService.SetHiddenAsync()` for event sourcing | ✅ Complete | FetchForWriting + AppendOne(PageVisibilityChanged); cascade via direct update |
| 3.7 | Bootstrap event streams for existing pages | ✅ Complete | `EventStreamBootstrapMigration.cs` |
| 3.8 | Implement `IPagePublishingWorkflowService` | ✅ Complete | `PagePublishingWorkflowService.cs` — uses `PageStateChanged` event |
| 3.9 | Create TickerQ event archiving job | ✅ Complete | `PageEventArchiveJob.cs` — `[TickerFunction("pages.archive-events")]` |
| 3.10 | Global Marten event store config (StreamIdentity.AsString) | ✅ Complete | `AeroAppServerExtensions.cs` |
| 3.11 | Create UI: version history panel | ⬜ Pending | Query `mt_events` via FetchStreamAsync |

**Phase 3 Progress: 8/11 complete**

### Removed from Spec (replaced by event sourcing)

- ❌ `IPageVersioningService` + `PageVersioningService`
- ❌ `PageVersion` entity + Marten mapping
- ❌ `PageAuditEntry` + `PageAuditListener : DocumentSessionListenerBase`
- ❌ `IsContentChanged()` predicate
- ❌ `PageVersionCleanupJob` (old TickerQ job)

---

## Phase 4: Audit & Observability (Sprint 4) — 3 days

| ID | Task | Status | Notes |
|----|------|--------|-------|
| 4.1 | Create `Aero.Cms.Modules.Audit` module scaffold | ⬜ Pending | `[Module("Audit")]` |
| 4.2 | Create `PageAuditEntry` document (Marten) | ⬜ Pending | PageId, Action, ChangedBy, Details |
| 4.3 | Implement `PageAuditListener : DocumentSessionListenerBase` | ⬜ Pending | Uses `session.PendingChanges.InsertsFor/UpdatesFor/DeletionsFor<PageDocument>()` |
| 4.4 | Register audit listener in `PagesModule.ConfigureServices()` | ⬜ Pending | |
| 4.5 | Create TickerQ job for audit log cleanup (configurable retention) | ⬜ Pending | |

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

---

## Status Key

- ⬜ Pending
- 🔄 In Progress
- ✅ Complete
- ❌ Blocked
- ⏭️ Skipped
