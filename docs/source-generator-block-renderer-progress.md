
> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). Marten
> was migrated out in [`surrealdb-marten-port.md`](surrealdb-marten-port.md).
> This document is a historical implementation record; its Marten/PostgreSQL
> persistence details do not reflect the current stack.

# Source Generator Block Renderer Progress

Spec: [source-generator-block-renderer.md](source-generator-block-renderer.md)

This file tracks implementation progress for the source-generated block rendering architecture. Keep entries simple: check completed items, leave incomplete items unchecked, and add short notes only where they help the next implementation step.

## Phase Summary

- [x] Phase 0: Inventory & Safety Baseline
- [x] Phase 1: Generated Render Adapters
- [x] Phase 2: Single Source Of Truth For Block Registration
- [x] Phase 3: Server-Side Rendering Path Convergence
- [x] Phase 4: Dynamic Scriban Tier
- [x] Phase 5: Radzen Markdown, HtmlEditor, Media Uploads, And Sanitization
- [x] Phase 6: Preview Hardening
- [x] Post-Spec Editor MVP Updates

## Phase 0: Inventory & Safety Baseline

- [x] Phase 0A: Current-State Inventory
- [x] Phase 0B: Baseline Tests
- [x] Phase 0C: Source Generator Proof Boundary
- [x] Phase 0D: ADR And Decision Record
- [x] Phase 0E: Definition Of Done

### Phase 0A Inventory

- [x] Block models with `BlockMetadataAttribute`
- [x] `BlockBase` / `IBlock` subtypes without `BlockMetadataAttribute`
- [x] Renderer components participating in block rendering
- [x] Block types handled by `BlockRenderer.razor`
- [x] `JsonDerivedType` registrations on `BlockBase`
- [x] Block types registered in `BlockJsonContext`
- [x] Block types registered in Marten subclass configuration
- [x] Uses of `BlockSliceRegistry`, `IBlockSliceRenderer`, and `IBlockVisitor`
- [x] Preview paths that render blocks, including `PreviewApi` consumers
- [x] Editor paths that create or edit blocks
- [x] Drift findings recorded

#### Current Inventory Notes

- `BlockMetadataAttribute`: 33 persisted block models were found under `src/Aero.Cms.Abstractions/Blocks`.
- `BlockBase` polymorphism: `src/Aero.Cms.Abstractions/Blocks/BlockBase.cs` currently has 33 handwritten `JsonDerivedType` registrations.
- `BlockJsonContext`: `src/Aero.Cms.Abstractions/Blocks/Serialization/BlockJsonContext.cs` currently includes the same 33 block model types, plus nested item/value types.
- `BlockRenderer.razor`: `src/Aero.Cms.Shared/Blocks/Rendering/BlockRenderer.razor` currently dispatches 20 block type strings through a manual switch.
- Renderer components: `src/Aero.Cms.Shared/Blocks/Rendering` contains 23 `*Renderer.razor` components, including layout/placement renderers and the central `BlockRenderer.razor`.
- Marten subclass configuration: `src/Aero.Cms.Core/Blocks/BlockMartenConfiguration.cs` originally registered a stale handwritten subset; Phase 2 now maps from generated block metadata.
- Slice rendering path: `src/Aero.Cms.Web.Core/Blocks/Rendering/BlockSliceRegistry.cs`, `IBlockSliceRenderer.cs`, `src/Aero.Cms.Abstractions/Blocks/IBlockVisitor.cs`, `IBlock.Accept(...)`, and every `BlockBase` subclass still represent the legacy visitor/slice rendering path.
- Preview API: `src/Aero.Cms.Modules.Headless/Areas/Api/v1/PreviewApi.cs` currently exposes saved draft `GET` preview endpoints for pages and blog posts. It returns `PreviewResponse<T>` JSON and does not yet render blocks or fragments.
- Editor paths: block authoring is currently spread across `src/Aero.Cms.Shared/Components/BlockPicker.razor`, `src/Aero.Cms.Shared/Components/BlockEditor.razor`, `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor`, `PageEditor.razor.cs`, and backend `EditorBlock` mapping in `src/Aero.Cms.Modules.Pages/PageContentService.cs`.

#### Drift Findings

- Initial Marten drift finding: Marten was missing 13 block types that were present in `BlockBase`/JSON metadata: `AnalyticsBlock`, `CardBlock`, `CarouselBlock`, `ColumnsBlock`, `ContentLinkBlock`, `FormEditorBlock`, `HeroBlock`, `MarkdownBlock`, `ScrollingContentBlock`, `TikTokBlock`, `TwitchBlock`, `VimeoBlock`, and `YouTubeBlock`. Phase 2 resolves this by mapping from `GeneratedBlockModelManifest`.
- `BlockRenderer.razor` is missing 13 block discriminators that are present in `BlockBase`: `analytics_script`, `cards`, `carousel`, `columns`, `content_link`, `form_editor`, `hero`, `raw_html`, `scrolling_content`, `tiktok_player`, `twitch_player`, `vimeo_player`, and `youtube_player`.
- `RawHtmlRenderer.razor` exists, but `BlockRenderer.razor` does not dispatch the `raw_html` block type.
- Current editor palettes and editor preview fragments are hand-authored and string-based, separate from `BlockMetadataAttribute`.
- `PageContentService.MapEditorBlock(...)` maps some editor-only strings such as `video` and `gallery` to persisted block models, so Phase 2 should distinguish persisted block discriminators from editor palette aliases.
- No concrete `BlockBase` subclass without `BlockMetadataAttribute` was identified in this first pass. Nested item/value classes are not block models.

