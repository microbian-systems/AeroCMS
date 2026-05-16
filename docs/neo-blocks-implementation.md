# NeoUI Blocks Implementation Plan

## Overview

Add NeoUI Blazor components as page editor blocks alongside the existing HyperUI blocks.
This is **additive** — both libraries coexist in the page editor palette and public rendering pipeline.

| | HyperUI | NeoUI |
|---|---|---|
| **Rendering** | Raw HTML + Tailwind CSS | NeoUI Blazor components (`<Card>`, `<Button>`, `<DataTable>`, etc.) |
| **SSR** | Static SSR safe | Requires `InteractiveServer` / `InteractiveWebAssembly` |
| **Dependencies** | CDN (Tailwind Play CDN) | NuGet (`NeoUI.Blazor`, `NeoUI.Blazor.Primitives`, `NeoUI.Icons.Lucide`) |
| **Location** | `src/Aero.Cms.Ui.Hyper/Blocks/` | `src/Aero.Cms.Ui.Neo/Blocks/` |
| **Palette** | "Hyper" section | "Neo" section (flat list) |

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
| **Palette source** | `NeoHyperCatalogItems` = `ToCatalogItem()` from provider definitions | `NeoNeoCatalogItems` = `ToCatalogItem()` from provider definitions |
| **Registry** | `PageEditorBlockRegistry` (shared static `Dictionary`) | Same registry (no new one needed) |
| **Editor routing** | Registry-first via `PageEditorBlockRegistry.TryGet()`, switch-fallback | Same |
| **_Imports** | Per-namespace `@using` directives | Same |
| **Razor rendering** | Static SSR, code-behind `.razor.cs` preferred | Same code-behind pattern |

**Differences:**
- HyperUI blocks render raw HTML markup with Tailwind CDN classes — NeoUI blocks render actual NeoUI.Blazor components (`<Card>`, `<Button>`, `<DataTable>`, etc.)
- NeoUI blocks require `InteractiveServer` render mode for public-facing pages (HyperUI blocks are static SSR safe)
- NeoUI project references `NeoUI.Blazor` packages (HyperUI project needs only Tailwind CDN)

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
- `[BlockMetadata]` stays as `"aero.hero.01"`, `"Hero 01"`, `Category = "Neo"`, `SortOrder = 10`

Register in:
- `NeoPageEditorBlockProvider.cs` — add definition instance + block model registration
- `Blocks/RendererMarkers.cs` — add `[CmsBlockRenderer(typeof(Hero01Block))]` marker
- `_Imports.razor` — add `@using Aero.Cms.Ui.Neo.Blocks.Hero`

**Wiring updates** (registry-first, switch-fallback remains as safety net):
- `BlockEditorHost.razor` — `TryGetRegisteredEditor()` already checks `PageEditorBlockRegistry.TryGet()` first; Hero01's `EditorBlockDefinition` sets `PropertyEditorComponentType` so it resolves from registry
- `BlockEditorPreviewHost.razor` — same pattern, registry-first
- `EditorBlockMapper.cs` (Aero.Cms.Modules.Pages) — `PageEditorBlockRegistry.TryGet()` already runs first; switch at line 32 stays as fallback
- Source generator `BlockRendererGenerator.cs` — update hardcoded `AeroUi.Hero01` paths (line 249, 1092-1118) to derive from the block's actual assembly location

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
public static class NeoPageEditorBlockProvider
{
    public static readonly IReadOnlyList<IPageEditorBlockDefinition> Definitions = new[] { ... };
    public static readonly IReadOnlyList<BlockModelRegistration> BlockModels = new[] { ... };
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
        <PageEditorPaletteSection Items="NeoNeoCatalogItems.ToList()"
                                  IsCollapsed="RightSidebarCollapsed" />
    }
