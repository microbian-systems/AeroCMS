# Implementation Plan: Localization & Globalization for AeroCMS

## Overview

Add multi-culture support across the full AeroCMS stack using **document-per-culture** for rich entities (Pages, Posts, NavMenus, Footers) and **sidecar translation tables** for simple entities (Categories, Tags, Products). Documents of the same logical content are linked by `TranslationSetId`, each with their own blocks, layouts, and slug. URL-prefix routing is site-aware via a custom `AeroRequestCultureProvider`. Full RTL layout support for both public and admin UIs.

## Architecture Decisions

### Decision: Document-per-culture for rich entities (not sidecar translation tables)
**Rationale:** AeroCMS's Marten document model and Wolverine event sourcing treat documents as the unit of change. PageDocument already owns slug, title, SEO, hierarchy, LayoutRegions, Blocks, BlockIdMap, and publish state — updates apply these together. Creating a separate `PageTranslation` sidecar would fracture this natural unit. Each culture gets its own full PageDocument with `Culture` and `TranslationSetId`, so blocks, images, CTAs, layouts, and publish state can all differ per culture. This is idiomatic Marten.

### Decision: Sidecar translations for simple entities (Categories, Tags)
**Rationale:** CategoryModel and TagModel have only name/slug/description — they are referenced by ID from posts. A full document-per-culture approach here wastes storage and complicates ID references. A sidecar `CategoryTranslation`/`TagTranslation` is the right weight for these simple types.

### Decision: URL culture prefix + site-aware custom provider (not cookie/header-based)
**Rationale:** URL prefix (`/en-us/page-slug`) is SEO-friendly, cache-friendly (same URL = same content), and works without JavaScript. Loading cultures globally at startup is wrong for a multi-site CMS — each site has different supported cultures. A custom `AeroRequestCultureProvider` resolves the site from the host header first, then validates the URL culture against that site's `SupportedCultures`, before setting `CurrentCulture`/`CurrentUICulture`.

### Decision: Separate NavMenu per culture (not per-item translation table)
**Rationale:** NavMenu structure often differs between cultures (different menu items, different hierarchy). Document-per-culture NavMenus linked by `TranslationSetId` give full flexibility. Each NavMenu has its own `Culture` field for direct querying (`(TranslationSetId, Culture)` → NavMenu).

### Decision: Culture banner on missing translations (not redirect, not silent)
**Rationale:** A fallback banner preserves SEO (avoids 404/301 noise), signals to users that more translated content exists, and doesn't break the navigation flow. The user can still read the default-culture content.

### Decision: Blocks are culture-agnostic as a Phase 1 bootstrap compromise
**Rationale:** Cross-culture block sharing (same `BlockBase` IDs across variants) may be offered as an optimization for initial migration speed. But the target data model supports independent blocks per culture — each per-culture PageDocument has its own `Blocks` and `LayoutRegions`. Phase 1 may clone block structures during fork; a future phase can add cross-culture block linking.

### Decision: RTL via `<html dir="rtl">` + logical CSS properties
**Rationale:** For static SSR rendering, `<html dir="rtl">` combined with Tailwind logical properties handles 95% of RTL automatically. Only components with programmatic keyboard navigation (DropdownMenu, Carousel) need a `Dir` parameter specifically for arrow key reversal.

---

## Task List

### Phase 1: Foundation — Site Config + Entity Updates

#### Checkpoint 1A: Data Layer Foundation (Tasks 1.1–1.3)

- [ ] **Task 1.1:** Add `SupportedCultures` to `SitesModel` (additive, retain `DefaultCulture`)
  - **Acceptance:** SitesModel retains `DefaultCulture` and gains `SupportedCultures` (list of culture codes). `DefaultCulture` must be present in `SupportedCultures`. Marten serializes `SupportedCultures` as JSON array. Existing sites with null `SupportedCultures` default to `["en-US"]`.
  - **Verify:** Build succeeds. Site document serializes/deserializes correctly.
  - **Files:** `src/Aero.Cms.Core.Entities/SitesModel.cs`
  - **Scope:** XS (1 file)

