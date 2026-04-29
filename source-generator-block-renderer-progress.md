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
- [ ] Phase 6: Preview Hardening

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
- [ ] Add whole-page iframe preview with strict origin validation
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
- `BlockEditor.razor` now has an inline preview panel that calls `IPreviewHttpClient.RenderBlockFragmentAsync`, so single-block authoring previews use the same generated adapter-backed server rendering path as runtime rendering.
- The block fragment endpoint lives in `PreviewApi`, preserving preview ownership there.
- Preview fragment endpoints render through `CmsBlockHtmlRenderer`, which renders `BlockRenderer` through Blazor `HtmlRenderer` and therefore uses the generated adapter registry.
- Runtime DI now registers `HtmlRenderer`, `CmsBlockHtmlRenderer`, and the legacy `IBlockSliceRenderer` bridge from `AddAeroCmsRuntimeAsync`.
- `Aero.Cms.Modules.Headless` now references `Aero.Cms.Web.Core` so preview endpoints can use the shared server-side block rendering bridge.
- Full preview convergence remains incomplete until the optional iframe preview shell with strict origin validation is implemented. The practical static SSR fragment path is now wired through `PreviewApi` for inline block previews and whole-page editor previews.
- Verification: `dotnet test --project tests\Aero.Cms.BlockRendering.Tests\Aero.Cms.BlockRendering.Tests.csproj --no-restore -v:minimal` -> 32 passed, 0 failed.
- Verification: `dotnet build src\Aero.Cms.slnx --no-restore -v:minimal` -> succeeded with existing package advisory, Razor SDK, nullability, and deprecation warnings.
