# Aero CMS MVP — Final Implementation Plan

> Council-validated plan for the final MVP work package, updated with implementation-readiness findings.

| Metadata | |
|---|---|
| **Plan Status** | `Approved` |
| **Priority Chain** | Bug fixes → SEO infra → Content admin/i18n → Reference picker → Block types → Mega menu → UX polish |
| **Last Modified** | `2026-06-02` |
| **Implementation Started** | `2026-06-02` |
| **Implementation Complete** | `—` |
| **Plan Author** | Council-validated via OpenCode; Codex review notes incorporated |
| **Build Mode** | `Aero.Cms.Core, Shared, Navigation, Footer, Analytics, Content, Web builds passing` |

---

## Progress Dashboard

| # | Item | Status | Risk | Effort | Started | Completed | Notes |
|---|------|--------|------|--------|---------|-----------|-------|
| 1 | **Item 2** — Fix Delete in Header Editor | `review` | Low | ~2 hr | 2026-06-02 | — | Header canvas actions now use stable `ClientId` identity; build passed; user verification pending |
| 2 | **Item 5** — Fix Tablet Preview | `review` | Low | ~3 hr | 2026-06-02 | — | Added `NavMenuRenderMode.Tablet`; nav/footer canvas CSS now carries mobile/tablet/desktop spans |
| 3 | **Item 6** — SEO Scripts | `review` | Med | ~8-12 hr | 2026-06-02 | — | Settings-backed renderer, layout injection points, and Radzen provider list/detail UI implemented; user verification pending |
| 4 | **Item 8** — Content Item Translations | `in-progress` | Med | ~8-12 hr | 2026-06-02 | — | Translation identity, list/fork endpoints, client methods, and editor Translations tab implemented; AI/bulk operations pending |
| 5 | **Item 9** — Content Type Entries Tab | `review` | Med | ~4-6 hr | 2026-06-02 | — | ContentTypeEditor now has embedded entries grid with search, CRUD routing, delete, publish/unpublish, and real item counts |
| 6 | **Item 10** — Page Reference Picker | `pending` | Med-High | ~8-12 hr | — | — | Cross-editor picker with API, DTO/client, and schema touches |
| 7 | **Item 3** — Footer Brand Block | `pending` | Med | ~4-6 hr | — | — | Movable brand block with root fallback/normalization |
| 8 | **Item 4** — Social Media Block | `pending` | Med-High | ~6-8 hr | — | — | Header/footer social links with verified SVG source |
| 9 | **Item 1** — Mega Menu Data Structure | `pending` | High | ~10-16 hr | — | — | Structured data menu alongside `NavHtml` |
| 10 | **Item 7** — Editor UX Polish | `pending` | Med | ~8-12 hr | — | — | Final pass after feature slices stabilize |

**Status Legend**: `pending` → `in-progress` → `review` → `complete`

---

### Dependency Chain

```
Item 2 → Item 5 → Item 6 → Item 8 → Item 9 → Item 10 → Item 3 → Item 4 → Item 1 → Item 7
(bug)   (bug)   (infra)  (i18n)   (admin)   (refs)   (block)  (social) (mega)  (polish)
                                   ╰─── Items 3-4-1 can proceed after shared editor contracts settle
```

---

## Item 2 — Fix Delete in Header Editor

**Goal**: Make column and block delete buttons work in NavMenuEditor.razor (same as FooterEditor).

**Root investigation**: Code-behind methods (`RemoveColumn`, `RemoveBlockFromColumn`) in `NavMenuEditor.razor.cs:610-636` were nearly identical to working FooterEditor versions, but they removed items by object reference. Header sortable/render cycles already use `ClientId` as the stable item identity, so the delete/move/duplicate path should use the same identity to avoid silent no-op behavior after reorder/render updates.

**Files**:
- `src/Aero.Cms.Shared/Pages/Manager/NavMenuEditor.razor` — verified delete button click handlers
- `src/Aero.Cms.Shared/Pages/Manager/NavMenuEditor.razor.cs` — updated row/column/block actions to match sortable `ClientId` identity

**Acceptance criteria**:
- [x] Delete column button removes the column and its blocks
- [x] Delete block button removes the block
- [x] Deleting last column auto-adds a replacement column
- [x] Selected block ID is cleared on delete
- [x] Row orders are normalized after delete

---

---

## Item 5 — Fix Tablet Preview

**Goal**: Tablet preview mode renders tablet-sized viewport (768px) with correct responsive styling instead of falling back to mobile.

**Root cause**: `NavMenuRenderMode` enum had only `Desktop`/`Mobile` — no `Tablet` member. The `HtmlVisitor` in `NavMenuHtmlRenderer.cs` branched on `mode == NavMenuRenderMode.Mobile` for every component, making tablet behavior implicit. Runtime nav/footer canvas rendering also emitted tablet/desktop span CSS but did not carry the mobile span variable, leaving the three breakpoint settings inconsistent.

**Files**:
- `src/Aero.Cms.Modules.Navigation/Rendering/NavMenuHtmlRenderer.cs` — added `Tablet` to enum and made mobile-only branches explicit
- `src/Aero.Cms.Modules.Navigation/Views/Shared/Components/AeroNavBar/Default.cshtml` — runtime nav canvas now emits and consumes mobile/tablet/desktop span variables
- `src/Aero.Cms.Shared/Components/PreviewOverlay.razor` — verified tablet frame sizing is already present
- `src/Aero.Cms.Modules.Footer/Rendering/FooterHtmlRenderer.cs` — runtime footer canvas now emits and consumes mobile/tablet/desktop span variables
- `src/Aero.Cms.Shared/wwwroot/aero-manager.css` — verify tablet preview sizing

**Acceptance criteria**:
- [x] Tablet toggle renders preview at 768px width
- [x] Content uses tablet-responsive CSS classes (not mobile)
- [x] Desktop and mobile previews continue to work

---

---

## Item 6 — SEO Scripts

**Goal**: Wire up SEO list/detail pages and inject analytics scripts into `_CmsLayout.cshtml`.

**Implementation-readiness finding**: existing provider snippet code is useful, but it is not yet a complete render path. `AnalyticsInjectionHook` currently writes generated HTML into page metadata (`AnalyticsScripts`), while `_CmsLayout.cshtml` does not read that metadata. This item should first establish a simple, explicit layout injection bridge, then add the manager UI.

**Existing infrastructure** (already in place):
- `Aero.Cms.Modules.Analytics/AnalyticsInjectionHook.cs` — injects GA, Facebook Pixel, LinkedIn, Posthog, Clarity via `IPageReadHook` pipeline (Order 100)
- `AnalyticsSettings.cs` — 6 fields: `FacebookPixelId`, `GoogleAnalyticsId`, `LinkedInPartnerId`, `PosthogApiKey`, `PosthogHost`, `MicrosoftClarityId`
- `AnalyticsBlock` in `HtmlBlocks.cs` — per-page analytics block
- `SeoEnrichmentHook.cs` — per-page SeoTitle/SeoDescription injection
- `Settings.razor` — SEO section with Robots.txt, DefaultMetaDescription, DefaultOgImage