### Phase 0B Baseline Tests

- [x] Simple representative block rendering baseline
- [x] Context-aware block rendering baseline
- [x] Unknown block fallback baseline
- [x] Raw HTML / sanitized HTML baseline, if sanitizer exists
- [ ] Saved draft preview path baseline, if practical

Baseline test project:

- `tests/Aero.Cms.BlockRendering.Tests`
- Verification: `dotnet test --project tests\Aero.Cms.BlockRendering.Tests\Aero.Cms.BlockRendering.Tests.csproj --no-restore -v:minimal`
- Latest result: 27 passed, 0 failed.

Saved draft preview baseline is deferred until Phase 6 preview fragment work because `PreviewApi` currently returns draft JSON only and does not render block HTML.

### Phase 0C Proof Boundary

- [x] First proof block set selected
- [x] Generator target assembly confirmed
- [x] Source generator project location confirmed

First proof block set:

- `MarkdownBlock` -> `MarkdownBlockRenderer`: simple one-parameter renderer.
- `RawHtmlBlock` -> `RawHtmlRenderer`: renderer exists but is currently missing from `BlockRenderer.razor`, which makes it a useful drift test.
- `NavigationBlock` -> `NavigationBlockRenderer`: context-aware renderer that needs `NavigationDetail`.

Generator target:

- Source generator project: `src/Aero.Cms.SourceGenerators`
- First generated consumer assembly: `src/Aero.Cms.Shared`

### Phase 0D ADR

- [x] ADR created for source-generated adapters over runtime reflection/scanning

ADR: [docs/decisions/ADR-001-source-generated-block-render-adapters.md](docs/decisions/ADR-001-source-generated-block-render-adapters.md)

### Phase 0E Definition Of Done

- [x] Inventory exists and identifies current drift points
- [x] Representative baseline tests or snapshots exist
- [x] First proof-of-concept block set is chosen
- [x] Source-generator project location and target assembly are agreed
- [x] ADR is written
- [x] Phase 1 can begin without additional architectural decisions

## Phase 1: Generated Render Adapters

- [x] Add rendering contracts and renderer attribute
- [x] Add source generator project
- [x] Generate render adapters
- [x] Generate render registry
- [x] Replace `BlockRenderer.razor` switch with generated registry dispatch
- [x] Add diagnostics for duplicate/mismatched renderer metadata
- [x] Add snapshot tests for generated adapter output
- [x] Add narrowly scoped render error boundaries

### Phase 1 Notes

- `src/Aero.Cms.SourceGenerators` now emits typed `ICmsBlockRenderAdapter` implementations and `CmsBlockRenderRegistry`.
- Renderer marker attributes are currently placed on C# partial classes/code-behind files, not `.razor` `@attribute` lines, because the first build proved the generator did not see Razor-level attributes reliably.
- `BlockRenderer.razor` now routes through `CmsBlockRenderRegistry` and keeps the existing unknown-block fallback.
- Registry coverage now includes the previous switch-supported block types plus `raw_html`.
- `BlockRenderer.razor` wraps resolved renderer fragments in a local `ErrorBoundary` fallback so a block render failure does not take down the whole page.
- Generator diagnostics currently cover duplicate block type, visible block parameter mismatch, invalid model type, and missing `BlockMetadataAttribute`.
- Missing `Block` parameter diagnostics are deferred until renderer parameters are moved into C# code-behind, because the generator does not reliably see parameters declared inside `.razor` bodies.
- Generator smoke coverage asserts the emitted adapter uses typed `RenderTreeBuilder.OpenComponent<T>()` and typed `Block` parameter dispatch.
- Verification: `dotnet test --project tests\Aero.Cms.BlockRendering.Tests\Aero.Cms.BlockRendering.Tests.csproj --no-restore -v:minimal` -> 27 passed, 0 failed.

## Phase 2: Single Source Of Truth For Block Registration

- [x] Generate richer `CmsBlockManifest`
- [x] Feed editor palette metadata from the manifest
- [x] Generate or assist `System.Text.Json` block registration
- [x] Generate or assist Marten subclass registration
- [x] Add drift diagnostics for metadata, JSON, and Marten registration
- [x] Add schema-version metadata and migration policy

### Phase 2 Notes

