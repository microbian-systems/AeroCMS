# Aero CMS — Blocks, Renderers & NeoUI

## Purpose of this document

This document defines the block system architecture for Aero CMS, including block types, container blocks, the rendering pipeline, ViewComponent conventions, NeoUI integration, theming, and RTL support. It is intended as a reference for AI agents and developers implementing or extending the block system.

---

## 1. Core Concepts

### What is a Block?

A block is the fundamental unit of page composition in Aero CMS. A `PageDocument` contains an ordered list of `BlockBase` instances. Blocks describe **content and semantic intent** — never presentation details.

**Blocks are NOT:**
- UI components (Button, Card, Dialog)
- CSS class containers
- Layout wrappers with hardcoded styles

**Blocks ARE:**
- Typed data models with content fields
- Semantic intent signals (e.g. `HeroLayout.SideImage`)
- Versioned via the `PageDocument` event stream
- Rendered by a paired ViewComponent on the public site

### The Single Responsibility Split

Every block has two completely separate concerns:

```
BlockBase (data model)          ViewComponent (renderer)
──────────────────────────────────────────────────────
What the content IS             How the content LOOKS
Stored in Marten                Lives in /Views/Shared/Components
Versioned with the page         Swappable per theme/context
No UI framework knowledge       Full Tailwind/HTML control
```

Presentation properties (opacity, parallax, text-alignment, button variants) belong in the **renderer or CSS**, never on the block data model.

---

## 2. Block Base Class

```csharp
public abstract class BlockBase
{
    public string BlockType { get; }
    public abstract IHtmlContent Accept(IBlockVisitor visitor);
}
```

All blocks inherit from `BlockBase`. The `Accept` method implements the Visitor pattern for the rendering pipeline.

### BlockMetadata Attribute

```csharp
[BlockMetadata("feature_grid", "Feature Grid", Category = "Marketing")]
public sealed class FeatureGridBlock : BlockBase { ... }
```

The attribute provides:
- `blockType` — unique string identifier, used for ViewComponent lookup
- `displayName` — shown in the editor palette
- `Category` — groups blocks in the editor palette

---

## 3. Block Taxonomy

### 3.1 Container Blocks

Container blocks hold other blocks. They are the composition surface for building custom layouts.

#### `SectionBlock`

Full-width wrapper. Controls background, spacing, and direction. Can contain any block including other container blocks.

```csharp
[BlockMetadata("section", "Section", Category = "Layout")]
public sealed class SectionBlock : BlockBase
{
    public override string BlockType => "section";

    public string? BackgroundColor { get; set; }
    public long? BackgroundImageMediaId { get; set; }
    public SectionPadding Padding { get; set; } = SectionPadding.Medium;
    public List<BlockBase> Children { get; set; } = [];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public enum SectionPadding { None, Small, Medium, Large }
```

#### `ColumnsBlock`

Divides a row into up to 12 columns. Each column contains its own list of child blocks. This is the primary flexible layout tool — stacking multiple `ColumnsBlock`s inside a `SectionBlock` produces arbitrary row/column grid layouts.

```csharp
[BlockMetadata("columns", "Columns", Category = "Layout")]
public sealed class ColumnsBlock : BlockBase
{
    public override string BlockType => "columns";

    public List<ColumnDefinition> Columns { get; set; } = [];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public sealed class ColumnDefinition
{
    public int Span { get; set; } = 6; // out of 12
    public List<BlockBase> Children { get; set; } = [];
}
```

**Why no `GridBlock`?**

A `GridBlock` (rows + columns) is not needed. Multi-row grids are composed by stacking multiple `ColumnsBlock` instances inside a `SectionBlock`. Named blocks (`FeatureGridBlock`, `TestimonialsBlock`) handle same-type repeating grid content. A generic `GridBlock` would duplicate this without adding value.

### 3.2 Leaf Blocks (Content)

Leaf blocks cannot contain other blocks.

#### `HeroBlock`