**Work required**:

### 6a — Rendering Contract First
- [x] Introduce an `ISeoScriptRenderer` or `ISeoScriptProvider` abstraction that returns grouped script output by placement: `Head`, `BodyStart`, `BodyEnd`.
- [x] Keep provider-specific snippet generation inside the Analytics/SEO module, not inside `_CmsLayout.cshtml`.
- [x] Add a small view component or layout service bridge that `_CmsLayout.cshtml` can call directly.
- Avoid relying on Razor `RenderSectionAsync` for scripts generated from page metadata because content pages cannot dynamically define sections after the layout has started rendering.
- Preserve the existing `IPageReadHook` path for page metadata enrichment, but do not make script output depend on metadata unless the layout has a proven way to read it.

### 6b — SEO List Page
- [x] New page at `/manager/seo` and `/manager/seo/general` using Radzen Grid.
- [x] Columns: Provider name, Tracking ID, Status (enabled/disabled), Last modified, Actions.
- [x] Row click navigates to detail page.
- [x] Disable action writes empty SEO settings so disabled providers override appsettings fallback.
- [x] Removed ambiguous Blazor routing by keeping the provider list on `/manager/seo` + `/manager/seo/general` and moving the older SEO analysis placeholder to `/manager/seo/analysis`.

### 6c — SEO Detail Page
- [x] Per-provider configuration form at `/manager/seo/{providerKey}`.
- [x] Supports Google Analytics, Facebook Pixel, LinkedIn, Posthog, and Microsoft Clarity.
- [x] Posthog includes optional host configuration with default fallback.
- [x] Disable action clears provider settings.
- [ ] Custom script input (raw HTML for unsupported providers) is deferred until the `SeoScript` entity slice.

### 6d — Custom Script Entity
- `SeoScript` entity: `IEntity<long>` (Snowflake ID)
- Fields: `Name`, `Provider` (enum: GoogleAnalytics, FacebookPixel, TikTokPixel, MicrosoftClarity, Posthog, LinkedIn, Custom), `TrackingId`, `ScriptContent`, `Placement` (enum: Head, BodyStart, BodyEnd), `IsEnabled`, `Order`

### 6e — Layout Injection
- [x] Add explicit injection points in `_CmsLayout.cshtml`:
  - Head: before `</head>`
  - Body start: immediately after `<body ...>`
  - Body end: before existing scripts / `</body>`
- [x] Render scripts through the new renderer/view component, not by hardcoding provider snippets into the layout.
- [x] Disabled provider scripts are filtered before rendering.
- [ ] Custom scripts must be emitted only at their configured placement and order.
- Use `ctx7` for exact script snippets per provider

### 6f — Provider Script Requirements (via ctx7)
- Google Analytics 4 (gtag.js)
- Microsoft Clarity
- Facebook Pixel
- TikTok Pixel
- Posthog

**Files**:
- `src/Aero.Cms.Modules.Analytics/SeoScriptRenderer.cs` — settings-backed grouped provider script renderer
- `src/Aero.Cms.Modules.Analytics/SeoScriptPlacement.cs` — layout placement enum
- `src/Aero.Cms.Modules.Analytics/ViewComponents/SeoScriptsViewComponent.cs` — Razor layout bridge
- `src/Aero.Cms.Modules.Analytics/AnalyticsModule.cs` — registers renderer
- `src/Aero.Cms.Shared/Pages/Manager/SeoScripts/SeoProviderList.razor` + `.razor.cs` — Radzen Grid provider list
- `src/Aero.Cms.Shared/Pages/Manager/SeoScripts/SeoProviderDetail.razor` + `.razor.cs` — provider settings form
- `src/Aero.Cms.Shared/Pages/Manager/SeoScripts/SeoProviderModels.cs` and `SeoProviderRegistry.cs` — provider definitions and UI models
- `src/Aero.Cms.Web/Views/Shared/_CmsLayout.cshtml` — add explicit SEO injection points

**Acceptance criteria**:
- [x] SEO list page shows all providers with Radzen Grid
- [x] Click row → detail page with configuration
- [ ] Custom scripts can be added (name + raw HTML)
- [x] Provider scripts render in correct layout position (head / body start / body end)
- [x] Existing `AnalyticsInjectionHook` metadata gap is resolved or replaced by a direct renderer/view component path
- [x] Disabled provider scripts are not rendered
- [x] `_CmsLayout.cshtml` has explicit SEO injection points for head/body placement

---

---

## Item 8 — Content Item Translations (Modeled After Pages)

**Risk**: Medium | **Effort**: ~8-12 hrs | **Dependency**: Can run in parallel with Items 3-4-1

**Goal**: Add culture/translation support to Content Items, mirroring the Pages translation system — fork to culture, AI translate, bulk publish/unpublish, culture variants list.

**Key architecture decisions**:
- Content items are localized; content type schemas are global within a site by default.
- A content type's invariant identity (`Id`, `SiteId`, `Alias`) and field handles (`Name`, `FieldType`) must not vary by culture.
- Editor-facing content type metadata may be localized/globalized later: type name, description, category label, field label, help text, placeholder, and display strings. This should be modeled as localization metadata on the schema, not as separate content type definitions per culture.
- Content item translations use the same content type schema so validation, rendering, AI translation, and search stay comparable across culture variants.
- Persisted entities must use Snowflake `long` IDs. Do not introduce GUID primary keys for content items, translation groups, SEO scripts, or other database entities.
- This repo is not in production yet, so no Marten migration script is required for MVP. Update the model/schema and use load/save normalization or startup seed repair where helpful.
- Slug uniqueness must be decided before implementation. Recommended MVP invariant: unique by `(SiteId, ContentTypeAlias, Culture, Slug)` for content items. If public routes later need global collision protection across pages/posts/docs/content items, route reservation should happen in the site-scoped slug/route registry for only the public URL surface.

### Existing Pages Translation Pattern (Proven Reference)

Pages translation uses a 4-layer architecture:

| Layer | Pages Implementation | Content Item Mirror |
|---|---|---|
| Data Model | `PageDocument.TranslationGroupId`, `.Culture`, `.SourcePageId` | Add same to `ContentItem` |
| Forker | `PageCultureForker` deep-clones document | New `ContentItemCultureForker` |
| API | `GET/POST /{id}/translations`, `POST /{id}/ai-translate`, group publish/unpublish | Same shape under `/{alias}/{id}/translations` |
| UI | "Localized Versions" tab in `PageEditor.razor` | New "Translations" tab in `ContentItemEditor.razor` |
| AI Translate | `BuildTranslatableFields()` → `AiContentTranslationService` → `ApplyTranslatedFields()` | Same flow with dynamic field mapping via `IFieldHintResolver` |

