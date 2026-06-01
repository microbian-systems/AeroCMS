# Implementation Plan: AI-Powered Translation Service

> **Status:** Draft for review
> **Companion spec:** [`docs/localization-implementation.md`](./localization-implementation.md) — covers the broader multi-culture infrastructure (entity model, middleware, RTL, SEO). This document focuses on the AI translation layer and editor UX that builds on top of that foundation.

---

## 1. Objective

Integrate the existing AI provider infrastructure (OpenAI, Anthropic, Google Gemini, etc.) into the AeroCMS manager portal to **automatically translate content** across all supported content types. Add a per-NavMenu/per-Footer **"Enable Languages" toggle** so site admins control whether the public language switcher appears.

### User Stories

- As a content editor, I can select a target culture and click **"AI Translate"** to fork my page/post/doc/nav/footer to that culture with all translatable fields populated by AI — without leaving the editor.
- As a site admin, I can configure supported languages per site via a proper **add/remove tag-style picker** (not a raw comma-separated text input).
- As a site admin, I can toggle **"Enable Languages"** on each NavMenu and Footer to control whether the public language switcher is shown.
- As a content editor, I can AI-translate category and tag names/descriptions for each supported culture.

### Non-Goals (separate scope)

- AI Enhance panel for PageEditor (deferred — currently only PostEditor has it)
- Bulk/background translation via TickerQ
- Translation memory or glossary management
- Automatic translation on content save

---

## 2. Architecture Overview

### 2.1 Current State

The existing localization infrastructure already provides:

| Capability | Status |
|---|---|
| Document-per-culture with `TranslationGroupId` + `Culture` | ✅ Pages, Posts, Docs, NavMenus, Footers |
| Sidecar translation entities | ✅ CategoryTranslation, TagTranslation |
| Fork endpoints (manual copy to new culture) | ✅ All content types |
| Translation variant switching in editors | ✅ DocsEditor, PostEditor, PageEditor, NavMenuEditor, FooterEditor |
| AI provider infrastructure | ✅ 15 providers, encrypted keys, `IAiContentEnhancementService` |
| Site-level supported cultures | ✅ `SitesModel.SupportedCultures` as `List<string>` |
| Public CultureSwitcher component | ✅ Rendered in `_CmsLayout.cshtml` |
| Language management UI | ⚠️ Comma-separated text input (poor UX) |

### 2.2 What We're Adding

```
Editor ──► "AI Translate" button
                │
                ▼
     ┌─────────────────────────┐
     │  Orchestrator Endpoint  │  ← One per content type
     │  POST /admin/{type}/    │
     │    {id}/ai-translate    │
     └────────┬────────────────┘
              │
       ┌──────┴──────┐
       ▼              ▼
  ForkToCulture()   IAiContentTranslationService
  (existing)         (new, per-document/per-culture batch)
                       │
                       ▼
                 AI Provider
                 (reuses existing
                  IAiChatClientFactory
                  + IAiSettingsProvider)
```

### 2.3 Where Translations Live

Each content module owns its own translation data in its own document type. There is no global translation table.

| Content Type | Document | Module | Key Fields |
|---|---|---|---|
| **Page** | `PageDocument` | `Aero.Cms.Modules.Pages` | `Culture`, `TranslationGroupId`, culture-specific `Slug`, title, SEO fields, layout/blocks |
| **Post** | `PostDocument` | `Aero.Cms.Modules.Posts` | `Culture`, `TranslationGroupId`, culture-specific `Slug`, title, content/excerpt, SEO fields |
| **Doc** | `DocsPage` | `Aero.Cms.Modules.Docs` | `Culture`, `TranslationGroupId`, culture-specific `Slug`, markdown content, title, SEO fields, parent/tree info |
| **NavMenu** | `NavMenuDocument` | `Aero.Cms.Modules.Navigation` | `Culture`, `TranslationGroupId`, published snapshot |
| **Footer** | `FooterDocument` | `Aero.Cms.Modules.Footer` | `Culture`, `TranslationGroupId`, published snapshot |

Each translated variant is a full document in its own right (same document type, same `TranslationGroupId`, different `Culture`). The module's service layer resolves the correct variant at query time, per `(SiteId, Culture, Slug)`.

