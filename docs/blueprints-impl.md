# BlueprintUI — PageEditor Palette Integration Plan

> **Status:** Planned — supersedes the paused NeoUI work. Revisit NeoUI (see `neo-blocks-transitory.md`) once Blueprint is stable.
>
> **Related docs:**
> - [`ui/`](../ui/) — BlazorBlueprint source checkout (reference only, not a project reference)
> - [`neo-blocks-transitory.md`](neo-blocks-transitory.md) — prior NeoUI integration doc (pattern template)
> - [`aero-blocks-renderers-neoui.md`](aero-blocks-renderers-neoui.md) — NeoUI renderer contract (adapt for Blueprint)

---

## Overview

Add BlazorBlueprint UI components (`BlazorBlueprint.Components` + `BlazorBlueprint.Primitives`) as **canvas blocks in the PageEditor palette sidebar** — same pattern as NeoUI, HyperUI, and Custom sections. Users drag Blueprint components from the palette onto the page editor canvas alongside existing block families.

| Dimension | BlueprintUI | NeoUI (reference) |
|---|---|---|
| **Styled components** | `BlazorBlueprint.Components` (115+ components) | `NeoUI.Blazor` |
| **Headless primitives** | `BlazorBlueprint.Primitives` (15+ primitives) | `NeoUI.Blazor.Primitives` |
| **Icons** | `BlazorBlueprint.Icons.Lucide` (1640+ icons) | `NeoUI.Icons.Lucide` |
| **Rendering** | Styled NuGet components + static SSR fallback | Same |
| **SSR** | ServerPrerendered (same as NeoUI/Hyper) | ServerPrerendered |
| **Source checkout** | `ui/` (reference only) | `NeoUI/` (reference only) |
| **RCL** | `src/Aero.Cms.Ui.Blueprint/` (planned) | `src/Aero.Cms.Ui.Neo/` |
| **Palette section** | `"Blueprint"` — new collapsible category in sidebar | `"Aero UI"`, `"Hyper"`, `"Neo"` sections |

> **Scope:** Canvas blocks only. Radzen and NeoUI remain untouched. Blueprint components are added as drag-to-canvas palette items — no editor shell changes.

### Why Blueprint Instead of NeoUI

- **Active development** — BlazorBlueprint is newer, actively maintained, ships releases weekly, has 38k+ NuGet downloads
- **shadcn/ui ecosystem** — works with `tweakcn.com` for visual theme editing
- **MCP server** — includes a built-in MCP server + `llms.txt` for AI tooling integration (Claude, Cursor, Copilot, Windsurf)
- **Two-tier architecture** — headless primitives for custom rendering + pre-styled components for rapid development
- **Tailwind native** — all components use Tailwind CSS (already our CSS framework)
- **Enterprise features** — Data Grid, Dynamic Forms, Filter Builder, Form Wizard, Dashboard Grid

---

## Library Reference