</div>
```

**PageEditor.razor.cs changes:**
- Add `CategoryNeo` bool property (with toggle case)
- Add `NeoNeoCatalogItems` property — sourced from `NeoPageEditorBlockProvider.Definitions` mapped via `ToCatalogItem()` (existing method)
- Add `using Aero.Cms.Ui.Neo;`

**BlockEditorHost.razor:**
The `TryGetRegisteredEditor()` method already checks `PageEditorBlockRegistry.TryGet()` first. When Hero01's definition is registered with `PropertyEditorComponentType`, it will resolve without needing a new switch case.

### E. Build Verify + User Test

- `dotnet build src/Aero.Cms.Ui.Neo` → 0 errors
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

| # | Block ID | Display Name | Source Line |
|---|---|---|---|
| 1 | `neo.hero.centered` | Centered Hero | ~841 |
| 2 | `neo.hero.split` | Hero Split Layout | ~1066 |
| 3 | `neo.cta.banner` | CTA Banner | ~1950 |
| 4 | `neo.newsletter` | Newsletter Signup | ~1205 |
| 5 | `neo.stats.row` | Status / Social Row | ~691 |

### Phase 2 — Forms & Auth (8 blocks)

| # | Block ID | Display Name | Source Line |
|---|---|---|---|
| 6 | `neo.auth.signup` | Sign-Up Form | ~720 |
| 7 | `neo.auth.signin` | Sign-In Form | ~778 |
| 8 | `neo.auth.signin-split` | Sign-In Split Screen | ~1639 |
| 9 | `neo.auth.forgot` | Forgot Password | ~941 |
| 10 | `neo.auth.verify` | Verify Code | ~971 |
| 11 | `neo.auth.locked` | Account Locked | ~1721 |
| 12 | `neo.form.address` | Address Form | ~466 |
| 13 | `neo.form.feedback` | Feedback Rating | ~562 |

### Phase 3 — Commerce (3 blocks)

| # | Block ID | Display Name | Source Line |
|---|---|---|---|
| 14 | `neo.commerce.cart` | Shopping Cart | ~250 |
| 15 | `neo.commerce.product` | Product Detail | ~332 |
| 16 | `neo.commerce.product-card` | Product Card | ~405 |

### Phase 4 — Navigation (3 blocks)

| # | Block ID | Display Name | Source Line |
|---|---|---|---|
| 17 | `neo.nav.marketing` | Responsive Marketing Nav | ~587 |
| 18 | `neo.nav.topbar` | Top Navigation Bar | ~1343 |
| 19 | `neo.nav.breadcrumb` | Breadcrumb + Page Header | ~1413 |

### Phase 5 — Data & Tables (4 blocks)

| # | Block ID | Display Name | Source Line |
|---|---|---|---|
| 20 | `neo.data.filterable` | Filterable Table | ~13 |
| 21 | `neo.data.simple` | Simple Data Table | ~125 |
| 22 | `neo.data.usermgmt` | User Management Table | ~1450 |
| 23 | `neo.data.pricing-compare` | Pricing Comparison | ~1853 |

### Phase 6 — Content & Dashboard (10 blocks)

| # | Block ID | Display Name | Source Line |
|---|---|---|---|
| 24 | `neo.content.features` | Feature Grid | ~893 |
| 25 | `neo.content.features-row` | Feature Icon Row | ~1125 |
| 26 | `neo.content.pricing` | Pricing Cards | ~1756 |
| 27 | `neo.content.testimonials` | Testimonials Grid | ~1962 |
| 28 | `neo.content.contact` | Contact Us Basic | ~1160 |
| 29 | `neo.content.wizard` | Wizard | ~1221 |
| 30 | `neo.content.order-confirm` | Order Confirmation | ~183 |
| 31 | `neo.dashboard.analytics` | Analytics Dashboard | ~1909 |
| 32 | `neo.dashboard.metrics` | Metrics Board | ~1526 |
| 33 | `neo.dashboard.notifications` | Notifications Panel | ~1564 |

---

## Key Implementation Notes

### Render Mode

NeoUI blocks use interactive Blazor components. Public-facing pages rendering NeoUI blocks must use `InteractiveServer` render mode for the block area or the entire page. This is unlike HyperUI blocks which work with Static SSR.

### Editor Properties

NeoUI blocks have **no standard `EditorBlock` properties** (no `MainText`, `CtaText`, etc.). Instead, each block model exposes its own typed properties matching its NeoUI component parameters. The `EditorBlockDefinition` maps these directly without the `EditorBlock` intermediate object.

Example property mappings:
- `CenteredHeroBlock`: `Eyebrow`, `Title`, `Highlight`, `Description`, `PrimaryText`, `PrimaryUrl`, `SecondaryText`, `SecondaryUrl`, `TrustMarkers`
- `SignInBlock`: `Email`, `Password`, `RememberMe`, `ShowForgotPassword`
- `DataTableBlock`: `Query`, `ColumnDefinitions`, `Sortable`, `PageSize`

### Source Generator Update

The `BlockRendererGenerator.cs` hardcodes preview paths to `AeroUi.Hero01`. After Phase 0:
- Hero01's preview/editor types will be resolved from the Neo RCL assembly
- Other blocks (ImageBlock, etc.) will continue to use `AeroUi` paths (renamed namespace)
- Long-term: the generator should derive paths from the `[BlockMetadata]` category or the block's assembly rather than hardcoded strings

### Registry Pattern

Neo blocks use the same `IPageEditorBlockDefinition` interface as HyperUI blocks:
```csharp
public sealed class CenteredHeroEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string BlockType => "neo.hero.centered";
    public string DisplayName => "Centered Hero";
    public Type BlockType => typeof(CenteredHeroBlock);
    public Type EditorPreviewComponentType => typeof(CenteredHeroBlockEditorPreview);
    public Type PropertyEditorComponentType => typeof(CenteredHeroBlockEditor);
    public CenteredHeroBlock ToBlockBase(EditorBlock editor) => new() { ... };
    public EditorBlock ToEditorBlock(BlockBase block) => ...;
}
```

### Neonaming Convention

| Prefix | Library | Example |
|---|---|---|
| `aero.hero.*` | Aero original (legacy) | `aero.hero.01` (Hero01Block — moves to Neo RCL) |
| `aero.{type}` | Aero UX (Meraki-based) | `aero_features`, `aero_cta` |
| `neo.{category}.{name}` | NeoUI blocks | `neo.hero.centered`, `neo.auth.signin` |
| `hyper.{category}.{n}` | HyperUI blocks | (existing, unchanged) |