Sidecar translations for simpler entities follow the same module-owned pattern:

| Entity | Document | Module | Key Fields |
|---|---|---|---|
| **Category translation** | `CategoryTranslation` | `Aero.Cms.Modules.Posts` | `CategoryId`, `Culture`, name, slug, description |
| **Tag translation** | `TagTranslation` | `Aero.Cms.Modules.Posts` | `TagId`, `Culture`, name, description |

### 2.4 Content Types & Translatable Fields

| Type | Translatable Fields | Strategy |
|---|---|---|
| **Page** | Title, Summary, SeoTitle, SeoDescription, block content text | Document-per-culture fork + translate; block graph handled by `IPageBlockTranslator` |
| **Post** | Title, Excerpt, SeoTitle, SeoDescription, markdown content | Document-per-culture fork + translate |
| **Doc** | Title, MarkdownContent, Summary, SeoTitle, SeoDescription | Document-per-culture fork + translate |
| **NavMenu** | Name, each item's Label, AltText | Document-per-culture fork + translate |
| **Footer** | CompanyName, Tagline, CopyrightText, group names, link labels/alt text | Document-per-culture fork + translate |
| **Category** | Name, Description | Sidecar translation create/update |
| **Tag** | Name, Description | Sidecar translation create/update |

### 2.5 "Enable Languages" Toggle

Each NavMenuDocument and FooterDocument gets a `ShowLanguageSelector` bool property. When enabled:

1. The editor checkbox persists the flag
2. The public NavBar/Footer ViewComponents set a `ViewBag.ShowLanguageSelector`
3. The `_CmsLayout.cshtml` conditionally renders `CultureSwitcher` based on the flag

The culture switcher links themselves are always derived from the site's `SupportedCultures` — the toggle just controls **visibility**.

### 2.6 Architecture Boundary Rules

Keep this feature aligned with SRP and vertical slice architecture:

- `Aero.Cms.Modules.Ai` owns reusable AI translation primitives only: contracts, prompt building, provider resolution, chat client execution, output parsing, validation, and provider-level error handling.
- `Aero.Cms.Modules.Pages`, `Aero.Cms.Modules.Posts`, `Aero.Cms.Modules.Docs`, `Aero.Cms.Modules.Navigation`, and `Aero.Cms.Modules.Footer` own their own translation workflows end-to-end: loading source content, validating site/culture rules, detecting slug conflicts, forking variants, applying translated fields, saving drafts, and returning navigation targets.
- Do not create a central "translate any CMS entity" orchestrator in the AI module. That would couple unrelated module persistence rules and weaken vertical slices.
- Shared AI services may be reused by all modules through abstractions, but content-specific translation/application logic stays inside the owning module.
- Posts already has the base translation workflow (`ListCultureVariantsAsync`, `ForkToCultureAsync`, `PostCultureForker`, and PostEditor translation UI). AI translation for Posts extends that slice rather than replacing or redesigning it.
- Docs and Pages should follow their own existing fork/list endpoints and service patterns, not call into Posts translation code.

---

## 3. Design Decisions

| Decision | Rationale |
|---|---|
| **One AI call per target culture** | Each content-type orchestrator sends all fields for one target culture in one request. This keeps voice, terminology, and SEO wording coherent across fields while avoiding the latency of sequential field-by-field calls. |
| **No multi-language prompt batching** | A single LLM call should never ask for multiple target cultures. If a future workflow translates into multiple cultures at once, dispatch one request per target culture with `Task.WhenAll` and wrap each language result independently. |
| **Separate orchestrator endpoints per content type** | Each type has unique fields, fork logic, and persistence patterns. A single generic endpoint would be too coupled. |
| **Sync translation for one target culture** (not async/background) | Editors translate one target culture at a time and expect immediate navigation to the translated variant. Bulk/background translation via TickerQ remains a future enhancement. |
| **Reuse existing AI infrastructure** | `IAiChatClientFactory`, `IAiSettingsProvider`, encrypted key storage, FluentValidation — all already proven in `AiContentEnhancementService`. |
| **Keep `SupportedCultures` as `List<string>`** | Display names derived from `CultureInfo.GetCultureInfo()`. No schema migration needed. |
| **`ShowLanguageSelector` on document, not snapshot** | Simpler persistence; Marten handles new fields on existing documents without explicit migration. |
| **Typed field hints with stable field keys** | `ContentFieldHint` avoids stringly typed prompt behavior, while stable keys such as `title`, `items[main].label`, or `blocks[hero-1].title` allow repeated/nested fields to round-trip safely. |
| **Translate before committing a new variant** | The default AI translate flow should avoid creating half-translated variants. Validate target culture/slug first, translate fields, then fork/save through the owning module. Only create a partial draft if the UI explicitly offers that recovery path. |
| **Chunk only when token budget requires it** | One request per target culture is the default. If the field payload is too large for the configured provider, split deterministically by field group or block group while preserving stable field keys. |