```csharp
[BlockMetadata("hero", "Hero", Category = "Marketing")]
public sealed class HeroBlock : BlockBase
{
    public override string BlockType => "hero";

    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public long? BackgroundImageMediaId { get; set; }
    public string? AltText { get; set; }
    public List<BlockAction> Actions { get; set; } = [];
    public HeroLayout Layout { get; set; } = HeroLayout.Centered;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public enum HeroLayout
{
    Centered,        // content centered, optional background image
    SideImage,       // image right, content left
    SideImageFlip,   // image left, content right
    BackgroundImage  // full bleed background image
}
```

**Note:** `BoringHeroBlock` is deleted — it is `HeroBlock` with `Layout.Centered` and no image. `AeroHeroBlock` is deleted — its `SideImage`/`SideImageFlip` layouts are folded into `HeroLayout`.

#### `FeatureGridBlock`

```csharp
[BlockMetadata("feature_grid", "Feature Grid", Category = "Marketing")]
public sealed class FeatureGridBlock : BlockBase
{
    public override string BlockType => "feature_grid";

    public string? Heading { get; set; }
    public string? Subheading { get; set; }
    public List<FeatureItem> Features { get; set; } = [];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}

public sealed record FeatureItem
{
    public string Icon { get; init; } = string.Empty; // Lucide icon name
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
```

#### `ButtonGroupBlock`

Standalone button(s). Used inside `ColumnsBlock` or as a leaf block on a page. A `<Button>` is never a block on its own — always `ButtonGroupBlock`.

```csharp
[BlockMetadata("button_group", "Button Group", Category = "Content")]
public sealed class ButtonGroupBlock : BlockBase
{
    public override string BlockType => "button_group";

    public List<BlockAction> Buttons { get; set; } = [];
    public BlockAlignment Alignment { get; set; } = BlockAlignment.Left;

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
```

#### Shared Value Types

```csharp
public sealed record BlockAction
{
    public string Label { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public BlockActionRole Role { get; init; } = BlockActionRole.Primary;
    public bool OpenInNewTab { get; init; }
}

public enum BlockActionRole { Primary, Secondary, Ghost }
public enum BlockAlignment { Left, Center, Right }
```

`BlockActionRole` is **semantic**, not a CSS class. `Primary` means "most important action." The renderer maps it to whatever CSS/component variant is appropriate. This means the same block data renders correctly in a Tailwind ViewComponent, an email template, or a plain HTML export.

### 3.3 Additional Leaf Blocks

The following blocks follow the same pattern — content-only data models, no presentation properties:

| Block | Category | Key Fields |
|---|---|---|
| `RichTextBlock` | Content | `Html: string` |
| `HeadingBlock` | Content | `Text`, `Level` (H1–H6) |
| `ImageBlock` | Media | `MediaId`, `AltText`, `Caption` |
| `VideoBlock` | Media | `MediaId`, `EmbedUrl`, `Autoplay` |
| `CarouselBlock` | Media | `List<CarouselItem>` |
| `QuoteBlock` | Content | `Text`, `Author`, `Source` |
| `ContentLinkBlock` | Navigation | `TargetPageId`, `LinkText` |
| `TestimonialsBlock` | Marketing | `List<Testimonial>` |
| `PricingBlock` | Marketing | `List<PricingTier>` |
| `StatsBlock` | Marketing | `List<StatItem>` |
| `CallToActionBlock` | Marketing | `Heading`, `Subheading`, `List<BlockAction>` |
| `BlogGridBlock` | Content | `PostIds`, `MaxCount`, `Layout` |
| `FormBlock` | Interactive | `FormId` (references FormDocument) |

---

## 4. The Visitor Pattern

The block rendering pipeline uses Visitor to dispatch from a `BlockBase` to its renderer without `if/switch` type checking.

```csharp
public interface IBlockVisitor
{
    IHtmlContent Visit(HeroBlock block);
    IHtmlContent Visit(FeatureGridBlock block);
    IHtmlContent Visit(ButtonGroupBlock block);
    IHtmlContent Visit(ColumnsBlock block);
    IHtmlContent Visit(SectionBlock block);
    IHtmlContent Visit(CarouselBlock block);
    IHtmlContent Visit(RichTextBlock block);
    // ... one Visit overload per block type
}
```

