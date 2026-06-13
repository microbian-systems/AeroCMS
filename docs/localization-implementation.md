# Spec: Localization & Globalization for AeroCMS

## Objective

Make AeroCMS fully localization and globalization ready — every content entity, admin screen, and public-facing page must support multiple cultures with independent slugs, per-entity translations, full RTL layout support, and both manual (CMS UI) and import-based translation workflows.

**User stories:**
- As a site admin, I can add supported cultures to my site (e.g., `en-US`, `es-MX`, `ar-SA`)
- As a content editor, I can create a page in `en-US`, then clone/import a translated `es-MX` variant with its own slug (`/about` vs `/acerca-de`) and its own blocks/layout
- As a visitor, I see content in my preferred culture via URL prefix (`/en-us/about`, `/es-mx/acerca-de`), with `<html dir="rtl">` for RTL cultures
- As an SEO specialist, I get `hreflang` tags and per-culture sitemaps
- As a developer, I can import translations via file (matching the existing ZipPostImportParser pattern)
- As an admin, I configure separate NavMenus per culture (e.g., `"Main-en-US"`, `"Main-es-MX"`)
- As a visitor seeing untranslated content, I see a banner: *"This page isn't available in [Language] yet"*

## Tech Stack

- **Backend**: ASP.NET Core (.NET 10), Orleans, MartenDB (PostgreSQL/JSONB), Wolverine (event sourcing)
- **Frontend**: Blazor/Razor (code-behind), Tailwind CSS v4 (CDN), Radzen Blazor, NeoUI components
- **Current patterns**: Railway-oriented programming (`Result<T>`, `Option<T>`), per-entity event sourcing, `IGenericMartenRepository<T>`

## Commands

```
Build:     dotnet build
Test:      dotnet test
Lint:      dotnet format --verify-no-changes
Dev:       dotnet run --project src/Aero.Cms.Host
Ef Mig:    dotnet ef migrations add [name]
```

## Project Structure (affected areas)

```
src/Aero.Cms.Core.Entities/
  ├── PageDocument.cs              ← Updated: Culture + TranslationGroupId
  ├── PostDocument.cs              ← Updated: Culture + TranslationGroupId
  ├── CategoryTranslation.cs       ← New (sidecar translation table)
  ├── TagTranslation.cs            ← New (sidecar translation table)
  ├── SitesModel.cs                ← Updated: add SupportedCultures (retain DefaultCulture)
  └── ContentSlugDocument.cs       ← Updated: add Culture field (see SlugRegistry)

src/Aero.Cms.Abstractions/Events/
  ├── PageCreated.cs               ← Updated: add Culture + TranslationGroupId params
  ├── PageContentUpdated.cs        ← Updated: add Culture param
  ├── PageMetadataUpdated.cs       ← Updated: add Culture param
  ├── PostCreated.cs               ← Updated: add Culture + TranslationGroupId params
  ├── CategoryTranslationSaved.cs  ← New
  ├── TagTranslationSaved.cs       ← New
  └── ProductTranslationSaved.cs   ← New

src/Aero.Cms.Abstractions/Requests/
  ├── CreateSiteRequest.cs         ← Updated: add DefaultCulture + SupportedCultures
  └── UpdateSiteRequest.cs         ← Updated: add DefaultCulture + SupportedCultures

src/Aero.Cms.Abstractions/Models/
  └── SiteViewModel.cs             ← Updated: add SupportedCultures

src/Aero.Cms.Data/Queries/         ← Updated: slug queries scoped to (SiteId, Culture)

src/Aero.Cms.Shared/Blocks/Rendering/
  └── BlockRenderContext.cs        ← Updated: populate Culture from request

src/Aero.Cms.Web.Core/Pipelines/
  └── Contexts.cs                  ← Updated: add Culture to MVC BlockRenderContext

src/Aero.Cms.Host/
  └── Program.cs                   ← Updated: custom AeroRequestCultureProvider + middleware

src/Aero.Cms.Modules.Setup/
  ├── SeedDataService.cs           ← Updated: culture-aware seed data
  └── Areas/Setup/                 ← Updated: setup UI for supported cultures

src/Aero.Cms.Modules.Pages/
  ├── Services/                    ← Updated: culture-scoped queries, translation fork service
  ├── SlugRegistry.cs              ← Updated: slug uniqueness per (SiteId, Culture)
  └── PagesModule.cs               ← Updated: Marten unique index for ContentSlugDocument

src/Aero.Cms.Modules.Posts/
  └── Services/                    ← Updated: culture-scoped queries, translation fork service

src/Aero.Cms.Modules.Navigation/
  └── Domain/
      ├── NavMenuDocument.cs       ← Updated: Culture + TranslationGroupId
      └── NavMenuEvents.cs         ← Updated: Culture + TranslationGroupId in events

src/Aero.Cms.Modules.Footer/
  └── Domain/
      ├── FooterDocument.cs        ← Updated: Culture + TranslationGroupId
      └── FooterEvents.cs          ← Updated: Culture + TranslationGroupId in events

src/Aero.Cms.Modules.Commerce/
  └── Catalog/Models/
      └── ProductDocument.cs       ← Updated: ProductTranslation (sidecar, TBD)

src/Aero.Cms.Modules.Cache/
  └── PageCacheHooks.cs            ← Verified: ctx.Culture already in cache key

src/Aero.Cms.Shared/Pages/Manager/
  ├── Sites.razor                  ← Updated: culture config in site management
  ├── Settings/Settings.razor      ← Updated: supported cultures in settings
  └── TranslationEditor.razor (new)

src/Aero.Cms.Shared/Components/
  ├── CultureSwitcher.razor (new)
  ├── HreflangTags.razor (new)
  └── FallbackBanner.razor (new)

NeoUI/src/NeoUI.Blazor/            ← Updated: systematic RTL via logical CSS
```