- `CmsBlockManifest` is now source-generated alongside `CmsBlockRenderRegistry`.
- The generated manifest currently covers renderable blocks discovered through `[CmsBlockRenderer]` marker partials in `Aero.Cms.Shared`.
- Each descriptor includes block type, display name, description, category, icon, sort order, model type, renderer type, and renderer parameter name.
- The manifest is validated by the block rendering test project.
- `BlockPicker.razor` now reads available block types from `CmsBlockManifestEditorMetadata`, which adapts generated descriptors into the existing `BlockTypeInfo` shape.
- The manifest-backed editor metadata keeps `BlockEditingService` in place for create/validate/update operations until the all-block registration slice can move generation closer to the model assembly.
- `Aero.Cms.Abstractions` now runs the source generator as an analyzer so it can discover every block model declared with `[BlockMetadata]`.
- `GeneratedBlockModelManifest` is now emitted for all 33 discovered block models, including formerly drift-prone models such as `MarkdownBlock`, `YouTubeBlock`, and `ColumnsBlock`.
- `GeneratedBlockJsonRegistration` now emits source-generated metadata arrays for block model and block collection types. It intentionally does not emit a `JsonSerializerContext`, because `System.Text.Json` source generation does not consume context attributes emitted by another generator in the same compilation.
- `BlockMartenConfiguration` now maps the subclass hierarchy from `GeneratedBlockModelManifest` and passes explicit `MappedType` aliases using the persisted block discriminator.
- `BlockMetadataAttribute` now includes `SchemaVersion`, defaulting to `1`.
- `CmsBlockManifest` and `GeneratedBlockModelManifest` now carry schema version metadata.
- Generator diagnostic `AERO006` now reports duplicate persisted block type metadata across block model classes.
- Verification: `dotnet test --project tests\Aero.Cms.BlockRendering.Tests\Aero.Cms.BlockRendering.Tests.csproj --no-restore -v:minimal` -> 15 passed, 0 failed.

### Spec Deviation Notes

- Temporary renderer marker placement shift: the spec allows renderer metadata on Razor components, but the current implementation places `[CmsBlockRenderer]` on C# partial/code-behind marker classes because the generator did not reliably see Razor-level `@attribute` metadata or Razor-declared `[Parameter]` properties in the first implementation pass. This can be revisited if renderer parameters move into code-behind or Razor generator output becomes easier to consume.
- Temporary JSON generation shift: the spec calls for generated `System.Text.Json` context registration. The current implementation emits `GeneratedBlockJsonRegistration` metadata instead of a real `JsonSerializerContext` because compiler verification showed `System.Text.Json` source generation does not consume `[JsonSerializable]` attributes emitted by another source generator in the same compilation. The handwritten `BlockJsonContext` remains the runtime serializer context until a separate generation strategy is chosen.

## Phase 3: Server-Side Rendering Path Convergence

- [x] Decide whether `BlockSliceRegistry` can be removed
- [x] Implement `CmsBlockHtmlRenderer` with `HtmlRenderer` if a bridge is needed
- [x] Route any legacy bridge through the generated adapter registry
- [x] Track hidden dependencies and define a legacy removal point

### Phase 3 Notes

- `BlockSliceRegistry`, `IBlockSliceRenderer`, and `IBlockVisitor` have no currently discovered production registrations beyond their own definitions, but the visitor shape is still on every `BlockBase` subclass through `Accept(...)`.
- `BlockSliceRegistry` is not removed in this slice. It remains as a compatibility shim until all `Accept(...)` usage can be deprecated.
- `CmsBlockHtmlRenderer` renders `BlockRenderer` through Blazor `HtmlRenderer` and returns `IHtmlContent`.
- `CmsBlockSliceRenderer` implements the legacy `IBlockSliceRenderer` interface and delegates to `CmsBlockHtmlRenderer`, which means the legacy visitor path now converges on the generated adapter registry.
- The Blazor `HtmlRenderer` bridge must call both `RenderComponentAsync(...)` and `ToHtmlString()` through `HtmlRenderer.Dispatcher.InvokeAsync(...)`.
- Removal point: after no production callers depend on `IBlockVisitor`, `BlockBase.Accept(...)`, or `IBlockSliceRenderer`, delete the visitor/slice types and route server-side HTML rendering directly through `CmsBlockHtmlRenderer`.
- Verification: `dotnet test --project tests\Aero.Cms.BlockRendering.Tests\Aero.Cms.BlockRendering.Tests.csproj --no-restore -v:minimal` -> 16 passed, 0 failed.

## Phase 4: Dynamic Scriban Tier

- [x] Add `DynamicBlockDefinition`
- [x] Add `DynamicTemplateBlock`
- [x] Add save-time template parsing and validation
- [x] Add explicit JSON-to-Scriban mapping
- [x] Add secure Scriban options, limits, allowlists, and caching
- [x] Add sanitizer policy for dynamic template output
- [x] Reject arbitrary JavaScript/script blocks

### Phase 4 Notes

