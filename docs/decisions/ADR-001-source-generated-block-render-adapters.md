# ADR-001: Use Source-Generated Block Render Adapters

## Status

Accepted

## Date

2026-04-29

## Context

AeroCMS currently renders persisted CMS blocks through a manually maintained switch in `BlockRenderer.razor`. The same block knowledge is duplicated across `BlockBase` `JsonDerivedType` attributes, `BlockJsonContext`, Marten subclass registration, editor metadata, and a legacy visitor/slice rendering path.

This creates drift. A new block can be added to one registration point while missing another, which moves failures from build time to runtime. The current inventory already shows that Marten and `BlockRenderer.razor` do not cover the full set of block models known to `BlockBase` and `BlockJsonContext`.

AeroCMS also wants to improve trim-safety and prepare the rendering pipeline for future Native AOT support. Runtime assembly scanning and reflection-based renderer discovery would be convenient, but they are a poor fit for that direction.

## Decision

Use a Roslyn incremental source generator to discover block metadata and renderer components at compile time, then generate typed render adapters and a generated registry.

The initial implementation will focus on generated render adapters for a small proof set:

- `MarkdownBlock`
- `RawHtmlBlock`
- `NavigationBlock`

The generated adapters will implement a common `ICmsBlockRenderAdapter` contract and will adapt the generic `IBlock` rendering pipeline to concrete Razor components with strongly typed `[Parameter]` values.

Later phases will expand the same discovery model to generate or assist:

- the block manifest used by editor metadata.
- `System.Text.Json` polymorphic registration.
- Marten subclass registration.
- diagnostics for metadata, renderer, JSON, and Marten drift.

## Alternatives Considered

### Keep The Manual Switch

Keeping `BlockRenderer.razor` as the central dispatch point is simple in the short term.

Rejected because it is already drift-prone and requires every new block to touch multiple unrelated files. It also does not create a path toward compile-time validation.

### Runtime Reflection And Assembly Scanning

Runtime scanning could find renderer components with attributes at startup and build a registry dynamically.

Rejected because it preserves runtime discovery, is harder to trim safely, and does not align with future Native AOT preparation.

### Blazor `DynamicComponent` Dictionary Dispatch

`DynamicComponent` can render a component by `Type` with a parameter dictionary.

Deferred as a possible intermediate step, but not the preferred end state. Generated adapters are more explicit, allow compile-time diagnostics for renderer/model mismatches, and avoid fragile parameter-name dictionaries in application code.

### Generate Or Keep Parallel Slice Renderers

The legacy `BlockSliceRegistry` and `IBlockSliceRenderer` path could be kept and populated by generated wrappers.

Deferred as a migration bridge only. The preferred end state is for block rendering to flow through Razor components and generated adapters, with server-side HTML rendering using Blazor `HtmlRenderer` when needed.

## Consequences

- Adding a developer block should eventually require a model with `BlockMetadataAttribute` and a renderer component with a `CmsBlockRendererAttribute`, not manual edits in every registry.
- The first generator slice must be kept small and covered by generated-source snapshot tests.
- Generated `RenderTreeBuilder` code must use literal sequence numbers.
- `BlockBase` `JsonDerivedType` attributes remain in place until the later polymorphic serialization phase replaces the handwritten list with generated metadata/resolver configuration.
- The legacy visitor/slice path remains until Phase 3 defines and verifies the bridge or removal path.
- Arbitrary custom JavaScript blocks remain excluded. Dynamic runtime content should use HTMX, Scriban templates, or typed provider blocks.
