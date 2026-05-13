# Aero CMS — Blocks, Renderers & NeoUI: Design Review

> **Review date:** 2026-05-13
> **Method:** Multi-model council consensus (architect, senior engineer, migration specialist)
> **Purpose:** Compare `docs/aero-blocks-renderers-neoui.md` against the current codebase reality and assess architectural soundness, risks, and practicality.

---

## Executive Summary

The design document has the right direction but three critical flaws in execution:

1. **IBlockVisitor conflicts with the existing ICmsBlockRenderAdapter pipeline** — introducing a parallel dispatch mechanism would create duality without benefit
2. **ViewComponents for public rendering are a rewind** — the existing Blazor SSR path produces identical output at lower migration cost
3. **No migration strategy** — deleting block types breaks persisted content; the EditorBlock flat-property bridge is unaddressed

**Verdict:** Sound philosophy, wrong specifics. The document needs revision before implementation.

---

## 1. What to Keep From the Document

| Element | Rationale |
|---------|-----------|
| **Per-block editor components** (`{Name}BlockEditor.razor` + `{Name}BlockEditorPreview.razor`) | The 2021-line monolithic `PageEditor.razor` is the primary pain point. Decomposing into per-block components is the highest-value refactor. |
| **Dual render paths** (editor canvas ≠ public site) | Correct separation. The editor can use NeoUI; the public site gets lightweight HTML+Tailwind. |
| **Semantic-only block models** | Stripping `UseParallax`, `OverlayOpacity`, `TextAlignment`, `FullScreen`, `Height` from `HeroBlock` and moving them to the renderer is correct. |
| **Source-generator registration** | Already built (`BlockRendererGenerator.cs` scans `[BlockMetadata]`) and working. Extend it, don't replace it. |
| **`[BlockMetadata]` taxonomy and palette categories** | Well-thought-out grouping. The palette should be source-generated from these attributes. |
| **CSS-variable theming** (oklch, `[data-theme="dark"]`, logical properties) | Aligns with modern CSS and Tailwind best practices. The `SiteDocument.ThemeCssPath` mechanism is correct. |
| **RTL via CSS logical properties** | Zero-cost RTL support. No branching, no block changes. |

---

## 2. What to Change

### 2.1 Discard IBlockVisitor — Extend ICmsBlockRenderAdapter Instead

**Document says:** Generate `IBlockVisitor` with one `Visit(HeroBlock)`, `Visit(FeatureGridBlock)`, etc. overload per block type.

**Council recommends:** Extend the existing `ICmsBlockRenderAdapter` with a typed variant:

```csharp
// Already exists — keep and extend
public interface ICmsBlockRenderAdapter
{
    string BlockType { get; }
    Type ModelType { get; }
    RenderFragment Render(IBlock block, BlockRenderContext context);
}

// Add typed variant for compile-time safety
public interface ICmsBlockRenderAdapter<T> : ICmsBlockRenderAdapter where T : BlockBase
{
    RenderFragment Render(T block, BlockRenderContext context);
}
```

**Why:**