### 8a — Data Model Changes

Add to `ContentItem` (`src/Aero.Cms.Abstractions/Content/ContentItem.cs`) + `ContentItemViewModel`:

```csharp
public long? TranslationGroupId { get; set; }  // links culture variants
public string Culture { get; set; } = string.Empty;
public long? SourceItemId { get; set; }        // points back to original
```

Also add these fields to HTTP DTOs consumed by the manager UI:
- `ContentItemSummary`
- `ContentItemDetail`
- create/fork/translation response DTOs

Use `ContentSlugDocument.NormalizeCulture(...)` or the same normalization convention used by Pages before storing/querying culture values.

**Implementation checkpoint**:
- [x] `ContentItem` and `ContentItemViewModel` carry `TranslationGroupId`, `Culture`, and `SourceItemId`.
- [x] `ContentItemSummary` and `ContentItemDetail` carry translation metadata.
- [x] `ContentItemsApi` maps translation metadata for both actor-backed and query-backed results.
- [ ] Full save-time normalization still needs to be tightened so new/source items consistently get default culture and self-owned translation group IDs at the API boundary.

### 8a.1 — Schema / Validation Changes
- Update Marten schema for `ContentItem` to index `TranslationGroupId`, `Culture`, and the selected uniqueness invariant.
- Recommended pre-production schema:
  - `.Index(x => x.SiteId)`
  - `.Index(x => x.ContentTypeAlias)`
  - `.Index(x => x.TranslationGroupId)`
  - `.Index(x => x.Culture)`
  - unique index for `(SiteId, ContentTypeAlias, Culture, Slug)`
- Update `UniqueSlugValidator` and `MartenContentService.GetBySlugAsync(...)` so translation variants do not falsely collide with the source item.
- Add query methods for:
  - list variants by translation group
  - find by site/type/culture/slug
  - bulk publish/unpublish/delete by translation group

### 8b — ContentItemCultureForker

Model after `PageCultureForker.cs`. Content Items are simpler than Pages — no blocks, no parent hierarchy, no layout regions. The `Dictionary<string, JsonElement>` is a pure key-value clone:

```csharp
public static class ContentItemCultureForker
{
    public static ContentItem Fork(ContentItem source, long targetId, string targetCulture, string targetSlug)
    {
        return new ContentItem
        {
            Id = targetId,
            SiteId = source.SiteId,
            TranslationGroupId = source.TranslationGroupId ?? source.Id,
            SourceItemId = source.Id,
            ContentTypeAlias = source.ContentTypeAlias,
            Culture = targetCulture,
            Slug = targetSlug,
            Title = source.Title,
            Fields = source.Fields.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.DeepClone()),
            PublicationState = ContentPublicationState.Draft,
            VersionNumber = 1
        };
    }
}
```

### 8c — IFieldHintResolver

Since Content Items use dynamic fields (`Dictionary<string, JsonElement>`), the AI translator needs context about what each field means:

```csharp
public interface IFieldHintResolver
{
    ContentFieldHint ResolveHint(ContentFieldDefinition fieldDef);
}
```

Implementation maps `fieldDef.Name` + `fieldDef.FieldType` to hints:

| Field Type | Hint | Behavior |
|---|---|---|
| `text` | `DynamicText` | Translate as generic text |
| `richtext` | `DynamicRichText` | Translate preserving Markdown/HTML |
| `markdown` | `DynamicRichText` | Translate preserving Markdown |
| `image` | _skip_ | Not translatable |
| `url` | _skip_ | Not translatable |
| `number` | _skip_ | Not translatable |
| `boolean` | _skip_ | Not translatable |
| `date` | _skip_ | Not translatable |
| `reference` | _skip_ | Not translatable |

The field's `Label` property is passed alongside the hint so the AI can infer semantic meaning (e.g., `"field 'bio' with hint DynamicText and label 'Biography'"`).

### 8d — API Endpoints

Add to `src/Aero.Cms.Modules.Content/Areas/Api/v1/ContentItemsApi.cs`:

```
GET    /{alias}/{id:long}/translations                   → List culture variants
POST   /{alias}/{id:long}/translations                   → Fork item for culture
POST   /{alias}/{id:long}/ai-translate                   → AI translate targets
DELETE /translation-groups/{translationGroupId:long}      → Delete translation group
PUT    /translation-groups/{translationGroupId:long}/publish   → Publish all variants
PUT    /translation-groups/{translationGroupId:long}/unpublish → Unpublish all variants
```

Bulk operations must be scoped by current `SiteId` to avoid acting across tenants. Translation group IDs are Snowflake `long` values and should be treated as site-owned identifiers.

**Implementation checkpoint**:
- [x] `GET /{alias}/{id:long}/translations` lists culture variants.
- [x] `POST /{alias}/{id:long}/translations` forks a draft variant into the requested culture.
- [x] `IContentItemsHttpClient.GetTranslationsAsync(...)` and `ForkToCultureAsync(...)` are wired.
- [ ] AI translate endpoint is still pending.
- [ ] Translation-group delete/publish/unpublish endpoints are still pending.

### 8e — AI Translation Flow

1. **BuildTranslatableFields(source, contentTypeDef)**:
   - Title → `ContentFieldHint.Title`
   - Slug → `ContentFieldHint.Slug`
   - Each dynamic field matching a `text`/`richtext`/`markdown` field type → `DynamicText`/`DynamicRichText` with field label

2. **ApplyTranslatedFields(target, response, contentTypeDef)**:
   - Write translated title
   - For each translated dynamic field, write back as `JsonSerializer.SerializeToElement(stringValue)`
   - Skip null/empty translations

### 8f — Orleans Grain Changes

Add to `IAeroContentItemActor` (`src/Aero.Cms.Abstractions/Actors/IAeroCmsActors.cs`):

```csharp
Task<List<ContentItemViewModel>> ListCultureVariantsAsync(long id, CancellationToken ct);
Task<AeroRequestResponse<ContentItemViewModel>> ForkItemForCultureAsync(long id, string culture, string slug, CancellationToken ct);
```

**AI orchestration stays in the API layer** (not in grains) — same pattern as Pages.

### 8g — HTTP Client Changes

Add to `IContentItemsHttpClient` (`src/Aero.Cms.Abstractions/Http/Clients/ContentTypesClient.cs`):

```csharp
Task<Result<IReadOnlyList<ContentItemDetail>, AeroError>> ListCultureVariantsAsync(string alias, long id);
Task<Result<ContentItemDetail, AeroError>> ForkToCultureAsync(string alias, long id, ForkCultureRequest request);
Task<Result<AiTranslateContentItemResult, AeroError>> TranslateWithAiAsync(string alias, long id, AiTranslateRequest request);
Task<Result<int, AeroError>> DeleteTranslationGroupAsync(long translationGroupId);
Task<Result<PublicationBulkResult, AeroError>> PublishTranslationGroupAsync(long translationGroupId);
Task<Result<PublicationBulkResult, AeroError>> UnpublishTranslationGroupAsync(long translationGroupId);
```