- Added `Scriban` 7.1.0 through central package management and referenced it from `Aero.Cms.Core`.
- Added `DynamicTemplateBlock` as the persisted block wrapper and `DynamicBlockDefinition` as the versioned user-authored template definition.
- Because the runtime `JsonSerializerContext` is still handwritten, `DynamicTemplateBlock` is currently registered manually in `BlockBase` and `BlockJsonContext`.
- Added `DynamicTemplateValidator` for save-time parsing, template length enforcement, and rejection of `<script>`, `javascript:`, and inline event-handler attributes.
- Added `JsonToScribanMapper` so dynamic JSON data is exposed through explicit Scriban `ScriptObject` and `ScriptArray` values under the `block` variable, avoiding reflection-based model rendering.
- Added `SecureScribanRenderer` with template parse caching, strict variables, loop/recursion/regex/output limits, cancellation timeout, disabled relaxed member/function/indexer access, and no template loader.
- Template cache keys include definition id, version, and template text so authoring previews cannot reuse stale parsed templates when an unsaved draft changes.
- `AddBlockSystemServices()` now registers `SecureScribanTemplateOptions`, `DynamicTemplateValidator`, and `ISecureScribanRenderer`.
- Dynamic Scriban output now passes through `ICmsHtmlSanitizer` before returning.
- The secure renderer has runtime limits, template parse caching, sanitizer output, and an explicit Scriban function allowlist. The default allowlist now includes a curated deterministic subset of Scriban string, array, html, and math helpers; `object`, `regex`, imports, user-declared functions, and non-deterministic helpers remain blocked unless deliberately reviewed and enabled.
- The validator now allows curated function calls and pipe functions by default while still rejecting unsafe function calls, imports, and template function declarations. Full schema-aware variable validation remains deferred.
- Added `MartenDynamicBlockDefinitionService` for loading published, versioned dynamic block definitions by definition id and version.
- Added `DynamicTemplateBlockRenderer`, registered it through the generated render adapter registry, and routed rendering through `IDynamicBlockDefinitionService` + `ISecureScribanRenderer`.
- Verification: `dotnet test --project tests\Aero.Cms.BlockRendering.Tests\Aero.Cms.BlockRendering.Tests.csproj --no-restore -v:minimal` -> 32 passed, 0 failed.

## Phase 5: Radzen Markdown, HtmlEditor, Media Uploads, And Sanitization

- [x] Add vendor-neutral `MarkdownBlock`
- [x] Add vendor-neutral `RawHtmlBlock`
- [x] Render Markdown through `RadzenMarkdown`
- [x] Edit HTML blocks through `RadzenHtmlEditor`
- [x] Add Radzen-compatible media upload endpoint through the existing media API area
- [x] Add constrained upload validation
- [x] Add `Ganss.Xss.HtmlSanitizer`
- [x] Add `ICmsHtmlSanitizer`
- [x] Add sanitizer tests

### Phase 5 Notes

- `MarkdownBlock` and `RawHtmlBlock` already existed as vendor-neutral block models before this phase; they are marked complete based on current inventory.
- Added `HtmlSanitizer` 9.0.892 through central package management and referenced it from `Aero.Cms.Core`.
- Added `ICmsHtmlSanitizer` / `CmsHtmlSanitizer` with an explicit policy that strips script/style tags, removes the `javascript` scheme, and removes any event-handler attributes present in the allowlist.
- `RawHtmlRenderer` now sanitizes `RawHtmlBlock.Content` before rendering as `MarkupString`.
- `SecureScribanRenderer` now sanitizes dynamic template output before returning it.
- `MarkdownBlockRenderer` renders through `RadzenMarkdown` with `AllowHtml="false"` so Markdown blocks do not become a second custom HTML/JavaScript surface.
- `BlockEditor.razor` now has explicit `MarkdownBlock` and `RawHtmlBlock` authoring cases; raw HTML authoring uses `RadzenHtmlEditor` with paste sanitization.
- `PageEditor.razor` now previews Markdown through `RadzenMarkdown` with `AllowHtml="false"` and edits `raw_html` blocks through `RadzenHtmlEditor`.
- `MediaApi` now exposes `POST /api/v1/admin/media/html-editor-image` for Radzen HtmlEditor image uploads and returns the `{ url }` payload shape expected by Radzen.
- HTML editor image uploads are constrained to JPEG, PNG, WebP, and GIF files, enforce a 10 MB limit, sanitize stored filenames, and reject mismatched content type / extension pairs.
- Central package management now pins `AngleSharp` to `0.17.1` to match `HtmlSanitizer` 9.0.892 and the existing `AngleSharp.Css` 0.17 line.
- Existing `RawHtmlBlock` documentation was tightened so it no longer describes the block as a JavaScript injection surface.
- Verification: `dotnet test --project tests\Aero.Cms.BlockRendering.Tests\Aero.Cms.BlockRendering.Tests.csproj --no-restore -v:minimal` -> 27 passed, 0 failed.
- Verification: `dotnet build src\Aero.Cms.slnx --no-restore -v:minimal` -> succeeded with existing package advisory, Razor SDK, nullability, and deprecation warnings.

