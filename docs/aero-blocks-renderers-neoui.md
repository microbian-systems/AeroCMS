# Aero CMS - Blocks, Renderers, PageEditor, and NeoUI Refactor

## Purpose

This document is the implementation contract for refactoring Aero CMS page-editor and block-renderer functionality while integrating NeoUI.

The refactor is intentionally scoped to:

- `src/Aero.Cms.Shared/Pages/Manager/PageEditor`
- block editor previews and property panels
- public block renderer components
- source-generated block metadata/registration where it supports the editor and renderer

This is not a full manager-shell rewrite, not a site-builder rewrite, and not a public theming module implementation.

## Executive Decision

The design direction is a breaking PageEditor and block-authoring refactor.

Do not preserve the old PageEditor block-authoring functionality as a compatibility target. The new editor should rebuild page blocks around NeoUI primitives/components and a new block composition model.

Keep:

- semantic block models
- per-block editor components
- per-block editor-preview components
- NeoUI for editor chrome, property panels, dialogs, sheets, tooltips, sortable lists, canvas previews, and component composition affordances
- public output as static SSR Razor component output
- CSS-variable theming and RTL logical-property guidance
- source-generated block metadata and registry support

Change from the earlier proposal:

- Do not introduce a new `IBlockVisitor` rendering pipeline.
- Do not rebuild public rendering as ViewComponents.
- Do not extend the legacy `RenderBlock(EditorBlock block, bool isSelected)` switch.
- Do not keep the old block editor as a fallback surface.
- Do not keep `Page.cshtml` as the long-term public page host.
- Do not carry legacy Aero/Boring block types into the new authoring model unless they are recreated as Neo blocks.

The target public rendering pipeline is:

```text
Routable static SSR Razor component
  -> PageRenderView.razor
  -> LayoutRegionRenderer.razor
  -> LayoutColumnRenderer.razor
  -> BlockPlacementRenderer.razor
  -> BlockRenderer.razor
  -> CmsBlockRenderRegistry
  -> ICmsBlockRenderAdapter
  -> block-specific Razor renderer
```

## Current Codebase Anchors

Before implementing, verify these files in the current checkout:

- `src/Aero.Cms.Modules.Pages/Areas/Cms/Pages/Page.cshtml`
  - current public page host; replace this with static SSR `.razor` routing during this refactor
- `src/Aero.Cms.Modules.Pages/Areas/Cms/Pages/Page.cshtml.cs`
  - current `PagesPolicy` output-cache endpoint anchor
- `src/Aero.Cms.Modules.OutputCache/OutputCacheModule.cs`
  - registers ASP.NET Core output caching, named CMS policies, and `UseOutputCache`
- `src/Aero.Cms.Modules.OutputCache/Caching/CmsOutputCachePolicy.cs`
  - custom policy for anonymous public CMS GET/HEAD responses, manager/admin exclusions, and cache diagnostics
- `src/Aero.Cms.Shared/Blocks/Rendering/ICmsBlockRenderAdapter.cs`
  - current public renderer adapter contract
- `src/Aero.Cms.Shared/Blocks/Rendering/BlockRenderer.razor`
  - resolves adapters through `CmsBlockRenderRegistry` and wraps rendering in `ErrorBoundary`
- `src/Aero.Cms.SourceGenerators/BlockRendererGenerator.cs`
  - generates block manifests, renderer adapters, registry, factory, and `BlockBase` polymorphic metadata
- `src/Aero.Cms.Core.Entities/PageDocument.cs`
  - persists both `LayoutRegions` and editor `Blocks`
- `src/Aero.Cms.Modules.Pages/EditorBlockMapper.cs`
  - maps flat `EditorBlock` data into typed `BlockBase` instances
- `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor`
  - current right-sidebar, canvas, preview overlay, and monolithic block render switch
- `src/Aero.Cms.Shared/Pages/Manager/PageEditor/PageEditor.razor.cs`
  - current editor state, drag/drop, preview, save, publish, and `EditorBlock` lifecycle
- `NeoUI/README.md`
- `NeoUI/THEMING.md`
- `NeoUI/llms/*.txt`
  - current LLM docs are primarily DataGrid/theme focused; use them where editor grids and theme-token behavior are involved

## Core Architecture

### Blocks Are Semantic Content

A block describes content and intent. It should not describe a specific UI library component.

Good block fields:

- title, body, summary, caption
- media IDs or URLs
- semantic action roles
- semantic layout intent such as `HeroLayout.Centered`
- display order
- references to content, forms, navigation, or media

Avoid on block models:

- Tailwind classes
- NeoUI component names
- pixel spacing
- animation names
- opacity/parallax flags
- hardcoded color values
- renderer-specific CSS class names

Some existing blocks violate this today. Treat them as legacy input, not as the design baseline for the new editor.

### Public Rendering Uses Blazor Static SSR Razor Components

Public page rendering should move from `.cshtml` hosting to routable `.razor` components using Blazor static SSR.

Do not replace this with per-block `ViewComponent` classes. Static SSR Razor components are the modern view layer for public CMS pages.

The public site should not apply an interactive render mode at the page or layout level. In a Blazor Web App, a component with no `@rendermode` renders using static server-side rendering when no ancestor assigns interactivity. Microsoft Learn describes static SSR as server rendering with no interactivity, while Interactive Server uses server interactivity and Interactive WebAssembly/Auto introduce client-side runtime behavior.