## Code Style

Follow existing patterns — Railway Oriented Programming, event-sourced entities, code-behind Razor files, no inline code in `.razor` files. Use `CultureInfo` not string culture codes internally. Use `Result<T>` for service layer operations. All entities follow the same `Entity : IEntity<long>` pattern with Snowflake IDs.

## Core Architecture Decision: Document-Per-Culture (not sidecar translations)

For AeroCMS's Marten document model and event sourcing, the correct approach is **document-per-culture** for rich entities. Each culture gets its own full document, linked by `TranslationGroupId`. This is idiomatic Marten — documents are the unit of change, and having one document per culture means each culture can have independent blocks, layouts, publish state, and hierarchy.

```text
                        TranslationGroupId: 42
              ┌────────────────┼────────────────┐
              ▼                ▼                ▼
    PageDocument           PageDocument      PageDocument
    Culture: "en-US"       Culture: "es-MX"  Culture: "ar-SA"
    Slug: "/about"         Slug: "/acerca-de" Slug: "/حول"
    Blocks: [Hero content, Blocks: [Hero content, Blocks: [Hero content,
             CTA content]          CTA content]          CTA content]
```

The block examples above are culture-specific block **instances/data**, not
separate per-language block types. Aero should keep one shared block
implementation (`Hero`, `CTA`, etc.) and create or clone localized block data
only when a translated page variant is created.

### Entity translation strategy by type

| Entity | Strategy | Rationale |
|---|---|---|
| **PageDocument** | Document-per-culture | Rich document (blocks, layouts, hierarchy, publish state) — each culture needs independent content |
| **PostDocument** | Document-per-culture | Same reasoning as pages — full content per culture |
| **NavMenuDocument** | Document-per-culture | Independent menu structure, items, and labels per culture |
| **FooterDocument** | Document-per-culture | Independent footer content per culture |
| **ProductDocument** | Sidecar (Phase 1) | Current product model is relatively flat; document-per-culture can be a future decision if products gain rich content |
| **CategoryModel** | Sidecar | Simple entity with only name/slug/description; referenced by ID from posts |
| **TagModel** | Sidecar | Simple entity with only name/description; referenced by ID from posts |

## Entity Schemas

### Document-per-culture entities (PageDocument, PostDocument, NavMenuDocument, FooterDocument)