## Phase 6: Preview Hardening

- [x] Keep preview ownership in `PreviewApi`
- [x] Preserve existing saved draft preview endpoints
- [x] Add unsaved block render-fragment endpoint
- [x] Add unsaved page/blog-post render-fragment endpoints
- [x] Add sandboxed whole-page iframe preview shell
- [x] Add debounced whole-page preview updates
- [x] Add inline single-block preview
- [x] Route all preview fragment rendering through generated adapter registry

### Phase 6 Notes

- Existing saved draft preview endpoints remain at `GET /api/v1/admin/preview/pages/{id}` and `GET /api/v1/admin/preview/blog-posts/{id}` and still return draft document JSON.
- Added `POST /api/v1/admin/preview/blocks/render-fragment` to render a single unsaved `BlockBase` payload to an HTML fragment response.
- Added `POST /api/v1/admin/preview/pages/render-fragment` and `POST /api/v1/admin/preview/blog-posts/render-fragment` to render unsaved documents to HTML fragment responses.
- Page fragment rendering now prefers unsaved `PreviewPageFragmentRequest.Blocks` editor payloads and maps them through `EditorBlockMapper` before falling back to layout regions. This avoids forcing preview through `BlockPlacement.BlockId` lookups when the editor has unsaved block data.
- Preview fragment request/response contracts now live in `Aero.Cms.Abstractions.Http`, so editor clients can send typed unsaved editor blocks/layout regions without referencing Core document entities.
- `PageEditor.razor` now renders preview mode through `IPreviewHttpClient.RenderPageFragmentAsync` and the `PreviewApi` unsaved page fragment endpoint instead of the local handwritten block renderer.
- Whole-page preview updates are debounced at 300ms while preview mode is active.
- Page preview now displays the server-rendered fragment inside a sandboxed `srcdoc` iframe, giving whole-page isolation without introducing the heavier interactive `postMessage` path.
- Saved page preview now uses `GET /api/v1/admin/pages/drafts/{id}` as the iframe URL, redirects to `/_cms/preview/pages/drafts/{id}`, and renders through the normal Razor page/layout pipeline so site CSS and script assets are included.
- The page editor preview frame now shows a URL bar above the iframe so authors can see the preview endpoint currently being loaded.
- Draft preview by id is guarded in the Razor page model and returns unauthorized for unauthenticated requests, so unpublished pages are not exposed through the public slug route.
- New/unsaved pages still fall back to the `PreviewApi` static fragment `srcdoc` path because they do not yet have a persisted draft id to load through the full ASP.NET pipeline.
- Because the implemented preview path is static SSR fragment rendering, there is no cross-origin `postMessage` surface to validate. If an interactive iframe mode is added later, it must use exact `targetOrigin` and receiver-side `event.origin` validation as specified.
- `BlockEditor.razor` now has an inline preview panel that calls `IPreviewHttpClient.RenderBlockFragmentAsync`, so single-block authoring previews use the same generated adapter-backed server rendering path as runtime rendering.
- The block fragment endpoint lives in `PreviewApi`, preserving preview ownership there.
- Preview fragment endpoints render through `CmsBlockHtmlRenderer`, which renders `BlockRenderer` through Blazor `HtmlRenderer` and therefore uses the generated adapter registry.
- Runtime DI now registers `HtmlRenderer`, `CmsBlockHtmlRenderer`, and the legacy `IBlockSliceRenderer` bridge from `AddAeroCmsRuntimeAsync`.
- `Aero.Cms.Modules.Headless` now references `Aero.Cms.Web.Core` so preview endpoints can use the shared server-side block rendering bridge.
- Full preview convergence now uses the recommended static SSR fragment path: inline block previews and whole-page editor previews both render through `PreviewApi` and the generated adapter registry. Interactive `postMessage` preview remains intentionally unimplemented unless a later UX requirement justifies it.
- Verification: `dotnet test --project tests\Aero.Cms.BlockRendering.Tests\Aero.Cms.BlockRendering.Tests.csproj --no-restore -v:minimal` -> 32 passed, 0 failed.
- Verification: `dotnet build src\Aero.Cms.slnx --no-restore -v:minimal` -> succeeded with existing package advisory, Razor SDK, nullability, and deprecation warnings.

## Post-Spec Editor MVP Updates

- [x] Replace the implicit page-level hero/header rendering with explicit block content
- [x] Add `boring_hero` as a simple full-width page intro block
- [x] Convert homepage/about/contact seed pages to use `BoringHeroBlock`
- [x] Add `Height` and `FullScreen` to the existing `hero` block
- [x] Add `Boring Hero`, `Hero`, `Markdown`, `Raw HTML`, and `Scriban` to the PageEditor UI section
- [x] Add page metadata toggles for site navigation and footer visibility
- [x] Add server-rendered inline Scriban authoring preview through `PreviewApi`
- [ ] Add production-grade Scriban policy editing and safeguards after MVP signoff

