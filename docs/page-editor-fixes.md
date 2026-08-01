# Page Editor Fixes: NeoUI & HyperUI Not Rendering on Public Pages

## Summary

NeoUI and HyperUI blocks rendered correctly in the page editor (manager) but appeared as empty `<section>` elements on public CMS pages. The root cause was an overzealous normalization step in `PageTreeLegacyBlockNormalizer` that rewrote unrecognized block catalog IDs to `"primitive.section"`, preventing the DI-based block renderer registry from resolving them.

## Root Cause

### File

`src/Aero.Cms.Shared/Blocks/Rendering/PageTreeLegacyBlockNormalizer.cs`

### Method

`NormalizeLegacyBlock(NeoPageNode node)` — invoked from `BuildRootNode` in `DynamicPageModel` during public page composition loading.

### The Bug

The method has a `switch` on `node.CatalogId` that handles known legacy IDs (`"hero"`, `"image"`, `"text"`, `"video"`, etc.). The **default case** does:

```csharp
_ => CloneAs(
    node,
    catalogId: "primitive.section",
    kind: NeoPageNodeKind.Section,
    properties: CloneProperties(node.Properties))
```

This rewrites **any** unrecognized `CatalogId` to `"primitive.section"` and changes `Kind` from `Block` to `Section`.

### Why NeoUI/HyperUI Blocks Break

NeoUI blocks have catalog IDs like `"aero.hero.01"` and HyperUI blocks have IDs like `"hyper.cards.1"`. These are **not** listed in the switch, so they hit the default case and get rewritten.

When `NeoNodeRenderer` renders the page with `CatalogId = "primitive.section"`, it matches `case "primitive.section":` which renders children recursively — but Neo/Hyper blocks are leaf blocks with no child nodes. The result is an **empty `<section>`** with no content and no error.

### Why Legacy Blocks Work

Legacy catalog IDs (`"hero"`, `"boring_hero"`, `"image"`, `"text"`, `"video"`, etc.) are **explicitly listed** in the switch with proper mappings. They never reach the default case.

## Rendering Pipeline (Trace)

| Step | Component | Status |
|------|-----------|--------|
| Editor → Node creation | `ToNeoPageNode()` | ✅ Correct (CatalogId preserved) |
| Composition save | `PageCompositionDocument` | ✅ Correct |
| Composition load | `DynamicPageModel.LoadCompositionAsync` | ✅ Correct |
| Previous normalization | `PageTreeLegacyBlockNormalizer.Normalize` | 🔴 CatalogId was rewritten |
| Current migration | `PageTreeLegacyNodeMigrator` | ✅ Modern IDs preserved; known legacy IDs migrated only when detected |
| Renderer dispatch | `NeoNodeRenderer` switch | ✅ Would work if CatalogId preserved |
| Block mapper | `TryMapLegacyBlock` | ✅ Correctly resolves |
| Block renderer | `BlockRenderer` → `HyperCmsBlockRenderRegistry` | ✅ Correctly resolves |

## Implemented Direction

Rather than keeping the legacy normalizer in the center of the modern render
pipeline, the code now treats the persisted `NeoPageNode` tree as authoritative.
The legacy logic has been narrowed into an explicit known-legacy migrator.

### Changes

1. `PageTreeLegacyBlockNormalizer` was replaced in runtime paths by
   `PageTreeLegacyNodeMigrator`.
2. Save-time root-node deserialization now clones the submitted tree without
   rewriting catalog IDs.
3. Preview-fragment rendering now clones the submitted tree without rewriting
   catalog IDs.
4. Public page rendering only invokes legacy migration when the tree actually
   contains known old block IDs.
5. Unknown/package catalog IDs are preserved so the registry-based renderer can
   resolve NeoUI, HyperUI, AeroUI, and future external package blocks.

### Default Case

```diff
- _ => CloneAs(
-     node,
-     catalogId: "primitive.section",
-     kind: NeoPageNodeKind.Section,
-     properties: CloneProperties(node.Properties))
+ _ => Clone(node)
```

### Rationale

- Modern nodes preserve the original `CatalogId`, `Kind`, `Properties`, and `Children`.
- `NeoNodeRenderer` receives the intact catalog ID, falls through to the registered renderer path, and can resolve via `IPageEditorDefinitionRegistry` → `BlockRenderer` → `ICmsBlockRenderRegistry`.
- Legacy blocks are only migrated when their known old IDs are detected.
- Unrecognized blocks without a renderer hit `NeoNodeRenderer`'s final `default: { break; }` and render nothing — which is cleaner than an empty `<section>`.

### Safety

1. **Modern catalog IDs are no longer mutated** — package-owned blocks remain discoverable by registry.
2. **Legacy migration is explicit** — only known old `NeoPageNodeKind.Block` IDs are transformed.
3. **Save and preview are modern-first** — the editor stores and previews the tree it authored.
4. **No DI coupling in migration** — rendering ownership stays with the renderer/registry layer.
5. **Unregistered blocks render nothing publicly** — editor diagnostics should handle authoring-time feedback.

## Related Files

| File | Role |
|------|------|
| `src/Aero.Cms.Shared/Blocks/Rendering/PageTreeLegacyBlockNormalizer.cs` | Legacy migrator + compatibility facade |
| `src/Aero.Cms.Shared/Blocks/Rendering/NeoNodeRenderer.razor` | Public page renderer (switch + `TryMapLegacyBlock`) |
| `src/Aero.Cms.Shared/Blocks/Rendering/BlockRenderer.razor` | Adapter-based block renderer |
| `src/Aero.Cms.Shared/Blocks/Rendering/ComponentCmsBlockRenderAdapter.cs` | Runtime adapter for renderer components |
| `src/Aero.Cms.Ui.Neo/NeoCmsBlockRenderRegistry.cs` | NeoUI explicit render registry |
| `src/Aero.Cms.Ui.Hyper/HyperCmsBlockRenderRegistry.cs` | HyperUI explicit render registry |
| `src/Aero.Cms.Ui.Neo/NeoPageEditorBlockProvider.cs` | Editor + model registration |
| `src/Aero.Cms.Ui.Hyper/HyperPageEditorBlockProvider.cs` | Editor + model registration |
| `src/Aero.Cms.Shared/Pages/Manager/PageEditor/Definitions/PageEditorDefinitionRegistry.cs` | Registry builder |
| `src/Aero.Cms.Modules.Pages/Areas/Cms/Pages/Page.cshtml` | Public page template |
| `src/Aero.Cms.Modules.Pages/Areas/Cms/Pages/Page.cshtml.cs` | `DynamicPageModel` |
