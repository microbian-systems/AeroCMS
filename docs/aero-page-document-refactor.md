# Aero CMS — Page Document Architecture (v2)

> **See also:** [`aero-blocks-renderers-neoui.md`](aero-blocks-renderers-neoui.md) — Editor/renderer implementation contract that consumes this data model. The current document defines the data model (documents, events, pipelines); the NeoUI doc defines the PageEditor shell, block composition catalog, public Razor component rendering, output caching, and legacy migration implementation.

## Changelog from v1

| # | Change | Reason |
|---|---|---|
| 1 | `PageContentUpdated` renamed to `PageMetadataUpdated` | Event name implied block/body content; it carries metadata only |
| 2 | `HasUnpublishedChanges` removed from `PageDocument` | Domain aggregate should not take a dependency on `PageEditorState` |
| 3 | `IPageLayoutManifestBuilder` extracted; shared by preview and publish | Structural prevention of preview/publish drift |
| 4 | `PageEditorState` deletion policy documented | Separate document with no prior delete policy; hard-delete on page delete |
| 5 | `BlockStreamVersion` on `BlockPlacement` explicitly rejected | Nullable field approach creates permanent dual-mode render complexity; future need addressed via separate type |

---

## Design Principles

- **Editor state and render state are strictly separated documents.**
- **`LayoutRegions` is written only by the publish path.** No draft save may touch it.
- **`BlockBase` is retained as the persisted block content type.** Source generators, `[JsonPolymorphic]`, and the full type hierarchy are preserved.
- **Block placement is separated from block content.** `EditorBlockPlacement` references blocks by ID; the publish pipeline resolves them.
- **Preview is a first-class operation** with its own explicit pipeline that never writes to `PageDocument`.
- **Preview and publish share one layout builder.** `IPageLayoutManifestBuilder` is the single implementation; both callers use it.
- **Live reference model.** `BlockPlacement` stores `BlockId` + `BlockType` only. Block data is always loaded from the current `BlockBase` document at render time.
- **`PageEditorState` is hard-deleted when its page is deleted.** It is editor scratch state with no audit requirement.

---

## Document Map

```
PageDocument          — published/lifecycle/render aggregate (public renderer reads this)
PageEditorState       — draft/editor workspace (editor reads/writes this only)
BlockBase             — persisted block content document (unchanged type hierarchy)
```

---

## Core Documents

### `PageDocument`

The public render aggregate. Written only by publish/lifecycle events. The public
renderer and any caching layer reads exclusively from this document.

`HasUnpublishedChanges` has been removed. Version comparison belongs in the admin
service/read-model layer, not on the render aggregate. See `PageAdminStatusService`.