Update `ContentItemSummary` and `ContentItemDetail` records to carry:
- `long? TranslationGroupId`
- `string Culture`
- `long? SourceItemId`

If the Entries tab ships before full translations, default `Culture` from the site default culture at the API boundary.

### 8h — UI: Translations Tab in ContentItemEditor

Add a **Translations** tab to `ContentItemEditor.razor`, mirroring `PageEditor.razor`:

```
Tabs: [ Details ] [ Publishing ] [ Translations ]
```

Content:
- **Header**: Current variant culture badge, default culture badge, "X of Y cultures" coverage
- **Localized Versions table** (Radzen DataGrid):
  - Columns: Culture, Title, Status, Slug, Actions (Open / AI Translate / Delete)
- **Add Translation section**:
  - Culture dropdown (only available cultures shown)
  - Slug input — auto-generate `"{slug}-{culture}"` default
  - "Create Translation" button → calls fork endpoint
- **Bulk actions**:
  - AI Translate All (with overwrite toggle)
  - Publish All / Unpublish All

**Implementation checkpoint**:
- [x] Content item editor has a Translations tab.
- [x] Tab shows current variant culture, default culture, coverage, group ID, and variant table.
- [x] Create Translation opens the existing culture dialog, calls the fork endpoint, and navigates to the new draft variant.
- [x] Open/Delete actions are available per variant.
- [ ] AI Translate and bulk publish/unpublish controls are pending.

### 8i — ContentFieldHint Enum Additions

Add to `AiContentTranslationContracts.cs`:

```csharp
DynamicText,
DynamicRichText
```

### 8j — Pre-Production Data Normalization

No production Marten migration script is required for MVP because the project is not in production.

Required normalization:
- On read/API mapping, if `Culture` is null or empty, use the current site's default culture.
- On save, if `TranslationGroupId` is null, set it to the item's own Snowflake `Id`.
- On save, if `Culture` is null or empty, set it to the current site's default culture.
- For local/dev data, a one-time startup repair or manual reset is acceptable, but do not add a formal migration workflow unless production data appears.

### Files

| File | Change |
|---|---|
| `src/Aero.Cms.Abstractions/Content/ContentItem.cs` | Add TranslationGroupId, Culture, SourceItemId |
| `src/Aero.Cms.Abstractions/Models/ContentItemViewModel.cs` | Add TranslationGroupId, Culture, SourceItemId |
| `src/Aero.Cms.Core/Content/Services/IContentQueryService.cs` | Add list variants and entry count query surface |
| `src/Aero.Cms.Core/Content/Services/MartenContentQueryService.cs` | Implement list variants and entry count query surface |
| `src/Aero.Cms.Modules.Content/Grains/AeroContentItemGrain.cs` | Map translation fields between entity and view model |
| New: `src/Aero.Cms.Modules.Content/Domain/ContentItemCultureForker.cs` | Deep-clone forking logic |
| New: `src/Aero.Cms.Modules.Content/Services/IFieldHintResolver.cs` | Map field definitions → ContentFieldHint |
| New: `src/Aero.Cms.Modules.Content/Services/FieldHintResolver.cs` | Implementation of IFieldHintResolver |
| `src/Aero.Cms.Modules.Content/Areas/Api/v1/ContentItemsApi.cs` | Add 6 translation endpoints |
| `src/Aero.Cms.Abstractions/Http/Clients/ContentTypesClient.cs` | Add translation methods to IContentItemsHttpClient |
| `src/Aero.Cms.Abstractions/Ai/AiContentTranslationContracts.cs` | Add DynamicText, DynamicRichText to ContentFieldHint |
| `src/Aero.Cms.Shared/Pages/Manager/ContentTypes/ContentItemEditor.razor` + `.razor.cs` | Add "Translations" tab with all UI controls |
| `src/Aero.Cms.Modules.Content/AeroContentModule.cs` | Add culture/translation indexes and uniqueness invariant |
| `src/Aero.Cms.Core/Content/Services/AsyncContentValidatorServices.cs` | Update slug uniqueness validation for culture-aware content items |
| `src/Aero.Cms.Core/Content/Services/MartenContentService.cs` | Add culture-aware slug lookup |

### Acceptance Criteria

- [x] Content Items have TranslationGroupId, Culture, SourceItemId
- [x] Fork creates a cloned draft in the target culture
- [ ] AI Translate produces translated title + dynamic fields
- [ ] Field-to-hint mapping correctly skips non-text fields (image, url, number, boolean, date, reference)
- [x] "Translations" tab renders in ContentItemEditor with core UI controls
- [x] Create Translation (fork) works from the UI
- [ ] AI Translate single variant works from the UI
- [ ] AI Translate All (bulk) works from the UI
- [ ] Bulk Publish All / Unpublish All works
- [ ] Delete Translation Group deletes all variants
- [ ] Slug uniqueness is culture-aware and does not block valid translation variants
- [ ] Existing local/dev content items without Culture default correctly through normalization at all save/read boundaries

---

---

## Item 9 — Content Type Entries Tab

**Goal**: Add an **Entries** tab to `ContentTypeEditor` so editors can see and manage entries for the current content type without leaving the schema editor. This complements the existing `/manager/content/{alias}` and `/manager/content/{alias}/editor/{id?}` pages; it does not replace them.

**Current state**:
- Current code already has `ContentItemsList.razor` and `ContentItemEditor.razor` under `src/Aero.Cms.Shared/Pages/Manager/ContentTypes/`.
- Existing docs mention the standalone entry list/editor routes in `docs/content-type-admin-ui.md`, `docs/content-type-implementation.md`, and `docs/aero-content-types.md`.
- The missing UX is an embedded tab inside the content type editor for quick CRUD and status management.

**Design decision**:
- Content type schemas are site-owned and global within that site by default. Do not create separate content type definitions per culture.
- For multilingual sites, localize editor-facing schema metadata where useful: content type display name/description/category and field labels/help text/placeholders. Keep invariant handles stable: `Id`, `SiteId`, `Alias`, field `Name`, field `FieldType`, validation settings, and render mode.
- Content item translations use the same content type schema across cultures. This keeps entries comparable across variants and avoids per-culture schema drift.
- Any persisted localization records must use Snowflake `long` IDs if modeled as database entities. If localization is embedded as a JSON/value object on `ContentTypeDocument`, it does not need its own entity ID.

### 9a — ContentTypeEditor Tab
- Add `Entries` tab after `Fields` or after `Display`.
- Only show the full entries grid when editing an existing content type (`Alias` exists). For new unsaved content types, the tab is hidden until the type has been saved.
- Preserve existing `Basics`, `Fields`, and `Display` behavior.