Each `BlockBase.Accept(IBlockVisitor visitor)` calls `visitor.Visit(this)` — the compiler resolves the correct overload at compile time.

---

## 5. Rendering Pipeline

### 5.1 Dual Render Paths

Every block has two render paths:

```
Public Site (.cshtml)              Editor Canvas (Blazor)
──────────────────────────────────────────────────────────
ViewComponent                      BlockEditorPreview component
Plain HTML + Tailwind classes      NeoUI Blazor components allowed
SSR, no Blazor overhead            Full interactivity
SEO-optimised output               Simplified preview, not pixel-perfect
Fast, no JS required               Can use animations, transitions
```

The public renderer and editor preview are **completely independent**. Changing one never affects the other.

### 5.2 Public Rendering — ViewComponents

Each block type maps 1:1 to a ViewComponent. The convention:

```
Block Type:      "feature_grid"
ViewComponent:   FeatureGridBlockViewComponent
View:            /Views/Shared/Components/FeatureGridBlock/Default.cshtml
```

#### ViewComponent implementation

```csharp
public sealed class FeatureGridBlockViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(FeatureGridBlock block)
        => View(block);
}
```

The ViewComponent is intentionally thin. All rendering logic lives in `Default.cshtml`.

#### ViewComponent view — `Default.cshtml`

NeoUI Blazor components (`<Card>`, `<Button>`, etc.) **cannot be used in `.cshtml` files**. Instead, inline the equivalent HTML that NeoUI would emit. NeoUI components compile to plain HTML + Tailwind classes — the visual output is identical.

```cshtml
@* /Views/Shared/Components/FeatureGridBlock/Default.cshtml *@
@model FeatureGridBlock

<section class="container w-full bg-background px-6 py-16">
    <div class="max-w-5xl mx-auto">
        @if (!string.IsNullOrEmpty(Model.Heading))
        {
            <div class="text-center mb-12">
                <h2 class="text-3xl font-bold tracking-tight mb-3">@Model.Heading</h2>
                @if (!string.IsNullOrEmpty(Model.Subheading))
                {
                    <p class="text-muted-foreground text-lg max-w-xl mx-auto">
                        @Model.Subheading
                    </p>
                }
            </div>
        }
        <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
            @foreach (var feature in Model.Features)
            {
                <div class="rounded-lg border bg-card text-card-foreground shadow-sm">
                    <div class="flex flex-col space-y-1.5 p-6 pb-3">
                        <div class="size-10 rounded-lg bg-primary/10 flex items-center justify-center mb-3">
                            <i data-lucide="@feature.Icon" class="size-[18px] text-primary"></i>
                        </div>
                        <h3 class="text-base font-semibold leading-none tracking-tight">
                            @feature.Title
                        </h3>
                    </div>
                    <div class="p-6 pt-0">
                        <p class="text-sm text-muted-foreground leading-relaxed">
                            @feature.Description
                        </p>
                    </div>
                </div>
            }
        </div>
    </div>
</section>
```

#### Invoking ViewComponents from the page

```cshtml
@* Page.cshtml — renders all blocks *@
@foreach (var block in Model.Blocks)
{
    @await Component.InvokeAsync(block.BlockType, block)
}
```

The `block.BlockType` string (e.g. `"feature_grid"`) resolves to `FeatureGridBlockViewComponent` via ASP.NET Core's ViewComponent naming convention.

### 5.3 NeoUI Component → ViewComponent Conversion

NeoUI Blazor components are used as a **design reference**. When building a ViewComponent, look up the NeoUI source and inline its output HTML directly. The mapping rules:

| NeoUI Blazor | Inline HTML equivalent |
|---|---|
| `<Card Class="...">` | `<div class="rounded-lg border bg-card text-card-foreground shadow-sm ...">` |
| `<CardHeader Class="...">` | `<div class="flex flex-col space-y-1.5 p-6 ...">` |
| `<CardTitle Class="...">` | `<h3 class="font-semibold leading-none tracking-tight ...">` |
| `<CardContent>` | `<div class="p-6 pt-0">` |
| `<Button Variant="Default">` | `<a class="inline-flex items-center justify-center rounded-md bg-primary text-primary-foreground px-4 py-2 text-sm font-medium ...">` |
| `<Button Variant="Outline">` | `<a class="inline-flex ... border border-input bg-background ...">` |
| `<LucideIcon Name="x" />` | `<i data-lucide="x"></i>` |
| `@container` / `@md:` prefix | Remove `@` — becomes `container` / `md:` |

### 5.4 Lucide Icons in ViewComponents

Add the Lucide vanilla JS bundle to `_CmsLayout.cshtml`. It auto-replaces `<i data-lucide="name">` with inline SVG:

```html
<script src="https://unpkg.com/lucide@latest/dist/umd/lucide.min.js"></script>
<script>lucide.createIcons();</script>
```

Self-host the bundle in production. No other changes needed — every ViewComponent that uses `<i data-lucide="...">` gets icons automatically.

### 5.5 Editor Preview — Blazor Components

The editor canvas renders a preview of each block using a Blazor component. This is separate from the public ViewComponent. NeoUI components **can** be used here.

```razor
@* HeroBlockEditorPreview.razor *@
@inherits BlockEditorPreviewBase<HeroBlock>

<div class="aero-editor-preview aero-editor-preview--hero">
    <div class="aero-editor-preview__badge">Hero — @Block.Layout</div>

    @if (!string.IsNullOrEmpty(Block.Title))
    {
        <h2 class="aero-editor-preview__title">@Block.Title</h2>
    }

    @if (!string.IsNullOrEmpty(Block.Subtitle))
    {
        <p class="aero-editor-preview__subtitle">@Block.Subtitle</p>
    }

    @if (Block.BackgroundImageMediaId.HasValue)
    {
        <AeroMediaThumbnail MediaId="@Block.BackgroundImageMediaId.Value" />
    }

    <div class="aero-editor-preview__actions">
        @foreach (var action in Block.Actions)
        {
            <Badge Variant="@(action.Role == BlockActionRole.Primary
                ? BadgeVariant.Default
                : BadgeVariant.Outline)">
                @action.Label
            </Badge>
        }
    </div>
</div>
```

The editor preview is intentionally simplified — it shows the author what content is in the block, not a pixel-perfect rendering. Animations, parallax, and hover effects are omitted.

### 5.6 Property Panel — Blazor Form

The property panel renders form inputs for each field on the block model. It binds directly to the block instance.

```razor
@* HeroBlockEditor.razor — property panel *@
@inherits BlockEditorBase<HeroBlock>

<Field Label="Title">
    <Input @bind-Value="Block.Title" />
</Field>

<Field Label="Subtitle">
    <Input @bind-Value="Block.Subtitle" />
</Field>

<Field Label="Layout">
    <Select @bind-Value="Block.Layout">
        @foreach (var layout in Enum.GetValues<HeroLayout>())
        {
            <SelectItem Value="@layout">@layout</SelectItem>
        }
    </Select>
</Field>

<Field Label="Background Image">
    <AeroMediaPicker @bind-MediaId="Block.BackgroundImageMediaId" />
</Field>

<Field Label="Actions">
    <AeroActionListEditor @bind-Actions="Block.Actions" />
</Field>
```

Property panel components use NeoUI directly — `Field`, `Input`, `Select` are all NeoUI Blazor components.

---

## 6. View & Asset Overriding

Consuming apps that reference Aero as a NuGet package (Razor Class Library) can override any view or CSS theme without modifying Aero itself. View resolution and static asset resolution use separate mechanisms — they must not be confused.

### 6.1 View Overriding — RCL Shadowing (Zero Config)

ASP.NET Core resolves Razor views with a built-in fallback chain. Host app views always win over RCL-embedded views. No configuration required.