```csharp
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Models;
using Aero.Core.Entities;
using Marten.Metadata;

namespace Aero.Cms.Core.Entities;

public sealed class PageDocument : Entity, ISiteOwned, ISoftDeleted, IAuditableEntity
{
    // ── Identity ──────────────────────────────────────────────────────────

    public long SiteId { get; set; }
    public PageKind Kind { get; set; } = PageKind.Standard;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    // ── Grouped state (value objects, not flat fields) ────────────────────

    public PageHierarchyState Hierarchy { get; set; } = new(null, "/", 0, 0);
    public PageSeoMetadata Seo { get; set; } = new(null, null, null);
    public PageDisplaySettings Display { get; set; } = PageDisplaySettings.Default;

    // ── Publication lifecycle ─────────────────────────────────────────────

    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    public DateTimeOffset? PublishedOn { get; set; }

    /// <summary>
    /// Monotonic counter incremented on every publish.
    /// Compared against <see cref="PageEditorState.DraftVersion"/> in the admin
    /// service layer to detect unpublished changes. Not compared here.
    /// </summary>
    public long PublishedVersion { get; set; }

    // ── Render manifest ───────────────────────────────────────────────────

    /// <summary>
    /// The canonical published layout manifest.
    /// Written ONLY by <see cref="Apply(PagePublished)"/>.
    /// Never written by draft save paths.
    /// Each placement holds only a BlockId reference — blocks are resolved
    /// at render time from the BlockBase document store (reference model).
    /// </summary>
    public List<LayoutRegion> LayoutRegions { get; set; } = [];

    // ── Soft Delete (Marten-managed) ──────────────────────────────────────

    public bool Deleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // ── Computed ──────────────────────────────────────────────────────────

    public bool IsPubliclyVisible =>
        PublicationState == ContentPublicationState.Published && !Deleted;

    // ── Event Sourcing ────────────────────────────────────────────────────

    public static PageDocument Create(PageCreated e) => new()
    {
        SiteId           = e.SiteId,
        Title            = e.Title,
        Slug             = e.Slug,
        Kind             = e.Kind,
        Hierarchy        = new PageHierarchyState(e.ParentId, e.Path, e.Depth, e.Order),
        PublicationState = e.PublicationState,
    };

    /// <summary>
    /// Metadata-only draft save. Updates title, slug, SEO, display settings.
    /// LayoutRegions deliberately NOT touched — publish path owns them.
    /// Renamed from PageContentUpdated: this event carries no block/body content.
    /// </summary>
    public void Apply(PageMetadataUpdated e)
    {
        Title   = e.Title;
        Slug    = e.Slug;
        Kind    = e.Kind;
        Seo     = new PageSeoMetadata(e.SeoTitle, e.SeoDescription, e.Summary);
        Display = Display with
        {
            ShowHeaderNavigation = e.ShowHeaderNavigation,
            HeaderImageUrl       = e.HeaderImageUrl,
            HideHeader           = e.HideHeader,
            HideFooter           = e.HideFooter,
            ShowChatAgent        = e.ShowChatAgent,
        };
        ModifiedOn = DateTimeOffset.UtcNow;
        // LayoutRegions: intentionally absent. Publish path only.
    }

    /// <summary>
    /// The only path that may write LayoutRegions.
    /// The publish pipeline builds the manifest via IPageLayoutManifestBuilder
    /// and passes it here through the event.
    /// </summary>
    public void Apply(PagePublished e)
    {
        PublicationState = ContentPublicationState.Published;
        PublishedOn      = DateTimeOffset.UtcNow;
        PublishedVersion = e.Version;
        LayoutRegions    = e.LayoutRegions.ToList();
    }

    public void Apply(PageArchived _) =>
        PublicationState = ContentPublicationState.Archived;

    public void Apply(PageStateChanged e)
    {
        PublicationState = e.NewState;
        if (e.NewState == ContentPublicationState.Published)
            PublishedOn = DateTimeOffset.UtcNow;
    }

    public void Apply(PageDeleted _)  => Deleted = true;
    public void Apply(PageRestored _) => Deleted = false;

    public void Apply(PageMoved e) =>
        Hierarchy = new PageHierarchyState(e.NewParentId, e.NewPath, e.NewDepth, e.NewOrder);

    public void Apply(PageVisibilityChanged e) =>
        Display = Display with
        {
            IsHidden      = e.IsHidden,
            ShowInNavMenu = e.ShowInNavMenu,
        };

    public PageViewModel ToViewModel() => new()
    {
        Id             = Id,
        Title          = Title,
        Slug           = Slug,
        Kind           = Kind,
        Summary        = Seo.Summary,
        SeoTitle       = Seo.SeoTitle,
        SeoDescription = Seo.SeoDescription,
        PublishedOn    = PublishedOn,
        IsPublished    = PublicationState == ContentPublicationState.Published,
        SiteId         = SiteId,
        ParentId       = Hierarchy.ParentId,
        Path           = Hierarchy.Path,
        Depth          = Hierarchy.Depth,
        Order          = Hierarchy.Order,
        IsHidden       = Display.IsHidden,
        ShowInNavMenu  = Display.ShowInNavMenu,
    };
}
```

---

### `PageEditorState`

The editor's draft workspace. The public renderer never reads this document.
Loaded by the editor API. Written on every draft save.

V1 decision: `PageEditorState` remains a flat top-level block placement document. It is intentionally not a page-level `NeoPageNode` tree. Nested composition for custom Neo-authored content lives inside a `NeoCompositionBlock : BlockBase`. A later PageEditor tree-view/outline may project this data visually for easier navigation, but that tree-view is a UX layer, not the V1 persistence model.

Product UX goal: the PageEditor should be simple for non-technical users. Authors should build pages by adding recognizable sections/blocks, editing obvious fields, and seeing WYSIWYG previews. The data model should support that experience without forcing users or implementers to treat the entire page as a developer-facing component tree.