- [ ] **Task 1.2:** Add `DefaultCulture` + `SupportedCultures` to site request DTOs + API handler
  - **Acceptance:** `CreateSiteRequest`, `UpdateSiteRequest`, and `SiteViewModel` include `DefaultCulture` + optional `SupportedCultures`. `SitesApi` populates both from requests, defaulting `SupportedCultures` to `["en-US"]` when not provided.
  - **Verify:** Build succeeds. Existing sites with null values default correctly.
  - **Files:** `src/Aero.Cms.Abstractions/Requests/CreateSiteRequest.cs`, `src/Aero.Cms.Modules.Sites/SitesApi.cs`, `src/Aero.Cms.Abstractions/Models/SiteViewModel.cs`
  - **Scope:** S (3 files)

- [ ] **Task 1.3:** Add `Culture` + `TranslationSetId` to document-per-culture entities
  - **Acceptance:** `PageDocument`, `PostDocument`, `NavMenuDocument`, and `FooterDocument` each gain `long? TranslationSetId` (nullable) and `string Culture` (non-null, defaults to `"en-US"`). Existing documents without culture values default to site's `DefaultCulture` via migration or document read fallback.
  - **Verify:** Build succeeds. Existing documents can be read from Marten without errors.
  - **Files:** `src/Aero.Cms.Core.Entities/PageDocument.cs`, `src/Aero.Cms.Core.Entities/PostDocument.cs`, `src/Aero.Cms.Modules.Navigation/Domain/NavMenuDocument.cs`, `src/Aero.Cms.Modules.Footer/Domain/FooterDocument.cs`
  - **Scope:** M (4 files)

#### Checkpoint 1B: Events + Slug Registry + Sidecar Translations (Tasks 1.4–1.7)

- [ ] **Task 1.4:** Add `Culture` + `TranslationSetId` to document-per-culture events
  - **Acceptance:** Wolverine events gain culture parameters where needed:
    - `PageCreated` → + `Culture`, `TranslationSetId`
    - `NavMenuCreated` → + `Culture`, `TranslationSetId`
    - `FooterCreated` → + `Culture`, `TranslationSetId`
    - `PostCreated` (if any) → + `Culture`, `TranslationSetId`
    - Existing events that don't create new documents (e.g., `PageContentUpdated`, `NavMenuPublished`) do NOT need culture params — they inherit from the document's already-set culture.
    - Doc `Apply()` methods assign the new fields from events where present.
  - **Verify:** Build succeeds. Existing event-sourced documents replay correctly (new params are additive, null defaults).
  - **Files:** `src/Aero.Cms.Abstractions/Events/PageEvents.cs`, `src/Aero.Cms.Modules.Navigation/Events/NavMenuEvents.cs`, `src/Aero.Cms.Modules.Footer/Events/FooterEvents.cs`
  - **Scope:** M (3 files)

- [ ] **Task 1.5:** Add `Culture` to `ContentSlugDocument` + update Marten unique index
  - **Acceptance:** `ContentSlugDocument` gains `Culture` field. Marten unique index updated from `(SiteId, NormalizedSlug)` to `(SiteId, Culture, NormalizedSlug)`. Backfill: existing slugs assigned site's `DefaultCulture` on migration.
  - **Verify:** Create two pages with same `NormalizedSlug` but different `Culture` — both succeed. Same culture + slug — rejected by unique index.
  - **Files:** `src/Aero.Cms.Modules.Pages/SlugRegistry.cs` (`ContentSlugDocument`), `src/Aero.Cms.Modules.Pages/PagesModule.cs` (Marten index configuration)
  - **Scope:** S (2 files)

- [ ] **Task 1.6:** Create sidecar translation entities for simple types
  - **Acceptance:** `CategoryTranslation`, `TagTranslation`, `ProductTranslation` entities created in `src/Aero.Cms.Core.Entities/`. Each extends `Entity`, follows the same `{ SourceId, Culture, TranslatedFields }` pattern.
  - **Verify:** Build succeeds. Entities can be stored/retrieved via `IGenericMartenRepository<T>`.
  - **Files:** `src/Aero.Cms.Core.Entities/CategoryTranslation.cs` (new), `src/Aero.Cms.Core.Entities/TagTranslation.cs` (new), `src/Aero.Cms.Core.Entities/ProductTranslation.cs` (new)
  - **Scope:** S (3 new files)