For block ViewComponents:

```
1. /Views/Shared/Components/HeroBlock/Default.cshtml     ← host app wins
2. /Pages/Shared/Components/HeroBlock/Default.cshtml     ← host app wins
3. RCL embedded view (Aero NuGet)                        ← Aero default fallback
```

For layouts and pages:

```
Host app /Views/Shared/_CmsLayout.cshtml   → overrides Aero's _CmsLayout
Host app /Pages/Page.cshtml                → overrides Aero's Page.cshtml
Host app /Pages/Post.cshtml                → overrides Aero's Post.cshtml
Host app /Pages/ContentEntry.cshtml        → overrides Aero's ContentEntry.cshtml
```

A consuming app overrides any block renderer by dropping a `Default.cshtml` at the correct path. The block data model, grain, editor property panel, and source generators are completely unchanged — only the public HTML output changes.

### 6.2 View Overriding — Named Themes via `IViewLocationExpander`

For advanced scenarios where a consuming app wants per-theme view resolution (e.g. `/Views/Themes/Acme/Components/HeroBlock/Default.cshtml`), implement a custom `IViewLocationExpander`:

```csharp
public sealed class AeroThemeViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
        var themeService = context.ActionContext.HttpContext
            .RequestServices.GetService<IAeroThemeService>();

        if (themeService?.CurrentTheme is not null)
            context.Values["theme"] = themeService.CurrentTheme;
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        if (!context.Values.TryGetValue("theme", out var theme))
            return viewLocations;

        var themeLocations = new[]
        {
            $"/Views/Themes/{theme}/Components/{{1}}/{{0}}.cshtml",
            $"/Views/Themes/{theme}/{{1}}/{{0}}.cshtml",
            $"/Views/Themes/{theme}/Shared/{{0}}.cshtml",
        };

        return themeLocations.Concat(viewLocations);
    }
}
```

Register in the consuming app:

```csharp
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationExpanders.Add(new AeroThemeViewLocationExpander());
});
```

Resolution chain with expander active:

```
1. /Views/Themes/acme/Components/HeroBlock/Default.cshtml   ← theme-specific
2. /Views/Shared/Components/HeroBlock/Default.cshtml         ← host app override
3. RCL embedded view (Aero NuGet)                            ← Aero default
```

**Important:** `IViewLocationExpander` is a Razor view mechanism only. It has no awareness of CSS files, static assets, or `wwwroot`. Do not attempt to use it for CSS overriding.

### 6.3 CSS Overriding — Static Assets

View Location Expanders cannot override CSS. Static asset overriding uses a completely separate mechanism.

Aero ships two CSS files with distinct responsibilities:

```
aero-base.css          → structural/functional styles (block layout, editor chrome)
                         served from /_content/Aero.Cms.Shared/css/aero-base.css
                         never overridden — consuming apps must not modify this

aero-default-theme.css → CSS custom properties only, no actual styles
                         served from /_content/Aero.Cms.Shared/css/aero-default-theme.css
                         replaced entirely by the consuming app's theme file
```

`_CmsLayout.cshtml` loads both in order:

```cshtml
@inject ICmsPageContext PageContext

@* Aero structural — RCL, never override *@
<link rel="stylesheet" href="/_content/Aero.Cms.Shared/css/aero-base.css" />

@* Theme — consuming app provides this via SiteDocument.ThemeCssPath *@
@if (PageContext.ThemeCssPath is not null)
{
    <link rel="stylesheet" href="@PageContext.ThemeCssPath" />
}
else
{
    @* Aero default theme as fallback *@
    <link rel="stylesheet" href="/_content/Aero.Cms.Shared/css/aero-default-theme.css" />
}
```

The consuming app sets `SiteDocument.ThemeCssPath = "/css/my-theme.css"` and serves that file from its own `wwwroot`. The file contains only CSS custom property overrides — no structural styles.

### 6.4 Override Mechanism Summary

Every concern has its own override mechanism. They are separate and must not be mixed:

```
Concern                  Mechanism
───────────────────────────────────────────────────────────────────────
Block renderer (.cshtml) RCL view shadowing — drop file at correct path
Layout / page (.cshtml)  RCL view shadowing — drop file at correct path
Named theme views        IViewLocationExpander registered in host app
CSS custom properties    SiteDocument.ThemeCssPath → host app wwwroot
CSS structure/base       Not overridable — intentional
JavaScript               Override _CmsLayout.cshtml, add <script> tags
Block data models        Not overridable — extend via content types
Block editor UI          Not overridable — Aero editor chrome is fixed
```

---

## 7. NeoUI Integration

### 7.1 What NeoUI Is in the Aero Context

NeoUI (package: `NeoUI.Blazor`) is a shadcn/ui-inspired Blazor component library. In Aero CMS it plays three roles:

1. **Editor chrome** — the entire Blazor editor UI (sidebar, toolbar, dialogs, sheets) is built with NeoUI components
2. **Editor property panels** — form fields (`Input`, `Select`, `Field`, `Switch`, `Slider`) for block editing
3. **Editor canvas previews** — block preview components can use NeoUI for visual fidelity

NeoUI is **not used on the public-facing site**. Public block rendering uses plain HTML + Tailwind classes that produce identical visual output.

### 7.2 NeoUI Setup (Editor / Blazor only)

```razor
@* MainLayout.razor — editor layout *@
<AppProvider>
    @Body
    <ToastViewport />
    <DialogHost />
</AppProvider>
```

```razor
@* _Imports.razor *@
@using NeoUI.Blazor
@using NeoUI.Icons.Lucide
```

```html
<!-- App.razor head -->
<link rel="stylesheet" href="styles/theme.css" />
<link href="@Assets["_content/NeoUI.Blazor/components.css"]" rel="stylesheet" />
<script src="@Assets["_content/NeoUI.Blazor/js/theme.js"]"></script>
```

### 7.3 NeoUI Components Used in the Editor

The editor uses NeoUI components extensively. Key components:

**Layout:** `Sidebar`, `Sheet`, `Resizable`, `ScrollArea`, `Separator`
**Navigation:** `Tabs`, `Breadcrumb`, `NavigationMenu`
**Forms:** `Input`, `Textarea`, `Select`, `Combobox`, `Switch`, `Slider`, `Field`, `Label`, `Checkbox`, `RadioGroup`
**Overlay:** `Dialog`, `DialogService`, `Drawer`, `Popover`, `Toast`, `Tooltip`, `DropdownMenu`
**Data:** `DataTable`, `DataView`, `Badge`, `Avatar`, `Card`
**Feedback:** `Skeleton`, `Spinner`, `Progress`, `Alert`
**Drag/Drop:** `Sortable` (block reordering in the canvas)

### 7.4 Block Palette in the Editor

The editor palette lists block types the user can drag onto the canvas. The palette does **not** list NeoUI components directly — it lists semantic block types that are rendered using NeoUI-inspired markup.

```
Block Palette
├── Layout
│     ├── Section
│     └── Columns
├── Content
│     ├── Rich Text
│     ├── Heading
│     ├── Image
│     ├── Video
│     ├── Quote
│     └── Button Group
├── Media
│     ├── Carousel
│     └── Gallery
├── Marketing
│     ├── Hero
│     ├── Feature Grid
│     ├── Testimonials
│     ├── Pricing
│     ├── Stats
│     └── Call To Action
├── Navigation
│     ├── Nav Menu
│     └── Content Link
└── Commerce
      ├── Product Gallery
      └── Add To Cart
```

A NeoUI "Feature" component becomes the `FeatureGridBlock` entry in this palette. The user drags "Feature Grid" — not a NeoUI component.

---

## 8. Theming

### 8.1 Philosophy

Aero CMS is **opinionated and not multi-theme**. There is no built-in theme switcher with 50 themes. The design system is:

- Developer-controlled via a single CSS variables file
- Future: editor-controlled via a `ThemeDocument` that generates the CSS file