**Deletion policy:** `PageEditorState` is hard-deleted when its corresponding
`PageDocument` is deleted (soft or hard). It carries no audit requirement and
does not participate in Marten's `ISoftDeleted` policy. The page delete handler
is responsible for issuing the hard delete. See `PageDeleteHandler`.

```csharp
namespace Aero.Cms.Core.Entities;

public sealed class PageEditorState
{
    // ── Identity ──────────────────────────────────────────────────────────
    // Same Id as the corresponding PageDocument.

    public long Id { get; set; }
    public long SiteId { get; set; }

    // ── Draft versioning ──────────────────────────────────────────────────

    /// <summary>
    /// Incremented on every draft save.
    /// Compared against PageDocument.PublishedVersion in PageAdminStatusService
    /// to detect unpublished changes. Never compared inside this class.
    /// </summary>
    public long DraftVersion { get; set; }

    // ── Editor block state ────────────────────────────────────────────────

    /// <summary>
    /// The editor's working set of block placements.
    /// Each placement references a persisted BlockBase by BlockId (once saved),
    /// or carries only a ClientId for new blocks not yet persisted.
    /// </summary>
    public List<EditorBlockPlacement> Blocks { get; set; } = [];

    /// <summary>
    /// Maps client-side EditorBlock.EditorId to the persisted BlockBase.Id.
    /// Rebuilt on every save so existing blocks are updated in-place.
    /// </summary>
    public Dictionary<string, long> BlockIdMap { get; set; } = [];

    public DateTimeOffset LastModified { get; set; }
}
```

---

### `EditorBlockPlacement`

Separates block *placement metadata* from block *content*. An `EditorBlockPlacement`
says "block X goes in region Y at order Z". Block content lives in `BlockBase`.

```csharp
namespace Aero.Cms.Core.Entities;

/// <summary>
/// Represents the placement of a block within the editor's working layout.
/// Block content is resolved separately from BlockBase documents.
/// </summary>
public sealed class EditorBlockPlacement
{
    /// <summary>
    /// Stable client-side identifier assigned by the editor UI.
    /// Used as the key in PageEditorState.BlockIdMap.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Persisted BlockBase.Id. Null for new blocks not yet saved.
    /// </summary>
    public long? BlockId { get; set; }

    /// <summary>
    /// The layout region this block belongs to (e.g. "main", "sidebar").
    /// </summary>
    public string Region { get; set; } = "main";

    /// <summary>
    /// Display order within the region. Lower = first.
    /// </summary>
    public int Order { get; set; }
}
```

---

### `BlockBase` — Unchanged Type Hierarchy

`BlockBase` is **not replaced**. The full `[JsonPolymorphic]` hierarchy,
source generators, and AOT-compatible serialization are preserved exactly as-is.

`BlockBase` *is* the persisted block content document. `BlockBase.Id` is the
stable reference key used in both `EditorBlockPlacement.BlockId` and
`BlockPlacement.BlockId` in the published manifest.

```csharp
// No changes to BlockBase or its subtypes.
// [JsonPolymorphic], [JsonDerivedType], source generators all remain.
```

---

## Value Objects

```csharp
namespace Aero.Cms.Core.Entities;

/// <summary>
/// Hierarchy position within the page tree.
/// Replaces: ParentId, Path, Depth, Order as flat fields.
/// </summary>
public sealed record PageHierarchyState(
    long? ParentId,
    string Path,
    int Depth,
    int Order
);

/// <summary>
/// SEO and summary metadata.
/// Replaces: SeoTitle, SeoDescription, Summary as flat fields.
/// </summary>
public sealed record PageSeoMetadata(
    string? SeoTitle,
    string? SeoDescription,
    string? Summary
);

/// <summary>
/// Display and navigation flags.
/// Replaces: IsHidden, ShowInNavMenu, ShowHeaderNavigation, HideHeader,
///           HideFooter, ShowChatAgent, HeaderImageUrl as flat fields.
/// </summary>
public sealed record PageDisplaySettings(
    bool IsHidden,
    bool ShowInNavMenu,
    bool ShowHeaderNavigation,
    bool HideHeader,
    bool HideFooter,
    bool ShowChatAgent,
    string? HeaderImageUrl
)
{
    public static PageDisplaySettings Default => new(
        IsHidden:             false,
        ShowInNavMenu:        true,
        ShowHeaderNavigation: true,
        HideHeader:           false,
        HideFooter:           false,
        ShowChatAgent:        true,
        HeaderImageUrl:       null
    );
}
```