- [ ] **Task 1.7:** Create sidecar translation events + `ICultureAware` validator
  - **Acceptance:** `CategoryTranslationSaved`, `TagTranslationSaved`, `ProductTranslationSaved` sealed records created in Events directory. `ICultureAware` marker interface and `CultureCodeValidator : AbstractValidator<ICultureAware>` created for FluentValidation integration.
  - **Verify:** Build succeeds. Validator rejects invalid culture codes.
  - **Files:** `src/Aero.Cms.Abstractions/Events/CategoryTranslationSaved.cs` (new, +2 more), `src/Aero.Cms.Abstractions/Interfaces/ICultureAware.cs` (new), `src/Aero.Cms.Abstractions/Validators/CultureCodeValidator.cs` (new)
  - **Scope:** S (5 new files)

---

### Phase 2: Pipeline — Middleware + Culture Routing

- [ ] **Task 2.1:** Create custom `AeroRequestCultureProvider` + register in host
  - **Acceptance:** New `AeroRequestCultureProvider : RequestCultureProvider` in `src/Aero.Cms.Shared/Localization/`. Provider:
    1. Resolves site from host header (via `ISiteResolver` or `IQuerySession`)
    2. Reads `{culture}` from route data (URL prefix `/{culture}/...`)
    3. Validates culture is in the resolved site's `SupportedCultures`
    4. Falls back to site's `DefaultCulture` if invalid/missing
    5. Sets `CurrentCulture` + `CurrentUICulture`
    Registered in `Program.cs` via `RequestLocalizationOptions.RequestCultureProviders.Add()`. Middleware inserted after routing, before auth.
  - **Verify:** Navigate to `/es-mx/page` — `CultureInfo.CurrentCulture.Name == "es-MX"`. Navigate to `/invalid-culture/page` — falls back to default culture. Navigate to bare `/page` — uses default culture.
  - **Files:** `src/Aero.Cms.Shared/Localization/AeroRequestCultureProvider.cs` (new), `src/Aero.Cms.Host/Program.cs`
  - **Scope:** M (2 files, new concept)

- [ ] **Task 2.2:** Populate `BlockRenderContext.Culture` from resolved request culture
  - **Acceptance:** `BlockRenderContext.Culture` (currently nullable, defaults to null) is populated from `CultureInfo.CurrentCulture` when rendering pages. Both Blazor path (`BlockRenderContext.cs`) and MVC pipeline path (`Contexts.cs`) updated. Default value changes from `null` to `CultureInfo.CurrentCulture`.
  - **Verify:** Set breakpoint — `BlockRenderContext.Culture` is non-null during page render and matches URL culture. Run existing tests; no regressions.
  - **Files:** `src/Aero.Cms.Shared/Blocks/Rendering/BlockRenderContext.cs`, `src/Aero.Cms.Web.Core/Pipelines/Contexts.cs`, callers that create `BlockRenderContext`
  - **Scope:** S (2 files)

- [ ] **Task 2.3:** Verify cache key culture dimension
  - **Acceptance:** `PageCacheHooks.cs` already uses `ctx.Culture` in cache key template. Verify the value is non-null and correct for culture-prefixed URLs. Add debug assertion or test.
  - **Verify:** Cache keys differ between `/en-us/about` and `/es-mx/about`.
  - **Files:** `src/Aero.Cms.Modules.Cache/PageCacheHooks.cs`
  - **Scope:** XS (1 file, verification)

---

### Phase 3: Data Access — Culture-Scoped Queries + Fork Services

#### Checkpoint 3A: Pages + Posts (Tasks 3.1–3.2)

- [ ] **Task 3.1:** Update page services for culture-scoped queries + fork-to-culture
  - **Acceptance:** `PageService` exposes:
    - `GetPage(siteId, culture, slug)` — resolves PageDocument by `(SiteId, Culture, Slug)`. Falls back to default culture if not found. When falling back, returns a flag or wrapper identifying the fallback for `FallbackBanner`.
    - `ForkPage(sourcePageId, targetCulture, targetSlug)` — creates new PageDocument with same `TranslationSetId`, new Snowflake `Id`, new `Culture`, new `Slug`. Clones blocks/layout by default (Phase 1).
    - `ListPageCultureVariants(translationSetId)` — returns all PageDocuments sharing a `TranslationSetId`.
  - **Verify:** Unit test: create page in `en-US` (TranslationSetId=42), fork to `es-MX` with different slug → second PageDocument exists with TranslationSetId=42 and Culture="es-MX". Query by (siteId, "es-MX", "nuevo-slug") returns the fork. Query by (siteId, "fr-FR", "about") falls back to `en-US` variant.
  - **Files:** `src/Aero.Cms.Modules.Pages/Services/PageService.cs`, `src/Aero.Cms.Data/Queries/` (slug queries), `src/Aero.Cms.Modules.Pages/SlugRegistry.cs`
  - **Scope:** L (3-5 files, includes new fork method and query updates)

