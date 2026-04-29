# Source Generator Block Renderer Progress

Spec: [source-generator-block-renderer.md](source-generator-block-renderer.md)

This file tracks implementation progress for the source-generated block rendering architecture. Keep entries simple: check completed items, leave incomplete items unchecked, and add short notes only where they help the next implementation step.

## Phase Summary

- [x] Phase 0: Inventory & Safety Baseline
- [x] Phase 1: Generated Render Adapters
- [ ] Phase 2: Single Source Of Truth For Block Registration
- [ ] Phase 3: Server-Side Rendering Path Convergence
- [ ] Phase 4: Dynamic Scriban Tier
- [ ] Phase 5: Radzen Markdown, HtmlEditor, Media Uploads, And Sanitization
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

- `BlockMetadataAttribute`: 31 persisted block models were found under `src/Aero.Cms.Abstractions/Blocks`.
- `BlockBase` polymorphism: `src/Aero.Cms.Abstractions/Blocks/BlockBase.cs` currently has 31 handwritten `JsonDerivedType` registrations.
- `BlockJsonContext`: `src/Aero.Cms.Abstractions/Blocks/Serialization/BlockJsonContext.cs` currently includes the same 31 block model types, plus nested item/value types.
- `BlockRenderer.razor`: `src/Aero.Cms.Shared/Blocks/Rendering/BlockRenderer.razor` currently dispatches 20 block type strings through a manual switch.
- Renderer components: `src/Aero.Cms.Shared/Blocks/Rendering` contains 23 `*Renderer.razor` components, including layout/placement renderers and the central `BlockRenderer.razor`.
- Marten subclass configuration: `src/Aero.Cms.Core/Blocks/BlockMartenConfiguration.cs` currently registers 18 block model types.
- Slice rendering path: `src/Aero.Cms.Web.Core/Blocks/Rendering/BlockSliceRegistry.cs`, `IBlockSliceRenderer.cs`, `src/Aero.Cms.Abstractions/Blocks/IBlockVisitor.cs`, `IBlock.Accept(...)`, and every `BlockBase` subclass still represent the legacy visitor/slice rendering path.
- Preview API: `src/Aero.Cms.Modules.Headless/Areas/Api/v1/PreviewApi.cs` currently exposes saved draft `GET` preview endpoints for pages and blog posts. It returns `PreviewResponse<T>` JSON and does not yet render blocks or fragments.
- Editor paths: block authoring is currently spread across `src/Aero.Cms.Shared/Components/BlockPicker.razor`, `src/Aero.Cms.Shared/Components/BlockEditor.razor`, `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor`, `PageEditor.razor.cs`, and backend `EditorBlock` mapping in `src/Aero.Cms.Modules.Pages/PageContentService.cs`.

#### Drift Findings

- Marten is missing 13 block types that are present in `BlockBase`/JSON metadata: `AnalyticsBlock`, `CardBlock`, `CarouselBlock`, `ColumnsBlock`, `ContentLinkBlock`, `FormEditorBlock`, `HeroBlock`, `MarkdownBlock`, `ScrollingContentBlock`, `TikTokBlock`, `TwitchBlock`, `VimeoBlock`, and `YouTubeBlock`.
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
- Latest result: 12 passed, 0 failed.

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
- Verification: `dotnet test --project tests\Aero.Cms.BlockRendering.Tests\Aero.Cms.BlockRendering.Tests.csproj --no-restore -v:minimal` -> 12 passed, 0 failed.

## Phase 2: Single Source Of Truth For Block Registration

- [x] Generate richer `CmsBlockManifest`
- [x] Feed editor palette metadata from the manifest
- [ ] Generate or assist `System.Text.Json` block registration
- [ ] Generate or assist Marten subclass registration
- [ ] Add drift diagnostics for metadata, JSON, and Marten registration
- [ ] Add schema-version metadata and migration policy

### Phase 2 Notes

- `CmsBlockManifest` is now source-generated alongside `CmsBlockRenderRegistry`.
- The generated manifest currently covers renderable blocks discovered through `[CmsBlockRenderer]` marker partials in `Aero.Cms.Shared`.
- Each descriptor includes block type, display name, description, category, icon, sort order, model type, renderer type, and renderer parameter name.
- The manifest is validated by the block rendering test project.
- `BlockPicker.razor` now reads available block types from `CmsBlockManifestEditorMetadata`, which adapts generated descriptors into the existing `BlockTypeInfo` shape.
- The manifest-backed editor metadata keeps `BlockEditingService` in place for create/validate/update operations until the all-block registration slice can move generation closer to the model assembly.
- JSON and Marten generation remain pending. The current generator consumer is `Aero.Cms.Shared`, so this slice is authoritative for renderable block metadata first; all-block JSON/Marten registration will need either a generator consumer closer to the block model assembly or a metadata handoff.
- Verification: `dotnet test --project tests\Aero.Cms.BlockRendering.Tests\Aero.Cms.BlockRendering.Tests.csproj --no-restore -v:minimal` -> 12 passed, 0 failed.

## Phase 3: Server-Side Rendering Path Convergence

- [ ] Decide whether `BlockSliceRegistry` can be removed
- [ ] Implement `CmsBlockHtmlRenderer` with `HtmlRenderer` if a bridge is needed
- [ ] Route any legacy bridge through the generated adapter registry
- [ ] Track hidden dependencies and define a legacy removal point

## Phase 4: Dynamic Scriban Tier

- [ ] Add `DynamicBlockDefinition`
- [ ] Add `DynamicTemplateBlock`
- [ ] Add save-time template parsing and validation
- [ ] Add explicit JSON-to-Scriban mapping
- [ ] Add secure Scriban options, limits, allowlists, and caching
- [ ] Add sanitizer policy for dynamic template output
- [ ] Reject arbitrary JavaScript/script blocks

## Phase 5: Radzen Markdown, HtmlEditor, Media Uploads, And Sanitization

- [ ] Add vendor-neutral `MarkdownBlock`
- [ ] Add vendor-neutral `RawHtmlBlock`
- [ ] Render Markdown through `RadzenMarkdown`
- [ ] Edit HTML blocks through `RadzenHtmlEditor`
- [ ] Add Radzen-compatible media upload endpoint through the existing media API area
- [ ] Add constrained upload validation
- [ ] Add `Ganss.Xss.HtmlSanitizer`
- [ ] Add `ICmsHtmlSanitizer`
- [ ] Add sanitizer tests

## Phase 6: Preview Hardening

- [ ] Keep preview ownership in `PreviewApi`
- [ ] Preserve existing saved draft preview endpoints
- [ ] Add unsaved page/blog-post/block render-fragment endpoints
- [ ] Add whole-page iframe preview with strict origin validation
- [ ] Add debounced whole-page preview updates
- [ ] Add inline single-block preview
- [ ] Route all preview rendering through generated adapter registry