### 9b — Embedded Entries Grid
- Reuse `IContentItemsHttpClient.GetAllAsync(alias, skip, take, search)` for data loading.
- Use `RadzenDataGrid<ContentItemSummary>`.
- Columns: Entry, URL/embedded status, Culture, Status, Published, Version, Actions.
- Culture column depends on Item 8; if translations are not implemented yet, hide it or show the default culture only.
- Search should call the server-side content items endpoint rather than duplicating filtering in the editor.

### 9c — CRUD Actions
- Create: navigate to `/manager/content/{alias}/editor` or open the existing editor flow in a dialog only if that is simple with current routing.
- Edit: navigate to `/manager/content/{alias}/editor/{id}`.
- Delete: confirmation dialog, then refresh grid.
- Publish/Unpublish: call existing endpoints and refresh grid.
- Keep the standalone content item list route as the power-user/full-page view.

### 9d — Entry Count
- Fix `ContentTypeSummary.ItemCount` so the content type list and Entries tab show real counts instead of placeholder `0`.
- Count should be scoped by `SiteId` and `ContentTypeAlias`.
- Once Item 8 lands, decide whether the count is total variants or primary/default-culture entries. MVP recommendation: show total entries in the grid footer and optionally a culture coverage badge later.

**Implementation checkpoint**:
- [x] Entries tab added to `ContentTypeEditor` for saved content types.
- [x] Embedded grid uses `IContentItemsHttpClient.GetAllAsync(...)` with server-side paging/search.
- [x] Create/Edit route to the existing content item editor pages.
- [x] Delete uses confirmation and reloads the grid.
- [x] Publish/Unpublish actions call existing content item endpoints and reload the grid.
- [x] `ContentTypeSummary.ItemCount` now uses content item counts scoped by site and content type alias.
- [ ] Optional save-first empty state is skipped because the tab is hidden for unsaved content types.

**Files**:
- `src/Aero.Cms.Shared/Pages/Manager/ContentTypes/ContentTypeEditor.razor` + `.razor.cs` — add Entries tab, grid, actions
- `src/Aero.Cms.Shared/Pages/Manager/ContentTypes/ContentItemsList.razor` + `.razor.cs` — left intact as standalone full-page route
- `src/Aero.Cms.Modules.Content/Areas/Api/v1/ContentTypesApi.cs` — ensure `ItemCount` is real
- `src/Aero.Cms.Core/Content/Services/IContentQueryService.cs` + `MartenContentQueryService.cs` — add count query
- `src/Aero.Cms.Abstractions/Http/Clients/ContentTypesClient.cs` — add culture/count DTO fields after Item 8 as needed

**Acceptance criteria**:
- [x] Existing content type editor has an Entries tab for saved content types
- [x] Entries tab lists entries for the current content type with search, paging, and status
- [x] Create/Edit actions route to the existing content item editor
- [x] Delete uses confirmation and refreshes the grid
- [x] Publish/Unpublish actions work from the tab
- [x] Content type list `ItemCount` reflects real data
- [x] New unsaved content types do not expose the Entries tab until the type exists
- [x] Standalone `/manager/content/{alias}` route continues to work

---

---

## Item 10 — Page Reference Picker (All Editors)

**Goal**: Add a shared content/page reference picker that works across page, header, footer, and content item editing flows.

**Scope note**: this is a cross-editor feature with API surface, DTO/client contracts, footer schema changes, and content item reference editing. It is intentionally tracked as its own item rather than hidden under UX polish.

**Current state**: Partially built infrastructure in `PageEditor.razor.cs` and `NavMenuEditor.razor.cs` — data models exist but no UI picker is wired up anywhere.

| Component | What Exists | What's Missing |
|---|---|---|
| `PageEditor.razor.cs` | `SelectedReferenceId` on `EditorBlock`, `ReferenceItem` model, `IBlockEditorCallbacks.GetReferenceItems()` contract, `_referenceData` loads pages/posts/categories/tags | No UI picker calls `GetReferenceItems()`. Nobody uses `SelectedReferenceId`. |
| `NavMenuEditor.razor.cs` | `NavLink.PageId` on `NavMenuSnapshot` and `NavComponentEditorModel` | NavMenuEditor's `RenderHeaderBlockFields` only shows a plain text URL input — no page picker to set `PageId` |
| `FooterEditor.razor.cs` | Nothing | `FooterLink` has no `PageId` at all. Only plain text label/URL. |
| `ContentItemEditor.razor` | Nothing | No page reference field type exists. |

**Work required**:

### 10a — Shared Page Picker Dialog Component
Create a `ContentReferencePicker` Blazor component (Radzen dialog) usable in all editors:
- Search input performing OR search across **Name**, **Title**, and **Slug** (case-insensitive `Contains`)
- Results grouped by type: Pages, Posts, Docs, Categories, Tags
- Radzen DataGrid showing: Type icon, Title, Path/Slug, Culture badge
- Single-select row click → returns selected `ReferenceItem` (Id, Title, Type)
- Server-side search via enhanced `IPagesHttpClient.GetAllAsync(search:)` and equivalent post/doc search endpoints
- Keyboard navigable (search → arrow keys → enter to select)

### 10b — PageEditor: Wire Up Reference Block
- Add "Reference" block type to page editor palette
- Property panel shows a "Select Page" button → opens ContentReferencePicker
- On selection, stores `SelectedReferenceId = $"{type}:{id}"` on the `EditorBlock`
- Renderer outputs the referenced page's title as a link (or full embed for pages)
- Search endpoint supports OR search: `MatchesSearch(search, item) => item.Title.Contains(search) || item.Slug.Contains(search) || item.Name.Contains(search)`

### 10c — NavMenuEditor: Wire Up Page Picker for NavLink
- In `RenderHeaderBlockFields`, replace/hide the plain URL text input when a page is selected
- Add "Link to Page" button → opens ContentReferencePicker → sets `NavComponentEditorModel.PageId`
- Display selected page title as a badge/label in the UI
- If `PageId` is set, URL is auto-resolved from the page's path (renderer already supports this)
- Keep the manual URL input for external links

### 10d — FooterEditor: Add PageId to FooterLink
- Add `PageId` (long?) to `FooterLink` record in `FooterSnapshot.cs`
- Add corresponding field to `FooterComponentEditorModel`
- Add "Link to Page" button in the link editor → opens ContentReferencePicker

### 10e — ContentItemEditor: Add Reference Field Support
- In the content type field editor, a `reference` field type already exists
- For content items, when editing a `reference` field, show a page/post picker button
- Store the reference as `$"{type}:{id}"` in the field value
- Update `ReferenceExistenceValidator` before changing storage format. It currently validates reference fields by parsing a raw `long` content item ID. Either:
  - keep content-type `reference` fields as content-item-only `long` Snowflake IDs for MVP, or
  - introduce typed references (`content-item:{id}`, `page:{id}`, `post:{id}`, etc.) and update validation/rendering/search to understand the typed format.
- Recommendation: keep existing content item reference fields as `long` IDs, and use typed reference strings only for the new cross-content picker DTO until a broader reference model is designed.