| Concern | IBlockVisitor | ICmsBlockRenderAdapter |
|---------|---------------|----------------------|
| Async support | `IHtmlContent` is synchronous only. Adding `Task<IHtmlContent>` breaks the interface. | `RenderFragment` is async-compatible (captures `Task` yielding via Blazor's `__builder`). |
| Blazor integration | Returns `IHtmlContent` — no integration with Blazor rendering model. | Returns `RenderFragment` — plugs directly into Blazor SSR and interactive modes. |
| Error handling | Requires wrapper. | `BlockRenderer.razor` wraps every adapter in `<ErrorBoundary>`. |
| Cross-cutting concerns | Needs a separate context parameter. | `BlockRenderContext` already carries navigation, preview mode, HTMX flags, depth limits. |
| Existing codebase | Zero consumers of per-type visitor overloads. | 24+ `[CmsBlockRenderer]` markers, full source-gen pipeline, working registry. |

The source generator already emits per-block adapter classes (`HeroBlockRenderAdapter : ICmsBlockRenderAdapter`). Making it implement `ICmsBlockRenderAdapter<HeroBlock>` is a small, additive change.

**Action:** Remove `Accept(IBlockVisitor visitor)` from `BlockBase`. It's dead code that calls `visitor.Visit(this)` dispatching to a single `Visit(BlockBase)` overload, which does runtime type-checks anyway — exactly what the adapter already does, but without Blazor integration.

### 2.2 Discard ViewComponents — Keep Blazor SSR for Public Rendering

**Document says:** Each block gets a `{Name}BlockViewComponent : ViewComponent` with `Default.cshtml` at `/Views/Shared/Components/{Name}Block/`. Invoked via `@await Component.InvokeAsync(block.BlockType, block)`.

**Council recommends:** Keep the existing Blazor SSR pipeline:
```
Page.cshtml → LayoutRegionRenderer.razor → BlockRenderer.razor → ICmsBlockRenderAdapter → block-specific razor
```

**Why:**

| Concern | ViewComponents | Blazor SSR |
|---------|---------------|------------|
| HTML output | Plain HTML + Tailwind | Identical — Blazor SSR renders to HTML with zero JS |
| SSR capability | Yes | Yes — `<component type="..." render-mode="Static" />` |
| State/context | `ViewData`, `ViewBag` | `BlockRenderContext`, cascading parameters |
| Error boundaries | Must wrap manually | `<ErrorBoundary>` built into `BlockRenderer.razor` |
| HTMX support | Requires middleware | `BlockRenderContext.IsHtmxRequest` already present |
| Migration cost | Build every renderer from scratch | Zero — pipeline is already in production |
| Override mechanism | RCL shadowing (works) | RCL shadowing also works for Razor components |
| Per-theme views | `IViewLocationExpander` (works) | `IComponentRenderMode` or custom cascading override |

The output is functionally equivalent in both paths. Introducing ViewComponents requires rebuilding ~25 renderers for zero end-user benefit.

**If ViewComponents are needed later** (e.g., for a consuming app that wants plain `.cshtml` without Blazor), write a `ViewComponentBlockRenderAdapter` that wraps `ICmsBlockRenderAdapter`. This can be done as a non-breaking addition.

### 2.3 Remove `Accept(IBlockVisitor)` from BlockBase

The `Accept` method exists on every block but has no concrete consumers. The single `IBlockVisitor.Visit(BlockBase)` method does runtime dispatch anyway — it provides no type-safety benefit. Removing it removes a design artifact that would mislead future developers.

If a generic visitor-like pattern is needed later for traversal (e.g., "find all images in this page"), implement it as a standalone service, not a method on every block.

---

## 3. What to Add

### 3.1 Migration Strategy (Critical — Missing Entirely)

The document describes a target architecture but is silent on how to reach it from the current codebase. This is the most dangerous gap.

#### Discriminator Aliasing

Deleting block types breaks deserialization of stored Marten documents. When the JSON discriminator `"boring_hero"` has no registered `[JsonDerivedType]`, `System.Text.Json` throws.

Current block types and their proposed fate:

| Current Class | Discriminator | Fate | Replacement |
|---|---|---|---|
| `BoringHeroBlock` | `"boring_hero"` | Deleted | `HeroBlock` (Layout.Centered) |
| `AeroHeroBlock` | `"aero_hero"` | Deleted | `HeroBlock` (Layout.SideImage etc.) |
| `HeroBlock` | `"hero"` | Kept, simplified | Strip presentation props |
| `AeroFeaturesBlock` | `"aero_features"` | Deleted | `FeatureGridBlock` |
| `AeroCtaBlock` | `"aero_cta"` | Deleted | `CallToActionBlock` |
| `AeroTestimonialsBlock` | `"aero_testimonials"` | Deleted | `TestimonialsBlock` |
| `AeroPricingBlock` | `"aero_pricing"` | Deleted | `PricingBlock` |
| (SectionBlock — new) | — | New | N/A |
| (ButtonGroupBlock — new) | — | New | N/A |
| (StatsBlock — new) | — | New | N/A |
| (BlogGridBlock — new) | — | New | N/A |

**Strategy:** Register a custom `JsonConverter<BlockBase>` that maps old discriminator values to new types:

```csharp
public class BlockDiscriminatorConverter : JsonConverter<BlockBase>
{
    private static readonly Dictionary<string, string> Alias = new()
    {
        ["boring_hero"] = "hero",
        ["aero_hero"] = "hero",
        ["aero_features"] = "feature_grid",
        ["aero_cta"] = "cta",       // maps to CtaBlock or new CallToActionBlock
        ["aero_testimonials"] = "testimonials",
        ["aero_pricing"] = "pricing",
    };
    // ... read: check Alias, then delegate to STJ's polymorphic deserializer
    // ... write: always write the canonical discriminator
}
```

Register in Marten:
```csharp
services.Configure<JsonOptions>(o =>
    o.SerializerOptions.Converters.Add(new BlockDiscriminatorConverter()));
```

**Deprecation lifecycle:**
1. Phase 1: Add converter + new types. Old types remain as deprecated wrappers.
2. Phase 2: On page save, rewrite old blocks to new types (lazy migration).
3. Phase 3: After N release cycle, remove old type source code (converter handles remaining data).

#### EditorBlock Flat-Property Bridge

The current `EditorBlock` class has ~30 flat properties covering all block types (`MainText`, `SubText`, `CtaText`, `CtaUrl`, `Src`, `Url`, `GalleryImages`, `FeatureItems`, etc.). Content stored in this structure needs transformation.

**Strategy:** A `PageUpgradeService` that runs on page load (in the grain) and lazily upgrades old pages:

```
Old page load → check schema version → apply transforms:
  1. EditorBlock with old "boring_hero" type → map to unified HeroBlock
  2. EditorBlock with old "aero_features" type → map to FeatureGridBlock
  3. EditorBlock.BlogPosts (flat list) → wrap in BlogGridBlock
  etc.
```

The `PageDocument` gets a `SchemaVersion` integer to gate upgrades.

### 3.2 MaxNestingDepth for Container Blocks

`SectionBlock.Children` → `List<BlockBase>` → can contain another `SectionBlock`. Infinite nesting is possible.

**Add to `BlockRenderContext`:**
```csharp
public sealed record BlockRenderContext
{
    // ... existing fields ...
    public int NestingDepth { get; init; } = 0;
    public const int MaxNestingDepth = 10;
}
```

**Enforce in the rendering pipeline:**
```csharp
if (context.NestingDepth >= BlockRenderContext.MaxNestingDepth)
    return builder => builder.AddMarkupContent(0, "<!-- max depth exceeded -->");
```

### 3.3 BlockAction Unification

Currently three overlapping constructs:

| Location | Type | Fields |
|---|---|---|
| `AeroHeroBlock` | `AeroButton` | Text, Url, AeroButtonStyle (Primary/Secondary/Outline) |
| `HeroBlock` (`MediaBlocks.cs`) | Inline | CtaText, CtaUrl separately |
| `CtaBlock` (`ConcreteBlocks.cs`) | Inline | Text, Url, Style |

**Target:** All use `BlockAction`:
```csharp
public sealed record BlockAction
{
    public string Label { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public BlockActionRole Role { get; init; } = BlockActionRole.Primary;
    public bool OpenInNewTab { get; init; }
}
```

### 3.4 Async Rendering Support

If the `Accept`/Visitor path is retained (recommendation: don't retain), it needs async support:
```csharp
ValueTask<IHtmlContent> AcceptAsync(IBlockVisitor visitor, CancellationToken ct);
```

Many blocks (`BlogGridBlock`, `FormBlock`, `NavigationBlock`, `ContentLinkBlock`, `ResourcePagesReferenceBlock`) need async data fetching. Synchronous `IHtmlContent` forces either blocking calls or complexity that defeats the pattern.

With the adapter pattern, this is already handled — `RenderFragment` naturally supports async via Blazor's render tree.

### 3.5 Concrete Block Merge/Delete List

Document the exact plan for every existing block type:

| Current | Action | Notes |
|---------|--------|-------|
| `RichTextBlock` | Keep | Already matches spec |
| `HeadingBlock` | Keep | Already matches spec |
| `ImageBlock` | Keep | Already matches spec |
| `QuoteBlock` | Keep | Already matches spec |
| `EmbedBlock` | Keep | Already matches spec |
| `NavigationBlock` | Keep | Already matches spec |
| `MarkdownBlock` | Keep | Already matches spec |
| `DynamicTemplateBlock` | Keep | Already matches spec |
| `RawHtmlBlock` | Keep | Already matches spec |
| `ColumnsBlock` | Keep | Add `ColumnDefinition.Span` if missing |
| `CarouselBlock` | Keep | Already matches spec |
| `ContentLinkBlock` | Keep | Already matches spec |
| `ContentEmbedBlock` | Keep | Already matches spec |
| `VideoBlock` / YouTube/Vimeo etc. | Consolidate to `VideoBlock` | Single block with provider enum |
| `FormBlock` | Keep | Already matches spec |
| `CtaBlock` | Rename to `CallToActionBlock` | Add `BlockAction` list support |
| `BoringHeroBlock` | Delete → `HeroBlock` | Via discriminator alias |
| `HeroBlock` | Keep, **strip presentation props** | Remove `UseParallax`, `OverlayOpacity`, `TextAlignment`, `FullScreen`, `Height` |
| `AeroHeroBlock` | Delete → `HeroBlock` | Via discriminator alias |
| `AeroFeaturesBlock` | Delete → `FeatureGridBlock` | Via discriminator alias |
| `AeroCtaBlock` | Delete → `CallToActionBlock` | Via discriminator alias |
| `AeroBlogBlock` | Delete → `BlogGridBlock` | Via discriminator alias |
| `AeroPricingBlock` | Delete → `PricingBlock` | Via discriminator alias |
| `AeroTeamsBlock` | Rename → `TestimonialsBlock` | Or keep as `TeamBlock` (distinct from testimonials) |
| `AeroTestimonialsBlock` | Rename → `TestimonialsBlock` | Merge with Teams if same shape |
| `AeroFaqBlock` | Keep | Could also be standalone |
| `AeroPortfolioBlock` | Keep or convert to generic `GalleryBlock` | Depends on product need |
| `AeroContactBlock` | Keep | Could also be `FormBlock` reference |
| `AeroTableBlock` | Keep | Or `RichTextBlock` with HTML table |
| `AeroAuthBlock` | Keep | Special-purpose block |
| `SectionBlock` | **New** | Container block |
| `ButtonGroupBlock` | **New** | Composes with `BlockAction` |
| `StatsBlock` | **New** | Marketing block |
| `TestimonialsBlock` | **New** (see above) | Already covered by existing types |
| `PricingBlock` | **New** (see above) | Already covered by existing types |
| `BlogGridBlock` | **New** (see above) | Already covered by existing types |
| `FeatureGridBlock` | **New** (see above) | Already covered by existing types |
| `CallToActionBlock` | **New** (see above) | Already covered by existing types |

---

## 4. Revised Architecture

### 4.1 Rendering Pipeline (Recommended)

```
         ┌──────────────────────────────────────┐
         │           PageDocument               │
         │  LayoutRegions → Blocks (EditorBlock)│
         └──────────┬──────────────────────────-┘
                    │
         ┌──────────▼──────────────────────────┐
         │     LayoutRegionRenderer.razor      │  ← Blazor SSR
         │     (iterates regions → columns)     │
         └──────────┬──────────────────────────┘
                    │
         ┌──────────▼──────────────────────────┐
         │     BlockRenderer.razor              │  ← wraps in ErrorBoundary
         │     CmsBlockRenderRegistry.TryGet()  │  ← source-generated
         └──────────┬──────────────────────────┘
                    │
         ┌──────────▼──────────────────────────┐
         │  ICmsBlockRenderAdapter<T>.Render()  │  ← source-generated per block
         │  → block-specific .razor component   │  ← plain HTML + Tailwind
         └──────────────────────────────────────┘
```

### 4.2 Editor Architecture (Recommended)

```
         PageEditor.razor (orchestrator, ~300 lines)
         ├── Block palette (source-generated from [BlockMetadata])
         ├── Block canvas (iterates blocks, shows per-block preview)
         │    └── {Name}BlockEditorPreview.razor   ← simplified, shows content
         └── Property panel (sidebar)
              └── {Name}BlockEditor.razor           ← NeoUI form fields
```

Each `{Name}BlockEditorPreview.razor` and `{Name}BlockEditor.razor` is a standalone Blazor component. The `PageEditor.razor` becomes an orchestrator — it no longer contains rendering logic for each block type.

### 4.3 Source Generator Output

The `BlockRendererGenerator.cs` should produce:

1. `ICmsBlockRenderAdapter<T>` implementations (already does — add typed interface)
2. `CmsBlockRenderRegistry` (already does)
3. `GeneratedBlockFactory` (already does)
4. `BlockBase.Polymorphic.g.cs` — `[JsonPolymorphic]` + `[JsonDerivedType]` attributes (already does)
5. **Editor palette metadata** (`IBlockMetadata` with display name, category, icon) — extend the generator to emit this
6. **ViewComponent adapters** (optional, future) — wrapper that converts adapter to `InvokeAsync` compatible signature

---

## 5. Risk Summary

| Risk | Severity | Mitigation |
|------|----------|------------|
| **Visitor/adapter duality confusion** | High | Discard Visitor, extend typed adapter |
| **Deleted block types break deserialization** | **Critical** | Add `BlockDiscriminatorConverter` before deployment |
| **EditorBlock flat-property bridge migration** | High | `PageUpgradeService` with schema versioning |
| **Async rendering not supported by `IHtmlContent`** | Medium | Use adapter's `RenderFragment` (already async-aware) |
| **Recursive container rendering** (stack overflow) | Low | `MaxNestingDepth` in `BlockRenderContext` |
| **Three HeroBlock variants** | High | Unify via discriminator alias plan |
| **`BlockAction` fragmentation** (AeroButton, CtaText, etc.) | Medium | Replace all with shared `BlockAction` type |
| **Migration cost from monolithic editor** | Medium | Phased: 3 blocks at a time, keep switch fallback |

---

## 6. Summary: Document vs. Reality

| Aspect | Document Says | Codebase Has | Recommended |
|--------|--------------|--------------|-------------|
| Block dispatch | `IBlockVisitor` per-type | `ICmsBlockRenderAdapter` | **Keep adapter, add typed variant** |
| Public rendering | ViewComponents | Blazor SSR → adapter pipeline | **Keep Blazor SSR** |
| `Accept()` on BlockBase | Core to design | Dead code, single `Visit(BlockBase)` | **Remove** |
| Editor structure | Per-block components | Monolithic 2021-line switch | **Decompose** — this is the right target |
| Block models | Semantic only, no presentation props | Mix of clean + dirty models | **Clean up per merge/delete list** |
| Themability | oklch, data-theme, logical props | Not yet implemented | **Full steam ahead** |
| RTL support | CSS logical properties | Not yet implemented | **Full steam ahead** |
| Source gen output | Visitor + DI + palette | Adapters + manifest + JSON | **Add typed adapter + palette metadata** |
| Migration strategy | None | N/A | **Must add before implementation** |