Target public page shape:

```razor
@page "/{*path}"

<SeoHead Model="Page.Seo" />
<PageRenderView Page="Page" />
```

Target service/endpoint shape:

```csharp
builder.Services.AddRazorComponents();

app.MapRazorComponents<App>();
```

If the manager/admin app or public islands need interactivity, register the interactive modes app-wide, but apply them only to the manager surface or explicit island components:

```csharp
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();
```

Public content pages should remain static SSR by default. Use `@rendermode` only for explicitly interactive islands.

The block rendering layer should still use a registry/adapter architecture:

```csharp
public interface ICmsBlockRenderAdapter
{
    string BlockType { get; }
    Type ModelType { get; }
    RenderFragment Render(IBlock block, BlockRenderContext context);
}
```

Add a typed adapter variant as an additive improvement:

```csharp
public interface ICmsBlockRenderAdapter<TBlock> : ICmsBlockRenderAdapter
    where TBlock : BlockBase
{
    RenderFragment Render(TBlock block, BlockRenderContext context);
}
```

The source generator should emit adapters that implement both interfaces:

```text
HeroBlockRenderAdapter : ICmsBlockRenderAdapter<HeroBlock>
```

`BlockRenderer.razor` can continue to depend on `ICmsBlockRenderAdapter`, but it should be hosted from static SSR Razor components instead of `Page.cshtml`.

### Do Not Build a New Visitor Pipeline

`BlockBase.Accept(IBlockVisitor)` exists today, but the current `IBlockVisitor` only exposes `Visit(BlockBase)`. It does not provide compile-time dispatch per block type, and it does not integrate with the live Blazor SSR renderer.

Do not expand it into a second rendering system.

If traversal is needed later for non-rendering behavior such as "find all image references" or "collect links," implement that as a standalone traversal service. Do not put rendering dispatch back onto every block model.

### Editor Preview and Public Rendering Are Separate

The editor canvas and the public site have different jobs:

```text
Editor canvas
  - interactive Blazor
  - can use NeoUI components
  - optimized for authoring clarity
  - shows simplified previews

Public site
  - static SSR output
  - no manager/editor NeoUI dependency in page output
  - static-renderable NeoUI primitives/components allowed only when they meet public renderer rules
  - optimized for speed, SEO, and theming
  - uses block-specific Razor renderers through adapters
```

The editor preview may resemble the public output, but it is not required to be pixel-perfect.

**Note:** Preserve quick block prerender behavior for all new editor blocks, as described in `aero-manager/page-editor-port.md`. Editor preview components should give authors a visual approximation of each block, not merely a metadata card. Neo-based blocks are the required target: every new block should provide a lightweight canvas preview that shows enough layout, media, typography, copy, and action treatment for the author to understand the block at a glance. Legacy blocks do not need equivalent editor support unless they are recreated as Neo blocks. These previews are still not full public page renders and should avoid public layout wrappers, expensive data loading, and pixel-perfect theme behavior.

### Editor Preview Modernization

The current inline preview approach should be removed from the new editor. Do not modernize by slowly adding more cases to the large `RenderBlock(EditorBlock block, bool isSelected)` switch.

Neo block prerendering should use explicit editor-preview components:

```text
BlockEditorPreviewHost
  -> maps block/component type to preview component metadata
  -> passes block/component node state plus editor services/events
  -> renders {Name}BlockEditorPreview.razor
```

Each Neo block should have:

- a public renderer for published output
- an editor preview component for the PageEditor canvas
- an editor/property component for structured editing controls
- a mapper or adapter boundary that converts editor composition state to the typed block model when needed

The editor preview component may internally reuse the public renderer when that is cheap and visually useful, but it must remain editor-owned. It can add selection chrome, inline affordances, placeholders, empty-state visuals, simplified data, and NeoUI controls without leaking those concerns into public rendering.

## PageEditor Refactor

### Current Problem

`PageEditor.razor` currently combines:

- page metadata UI
- right sidebar palette
- drag/drop state
- block canvas
- block toolbar actions
- block preview rendering
- per-block property editing
- preview overlay
- save/publish controls

It also contains a large `RenderBlock(EditorBlock block, bool isSelected)` switch with many inline render fragments. This is the main refactor target.

### Target Shape

Refactor `PageEditor` into an orchestrator:

```text
PageEditor.razor
  -> PageEditorHeader
  -> PageEditorTabs
  -> PageEditorCanvas
       -> EditorBlockFrame
       -> BlockEditorPreviewHost
            -> {Name}BlockEditorPreview.razor
  -> PageEditorPropertyPanel
       -> BlockEditorHost
            -> {Name}BlockEditor.razor
  -> BlockPalette
       -> generated block metadata
  -> PreviewOverlay
```

Keep `PageEditor.razor.cs` responsible for top-level state orchestration at first:

- selected block
- dirty state
- save/publish flow
- preview refresh
- page metadata
- current site preview URL resolution

Move block-specific editing and preview behavior out of the main file.

### Preserve Existing Interaction Model

The current PageEditor interaction model remains the baseline:

- right sidebar palette
- collapsed-sidebar tooltips
- drag blocks from the sidebar into the canvas
- click blocks to select
- selected block exposes a toolbar
- selected block exposes editable properties
- preview overlay uses the existing preview service and draft page URL behavior

