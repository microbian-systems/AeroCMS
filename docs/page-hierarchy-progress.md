# Page Hierarchy Implementation — Progress Tracker

**Spec:** `docs/page-hierarchy-implementation.md`  
**Version:** 2.0  
**Last Updated:** 2026-05-10

---

## Phase 1: Core Infrastructure (Sprint 1) — 5 days

| ID | Task | Status | Notes |
|----|------|--------|-------|
| 1.1 | Add hierarchy fields to `PageDocument` (`ParentId`, `Path`, `Depth`, `Order`) | ⬜ Pending | |
| 1.2 | Remove `ParentSlug` from `PageDocument` | ⬜ Pending | Redundant; use `ParentId` + `Path` |
| 1.3 | Add `ISoftDeleted` interface to `PageDocument` | ⬜ Pending | Marten native soft-delete |
| 1.4 | Add `IAuditableEntity` marker to `PageDocument` | ⬜ Pending | From `Aero.Cms.Abstractions.Interfaces` |
| 1.5 | Update `ContentPublicationState` enum (append new values) | ⬜ Pending | `Published=1, Archived=2, InReview=3, Scheduled=4` |
| 1.6 | Configure Marten indexes on `PageDocument` in `PagesModule.Configure()` | ⬜ Pending | Computed indexes + NgramIndex on Path |
| 1.7 | Replace old `UniqueIndex(SiteId, Slug)` with `UniqueIndexType.Computed(SiteId, ParentId, Slug)` | ⬜ Pending | |
| 1.8 | Implement `SlugValidator` → FluentValidation `PageDocumentValidator` expansion | ⬜ Pending | |
| 1.9 | Implement `IPageTreeService` + `PageTreeService` | ⬜ Pending | `Result<T, AeroError>`, `ISiteContext` |
| 1.10 | Implement `INavigationService` + `NavigationService` | ⬜ Pending | Cascade hidden parent visibility |
| 1.11 | Optimize breadcrumb query (single query, not N+1) | ⬜ Pending | |
| 1.12 | Register services in `PagesModule.ConfigureServices()` | ⬜ Pending | |
| 1.13 | Write unit tests (TUnit) for `PageTreeService` | ⬜ Pending | |
| 1.14 | Create migration script for existing pages | ⬜ Pending | Set `Path=/slug`, `Depth=0`, `Order=0` |
| 1.15 | Create `IAuditableEntity` marker interface in `Aero.Cms.Abstractions` | ⬜ Pending | |

---

## Phase 2: Blazor UI (Sprint 2) — 5 days

| ID | Task | Status | Notes |
|----|------|--------|-------|
| 2.1 | Create `PageTreeSelect` component | ⬜ Pending | Hierarchical dropdown |
| 2.2 | Create `PathPreview` component | ⬜ Pending | |
| 2.3 | Update `PageEditor` to support parent selection | ⬜ Pending | |
| 2.4 | Create page tree manager using **Radzen DataGrid self-ref hierarchy** | ⬜ Pending | `LoadChildData` callback for lazy loading |
| 2.5 | Add breadcrumb component | ⬜ Pending | |
| 2.6 | Write UI integration tests | ⬜ Pending | Microsoft Playwright |

---

## Phase 3: Advanced Features (Sprint 3) — 5 days

| ID | Task | Status | Notes |
|----|------|--------|-------|
| 3.1 | Implement `IPageVersioningService` + `PageVersioningService` | ⬜ Pending | Unlimited versions |
| 3.2 | Create `PageVersion` Marten document + mapping | ⬜ Pending | |
| 3.3 | Create TickerQ job for version cleanup (daily, 90-day retention) | ⬜ Pending | Configurable via site settings |
| 3.4 | Implement `IPagePublishingWorkflowService` + `PagePublishingWorkflowService` | ⬜ Pending | |
| 3.5 | Add page cloning feature (CloneAsync) | ⬜ Pending | `Snowflake.NewId()` for new pages |
| 3.6 | Add `PageSlugChanged` Wolverine event + handlers | ⬜ Pending | Alias module + Sitemap module |
| 3.7 | Create UI: version history panel | ⬜ Pending | |
| 3.8 | Create UI: publishing workflow (submit, approve, reject, schedule) | ⬜ Pending | |

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