| Package | NuGet | Version | Purpose |
|---|---|---|---|
| `BlazorBlueprint.Components` | [link](https://www.nuget.org/packages/BlazorBlueprint.Components) | 3.12.1 | 115+ styled shadcn/ui components |
| `BlazorBlueprint.Primitives` | [link](https://www.nuget.org/packages/BlazorBlueprint.Primitives) | 3.12.0 | 15+ headless unstyled primitives (ARIA, keyboard) |
| `BlazorBlueprint.Icons.Lucide` | [link](https://www.nuget.org/packages/BlazorBlueprint.Icons.Lucide) | 2.0.0 | 1640+ Lucide SVG icons |
| `BlazorBlueprint.Icons.Heroicons` | [link](https://www.nuget.org/packages/BlazorBlueprint.Icons.Heroicons) | 2.0.0 | 1288 Heroicons (4 variants) |
| `BlazorBlueprint.Icons.Feather` | [link](https://www.nuget.org/packages/BlazorBlueprint.Icons.Feather) | 2.0.0 | 286 Feather icons |
| `BlazorBlueprint.Templates` | [link](https://www.nuget.org/packages/BlazorBlueprint.Templates) | 3.1.0 | `dotnet new` project templates |

> **Component prefix:** All components use the `Bb` prefix (`<BbButton>`, `<BbCard>`, `<BbDataTable>`, `<BbDialog>`, `<BbAccordion>`, etc.). Both styled components and headless primitives share the same prefix.

### Key Feature Pages

- **Components catalog:** https://blazorblueprintui.com/components — 115+ components across 9 categories
- **Blueprints (full page layouts):** https://blazorblueprintui.com/blueprints — 60+ production-ready page compositions
- **MCP setup:** `npx @blazorblueprint/mcp@latest` — built-in MCP server for AI tooling
- **Theming:** Works with `tweakcn.com` for visual theme editing → CSS variables

---

## Source Reference (`ui/`)

The `ui/` directory at repo root is a **git submodule** pointing to:

```
https://github.com/blazorblueprintui/ui
```

```
components/v3.10.2
```

**Rules:**
- The submodule is **reference only** — do not add `<ProjectReference>` to any of its `.csproj` files
- All consumption happens via **NuGet packages** (`BlazorBlueprint.Components`, etc.)
- Use the source to inspect component internals, rendering patterns, CSS variables, and available slots

---

## Integration Architecture

### 1. Create `Aero.Cms.Ui.Blueprint` RCL

Following the deliberate-copy pattern established by `Aero.Cms.Ui.Neo` (which copied `Aero.Cms.Ui.Hyper`):

```
src/Aero.Cms.Ui.Blueprint/
  Aero.Cms.Ui.Blueprint.csproj
  _Imports.razor
  BlueprintUiServiceCollectionExtensions.cs
  BlueprintPageEditorBlockProvider.cs
  BlueprintCmsBlockRenderRegistry.cs
  Blocks/
    RendererMarkers.cs
    {BlockName}/
      Block.cs
      Renderer.razr + Renderer.razor.cs
      Mapper.cs
      EditorBlockDefinition.cs
      EditorPreview.razr + EditorPreview.razor.cs
      Editor.razr + Editor.razor.cs
  Primitives/
    {PrimitiveName}/
      PrimitiveDefinition.cs
      PrimitivePreview.razr
  Definitions/
    {Name}EditorBlockDefinition.cs
  Embed/
    (YouTubeEmbedResolver, VimeoEmbedResolver, etc. — copy from Aero.Cms.Ui.Neo)
```

**csproj references:**
```xml
<PackageReference Include="BlazorBlueprint.Components" Version="3.12.1" />
<PackageReference Include="BlazorBlueprint.Primitives" Version="3.12.0" />
<PackageReference Include="BlazorBlueprint.Icons.Lucide" Version="2.0.0" />
<ProjectReference Include="..\Aero.Cms.Abstractions\Aero.Cms.Abstractions.csproj" />
<ProjectReference Include="..\Aero.Cms.Shared\Aero.Cms.Shared.csproj" />
<!-- Source generator references as needed -->
```

This RCL must be added as a `<ProjectReference>` in:
- `Aero.Cms.Web.Client/Aero.Cms.Web.Client.csproj`
- `Aero.Cms.Web/Aero.Cms.Web.csproj`

### 2. Palette Section — "Blueprint"

Add a new collapsible `pe-category` section in `PageEditor.razor` alongside the existing "Custom", "Aero UI", "Primitives", "Components", "References", "Hyper", and "Neo" sections:

**In `PageEditor.razor.cs`:**
```csharp
private IReadOnlyList<NeoEditorCatalogItem> BlueprintCatalogItems =>
    DefinitionRegistry.AllDescriptors
        .Select(d => d.ToCatalogItem())
        .Where(i => i.Section == NeoEditorCatalogSection.Blueprint)
        .ToList();
```

**In `PageEditor.razor`** (insert after the Neo section, around line 658):
```razor
@* ── Blueprint section ── *@
<div class="pe-category">
    <button class="pe-category-header" @onclick='() => ToggleCategory("blueprint")'
            data-pe-tooltip-target
            data-pe-tooltip="@(RightSidebarCollapsed ? L["Blueprint"] : null)"
            data-pe-tooltip-placement="left">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="3" y="3" width="18" height="18" rx="2"/>
            <path d="M12 8v8M8 12h8"/>
            <path d="M3 12h3M18 12h3"/>
            <path d="M12 3v3M12 18v3"/>
        </svg>
        @if (!RightSidebarCollapsed)
        {
            @L["Blueprint"]
            <svg class="pe-chevron @(CategoryBlueprint ? "rotated" : "")" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <polyline points="6 9 12 15 18 9"/>
            </svg>
        }
    </button>
    @if (!RightSidebarCollapsed && CategoryBlueprint)
    {
        <PageEditorPaletteSection Items="FilterPaletteItems(BlueprintCatalogItems).ToList()"
                                  IsCollapsed="RightSidebarCollapsed"
                                  OnDragStarted="OnPaletteDragStarted"
                                  OnDragEnded="OnPaletteDragEnded"
                                  OnAdd="AddPaletteItemAsync" />
    }
</div>
```

### 3. Service Registration

Following the same two-tier registration as NeoUI:

**Tier 1 — Blueprint library services** (called in host projects):
```csharp
builder.Services.AddBlueprintComponents();      // from BlazorBlueprint.Components.Extensions
builder.Services.AddBlueprintPrimitives();      // from BlazorBlueprint.Primitives.Extensions
```

**Tier 2 — AeroCMS Blueprint block services** (called in host projects):
```csharp
services.AddAeroCmsBlueprintUiBlocks();
```

Registers:
- `BlueprintPageEditorBlockProvider` as `IPageEditorBlockProvider`, `IPageEditorDefinitionProvider`, `ICmsBlockModelProvider`
- `BlueprintCmsBlockRenderRegistry` as `ICmsBlockRenderRegistry`
- Embed resolver pipeline (inherited pattern from NeoUI/Hyper)

**Registration sites** (mirror `AddAeroCmsNeoUiBlocks()` calls):
- `Aero.Cms.Web.Client/Program.cs`
- `AeroCmsExtensions.cs`
- `MauiProgram.cs`
- `SetupAppFactory.cs`

### 4. Catalog & Definition Model

Blueprint blocks follow the existing `NeoEditorCatalogItem` record model:

| Property | Value |
|---|---|
| `CatalogId` | `"blueprint.{category}.{name}"` |
| `Section` | `NeoEditorCatalogSection.Blueprint` |
| `Kind` | `Block` / `Primitive` |
| `EditorPreviewComponentType` | Blueprint component rendered inside palette |
| `PropertyEditorComponentType` | Blueprint component property panel (design/content/advanced tabs) |
| `PublicRendererComponentType` | Outer rendering wrapper (SSR safe) |

### 5. NeoEditorCatalogSection Enum Extension

Add `Blueprint` to the section enum:

```csharp
public enum NeoEditorCatalogSection
{
    AeroUi,
    Primitives,
    Components,
    Hyper,
    Neo,
    Blueprint   // new
}
```

Update `NeoCatalogSectionMapper` / `ToCatalogSection()` to handle `"blueprint"`, `"blueprintui"`, `"blueprint ui"`.

### 6. Code-Behind State (PageEditor.razor.cs)

Add a `CategoryBlueprint` property (matching the existing `CategoryCustom`, `CategoryAeroUi`, etc. pattern):

```csharp
private bool CategoryBlueprint { get; set; } = true;
```

Update `ToggleCategory` to handle `"blueprint"`.

---

## Implementation Phases

### Phase 0 — Foundation

- [ ] Add `BlazorBlueprint.Components`, `BlazorBlueprint.Primitives`, `BlazorBlueprint.Icons.Lucide` NuGet references to the host projects
- [ ] Call `builder.Services.AddBlueprintComponents()` and `AddBlueprintPrimitives()` in all host entrypoints
- [ ] Verify components render in a test page (e.g., `<BbButton Variant="Primary">Hello Blueprint</BbButton>`)
- [ ] Add `Blueprint` to `NeoEditorCatalogSection` enum
- [ ] Create `Aero.Cms.Ui.Blueprint` RCL project (bare minimum structure, no blocks yet)
- [ ] Add palette section stub in `PageEditor.razor` + `PageEditor.razor.cs`
- [ ] Wire up service registration in all host projects

### Phase 1 — Canvas Block Primitives

- [ ] Register `BbContainer` / layout primitives
- [ ] Register `BbText`, `BbHeading`, `BbTypography` for rich text blocks
- [ ] Register `BbButton`, `BbButtonGroup` for CTA blocks
- [ ] Register `BbBadge`, `BbAvatar` for decorative blocks
- [ ] Register `BbSeparator`, `BbSpacer` for layout blocks
- [ ] Register `BbCard`, `BbCardHeader`, `BbCardContent`, `BbCardFooter` for card-based blocks
- [ ] Register `BbAccordion`, `BbCollapsible`, `BbTabs` for content toggles
- [ ] Register `BbAspectRatio`, `BbImage` for media blocks
- [ ] Register `BbIcon` / `BbLucideIcon` for icon blocks

### Phase 2 — Canvas Block Components (Styled)

- [ ] `BbHeroSection` — centered hero, split hero, basic hero
- [ ] `BbCTASection` — call-to-action banners
- [ ] `BbPricingSection` — pricing tables
- [ ] `BbTestimonialSection` — testimonial carousels
- [ ] `BbFAQSection` — accordion-based FAQ
- [ ] `BbNewsletterSection` — email signup
- [ ] `BbStatsSection` — statistics row
- [ ] `BbTeamSection` — team grid
- [ ] `BbBlogSection` — blog post list
- [ ] `BbContactSection` — contact forms
- [ ] `BbFooterSection` — footer layouts
- [ ] `BbNavSection` — navigation menus

### Phase 3 — Data Blocks

- [ ] `BbDataTable` — sortable, filterable, paginated data tables for listing pages
- [ ] `BbDataGrid` — high-performance grid for admin data views
- [ ] `BbDataView` — list/grid toggle views
- [ ] `BbPagination` — pagination controls
- [ ] `BbFilterBuilder` — visual query builder
- [ ] `BbTimeline` — chronological content displays

### Phase 4 — Enterprise Components

- [ ] `BbDashboardGrid` — dashboard-style layouts
- [ ] `BbChart` — chart components (ApexCharts with shadcn theming)
- [ ] `BbDynamicForms` — schema-driven form blocks
- [ ] `BbFormWizard` — multi-step form wizards
- [ ] `BbFileUpload` — drag-and-drop file upload

### Phase 5 — Asset & Embed Blocks

- [ ] Register `BbCarousel` for image galleries
- [ ] Register `BbVideo` / embed resolver for YouTube/Vimeo
- [ ] Register `BbAudio` for audio embeds
- [ ] Register `BbMap` for Google Maps embed

---

## Key Design Decisions

### 1. Palette Only — No Editor Shell Changes

Blueprint is added as a **new palette section** alongside existing ones. The editor chrome (tabs, dialogs, toasts, etc.) continues to use Radzen and the existing pe-* CSS. No Radzen replacement.

### 2. SSR Rendering

Same approach as NeoUI: Blueprint blocks use `ServerPrerendered` render mode via the Component Tag Helper. The outer rendering wrapper is static-SSR safe even when the inner Blueprint component requires interactivity.

### 3. Theme Compatibility

Blueprint components use CSS variables for theming (shadcn/ui standard). The existing AeroCMS theme variables (`--pe-*`, `--ae-*`) should be mapped to shadcn CSS variable names (`--background`, `--foreground`, `--primary`, `--card`, etc.) or aliased in `site.css`. If conflicts arise, wrap Blueprint components in an isolated scope using `<BbTheme>` with custom variable mapping.

### 4. Localization

Blueprint has built-in `IBbLocalizer` support — use this for all editor-facing strings. The existing AeroCMS `L[...]` localization can wrap `IBbLocalizer` for string overrides.

### 5. Block Provider Pattern

`BlueprintPageEditorBlockProvider` will register both:
- **Styled components** as `NeoEditorCatalogKind.Block` (e.g., `BbCard`, `BbAccordion`, `BbPricingSection`)
- **Headless primitives** as `NeoEditorCatalogKind.Primitive` (e.g., `BbCollapsible`, `BbPopover`, `BbTooltip`)

Both appear under the "Blueprint" collapsible category in the palette sidebar, sorted by `SortOrder`.

---

## Component Prefix Reference

| Prefix | Library | Example |
|---|---|---|
| `Bb` | `BlazorBlueprint.Components` — styled components | `<BbButton>`, `<BbCard>`, `<BbDialog>`, `<BbDataTable>` |
| `Bb` | `BlazorBlueprint.Primitives` — headless primitives | `<BbAccordion>`, `<BbCollapsible>`, `<BbPopover>` |
| `Bb` | `BlazorBlueprint.Icons.Lucide` — Lucide icons | `<BbLucideIcon Name="heart" />` |

All three tiers share the `Bb` prefix. Disambiguate by namespace / import in `_Imports.razor`.

---

## Things to Investigate

- [ ] Does `AddBlueprintComponents()` registration conflict with Radzen or NeoUI service registrations? (Likely no — different DI keys)
- [ ] Does the Blueprint CSS reset conflict with existing CMS Tailwind styles?
- [ ] Can we use `<BbTheme>` scoping to isolate Blueprint CSS variables from the global CMS theme?
- [ ] What is the tree-shaking story for unused components? (NuGet pulls all assemblies — consider AOT/trimming)
- [ ] Does the MCP server (`@blazorblueprint/mcp`) work with LLM tooling (Claude Code, Cursor)? If so, document setup in `AGENTS.md` or a `.opencode/` skill.

---

## Acceptance Criteria

- [ ] `Aero.Cms.Ui.Blueprint` RCL builds without errors
- [ ] All host projects reference Blueprint NuGet packages
- [ ] Palette renders a "Blueprint" section with collapsible categories, alongside existing sections
- [ ] At least one Blueprint canvas block renders in the PageEditor preview and on the public page
- [ ] No regression in existing HyperUI, NeoUI, or Radzen components
- [ ] Existing editor chrome (tabs, dialogs, toasts) is completely unchanged

---

## NEO On-Hold Note

> All NEO-related work (`neo-blocks-transitory.md`, `neo-blocks-future-plan.md`, `neo-blocks-template.md`) is **paused**. The `Aero.Cms.Ui.Neo` RCL, `NeoUI/` submodule, and existing NeoUI-based blocks remain in the codebase as-is. No further NeoUI block development will proceed until Blueprint integration is stable. The NeoUI documentation is preserved for reference patterns only.