```csharp
// PageDocument — revised with culture fields
public sealed class PageDocument : Entity, ISiteOwned, ISoftDeleted, IAuditableEntity
{
    public long SiteId { get; set; }
    public long? TranslationGroupId { get; set; }   // NEW: links culture variants
    public string Culture { get; set; }            // NEW: "en-US", "es-MX", etc.
    // ... all existing fields (Slug, Title, Path, LayoutRegions, Blocks, etc.)
}

// PostDocument — same pattern
public sealed class PostDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long? TranslationGroupId { get; set; }   // NEW
    public string Culture { get; set; }            // NEW
    // ... all existing fields
}

// NavMenuDocument — same pattern
public sealed class NavMenuDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long? TranslationGroupId { get; set; }   // NEW
    public string Culture { get; set; }            // NEW
    // ... all existing fields (Name, Key, State, etc.)
}

// FooterDocument — same pattern
public sealed class FooterDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public long? TranslationGroupId { get; set; }   // NEW
    public string Culture { get; set; }            // NEW
    // ... all existing fields
}
```

### Sidecar translation entities (categories, tags, products — simpler types)

```csharp
public sealed class CategoryTranslation : Entity
{
    public long CategoryId { get; set; }
    public string Culture { get; set; }         // "es-MX"
    public string Name { get; set; }
    public string Slug { get; set; }
    public string? Description { get; set; }
}

public sealed class TagTranslation : Entity
{
    public long TagId { get; set; }
    public string Culture { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
}

// ProductTranslation — sidecar for Phase 1 (TBD if product becomes document-per-culture later)
public sealed class ProductTranslation : Entity
{
    public long ProductId { get; set; }
    public string Culture { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
}
```

### Slug registry

```csharp
// ContentSlugDocument — add Culture dimension
// Current unique index: (SiteId, NormalizedSlug)
// New unique index:      (SiteId, Culture, NormalizedSlug)
public sealed class ContentSlugDocument : Entity
{
    public long SiteId { get; set; }
    public string Culture { get; set; }          // NEW
    public string Slug { get; set; }
    public string NormalizedSlug { get; set; }
    public long OwnerId { get; set; }
    public string OwnerType { get; set; }
}
```

## Testing Strategy

- TUnit for unit tests (translation fork/clone logic, fallback logic, culture-scoped queries)
- Playwright for GUI integration (culture switcher, RTL rendering, hreflang tags, fallback banner)
- NSubstitute + AutoFixture for mocking
- Bogus for fake translation data
- Embedded Postgres (mysticmind-postgresembed) for Marten integration tests

## Boundaries

- **Always do:** Culture-scope queries, propagate `BlockRenderContext.Culture`, validate culture codes against site's `SupportedCultures`, add RTL dir attribute on `<html>`, show fallback banner when translation missing
- **Ask first:** Adding new culture providers beyond URL prefix, changing the existing `ContentSlugDocument` unique constraints, introducing new document-per-culture entity types beyond the seven core ones above
- **Never do:** Remove existing monolingual data, break the `PageDocument` event stream schema without migration path, use reflection-based culture discovery, commit without culture propagation in the rendering pipeline

## Implementation Phases

### Phase 1 — Data Model + Site Config

**Files modified (document-per-culture entities):**
- `src/Aero.Cms.Core.Entities/PageDocument.cs` — add `TranslationGroupId` (nullable long), `Culture` (string)
- `src/Aero.Cms.Core.Entities/PostDocument.cs` — add `TranslationGroupId`, `Culture`
- `src/Aero.Cms.Modules.Navigation/Domain/NavMenuDocument.cs` — add `TranslationGroupId`, `Culture`
- `src/Aero.Cms.Modules.Footer/Domain/FooterDocument.cs` — add `TranslationGroupId`, `Culture`
- `src/Aero.Cms.Core.Entities/SitesModel.cs` — add `SupportedCultures` (retain `DefaultCulture`; `DefaultCulture` must be in `SupportedCultures`)
- `src/Aero.Cms.Modules.Pages/SlugRegistry.cs` (`ContentSlugDocument`) — add `Culture` field + update unique index to `(SiteId, Culture, NormalizedSlug)`

**Files modified (events — add Culture + TranslationGroupId to existing Wolverine events):**
- `src/Aero.Cms.Abstractions/Events/PageEvents.cs` — `PageCreated` gains `Culture`, `TranslationGroupId` params
- Navigation events — `NavMenuCreated` gains `Culture`, `TranslationGroupId` params
- Footer events — `FooterCreated` gains `Culture`, `TranslationGroupId` params

**Files created (sidecar translations for simple entities):**
- `src/Aero.Cms.Core.Entities/CategoryTranslation.cs`
- `src/Aero.Cms.Core.Entities/TagTranslation.cs`
- `src/Aero.Cms.Core.Entities/ProductTranslation.cs` (sidecar, TBD for later)

