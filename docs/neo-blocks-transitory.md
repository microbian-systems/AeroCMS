# NeoUI Blocks Implementation Plan — Transitory Revision

> **Status:** Updated based on council review & Microsoft Learn docs verification.
> Original: `neo-blocks-implementation.md` (preserved as-is).
> Changes in this revision: render mode fixed to `ServerPrerendered`, `PrefersInteractive` dropped, `@rendermode` placement corrected, source generator plan revised, minor fixes applied.

---

## Overview

Add NeoUI Blazor components as page editor blocks alongside the existing HyperUI blocks.
This is **additive** — both libraries coexist in the page editor palette and public rendering pipeline.

| | HyperUI | NeoUI |
|---|---|---|
| **Rendering** | Raw HTML + Tailwind CSS | NeoUI Blazor components (`<Card>`, `<Button>`, `<DataTable>`, etc.) |
| **SSR** | Static SSR safe | ServerPrerendered (server-side prerendering via Blazor Server) |
| **Dependencies** | CDN (Tailwind Play CDN) | NuGet (`NeoUI.Blazor`, `NeoUI.Blazor.Primitives`, `NeoUI.Icons.Lucide`) |
| **Location** | `src/Aero.Cms.Ui.Hyper/Blocks/` | `src/Aero.Cms.Ui.Neo/Blocks/` |
| **Palette** | "Hyper" section | "Neo" section (flat list) |

> **Note:** The app is a Blazor Web App (server + WASM). In .NET 10, `blazor.webassembly.js` / `blazor.server.js` script references are handled automatically by the framework — no manual script tag needed.

## Architectural Design — Deliberate Copy of `Aero.Cms.Ui.Hyper`

The `Aero.Cms.Ui.Neo` project is a deliberate architectural copy of `Aero.Cms.Ui.Hyper`.
Every pattern, convention, and infrastructure decision established in Hyper is replicated here:

| Pattern | Hyper Implementation | Neo Implementation |
|---|---|---|
| **Project type** | Razor Class Library (RCL) | Same |
| **Block directory** | `Blocks/{Slice}/` (e.g. `Blocks/Pricing/`) | Same |
| **File convention** | 6 files: `Block.cs`, `Renderer.razor`, `Mapper.cs`, `EditorBlockDefinition.cs`, `EditorPreview.razor`, `Editor.razor` | Same |
| **Block model** | Inherits `BlockBase`, has `BlockTypeId` constant, `[BlockMetadata]`, `Accept(IBlockVisitor)` | Same |
| **BlockTypeId** | `"hyper.{slice}.{n}"` | `"neo.{category}.{name}"` |
| **Renderers** | Static partial classes with `[CmsBlockRenderer]` markers in shared `RendererMarkers.cs` | Same |
| **Editor definitions** | `IPageEditorBlockDefinition` per block, registered in `*PageEditorBlockProvider.cs` | Same |
| **Provider** | `HyperPageEditorBlockProvider { Definitions + BlockModels }` | `NeoPageEditorBlockProvider { Definitions + BlockModels }` |
| **Palette source** | `NeoHyperCatalogItems` = `ToCatalogItem()` from provider definitions | `NeoCatalogItems` = `ToCatalogItem()` from provider definitions |
| **Registry** | `PageEditorBlockRegistry` (shared static `Dictionary`) | Same registry (no new one needed) |
| **Editor routing** | Registry-first via `PageEditorBlockRegistry.TryGet()`, switch-fallback | Same |
| **_Imports** | Per-namespace `@using` directives | Same |
| **Razor rendering** | Static SSR, code-behind `.razor.cs` preferred | Same code-behind pattern |