---

## 4. GoF & SOLID Patterns Applied

| Pattern | Where |
|---|---|
| **Strategy** | `IAiContentTranslationService` translates a field batch for one target culture; each module-owned orchestrator is a strategy for its content type |
| **Facade** | Orchestrator endpoints wrap `fork + translate fields + update` into one operation |
| **Factory** | `IAiChatClientFactory` creates the right `IChatClient` per provider |
| **Template Method** | All orchestrators follow: load → fork → translate fields → save → return |
| **Single Responsibility** | AI service translates text; orchestrators handle content-type-specific logic; editors handle UX |
| **Open/Closed** | Add new content types by adding an orchestrator — no changes to the AI translation service |

Use these patterns lightly. They describe the shape of the solution; they should not introduce shared base classes or generic orchestrators unless the concrete modules have already proven the duplication is harmful.

---

## 5. Detailed Task Breakdown

### Phase 1: Foundation — AI Translation Service

#### Task 1.1 — Translation Contracts

**New file:** `src/Aero.Cms.Abstractions/Ai/AiContentTranslationContracts.cs`

```csharp
namespace Aero.Cms.Abstractions.Ai;

public enum ContentFieldHint
{
    Title,
    Summary,
    Excerpt,
    SeoTitle,
    SeoDescription,
    MarkdownContent,
    Label,
    AltText,
    CompanyName,
    Tagline,
    CopyrightText,
    GroupName,
    CategoryName,
    CategoryDescription,
    TagName,
    TagDescription,
    BlockText,
    BlockCaption,
    BlockPlaceholder
}

public static class ContentFieldHintExtensions
{
    public static bool IsMarkdown(this ContentFieldHint hint) =>
        hint is ContentFieldHint.MarkdownContent;
}

public sealed record TranslateDocumentField(
    string Key,              // stable round-trip key: "title", "items[main].label", "blocks[hero-1].title"
    ContentFieldHint Hint,
    string SourceText);

public sealed record TranslateDocumentRequest(
    IReadOnlyList<TranslateDocumentField> Fields,
    string SourceCulture,
    string TargetCulture,
    string? ProviderId = null
);

public sealed record TranslateDocumentResponse(
    IReadOnlyDictionary<string, string> TranslatedFields, // same keys as input
    IReadOnlyList<string> Warnings,
    string Provider,
    string Model
);
```

Follows the same record pattern as `EnhanceContentRequest` / `EnhanceContentResponse`, but the request is document-shaped instead of single-field-shaped. `ContentFieldHint` is used for prompt rules and markdown detection; `Key` is used for safe rehydration, including repeated labels and nested block fields.

#### Task 1.2 — Translation Prompt Builder

**New files:**
- `src/Aero.Cms.Modules.Ai/Services/ITranslateDocumentPromptBuilder.cs`
- `src/Aero.Cms.Modules.Ai/Services/TranslateDocumentPromptBuilder.cs`

System prompt specialized for translation:

```
You are a professional translator specializing in website content localization.
Translate the following CMS content fields from {SourceCulture} to {TargetCulture}.

Rules:
- Return only a JSON object with the same keys and translated values: { "fields": { ... }, "warnings": [] }
- Do not include markdown fences, comments, or preamble
- Preserve ALL markdown structure, code blocks, links, HTML tags, and front matter for markdown fields
- Do NOT translate brand names, technical terms, code, or proper nouns
- Maintain the original tone (professional, conversational, technical)
- For fields with hint=Title: keep them concise and under 60 characters
- For SEO fields: preserve keyword intent and adapt for the target locale

Input fields:
{fieldsAsJson}
```