**Files created (sidecar translation events):**
- `src/Aero.Cms.Abstractions/Events/CategoryTranslationSaved.cs`
- `src/Aero.Cms.Abstractions/Events/TagTranslationSaved.cs`
- `src/Aero.Cms.Abstractions/Events/ProductTranslationSaved.cs`

**Files modified (site DTOs):**
- `src/Aero.Cms.Abstractions/Requests/CreateSiteRequest.cs` — add `DefaultCulture` + `SupportedCultures`
- `src/Aero.Cms.Abstractions/Requests/UpdateSiteRequest.cs` — add `DefaultCulture` + `SupportedCultures`
- `src/Aero.Cms.Abstractions/Models/SiteViewModel.cs` — add `SupportedCultures`
- `src/Aero.Cms.Modules.Sites/SitesApi.cs` — populate new fields

### Phase 2 — Middleware + Pipeline

**Key change from initial design:** Culture routing is **site-aware**, not global. A custom `AeroRequestCultureProvider` resolves the site first, then validates the URL culture against that site's `SupportedCultures`.

**Files created:**
- `src/Aero.Cms.Shared/Localization/AeroRequestCultureProvider.cs` (new) — custom `RequestCultureProvider` that:
  1. Resolves site from host header
  2. Reads culture from URL route (`{culture}`)
  3. Validates against site's `SupportedCultures`
  4. Falls back to site's `DefaultCulture` if invalid/missing
  5. Sets `CultureInfo.CurrentCulture` and `CultureInfo.CurrentUICulture`

**Files modified:**
- `src/Aero.Cms.Host/Program.cs` — register `AeroRequestCultureProvider`, configure middleware order (after routing, before auth)
- `src/Aero.Cms.Shared/Blocks/Rendering/BlockRenderContext.cs` — populate `Culture` from request culture (currently null)
- `src/Aero.Cms.Web.Core/Pipelines/Contexts.cs` — add `Culture` property to MVC pipeline `BlockRenderContext`
- `src/Aero.Cms.Modules.Cache/PageCacheHooks.cs` — verify `ctx.Culture` is populated (cache key already references it)

### Phase 3 — Content Queries + Fork Service

**Files modified (per entity query scoping):**
- `src/Aero.Cms.Data/Queries/` — all slug+site queries: `SiteId + Culture + Slug`
- `src/Aero.Cms.Modules.Pages/Services/PageService.cs` — `ForkPageForCulture(sourcePageId, targetCulture, targetSlug)` creates new PageDocument with same `TranslationGroupId`, new `Id`, independent blocks/layout. `GetPage(siteId, culture, slug)` returns page or falls back to default culture.
- `src/Aero.Cms.Modules.Posts/Services/PostService.cs` — same fork/query pattern
- `src/Aero.Cms.Modules.Navigation/Services/NavMenuService.cs` — `GetByKey(siteId, key, culture)` resolves `(TranslationGroupId, Culture)` to the correct NavMenuDocument; falls back to default culture
- `src/Aero.Cms.Modules.Footer/Services/FooterService.cs` — same pattern as NavMenu
- `src/Aero.Cms.Modules.Commerce/Catalog/Services/ProductService.cs` — culture-scoped sidecar translation lookups, default-culture fallback
- `src/Aero.Cms.Modules.Posts/Services/CategoryService.cs` — sidecar translation lookups + fallback
- `src/Aero.Cms.Modules.Posts/Services/TagService.cs` — same

**Default culture fallback (for querying non-existent translations):**
- When a document-per-culture entity is requested in `es-MX` but no `es-MX` variant exists:
  1. Find any document with same `TranslationGroupId` and site's `DefaultCulture`
  2. Render it with a `FallbackBanner` component
  3. Do NOT redirect — preserve the URL the user requested

### Phase 4 — Admin UI Translations

**Files modified:**
- `src/Aero.Cms.Shared/Pages/Manager/Sites.razor` — supported cultures config UI
- `src/Aero.Cms.Shared/Pages/Manager/Settings/Settings.razor` — culture settings
- `src/Aero.Cms.Shared/Pages/Manager/TranslationEditor.razor` (new) — "Fork to culture" button + translation editing
- `src/Aero.Cms.Shared/Components/CultureSwitcher.razor` (new) — language switcher component
- `src/Aero.Cms.Shared/Layouts/ManagerLayout.razor` — add culture switcher to admin toolbar
- `src/Aero.Cms.Shared/Layouts/PublicLayout.razor` — add culture switcher to site header