**Differences:**
- HyperUI blocks render raw HTML markup with Tailwind CDN classes — NeoUI blocks render actual NeoUI.Blazor components (`<Card>`, `<Button>`, `<DataTable>`, etc.)
- **Hero01Block is an exception**: despite living in `Aero.Cms.Ui.Neo`, its renderer emits raw HTML + Tailwind (identical to HyperUI blocks) — NOT NeoUI Blazor components. The 33 new blocks from Phases 1-6 WILL use actual NeoUI.Blazor components.
- NeoUI blocks use `ServerPrerendered` via the Component Tag Helper (consistent with the existing CMS public page rendering approach).
- NeoUI project references `NeoUI.Blazor` packages (HyperUI project needs only Tailwind CDN).

> **Original plan called for `InteractiveAuto` and a `PrefersInteractive` property. This has been **removed**. Neo blocks follow the same server-side prerendering design as current public CMS pages. See the revised Public Page Rendering section below.**

## Phase 0 — Foundation + Hero01 Extraction

### A. Create `Aero.Cms.Ui.Neo` RCL project

Mirror structure of `Aero.Cms.Ui.Hyper`:

```
src/Aero.Cms.Ui.Neo/
  Aero.Cms.Ui.Neo.csproj
  _Imports.razor
  NeoPageEditorBlockProvider.cs          (registry — IPageEditorBlockDefinition list + block model list)
  Blocks/
    RendererMarkers.cs                   (shared renderer markers)
    Hero/                                (first block — extracted from old location)
```

Project references:
- `Aero.Cms.Abstractions`
- `NeoUI.Blazor`
- `NeoUI.Blazor.Primitives`
- `NeoUI.Icons.Lucide`

Add as `<ProjectReference>` in:
- `Aero.Cms.Web.Client/Aero.Cms.Web.Client.csproj`
- `Aero.Cms.Web/Aero.Cms.Web.csproj`

### B. Extract Hero01Block → Neo RCL

Hero01Block is the **only** existing NeoUI block. It was dropped in as a test into the old Aero abstractions. Move all its files:

| From | To |
|---|---|
| `Abstractions/Blocks/Neo/Hero01Block.cs` | `Ui.Neo/Blocks/Hero/Hero01Block.cs` |
| `Abstractions/Blocks/Neo/Hero01BlockMapper.cs` | `Ui.Neo/Blocks/Hero/Hero01BlockMapper.cs` |
| `Shared/Blocks/Rendering/Hero01BlockRenderer.razor` | `Ui.Neo/Blocks/Hero/Hero01BlockRenderer.razor` |
| `Shared/.../AeroUi/Hero01/Hero01BlockEditor.razor` | `Ui.Neo/Blocks/Hero/Hero01BlockEditor.razor` |
| `Shared/.../AeroUi/Hero01/Hero01BlockEditorPreview.razor` | `Ui.Neo/Blocks/Hero/Hero01BlockEditorPreview.razor` |

Create 6th file: `Hero01EditorBlockDefinition.cs` implementing `IPageEditorBlockDefinition`.

Block model updates:
- Namespace: `Aero.Cms.Ui.Neo.Blocks.Hero`
- `[BlockMetadata]` uses `"aero.hero.01"`, `"Hero 01"`, `Category = "Neo"`, `SortOrder = 10`

Register in:
- `NeoPageEditorBlockProvider.cs` — add definition instance + block model registration
- `Blocks/RendererMarkers.cs` — add `[CmsBlockRenderer(typeof(Hero01Block))]` marker
- `_Imports.razor` — add `@using Aero.Cms.Ui.Neo.Blocks.Hero`

**Wiring updates** (registry-first, switch-fallback remains as safety net):
- `BlockEditorHost.razor` — `TryGetRegisteredEditor()` already checks `PageEditorBlockRegistry.TryGet()` first; Hero01's `EditorBlockDefinition` sets `PropertyEditorComponentType` so it resolves from registry
- `BlockEditorPreviewHost.razor` — same pattern, registry-first
- `EditorBlockMapper.cs` (Aero.Cms.Modules.Pages) — `PageEditorBlockRegistry.TryGet()` already runs first; switch at line 32 stays as fallback
- Source generator `BlockRendererGenerator.cs` — update hardcoded `AeroUi.Hero01` paths (see Source Generator Update section for revised approach)