- [ ] **Task 3.2:** Update post services for culture-scoped queries + fork-to-culture
  - **Acceptance:** Same pattern as pages — `PostService` gains `GetPost(siteId, culture, slug)`, `ForkPost()`, `ListPostCultureVariants()`. Category and tag references remain shared (no culture fork for those IDs).
  - **Verify:** Unit tests for culture-scoped post queries and fork.
  - **Files:** `src/Aero.Cms.Modules.Posts/Services/PostService.cs`, relevant query files in `src/Aero.Cms.Data/Queries/`
  - **Scope:** M (2-3 files)

#### Checkpoint 3B: Sidecar Translations + Nav/Footer (Tasks 3.3–3.6)

- [ ] **Task 3.3:** Update category and tag services for sidecar translation lookups
  - **Acceptance:** `CategoryService`, `TagService` look up translations from `CategoryTranslation`/`TagTranslation`. On category/tag query: if translation exists for current culture, return translated name/slug/description. If not, fall back to default culture.
  - **Verify:** Unit test: category has English name "Sports", Spanish translation "Deportes". Query in `es-MX` → returns "Deportes". Query in `fr-FR` → falls back to "Sports".
  - **Files:** `src/Aero.Cms.Modules.Posts/Services/CategoryService.cs`, `src/Aero.Cms.Modules.Posts/Services/TagService.cs`, query files
  - **Scope:** S (2-3 files)

- [ ] **Task 3.4:** Update product service for sidecar translation lookups
  - **Acceptance:** `ProductService` resolves `ProductTranslation` for current culture with default-culture fallback. (ProductTranslation is sidecar for Phase 1; may become document-per-culture in future.)
  - **Verify:** Unit test: product queried in `es-MX` returns translated name/description; falls back to `en-US` if missing.
  - **Files:** `src/Aero.Cms.Modules.Commerce/Catalog/Services/ProductService.cs`, query files
  - **Scope:** S (2-3 files)

- [ ] **Task 3.5:** Update NavMenu service for per-culture document resolution
  - **Acceptance:** `NavMenuService.GetByKey(siteId, key, culture)` resolves `(TranslationSetId, Culture)` to the correct `NavMenuDocument`. If no menu exists for that culture, falls back to `(TranslationSetId, DefaultCulture)`. Key uniqueness becomes `(SiteId, Key, Culture)`.
  - **Verify:** Unit test: create two nav menus with same `TranslationSetId` but different `Culture`. `GetByKey("main", "es-MX")` returns the Spanish menu. `GetByKey("main", "fr-FR")` falls back to English.
  - **Files:** `src/Aero.Cms.Modules.Navigation/Services/NavMenuService.cs`, `src/Aero.Cms.Modules.Navigation/Domain/SiteNavigationSettingsDocument.cs`, query files
  - **Scope:** S (2-3 files)

- [ ] **Task 3.6:** Update Footer service for per-culture document resolution
  - **Acceptance:** Same pattern as NavMenu — `FooterService.GetByKey(siteId, key, culture)` resolves `(TranslationSetId, Culture)` to the correct `FooterDocument` with default-culture fallback.
  - **Verify:** Unit test with dual-culture footers.
  - **Files:** `src/Aero.Cms.Modules.Footer/Services/FooterService.cs`, `src/Aero.Cms.Modules.Footer/Domain/SiteFooterSettingsDocument.cs`, query files
  - **Scope:** S (2-3 files)

---

### Phase 4: Admin UI — CMS Culture Management

- [ ] **Task 4.1:** Add supported cultures UI to site management
  - **Acceptance:** `Sites.razor` allows adding/removing culture codes to `SupportedCultures`. Tag-input or multi-select with valid .NET culture codes. Default culture must be selected from supported list. `Settings.razor` shows `General.DefaultLocale` alongside site-specific cultures.
  - **Verify:** Create a site in admin UI, add `es-MX` as supported culture, save. Reload — cultures persist.
  - **Files:** `src/Aero.Cms.Shared/Pages/Manager/Sites.razor`, `src/Aero.Cms.Shared/Pages/Manager/Settings/Settings.razor`
  - **Scope:** S (2 files)