Do not redesign the editor around a different interaction model unless the user explicitly approves that architectural decision.

### Replace EditorBlock With a Neo Composition Model

`EditorBlock` is part of the legacy PageEditor design. Do not make it the permanent DTO for the new Neo editor.

The new editor should persist a structured composition model that can represent:

- full page blocks
- layout/container nodes
- NeoUI primitives
- NeoUI components
- nested children
- component properties
- design tokens and semantic variants

Do not persist arbitrary Razor source, arbitrary HTML, arbitrary C# expressions, or runtime component type names supplied by users. Persist stable catalog identifiers and validated property bags.

Example shape:

```csharp
public sealed class NeoPageNode
{
    public string NodeId { get; set; } = string.Empty;
    public string CatalogId { get; set; } = string.Empty;
    public NeoNodeKind Kind { get; set; }
    public Dictionary<string, JsonElement> Properties { get; set; } = [];
    public List<NeoPageNode> Children { get; set; } = [];
}

public enum NeoNodeKind
{
    Block,
    Layout,
    Primitive,
    Component
}
```

The exact persisted type names can change during implementation, but the model must be structured, versioned, and generated/validated from a trusted catalog.

### Legacy Block Removal

The new PageEditor does not need to edit legacy blocks.

For existing pages, choose one of these explicit approaches before removing old source types:

1. Recreate existing content manually in the new Neo editor.
2. Run a controlled migration that maps old page blocks into the new Neo composition model.
3. Archive/reset old demo content if the old blocks are not production data.

Do not keep a compatibility editor for `boring_hero`, `aero_hero`, `aero_features`, `aero_cta`, and similar old block types. If a block concept is still useful, recreate it as a new Neo block with a new schema and preview component.

## NeoUI Integration

### Role of NeoUI

NeoUI is used for manager/editor UI only:

- editor shell controls
- sidebar and palette affordances
- dialogs, sheets, popovers, dropdowns, tooltips
- form controls in property panels
- sortable/reorderable editor lists
- simplified canvas preview components
- data-heavy editor grids where useful

NeoUI is not the public rendering runtime. Public block renderers should output ordinary HTML through Razor components and the existing adapter pipeline.

### Required Setup

NeoUI docs require:

```razor
<AppProvider>
    @Body
    <ToastViewport />
    <DialogHost />
</AppProvider>
<ContainerPortalHost />
<OverlayPortalHost />
```

NeoUI also requires service registration:

```csharp
builder.Services.AddNeoUIPrimitives();
builder.Services.AddNeoUIComponents();
```

The portal hosts that need theme/style cascade must be inside the `AppProvider` boundary, per the NeoUI theming guide.

### Asset Setup

NeoUI docs use:

```razor
<link rel="stylesheet" href="styles/theme.css" />
<link href="@Assets["_content/NeoUI.Blazor/components.css"]" rel="stylesheet" />
<script src="@Assets["_content/NeoUI.Blazor/js/theme.js"]"></script>
```

For AeroCMS:

- load NeoUI assets in the manager app root, not in the public CMS layout
- keep existing manager CSS until each surface is migrated
- avoid loading NeoUI assets on public pages unless a future public feature explicitly needs them
- verify interaction with existing Radzen, Tippy, Monaco, and Tailwind browser script order

### No NPM Rule

AeroCMS has a project rule: do not use npm.

The local `NeoUI/src/NeoUI.Blazor/NeoUI.Blazor.csproj` contains local development targets that run npm to rebuild CSS. Do not trigger those targets in AeroCMS development.

Preferred integration choices:

1. Use the published NuGet packages if available and compatible.
2. If using local project references to `NeoUI`, add a repo-safe MSBuild property/condition so AeroCMS builds use prebuilt CSS and do not run NeoUI npm targets.
3. Do not add npm scripts to AeroCMS.

### NeoUI Imports

Add imports only where needed:

```razor
@using NeoUI.Blazor
@using NeoUI.Icons.Lucide
```

Avoid broad imports if naming collisions with Radzen or existing components become noisy.

## Neo Block Composer

### Authoring Model

The PageEditor should become a Neo block composer, not only a block picker.

NeoUI's README describes the library as a broad Blazor component system with styled components, headless primitives, shadcn-compatible tokens, and components such as Button, Card, Badge, Dialog, Sheet, Tabs, Tooltip, Sortable, DataGrid, and related primitives. Use that component surface as the candidate pool for the trusted catalog, but approve each public/editor use explicitly.

Users should be able to drag onto the canvas:

- complete page blocks, such as Hero, Feature Grid, Pricing, CTA, Testimonials
- layout primitives, such as Section, Container, Grid, Columns, Stack, Spacer
- content primitives, such as Heading, Text, Image, Button, Badge, Card, Icon
- Neo components that are safe to use inside page blocks

This enables two authoring workflows:

1. Drop a complete block and edit its high-level properties.
2. Compose a custom block from Neo primitives/components and save it as a reusable block pattern later.

### Catalog

Create a trusted component catalog. The palette should not discover arbitrary components at runtime.

Catalog entries should define:

- stable catalog ID
- display name
- category
- icon
- kind: block, layout, primitive, component
- allowed parent/child relationships
- editable properties with type, default value, validation, and editor control
- preview component type
- public renderer/component type
- whether the component is allowed on public static SSR pages
- whether the component requires an interactive island