### Phase 5 — RTL CSS Audit

**Scope:** Every `.razor`, `.cshtml`, and `.css` file in the project (~300+ files)

**Pattern — physical → logical replacements:**

| Physical | Logical |
|---|---|
| `ml-*`/`mr-*` | `ms-*`/`me-*` |
| `pl-*`/`pr-*` | `ps-*`/`pe-*` |
| `space-x-*` | `gap-*` |
| `left-*`/`right-*` | `inset-inline-start-*`/`inset-inline-end-*` |
| `text-left`/`text-right` | `text-start`/`text-end` |
| `rounded-l-*`/`rounded-r-*` | `rounded-s-*`/`rounded-e-*` |
| `border-l-*`/`border-r-*` | `border-s-*`/`border-e-*` |

**Files modified:**
- `src/Aero.Cms.Shared/Layouts/PublicLayout.razor` — add `<html dir="@dir">`
- `src/Aero.Cms.Shared/Layouts/ManagerLayout.razor` — add `<html dir="@dir">`
- All HyperUI block renderers — complete existing partial audit
- All admin pages (`src/Aero.Cms.Shared/Pages/Manager/**/*.razor`)
- All public pages (`src/Aero.Cms.Modules.*/Areas/**/*.cshtml`)
- NeoUI component Razor files — add `Dir` cascading parameter to components with programmatic navigation

**Reference:** `docs/aero-hyper-ui-implementation.md` Sections 5 and 10.

### Phase 6 — SEO

**Files created/modified:**
- `src/Aero.Cms.Shared/Components/HreflangTags.razor` (new) — `<link rel="alternate" hreflang="...">` per supported culture
- `src/Aero.Cms.Shared/Components/FallbackBanner.razor` (new) — *"This page isn't available in [Language]. Showing [Default] version."*
- `src/Aero.Cms.Shared/Layouts/PublicLayout.razor` — include HreflangTags + FallbackBanner
- `src/Aero.Cms.Modules.SiteMap/SiteMapService.cs` — per-culture sitemaps + sitemap index

### Phase 7 — Seed Data + Translation Import

**Files modified:**
- `src/Aero.Cms.Modules.Setup/SeedDataService.cs` — add `es-MX` variants for pages, posts, nav, footer
- `src/Aero.Cms.Modules.Setup/Areas/Setup/Pages/Setup.razor` — add supported cultures step

**Files created:**
- `src/Aero.Cms.Modules.Setup/Services/TranslationImportService.cs` (new) — bulk import from zip/json
- `src/Aero.Cms.Modules.Setup/Endpoints/TranslationImportEndpoint.cs` (new) — upload endpoint

## Success Criteria

- [ ] Site config supports multiple cultures with a configured default
- [ ] `/en-us/about` and `/es-mx/acerca-de` resolve to different PageDocuments with the same `TranslationGroupId`
- [ ] Each PageDocument variant can have independent blocks, layouts, and slug
- [ ] Admin can fork/clone a page to a new culture via CMS UI
- [ ] Import/export translations via file (bulk endpoint)
- [ ] `<html dir="rtl">` applied for RTL cultures (Arabic, Hebrew, etc.)
- [ ] All Razor views use logical CSS properties (no `ml-`, `mr-`, `pl-`, `pr-`, `space-x-`, `left`, `right`)
- [ ] `hreflang` tags present on public `<head>`
- [ ] Per-culture sitemaps generated
- [ ] NeoUI components work correctly in RTL mode
- [ ] `BlockRenderContext.Culture` populated from request
- [ ] Fallback banner shown when no translation exists for requested culture
- [ ] Cache keys include culture dimension (verify existing pattern)
- [ ] NavMenu resolves per `(TranslationGroupId, Culture)`
- [ ] Custom `AeroRequestCultureProvider` validates culture against resolved site
- [ ] `ContentSlugDocument` unique constraint scoped to `(SiteId, Culture, NormalizedSlug)`
- [ ] All existing tests still pass
- [ ] New TUnit tests for fork/clone logic, fallback logic, culture-scoped queries
- [ ] Playwright tests for culture switcher, RTL rendering, fallback banner

---

*Last updated: 2026-05-30*
