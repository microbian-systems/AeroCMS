# Aero CMS - Blocks, Renderers, PageEditor, and NeoUI Refactor

> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). See
> [`surrealdb-marten-port.md`](surrealdb-marten-port.md). `BlockBase` page
> content still persists, but through AeroDB.Sable document sessions, not Marten.

> **See also:** [`aero-page-document-refactor.md`](aero-page-document-refactor.md) — Data model contract that this editor/renderer implementation depends on. That document defines the core documents (`PageDocument`, `PageEditorState`, `BlockBase`), event shapes (`PageMetadataUpdated`, `PagePublished`), the `IPageLayoutManifestBuilder`, preview/publish pipelines, and value objects. This document assumes that model; changes to the data contract must be reflected in both.

## Purpose

This document is the implementation contract for refactoring Aero CMS page-editor and block-renderer functionality while integrating NeoUI.

The refactor is intentionally scoped to:

- `src/Aero.Cms.Shared/Pages/Manager/PageEditor`
- block editor previews and property panels
- public block renderer components
- source-generated block metadata/registration where it supports the editor and renderer

This is not a full manager-shell rewrite, not a site-builder rewrite, and not a public theming module implementation.

## Product UX Principle

The PageEditor must be easy for non-technical users. The default experience should feel like a simple WYSIWYG page builder, not a developer component tree.

V1 authoring rules:

- Users primarily add and reorder page sections as blocks.
- Blocks are recognizable page sections such as Hero, CTA, Feature Grid, Gallery, Image, Video, and Separator.
- Every new block needs a quick visual preview on the canvas.
- Editing should favor plain-language fields, inline affordances, and obvious controls.
- Neo primitives/components should be progressively disclosed inside composition-capable blocks.
- A visual tree/outline can be added later, but it is not required for users to build a normal page in V1.
- Do not make users understand `NeoPageNode`, catalog IDs, renderers, or layout manifests.

Architecture consequence: V1 uses flat top-level page placement with optional nested composition inside `NeoCompositionBlock`. Do not store a page-level `NeoPageNode` tree in `PageEditorState` for this refactor.

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
  -> BlockPlacementRenderer.razor
  -> BlockRenderer.razor
  -> CmsBlockRenderRegistry
  -> ICmsBlockRenderAdapter
  -> block-specific Razor renderer
```

The live code may still contain `LayoutColumnRenderer`, but the target data model in `aero-page-document-refactor.md` is flatter: `LayoutRegion` owns ordered `BlockPlacement` entries directly. Treat any `LayoutColumn` wording in this document as current-state context only, not the target PageDocument contract.

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
  - currently persists both `LayoutRegions` and editor `Blocks`; target refactor keeps only published `LayoutRegions` on `PageDocument`
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

If the manager/admin app needs interactivity, register the interactive modes app-wide, but apply them only to the manager surface. Public CMS pages do not get Blazor interactive islands in V1:

```csharp
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();
```

Public content pages should remain static SSR. Do not add public Blazor interactive islands in V1.

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
  - no NeoUI component usage in V1 public renderers
  - plain HTML, Razor, Tailwind utility classes, CSS variables, and inline SVG/icons only
  - optimized for speed, SEO, and theming
  - uses block-specific Razor renderers through adapters
```

For V1, NeoUI is an editor/composition dependency only. Public static SSR renderers must not import `NeoUI.Blazor`, render NeoUI components such as `<Badge>` or `<Button>`, or depend on NeoUI `components.css` being present on public pages. If a NeoUI component is dragged onto the canvas, the public renderer must translate the approved catalog item into public-safe HTML. Custom public interactivity is out of scope for this refactor and should come later through explicit Alpine/HTMX behavior, not Blazor interactive islands.

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

The editor preview component may internally reuse public-safe rendering logic when that is cheap and visually useful, but it must remain editor-owned. It can add selection chrome, inline affordances, placeholders, empty-state visuals, simplified data, and NeoUI controls without leaking those concerns into public rendering.

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

The new editor should use a structured composition model inside composition-capable blocks. It should not replace `PageEditorState.Blocks` with a page-level tree in V1.

The nested composition model can represent:

- full page blocks
- layout/container nodes
- NeoUI primitives
- NeoUI components
- nested children
- component properties
- design tokens and semantic variants

Marten persistence still flows through `BlockBase` page content. A structured `NeoPageNode` tree is persisted only as the payload of a `NeoCompositionBlock : BlockBase` or converted into a typed block such as `Hero01Block`. Do not persist a second standalone Neo page tree beside the block list.

`PageDocument.LayoutRegions` is still part of the published render contract. It is the placement manifest that tells public rendering which block documents appear in which regions and in what order. The new editor should produce validated `BlockBase` documents during draft save, update `PageEditorState.BlockIdMap`, and generate `PageDocument.LayoutRegions` only during publish through the existing page service flow. Do not bypass `LayoutRegions` by saving only a loose block list and expecting public pages to render it.

Decision for V1: keep top-level page authoring flat. A future tree-view should be a UI projection over `PageEditorState.Blocks` plus nested `NeoCompositionBlock.Nodes`, not a new persisted page-level tree.

Do not persist arbitrary Razor source, arbitrary C# expressions, or runtime component type names supplied by users. Persist stable catalog identifiers and validated property bags. Raw HTML is allowed only through the explicit Raw HTML block path and must preserve the existing safety/permission rules.

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

The exact type names can change during implementation, but the model must be structured, versioned, and generated/validated from a trusted catalog.

### Legacy Block Removal

The new PageEditor does not need to edit legacy blocks.

For existing pages, run the controlled one-time migration before removing old source types or old editability. The migration target set is Boring Hero, Columns, Scriban, Image, Video, Audio, Gallery, Raw HTML, and Separator.

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
- do not load NeoUI assets for public CMS pages in V1
- verify interaction with existing Radzen, Tippy, Monaco, and Tailwind browser script order

### No NPM Rule

AeroCMS has a project rule: do not use npm.

The local `NeoUI/src/NeoUI.Blazor/NeoUI.Blazor.csproj` contains local development targets that run npm to rebuild CSS. Do not trigger those targets in AeroCMS development.

Preferred integration choices:

1. Use the published NuGet packages.
2. Use the local `NeoUI/` checkout only as reference source/docs.
3. Do not add local project references to `NeoUI`.
4. Do not add npm scripts to AeroCMS.

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
- whether the component would require a future interactive island; this must be `false` for all V1 public items

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
    bool RequiresInteractiveIsland); // false for V1 public rendering