#### Task 1.3 — Output Parser

**New file:** `src/Aero.Cms.Modules.Ai/Services/TranslateDocumentAgentOutputParser.cs`

Parses `{ fields: { ... }, warnings: [] }` from LLM JSON response — mirrors `EnhanceContentAgentOutputParser`.

Parser requirements:

- Validate that every input `Key` appears in the response.
- Preserve partial output when some keys are missing, but add warnings for missing keys.
- Treat malformed JSON as a failed result.
- Treat `FinishReason == Length` as a failed result for that target culture unless the parser can prove every requested field is complete.

#### Task 1.4 — Translation Service

**New files:**
- `src/Aero.Cms.Modules.Ai/Services/IAiContentTranslationService.cs`
- `src/Aero.Cms.Modules.Ai/Services/AiContentTranslationService.cs`

Pipeline (identical pattern to `AiContentEnhancementService`):

```
1. Validate request (FluentValidation)
2. Resolve AI runtime settings via IAiSettingsProvider
3. Create IChatClient via IAiChatClientFactory
4. Build system + user messages (instructions + prompt)
5. Call LLM with timeout and truncation detection
6. Parse JSON response
7. Validate response key coverage and return `Result<TranslateDocumentResponse>`
```

The translation service translates one target culture per request. If a future workflow accepts multiple target cultures, concurrency belongs in the content-type orchestrator:

```csharp
var translationTasks = targetCultures.Select(culture =>
    TranslateOneCultureAsync(source, culture, ct)); // each task returns Result<T>

var results = await Task.WhenAll(translationTasks);
```

Each target culture must return its own `Result<T>`. Do not use a bare `Task.WhenAll` over tasks that throw, because one failed provider call should not cancel successful languages.

#### Task 1.5 — Validation + DI Registration

**New file:** `src/Aero.Cms.Modules.Ai/Validation/TranslateDocumentRequestValidator.cs`

**Modified:** `src/Aero.Cms.Modules.Ai/AiModule.cs` — register:
```csharp
services.AddScoped<IAiContentTranslationService, AiContentTranslationService>();
services.AddScoped<ITranslateDocumentPromptBuilder, TranslateDocumentPromptBuilder>();
services.AddScoped<IValidator<TranslateDocumentRequest>, TranslateDocumentRequestValidator>();
```

#### Task 1.6 — Translation API Endpoint

**Modified:** `src/Aero.Cms.Modules.Ai/Api/AiApi.cs`

Add endpoint:

```
POST /api/v1/admin/ai/content/translate
Body: TranslateDocumentRequest
Returns: TranslateDocumentResponse
```

Same Railway-Oriented result matching, logging, and error handling as the existing `/content/enhance` endpoint.

---

### Phase 2: Content-Type Translation Orchestrators

Each orchestrator is a minimal API endpoint in its respective module that:

1. Loads the source document
2. Validates the target culture against both `CultureInfo.GetCultureInfo()` and the current site's `SupportedCultures`
3. Validates target slug/key conflicts before doing AI work
4. Builds a `TranslateDocumentRequest` for the target culture using stable keys and `ContentFieldHint`
5. Calls `IAiContentTranslationService.TranslateDocumentAsync()`
6. Calls the existing fork/save workflow to create the target-culture variant
7. Updates the forked document with translated values by matching response keys back to fields
8. Returns `{ Id, Culture, Title }` of the new variant

All orchestrators return Railway `Result<T, AeroError>`.

Provider timeouts, rate limits, and other provider failures must be caught, logged, and returned as failed `Result<T>` values with clear `AeroError` messages. Do not let provider exceptions escape the endpoint.

Atomicity rule: the default flow should not persist a new culture variant if AI translation fails. If the owning module must fork first because of existing service constraints, it must either cleanly abandon the draft on failure or return a clearly marked draft-recovery response that the editor can show to the user.

Provider selection rule: each AI translate endpoint should accept an optional `ProviderId`, default to the saved AI provider when omitted, and match the provider-selection behavior already used by the PostEditor AI enhancement panel.

Token budget rule: if the serialized field payload exceeds the configured provider budget, split work deterministically by field group while preserving stable field keys. For Pages, prefer block-group chunking after the page-level fields are translated.