---

## Layout Region Model (Publish Manifest)

`LayoutRegion` and `BlockPlacement` form the published render manifest stored on
`PageDocument.LayoutRegions`. Pure reference types — `BlockId` only, no data snapshot.

```csharp
namespace Aero.Cms.Abstractions.Blocks.Layout;

/// <summary>
/// A named layout region in the published page manifest.
/// The public renderer iterates regions and renders each placement.
/// </summary>
public sealed record LayoutRegion(
    string Name,
    IReadOnlyList<BlockPlacement> Placements
);

/// <summary>
/// A single block's position in a published layout region.
/// BlockId references a BlockBase document resolved at render time.
/// BlockType is stored for fast renderer dispatch only — not a data snapshot.
/// </summary>
public sealed record BlockPlacement(
    long BlockId,
    string BlockType,   // fast dispatch only — not a data copy
    int Order
);
```

### Block Reference Model Decision

| Concern | Decision |
|---|---|
| Block data source at render time | Live `BlockBase` load (reference model) |
| `BlockType` on placement | Yes — renderer dispatch without a full load |
| Full data snapshot on placement | **No** — avoids three copies of state and stale-manifest risk |
| Shared blocks edited after publish | Safe — renderer reads current `BlockBase` |
| Caching | Per `BlockBase` document ID, not per manifest |
| Future point-in-time rendering | Via a separate `SnapshotBlockPlacement` type, **not** a nullable field on `BlockPlacement` |

The N+1 concern is addressed by batch-loading `BlockBase` documents in the renderer
pipeline, not by embedding data in the manifest.

### Rejected: nullable `BlockStreamVersion` on `BlockPlacement`

Adding `long? BlockStreamVersion = null` to `BlockPlacement` to enable future
point-in-time rendering is explicitly rejected. A nullable field would require
the render pipeline to handle two modes permanently for every page render —
"load current" when null, "load as-of version" when set. That is dual-mode
complexity baked into the hot render path. If point-in-time block rendering is
ever needed, the correct approach is a dedicated `SnapshotBlockPlacement` type
or a published block snapshot document, keeping the default render path clean.

---

## `IPageLayoutManifestBuilder` — Shared by Preview and Publish

The single implementation that converts `PageEditorState` + resolved `BlockBase[]`
into `IReadOnlyList<LayoutRegion>`. Both preview and publish call this.
Prevents the two pipelines from drifting independently.

```csharp
namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Builds a layout manifest from editor placements and resolved block documents.
/// Used by both the preview pipeline (transient) and the publish pipeline (persisted).
/// This is the single place where EditorBlockPlacement[] becomes LayoutRegion[].
/// </summary>
public interface IPageLayoutManifestBuilder
{
    /// <summary>
    /// Builds layout regions from the editor state and the resolved block set.
    /// Blocks must be pre-loaded by the caller; this method does not load from the store.
    /// </summary>
    Task<IReadOnlyList<LayoutRegion>> BuildAsync(
        PageEditorState editor,
        IReadOnlyList<BlockBase> blocks,
        CancellationToken ct = default);
}
```

Default implementation:

```csharp
namespace Aero.Cms.Modules.Pages;

internal sealed class PageLayoutManifestBuilder : IPageLayoutManifestBuilder
{
    public Task<IReadOnlyList<LayoutRegion>> BuildAsync(
        PageEditorState editor,
        IReadOnlyList<BlockBase> blocks,
        CancellationToken ct = default)
    {
        var blockIndex = blocks.ToDictionary(b => b.Id);

        var regions = editor.Blocks
            .Where(p => p.BlockId.HasValue && blockIndex.ContainsKey(p.BlockId.Value))
            .GroupBy(p => p.Region)
            .Select(group => new LayoutRegion(
                Name: group.Key,
                Placements: group
                    .OrderBy(p => p.Order)
                    .Select(p => new BlockPlacement(
                        BlockId:   p.BlockId!.Value,
                        BlockType: blockIndex[p.BlockId.Value].BlockType,
                        Order:     p.Order))
                    .ToList()
            ))
            .ToList();

        return Task.FromResult<IReadOnlyList<LayoutRegion>>(regions);
    }
}
```