Supported styling approaches on the public site:
- **Pure modern CSS** with CSS custom properties
- **Tailwind CSS** utility classes referencing CSS variables
- **NeoUI-compatible classes** (same variable names, same output)

### 8.2 CSS Variables File

All visual tokens are defined as CSS custom properties in one file. Block ViewComponents reference these variables via Tailwind utility classes — never hardcoded values.

```css
/* /wwwroot/themes/site-theme.css */
:root {
    /* Brand */
    --color-primary: oklch(0.55 0.2 250);
    --color-primary-foreground: oklch(0.98 0 0);
    --color-secondary: oklch(0.96 0.01 250);
    --color-secondary-foreground: oklch(0.09 0 0);

    /* Surface */
    --color-background: oklch(1 0 0);
    --color-foreground: oklch(0.09 0 0);
    --color-card: oklch(1 0 0);
    --color-card-foreground: oklch(0.09 0 0);
    --color-muted: oklch(0.96 0.01 250);
    --color-muted-foreground: oklch(0.45 0.01 250);
    --color-border: oklch(0.9 0.01 250);
    --color-input: oklch(0.9 0.01 250);

    /* Feedback */
    --color-destructive: oklch(0.577 0.245 27.325);
    --color-success: oklch(0.6 0.18 145);

    /* Shape */
    --radius-sm: 0.25rem;
    --radius-md: 0.5rem;
    --radius-lg: 0.75rem;
    --radius-xl: 1rem;

    /* Typography */
    --font-sans: 'Inter', system-ui, sans-serif;
    --font-serif: 'Georgia', serif;
    --font-mono: 'JetBrains Mono', monospace;

    /* Spacing */
    --spacing-section: 5rem;
    --spacing-block: 2rem;
}

/* Dark mode */
[data-theme="dark"] {
    --color-background: oklch(0.09 0 0);
    --color-foreground: oklch(0.98 0 0);
    --color-card: oklch(0.12 0 0);
    --color-card-foreground: oklch(0.98 0 0);
    --color-muted: oklch(0.15 0 0);
    --color-muted-foreground: oklch(0.65 0 0);
    --color-border: oklch(0.2 0 0);
}
```

### 8.3 SiteDocument Theme Reference

```csharp
public sealed class SiteDocument : Entity
{
    public string? ThemeCssPath { get; set; }  // "/themes/acme-corp.css"
    public string Direction { get; set; } = "ltr";
    public string? Language { get; set; } = "en";
    public string? FontPreloadUrl { get; set; }
}
```

`_CmsLayout.cshtml` injects the theme file:

```cshtml
@inject ICmsPageContext PageContext

<html dir="@PageContext.Direction" lang="@PageContext.Language">
<head>
    @if (PageContext.ThemeCssPath is not null)
    {
        <link rel="stylesheet" href="@PageContext.ThemeCssPath" />
    }
    <link rel="stylesheet" href="/css/aero-base.css" />
</head>
```

Two CSS files:
- `aero-base.css` — Aero's structural styles, never modified
- `{site-theme}.css` — all CSS variables, fully replaced per site/client

### 8.4 Developer Theming Workflow

```
1. Copy /themes/aero-default.css → /themes/my-brand.css
2. Edit CSS variable values only
3. Set SiteDocument.ThemeCssPath = "/themes/my-brand.css"
4. Done — every block on every page reflects the new theme
```

No block changes. No ViewComponent changes. No Tailwind recompilation if only variable values change.

### 8.5 Future Editor Theme Builder

When the in-editor theme builder is implemented, it writes CSS variable values to a `ThemeDocument` event stream and regenerates the CSS file on publish. The block and rendering systems require zero changes because they already reference variables, not hardcoded values.

```csharp
// Future — same event-sourced pattern as NavMenuDocument
public sealed class ThemeDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public Dictionary<string, string> Variables { get; set; } = [];
    // Generates /themes/{siteId}.css on publish event
}
```

---

## 9. RTL Support

### 9.1 Approach