- [ ] **Task 4.2:** Create "Fork to Culture" button + translation tabs in page editor
  - **Acceptance:** Page editor has a culture dropdown showing all page variants. "Fork to Culture" button creates a new culture variant via `ForkPage()`. Translation tab shows culture variants with edit links. Post editor gets the same treatment.
  - **Verify:** Open page editor for `en-US` page → click "Fork → es-MX" → new draft created with `Culture="es-MX"`. Edit title/slug/blocks → save. Public site at `/es-mx/nuevo-slug` shows new content.
  - **Files:** `src/Aero.Cms.Shared/Pages/Manager/TranslationEditor.razor` (new), `TranslationEditor.razor.cs` (new), existing page/post editor pages
  - **Scope:** M (2-3 new files, integration with page/post editors)

- [ ] **Task 4.3:** Create `CultureSwitcher` component + integrate into layouts
  - **Acceptance:** Reusable Blazor component renders available cultures as links/buttons. Active culture highlighted. Adds to `ManagerLayout` (admin toolbar) and `PublicLayout` (site header). Navigates to same page in selected culture via URL prefix change.
  - **Verify:** Click "ES" on public site → navigates to `/es-mx/current-page`. Click "EN" → back to `/en-us/current-page`. Admin toolbar switcher works similarly.
  - **Files:** `src/Aero.Cms.Shared/Components/CultureSwitcher.razor` (new), `ManagerLayout.razor`, `PublicLayout.razor`
  - **Scope:** S (2-3 files)

---

### Phase 5: RTL CSS Audit

- [ ] **Task 5.1:** Add `<html dir>` to layouts
  - **Acceptance:** `PublicLayout.razor` and `ManagerLayout.razor` set `<html dir="@(IsRtl ? "rtl" : "ltr")">` based on `CultureInfo.CurrentCulture.TextInfo.IsRightToLeft`. Property computed in code-behind once per request.
  - **Verify:** Navigate to site in Arabic culture (`ar-SA`) → `<html dir="rtl">` in DOM. Layout flips correctly.
  - **Files:** `src/Aero.Cms.Shared/Layouts/PublicLayout.razor`, `PublicLayout.razor.cs`, `ManagerLayout.razor`, `ManagerLayout.razor.cs`
  - **Scope:** S (2 files)

- [ ] **Task 5.2:** Audit HyperUI blocks for physical CSS classes
  - **Acceptance:** All `.razor` files in `src/Aero.Cms.Ui.Hyper/Blocks/**/` use logical CSS properties. `rg` search for `ml-`, `mr-`, `pl-`, `pr-`, `space-x-`, `left-`, `right-`, `text-left`, `text-right`, `rounded-l-`, `rounded-r-`, `border-l-`, `border-r-` across HyperUI blocks directory → zero results.
  - **Verify:** `rg` search clean. Visual check in RTL mode.
  - **Files:** ~20 block renderer `.razor` files (complete existing partial audit)
  - **Scope:** M (~20 files, same pattern)

- [ ] **Task 5.3:** Audit admin pages for physical CSS classes
  - **Acceptance:** All `.razor` files in `src/Aero.Cms.Shared/Pages/Manager/**/*.razor` use logical CSS properties. `rg` search clean.
  - **Verify:** `rg` search across Manager pages → zero physical properties. Smoke test admin in RTL.
  - **Files:** ~30 `.razor` files in Manager directory
  - **Scope:** L (parallelizable by subdirectory)

- [ ] **Task 5.4:** Audit public pages for physical CSS classes
  - **Acceptance:** All `.cshtml` files in `src/Aero.Cms.Modules.*/Areas/**/*.cshtml` use logical CSS properties. `rg` search clean.
  - **Verify:** `rg` search → zero physical properties. Smoke test public pages in RTL.
  - **Files:** ~40 `.cshtml` files across modules (Docs, Posts, Pages, etc.)
  - **Scope:** L (parallelizable by module)

- [ ] **Task 5.5:** Add `Dir` to NeoUI components needing programmatic RTL
  - **Acceptance:** Components with arrow key navigation (CarouselRenderer, DropdownMenu, etc.) get `Dir` parameter defaulting to `"ltr"`, set from `CultureInfo.CurrentCulture.TextInfo.IsRightToLeft` in parent layout.
  - **Verify:** Carousel arrows reverse in RTL (left arrow = next, right arrow = previous).
  - **Files:** `CarouselRenderer.razor`, `CarouselRenderer.razor.cs`, any other navigation components
  - **Scope:** S (2-4 files)