#### Task 2.1 — Page Block Translation Strategy (Blocker)

**New files:**
- `src/Aero.Cms.Modules.Pages/Translation/IPageBlockTranslator.cs`
- `src/Aero.Cms.Modules.Pages/Translation/PageBlockTranslator.cs`

Page block content is not a flat string. `PageDocument` currently carries editor state as `List<EditorBlock>`, published render state as `List<LayoutRegion>`, and Neo composition data can contain `NeoPageNode.Properties` as `Dictionary<string, JsonElement>`. Do not send the raw block graph to the LLM.

The block translator must:

1. Deep-clone the source block/editor graph.
2. Traverse known user-facing text fields only.
3. Build stable translation keys for each text value.
4. Delegate translation through `IAiContentTranslationService`.
5. Rehydrate translated values back into the clone by key.

```csharp
namespace Aero.Cms.Modules.Pages.Translation;

public interface IPageBlockTranslator
{
    Task<Result<IReadOnlyList<EditorBlock>, AeroError>> TranslateAsync(
        IReadOnlyList<EditorBlock> sourceBlocks,
        string sourceCulture,
        string targetCulture,
        string? providerId = null,
        CancellationToken ct = default);
}
```

Traversal rules:

- Prefer typed traversal over raw JSON heuristics for `EditorBlock` and nested editor models.
- For `NeoPageNode.Properties`, use a whitelist of translatable property names rather than every string leaf.
- Likely translatable property names: `Title`, `MainText`, `SubText`, `Eyebrow`, `Highlight`, `Content`, `Author`, `Alt`, `Caption`, `Description`, `Question`, `Answer`, `Label`, `Text`, `Placeholder`.
- Never translate structural/configuration fields such as `EditorId`, `Type`, `Url`, `CtaUrl`, `Src`, `BackgroundImage`, `Icon`, `Style`, `Id`, numeric values, booleans, or dates.
- Rehydration must apply translated strings into the cloned source structure. Do not ask the LLM to return a replacement block JSON graph.

> **Blocking rule:** Do not start Page AI Translation until the `IPageBlockTranslator` traversal and rehydration strategy has been reviewed against the live block model.

#### Task 2.2 — Page AI Translation

- **Endpoint:** `POST /api/v1/admin/pages/{id}/ai-translate`
- **Module:** `Aero.Cms.Modules.Pages`
- **Body:** `{ TargetCulture: string, TargetSlug: string, ProviderId?: string }`
- **Translatable fields:** Title, Summary, SeoTitle, SeoDescription, plus block text content via `IPageBlockTranslator`
- **Add method:** `AiTranslateAsync(long id, ...)` to `IPagesHttpClient`

#### Task 2.3 — Post AI Translation

- **Endpoint:** `POST /api/v1/admin/posts/{id}/ai-translate`
- **Module:** `Aero.Cms.Modules.Posts`
- **Body:** `{ TargetCulture: string, TargetSlug: string, ProviderId?: string }`
- **Translatable fields:** Title, Excerpt, SeoTitle, SeoDescription, markdown content
- **Add method:** `AiTranslateAsync(long id, ...)` to `IBlogHttpClient`
- **Reuse:** Existing Posts translation slice (`ForkToCultureAsync`, `PostCultureForker`, PostEditor variant UI). Do not introduce a parallel Posts translation workflow.

#### Task 2.4 — Docs AI Translation

- **Endpoint:** `POST /api/v1/admin/docs/{id}/ai-translate`
- **Module:** `Aero.Cms.Modules.Docs`
- **Body:** `{ TargetCulture: string, TargetSlug: string, ProviderId?: string }`
- **Translatable fields:** Title, MarkdownContent, Summary, SeoTitle, SeoDescription
- **Add method:** `AiTranslateAsync(long id, ...)` to `IDocsHttpClient`

#### Task 2.5 — NavMenu AI Translation

- **Endpoint:** `POST /api/v1/admin/navigations/{id}/ai-translate`
- **Module:** `Aero.Cms.Modules.Navigation`
- **Body:** `{ TargetCulture: string, ProviderId?: string }` (nav menus have a single key, no slug)
- **Translatable fields:** Name, Description, each item's Label, AltText
- **Add method:** `AiTranslateAsync(long id, ...)` to `INavigationsHttpClient`