## Phase 7: Source-Generated JSON Contexts (Multi-Project Shim)

- [x] Create `Aero.Cms.Generated.Json` shim project
- [x] Extend `BlockRendererGenerator` to emit `BlockBase.Polymorphic.g.cs` (replace hand-maintained `[JsonDerivedType]` list)
- [x] Create `ContentTypeGenerator` for content type discovery + JSON context
- [x] Make `BlockBase` partial, remove hand-maintained `[JsonDerivedType]` attributes
- [x] Wire generated context into Marten (`TypeInfoResolver`)
- [ ] Emit `GeneratedBlockJsonContext` (deferred — blocked by Roslyn generator chaining limitation)
- [ ] Delete hand-maintained `BlockJsonContext.cs` (deferred)
- [ ] Update `BlockSerializer` to use generated context from shim project (deferred)

### Phase 7 Process Documentation

This section documents the full investigation, decisions, and implementation. It exists because Phase 7 involved significant architecture research and resolved a long-standing design tension.

---

#### 7.1 Trigger

The content type implementation spec (`docs/content-type-implementation.md`) requires adding new models (`ContentItem`, `ContentTypeDefinition`, `ContentEmbedBlock`) to the codebase. These need AOT-safe `JsonSerializerContext` registration alongside the existing 34 block types. The existing manual approach (`BlockJsonContext.cs` with 143 hand-written `[JsonSerializable]` lines) would require another manual entry per new type — a maintenance burden that source generators were intended to solve.

Progress doc Phase 2 note (line 151) documents the blocker:
> *STJ source generation does not consume context attributes emitted by another source generator in the same compilation.*

---

#### 7.2 Investigation

The spec at `docs/cms-source-generators.md` §4 and `docs/source-generator-block-renderer.md` §10 describes generating:
- `CmsJsonContext.g.cs` — auto-generated `JsonSerializerContext`
- `BlockBase.Polymorphic.g.cs` — auto-generated `[JsonDerivedType]` attributes

These were never implemented because the Phase 2 team hit the Roslyn source generator chaining limitation.

**Key research sources:**