---

## Preview Pipeline

Preview is a first-class operation. It **never** writes to `PageDocument`.
Uses `IPageLayoutManifestBuilder` — the same builder as publish.

```csharp
namespace Aero.Cms.Modules.Pages;

public interface IPagePreviewService
{
    /// <summary>
    /// Builds a transient render manifest from the current editor state.
    /// Does not persist anything. Does not touch PageDocument.LayoutRegions.
    /// </summary>
    Task<PreviewRenderModel> BuildPreviewAsync(long pageId, CancellationToken ct = default);
}

public sealed class PreviewRenderModel
{
    /// <summary>Page metadata (title, slug, display settings). LayoutRegions is ignored.</summary>
    public PageDocument PageMeta { get; init; } = null!;

    /// <summary>Transient layout built from the current draft state.</summary>
    public IReadOnlyList<LayoutRegion> PreviewLayout { get; init; } = [];

    public bool IsDraft => true;
}
```

```
Preview pipeline:
  1. Load PageEditorState by pageId
  2. Load PageDocument by pageId (metadata only — title, slug, display settings)
  3. Batch-load BlockBase[] for all BlockIds in PageEditorState.Blocks
  4. Call IPageLayoutManifestBuilder.BuildAsync(editor, blocks)
  5. Return PreviewRenderModel { PageMeta, PreviewLayout }
  — renderer uses PreviewLayout, never PageDocument.LayoutRegions
```

---

## Publish Pipeline

The only path that may write `PageDocument.LayoutRegions`.
Uses `IPageLayoutManifestBuilder` — the same builder as preview.

```
Publish pipeline (PageContentService):

1.  Load PageEditorState
2.  Validate: all EditorBlockPlacement.BlockId values must resolve
3.  Batch-load BlockBase[] for all BlockIds
4.  Call IPageLayoutManifestBuilder.BuildAsync(editor, blocks)
5.  Compute next version: newVersion = PageDocument.PublishedVersion + 1
6.  Append PagePublished event:
      PagePublished
      {
          PageId:        pageId,
          Version:       newVersion,
          LayoutRegions: builtRegions,
      }
7.  PageDocument.Apply(PagePublished) writes LayoutRegions + bumps PublishedVersion
8.  PageEditorState.DraftVersion is NOT modified by publish
      (DraftVersion > PublishedVersion remains true until next draft save resets it,
       or until the admin UI refreshes status — by design, so the editor knows
       the last save has been published)
```

---

## Admin Status — `PageAdminStatusService`

Version comparison lives here, not on `PageDocument` or `PageEditorState`.
This is a read-model / admin-layer concern only.

```csharp
namespace Aero.Cms.Modules.Pages.Admin;

public sealed class PageAdminStatusService
{
    /// <summary>
    /// Derives the admin UI status from the published document and editor state.
    /// Neither PageDocument nor PageEditorState perform this comparison internally.
    /// </summary>
    public PageAdminStatus GetStatus(PageDocument page, PageEditorState editor) =>
        page.PublicationState switch
        {
            ContentPublicationState.Draft when page.PublishedVersion == 0
                => PageAdminStatus.NeverPublished,

            ContentPublicationState.Published when editor.DraftVersion > page.PublishedVersion
                => PageAdminStatus.PublishedWithDraftChanges,

            ContentPublicationState.Published
                => PageAdminStatus.Published,

            ContentPublicationState.Archived
                => PageAdminStatus.Archived,

            _ => PageAdminStatus.Draft
        };
}

public enum PageAdminStatus
{
    NeverPublished,
    Draft,
    Published,
    PublishedWithDraftChanges,
    Archived,
    Scheduled,   // reserved: add when ScheduledPublishOn is introduced
}
```

---

## `PageDeleteHandler` — Editor State Cleanup

`PageEditorState` has no `ISoftDeleted` and no audit requirement. It must be
hard-deleted when its page is deleted to avoid orphaned editor documents.

```csharp
namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Handles PageDeleted by hard-deleting the corresponding PageEditorState.
/// PageEditorState is editor scratch state — it does not participate in soft delete
/// and does not belong in any audit trail.
/// </summary>
public sealed class PageDeleteHandler
{
    private readonly IDocumentSession _session;

    public PageDeleteHandler(IDocumentSession session) =>
        _session = session;

    public async Task HandleAsync(PageDeleted e, CancellationToken ct = default)
    {
        // Hard delete — no soft delete, no recovery path.
        _session.HardDelete<PageEditorState>(e.PageId);
        await _session.SaveChangesAsync(ct);
    }
}
```