### C. Rename `Neo/` → `AeroUI/` in Abstractions

The `Abstractions/Blocks/Neo/` folder contained Hero01Block (extracted above) plus these original Aero blocks:

| File | Notes |
|---|---|
| `ImageBlock.cs` | Media block |
| `VideoBlock.cs` | Media block |
| `AudioBlock.cs` | Media block |
| `GalleryBlock.cs` | Media block |
| `NeoRawHtmlBlock.cs` | UI primitive |
| `SeparatorBlock.cs` | UI primitive |
| `NeoColumnsBlock.cs` | Layout block |
| `ScribanBlock.cs` | Template block |
| `NeoCompositionBlock.cs` | Composition container |
| `NeoPageNode.cs` | Page node model |
| `NeoPageNodeKind.cs` | Node kind enum |
| `NeoCatalogIds.cs` | Catalog ID constants |
| `BasicHeroBlock.cs` | Will stay (not Hero01) |

**Rename plan:**
1. Rename folder: `Abstractions/Blocks/Neo/` → `Abstractions/Blocks/AeroUI/`
2. Update namespace: `Aero.Cms.Abstractions.Blocks.Neo` → `Aero.Cms.Abstractions.Blocks.AeroUI`
3. Update ALL references across codebase:
   - `Shared/Blocks/Rendering/RendererMarkers.cs` — using directives + type references
   - `Shared/Pages/Manager/PageEditor/BlockEditorHost.razor` — using + switch cases
   - `Shared/Pages/Manager/PageEditor/BlockEditorPreviewHost.razor` — using + switch cases
   - `Shared/Pages/Manager/PageEditor/PageEditor.razor.cs` — using directives
   - `Aero.Cms.Modules.Pages/EditorBlockMapper.cs` — using + switch cases
   - `Aero.Cms.SourceGenerators/BlockRendererGenerator.cs` — hardcoded paths
   - `Abstractions/Blocks/Serialization/BlockJsonContext.cs` — JsonSerializable attributes
4. Update `[BlockMetadata]` on all moved files — `Category = "Aero UI"` stays same (maps to legacy AeroUi section)

### D. Wire Palette + Editor Routing

**NeoPageEditorBlockProvider** structure (mirrors `HyperPageEditorBlockProvider`):

```csharp
public sealed class NeoPageEditorBlockProvider : IPageEditorBlockProvider, ICmsBlockModelProvider
{
    private static readonly IReadOnlyCollection<IPageEditorBlockDefinition> Definitions = [ ... ];
    private static readonly IReadOnlyCollection<CmsBlockModelRegistration> BlockModels = [ ... ];

    public IReadOnlyCollection<IPageEditorBlockDefinition> GetDefinitions() => Definitions;
    public IReadOnlyCollection<CmsBlockModelRegistration> GetBlockModels() => BlockModels;
}
```

**PageEditor.razor changes:**
Add "Neo" panel section after "Hyper" (line 343):

```razor
<div class="pe-category">
    <button class="pe-category-header" @onclick='() => ToggleCategory("neo")'>
        <svg>...</svg>
        @if (!RightSidebarCollapsed) { @:Neo <svg class="pe-chevron">...</svg> }
    </button>
    @if (CategoryNeo)
    {
        <PageEditorPaletteSection Items="NeoCatalogItems.ToList()"
                                  IsCollapsed="RightSidebarCollapsed" />
    }
</div>
```

**PageEditor.razor.cs changes:**
- Add `CategoryNeo` bool property (with toggle case)
- Add `NeoCatalogItems` property — sourced from `NeoPageEditorBlockProvider.Definitions` mapped via `ToCatalogItem()` (existing method)
- Add `using Aero.Cms.Ui.Neo;`