#### Task 2.6 — Footer AI Translation

- **Endpoint:** `POST /api/v1/admin/footers/{id}/ai-translate`
- **Module:** `Aero.Cms.Modules.Footer`
- **Body:** `{ TargetCulture: string, ProviderId?: string }`
- **Translatable fields:** CompanyName, Tagline, CopyrightText, group names, link labels, link alt text
- **Add method:** `AiTranslateAsync(long id, ...)` to `IFootersHttpClient`

#### Task 2.7 — Taxonomy AI Translation

- **Endpoints:**
  - `POST /api/v1/admin/taxonomy/categories/{id}/ai-translate`
  - `POST /api/v1/admin/taxonomy/tags/{id}/ai-translate`
- **Module:** `Aero.Cms.Modules.Posts` (or `Aero.Cms.Modules.Taxonomy`)
- **Body:** `{ TargetCulture: string, ProviderId?: string }`
- **Logic:** Creates or updates the sidecar translation entity (`CategoryTranslation` / `TagTranslation`), falling back to the base entity name if the translation already exists
- **Add method** to respective HTTP clients

---

### Phase 3: Language Management UX

#### Task 3.1 — Improved Culture Editor in Sites.razor

**Modified:** `src/Aero.Cms.Shared/Pages/Manager/Sites.razor`

Replace the comma-separated `<input>` with a tag-style add/remove component:

- Display current supported cultures as removable badges: `[English (en-US) ×] [Spanish (es-MX) ×]`
- "Add Language" opens a searchable picker; do not render the full `CultureInfo.GetCultures(CultureTypes.AllCultures)` list on focus
- Picker shows `DisplayName — code` format, for example `French (France) — fr-FR`
- Filter client-side by display name or code and cap the rendered results, e.g. `Take(30)`, so the UI remains responsive across the full culture list
- Prevents duplicates, validates via `CultureInfo.GetCultureInfo()`, and stores the normalized `CultureInfo.Name`
- Default culture shown with a "(Default)" badge
- Backward compatible: stored as comma-separated codes in the same `List<string>` field

Recommended first implementation:

```razor
<input @bind="cultureSearch" @bind:event="oninput" list="culture-list"
       placeholder="Search languages..." class="..." />

<datalist id="culture-list">
    @foreach (var c in FilteredCultures)
    {
        <option value="@c.Name">@c.DisplayName - @c.Name</option>
    }
</datalist>
```

```csharp
private string cultureSearch = "";

private IEnumerable<CultureInfo> FilteredCultures =>
    string.IsNullOrWhiteSpace(cultureSearch)
        ? []
        : CultureInfo
            .GetCultures(CultureTypes.AllCultures)
            .Where(c =>
                c.Name.Contains(cultureSearch, StringComparison.OrdinalIgnoreCase) ||
                c.DisplayName.Contains(cultureSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.DisplayName)
            .Take(30);
```

This is search-capped rather than true virtualization. If a richer dropdown/list is needed later, replace it with a Blazor `Virtualize<TItem>` or virtualized Radzen component while keeping the same validation and duplicate-prevention behavior. `Virtualize<TItem>` is appropriate only when the UI renders a scrollable list; the capped datalist avoids the full-list render by returning no items until search begins.

#### Task 3.2 — Add "Enable Languages" to NavMenu Editor

**Modified files:**

| File | Change |
|---|---|
| `NavMenuDocument.cs` | Add `bool ShowLanguageSelector { get; set; }` |
| `NavMenuEvents.cs:NavMenuDraftSaved` | Add `bool ShowLanguageSelector` field (nullable for backward compat with existing events) |
| `NavigationsClient.cs:NavigationDetail` | Add `bool ShowLanguageSelector` field |
| `NavigationsClient.cs:UpdateNavigationRequest` | Add `bool ShowLanguageSelector` field |
| `NavMenuEditor.razor.cs` | Add `ShowLanguageSelector` field, wire to save/load |
| `NavMenuEditor.razor` | Add checkbox: `<label><input type="checkbox" @bind="ShowLanguageSelector" /> Enable Languages</label>` |

#### Task 3.3 — Add "Enable Languages" to Footer Editor

**Modified files:**