Example shape:

```csharp
public sealed record NeoComponentCatalogItem(
    string CatalogId,
    string DisplayName,
    string Category,
    NeoNodeKind Kind,
    Type EditorPreviewType,
    Type PropertyEditorType,
    Type PublicRenderType,
    bool AllowsChildren,
    bool IsPublicStaticSsrSafe,
    bool RequiresInteractiveIsland);
```

### Drag and Drop Rules

The editor should validate placement before inserting a node:

- sections can contain containers, grids, stacks, and blocks
- grids/columns can contain blocks, components, and primitives
- text primitives cannot contain children
- interactive components must be marked as islands
- DataGrid-style components are manager/editor candidates by default, not ordinary public content blocks

### NeoUI Grid and Theme Notes

The current `NeoUI/llms/*.txt` files are focused on DataGrid and theme behavior.

Use those docs for manager/editor grids, data-heavy property panels, and theme-token behavior. The docs show that NeoUI grids can use strongly typed parameters, selection, server-side data requests, density/style settings, and NeoUI/shadcn-compatible CSS variables.

Do not assume DataGrid is a general page-builder primitive for public pages. It can be available later as a deliberate component if a public use case needs a static table/grid or an explicit interactive island.

## Block Palette

### Source of Truth

The palette should be generated from trusted block/component metadata, not hand-maintained in `PageEditor.razor`.

Current attribute shape already includes useful editor metadata:

```csharp
[BlockMetadata("hero", "Hero", Category = "Marketing", Icon = "image")]
public sealed class HeroBlock : BlockBase
{
}
```

Extend `BlockRendererGenerator` or a related source generator to emit editor palette/catalog metadata if the current generated manifest does not expose enough.

Target generated data:

```csharp
public sealed record BlockPaletteItem(
    string CatalogId,
    string DisplayName,
    string Category,
    string? Icon,
    NeoNodeKind Kind,
    int SortOrder,
    int SchemaVersion,
    Type ModelType);
```

The palette lists both semantic page blocks and allowed Neo primitives/components. The persisted page model stores stable catalog IDs and validated properties, not arbitrary NeoUI component names typed by the user.

Example:

```text
Marketing
  Hero
  Feature Grid
  Testimonials
  Pricing
  Stats
  Call To Action

Layout
  Section
  Container
  Columns
  Grid
  Stack

Content
  Rich Text
  Heading
  Image
  Quote
  Button Group

Primitives
  Button
  Badge
  Card
  Icon
  Separator
```

## Public Renderer Rules

Public renderer components should:

- be Razor components under `src/Aero.Cms.Shared/Blocks/Rendering`
- be registered through `[CmsBlockRenderer(typeof(...))]`
- render plain semantic HTML
- render safely under Blazor static SSR with no page-level `@rendermode`
- use Tailwind-compatible utility classes where appropriate
- use CSS variables for theme tokens
- use logical properties for RTL readiness
- avoid interactive-only Blazor behavior
- avoid fetching data directly unless that is already the established renderer pattern
- receive cross-cutting data through `BlockRenderContext`

Public renderer components should not:

- depend on NeoUI manager/editor services
- use ViewComponents as the primary renderer
- use `IBlockVisitor`
- encode business logic or persistence logic
- hardcode colors, fonts, or site-specific branding
- add public JavaScript dependencies for ordinary content blocks

Public renderers may use static-renderable NeoUI primitives/components only if:

- they render useful static HTML during static SSR
- they do not require a Blazor circuit for ordinary content behavior
- they do not pull manager/editor-only assets into public pages
- their CSS/token dependencies are part of the public theme contract

If a public component genuinely needs interactivity, model it as an explicit island and require a render-mode decision for that component only.

### Static SSR Forms

Static SSR supports normal HTTP form posts. Public blocks such as contact forms, newsletter signup, search, lead capture, and simple request forms do not need Interactive Server or WebAssembly by default.

Use Blazor form binding patterns that work with static SSR:

- every statically rendered `EditForm` needs a unique `FormName`
- bind posted form data with `[SupplyParameterFromForm]`
- include antiforgery support
- use Post/Redirect/Get or query-string state after successful submission
- do not rely on in-memory component state surviving a POST

Example:

```razor
@page "/contact"
@inject NavigationManager Nav
@using System.ComponentModel.DataAnnotations

@if (Submitted)
{
    <p>Thanks! We received your message.</p>
}
else
{
    <EditForm Model="Model" FormName="contact-form" OnValidSubmit="SubmitAsync">
        <DataAnnotationsValidator />
        <AntiforgeryToken />

        <InputText @bind-Value="Model.Name" />
        <InputText @bind-Value="Model.Email" />
        <InputTextArea @bind-Value="Model.Message" />

        <button type="submit">Send</button>
    </EditForm>
}

@code {
    [SupplyParameterFromForm]
    private ContactFormModel Model { get; set; } = new();

    [SupplyParameterFromQuery(Name = "submitted")]
    private bool Submitted { get; set; }

    private Task SubmitAsync()
    {
        Nav.NavigateTo("/contact?submitted=true");
        return Task.CompletedTask;
    }

    private sealed class ContactFormModel
    {
        [Required] public string Name { get; set; } = "";
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string Message { get; set; } = "";
    }
}
```

