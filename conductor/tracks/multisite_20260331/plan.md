# Implementation Plan: Multi-Site Support (OSS) - Track multisite_20260331

## Phase 1: Core Abstractions and Host Resolution
- [ ] Task: Implement `ISiteOwned` in `Aero.Cms.Abstractions`.
    - [ ] Write unit tests for `ISiteOwned` interface.
    - [ ] Add `long SiteId { get; set; }` to `ISiteOwned.cs`.
- [ ] Task: Implement `ICurrentSiteAccessor` and `ISiteLookupService` in `Aero.Cms.Abstractions`.
    - [ ] Write unit tests for `ICurrentSiteAccessor`.
    - [ ] Add `SiteViewModel` to include `Id`, `Name`, `PrimaryHost`, etc.
    - [ ] Implement `CurrentSiteAccessor` to hold the request-scoped site context.
- [ ] Task: Implement Host Normalization and Site Resolution Middleware in the Web Host layer.
    - [ ] Write unit tests for host normalization logic (lowercase, trim, strip port).
    - [ ] Implement `SiteResolutionMiddleware` to resolve `SiteId` by host.
    - [ ] Add error handling for unknown hosts or disabled sites.
- [ ] Task: Conductor - User Manual Verification 'Phase 1: Core Abstractions and Host Resolution' (Protocol in workflow.md)

## Phase 2: Persistence and Marten Mapping
- [ ] Task: Update Persisted Models to implement `ISiteOwned`.
    - [ ] Add `SiteId` to `Page`, `Post`, `Media`, `Category`, `Tag`, `Alias`, and `Doc`.
    - [ ] Update Marten mappings for all site-owned documents to include `Index(x => x.SiteId)`.
- [ ] Task: Implement Composite Unique Indexes in Marten.
    - [ ] Write integration tests for site-scoped uniqueness (e.g., same slug on different sites).
    - [ ] Configure composite indexes for `(SiteId, Slug)`, `(SiteId, Name)`, and `(SiteId, SourcePath)`.
- [ ] Task: Create Migration and Backfill Logic.
    - [ ] Write migration script to generate a default site using `Snowflake.NewId()`.
    - [ ] Implement backfill logic for existing single-site data to the new default site.
- [ ] Task: Conductor - User Manual Verification 'Phase 2: Persistence and Marten Mapping' (Protocol in workflow.md)

## Phase 3: Module Refactoring (Pages, Blogs, Media)
- [ ] Task: Refactor `Aero.Cms.Modules.Pages` for Site Scoping.
    - [ ] Update CRUD handlers to stamp `SiteId` from `ICurrentSiteAccessor`.
    - [ ] Filter all page queries (list, search, lookup) by `SiteId`.
    - [ ] Update `PageCreated/Updated/Deleted` events to include `SiteId`.
- [ ] Task: Refactor `Aero.Cms.Modules.Posts` for Site Scoping.
    - [ ] Update blog post handlers and services to be site-aware.
    - [ ] Ensure categories and tags are scoped within the same site as the post.
- [ ] Task: Refactor `Aero.Cms.Modules.Media` for Site-Scoped Storage.
    - [ ] Update media upload logic to use site-specific folders: `/media/{SiteId}/`.
    - [ ] Ensure media library views are filtered by the current `SiteId`.
- [ ] Task: Conductor - User Manual Verification 'Phase 3: Module Refactoring (Pages, Blogs, Media)' (Protocol in workflow.md)

## Phase 4: Secondary Modules and Admin UI
- [ ] Task: Refactor `Categories`, `Tags`, `Aliases`, and `Docs` for Site Scoping.
    - [ ] Update each module's handlers, queries, and validators for `SiteId` scoping.
    - [ ] Ensure cross-site relationships are strictly prevented.
- [ ] Task: Update Manager UI with Site Selection.
    - [ ] Implement a site switcher in the Manager dashboard.
    - [ ] Ensure all content lists in the UI respect the active site filter.
- [ ] Task: Update Setup Wizard for Initial Site Configuration.
    - [ ] Update `@setup.cshtml` in `Aero.Cms.Modules.Setup` to capture the site name.
- [ ] Task: Conductor - User Manual Verification 'Phase 4: Secondary Modules and Admin UI' (Protocol in workflow.md)

## Phase 5: Final Hardening and Verification
- [ ] Task: Implement Site Ownership Checks on Update/Delete.
    - [ ] Add validation to ensure the current site owns the record being modified.
- [ ] Task: Final End-to-End and Integration Testing.
    - [ ] Verify complete host-to-content isolation for multiple sites.
    - [ ] Ensure migration path works correctly on a legacy database.
- [ ] Task: Conductor - User Manual Verification 'Phase 5: Final Hardening and Verification' (Protocol in workflow.md)