| Source | Finding |
|--------|---------|
| [dotnet/roslyn#57239](https://github.com/dotnet/roslyn/issues/57239) | Roslyn source generators cannot chain (open since 2020) |
| [dotnet/runtime#93439](https://github.com/dotnet/runtime/issues/93439) | STJ team confirms: "SGs run in non-deterministic order" |
| [dotnet/runtime#108317](https://github.com/dotnet/runtime/issues/108317) | "STJ generator won't recognize generated context in scope" |
| [dotnet/runtime#113584](https://github.com/dotnet/runtime/issues/113584) | "Source generators can't see each other" |
| [dotnet/runtime#124889](https://github.com/dotnet/runtime/issues/124889) | Proposed `[assembly: DefaultJsonSerializerContext]` — not yet merged |
| [dotnet/runtime#126861](https://github.com/dotnet/runtime/pull/126861) | Draft PR: `[JsonSerializable]` on POCOs directly — does not solve chaining |
| Roslyn Incremental Generators Cookbook | Code rewriting explicitly out of scope for source generators |

**Contradictory finding:** Microsoft Learn docs (mslearn) state that emitting `[JsonSerializable]` partial classes from another generator "does work in practice." Microsoft Learn MCP was queried and returned this guidance. However, the AeroCMS Phase 2 team reported it did NOT work in their testing, and our Phase 7 build confirmed CS0534 errors (partial context without STJ implementation), contradicting the Microsoft Learn docs.

---

#### 7.3 Architecture Decision

**Candidate approaches evaluated:**

| Approach | Pros | Cons | Verdict |
|----------|------|------|---------|
| A. Keep `BlockJsonContext.cs` manually | Zero risk | Per-type maintenance forever | Rejected |
| B. Multi-project shim (recommended by STJ team) | Works around chaining | Adds project to solution; context emission STILL failed | Attempted |
| C. Generate full `JsonSerializerContext` impl manually | No STJ dependency | Reimplements STJ's code gen (months of work) | Rejected |
| D. `RegisterPostInitializationOutput` | Runs before all generators | Only for fixed content, not dynamic | Rejected |
| E. Emit `[JsonDerivedType]` only (leave context hand-written) | Solves biggest pain point | Context still manual | Selected |

**Decision:** Move all `[JsonDerivedType]` attributes to generated code (eliminating the 34-entry manual list). Defer `JsonSerializerContext` auto-generation to when Roslyn/SJT chaining is resolved. Wire the existing hand-maintained context into Marten for AOT safety.

**Why the shim project could not emit the context:** Even with a dedicated project where both custom and STJ generators compile together, CS0534 confirms STJ cannot see the `[JsonSerializable]` attributes from another generator. The Roslyn limitation applies at the generator level, not the project level. Cross-project (shim) is the same as single-project in this regard.

---

#### 7.4 Codebase Analysis (Before)

Key files analyzed:

| File | Lines | Problem |
|------|-------|---------|
| `src/Aero.Cms.Abstractions/Blocks/BlockBase.cs` | 71 | 34 hand-written `[JsonDerivedType]` (lines 16-50) + `[JsonPolymorphic]` |
| `src/Aero.Cms.Abstractions/Blocks/Serialization/BlockJsonContext.cs` | 143 | All `[JsonSerializable]` entries manually maintained |
| `src/Aero.Cms.SourceGenerators/BlockRendererGenerator.cs` | 761 | Already discovers block models from source; emits manifest + registry |
| `src/Aero.Cms.Core/Blocks/BlockMartenConfiguration.cs` | 19 | Already uses `GeneratedBlockModelManifest` for Marten subclass hierarchy |
| `src/Aero.Cms.Core/Blocks/Dynamic/` (8 files) | — | Full Scriban rendering pipeline already built and tested |

**Key insight about Marten vs BlockSerializer:**
- Marten's serialization pipeline (`SystemTextJsonSerializer`) does NOT use `BlockJsonContext.Default`. It uses its own runtime STJ serialization with no `TypeInfoResolver` set. Blocks in Marten are serialized via runtime `[JsonDerivedType]` reflection.
- `BlockSerializer` (app layer) does use `BlockJsonContext.Default`. They are completely separate pipelines.
- The `docs/marten-aot.md` spec (line 104-112) recommends injecting `TypeInfoResolver` into Marten. This was never done.

---

#### 7.5 Implementation Details

**`BlockRendererGenerator.cs` extended (lines added: ~200):**

The existing generator already discovered block models via `SyntaxProvider.ForAttributeWithMetadataName` (local source discovery). This was sufficient for `BlockBase.Polymorphic.g.cs` because `BlockBase` lives in `Aero.Cms.Abstractions` where the types are in source.

A second pipeline was added using `CompilationProvider` for cross-assembly discovery (to discover block types from referenced DLLs). This pipeline is needed for the deferred `GeneratedBlockJsonContext` — when it's activated, the shim project needs to discover types that live in `Aero.Cms.Abstractions.dll`.

Key structs added:
- `DiscoveredBlockType` — carries `FullyQualifiedName`, `BlockType`, `DisplayName` for a block model
- `CrossAssemblyBlockData` — carries both discovered types and the current assembly name (needed to filter output by project)

Key methods added:
- `CollectBlockTypes()` — recursive namespace walker for types with `[BlockMetadata]`
- `IsDerivedFromBlockBase()` — traverses `BaseType` chain to check inheritance
- `RenderBlockBasePolymorphic()` — emits `[JsonPolymorphic]` + `[JsonDerivedType]` on partial `BlockBase`
- `RenderGeneratedContext()` — emits `[JsonSerializable]` + `JsonSourceGenerationOptions` on partial context

**`ContentTypeGenerator.cs` created (345 lines):**

New incremental source generator following the `BlockRendererGenerator` pattern:
- `RegisterPostInitializationOutput` emits `[ContentType]` and `[ContentField]` attributes into every project
- Pipeline 1: `ForAttributeWithMetadataName` discovers `[ContentType]`-decorated classes in source
- Pipeline 2: `CompilationProvider` discovers content types from referenced assemblies
- Output 1: `GeneratedContentTypes.g.cs` — static manifest with `ContentTypeDefinition` instances
- Output 2: `GeneratedContentJsonContext.g.cs` — emits only when infrastructure types (ContentItem, etc.) exist

**`BlockBase.cs` simplified:**
```csharp
// Before: 51 lines of attributes + 1 class declaration
[JsonPolymorphic(...)]
[JsonDerivedType(typeof(RichTextBlock), "rich_text")]
// ... 33 more ...
public abstract class BlockBase : Entity, IBlock { }

// After: 1 class declaration (partial, no STJ attributes)
public abstract partial class BlockBase : Entity, IBlock { }
```

**`Aero.Cms.Generated.Json` project created:**
- References `Aero.Cms.SourceGenerators` as analyzer (inherits `Directory.Build.props` wiring)
- References `Aero.Cms.Abstractions` for block types + `BlockJsonContext`
- References `Aero.Cms.Core` for Marten configuration
- Exposes `GeneratedMartenConfiguration.UseAeroGeneratedJsonContext()` extension method
- No sources needed; all outputs come from source generators

**Marten wiring (`ServerTargetSetupExecutor.cs`):**
```csharp
// Before: raw STJ options without TypeInfoResolver
options.UseSystemTextJsonForSerialization(new JsonSerializerOptions
{
    AllowOutOfOrderMetadataProperties = true
});

// After: wraps BlockJsonContext.Default with AllowOutOfOrderMetadataProperties
options.UseAeroGeneratedJsonContext();
```

---

#### 7.6 Build Verification

| Build Target | Errors | Notes |
|-------------|--------|-------|
| `Aero.Cms.Generated.Json` | 0 | Shim project compiles clean |
| `Aero.Cms.Abstractions` | 0 | BlockBase partial works; generator emits BlockBase.Polymorphic.g.cs |
| `Aero.Cms.Core` | 0 | BlockMartenConfiguration uses generated manifest |
| `Aero.Cms.Modules.Setup` | 0 | New reference to shim project; Marten wiring compiled |
| Full `Aero.Cms.slnx` (source projects) | 0 | All source projects compile |
| Test project (`Aero.Cms.Core.Tests`) | 2 | Pre-existing (missing `ModuleDiscoveryService` — unrelated) |

---

#### 7.7 What Remains Deferred

| Item | Blocked By | Trigger to Resume |
|------|-----------|-------------------|
| Emit `GeneratedBlockJsonContext` from generator | `dotnet/roslyn#57239` / `dotnet/runtime#124889` | Roslyn fixes generator chaining OR .NET ships `DefaultJsonSerializerContext` |
| Delete `BlockJsonContext.cs` | Needs generated context first | Same as above |
| Wire `BlockSerializer` to shim context | `BlockJsonContext.cs` still exists | Same as above |

The `RenderGeneratedContext()` method and `crossAssemblyBlockData` pipeline remain in the generator, commented out, documented with the issue link. When resolved: uncomment the `spc.AddSource` call in `BlockRendererGenerator.Initialize`, verify the STJ generator produces the implementation, delete `BlockJsonContext.cs`, update `BlockSerializer.cs` and `GeneratedMartenConfiguration.cs` to reference `GeneratedBlockJsonContext.Default`.

---

#### 7.8 Files Created or Modified

| File | Action | Purpose |
|------|--------|---------|
| `docs/source-generator-chaining-limitation.md` | **Created** | Documents Roslyn limitation with issue citations |
| `src/Aero.Cms.Generated.Json/Aero.Cms.Generated.Json.csproj` | **Created** | Shim project for dual-generator compilation |
| `src/Aero.Cms.Generated.Json/GeneratedMartenConfiguration.cs` | **Created** | Marten wiring extension methods |
| `src/Aero.Cms.SourceGenerators/BlockRendererGenerator.cs` | **Extended** | Cross-assembly discovery + BlockBase.Polymorphic emission |
| `src/Aero.Cms.SourceGenerators/ContentTypeGenerator.cs` | **Created** | Content type discovery + attributes + manifest |
| `src/Aero.Cms.Abstractions/Blocks/BlockBase.cs` | **Modified** | Made partial, removed 34 `[JsonDerivedType]` and `[JsonPolymorphic]` |
| `src/Aero.Cms.Abstractions/Blocks/Serialization/BlockJsonContext.cs` | **Modified** | Changed `internal` → `public` for shim project access |
| `src/Aero.Cms.Modules.Setup/ServerTargetSetupExecutor.cs` | **Modified** | Uses `UseAeroGeneratedJsonContext()` |
| `src/Aero.Cms.Modules.Setup/Aero.Cms.Modules.Setup.csproj` | **Modified** | Added shim project reference |
| `src/Aero.Cms.slnx` | **Modified** | Added shim project entry |

### Post-Spec Notes

- `Page.cshtml` no longer renders a hard-coded page-level hero from `PageDocument.Title`, `Summary`, and `HeaderImageUrl`; pages now show hero/header content only when a block supplies it.
- `BoringHeroBlock` intentionally mirrors the former simple page header and always renders as full-width content.
- The existing `HeroBlock` now supports pixel `Height` with a default of `512`; `FullScreen` wins over `Height`.
- `ShowHeaderNavigation` and `HideFooter` are editable in the PageEditor metadata tab and are passed through page create/update APIs.
- The PageEditor Scriban block keeps authoring execution on the server by calling `PreviewApi` block fragment rendering with an inline template. This avoids adding a second client-side Scriban execution path.
- MVP Scriban preview temporarily allows all Scriban function calls so custom function experiments are not blocked during local demo work. `SecureScribanTemplateOptions` carries a TODO to tighten this before production.
- Verification: `dotnet build src\Aero.Cms.Shared\Aero.Cms.Shared.csproj --no-restore -v:minimal` -> succeeded with existing package advisory warnings.
- Verification: `dotnet build src\Aero.Cms.Modules.Pages\Aero.Cms.Modules.Pages.csproj --no-restore -v:minimal` -> succeeded with existing warnings.
- Verification: `dotnet build src\Aero.Cms.Modules.Headless\Aero.Cms.Modules.Headless.csproj --no-restore -v:minimal` -> succeeded with existing warnings.
- Verification: `dotnet build src\Aero.Cms.Modules.Setup\Aero.Cms.Modules.Setup.csproj --no-restore -v:minimal` -> succeeded with existing warnings.