Note: if `PageDocument` is later hard-deleted (purged), `PageEditorState` will
already be gone. The handler fires on soft delete so the editor document is
cleaned up immediately and does not linger while the page is in the soft-deleted state.

---

## Event Shapes (Updated)

```csharp
namespace Aero.Cms.Abstractions.Events;

// Renamed from PageContentUpdated.
// Carries metadata only — no block/body content, no LayoutRegions.
// OldSlug is populated only when Slug has changed (for cache eviction).
public sealed record PageMetadataUpdated(
    long PageId,
    long SiteId,
    string Title,
    string Slug,
    string? OldSlug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    PageKind Kind,
    bool ShowHeaderNavigation,
    string? HeaderImageUrl,
    bool HideHeader,
    bool HideFooter,
    bool ShowChatAgent
);

// The only event that may produce LayoutRegions.
// Built by IPageLayoutManifestBuilder before this event is appended.
public sealed record PagePublished(
    long PageId,
    long Version,
    IReadOnlyList<LayoutRegion> LayoutRegions
);
```

---

## Migration Notes

### Existing `PageDocument` rows with `Blocks` / `BlockIdMap`

```
For each PageDocument where Blocks.Count > 0:
  Create PageEditorState {
      Id           = page.Id,
      SiteId       = page.SiteId,
      DraftVersion = page.PublishedVersion + 1,
      Blocks       = map page.Blocks -> EditorBlockPlacement[],
      BlockIdMap   = page.BlockIdMap,
      LastModified = page.ModifiedOn ?? DateTimeOffset.UtcNow
  }

After migration:
  Blocks and BlockIdMap on PageDocument can be dropped.
  LayoutRegions-only pages get an empty PageEditorState with DraftVersion = 0.
  These surface in the admin UI as Published (no draft changes) until re-edited.
```

### Event stream rename: `PageContentUpdated` → `PageMetadataUpdated`

Existing persisted events named `PageContentUpdated` need a one-time upcasting
registration in Marten:

```csharp
options.Events.MapEventType<PageMetadataUpdated>("page-content-updated");
```

This tells Marten to deserialize old `page-content-updated` stream entries as
`PageMetadataUpdated` without touching the stored data.

---

## What Is Explicitly Preserved

| Concern | Status |
|---|---|
| `BlockBase` type hierarchy | **Unchanged** |
| `[JsonPolymorphic]` + `[JsonDerivedType]` attributes | **Unchanged** |
| Roslyn source generators (AOT, block registration) | **Unchanged** |
| `LayoutRegion` / `BlockPlacement` types | **Refined, not replaced** |
| Event sourcing `Apply` methods | **Refined** |
| `IBlockService`, `IBlockRegistry` | **Unchanged** |
| Marten document store pattern | **Unchanged** |
| `ISiteOwned`, `ISoftDeleted`, `IAuditableEntity` | **Unchanged** |
| Marten stream versioning for `BlockBase` | **Unchanged — Marten owns it** |

---

## STEP BY STEP: Add a New Page Block in the Refactored Editor

Use this checklist for every new PageEditor block. The goal is one vertical
slice per UI library or block family: the package that owns the block should own
the persisted model, public static SSR renderer, editor preview, modal editor,
mapper, and runtime editor definition. Do not add a new block by sprinkling
cases across every PageEditor switch unless the block is a legacy inline-edit
exception.

1. Create the persisted block model.
   - Add a `BlockBase` subtype in the owning UI package, for example `src/Aero.Cms.Ui.Hyper/Blocks/Pricing/Pricing1Block.cs`.
   - Add `[BlockMetadata("catalog.id", "Display Name", Category = "...", Icon = "...", SortOrder = ...)]`.
   - Keep the `BlockType` value exactly equal to the catalog id.
   - Example: `hyper.pricing.1`.