For static SSR forms, `OnInitialized{Async}` and `OnParametersSet{Async}` can run again on POST. Initialize models carefully so posted values are not overwritten.

## Output Caching

### Retain the Existing Module

The static SSR renderer must use the existing AeroCMS output-cache infrastructure. Do not create a parallel cache layer for the new Razor component renderer.

Current anchors:

- `OutputCacheModule` calls `services.AddOutputCache(...)`.
- `OutputCacheModule.ConfigurePipeline` calls `app.UseOutputCache()`.
- `CmsOutputCachePolicy` limits caching to anonymous GET/HEAD public responses.
- Manager/admin/API admin paths are excluded from caching.
- Diagnostic header behavior is best-effort and must not register late `OnStarting` callbacks after the response starts.
- Current named policies include `PagesPolicy`, `BlogPolicy`, `BlogPartialPolicy`, `DocsPolicy`, and `DocsIndexPolicy`.

Microsoft Learn confirms the key ASP.NET Core mechanics:

- `AddOutputCache` and `UseOutputCache` make output caching available.
- Caching must still be selected by endpoint/policy.
- Policies can set expiration, vary rules, tags, and custom policy behavior.
- Tags can be evicted through `IOutputCacheStore.EvictByTagAsync(...)`.
- The default output-cache rules cache only successful GET/HEAD anonymous responses and exclude Set-Cookie responses, which is why AeroCMS keeps a custom CMS policy for antiforgery-cookie behavior.

### Static SSR Page Policy

The new static SSR public page host must select the existing CMS output-cache policies.

If `[OutputCache]` endpoint metadata works directly on routable Razor components, use:

```razor
@page "/{*path}"
@attribute [OutputCache(PolicyName = "PagesPolicy")]

<PageRenderView Page="Page" />
```

If routable component endpoint metadata does not apply the policy correctly in the current .NET 10 app shape, apply the policy at endpoint mapping time or through route-group conventions. Do not drop output caching during the `.cshtml` to `.razor` migration.

The first implementation slice must include an integration test that proves the static SSR component route:

- uses the intended policy
- receives `X-Aero-Output-Cache` diagnostics when headers can still be written
- returns a cache HIT on a repeated anonymous GET
- bypasses cache for authenticated/manager/admin requests

### Cache Tags and Invalidation

Keep policy-level tags and extend them deliberately.

Baseline tags:

```text
pages-list
blog-index
docs-index
cms
```

Recommended granular tags:

```text
site:{siteId}
content:{contentId}
page:{pageId}
slug:{slug}
blog-post:{postId}
docs-page:{docsPageId}
tag:{tagSlug}
```

When content is saved, published, unpublished, moved, or its slug changes, invalidate through Wolverine/event handlers using `IOutputCacheStore`.

Example:

```csharp
public sealed class CmsOutputCacheInvalidationHandler(IOutputCacheStore cache)
{
    public async Task Handle(PageContentUpdatedEvent evt, CancellationToken ct)
    {
        await Task.WhenAll(
            cache.EvictByTagAsync("pages-list", ct),
            cache.EvictByTagAsync($"site:{evt.SiteId}", ct),
            cache.EvictByTagAsync($"content:{evt.ContentId}", ct),
            cache.EvictByTagAsync($"slug:{evt.NewSlug}", ct),
            string.IsNullOrWhiteSpace(evt.OldSlug)
                ? Task.CompletedTask
                : cache.EvictByTagAsync($"slug:{evt.OldSlug}", ct));
    }
}
```

Use coarse eviction first if needed. Fine-grained tags can be added once the static SSR route and publish pipeline are stable.

### Cache Safety Rules

Output caching stores the full rendered HTML response. Therefore:

- cache only public anonymous content pages
- do not cache manager/editor/preview routes
- do not cache draft previews
- do not cache pages with user-specific personalization unless the variation key is explicit and tested
- do not cache interactive island state as if it were static page content
- keep public forms POST/PRG-driven and avoid caching POST responses
- vary by site/host/path/slug/query values that affect output

The current `CmsOutputCachePolicy` already bypasses authenticated requests and manager/admin paths. Preserve that behavior when moving from `Page.cshtml` to static SSR Razor components.

## BlockRenderContext

The current `BlockRenderContext` carries:

- navigation
- preview state
- HTMX facts
- culture

Extend it conservatively as new renderer requirements appear.

Recommended additions:

```csharp
public sealed record BlockRenderContext(
    NavigationDetail? Navigation = null,
    bool IsPreview = false,
    bool IsHtmxRequest = false,
    string? HtmxTarget = null,
    CultureInfo? Culture = null,
    int NestingDepth = 0)
{
    public const int MaxNestingDepth = 10;
}
```

Container renderers must increment `NestingDepth` and stop rendering if the limit is exceeded.

## Container Blocks

### Current State

The codebase already has:

- `LayoutRegion`
- `LayoutColumn`
- `BlockPlacement`
- `ColumnsBlock`
- `ColumnItem`

The original proposal's `SectionBlock` can still be valuable, but it must be introduced as an additive block type that composes with existing layout infrastructure.

### SectionBlock

`SectionBlock` is allowed as a content section wrapper if it stays semantic:

```csharp
[BlockMetadata("section", "Section", Category = "Layout")]
public sealed class SectionBlock : BlockBase
{
    public override string BlockType => "section";
    public string? BackgroundToken { get; set; }
    public long? BackgroundImageMediaId { get; set; }
    public SectionPadding Padding { get; set; } = SectionPadding.Medium;
    public List<BlockBase> Children { get; set; } = [];
}
```

