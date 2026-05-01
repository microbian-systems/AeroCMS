# Implementation Plan: Multi-Site Support (OSS) - Track multisite_20260331

## Phase 1: Core Abstractions and Host Resolution
- [x] Task: Implement `ISiteOwned` in `Aero.Cms.Abstractions`. [checkpoint: e0b4841]
    - [x] Write unit tests for `ISiteOwned` interface.
    - [x] Add `long SiteId { get; set; }` to `ISiteOwned.cs`.
- [x] Task: Enhance `SiteDocument` and `ISiteLookupService`. [checkpoint: f2a1b92]
    - [x] Add `Hostnames` (list of strings) to `SiteDocument`.
    - [x] Implement `ISiteLookupService` to resolve `SiteDocument` by hostname.
- [x] Task: Implement `ICurrentSiteAccessor` and Middleware. [checkpoint: 3d9a241]
    - [x] Create `ICurrentSiteAccessor` to hold the resolved `SiteId` for the request.
    - [x] Implement `SiteResolutionMiddleware` to detect the site via hostname.

## Phase 2: Data Layer Site-Scoping
- [x] Task: Update Persisted Models to implement `ISiteOwned`. [checkpoint: 87948]
    - [x] Update `PageDocument`, `BlogPostDocument`, `ContentSlugDocument`, `MediaDocument`.
- [x] Task: Implement Marten Tenancy or Global Filters. [checkpoint: 9a2b1]
    - [x] Apply a global filter in `MartenRegistry` or individual services to ensure all queries include `.Where(x => x.SiteId == currentSite.SiteId)`.
- [x] Task: Update Repositories and Services. [checkpoint: c0d31]
    - [x] Ensure `Save` operations automatically populate `SiteId` from `ICurrentSiteAccessor`.

## Phase 3: Manager UI Updates
- [x] Task: Create `SiteSwitcher.razor` component. [checkpoint: 12qcoa]
    - [x] Allow admins to switch between sites in the Manager.
- [x] Task: Update Layouts to include the Site Switcher. [checkpoint: cmw7se]
    - [x] Integrate the site switcher into the `ManagerHeader.razor`.
- [x] Task: Verify Manager UI site scoping.
    - [x] Pages, Posts, Media lists correctly filter by the active site.

## Phase 4: Shared and Secondary Modules
- [x] Task: Update `Aliases` module for site scoping.
    - [x] Ensure alias lookup and management is site-aware.
- [x] Task: Update `Categories`, `Tags`, and `Docs` for site scoping.
    - [x] Blog categories and tags should be site-isolated.
- [x] Task: Update Setup Wizard to capture initial Site Name.
    - [x] Capture `SiteName` during setup and create the first `SiteDocument`.

## Phase 5: Final Hardening and Verification
- [x] Task: Implement Site Ownership Checks on Update/Delete.
    - [x] Ensure a user on Site A cannot modify content belonging to Site B.
- [x] Task: End-to-End Verification of Site Isolation.
    - [x] Verify that two sites can have different homepages on the same installation.
    - [x] Ensure cross-site content leakage is impossible.
    - [x] Verify migration path works correctly on a legacy database.
- [x] Task: Conductor - User Manual Verification 'Phase 5: Final Hardening and Verification' (Protocol in workflow.md)