| File | Change |
|---|---|
| `FooterDocument.cs` | Add `bool ShowLanguageSelector { get; set; }` |
| `FooterEvents.cs:FooterDraftSaved` | Add `bool ShowLanguageSelector` field |
| `FootersClient.cs:FooterDetail` | Add `bool ShowLanguageSelector` field |
| `FootersClient.cs:UpdateFooterRequest` | Add `bool ShowLanguageSelector` field |
| `FooterEditor.razor.cs` | Add `ShowLanguageSelector` field, wire to save/load |
| `FooterEditor.razor` | Add checkbox: `<label><input type="checkbox" @bind="ShowLanguageSelector" /> Enable Languages</label>` |

#### Task 3.4 — Conditionally Render CultureSwitcher in Public Layout

**Modified files:**

| File | Change |
|---|---|
| `AeroNavBarViewComponent.cs` | After resolving NavMenuContext, set `ViewBag.ShowLanguageSelector = context.Snapshot.ShowLanguageSelector` |
| `AeroFooterViewComponent.cs` | After resolving FooterContext, set `ViewBag.ShowLanguageSelector \|\|= context.Snapshot.ShowLanguageSelector` |
| `_CmsLayout.cshtml` | Change `@if (cultureSwitcherLinks.Count > 1)` to `@if (cultureSwitcherLinks.Count > 1 && ViewBag.ShowLanguageSelector == true)` |

Since ViewComponents run before the CultureSwitcher renders, if either the NavMenu or Footer has the toggle enabled, the language switcher will appear.

---

### Phase 4: Editor UI Integration

#### Task 4.1 — "AI Translate" Button in DocsEditor

**Modified:** `DocsEditor.razor` + `DocsEditor.razor.cs`

In the translation section, add an "AI Translate" button beside "Create Translation":

```
[ Target Culture ▼ ] [ Translated Slug  ] [Create Translation] [AI Translate ⚡]
```

Flow:
1. User selects target culture + optional slug
2. Clicks "AI Translate"
3. If content is dirty, save first
4. Call `DocsClient.AiTranslateAsync(Current.Id, targetCulture, slug, providerId)`
5. On success: navigate to `/manager/docs/{SpaceId}/sections/{newId}`
6. On failure: show error toast

State: `IsAiTranslating` (bool), disables both buttons during operation.

Provider selector: default to the saved AI provider and allow override when configured providers are available. Reuse the PostEditor AI provider option pattern.

#### Task 4.2 — "AI Translate" Button in PostEditor

**Modified:** `PostEditor.razor` + `PostEditor.razor.cs`

Same pattern; calls `BlogClient.AiTranslateAsync()`. This extends the existing PostEditor translation section and existing `BlogClient.ForkToCultureAsync()` flow rather than adding a parallel translation UI.

#### Task 4.3 — "AI Translate" Button in PageEditor

**Modified:** `PageEditor.razor` + `PageEditor.razor.cs`

Same pattern; calls `PagesClient.AiTranslateAsync()`.

#### Task 4.4 — "AI Translate" Button in NavMenuEditor

**Modified:** `NavMenuEditor.razor` + `NavMenuEditor.razor.cs`

Same pattern; calls `NavigationsClient.AiTranslateAsync()`.

#### Task 4.5 — "AI Translate" Button in FooterEditor

**Modified:** `FooterEditor.razor` + `FooterEditor.razor.cs`

Same pattern; calls `FootersClient.AiTranslateAsync()`.

---

## 6. File Impact Summary

| Area | New | Modified |
|---|---|---|
| **AI Contracts** — `AiContentTranslationContracts.cs` | 1 | — |
| **AI Service** — interface, impl, prompt builder, parser, validator | 5 | — |
| **Page block translation** — block traversal + rehydration | 2 | — |
| **AI Module** — DI registration + API endpoint | — | 2 (`AiModule.cs`, `AiApi.cs`) |
| **Content Module APIs** — one per content type | 6-7 | — |
| **HTTP Clients** — `AiTranslateAsync()` method per client | — | 5 |
| **Entities + Events** — `ShowLanguageSelector` on NavMenu/Footer docs | — | 4 |
| **Snapshot/Detail records** — propagate `ShowLanguageSelector` | — | 4 |
| **Editor Razor + Code-behind** — "AI Translate" button | — | 5 |
| **Sites.razor** — language picker UX | — | 1 |
| **Public layout + ViewComponents** — conditional CultureSwitcher | — | 3 |
| **Total** | **~14-15** | **~24** |