Prefer token/intent values over raw CSS.

Do not introduce `SectionBlock` and `LayoutRegion` replacements in the same slice. That is too much churn.

### ColumnsBlock

Keep `ColumnsBlock`, but align it with the existing 12-column grid model.

If `ColumnItem.Span` or equivalent width data is missing or inconsistent, evolve it through `EditorBlockMapper` and renderer tests.

## Block Model Cleanup

### Semantic Action Model

Unify button/action shapes behind:

```csharp
public sealed record BlockAction
{
    public string Label { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public BlockActionRole Role { get; init; } = BlockActionRole.Primary;
    public bool OpenInNewTab { get; init; }
}

public enum BlockActionRole
{
    Primary,
    Secondary,
    Ghost
}
```

Renderer maps role to visual treatment.

### Candidate Consolidations

Do not carry these legacy blocks into the new editor. Treat the table as a recreation guide if the concept still matters.

| Legacy block | New Neo direction | Note |
| --- | --- | --- |
| `BoringHeroBlock` | `NeoHeroBlock` | recreate as a simple centered hero preset |
| `AeroHeroBlock` | `NeoHeroBlock` | recreate layouts/actions as Neo variants |
| `HeroBlock` | `NeoHeroBlock` | do not preserve presentation-only fields directly |
| `AeroFeaturesBlock` | `NeoFeatureGridBlock` | recreate as grid/stack/card composition |
| `AeroCtaBlock` | `NeoCallToActionBlock` | recreate with semantic actions |
| `AeroBlogBlock` | `NeoBlogGridBlock` | decide live data vs stored snapshot |
| `AeroPricingBlock` | `NeoPricingBlock` | recreate plan cards with structured plan data |
| `AeroTestimonialsBlock` | `NeoTestimonialsBlock` | recreate quote/card composition |
| `AeroTeamsBlock` | `NeoTeamBlock` | keep distinct if fields differ from testimonials |
| `AeroFaqBlock` | `NeoFaqBlock` | recreate as accordion/list; public interactivity decision required |
| `AeroPortfolioBlock` | `NeoPortfolioBlock` | product decision needed |
| `AeroContactBlock` | `NeoContactBlock` | static SSR form support and PRG required |
| `AeroTableBlock` | `NeoTableBlock` or `NeoDataGridBlock` | static table by default; interactive grid only as island |
| `AeroAuthBlock` | explicit interactive/auth island | do not treat as ordinary public content |
| `DynamicTemplateBlock` / `dynamic_template` | `NeoScribanBlock` or `NeoTemplateBlock` | preserve the Scriban block capability, but port it into the new catalog/composition model instead of assuming the old editor UI or renderer architecture survives |

### Scriban Block Port

The existing Scriban/dynamic-template block is a capability to carry forward, not a requirement to preserve the old PageEditor implementation. The new editor should expose it as a Neo-era block/component with catalog metadata, a property editor, an editor preview, and a public static SSR renderer.

Requirements:

- The persisted model should use a stable Neo catalog ID such as `neo.template.scriban`, not the legacy `dynamic_template` discriminator.
- The public renderer may reuse compatible Scriban rendering services if they fit the new static SSR adapter model, but it should not keep the old PageEditor switch or old authoring UI alive.
- The editor preview must render a quick visual approximation using the provided template and sample/validated data, with clear validation feedback for template errors.
- Template data must be stored as validated structured JSON or a typed data contract; do not allow arbitrary runtime objects, C# expressions, service access, or unsafe host APIs from page authors.
- Treat script/template safety, allowed functions, data binding shape, caching behavior, and error rendering as explicit design decisions before public use.

## Legacy Content Strategy

This refactor intentionally removes old PageEditor block functionality.

### Persisted Data Risk

Marten documents may contain old block discriminators such as:

- `boring_hero`
- `aero_hero`
- `aero_features`
- `aero_cta`
- `aero_blog`
- `aero_pricing`
- `aero_testimonials`

If those types are removed before content is migrated, archived, or reset, deserialization can fail. The product decision is to avoid carrying old editing behavior forward, but persisted data still needs an explicit handling path.

### Required Legacy Handling Pieces

Choose and implement one path:

1. Recreate content manually and reset/remove old persisted page block data.
2. Add a one-time migration job that maps old block data into `NeoPageNode` composition data.
3. Keep a read-only legacy renderer temporarily outside the new editor, only long enough to avoid breaking public pages while content is recreated.

Do not build a legacy compatibility editor. Do not add new features to old block types.

### Page Schema Version

Add a schema/version marker before broad data upgrades:

```csharp
public int BlockSchemaVersion { get; set; } = 1;
```

This can live on `PageDocument` or in a dedicated page content version model. Choose the location after inspecting current event/projection behavior.

### Page Upgrade Service

If existing content must be preserved, introduce a focused upgrade service:

```text
Controlled upgrade command
  -> detect old schema
  -> map old EditorBlock shapes
  -> map old BlockBase discriminators
  -> produce NeoPageNode composition data
  -> validate the new composition
  -> save only when explicitly approved or run as a migration job
```

Do not mutate persisted pages silently on read unless the user explicitly saves or a controlled migration job is running.