2. Create or update the public renderer.
   - Add a renderer component beside the block model in the owning package, for example `src/Aero.Cms.Ui.Hyper/Blocks/Pricing/Pricing1BlockRenderer.razor`.
   - Public renderers must output static SSR-safe HTML. Do not require PageEditor-only services or interactive Blazor render modes for public CMS pages.
   - Add a package-local renderer marker in a `.cs` file beside the block or in a package `RendererMarkers.cs`.
   - Do not rely on Razor `@attribute [CmsBlockRenderer(...)]` for package discovery. The source generator runs against C# symbols and must see a normal `.cs` partial declaration.

```csharp
using Aero.Cms.Abstractions.Blocks.Rendering;

namespace Aero.Cms.Ui.Hyper.Blocks.Pricing;

[CmsBlockRenderer(typeof(Pricing1Block))]
public partial class Pricing1BlockRenderer;
```

3. Add mapper logic only if the editor uses a node/DTO shape.
   - For Neo/Hyper editor blocks, create a mapper beside the block such as `Pricing1BlockMapper`.
   - Map `BlockBase -> NeoPageNode` for preview.
   - Map `NeoPageNode -> BlockBase` when needed by editor previews.

4. Add a PageEditor runtime definition in the owning package.
   - Implement `IPageEditorBlockDefinition`.
   - The definition owns:
     - catalog metadata for the PageEditor palette,
     - default `EditorBlock` creation,
     - `EditorBlock -> NeoPageNode`,
     - `EditorBlock -> BlockBase`,
     - optional preview component type,
     - optional modal editor component type.
   - This replaces the old pattern of adding the same catalog id to many hardcoded switches.

5. Add the editor preview component.
   - Place it beside the block in the owning package.
   - Prefer a lightweight preview that shows what the block will look like without turning the public page renderer into an interactive manager component.
   - Registered preview components should accept a `NeoPageNode Node` parameter unless the definition intentionally uses a different parameter shape.

6. Add modal/editor fields.
   - For simple/new blocks, provide a block-specific editor component through `IPageEditorBlockDefinition.PropertyEditorComponentType`.
   - The editor UX should be simple enough for non-technical authors: obvious labels, direct editing, minimal hidden configuration, and sensible defaults.

7. Add a provider and service registration for the package.
   - Implement `IPageEditorBlockProvider` in the owning package and return the package's definitions.
   - Implement `ICmsBlockModelProvider` or reuse the same provider so Marten can map package-owned `BlockBase` subtypes.
   - Add an extension such as `services.AddAeroCmsHyperUiBlocks()`.
   - The extension registers the editor provider, block model provider, and the package's generated `ICmsBlockRenderRegistry`.
   - The public/server web host references the package and calls the extension once so public `.cshtml` rendering can find the package renderer registry.
   - The WebAssembly client also references the package and calls the extension once so PageEditor can find the package editor definitions, previews, and modal editors.

8. Confirm the catalog appears in the PageEditor menu.
   - The PageEditor palette combines source-generated catalog items with registered `IPageEditorBlockDefinition` metadata.
   - Existing sections should not require new sidebar plumbing.
   - A third-party UI package should not edit `Aero.Cms.Shared` per block.

9. Verify save and public render.
   - Add the block in PageEditor.
   - Confirm editor preview renders.
   - Save/publish.
   - Confirm `EditorBlockMapper` uses `PageEditorBlockRegistry` and persists the correct `BlockBase`.
   - Confirm the public `.cshtml` renderer path renders the block through generated renderer wiring.

10. Build the affected projects.

```powershell
dotnet build src\Aero.Cms.Abstractions\Aero.Cms.Abstractions.csproj /p:UseSharedCompilation=false --verbosity minimal
dotnet build src\Aero.Cms.Ui.Hyper\Aero.Cms.Ui.Hyper.csproj /p:UseSharedCompilation=false --verbosity minimal
dotnet build src\Aero.Cms.Web.Client\Aero.Cms.Web.Client.csproj /p:UseSharedCompilation=false --verbosity minimal
dotnet build src\Aero.Cms.Web\Aero.Cms.Web.csproj /p:UseSharedCompilation=false --verbosity minimal
dotnet build src\Aero.Cms.Modules.Pages\Aero.Cms.Modules.Pages.csproj /p:UseSharedCompilation=false --verbosity minimal
```

Important rule: `PageEditorState` remains a flat editor draft list. Highly
composed layouts can still be represented inside a single `BlockBase` payload
when needed, but V1 should keep the simplest authoring model: drag a section,
edit the section, preview the section, publish the page.