```

### Drag and Drop Rules

The editor should validate placement before inserting a node:

- sections can contain containers, grids, stacks, and blocks
- grids/columns can contain blocks, components, and primitives
- text primitives cannot contain children
- interactive components are not allowed in public V1 rendering
- DataGrid-style components are manager/editor candidates by default, not ordinary public content blocks

### NeoUI Grid and Theme Notes

The current `NeoUI/llms/*.txt` files are focused on DataGrid and theme behavior.

Use those docs for manager/editor grids, data-heavy property panels, and theme-token behavior. The docs show that NeoUI grids can use strongly typed parameters, selection, server-side data requests, density/style settings, and NeoUI/shadcn-compatible CSS variables.

Do not assume DataGrid is a general page-builder primitive for public pages. It can be available later as a deliberate component if a public use case needs a static table/grid. Do not add it as a public Blazor interactive island in V1.

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

Public renderers must not use NeoUI primitives/components in V1. If an editor-authored NeoUI primitive/component appears in page composition, translate it to public-safe static HTML in the renderer.

If a public component genuinely needs interactivity later, design that as separate Alpine/HTMX work and update this document before implementation.

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
// Wolverine message used for output-cache eviction.
// The Marten event PageMetadataUpdated (see aero-page-document-refactor.md)
// feeds this handler after a draft metadata save.
public sealed record PageMetadataUpdatedEvent(
    long PageId,
    long SiteId,
    string Slug,
    string? OldSlug);

public sealed class CmsOutputCacheInvalidationHandler(IOutputCacheStore cache)
{
    public async Task Handle(PageMetadataUpdatedEvent evt, CancellationToken ct)
    {
        await Task.WhenAll(
            cache.EvictByTagAsync("pages-list", ct),
            cache.EvictByTagAsync($"site:{evt.SiteId}", ct),
            cache.EvictByTagAsync($"page:{evt.PageId}", ct),
            cache.EvictByTagAsync($"slug:{evt.Slug}", ct),
            string.IsNullOrWhiteSpace(evt.OldSlug)
                ? Task.CompletedTask
                : cache.EvictByTagAsync($"slug:{evt.OldSlug}", ct));
    }
}
```

Use fine-grained eviction for the first implementation slice. Keep coarse tags such as `cms` or `pages-list` only as emergency fallback or broad invalidation tools.

The reason for starting fine-grained is that AeroCMS already separates page, blog, docs, and index policies, and the new static SSR host should preserve that shape instead of collapsing all page updates into one global CMS eviction. This must still be implemented defensively: every fine-grained invalidation handler should also have a simple coarse fallback path available for operational recovery if a tag bug is discovered.

### Cache Safety Rules

Output caching stores the full rendered HTML response. Therefore:

- cache only public anonymous content pages
- do not cache manager/editor/preview routes
- do not cache draft previews
- do not cache pages with user-specific personalization unless the variation key is explicit and tested
- do not introduce public Blazor interactive island state in V1
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

At the time of this review, the live `src/Aero.Cms.Shared/Blocks/Rendering/BlockRenderContext.cs` contains navigation, preview, HTMX, and culture fields but does not yet include `NestingDepth`. Add `NestingDepth` and `MaxNestingDepth` before implementing `NeoCompositionBlockRenderer`, or the recursive composition renderer will have no shared guardrail.

## Container Blocks

### Current State

The codebase already has:

- `LayoutRegion`
- `LayoutColumn`
- `BlockPlacement`
- `ColumnsBlock`
- `ColumnItem`

`LayoutColumn` is current implementation context only. The target PageDocument refactor defines `LayoutRegion` as a flat published manifest with ordered `BlockPlacement` entries. Column/grid authoring should be modeled as block content, such as `ColumnsBlock` or a future Neo layout/composition node, not as a new `PageDocument.LayoutRegions` shape.

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
| `AeroFaqBlock` | `NeoFaqBlock` | recreate as static accordion/list first; Alpine/HTMX behavior can be considered later |
| `AeroPortfolioBlock` | `NeoPortfolioBlock` | product decision needed |
| `AeroContactBlock` | `NeoContactBlock` | static SSR form support and PRG required |
| `AeroTableBlock` | `NeoTableBlock` | static table by default; no public interactive DataGrid island in V1 |
| `AeroAuthBlock` | out of V1 public page composition | do not treat as ordinary public content; no public Blazor auth island in this refactor |
| `DynamicTemplateBlock` / `dynamic_template` | `NeoScribanBlock` or `NeoTemplateBlock` | preserve the Scriban block capability, but port it into the new catalog/composition model instead of assuming the old editor UI or renderer architecture survives |

### Scriban Block Port

The existing Scriban/dynamic-template block is a capability to carry forward, not a requirement to preserve the old PageEditor implementation. The new editor should expose it as a Neo-era block/component with catalog metadata, a property editor, an editor preview, and a public static SSR renderer.

Requirements:

- The persisted model should use a stable Neo catalog ID such as `neo.template.scriban`, not the legacy `dynamic_template` discriminator.
- The public renderer may reuse compatible Scriban rendering services if they fit the new static SSR adapter model, but it should not keep the old PageEditor switch or old authoring UI alive.
- The editor preview must render a quick visual approximation using the provided template and sample/validated data, with clear validation feedback for template errors.
- Template data must be stored as validated structured JSON or a typed data contract; do not allow arbitrary runtime objects, C# expressions, service access, or unsafe host APIs from page authors.

Security deferral note:

- For V1, implement the Neo Scriban/Template block with full Scriban permissions (`SecureScribanTemplateOptions.AllowAllFunctions = true`). Do not gate on a feature flag.
- Do not enforce sandbox restrictions, function allowlists, template length limits, recursion limits, render timeouts, or output sanitization in V1. These are explicitly deferred.
- The implementation must reuse or deliberately replace the existing `src/Aero.Cms.Core/Blocks/Dynamic/SecureScribanRenderer.cs`, `DynamicTemplateValidator`, and `SecureScribanTemplateOptions` path.
- A dedicated security hardening task must be completed before any production deployment that serves untrusted authors. The hardening checklist includes: allowed functions allowlist, data-binding schema constraints, template length limits, loop/recursion depth limits, render timeout enforcement, HTML sanitization, cache key variance by template version, sandboxed error rendering, and a review of whether the full-permissions default is acceptable for the target author audience.
- The first public or production release without this hardening must document the deferred security posture in release notes and ensure only trusted authors have access to Scriban block authoring.

## Legacy Content Strategy

This refactor intentionally removes old PageEditor block functionality.

### Persisted Data Risk

Marten documents may contain old block discriminators such as:

- `boring_hero`
- `columns`
- `dynamic_template`
- `image`
- `video`
- `audio`
- `gallery`
- `raw_html`
- `separator`
- `aero_hero`
- `aero_features`
- `aero_cta`
- `aero_blog`
- `aero_pricing`
- `aero_testimonials`

If those types are removed before content is migrated, archived, or reset, deserialization can fail. The product decision is to avoid carrying old editing behavior forward, but persisted data still needs an explicit handling path.

### Required Legacy Handling Pieces

Implement the chosen path:

1. Add a one-time migration job for Boring Hero, Columns, Scriban, Image, Video, Audio, Gallery, Raw HTML, and Separator.
2. Migration may run before final typed Aero UI blocks exist. In that phase, migrate legacy content into `NeoCompositionBlock` payloads that use stable catalog IDs and public-safe node renderers.
3. After `Hero01Block`, `BasicHeroBlock`, or other typed blocks exist, later migration improvements may map selected legacy content directly into those typed `BlockBase` models.
4. Migrate composed layout/content into `NeoCompositionBlock` when it is naturally a composition tree.
5. For migrated draft/editor state, rebuild `PageEditorState.BlockIdMap` from client IDs to saved `BlockBase.Id` values.
6. For migrated published pages, regenerate `PageDocument.LayoutRegions` through `IPageLayoutManifestBuilder` so the existing public render pipeline still has a placement manifest.
7. Keep a read-only legacy viewer or "migrate this page now" action only as an interim safety valve if the global migration cannot run before the new editor is enabled.

Do not build a legacy compatibility editor. Do not add new features to old block types. Do not remove old editability until the migration path or interim safety valve exists.

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
  -> produce NeoCompositionBlock payloads by default
  -> optionally produce typed BlockBase models only for types already implemented
  -> validate generated NeoPageNode composition where used
  -> save BlockBase documents
  -> rebuild PageEditorState.BlockIdMap for draft/editor state
  -> generate PageDocument.LayoutRegions through IPageLayoutManifestBuilder for publish
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

Marten and `BlockBase` subtype registration depend on this generator path. New block models such as `NeoCompositionBlock`, `Hero01Block`, `BasicHeroBlock`, and `NeoScribanBlock` must:

1. Inherit from `BlockBase`.
2. Be marked with `[BlockMetadata(...)]` using a stable discriminator.
3. Live in a project where `BlockRendererGenerator` can discover the source.
4. Produce entries in `GeneratedBlockModelManifest`.
5. Produce `[JsonDerivedType]` entries in generated `BlockBase.Polymorphic.g.cs`.
6. Flow into `src/Aero.Cms.Core/Blocks/BlockMartenConfiguration.cs`, which calls `GeneratedBlockModelManifest.Blocks` and `options.Schema.For<BlockBase>().AddSubClassHierarchy(...)`.

Do not hand-register these new `BlockBase` subtypes in a one-off Marten configuration unless the generator path is proven insufficient. The source-generated polymorphic registration is the normal AeroCMS path and must be tested after adding every new block subtype.

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

Implement both host-level and theme-level renderer overrides:

1. Host-level renderer override by block type.
   - Purpose: a consuming AeroCMS host/application can replace the renderer for `aero.hero.01` or any other block type.
   - Scope: application-wide unless the host override itself branches by site/theme.
   - Example use: a customer application wants its own hard-coded renderer for every Hero 01 block.
2. Theme-level renderer resolver.
   - Purpose: a selected site theme can choose a renderer variant for the same semantic block.
   - Scope: site/theme-specific.
   - Example use: Theme A renders Hero 01 with a centered layout while Theme B renders the same block model with a split layout.
3. Adapter wrapper/delegating resolver.
   - Purpose: keep `BlockRenderer.razor -> CmsBlockRenderRegistry -> ICmsBlockRenderAdapter` stable while allowing host/theme selection behind the adapter.

Do not claim public renderer overriding is solved until both host-level and theme-level override paths exist and are tested.

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
- public page output contains server-rendered HTML without a public Blazor circuit
- public page cache diagnostics show expected miss/hit behavior for anonymous requests when diagnostics are enabled

### NeoUI-Specific Checks

Verify:

- `AppProvider` wraps the manager UI surface that uses NeoUI
- `ToastViewport`, `DialogHost`, and portal hosts receive the correct cascade
- NeoUI assets load without breaking existing Radzen, Monaco, Tippy, or Tailwind usage
- AeroCMS build does not run npm
- WASM/client build remains valid
- NeoUI assets are limited to PageEditor/page-composition surfaces unless a later task explicitly expands scope
- NeoUI theme tokens flow into PageEditor previews and canvas authoring surfaces

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
- Register interactive modes only if the manager/admin surface requires them.
- Keep `BlockRenderer.razor` and adapter registry concepts, but invoke them from the Razor component host.
- Add static SSR smoke tests for a public page and a public form.
- Apply the existing `PagesPolicy` output-cache policy to the new static SSR page route.
- Prove the new route still flows through `OutputCacheModule` and `CmsOutputCachePolicy`.
- Preserve the existing cache-bypass behavior for authenticated, manager/admin, draft preview, and non-GET/HEAD requests.

### Phase 2 - Legacy Migration Bridge

- Implement the one-time migration path before removing old authoring behavior.
- Migrate these legacy editor/block concepts into the new model. In Phase 2, these are catalog IDs/node shapes inside `NeoCompositionBlock` unless the named typed `BlockBase` model already exists:
  - `boring_hero` -> `aero.hero.basic`
  - `columns` -> Neo layout/composition block
  - `dynamic_template` / Scriban -> `neo.template.scriban`
  - `image` -> Neo image block/component
  - `video` -> Neo video block/component
  - `audio` -> Neo audio block/component
  - `gallery` -> Neo gallery block/component
  - `raw_html` -> Neo raw HTML block with the existing safety rules preserved
  - `separator` -> Neo separator primitive/component
- Add a read-only legacy viewer or "migrate this page now" action if the migration cannot run globally before the new editor is enabled.
- Do not make old pages uneditable between the new editor rollout and legacy migration.
- Do not build a long-term legacy compatibility editor.

### Phase 3 - New Neo PageEditor Shell

- Build a new PageEditor shell after the migration bridge exists.
- Add `BlockPalette`.
- Add `PageEditorCanvas`.
- Add `EditorBlockFrame`.
- Add `BlockEditorPreviewHost`.
- Add `PageEditorPropertyPanel`.
- Add `BlockEditorHost`.
- Add the Neo component/primitive catalog.
- Remove the old `RenderBlock(EditorBlock block, bool isSelected)` switch from the new editor only after the migration bridge prevents an editability gap.

### Phase 4 - Neo Composition Model

- Introduce the Neo composition node model for editor composition and `NeoCompositionBlock` payloads.
- Keep Marten page content persistence centered on `BlockBase` blocks. Do not persist a second parallel page tree beside the block list.
- Add or verify `BlockRenderContext.NestingDepth` and `BlockRenderContext.MaxNestingDepth`.
- Implement recursive `NeoCompositionBlockRenderer` and child node rendering with the shared nesting-depth guard.
- Add schema versioning.
- Add catalog validation.
- Add placement validation for parent/child relationships.
- Add serialization tests.

### Phase 5 - Initial Neo Blocks and Primitives

- Implement initial page blocks:
  - Hero 01
  - Basic Hero
  - Neo Image
  - Neo Video
  - Neo Audio
  - Neo Gallery
  - Neo Raw HTML
  - Neo Separator
  - Neo Scriban/Template block
- Implement initial primitives:
  - Button
  - Badge
  - Card
  - Separator
- Each item must have an editor preview, property editor, public renderer/static output, and catalog metadata.
- The Neo Scriban/Template block must preserve the existing author capability to render Scriban-backed dynamic content through the new catalog, preview host, validation, and static SSR renderer path. V1 implements full Scriban permissions; security hardening is explicitly deferred (see Scriban Block Port security deferral note).
- Public renderer output for these blocks must be plain static SSR HTML and must not depend on NeoUI components or assets.

### Phase 6 - Generated Catalog and Typed Render Adapters

- Add `ICmsBlockRenderAdapter<TBlock>`.
- Update generator output.
- Keep existing registry API stable.
- Add adapter resolution tests.
- Extend `BlockRendererGenerator` or related generator output for catalog metadata.
- Verify new block types appear in `GeneratedBlockModelManifest`, generated `BlockBase.Polymorphic.g.cs`, and Marten's `BlockBase` subclass hierarchy through `BlockMartenConfiguration`.
- Replace hardcoded palette categories.
- Keep block/category ordering deterministic.

### Phase 7 - Final Legacy Content Cutover

- Run or require the one-time migration before the old editor path is removed.
- Do not build a legacy compatibility editor.
- Remove old block editor UI and old block authoring code after the chosen content path is complete.
- Add Wolverine/event-handler output-cache eviction for changed legacy/new content before enabling public traffic on the new renderer.

### Phase 8 - Expand Neo Blocks

- Add Feature Grid, Pricing, Testimonials, FAQ, Blog Grid, Contact, Portfolio, and Table/DataGrid variants as product decisions are made.
- Do not add public Blazor interactive islands in V1.
- Keep static SSR as the default for public output.

## Resolved Architectural Decisions

These questions are closed for the first implementation slice:

1. Public renderer overrides are both host-level and theme-level.
   - Host-level override: a consuming site/application can replace the renderer registered for a block type.
   - Theme-level override: a selected site theme can choose a renderer variant for the same semantic block when the host has not replaced it.
   - CSS/theme token overrides remain separate from renderer replacement.
2. Legacy content path is one-time migration.
   - Migrate Boring Hero, Columns, Scriban, Image, Video, Audio, Gallery, Raw HTML, and Separator.
   - Carousel is not included in the first migration set.
3. The first approved Aero UI catalog item is `Hero 01`.
4. Reusable saved custom block patterns are later-phase work.
   - For now, user-composed content is stored as a `NeoCompositionBlock` in Marten only after the public-safe composition renderer exists.
   - Until then, save/publish is limited to typed Aero UI blocks.
5. No public NeoUI components are allowed as Blazor interactive islands in V1.
   - NeoUI components are for the editor/composition experience.
   - Public output is static SSR HTML.
   - Any future public custom behavior should come through explicit Alpine/HTMX work.
6. Initial output-cache invalidation should be fine-grained.
   - Use content/page/site/slug tags such as `content:{id}`, `site:{id}`, and `slug:{slug}`.
   - Keep coarse tags available as an operational fallback, not as the primary V1 behavior.
7. NeoUI does not replace Radzen across the manager.
   - NeoUI is limited to the PageEditor page-composition system and the components/canvas being authored there.
8. The Neo Scriban/Template block uses full Scriban permissions for V1 (`AllowAllFunctions = true`). Security hardening (function allowlist, sandbox, timeouts, sanitization) is explicitly deferred to a follow-up task. The release notes must document this deferred posture and restrict Scriban block authoring to trusted authors until hardening is complete.
9. V1 may support user-composed `NeoCompositionBlock` content only through a public-safe static SSR composition renderer.
   - If that renderer is not implemented and tested, V1 must restrict save/publish to first-class typed Aero UI blocks.
   - Migration runs before typed block coverage is complete should target `NeoCompositionBlock` payloads, then regenerate `LayoutRegions`.

### Remaining Socratic Checkpoints

Ask only if implementation evidence contradicts these decisions:

1. If NeoUI `Sortable` cannot support palette-copy behavior cleanly, should the palette keep NeoUI styling while custom drop code creates new canvas nodes?
2. After V1, should `aero.hero.basic` remain a separate catalog item or become a preset of `aero.hero.01` once the editor supports block presets?
3. If `NeoCompositionBlockRenderer` cannot be completed safely in V1, should V1 ship typed Aero UI blocks only and defer primitive/component save-publish?

## Final Guidance

The target path is a deliberate editor/block rebuild:

```text
move public pages to static SSR Razor components
  -> migrate selected legacy blocks before removing old editability
  -> build new Neo PageEditor shell
  -> introduce trusted Neo component/primitive catalog
  -> persist page content as BlockBase blocks in Marten
  -> generate catalog and renderer metadata
  -> implement new Neo blocks with editor previews
  -> use fine-grained output-cache eviction
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

## Addendum - Implementation Architecture for the Neo Page Editor

This addendum is written for an implementation agent. It is the concrete system architecture for the new PageEditor and page-block system.

### Scope Boundary

This is still a PageEditor and page-rendering refactor only.

Do:

- Use the `NeoUI.Blazor` NuGet package.
- Use `NeoUI.Blazor.Primitives` from NuGet when primitives are needed.
- Use `NeoUI.Icons.Lucide` from NuGet for toolbar and block icons where possible.
- Use the local `NeoUI/` checkout only as reference documentation/source while implementing.
- Use NeoUI `Sortable` for PageEditor drag/drop and canvas sorting.
- Preserve the current PageEditor right-sidebar colors, sizing, spacing, collapsed behavior, and existing `pe-*` visual language unless a change is required for the new component model.

Do not:

- Add a project reference to `./NeoUI`.
- Run npm as part of AeroCMS.
- Replace the entire manager UI with NeoUI.
- Convert unrelated module editors to NeoUI.
- Keep the old `RenderBlock(EditorBlock block, bool isSelected)` switch as the new architecture.
- Add the legacy block list back as the new default authoring model.

### NuGet and Asset Integration

Add package versions to `src/Directory.Packages.props`:

```xml
<PackageVersion Include="NeoUI.Blazor" Version="[pin-approved-version]" />
<PackageVersion Include="NeoUI.Blazor.Primitives" Version="[pin-approved-version]" />
<PackageVersion Include="NeoUI.Icons.Lucide" Version="[pin-approved-version]" />
```

Add package references to `src/Aero.Cms.Shared/Aero.Cms.Shared.csproj` or the smallest shared project that owns the PageEditor components:

```xml
<PackageReference Include="NeoUI.Blazor" />
<PackageReference Include="NeoUI.Blazor.Primitives" />
<PackageReference Include="NeoUI.Icons.Lucide" />
```

Add imports to `src/Aero.Cms.Shared/_Imports.razor`:

```razor
@using NeoUI.Blazor
@using NeoUI.Blazor.Primitives
@using NeoUI.Icons.Lucide
```

Register NeoUI services in the host app that runs the manager UI:

```csharp
using NeoUI.Blazor.Extensions;
using NeoUI.Blazor.Primitives.Extensions;

builder.Services.AddNeoUIComponents();
builder.Services.AddNeoUIPrimitives();
```

Load NeoUI assets for manager/editor surfaces only. Public static SSR CMS renderers must not require these assets.

```razor
<link href="@Assets["_content/NeoUI.Blazor/components.css"]" rel="stylesheet" />
<script src="@Assets["_content/NeoUI.Blazor/js/theme.js"]"></script>
```

If AeroCMS can split manager and public layouts, place these assets in the manager layout only. If the current host layout is shared during the first slice, keep public renderers independent from NeoUI components/assets and create a follow-up task to split manager/public asset loading.

Wrap only the PageEditor or manager editor subtree that uses NeoUI:

```razor
<AppProvider>
    @Body

    <ToastViewport />
    <DialogHost />
</AppProvider>

<ContainerPortalHost />
<OverlayPortalHost />
```

The `AppProvider` boundary is for NeoUI components that need theme/style cascades and portals. It is not a signal to rewrite all manager pages.

### Reference NeoUI Source Folders

Use these local paths only for implementation reference:

```text
NeoUI/src/NeoUI.Blazor/Components
NeoUI/src/NeoUI.Blazor/Components/Sortable
NeoUI/src/NeoUI.Blazor.Primitives/Primitives
NeoUI/llms/*.txt
```

The PageEditor toolbar should be catalog-driven from approved items, not generated by scanning these folders at runtime.

### Target Folder Structure

Create the new PageEditor architecture under `src/Aero.Cms.Shared/Pages/Manager/PageEditor`.

Recommended structure:

```text
src/Aero.Cms.Shared/Pages/Manager/PageEditor/
  PageEditor.razor
  PageEditor.razor.cs
  ToastMessage.cs

  Catalog/
    NeoEditorCatalog.cs
    NeoEditorCatalogItem.cs
    NeoEditorCatalogSection.cs
    NeoEditorCatalogKind.cs
    NeoEditorCatalogProvider.cs
    INeoEditorCatalogProvider.cs
    NeoEditorCatalogValidator.cs

  Models/
    NeoPageNode.cs
    NeoPageNodeKind.cs
    NeoCompositionBlock.cs
    NeoPageDocumentModel.cs
    NeoNodePropertyBag.cs
    NeoNodePlacementRules.cs
    NeoPageEditorState.cs
    NeoPropertyDefinition.cs
    NeoPropertyFieldType.cs

  Shell/
    PageEditorShell.razor
    PageEditorHeader.razor
    PageEditorTabs.razor
    NeoPageEditorProvider.razor

  Palette/
    PageEditorPalette.razor
    PageEditorPaletteSection.razor
    PageEditorPaletteItem.razor
    PageEditorPaletteSearch.razor

  Canvas/
    PageEditorCanvas.razor
    PageEditorCanvasDropZone.razor
    EditorNodeFrame.razor
    EditorNodeToolbar.razor
    BlockEditorPreviewHost.razor
    ComponentEditorPreviewHost.razor

  Properties/
    PageEditorPropertyPanel.razor
    PropertyEditorHost.razor
    PropertyFieldRenderer.razor

  DragDrop/
    PageEditorDragItem.cs
    PageEditorDragDropService.cs
    PageEditorDropResult.cs

  AeroUi/
    Hero01/
      Hero01Block.cs
      Hero01BlockRenderer.razor
      Hero01BlockEditorPreview.razor
      Hero01BlockEditor.razor
      Hero01BlockMapper.cs

  Primitives/
    PrimitiveCatalogItems.cs
    PrimitivePreviewRenderer.razor
    PrimitivePropertyEditor.razor

  Components/
    ComponentCatalogItems.cs
    ComponentPreviewRenderer.razor
    ComponentPropertyEditor.razor
```

Longer-term, once the design stabilizes, reusable model and catalog pieces can move out of `PageEditor` into `src/Aero.Cms.Shared/Blocks/Editing` or `src/Aero.Cms.Abstractions/Blocks/Editing`. For the first implementation slice, keep the work close to PageEditor to avoid affecting unrelated modules.

### PageEditor Component Responsibilities

`PageEditor.razor` should become an orchestrator only:

```text
PageEditor.razor
  -> PageEditorShell
     -> PageEditorHeader
     -> PageEditorTabs
     -> PageEditorCanvas
        -> EditorNodeFrame
        -> BlockEditorPreviewHost
     -> PageEditorPropertyPanel
     -> PageEditorPalette in RightSidebar section
```

`PageEditor.razor.cs` owns loading, saving, publishing, preview URL behavior, dirty state, selected node state, and conversion between persisted page content and the editor model.

The primary editor surface should remain visual and block-first. A later tree-view/outline may help users navigate large pages, but V1 should not require that tree-view for normal editing. The tree-view, when added, should display top-level block placements and nested composition nodes as an outline over existing data.

It should not contain:

- per-block preview markup
- per-block property editor markup
- hardcoded palette contents
- old block-specific switch rendering

### Right Sidebar Menu Sections

The right sidebar should keep the current PageEditor visual style, but its content should be catalog-driven.

Order:

1. `Aero UI`
2. `Primitives`
3. `Components`

`Aero UI` contains Aero-authored page blocks and patterns. The first items are `Hero 01` and `Basic Hero`; `Hero 01` is the first approved new block and `Basic Hero` is the migration target for legacy `boring_hero`.

`Primitives` is the section reserved for approved headless primitives from `NeoUI.Blazor.Primitives`. Treat these as low-level authoring pieces for composing a block. Do not expose the whole primitive library automatically.

```text
Accordion
Checkbox
Collapsible
Dialog
DropdownMenu
HoverCard
InputOtp
Label
NavigationMenu
Popover
RadioGroup
Select
Sheet
Sortable
Switch
Table
Tabs
Tooltip
```

Do not expose every primitive automatically. Start with a small approved list that can be rendered safely in editor preview and public output. `Sortable` itself is an editor infrastructure component first; do not offer it as a public page primitive until there is a product decision for user-authored sortable UI.

`Components` is the section reserved for approved styled NeoUI components from `NeoUI.Blazor.Components`. Do not expose the whole component library automatically.

```text
Badge
Button
Card
Checkbox
Dialog
DropdownMenu
Select
Separator
Tabs
Tooltip
Typography
```

Components that imply complex data, scripts, or interactivity, such as DataGrid/DataTable, MarkdownEditor, DynamicForm, Command, Drawer, or Motion, must be individually approved before they become page-builder items.

For the first implementation slice, the only fully approved new catalog item is `Hero 01`, plus `Basic Hero` as the required migration target for `boring_hero`. Build the section architecture now, but enable primitives/components item-by-item after the Hero path proves the model.

### Catalog Model

The catalog is the source of truth for what can be dragged into the editor.

```csharp
public enum NeoEditorCatalogSection
{
    AeroUi,
    Primitives,
    Components
}

public enum NeoEditorCatalogKind
{
    Block,
    Primitive,
    Component
}

public sealed record NeoEditorCatalogItem
{
    public required string CatalogId { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required NeoEditorCatalogSection Section { get; init; }
    public required NeoEditorCatalogKind Kind { get; init; }
    public string IconName { get; init; } = "box";
    public int SortOrder { get; init; }
    public bool AllowChildren { get; init; }
    public bool PublicStaticSsrSafe { get; init; } = true;
    public bool RequiresInteractiveIsland { get; init; }
    public Type? EditorPreviewComponentType { get; init; }
    public Type? PropertyEditorComponentType { get; init; }
    public Type? PublicRendererComponentType { get; init; }
    public IReadOnlyList<NeoPropertyDefinition> PropertyDefinitions { get; init; } = [];
    public IReadOnlySet<string> AllowedChildCatalogIds { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> AllowedParentCatalogIds { get; init; } = new HashSet<string>();
}
```

Generated block metadata may still use string categories because `BlockMetadataAttribute.Category` is currently string-based. The catalog boundary must normalize those strings into `NeoEditorCatalogSection` values with an explicit mapper, not ad hoc string comparison.

```csharp
public static class NeoCatalogSectionMapper
{
    public static bool TryMap(string? category, out NeoEditorCatalogSection section)
    {
        switch (category?.Trim().ToUpperInvariant())
        {
            case "AERO UI":
                section = NeoEditorCatalogSection.AeroUi;
                return true;
            case "PRIMITIVES":
                section = NeoEditorCatalogSection.Primitives;
                return true;
            case "COMPONENTS":
                section = NeoEditorCatalogSection.Components;
                return true;
            default:
                section = default;
                return false;
        }
    }
}
```

The source generator or catalog provider should fail fast when a block advertises an unknown category. Do not silently drop a block into a default section.

`AllowedChildCatalogIds` answers "what can I contain?" `AllowedParentCatalogIds` answers "where can I be dropped?" Keep both. The inverse parent set avoids repeated whole-catalog scans during drag/drop validation and makes invalid placement messages easier to produce.

`PropertyDefinitions` is required for catalog items that use the generic property panel. A catalog item may instead provide a dedicated `PropertyEditorComponentType`, but it should still declare the persisted property keys so validation can run outside the UI.

Build the catalog infrastructure first, not the entire catalog. Avoid circular references by registering catalog items beside the components they describe. For example, `Hero01` contributes its catalog item from the `AeroUi/Hero01` folder after `Hero01BlockEditorPreview`, `Hero01BlockEditor`, and `Hero01BlockRenderer` exist.

Catalog IDs must be stable and explicit:

```text
aero.hero.01
primitive.tabs
primitive.tooltip
component.badge
component.button
component.card
component.typography
```

Do not persist raw .NET component type names as user-authored content. Persist catalog IDs and validated property bags.

### Editor Node Model and Persistence Rule

The editor should use structured composition data inside composition-capable blocks instead of the old all-purpose `EditorBlock` bag, but the page persistence contract is still `BlockBase` in Marten.

Persistence decision for V1:

- `PageDocument` page content is persisted as `BlockBase` blocks through the existing Marten/page pipeline.
- A first-class Aero UI block such as `Hero 01` persists as a typed `Hero01Block : BlockBase`.
- A user-composed primitive/component tree persists as a `NeoCompositionBlock : BlockBase` that contains validated `NeoPageNode` children.
- Do not persist a second parallel page tree beside `BlockBase` output.
- Do not persist both a separate `NeoPageNode` page document and duplicate typed block output for the same page.
- `PageEditorState.Blocks` remains a flat list of top-level `EditorBlockPlacement` entries.
- A visual tree/outline is allowed later as a UX projection, not as the V1 storage model.

```csharp
using System.Text.Json.Nodes;

public enum NeoPageNodeKind
{
    Block,
    Primitive,
    Component,
    Layout
}

public sealed class NeoPageNode
{
    public string NodeId { get; set; } = Guid.NewGuid().ToString("N");
    public required string CatalogId { get; set; }
    public required NeoPageNodeKind Kind { get; set; }
    public int Order { get; set; }
    public JsonObject Properties { get; set; } = [];
    public List<NeoPageNode> Children { get; set; } = [];
}
```

Composition block:

```csharp
using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

[BlockMetadata(
    "neo.composition",
    "Neo Composition",
    Category = "Aero UI",
    Icon = "layout-template",
    SortOrder = 100,
    SchemaVersion = 1)]
public sealed class NeoCompositionBlock : BlockBase
{
    public override string BlockType => "neo.composition";

    public string Name { get; set; } = "Custom Block";
    public List<NeoPageNode> Nodes { get; set; } = [];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
```

`Accept(...)` remains because `BlockBase` requires the visitor contract. The new public render path should not depend on the old visitor for Neo composition rendering; it should use the generated block renderer adapter and `NeoCompositionBlockRenderer`.

Validation rules:

- `CatalogId` must exist in `NeoEditorCatalog`.
- `Kind` must match the catalog item.
- Child placement must be allowed by the parent catalog item.
- Property keys must be declared by the catalog/property schema.
- Property values must validate before save.
- No arbitrary Razor, C#, JavaScript, service names, or runtime component type names may be persisted from users.

`JsonObject` is only the storage representation for flexible property bags. It is not the validation system. Schema enforcement comes from `NeoEditorCatalogItem.PropertyDefinitions`, dedicated block validators, and `NeoEditorCatalogValidator`.

Marten serialization for `NeoCompositionBlock` must be tested before migration is enabled. `JsonObject`/`JsonNode` payloads require the configured Marten serializer to support `System.Text.Json.Nodes`; if the current serializer does not handle them cleanly, replace the flexible property bag with a supported dictionary shape such as `Dictionary<string, JsonElement>` before broad data migration.

### LayoutRegions and Published Placement

`PageDocument.LayoutRegions` is not dead state. It is the current published placement manifest.

Current behavior:

- Existing/live `PageDocument.Blocks` stores editor-facing block data before the PageDocument refactor.
- `PageDocument.LayoutRegions` stores render-facing placement data.
- Existing/live `LayoutRegionRenderer` and `LayoutColumnRenderer` render the current region/column shape.
- `BlockPlacementRenderer` loads each `BlockBase` by `BlockPlacement.BlockId` and delegates to `BlockRenderer`.
- `PageContentService.ProcessEditorBlocks(...)` currently turns editor blocks into saved `BlockBase` documents, `BlockIdMap`, and one full-width `Main` region.

New Neo behavior:

```text
Neo editor state
  -> validate catalog nodes
  -> map first-class Aero UI nodes to typed BlockBase documents
  -> map user-composed node trees to NeoCompositionBlock documents
  -> save BlockBase documents through the block service
  -> [DRAFT SAVE] update BlockIdMap on PageEditorState (not PageDocument)
  -> [PUBLISH]    generate PageDocument.LayoutRegions via IPageLayoutManifestBuilder
  -> public static SSR page renders LayoutRegions
```

Key data-contract changes from the old model (aligned with `aero-page-document-refactor.md`):

- **`PageDocument.Blocks` is removed.** Editor block placement is no longer stored on `PageDocument`. It lives in `PageEditorState.EditorBlockPlacement[]`.
- **`PageDocument.BlockIdMap` is removed.** The ClientId→BlockId map lives in `PageEditorState.BlockIdMap`, rebuilt on every draft save.
- **`PageDocument.LayoutRegions` is preserved** as the published render manifest. Written only by the publish path — draft saves never touch it.
- **`PageEditorState` is a separate document** with `DraftVersion`, `Blocks` (as `EditorBlockPlacement[]`), `BlockIdMap`, and `LastModified`. Hard-deleted when its page is deleted.

This keeps the public rendering contract intact while replacing the editor authoring model. In the target model, the first slice may continue generating a single `main` region with ordered placements. More advanced multi-region layout editing can be added later without changing the public block renderer contract. Do not reintroduce `LayoutColumn` as part of `PageDocument.LayoutRegions`.

Migration must also handle `LayoutRegions`-only pages. Seeded or older pages can render today even when `PageDocument.Blocks` is empty because public pages read `LayoutRegions`. Align with `aero-page-document-refactor.md`: the PageDocument migration first creates an empty `PageEditorState` for these pages so they remain safe and show as published/no draft changes. A later Neo legacy-content migration may optionally load the placed `BlockBase` documents referenced by `LayoutRegions`, convert supported legacy placements into editable Neo blocks or `NeoCompositionBlock` payloads, rebuild `PageEditorState.BlockIdMap`, and republish through `IPageLayoutManifestBuilder`.

### NeoCompositionBlock Public Rendering

V1 supports user-composed primitive/component trees only if `NeoCompositionBlock` has a public static SSR renderer before those catalog items are enabled for public pages.

The renderer must be catalog-driven and public-safe:

- Resolve each `NeoPageNode.CatalogId` through `NeoEditorCatalog`.
- Reject or render an explicit safe error for unknown catalog IDs.
- Require `PublicStaticSsrSafe = true`.
- Require a `PublicRendererComponentType`.
- Render only public renderer components that output static HTML.
- Never render NeoUI editor components on public pages.
- Never use persisted runtime type names from user content.
- Never add an interactive render mode.

Microsoft Learn documents `DynamicComponent` as the supported way to render a Razor component from a trusted `Type` and parameter dictionary. Static SSR renders Razor components to HTML and discards component state after the response, so event handlers are not interactive. That makes catalog-driven dynamic dispatch acceptable for trusted public renderer component types, but not for user-supplied type names or NeoUI manager components.

Example renderer shape:

```razor
@using Microsoft.AspNetCore.Components

@foreach (var node in Block.Nodes.OrderBy(n => n.Order))
{
    <NeoCompositionNodeRenderer Node="node" Context="Context" />
}

@code {
    [Parameter, EditorRequired]
    public NeoCompositionBlock Block { get; set; } = default!;

    [Parameter, EditorRequired]
    public BlockRenderContext Context { get; set; } = new();
}
```

```razor
@using Microsoft.AspNetCore.Components

@if (Context.NestingDepth >= BlockRenderContext.MaxNestingDepth)
{
    <div class="cms-render-warning">Composition nesting limit exceeded.</div>
}
else if (RendererType is not null)
{
    <DynamicComponent Type="RendererType" Parameters="RendererParameters" />
}

@code {
    [Parameter, EditorRequired]
    public NeoPageNode Node { get; set; } = default!;

    [Parameter, EditorRequired]
    public BlockRenderContext Context { get; set; } = new();

    [Inject]
    public INeoEditorCatalog Catalog { get; set; } = default!;

    private BlockRenderContext ChildContext => Context with
    {
        NestingDepth = Context.NestingDepth + 1
    };

    private Type? RendererType => Catalog.TryGet(Node.CatalogId, out var item)
        && item.PublicStaticSsrSafe
        && !item.RequiresInteractiveIsland
        ? item.PublicRendererComponentType
        : null;

    private IDictionary<string, object?> RendererParameters => new Dictionary<string, object?>
    {
        ["Node"] = Node,
        ["Context"] = ChildContext
    };
}
```

Each public node renderer is responsible for recursively rendering approved children where children are allowed. The child renderer should pass `ChildContext`, not the original context, so every nesting step increments `BlockRenderContext.NestingDepth`.

For leaf nodes, the public renderer ignores `Node.Children`. For container nodes, the public renderer must explicitly render children:

```razor
@foreach (var child in Node.Children.OrderBy(n => n.Order))
{
    <NeoCompositionNodeRenderer Node="child" Context="Context" />
}
```

The top-level composition renderer and every container node renderer must share the same depth contract:

- Stop rendering when `Context.NestingDepth >= BlockRenderContext.MaxNestingDepth`.
- Produce safe diagnostic output in preview.
- Produce either no output or a safe non-interactive diagnostic on public pages.
- Add a validation error before save if the authored tree already exceeds the limit.

If the implementation cannot complete recursive rendering safely in V1, then V1 must restrict the public/editor catalog to typed Aero UI blocks only and defer user-composed primitive/component public rendering.

### Drag and Drop Architecture

Use NeoUI `Sortable` for:

- Page canvas node reordering.
- Reordering children inside container/layout nodes.
- Dragging approved catalog entries from the right sidebar onto the canvas, if the final tested behavior supports source-list copy.

Important behavior:

- Palette items are catalog entries. Dropping one onto the canvas creates a new `NeoPageNode`.
- Palette items should not be removed from the palette when dragged.
- Canvas items are persisted editor nodes. Reordering them changes `Order`.
- A drag from one canvas container to another moves the existing node.

Palette example:

```razor
@using NeoUI.Blazor

<Sortable TItem="NeoEditorCatalogItem"
          Items="Items"
          GetItemId="item => item.CatalogId"
          Group="page-editor-catalog"
          OnItemTransferredOut="OnPaletteTransferOut"
          Class="pe-category-items">
    <SortableContent>
        @foreach (var item in Items)
        {
            <SortableItem Value="@item.CatalogId" AsHandle="true" Class="pe-block-item">
                <LucideIcon Name="@item.IconName" Size="18" />
                @if (!IsCollapsed)
                {
                    <span>@item.DisplayName</span>
                }
            </SortableItem>
        }
    </SortableContent>
</Sortable>

@code {
    [Parameter, EditorRequired]
    public IList<NeoEditorCatalogItem> Items { get; set; } = [];

    [Parameter]
    public bool IsCollapsed { get; set; }

    private Task OnPaletteTransferOut(SortableTransferArgs args)
    {
        // No-op by design. The palette is a copy source, not a mutable list.
        return Task.CompletedTask;
    }
}
```

Canvas example:

```razor
@using NeoUI.Blazor

<Sortable TItem="NeoPageNode"
          Items="Nodes"
          GetItemId="node => node.NodeId"
          Group="page-editor-canvas"
          OnItemsReordered="OnNodesReordered"
          OnItemTransferredIn="OnCatalogItemDropped"
          OnCanDrop="CanDrop"
          Class="pe-blocks-container">
    <SortableContent Class="gap-3">
        @foreach (var node in Nodes)
        {
            <SortableItem Value="@node.NodeId" Class="pe-block-wrapper">
                <EditorNodeFrame Node="node"
                                 Selected="node.NodeId == SelectedNodeId"
                                 OnSelected="SelectNode" />
            </SortableItem>
        }
    </SortableContent>
</Sortable>
```

The actual transfer implementation may need one shared group for palette-to-canvas and per-container groups for nested canvas movement after testing NeoUI `Sortable` behavior. Keep the rule above: palette copy, canvas move.

### Preview Host Architecture

Use Blazor `DynamicComponent` for editor previews and property editors. Microsoft Learn documents `DynamicComponent` for rendering components by type and passing a parameter dictionary; use that pattern with explicit catalog metadata.

```razor
@if (Catalog.TryGet(Node.CatalogId, out var item) &&
     item.EditorPreviewComponentType is not null)
{
    <DynamicComponent Type="item.EditorPreviewComponentType"
                      Parameters="Parameters" />
}
else
{
    <div class="pe-block-placeholder">
        Unknown editor preview: @Node.CatalogId
    </div>
}

@code {
    [Parameter, EditorRequired]
    public NeoPageNode Node { get; set; } = default!;

    [Inject]
    public INeoEditorCatalogProvider Catalog { get; set; } = default!;

    private Dictionary<string, object?> Parameters => new()
    {
        [nameof(INeoEditorPreview.Node)] = Node
    };
}
```

Prefer typed preview/property interfaces where possible:

```csharp
public interface INeoEditorPreview
{
    NeoPageNode Node { get; set; }
}
```

### Public Rendering Architecture

Public rendering still goes through:

```text
BlockRenderer.razor
  -> CmsBlockRenderRegistry
  -> ICmsBlockRenderAdapter
  -> block-specific Razor renderer
```

Aero UI blocks should persist as typed `BlockBase` models where they are first-class blocks. User-composed primitive/component trees should persist as `NeoCompositionBlock : BlockBase`.

This is the single persistence rule for V1:

```text
PageEditor state
  -> validated BlockBase list
     -> Hero01Block for Hero 01
     -> BasicHeroBlock for Basic Hero
     -> NeoCompositionBlock for composed primitive/component trees
  -> save BlockBase documents
  -> [DRAFT SAVE] update PageEditorState.Blocks and PageEditorState.BlockIdMap
  -> [PUBLISH] generate PageDocument.LayoutRegions through IPageLayoutManifestBuilder
  -> Marten persists the affected documents through the existing page-service pipeline
```

There is no separate Neo page tree persisted beside the block list. `LayoutRegions` is not a second content tree; it is the published placement manifest that points at saved block documents.

Public renderers must be public-safe Razor components:

- no `@using NeoUI.Blazor`
- no `<Badge>`, `<Button>`, `<LucideIcon>`, or other NeoUI component tags
- no dependency on `_content/NeoUI.Blazor/components.css`
- no Blazor interactive islands in V1
- inline SVGs or existing public icon strategy for icons
- CSS variables and Tailwind utility classes are allowed when already part of the public site styling contract

`NeoCompositionBlock` public rendering is mandatory before composed primitive/component trees are enabled on public pages. Until that renderer and its tests exist, the editor catalog must expose only first-class typed Aero UI blocks for save/publish. The right sidebar may show future `Primitives` and `Components` sections as empty/deferred sections, but not save public content that has no renderer.

### First Aero UI Block - Hero 01

Start by adding `Hero 01` to the `Aero UI` section. This is a new Aero-authored block based on the NeoUI block at `https://neoui.io/blocks/hero-01`.

Catalog entry:

```csharp
new NeoEditorCatalogItem
{
    CatalogId = "aero.hero.01",
    DisplayName = "Hero 01",
    Description = "Centered hero with badge, headline, actions, and trust markers.",
    Section = NeoEditorCatalogSection.AeroUi,
    Kind = NeoEditorCatalogKind.Block,
    IconName = "sparkles",
    SortOrder = 10,
    PublicStaticSsrSafe = true,
    EditorPreviewComponentType = typeof(Hero01BlockEditorPreview),
    PropertyEditorComponentType = typeof(Hero01BlockEditor),
    PublicRendererComponentType = typeof(Hero01BlockRenderer),
    PropertyDefinitions =
    [
        new() { Name = "eyebrow", Label = "Eyebrow", FieldType = NeoPropertyFieldType.Text },
        new() { Name = "title", Label = "Title", FieldType = NeoPropertyFieldType.Text, Required = true },
        new() { Name = "highlight", Label = "Highlighted Text", FieldType = NeoPropertyFieldType.Text },
        new() { Name = "description", Label = "Description", FieldType = NeoPropertyFieldType.TextArea },
        new() { Name = "primaryText", Label = "Primary Button Text", FieldType = NeoPropertyFieldType.Text },
        new() { Name = "primaryUrl", Label = "Primary Button URL", FieldType = NeoPropertyFieldType.Url },
        new() { Name = "secondaryText", Label = "Secondary Button Text", FieldType = NeoPropertyFieldType.Text },
        new() { Name = "secondaryUrl", Label = "Secondary Button URL", FieldType = NeoPropertyFieldType.Url },
        new() { Name = "trustMarkers", Label = "Trust Markers", FieldType = NeoPropertyFieldType.StringList }
    ]
}
```

Block model:

```csharp
using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Abstractions.Blocks.Neo;

[BlockMetadata(
    "aero.hero.01",
    "Hero 01",
    Category = "Aero UI",
    Icon = "sparkles",
    SortOrder = 10,
    SchemaVersion = 1)]
public sealed class Hero01Block : BlockBase
{
    public override string BlockType => "aero.hero.01";

    public string Eyebrow { get; set; } = "Introducing NeoUI v3";
    public string Title { get; set; } = "Build beautiful Blazor apps";
    public string Highlight { get; set; } = "faster than ever";
    public string Description { get; set; } =
        "100+ production-ready components for .NET Blazor. Accessible, customizable, and built for speed. Start shipping in minutes, not days.";
    public string PrimaryText { get; set; } = "Get started for free";
    public string PrimaryUrl { get; set; } = "#";
    public string SecondaryText { get; set; } = "View on GitHub";
    public string SecondaryUrl { get; set; } = "#";
    public List<string> TrustMarkers { get; set; } =
    [
        "Free & open source",
        ".NET 8+ compatible",
        "Dark mode included",
        "100+ components"
    ];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
```

Public renderer:

The public renderer is intentionally plain Razor/HTML. The editor preview may use NeoUI components, but this public renderer must not import `NeoUI.Blazor`, render NeoUI component tags, or require NeoUI CSS. The original NeoUI snippet uses `@container` and `@md:` class tokens; those are not valid as-is in Razor. For this public sample, use ordinary `container` and `md:` utilities unless container-query support is explicitly added and escaped/tested.

```razor
@using Aero.Cms.Abstractions.Blocks.Neo

<section class="container flex flex-col items-center justify-center min-h-[460px] w-full bg-background px-6 py-16 text-center">
    <div class="inline-flex items-center rounded-md border border-border bg-muted px-2.5 py-0.5 text-xs font-semibold text-muted-foreground mb-4 gap-1.5">
        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
             stroke-linecap="round" stroke-linejoin="round" class="text-primary" aria-hidden="true">
            <path d="M12 3l1.9 5.8L20 10.5l-5 3.7L16.8 21 12 17.4 7.2 21 9 14.2l-5-3.7 6.1-1.7L12 3z" />
        </svg>
        @Block.Eyebrow
    </div>

    <h1 class="text-4xl md:text-5xl font-bold tracking-tight text-foreground max-w-3xl leading-tight mb-4">
        @Block.Title<br class="hidden md:block" />
        <span class="text-primary">@Block.Highlight</span>
    </h1>

    <p class="text-lg text-muted-foreground max-w-xl leading-relaxed mb-8">
        @Block.Description
    </p>

    <div class="flex flex-col md:flex-row items-center gap-3">
        <a href="@Block.PrimaryUrl" class="inline-flex items-center justify-center gap-2 rounded-md bg-primary px-6 py-3 text-sm font-medium text-primary-foreground shadow hover:opacity-90">
            @Block.PrimaryText
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                 stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M5 12h14" />
                <path d="M12 5l7 7-7 7" />
            </svg>
        </a>
        <a href="@Block.SecondaryUrl" class="inline-flex items-center justify-center gap-2 rounded-md border border-input bg-background px-6 py-3 text-sm font-medium text-foreground shadow-sm hover:bg-muted">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                <path fill-rule="evenodd" clip-rule="evenodd" d="M12 .5A11.5 11.5 0 0 0 8.36 22.9c.58.11.79-.25.79-.56v-2.02c-3.22.7-3.9-1.38-3.9-1.38-.53-1.34-1.3-1.7-1.3-1.7-1.06-.72.08-.71.08-.71 1.17.08 1.79 1.2 1.79 1.2 1.04 1.78 2.73 1.27 3.4.97.11-.75.41-1.27.74-1.56-2.57-.29-5.27-1.28-5.27-5.72 0-1.26.45-2.3 1.2-3.11-.12-.29-.52-1.47.11-3.07 0 0 .98-.31 3.2 1.19a11.1 11.1 0 0 1 5.82 0c2.22-1.5 3.2-1.19 3.2-1.19.63 1.6.23 2.78.11 3.07.75.81 1.2 1.85 1.2 3.11 0 4.45-2.71 5.42-5.29 5.71.42.36.79 1.07.79 2.16v3.05c0 .31.21.68.8.56A11.5 11.5 0 0 0 12 .5Z" />
            </svg>
            @Block.SecondaryText
        </a>
    </div>

    <div class="mt-12 flex items-center gap-6 text-sm text-muted-foreground flex-wrap justify-center">
        @foreach (var marker in Block.TrustMarkers)
        {
            <div class="flex items-center gap-1.5">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                     stroke-linecap="round" stroke-linejoin="round" class="text-primary" aria-hidden="true">
                    <circle cx="12" cy="12" r="10" />
                    <path d="m9 12 2 2 4-4" />
                </svg>
                @marker
            </div>
        }
    </div>
</section>

@code {
    [Parameter, EditorRequired]
    public Hero01Block Block { get; set; } = default!;
}
```

Editor preview:

```razor
@using Aero.Cms.Abstractions.Blocks.Neo

<div class="pe-neo-preview pe-neo-hero-preview">
    <Hero01BlockRenderer Block="PreviewBlock" />
</div>

@code {
    [Parameter, EditorRequired]
    public NeoPageNode Node { get; set; } = default!;

    private Hero01Block PreviewBlock => Hero01BlockMapper.FromNode(Node);
}
```

Mapper:

```csharp
using System.Text.Json.Nodes;
using Aero.Cms.Abstractions.Blocks.Neo;

public static class Hero01BlockMapper
{
    public static NeoPageNode ToNode(Hero01Block block) => new()
    {
        CatalogId = "aero.hero.01",
        Kind = NeoPageNodeKind.Block,
        Properties = new JsonObject
        {
            ["eyebrow"] = block.Eyebrow,
            ["title"] = block.Title,
            ["highlight"] = block.Highlight,
            ["description"] = block.Description,
            ["primaryText"] = block.PrimaryText,
            ["primaryUrl"] = block.PrimaryUrl,
            ["secondaryText"] = block.SecondaryText,
            ["secondaryUrl"] = block.SecondaryUrl,
            ["trustMarkers"] = new JsonArray(block.TrustMarkers.Select(m => JsonValue.Create(m)).ToArray())
        }
    };

    public static Hero01Block FromNode(NeoPageNode node) => new()
    {
        Eyebrow = GetString(node, "eyebrow", "Introducing NeoUI v3"),
        Title = GetString(node, "title", "Build beautiful Blazor apps"),
        Highlight = GetString(node, "highlight", "faster than ever"),
        Description = GetString(node, "description", string.Empty),
        PrimaryText = GetString(node, "primaryText", "Get started for free"),
        PrimaryUrl = GetString(node, "primaryUrl", "#"),
        SecondaryText = GetString(node, "secondaryText", "View on GitHub"),
        SecondaryUrl = GetString(node, "secondaryUrl", "#"),
        TrustMarkers = node.Properties["trustMarkers"] is JsonArray array
            ? array.Select(x => x?.GetValue<string>() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList()
            : []
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetPropertyValue(key, out var value)
            ? value?.GetValue<string>() ?? fallback
            : fallback;
}
```

The mapper is intentionally explicit. Do not use arbitrary reflection to map property bags to models.

### Basic Hero Legacy Port

Also port the old `boring_hero` concept as a new Aero UI block. Keep it separate in V1 so the one-time migration has a deterministic target. It can become a preset later if the editor gains reusable preset support.

Recommended catalog ID:

```text
aero.hero.basic
```

Purpose:

- centered page intro
- title
- subtitle
- optional background image
- optional call-to-action

Do not keep the old `boring_hero` editor type as the new authoring ID. If existing content must survive, map `boring_hero` to `aero.hero.basic` in the migration/upgrade service.

### Primitives and Components Authoring Rules

Primitives and components are not automatically public blocks. They are page-composition nodes.

Rules:

- A primitive/component must have a catalog item before it appears in the toolbar.
- Every catalog item must define whether it can have children.
- Every catalog item must define which parents it can be dropped into.
- Every catalog item must define which property editor fields are allowed.
- Every catalog item must provide an editor preview.
- Public rendering must be static SSR-safe. Do not mark catalog items as public interactive islands in V1.

First approved editor catalog slice:

```text
Aero UI:
  - Hero 01
  - Basic Hero

Primitives (section scaffold only until individually approved):
  - Tabs
  - Tooltip
  - Dialog
  - Sheet
  - Table
  - Select
  - Checkbox
  - Switch

Components (section scaffold only until individually approved):
  - Badge
  - Button
  - Card
  - Separator
  - Typography
  - Tabs
  - Tooltip
```

The first slice should fully implement `Hero 01` and `Basic Hero`. The primitive/component lists above are candidate section scaffolds, not blanket approval to expose every item immediately. Enable them item-by-item after the Hero path proves catalog validation, property editing, preview, persistence, and public rendering. These entries are editor/composition concepts only; public renderers must translate them to static SSR HTML or store them inside a public-safe `NeoCompositionBlock` renderer.

### Property Editing

Use property metadata instead of hand-coded forms in `PageEditor.razor`.

Example field schema:

```csharp
public sealed record NeoPropertyDefinition
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required NeoPropertyFieldType FieldType { get; init; }
    public bool Required { get; init; }
    public string? DefaultValue { get; init; }
    public IReadOnlyList<string> Options { get; init; } = [];
}

public enum NeoPropertyFieldType
{
    Text,
    TextArea,
    Url,
    Boolean,
    Select,
    Number,
    StringList,
    RichText,
    Json
}
```

The property panel renders fields from the selected node's catalog metadata. Complex blocks may still use a dedicated property editor component, but that component should live beside the block, not inside `PageEditor.razor`.

### Save and Publish Flow

Per the data model in `aero-page-document-refactor.md`, draft save and publish are **strictly separated** pipelines. A single "save" operation may trigger both (save draft then publish), but the two paths must not be conflated — `LayoutRegions` is written only by the publish path.

#### Draft Save Path

```text
PageEditor state
  -> validate NeoPageNode tree against catalog
  -> map Aero UI nodes to typed BlockBase models
  -> map composed primitive/component trees to NeoCompositionBlock
  -> persist the resulting BlockBase documents through the block service
  -> update PageEditorState:
       Blocks      = EditorBlockPlacement[] from current tree
       BlockIdMap  = rebuilt mapping of ClientId → BlockBase.Id
       DraftVersion = increment
       LastModified = now
  -> emit PageMetadataUpdated event (metadata-only — no blocks, no LayoutRegions)
  -> evict output cache tags for this page
```

The draft save **does NOT** write to `PageDocument.LayoutRegions`. LayoutRegions belongs exclusively to the publish path. This is a hard rule — see `IPageLayoutManifestBuilder` and the publish pipeline below.

#### Publish Path

```text
PageEditor state (latest draft)
  -> load all BlockBase documents referenced by PageEditorState.Blocks
  -> call IPageLayoutManifestBuilder.BuildAsync(editor, blocks)
       -> produces IReadOnlyList<LayoutRegion>
  -> compute next version: newVersion = PageDocument.PublishedVersion + 1
  -> append PagePublished event:
       PagePublished {
           PageId,
           Version:   newVersion,
           LayoutRegions: builtRegions,
       }
  -> PageDocument.Apply(PagePublished) writes LayoutRegions + bumps PublishedVersion
  -> PageEditorState.DraftVersion is NOT modified by publish
       (DraftVersion > PublishedVersion remains true — the editor knows
        the last draft hasn't been superseded by a new draft save)
  -> evict output cache tags
```

Do not bypass the existing page save/publish pipeline. The new editor model should feed it. Do not persist a separate Neo page tree beside the block list. Do not skip `LayoutRegions`; without ordered `BlockPlacement` entries, public page rendering has no published manifest to render.

### Output Cache Rules for New Blocks

New Aero UI blocks and public renderers must work with the existing output-cache module.

Rules:

- Public static SSR pages remain cacheable under the existing CMS page policies.
- Editor preview routes are not output cached.
- Draft preview routes are not output cached.
- Public blocks must not render per-user content inside cached output.
- Public Blazor interactive islands are not part of V1.
- Publishing or updating a page containing Neo blocks must evict the same page/content tags as legacy page updates.
- Use fine-grained eviction tags for page/content/site/slug changes.

### Tests the Implementing Agent Must Add

Add focused tests for the new architecture:

- Catalog contains sections in this order: `Aero UI`, `Primitives`, `Components`.
- `Hero 01` and `Basic Hero` appear in `Aero UI`.
- Primitive/component sections render from catalog metadata, even if their first item set is deferred.
- Approved primitive/component items appear in their sections only after explicit catalog approval.
- Catalog IDs are unique.
- Catalog items that use the generic property panel declare `PropertyDefinitions`.
- Invalid catalog IDs are rejected.
- Invalid child placement is rejected.
- `Hero01BlockMapper` round-trips node data.
- `boring_hero` migration maps to the `aero.hero.basic` catalog ID, initially inside a `NeoCompositionBlock` if `BasicHeroBlock` does not exist yet.
- Early migration can output `NeoCompositionBlock` without referencing typed blocks that do not exist yet.
- Migrated draft/editor state rebuilds `PageEditorState.BlockIdMap`.
- Migrated published pages regenerate `PageDocument.LayoutRegions` through `IPageLayoutManifestBuilder`.
- `LayoutRegions`-only pages get an empty `PageEditorState` during the PageDocument migration and are optionally reconstructed by the later Neo legacy-content migration.
- PageEditor canvas reorders nodes through the Sortable-backed path.
- Palette drag creates a new node and does not remove the palette item.
- Public `Hero01BlockRenderer` renders static HTML with the configured text and contains no NeoUI component tags.
- Public `NeoCompositionBlockRenderer` renders only catalog-approved static SSR renderer components.
- Public `NeoCompositionBlockRenderer` rejects unknown catalog IDs and renderer entries marked `RequiresInteractiveIsland`.
- Public `NeoCompositionBlockRenderer` recursively renders approved children and stops at `BlockRenderContext.MaxNestingDepth`.
- Save validation rejects Neo composition trees that exceed `BlockRenderContext.MaxNestingDepth`.
- Marten can serialize and deserialize the selected property bag representation for `NeoCompositionBlock`.
- `NeoCompositionBlock`, `Hero01Block`, and `BasicHeroBlock` appear in `GeneratedBlockModelManifest`.
- Generated `BlockBase.Polymorphic.g.cs` contains `JsonDerivedType` entries for every new `BlockBase` subtype.
- `BlockMartenConfiguration` maps every generated Neo block subtype into Marten's `BlockBase` subclass hierarchy.
- Block metadata categories normalize into valid `NeoEditorCatalogSection` values and fail on unknown categories.
- Static SSR page rendering still flows through the existing output-cache policy.
- Neo Scriban renders with full permissions in V1; a dedicated security hardening test suite must be added before the hardening follow-up ships.

Add Playwright coverage for:

- right sidebar opens and collapses with current visual behavior
- `Aero UI` section appears first
- dragging `Hero 01` onto the canvas creates a preview
- selecting the hero shows the property panel
- editing hero text updates the canvas preview
- saving and reopening preserves the node

### Implementation Order for the Agent

1. Complete the static SSR public page host path and prove it still uses the existing output-cache policy.
2. Add NuGet package references and NeoUI service/asset setup for manager/PageEditor only.
3. Extend `BlockRenderContext` with `NestingDepth` and `MaxNestingDepth` if the live code does not already include them.
4. Create catalog infrastructure, node model, `NeoCompositionBlock`, recursive `NeoCompositionBlockRenderer`, validation, and property schema types.
5. Confirm the source-generated block path discovers every new `BlockBase` subtype and feeds `GeneratedBlockModelManifest`, `BlockBase.Polymorphic.g.cs`, and `BlockMartenConfiguration`.
6. Add one-time migration support for Boring Hero, Columns, Scriban, Image, Video, Audio, Gallery, Raw HTML, and Separator before removing old editability. Because typed Aero UI blocks are not all implemented yet, this migration outputs `NeoCompositionBlock` payloads unless a target typed block already exists.
7. Implement Neo Scriban with full permissions. The security hardening deferral note in the Scriban Block Port section documents the V1 posture and required hardening checklist. No feature flag is needed in V1.
8. Replace right-sidebar hardcoded block lists with catalog-driven `Aero UI`, `Primitives`, and `Components` sections while preserving existing `pe-*` styling.
9. Add Sortable-backed palette/canvas behavior.
10. Add `Hero 01` Aero UI block, editor preview, property editor, mapper, and plain-HTML public renderer.
11. Port Basic Hero as `aero.hero.basic`.
12. Connect save/publish mapping to the existing page pipeline using `BlockBase` as the single persisted page content contract and `LayoutRegions` as the published placement manifest.
13. Add tests.
14. Only after this works, expand the catalog with more Aero UI blocks and NeoUI primitives/components.

### Socratic Checkpoints Before Expanding

Ask before making these decisions:

1. Should palette-to-canvas Sortable behavior copy from source, or should the palette use Sortable styling while custom code creates the node on drop?
2. After V1, should `aero.hero.basic` become a preset of `aero.hero.01`, or remain a separate block permanently?
3. Which additional Aero UI blocks should be approved after Hero 01 and Basic Hero?
4. When reusable custom block patterns are added later, should they be site-local, theme-local, or host-level assets?
5. If the public composition renderer cannot prove static SSR safety for primitives/components, should the next expansion stay typed-block-only until Alpine/HTMX behavior is designed?

## Handoff Advisory Notes for the Implementing Agent

These notes accompany the document but do not require modifying the architecture. They capture implementation-phase context from architectural review.

### Advisory 1 — N+1 Query Mitigation in BlockPlacementRenderer

The current rendering pipeline loads `BlockBase` documents one at a time through per-block `session.LoadAsync<BlockBase>(blockId)` (see BlockPlacementRenderer). A page with N blocks produces N+1 database round-trips (1 for PageDocument + N for blocks).

**Recommendation:** Use `session.LoadManyAsync<BlockBase>(blockIds)` instead. Marten batches `LoadManyAsync` into a single SQL `WHERE id = ANY(...)` query, reducing N+1 round-trips to 2 regardless of N. Apply this in `BlockPlacementRenderer` or its equivalent in the new pipeline. This is a V1 performance optimization, not a correctness issue, but should be in the first implementation slice to avoid a retroactive fix.

### Advisory 2 — JsonObject vs Dictionary<string, JsonElement> for NeoCompositionBlock

The editor node model defines `NeoPageNode.Properties` as `JsonObject` (from `System.Text.Json.Nodes`). `JsonObject` requires Marten's configured `JsonSerializer` to include `JsonNodeConverterFactory`, which is not guaranteed across Marten versions or custom serializer configurations.

**Recommendation:** Start the first implementation slice with `Dictionary<string, JsonElement>` instead of `JsonObject` for `NeoPageNode.Properties`. `JsonElement` is the native Marten/STJ serialization unit and is supported across all versions. The fallback is already documented in the Marten serialization warning at the `NeoCompositionBlock` section. Only switch to `JsonObject` after a dedicated serialization round-trip test confirms that Marten handles it cleanly in the current project configuration.

### Advisory 3 — Migration Idempotency

The one-time migration (Phase 2 / Addendum Step 6) processes pages with old block discriminators. If the migration is interrupted partway, or if pages are migrated and then new legacy content later arrives (e.g., from a reverted deployment), the migration must not double-process or corrupt already-migrated pages.

**Recommendation:** Check `BlockSchemaVersion` before applying migration transforms:
1. Read the page document.
2. If `page.BlockSchemaVersion >= CurrentSchemaVersion`, skip.
3. If below, apply transforms and set `page.BlockSchemaVersion = CurrentSchemaVersion`.
4. Persist through Marten's transactional session.

Wrap the migration entry point in a Wolverine handler that runs within a Marten transaction. This prevents partial migration on failure and enables safe retries.