---

## 7. Testing Strategy

| Test Type | Scope | Tools |
|---|---|---|
| **Unit** | Translation prompt builder, output parser, validator, field hint helpers | TUnit, NSubstitute |
| **Unit** | Page block extraction and rehydration for `EditorBlock` and `NeoPageNode` content | TUnit, NSubstitute |
| **Integration** | AI translation service with mock chat client | TUnit, AutoFixture |
| **Integration** | Orchestrator endpoints (fork + translate flow) | Alba, embedded Postgres |
| **Integration** | Posts AI translation extends the existing Posts translation workflow | Alba, embedded Postgres |
| **GUI** | "AI Translate" button clicks, success/error states, language picker | Playwright |

### Key Test Cases

- Source text with markdown structure is preserved in translation output
- Code blocks and inline code are not translated
- Empty source text returns appropriate error
- AI provider timeout is handled gracefully
- Target culture must be valid in .NET and included in the current site's `SupportedCultures`
- Target slug/key conflicts are detected before provider calls
- Provider selection defaults to saved AI provider and honors an explicit `ProviderId`
- `TranslateDocumentResponse.TranslatedFields` preserves all requested field keys
- Missing response keys produce warnings and preserve partial output
- `FinishReason == Length` fails the target-culture translation unless all fields are proven complete
- Page block translation updates only whitelisted user-facing fields and does not mutate source blocks
- Translation failure does not leave an unmarked half-translated variant behind
- Posts AI translation reuses the existing Posts fork/list culture-variant workflow
- "Enable Languages" checkbox persist through save/load cycle
- CultureSwitcher visibility responds to the toggle in both NavMenu and Footer
- Language picker in Sites.razor prevents duplicates, validates culture codes, and does not render the full culture list before search

---

## 8. Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| **AI translation quality** varies by provider | Content may need manual review | Always navigate to translated variant for review; never auto-publish |
| **Token limits** on large content | Truncated translations | Detect `FinishReason == Length`, fail that target culture unless all fields are complete, and show clear "Increase MaxOutputTokens" guidance |
| **Large payloads exceed provider budget** | Translation cannot fit into one call | Estimate serialized field size and chunk deterministically by field group or block group while preserving stable keys |
| **Markdown corruption** by AI | Broken formatting | Strict system prompt rules; mark markdown fields via `ContentFieldHint.IsMarkdown()` and validate parser output |
| **Block graph corruption** by AI | Broken pages or lost editor state | Never send raw block JSON for replacement; extract whitelisted text, translate by stable key, and rehydrate into a deep clone |
| **Cross-module coupling** | AI module learns Docs/Pages/Posts persistence rules | Keep reusable AI primitives in `Aero.Cms.Modules.Ai`; keep vertical translation workflows in each owning module |
| **Half-created translated variants** | Draft clutter or confusing editor state | Validate culture/slug first, translate before commit when possible, and explicitly mark any recovery draft if a module must fork first |
| **Rate limiting** on AI provider | Slow bulk operations | Sync-only for now; bulk/background via TickerQ is a future enhancement |
| **Culture picker over-rendering** | Slow or unusable site settings UI | Search-capped list initially; use true virtualization if moving beyond the capped datalist approach |
| **API key expiration** | Translation fails silently | Reuse existing `IAiSettingsProvider` health checks from `AiContentEnhancementService` |
| **Marten schema drift** for new `ShowLanguageSelector` field | Missing data | Field defaults to `false`; Marten handles additive schema changes automatically |

---

## 9. Open Questions

1. **PageEditor AI Enhance panel** — Should we add the AI Enhance panel to PageEditor in this scope (matching PostEditor's pattern)? Currently deferred.

2. **Category/tag translation UI** — The taxonomy management pages currently don't have a translation section. Should we add one, or should AI translation of categories/tags be triggered from a different entry point?

3. **Translation progress indicator** — For the initial one-target-culture flow, should we show a coarse status message (e.g., "Translating content...")? Fine-grained progress becomes more relevant if multi-culture translation is added later.

---

*Last updated: 2026-05-31*