---

### Phase 6: SEO — Hreflang + Sitemaps + Fallback Banner

- [ ] **Task 6.1:** Create `HreflangTags` component
  - **Acceptance:** Component renders `<link rel="alternate" hreflang="en-us" href="..." />` for every supported culture's variant of the current page. Included in `PublicLayout` `<head>`. Uses `TranslationSetId` to find all variants.
  - **Verify:** View page source — hreflang tags present for all site cultures. `x-default` points to default culture.
  - **Files:** `src/Aero.Cms.Shared/Components/HreflangTags.razor` (new), `PublicLayout.razor`
  - **Scope:** S (1-2 files)

- [ ] **Task 6.2:** Generate per-culture sitemaps
  - **Acceptance:** `SiteMapService` generates sitemap per culture. `/sitemap.xml` returns sitemap index referencing per-culture sitemaps (`/sitemap-en-us.xml`, `/sitemap-es-mx.xml`). Each URL includes `<xhtml:link rel="alternate" hreflang="...">` annotations.
  - **Verify:** Hit `/sitemap-es-mx.xml` → valid XML with Spanish URLs + hreflang annotations.
  - **Files:** `src/Aero.Cms.Modules.SiteMap/SiteMapService.cs`, sitemap endpoint
  - **Scope:** S (1-2 files)

- [ ] **Task 6.3:** Create `FallbackBanner` component
  - **Acceptance:** Rendered on public page layout when the current culture has no translation (rendering default-culture content). Shows: *"This page isn't available in [Language]. Showing [Default] version."* Dismissible, stored in session cookie.
  - **Verify:** Navigate to `/es-mx/page-without-translation` → banner appears. Dismiss → gone. Navigate to `/es-mx/page-with-translation` → no banner.
  - **Files:** `src/Aero.Cms.Shared/Components/FallbackBanner.razor` (new), `PublicLayout.razor`
  - **Scope:** S (2 files)

---

### Phase 7: Seed Data + Translation Import + Tests

- [ ] **Task 7.1:** Add Spanish (es-MX) seed variants
  - **Acceptance:** `SeedDataService` creates `es-MX` PageDocument variants for core pages (homepage, about, contact) with Spanish slugs, titles, and content. Creates `es-MX` NavMenuDocument and FooterDocument variants with same `TranslationSetId`. Creates `es-MX` CategoryTranslations for seeded categories.
  - **Verify:** Run setup wizard with `es-MX` as supported culture → seed data includes Spanish variants. Public site at `/es-mx/` shows Spanish content with Spanish navigation.
  - **Files:** `src/Aero.Cms.Modules.Setup/SeedDataService.cs`
  - **Scope:** M (1 file, substantial additions)

- [ ] **Task 7.2:** Create translation import service and endpoint
  - **Acceptance:** `TranslationImportService` imports culture variants from zip/json files. For document-per-culture entities: imports create new document variants. For sidecar entities: imports create translation records. Admin upload endpoint provided.
  - **Verify:** Upload zip with Spanish page variants → new PageDocuments created with Culture="es-MX", TranslationSetId linking to originals.
  - **Files:** `src/Aero.Cms.Modules.Setup/Services/TranslationImportService.cs` (new), `src/Aero.Cms.Modules.Setup/Endpoints/TranslationImportEndpoint.cs` (new)
  - **Scope:** S (2 new files)

- [ ] **Task 7.3:** Setup wizard — add supported cultures step
  - **Acceptance:** Setup wizard includes multi-select for `SupportedCultures` with common .NET culture codes as options. `DefaultCulture` dropdown limited to selected cultures.
  - **Verify:** Run setup wizard → cultures step appears → select `en-US` + `es-MX`, default=`en-US` → site created with both.
  - **Files:** `src/Aero.Cms.Modules.Setup/Areas/Setup/Pages/Setup.razor`, `Setup.razor.cs`, `SeedDataService.cs` (request model)
  - **Scope:** S (2-3 files)