### 10f — API: Add Unified Search Endpoint
- New endpoint: `GET /api/admin/references/search?q={query}&types=pages,posts,docs`
- Returns `List<ReferenceSearchResult>` with Id, Type, Title, Slug, Culture
- Searches Pages, Posts, Docs in parallel using existing `GetAllAsync(search:)` patterns
- Results merged, deduplicated, ordered by relevance

**Files**:
| File | Change |
|---|---|
| New: `src/Aero.Cms.Shared/Components/ContentReferencePicker.razor` + `.razor.cs` | Shared page/post/doc search dialog |
| New: `src/Aero.Cms.Abstractions/Models/ReferenceSearchResult.cs` | Search result DTO |
| New: `src/Aero.Cms.Abstractions/Http/Clients/ReferencesClient.cs` | Search API client |
| New: API endpoint in `src/Aero.Cms.Modules.Pages/Areas/Api/v1/` | Unified search endpoint |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/EditorBlockPropertyPanel.razor` | Add "Reference" block editor with picker button |
| `src/Aero.Cms.Shared/Pages/Manager/NavMenuEditor.razor` + `.razor.cs` | Add page picker to NavLink editor |
| `src/Aero.Cms.Modules.Navigation/Domain/NavMenuSnapshot.cs` | (PageId already exists) |
| `src/Aero.Cms.Modules.Footer/Domain/FooterSnapshot.cs` | Add PageId to FooterLink |
| `src/Aero.Cms.Modules.Footer/Rendering/FooterHtmlRenderer.cs` | Handle PageId in link rendering |
| `src/Aero.Cms.Shared/Pages/Manager/FooterEditor.razor` + `.razor.cs` | Add page picker to FooterLink editor |
| `src/Aero.Cms.Modules.Footer/Serialization/FooterJsonContext.cs` | Register updated FooterLink |
| `src/Aero.Cms.Shared/Pages/Manager/ContentTypes/ContentItemEditor.razor` + `.razor.cs` | Add reference picker for reference-type fields |

**Acceptance criteria**:
- [ ] ContentReferencePicker dialog opens from all 4 editors
- [ ] Search works as OR across Name, Title, Slug
- [ ] Results show Pages, Posts, Docs with type icons
- [ ] PageEditor: Reference block can be added and linked to a page
- [ ] NavMenuEditor: NavLink PageId can be set via picker (URL auto-resolves)
- [ ] FooterEditor: FooterLink PageId can be set via picker
- [ ] ContentItemEditor: Reference-type fields show the picker
- [ ] Server-side unified search endpoint exists
- [ ] Existing manual URL input still works for external links

---

---

## Item 3 — Footer Brand Block

**Goal**: Make company brand information (logo, name, tagline) a movable canvas block instead of root-level settings.

**Current state**: `FooterBrandSettings` exists at root of `FooterSnapshot` — not a canvas block. Set once, can't be placed within row/column layout.

**Implementation-readiness update**: because this project is not in production yet, this does not need a defensive production migration script. Make the model smoother by introducing the block and keeping root-level `FooterBrandSettings` as a fallback during the transition. Existing drafts can be normalized on save/load and eventually the root brand can be removed in a later cleanup if desired.

**Work required**:

### 3a — New Block Type
`FooterBrand : IFooterComponent` with:
- `CompanyName` (string)
- `LogoUrl` (string?)
- `LogoAltText` (string?)
- `Tagline` (string?)

### 3b — Editor Support
- Add `brand` to palette in `FooterEditor.razor`
- Editor UI for logo URL, alt text, company name, tagline

### 3c — Renderer
- Render logo `<img>` with alt text
- Render company name as heading
- Render tagline as subtitle

### 3d — Transition / Normalization
- No production Marten migration script is required.
- Keep `FooterSnapshot.Brand` readable as a fallback so old drafts continue to render.
- On load/save in `FooterService`, if no `FooterBrand` component exists and root `FooterBrandSettings` has meaningful values, normalize it into a `FooterBrand` component in the first available row/column.
- Renderer should prefer the movable `FooterBrand` component when present; otherwise render root `FooterBrandSettings` fallback.
- After MVP stabilizes, root `FooterBrandSettings` can be removed or left as backward-compatible fallback.

**Files**:
- `src/Aero.Cms.Modules.Footer/Domain/FooterSnapshot.cs` — add `FooterBrand` record
- `src/Aero.Cms.Modules.Footer/Rendering/FooterHtmlRenderer.cs` — add render case
- `src/Aero.Cms.Modules.Footer/Serialization/FooterJsonContext.cs` — register for serialization
- `src/Aero.Cms.Shared/Pages/Manager/FooterEditor.razor` + `.razor.cs` — palette entry + editor UI

**Acceptance criteria**:
- [ ] Brand block can be dragged from palette onto canvas
- [ ] Logo, company name, tagline are editable in property panel
- [ ] Existing draft data with root-level brand renders correctly through fallback/normalization
- [ ] Brand block renders correct HTML in public view

---

---

## Item 4 — Social Media Block

**Goal**: Social links block for both footer and header with display orientation and proper SVG icons.

### 4a — Footer: Display Orientation
- Add `SocialDisplayOrientation` enum: `Horizontal`, `Vertical`
- Add property to `FooterSocialLinks` record
- Editor UI: dropdown in property panel
- Renderer: flex row vs flex column

### 4b — Header: New NavSocialLinks Block
- `NavSocialLinks : INavMenuComponent`
- Fields: `List<SocialLink> Links`, `DisplayOrientation`
- `SocialLink` record: `Platform` (string), `Url` (string), `IconName` (string)

### 4c — SVG Icons
- List of supported platforms:
  - Facebook, Twitter/X, LinkedIn, Instagram, YouTube, TikTok, Snapchat, Pinterest, Reddit, GitHub, Discord, Telegram, WhatsApp, Threads, Bluesky, Mastodon
- Static `SocialIconRenderer` that outputs inline SVG strings (works in both Blazor and cshtml).
- First verify that NeoUI/Lucide contains actual brand icons for the supported platform list. Lucide is primarily a UI icon set, so it may not cover every social brand.
- If NeoUI does not contain enough brand icons, use a small local brand SVG dictionary for the MVP-supported platforms and keep the helper API the same.
- Do not depend on JavaScript icon libraries or npm.

### 4d — Editor UI
- Footer: extend existing social link editor with orientation dropdown
- Header: new social section in property panel (mirror footer pattern)
- Icon preview in editor

### 4e — Renderer
- Footer renderer (`FooterHtmlRenderer.cs`): update to use orientation + SVG icons
- Nav renderer (`NavMenuHtmlRenderer.cs`): add `NavSocialLinks` render case

**Files**:
- `src/Aero.Cms.Modules.Navigation/Domain/NavMenuSnapshot.cs` — add `NavSocialLinks`, `SocialLink`
- `src/Aero.Cms.Modules.Navigation/Rendering/NavMenuHtmlRenderer.cs` — add render case
- `src/Aero.Cms.Modules.Navigation/Serialization/NavMenuJsonContext.cs` — register types
- `src/Aero.Cms.Modules.Footer/Domain/FooterSnapshot.cs` — add orientation enum/property
- `src/Aero.Cms.Modules.Footer/Rendering/FooterHtmlRenderer.cs` — update render with orientation + SVGs
- `src/Aero.Cms.Shared/Pages/Manager/FooterEditor.razor` + `.razor.cs` — orientation UI
- `src/Aero.Cms.Shared/Pages/Manager/NavMenuEditor.razor` + `.razor.cs` — social link editor UI
- New: `src/Aero.Cms.Shared/Components/SocialIconRenderer.cs` — static SVG helper

**Acceptance criteria**:
- [ ] Footer social block has vertical/horizontal orientation option
- [ ] Header has social links block type
- [ ] MVP-supported platforms render with verified SVG icons
- [ ] Icons render in both Blazor editor preview and cshtml public view
- [ ] Old footer data without orientation property defaults to horizontal

---

---

## Item 1 — Mega Menu (Structured Data)

**Goal**: New `NavMegaMenu` block type replaces `NavHtml` raw HTML with structured data — columns, links, images, CTAs. Both coexist: `NavHtml` remains for quick custom HTML.

**Implementation-readiness notes**:
- This is additive, but every new polymorphic navigation component must be registered end-to-end: `JsonDerivedType`, `NavMenuJsonContext`, service mapping, editor model mapping, validation, culture forking, renderer visitor, and API DTO/detail mapping.
- Keyboard navigation support is broader than HTML markup. For MVP, define whether the target is accessible semantic markup plus hover/click behavior, or full keyboard interaction with TypeScript. If full keyboard behavior is required, implement it in the existing TypeScript/MSBuild pipeline without npm.
- Use `long` Snowflake IDs only for persisted database entities. Nested mega menu columns/links/CTAs can use stable string keys or generated component keys unless they become separate persisted entities.

### 1a — Data Model
```
NavMegaMenu : INavMenuComponent
├── Label (string) — menu trigger label
├── Columns (List<MegaMenuColumn>)
│   ├── Title (string?)
│   ├── Width (enum: Auto, OneQuarter, OneThird, Half, TwoThirds, Full)
│   ├── Links (List<MegaMenuLink>)
│   │   ├── Label (string)
│   │   ├── Href (string)
│   │   ├── Description (string?) — subtitle text
│   │   ├── IconName (string?) — optional Lucide icon
│   │   ├── Badge (string?) — optional badge text (e.g. "New", "Popular")
│   │   ├── OpenInNewTab (bool)
│   │   └── IsExternal (bool)
│   ├── FeaturedImage (MegaMenuImage?)
│   │   ├── Src (string)
│   │   ├── Alt (string)
│   │   └── LinkHref (string?)
│   └── Cta (MegaMenuCta?)
│       ├── Label (string)
│       ├── Href (string)
│       └── ButtonStyle (enum: Primary, Secondary, Outline)
├── BottomCta (MegaMenuCta?) — full-width CTA bar at bottom
└── Layout (enum: ColumnsEqual, ColumnsNarrowWide, Grid)
```

### 1b — Editor UI
- When block type is "megamenu", show structured editor:
  - Add/remove/reorder columns
  - Per-column: title, width selector, links editor, featured image, CTA
  - Link editor: label, href, description, icon picker, badge, external/new tab toggles
  - Bottom CTA section
  - Visual preview of mega menu layout

### 1c — Renderer
- `NavMenuHtmlRenderer.cs`: add `NavMegaMenu` render case
- Output modern mega menu HTML with proper ARIA roles:
  - `role="navigation"`, `aria-label`, `aria-haspopup="true"`
  - Keyboard navigation support
  - Responsive: columns stack on mobile

If full keyboard behavior is included in MVP, add a small TypeScript controller for open/close, escape, focus movement, and outside click handling. If not, adjust acceptance to "semantic markup with responsive behavior" and schedule richer keyboard support as a follow-up.

### 1d — Serialization / Compatibility
- No migration/upcaster needed for `NavHtml` because it coexists with `NavMegaMenu`
- New `NavMegaMenu` type is additive
- Register the new component and nested value objects in the navigation JSON context/source-generated serialization path

**Files**:
- `src/Aero.Cms.Modules.Navigation/Domain/NavMenuSnapshot.cs` — add new types
- `src/Aero.Cms.Modules.Navigation/Rendering/NavMenuHtmlRenderer.cs` — add full mega menu render
- `src/Aero.Cms.Modules.Navigation/Serialization/NavMenuJsonContext.cs` — register types
- `src/Aero.Cms.Shared/Pages/Manager/NavMenuEditor.razor` + `.razor.cs` — mega menu editor UI

**Acceptance criteria**:
- [ ] Mega menu block type available in palette (alongside existing NavHtml)
- [ ] Columns can be added/removed/reordered
- [ ] Links within columns support label, href, description, icon, badge
- [ ] Featured image and CTA per column
- [ ] Bottom CTA bar configurable
- [ ] Renders with proper ARIA accessibility attributes
- [ ] Responsive: columns stack on mobile
- [ ] Existing NavHtml menus continue to work unchanged

---

---

## Item 7 — Editor UX Polish (Full Pass)

**Goal**: Polish all three editors (header, footer, page) — fix remaining bugs, refine drag-and-drop, align responsive previews, improve property panels.

**Scope boundary**:

### 7a — Bug Fixes from Feature Fallout
- Fix any regressions introduced by new block types
- Ensure delete, reorder, and drag-from-palette work for all new block types

### 7b — Drag-and-Drop UX
- Visual drag feedback (ghost, placeholder)
- Smooth animations on reorder
- Drop target highlighting
- Scroll-while-dragging support

### 7c — Property Panel Refinement
- Consistent layout across all block types
- Better grouping of related properties
- Inline previews where applicable (color swatches, icon previews)
- Keyboard navigation and tab order

### 7d — Responsive Preview Alignment
- All three editors: desktop/tablet/mobile previews render correctly
- Preview frame sizing matches device targets (Desktop: 100%, Tablet: 768px, Mobile: 375px)
- Content inside preview respects the viewport

### 7e — Visual Consistency
- Align color scheme across all editors
- Consistent padding, spacing, typography
- Loading states and transitions

### 7f — Remove Redundant Culture Bar from Page Body

The `_CmsLayout.cshtml` renders a standalone culture selection bar above the page body. The NavMenu/Header already has its own language selector (`NavLanguageSelect` block type), making this redundant and visually cluttered.

- **File**: `src/Aero.Cms.Web/Views/Shared/_CmsLayout.cshtml:60-69`
- **Change**: Remove the culture switcher block (lines 60-69), which renders the `CultureSwitcher` component as a bar above `@RenderBody()`
- **Preserve**: `CultureSwitcher` component itself — still used by NavMenu/Header's `NavLanguageSelect` block type
- **Acceptance**: Page body no longer shows the duplicate culture bar; NavMenu language selector still works

**Item-wide polish files**:
- `src/Aero.Cms.Shared/Pages/Manager/NavMenuEditor.razor` + `.razor.cs`
- `src/Aero.Cms.Shared/Pages/Manager/FooterEditor.razor` + `.razor.cs`
- `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor` + `.razor.cs`
- `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditorCanvas.razor`
- `src/Aero.Cms.Shared/Pages/Manager/PageEditor/EditorBlockPropertyPanel.razor`
- `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditorHeader.razor` + `.razor.cs`
- `src/Aero.Cms.Shared/Components/PreviewOverlay.razor`
- `src/Aero.Cms.Shared/wwwroot/aero-manager.css`

**Acceptance criteria**:
- [ ] All block types support smooth drag-and-drop reorder
- [ ] Drag-from-palette works for all block types
- [ ] Property panels are consistent and keyboard-accessible
- [ ] Desktop, tablet, mobile previews render correctly in all editors
- [ ] Redundant culture bar is removed from page body
- [ ] No regressions in existing functionality

---

## Technical Notes

### Pre-Production Compatibility Strategy
This project is not in production yet, so prefer simple model/schema updates plus
load/save normalization over formal migration scripts or event upcasters.

- Footer brand: keep root `FooterBrandSettings` as a fallback while adding the movable `FooterBrand` component.
- Content item translations: normalize missing `Culture` and `TranslationGroupId` at the API/service boundary.
- New polymorphic block/menu component types: register the new types in source-generated JSON contexts and service mapping paths.
- Persisted database entities must use Snowflake `long` IDs. Nested value objects can use stable string keys unless they become independently persisted entities.

### Static SVG Icons (Item 4)
NeoUI's Lucide/Heroicons are Blazor components. For cshtml public pages, create a static helper. Verify brand-icon coverage first; if NeoUI does not contain the needed brand paths, use a small local static SVG dictionary behind the same helper API.

```csharp
public static class SocialIconRenderer
{
    public static string Render(string platform, int size = 20, string? className = null)
    {
        // Return inline SVG string from Lucide icon data dictionary
        // Works in both Blazor and cshtml
    }
}
```

### Analytics Script Requirements (Item 6)
Use `ctx7` to fetch exact script snippets for each provider:
- Google Analytics 4 (gtag.js) — script in `<head>`
- Microsoft Clarity — script in `<head>`
- Facebook Pixel — script in `<head>`
- TikTok Pixel — script in `<head>`
- Posthog — script in `<head>` with optional `<body>` snippet

### Radzen Grid SEO List Page (Item 6)
```
Provider        | Tracking ID        | Status     | Actions
────────────────┼───────────────────┼────────────┼─────────────
Google Analytics│ G-XXXXXXXXXX      │ ✅ Enabled │ [Edit] [Toggle]
Facebook Pixel  │ 1234567890        │ ❌ Disabled│ [Edit] [Toggle]
Microsoft Clarity│ abcdef1234       │ ✅ Enabled │ [Edit] [Toggle]
Custom Script   │ My Custom Script  │ ✅ Enabled │ [Edit] [Toggle]
```

---

## Architecture Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Mega menu | Coexists with NavHtml | No breaking changes for existing content |
| SEO scripts | Extend Analytics/SEO module with explicit renderer | Existing snippets are useful, but layout injection needs a direct bridge |
| Social icons | Verified SVG dictionary + static helper | Works in both Blazor and cshtml contexts without npm |
| Compatibility | Pre-production normalization | Avoids migration/upcaster complexity until production data exists |
| Brand block | New `IFooterComponent` type plus root fallback | Follows existing pattern while keeping old drafts readable |
| Content Item i18n | Mirror Pages pattern with culture-aware slug uniqueness | Proven pattern, but content items need schema/validation updates |
| Content Type localization | Site-global schema, localized metadata later | Same schema across cultures; invariant aliases/field names stay stable |
| AI field mapping | `IFieldHintResolver` + DynamicText/DynamicRichText hints | Dynamic fields need context for quality AI translation |
| Translation orchestration | API layer, not grains | Grains do state only; AI calls stay in API same as Pages |
| Entries tab | Additive tab in ContentTypeEditor | Reuses existing content item CRUD routes and clients |
| UX Polish | Final pass after feature slices | Prevents rework on new block types and reference picker work |
| Page Reference Picker | Standalone Item 10 with shared `ContentReferencePicker` dialog + per-editor wiring | Cross-editor feature, not polish |

---

## Acceptance Criteria Summary

- [ ] **Item 2**: Delete works in header editor (column + block)
- [ ] **Item 5**: Tablet preview renders correctly at 768px
- [ ] **Item 6**: SEO list/detail pages work, scripts inject in correct layout positions
- [ ] **Item 8**: Content Items support culture forking, AI translation, and translations tab UI
- [x] **Item 9**: ContentTypeEditor has an Entries tab with CRUD/status actions
- [ ] **Item 3**: Brand block is movable canvas block, old root brand data renders through fallback/normalization
- [ ] **Item 4**: Social links in both header and footer with proper icons + orientation
- [ ] **Item 1**: Mega menu has structured editor + renders accessible HTML
- [ ] **Item 10**: Page reference picker works in all 4 editors
- [ ] **Item 7**: Redundant culture bar removed from page body; all editors polished — drag-drop, property panels, responsive previews
- [ ] **All**: No regressions, Lighthouse passes, no console errors

---

## Changelog

| Date | Author | Change |
|------|--------|--------|
| `2026-06-02` | OpenCode | Initial plan — 8 items + progress dashboard + per-item tracking |
| `2026-06-02` | Codex | Added implementation-readiness findings, Entries tab, content localization decisions, culture-aware slug guidance, and pre-production normalization strategy |
| `2026-06-02` | Codex | Promoted Page Reference Picker from Item 7g to standalone Item 10 and made it visible in the priority/dependency chain |
| `2026-06-02` | Codex | Added Item 7f culture bar removal and scoped its implementation file to `_CmsLayout.cshtml` |
| `2026-06-02` | Codex | Collapsed duplicate progress/priority tables into one dashboard and reordered item sections to match implementation priority |
| `2026-06-02` | Codex | Implemented SEO provider renderer/UI checkpoint and marked custom raw script entity work as deferred |
| `2026-06-02` | Codex | Implemented content item translation identity, list/fork endpoints/client methods, and editor Translations tab; AI/bulk operations remain pending |
| `2026-06-02` | Codex | Implemented ContentTypeEditor Entries tab with search, CRUD routing, delete, publish/unpublish, and real content type item counts |
| `2026-06-02` | Codex | Fixed ambiguous `/manager/seo` Blazor routes by moving the old SEO analysis placeholder to `/manager/seo/analysis` |