**BlockEditorHost.razor:**
The `TryGetRegisteredEditor()` method already checks `PageEditorBlockRegistry.TryGet()` first. When Hero01's definition is registered with `PropertyEditorComponentType`, it will resolve without needing a new switch case.

### E. Build Verify + User Test

- `dotnet build src/Aero.Cms.Ui.Neo` → 0 errors
- `dotnet build src/Aero.Cms.Shared` → 0 errors (source-generated catalog with `typeof()` references resolves)
- `dotnet build src/Aero.Cms.Web.Client` → 0 errors
- `dotnet build src/Aero.Cms.Web` → 0 errors
- Manually test: open page editor → Neo panel visible → Hero 01 draggable → properties panel works → preview renders

---

## Phases 1-6 — All 33 NeoUI Blocks (sequential)

All blocks follow the same 6-file pattern established in HyperUI:
```
Block.cs + Renderer.razor + Mapper.cs + EditorBlockDefinition.cs + EditorPreview.razor + Editor.razor
```

Block models use `[BlockMetadata("neo.{type}.{n}", "Display Name", Category = "Neo")]`.

Palette: flat list under "Neo" panel (no subcategories).

### Phase 1 — Hero & Marketing (5 blocks)

| # | Block ID | Display Name | Interactivity Needed? |
|---|---|---|---|
| 1 | `neo.hero.centered` | Centered Hero | Display only |
| 2 | `neo.hero.split` | Hero Split Layout | Display only |
| 3 | `neo.cta.banner` | CTA Banner | Display only |
| 4 | `neo.newsletter` | Newsletter Signup | Moderate (form) |
| 5 | `neo.stats.row` | Status / Social Row | Display only |

### Phase 2 — Forms & Auth (8 blocks)

| # | Block ID | Display Name | Interactivity Needed? |
|---|---|---|---|
| 6 | `neo.auth.signup` | Sign-Up Form | High (form + validation) |
| 7 | `neo.auth.signin` | Sign-In Form | High (form + validation) |
| 8 | `neo.auth.signin-split` | Sign-In Split Screen | High (form) |
| 9 | `neo.auth.forgot` | Forgot Password | Moderate (form) |
| 10 | `neo.auth.verify` | Verify Code | Moderate (form) |
| 11 | `neo.auth.locked` | Account Locked | Display only |
| 12 | `neo.form.address` | Address Form | High (multi-field form) |
| 13 | `neo.form.feedback` | Feedback Rating | Moderate (interactive rating) |

### Phase 3 — Commerce (3 blocks)

| # | Block ID | Display Name | Interactivity Needed? |
|---|---|---|---|
| 14 | `neo.commerce.cart` | Shopping Cart | High (stateful) |
| 15 | `neo.commerce.product` | Product Detail | Moderate |
| 16 | `neo.commerce.product-card` | Product Card | Display only |

### Phase 4 — Navigation (3 blocks)

| # | Block ID | Display Name | Interactivity Needed? |
|---|---|---|---|
| 17 | `neo.nav.marketing` | Responsive Marketing Nav | Moderate (mobile toggle) |
| 18 | `neo.nav.topbar` | Top Navigation Bar | Moderate (mobile toggle) |
| 19 | `neo.nav.breadcrumb` | Breadcrumb + Page Header | Display only |

### Phase 5 — Data & Tables (4 blocks)

| # | Block ID | Display Name | Interactivity Needed? |
|---|---|---|---|
| 20 | `neo.data.filterable` | Filterable Table | High (filter/sort) |
| 21 | `neo.data.simple` | Simple Data Table | Moderate (sort) |
| 22 | `neo.data.usermgmt` | User Management Table | High (CRUD) |
| 23 | `neo.data.pricing-compare` | Pricing Comparison | Moderate |

### Phase 6 — Content & Dashboard (10 blocks)