RTL support is achieved entirely through **CSS logical properties** in ViewComponent markup. No additional CSS, no block changes, no ViewComponent branching.

Activating RTL:

```html
<html dir="rtl" lang="ar">
```

This is set from `SiteDocument.Direction` in `_CmsLayout.cshtml`.

### 9.2 Logical Property Rules

All ViewComponents must use Tailwind logical property variants. Never use directional properties:

| ❌ Directional (breaks RTL) | ✅ Logical (RTL aware) |
|---|---|
| `pl-6` / `pr-6` | `ps-6` / `pe-6` |
| `ml-4` / `mr-4` | `ms-4` / `me-4` |
| `text-left` / `text-right` | `text-start` / `text-end` |
| `border-l-2` / `border-r-2` | `border-s-2` / `border-e-2` |
| `rounded-l-lg` / `rounded-r-lg` | `rounded-s-lg` / `rounded-e-lg` |
| `left-0` / `right-0` | `start-0` / `end-0` |
| `float-left` / `float-right` | `float-start` / `float-end` |

Tailwind v3.3+ supports all logical property variants. CSS logical properties flip automatically when `dir="rtl"` is set on any ancestor element.

### 9.3 RTL CSS Variables (optional)

For content that requires directional overrides beyond what logical properties cover:

```css
[dir="rtl"] {
    --text-start: right;
    --text-end: left;
    --font-sans: 'Noto Sans Arabic', system-ui, sans-serif;
}
```

---

## 10. Conventions and Rules

### Block Data Model Rules

- ✅ Content fields (title, body, image IDs, URLs)
- ✅ Semantic layout intent (`HeroLayout`, `BlockAlignment`)
- ✅ Semantic role indicators (`BlockActionRole.Primary`)
- ❌ CSS class names or Tailwind utilities
- ❌ UI library component names (NeoUI, Bootstrap, etc.)
- ❌ Presentation properties (opacity, parallax, animation names)
- ❌ Color values or font names
- ❌ Pixel measurements or spacing values

### ViewComponent Rules

- ✅ Plain HTML with Tailwind utility classes
- ✅ CSS custom property references via Tailwind (`bg-card`, `text-primary`)
- ✅ Tailwind logical properties (`ps-`, `pe-`, `ms-`, `text-start`)
- ✅ Lucide icons via `<i data-lucide="name">`
- ❌ NeoUI Blazor components (`<Card>`, `<Button>`, etc.)
- ❌ Hardcoded color values
- ❌ Directional Tailwind classes (`pl-`, `pr-`, `text-left`)
- ❌ Business logic or data fetching (that belongs in the grain)

### Naming Conventions

```
Block class:          {Name}Block               (HeroBlock, FeatureGridBlock)
ViewComponent class:  {Name}BlockViewComponent  (HeroBlockViewComponent)
View path:            /Views/Shared/Components/{Name}Block/Default.cshtml
Editor preview:       {Name}BlockEditorPreview.razor
Editor form:          {Name}BlockEditor.razor
Block type string:    snake_case                (hero, feature_grid, button_group)
```

---

## 11. Source Generator Integration

Block registration uses Aero's source generator pipeline. The `[BlockMetadata]` attribute is scanned at compile time to generate:

1. `IBlockVisitor` interface with one `Visit` overload per block type
2. DI registration extension per module assembly
3. Editor palette metadata (display name, category, icon)

No manual registration. Adding a new block class with `[BlockMetadata]` is sufficient — the generator handles the rest.

```csharp
// GENERATED — per module assembly
public static class CmsCommonBlocksExtensions
{
    public static IServiceCollection AddCmsCommonBlocks(
        this IServiceCollection services)
    {
        services.AddSingleton<IBlockMetadata, HeroBlockMetadata>();
        services.AddSingleton<IBlockMetadata, FeatureGridBlockMetadata>();
        services.AddTransient<HeroBlockViewComponent>();
        services.AddTransient<FeatureGridBlockViewComponent>();
        // ...
        return services;
    }
}
```