- [ ] **Task 7.4:** Create comprehensive integration tests
  - **Acceptance:** TUnit integration tests covering:
    1. Create page in default culture, fork to `es-MX`, query both, verify fallback
    2. Culture-scoped slug uniqueness (`(SiteId, Culture, NormalizedSlug)` unique)
    3. NavMenu per-culture resolution (`(TranslationSetId, Culture)` → correct menu)
    4. Hreflang tag generation from `TranslationSetId` variant list
    5. Translation import → document-per-culture variant creation
    6. Custom `AeroRequestCultureProvider` site-aware routing
    7. Sidecar translation (CategoryTranslation) lookup with fallback
  - **Verify:** All tests pass with `dotnet test`.
  - **Files:** New test files in `tests/` (TUnit, embedded Postgres, NSubstitute)
  - **Scope:** M (multiple new test files)

---

## Parallelization Map

```
Phase 1 (Foundation)          Phase 5 (RTL CSS Audit)
  ├── 1.1 Site config          ├── 5.1 Layout dir
  ├── 1.2 Request DTOs         ├── 5.2 HyperUI blocks ───╮
  ├── 1.3 Entity culture fields│  5.3 Admin pages ────────┤ Can run in
  ├── 1.4 Event fields         │  5.4 Public pages ───────┤ parallel
  ├── 1.5 Slug registry        │  5.5 NeoUI components ───╯
  ├── 1.6 Sidecar entities
  └── 1.7 Sidecar events + validator
         │
    ┌────┴────┐
    ▼         ▼
Phase 2      Phase 3 (can run partially parallel after 1.3)
    │           │
    └────┬──────┘
         ▼
      Phase 4 (depends on 2 + 3)
         │
    ┌────┴────┐
    ▼         ▼
Phase 6      Phase 7
```

**Safe to parallelize:**
- Phase 5 (RTL CSS audit) with Phases 2–4 (different files, no dependencies)
- Tasks 5.2, 5.3, 5.4 (different directories, same pattern)
- Tasks 3.1, 3.2, 3.3, 3.4 (different services, shared contract)
- Tasks 6.1, 6.2, 6.3 (different files, no shared dependencies)

**Must be sequential:**
- Phase 1 → Phase 2 (middleware needs entity types + culture fields)
- Phase 1 → Phase 3 (queries need entity culture fields)
- Phase 2 + 3 → Phase 4 (admin UI needs services + routing working)
- Phase 4 → Phase 6 + 7 (SEO/seed need admin data + fork capability)

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Slug unique index migration breaks existing URLs | High | Existing slugs assigned site's `DefaultCulture` during migration. Marten index rebuild script. Dry-run on backup first. |
| PageDocument event stream versioning (Culture + TranslationSetId) | Med | New params are additive with defaults (`null`, `"en-US"`). Existing event streams replay correctly. Test with production-like data. |
| RTL CSS audit is tedious and error-prone | Med | `rg` search to find physical CSS. Fix in batches by directory. Playwright visual tests catch regressions. |
| Forking a page clones blocks — edits to one variant don't affect others (Phase 1) | Low | Documented as intended behavior. Cross-culture block linking is a future feature. |
| NavMenu event stream versioning with TranslationSetId + Culture | Low | Existing menus with null `TranslationSetId` + default `Culture` work as before. Additive fields. |
| Cache invalidation per culture | Low | Existing cache key already includes `ctx.Culture`. Just verify it's populated by the new provider. |
| AeroRequestCultureProvider resolves site per request | Med | Site resolution adds a query per request. Cache site resolution in `HttpContext.Items` for request duration. |

## File Summary (by phase)

| Phase | Files Created | Files Modified | Total |
|---|---|---|---|
| Phase 1 | 6 | 12 | 18 |
| Phase 2 | 1 | 3 | 4 |
| Phase 3 | 0 | 14 | 14 |
| Phase 4 | 2 | 4 | 6 |
| Phase 5 | 0 | ~95 | ~95 |
| Phase 6 | 2 | 3 | 5 |
| Phase 7 | 2 | 4 | 6 |
| **Total** | **13** | **~135** | **~148** |

Note: Phase 5 (RTL CSS audit) dominates the file count (~95 files) but all changes are the same mechanical pattern.

## Task Count Summary

| Phase | Tasks | Checkpoints |
|---|---|---|
| Phase 1 | 7 | 2 |
| Phase 2 | 3 | 0 |
| Phase 3 | 6 | 2 |
| Phase 4 | 3 | 0 |
| Phase 5 | 5 | 0 |
| Phase 6 | 3 | 0 |
| Phase 7 | 4 | 0 |
| **Total** | **31 tasks** | **4 checkpoints** |

---

*Generated from spec: `docs/localization-implementation.md`*