## Source Generator Work

The existing `BlockRendererGenerator` should be extended, not replaced.

Current useful generated outputs include:

- `GeneratedBlockModelManifest`
- `GeneratedBlockFactory`
- `CmsBlockRenderRegistry`
- renderer adapters
- `BlockBase.Polymorphic.g.cs`

Recommended additions:

- typed render adapter support
- generated palette metadata
- optional generated editor component descriptors
- warnings for block metadata missing category/icon if the editor requires them

Avoid creating a separate generator unless the existing generator becomes impossible to evolve cleanly.

## Theming

### Public Site Theme Strategy

Public renderers should use CSS custom properties compatible with NeoUI/shadcn-style tokens, but public output should not require NeoUI components.

Preferred token names should align with NeoUI where practical:

```css
:root {
  --background: oklch(1 0 0);
  --foreground: oklch(0.145 0 0);
  --card: oklch(1 0 0);
  --card-foreground: oklch(0.145 0 0);
  --primary: oklch(0.205 0 0);
  --primary-foreground: oklch(0.985 0 0);
  --muted: oklch(0.97 0 0);
  --muted-foreground: oklch(0.556 0 0);
  --border: oklch(0.922 0 0);
  --input: oklch(0.922 0 0);
  --ring: oklch(0.708 0 0);
  --radius: 0.5rem;
}

.dark {
  --background: oklch(0.145 0 0);
  --foreground: oklch(0.985 0 0);
}
```

The earlier `--color-*` naming can be retained only if the team chooses a mapping layer. Avoid supporting two token dialects indefinitely.

### Site Theme CSS

Use a site-owned theme CSS path only for public site theme variables.

Do not use `IViewLocationExpander` for CSS. It is a Razor view lookup mechanism, not a static asset override mechanism.

### Base vs Theme CSS

Separate responsibilities:

```text
aero-base.css
  - structural styles
  - editor/public block safety styles
  - not site-branded

site theme CSS
  - CSS variables
  - brand colors
  - fonts
  - radius
  - light/dark values
```

## RTL Support

Renderer markup should prefer logical properties:

| Avoid | Prefer |
| --- | --- |
| `pl-*`, `pr-*` | `ps-*`, `pe-*` |
| `ml-*`, `mr-*` | `ms-*`, `me-*` |
| `text-left`, `text-right` | `text-start`, `text-end` |
| `border-l-*`, `border-r-*` | `border-s-*`, `border-e-*` |
| `left-*`, `right-*` | `start-*`, `end-*` |

Set document direction from site settings when the site model supports it:

```html
<html dir="rtl" lang="ar">
```

Do not add RTL-specific block fields.

## Override Strategy

### Renderer Overrides

The previous ViewComponent proposal had a clear override story through `.cshtml` shadowing. Since AeroCMS is keeping Razor component renderers, the refactor must define an equivalent extension point.

Acceptable options:

1. Host-level renderer registration override by block type.
2. Theme-level renderer resolver that selects a component type.
3. Adapter wrapper that can delegate to host-provided renderers.

Do not claim public renderer overriding is solved until one of these mechanisms exists and is tested.

### CSS Overrides

CSS customization is handled by theme variables and site theme CSS files, not Razor view lookup.

### Editor Overrides

The first refactor does not need to make editor UI overridable. The editor is Aero-owned manager chrome.

## Verification Plan

### Build Verification

At minimum, run focused builds:

```powershell
dotnet build src\Aero.Cms.Shared\Aero.Cms.Shared.csproj /p:UseSharedCompilation=false -v:minimal
dotnet build src\Aero.Cms.Modules.Pages\Aero.Cms.Modules.Pages.csproj /p:UseSharedCompilation=false -v:minimal
dotnet build src\Aero.Cms.Web\Aero.Cms.Web.csproj /p:UseSharedCompilation=false -v:minimal
```

### Unit/Integration Tests

Add tests for:

- source-generated block/component catalog contains expected metadata
- `CmsBlockRenderRegistry.TryGet` resolves each renderer
- Neo composition nodes serialize and deserialize safely
- invalid catalog IDs and invalid child placements are rejected
- one-time legacy migration maps representative old blocks if migration is selected
- nested container depth limit
- public static SSR render does not require an interactive render mode
- static SSR public page uses the existing output-cache policy
- repeated anonymous static SSR page request produces an output-cache hit
- authenticated, manager, admin, draft preview, and POST requests bypass output caching
- content update events evict expected output-cache tags

### UI Tests

Use Playwright for PageEditor regressions:

- page editor loads
- right sidebar opens/collapses
- collapsed sidebar tooltip still appears quickly
- block, primitive, and component can be added from palette
- block can be selected
- block canvas shows quick visual previews for all blocks, with Neo blocks treated as the required baseline
- block properties can be edited
- block can be moved, duplicated, and deleted
- nested primitive/component composition works inside a block
- preview overlay opens
- existing draft preview URL behavior still works
- save/publish flow still works
- public page output contains server-rendered HTML without a public Blazor circuit unless an explicit island is present
- public page cache diagnostics show expected miss/hit behavior for anonymous requests when diagnostics are enabled

### NeoUI-Specific Checks

Verify:

- `AppProvider` wraps the manager UI surface that uses NeoUI
- `ToastViewport`, `DialogHost`, and portal hosts receive the correct cascade
- NeoUI assets load without breaking existing Radzen, Monaco, Tippy, or Tailwind usage
- AeroCMS build does not run npm
- WASM/client build remains valid
- NeoUI DataGrid assets are loaded only for surfaces that actually use DataGrid
- NeoUI theme tokens flow into editor previews and manager DataGrid surfaces

## Implementation Phases

### Phase 0 - Setup and Safety

- Add NeoUI references safely.
- Register NeoUI services.
- Load NeoUI assets only for manager/editor surfaces.
- Confirm no npm target runs during AeroCMS build.
- Add a tiny NeoUI smoke surface if needed.
- Confirm public pages can be hosted by static SSR Razor components.

### Phase 1 - Static SSR Public Page Host

- Replace the long-term `Page.cshtml` public host with routable `.razor` static SSR components.
- Keep public content pages free of page-level `@rendermode`.
- Register interactive modes only if the manager/admin surface or explicit public islands require them.
- Keep `BlockRenderer.razor` and adapter registry concepts, but invoke them from the Razor component host.
- Add static SSR smoke tests for a public page and a public form.
- Apply the existing `PagesPolicy` output-cache policy to the new static SSR page route.
- Prove the new route still flows through `OutputCacheModule` and `CmsOutputCachePolicy`.
- Preserve the existing cache-bypass behavior for authenticated, manager/admin, draft preview, and non-GET/HEAD requests.

### Phase 2 - New Neo PageEditor Shell

- Build a new PageEditor shell instead of preserving the old block editor.
- Add `BlockPalette`.
- Add `PageEditorCanvas`.
- Add `EditorBlockFrame`.
- Add `BlockEditorPreviewHost`.
- Add `PageEditorPropertyPanel`.
- Add `BlockEditorHost`.
- Add the Neo component/primitive catalog.
- Remove the old `RenderBlock(EditorBlock block, bool isSelected)` switch from the new editor.

### Phase 3 - Neo Composition Model

- Introduce the persisted Neo composition node model.
- Add schema versioning.
- Add catalog validation.
- Add placement validation for parent/child relationships.
- Add serialization tests.

### Phase 4 - Initial Neo Blocks and Primitives

- Implement initial page blocks:
  - Neo Hero
  - Neo Section
  - Neo Rich Text
  - Neo Image
  - Neo CTA
  - Neo Scriban/Template block
- Implement initial primitives:
  - Heading
  - Text
  - Button
  - Badge
  - Card
  - Separator
- Each item must have an editor preview, property editor, public renderer/static output, and catalog metadata.
- The Neo Scriban/Template block must preserve the existing author capability to render Scriban-backed dynamic content, but only through the new catalog, preview host, validation, and static SSR renderer path.

### Phase 5 - Generated Catalog and Typed Render Adapters

- Add `ICmsBlockRenderAdapter<TBlock>`.
- Update generator output.
- Keep existing registry API stable.
- Add adapter resolution tests.
- Extend `BlockRendererGenerator` or related generator output for catalog metadata.
- Replace hardcoded palette categories.
- Keep block/category ordering deterministic.

### Phase 6 - Legacy Content Handling

- Decide whether existing content will be recreated, reset, or migrated.
- Decide whether existing `dynamic_template` content is migrated to `neo.template.scriban`, recreated manually, or temporarily rendered read-only during content transition.
- If migrated, add the one-time upgrade service.
- Do not build a legacy compatibility editor.
- Remove old block editor UI and old block authoring code after the chosen content path is complete.
- Add Wolverine/event-handler output-cache eviction for changed legacy/new content before enabling public traffic on the new renderer.

### Phase 7 - Expand Neo Blocks

- Add Feature Grid, Pricing, Testimonials, FAQ, Blog Grid, Contact, Portfolio, and Table/DataGrid variants as product decisions are made.
- Mark interactive public components as islands.
- Keep static SSR as the default for public output.

## Open Architectural Questions

Ask before implementing these:

1. Should public renderer overrides be host-level, theme-level, or both?
2. Which legacy content path should be used: manual recreation, reset/archive, or one-time migration?
3. What is the first approved Neo primitive/component catalog?
4. Should composed custom blocks be saveable as reusable block patterns in this slice, or later?
5. Which public Neo components, if any, are allowed to become interactive islands?
6. Should initial output-cache invalidation be coarse (`cms`/`pages-list`) or fine-grained (`content:{id}`, `site:{id}`, `slug:{slug}`)?
7. Should NeoUI replace Radzen across the whole manager over time, or only inside PageEditor for this refactor?
8. For the Neo Scriban/Template block, what template sandbox, allowed functions, data-binding schema, and authoring permissions are acceptable?

## Final Guidance

The target path is a deliberate editor/block rebuild:

```text
move public pages to static SSR Razor components
  -> build new Neo PageEditor shell
  -> introduce trusted Neo component/primitive catalog
  -> persist structured Neo composition nodes
  -> generate catalog and renderer metadata
  -> implement new Neo blocks with editor previews
  -> handle legacy content through recreation, reset, or one-time migration
```

Avoid the tempting rewrite:

```text
new visitor pipeline
  + new ViewComponents
  + arbitrary Razor/source persisted from users
  + public page-level interactivity
  + hidden legacy compatibility editor
```

That version duplicates working infrastructure, creates security and maintenance risk, and undermines the static SSR public rendering goal.