| # | Block ID | Display Name | Interactivity Needed? |
|---|---|---|---|
| 24 | `neo.content.features` | Feature Grid | Display only |
| 25 | `neo.content.features-row` | Feature Icon Row | Display only |
| 26 | `neo.content.pricing` | Pricing Cards | Display only |
| 27 | `neo.content.testimonials` | Testimonials Grid | Display only |
| 28 | `neo.content.contact` | Contact Us Basic | Moderate (form) |
| 29 | `neo.content.wizard` | Wizard | **High** (multi-step stateful) |
| 30 | `neo.content.order-confirm` | Order Confirmation | Display only |
| 31 | `neo.dashboard.analytics` | Analytics Dashboard | High (charts) |
| 32 | `neo.dashboard.metrics` | Metrics Board | High (live data) |
| 33 | `neo.dashboard.notifications` | Notifications Panel | Moderate |

> **Complexity flag:** Block #29 (Wizard) is the highest-complexity block — multi-step stateful UI with form validation across steps. Consider scoping this carefully or deferring to a later phase if it blocks overall progress.

---

## Key Implementation Notes

### Public Page Rendering

CSHTML public pages use the [Component Tag Helper](https://learn.microsoft.com/aspnet/core/mvc/views/tag-helpers/built-in/component-tag-helper) to embed a `PageBlockRenderer` Blazor wrapper component. The wrapper owns a `DynamicComponent` loop — the same mechanism the page editor already uses for canvas previews. No new rendering infrastructure is needed.

**Revised approach:** Neo blocks use `ServerPrerendered` on the Component Tag Helper (consistent with the existing CMS public page rendering design). There is no dynamic render-mode switching — no `PrefersInteractive` property, no `needsInteractive` check, no `InteractiveAuto`.

```
┌─────────────────────────────────────────────────────────┐
│ CSHTML Public Page (Component Tag Helper)                │
│                                                         │
│  <component type="typeof(PageBlockRenderer)"            │
│      render-mode="ServerPrerendered"                    │
│      param-Blocks="@page.Blocks" />                     │
│                                                         │
│  ─────────────── boots Blazor Server runtime ────────►  │
│                                                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │ PageBlockRenderer.razor                         │   │
│  │                                                 │   │
│  │  @rendermode InteractiveServer                  │   │
│  │                                                 │   │
│  │  @foreach (var block in Blocks)                 │   │
│  │  {                                              │   │
│  │      <DynamicComponent Type="def.PreviewComp"   │   │
│  │           Parameters="new { Block = block }" /> │   │
│  │  }                                              │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

> **⚠️ Critical correction from original:** `@rendermode` is a Razor directive attribute that can **only** be applied to a component instance or component definition — **not** to an HTML `<div>` element. The corrected approach applies `@rendermode InteractiveServer` at the `PageBlockRenderer.razor` **component definition level** (or on a Blazor component wrapper, not a plain `<div>`). This is confirmed by the [official Blazor render modes documentation](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0).

**Why `ServerPrerendered` (not `WebAssemblyPrerendered` or `InteractiveAuto`)?**

| Original Idea | Problem | Corrected Approach |
|---|---|---|
| `WebAssemblyPrerendered` on tag helper + `InteractiveAuto` inside | `WebAssemblyPrerendered` boots a pure WebAssembly runtime. Inside WASM, `InteractiveAuto` would attempt to establish a SignalR circuit — which doesn't work. These are incompatible execution models. | `ServerPrerendered` on tag helper establishes a SignalR circuit. Neo blocks render server-side with interactive capabilities via Blazor Server. |
| `InteractiveAuto` for WASM upgrade path | Adds complexity (needs `.Client` project verification, JS interop guarding for prerender phase). The existing CMS rendering pipeline already works with server-side prerendering. | Keep consistent with existing design. `ServerPrerendered` provides interactive Blazor Server rendering with no WASM complexity. |

| Scenario | Tag Helper | Internal | Result |
|---|---|---|---|
| All Hyper blocks | `Static` | N/A (no Blazor wrapper needed) | Pure SSR — zero Blazor runtime overhead |
| Any Neo block present | `ServerPrerendered` | `@rendermode InteractiveServer` on `PageBlockRenderer` | Server-rendered with interactive Blazor Server support |

**Parameter passing:** Block data passed via the tag helper's `param-*` attributes must be JSON-serializable. Complex block model types must have public parameterless constructors and settable properties. This is confirmed by the Component Tag Helper documentation.

### Dropped: `PrefersInteractive` Property

The original plan added a `bool PrefersInteractive` property to `IPageEditorBlockDefinition` to dynamically decide render mode. This has been **removed** for two reasons:

1. It was a breaking change — all existing `IPageEditorBlockDefinition` implementations (~90+ HyperUI definitions, all AeroUI definitions) would fail to compile without it.
2. Neo blocks now use `ServerPrerendered` consistently, matching the existing CMS public page rendering approach. No dynamic switching is needed.

If render-mode discrimination is needed later, a default interface implementation (`bool PrefersInteractive => false;`) could be added without breaking existing code.

### Editor Properties

NeoUI blocks have **no standard `EditorBlock` properties** (no `MainText`, `CtaText`, etc.). Instead, each block model exposes its own typed properties matching its NeoUI component parameters. The `EditorBlockDefinition` maps these directly without the `EditorBlock` intermediate object.

Example property mappings:
- `CenteredHeroBlock`: `Eyebrow`, `Title`, `Highlight`, `Description`, `PrimaryText`, `PrimaryUrl`, `SecondaryText`, `SecondaryUrl`, `TrustMarkers`
- `SignInBlock`: `Email`, `Password`, `RememberMe`, `ShowForgotPassword`
- `DataTableBlock`: `Query`, `ColumnDefinitions`, `Sortable`, `PageSize`

> **⚠️ JS interop guard note:** `ServerPrerendered` (like all interactive modes) supports prerendering by default. During the server prerender phase, the browser isn't running — `IJSRuntime` calls will fail. If any NeoUI component (`<Card>`, `<Button>`, `<DataTable>`, etc.) calls JS interop in `OnInitialized()` or `OnParametersSet()`, it will throw during the prerender pass. All Neo block renderers should guard JS interop: use `if (RendererInfo.IsInteractive)` before calling JS, or defer JS calls to `OnAfterRenderAsync(firstRender: true)`. This is documented as a convention for all Phase 1-6 block authors.

### Source Generator Update

**Original issue:** `BlockRendererGenerator.cs` hardcodes paths to `AeroUi.Hero01` (lines 249, 1092-1118). The original plan patched only Hero01 paths, but 33 new blocks would each need new hardcoded strings — this doesn't scale.

**Revised approach:**

After Phase 0:
- Hero01's preview/editor types will be resolved from the Neo RCL assembly
- Other blocks (ImageBlock, etc.) will continue to use `AeroUi` paths (renamed namespace)

For the 33 new Neo blocks (Phases 1-6), the source generator should be restructured to:
1. **Attribute-driven discovery via Category mapping**: Derive editor type names from the `[BlockMetadata]` `Category` value using a configurable `CategoryEditorNamespace` dictionary. This maps category strings to the assembly-qualified namespace prefix where editor components actually live (e.g., `"Neo"` → `Aero.Cms.Ui.Neo.Blocks.`). The block model's namespace already follows the pattern `Aero.Cms.Ui.Neo.Blocks.{FolderName}` — the generator infers the folder by stripping the `Block` suffix from the model type name (e.g., `"CenteredHeroBlock"` → `"CenteredHero"`).
2. **Fix `MapCatalogSection()`**: Add `"Neo" => "Neo"` to the switch so Neo blocks route to the `NeoEditorCatalogSection.Neo` palette section (the enum value already exists — the mapping was simply missing).
3. **Eliminate hardcoded paths**: Remove the `AeroUi.Hero01` base namespace from `GetBlockModelCandidate()` (line 249) and replace the `null` editor type assignments in `RenderNeoEditorCatalog()` (lines 1062-1063) with `typeof()` references derived from the category namespace mapping.

This eliminates the need to add new entries for each of the 33 blocks.

### Registry Pattern

Neo blocks use the same `IPageEditorBlockDefinition` interface as HyperUI blocks:

```csharp
public sealed class CenteredHeroEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "neo.hero.centered";
    public string DisplayName => "Centered Hero";
    public string? Description => "A NeoUI centered hero section.";
    public string Category => "Neo";
    public string Kind => "Block";
    public string IconName => "sparkles";
    public int SortOrder => 10;
    public bool PublicStaticSsrSafe => false;
    public Type? PreviewComponentType => typeof(CenteredHeroBlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(CenteredHeroBlockEditor);
    public EditorBlock CreateDefaultEditorBlock() => new() { Type = CatalogId, /* ... */ };
    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock) { /* ... */ }
    public BlockBase? ToBlockBase(EditorBlock editorBlock) { /* ... */ }
}
```

> **Note:** `PublicStaticSsrSafe` is `false` for Neo blocks because they use `ServerPrerendered` (not static SSR). The `PublicStaticSsrSafe` convention already exists in the codebase — no new property is needed.

### Naming Convention

| Prefix | Library | Example |
|---|---|---|---|
| `aero.hero.01` | Hero01 (renders raw HTML in Neo RCL) | `aero.hero.01` (Hero01Block) |
| `aero.{type}` | Aero UX (Meraki-based) | `aero_features`, `aero_cta` |
| `neo.{category}.{name}` | NeoUI blocks (use Blazor components) | `neo.hero.centered`, `neo.auth.signin` |
| `hyper.{category}.{n}` | HyperUI blocks | (existing, unchanged) |

---

## Known Considerations & Caveats

These items represent decisions made, risks identified, or open questions from the council review.

### Resolved by Project Design

| Item | Resolution |
|---|---|
| Client project structure | The app is a Blazor Web App (server + WASM) by design. `InteractiveWebAssembly` / `InteractiveAuto` are not being used for Neo blocks — `ServerPrerendered` only requires the server-side Blazor runtime. |
| Script references (`blazor.*.js`) | In .NET 10, the framework handles required Blazor scripts automatically. No manual `<script>` tags needed. |

### Implementation Risks to Monitor

| Risk | Mitigation |
|---|---|
| JS interop during `ServerPrerendered` prerender phase | Document guard rule: use `RendererInfo.IsInteractive` or `OnAfterRenderAsync(firstRender: true)` for all JS interop in Neo block renderers. Add to Phase 0 build-verify checklist. |
| Hero01 naming confusion | Hero01 is cataloged as `aero.hero.01` (not `neo.hero.01`) and lives in the Neo RCL but renders raw HTML (not NeoUI components). The other 33 Neo blocks use actual Blazor components with `neo.{category}.{name}` IDs. This is an accepted inconsistency for now. |
| Source generator complexity | The revised approach (category-namespace mapping + deriving folder from model type name) should be proven with Hero01 first before committing to all 33 blocks. Verify `typeof()` references in `GeneratedNeoEditorCatalog.g.cs` resolve correctly. |
| 33 blocks scope | Prioritize Phases 1 and 2 (Hero/Marketing + Forms/Auth) — these cover the most common CMS page needs. Phases 5-6 (Data/Tables + Dashboard) are the most complex and can be deferred. |
| Wizard block (#29) | Highest complexity of all 33 blocks. Explicitly flag for a separate design pass before implementation. |
| `Aero.Cms.Shared → Aero.Cms.Ui.Neo` project reference | REQUIRED for source-generated catalog to compile. `GeneratedNeoEditorCatalog.g.cs` is emitted into `Aero.Cms.Shared` and contains `typeof()` references to Neo editor component types. Add this `<ProjectReference>` during Phase 0A. |
