# AeroCMS: Next-Generation Block Rendering Architecture

This document outlines the recommended architecture for AeroCMS's block rendering pipeline, resolving the current reliance on manually maintained rendering dispatch and duplicated block registration. It aligns the CMS with modern .NET performance standards, improves trim-safety, and prepares the rendering pipeline for future Native AOT support while preserving developer ergonomics and enabling dynamic runtime content creation.

> **Important note on Native AOT compatibility:**
> As of .NET 10, Microsoft does not fully support Native AOT compilation for ASP.NET Core MVC or Blazor Server, including Razor Pages that render components with the `<component>` tag helper. ASP.NET Core Native AOT support is currently strongest for gRPC and partial for Minimal APIs.
>
> This Source Generator + Adapter architecture should therefore be treated as an AOT-preparation and trim-safety step, not as a claim that the current AeroCMS web application can be fully published as Native AOT today. The goal is to remove avoidable runtime discovery, reflection-heavy registration, and manual dispatch so the rendering pipeline is ready for future ASP.NET Core hosting support and performs better immediately.

---

## 1. The Core Problem: Manual Dispatch & Duplicated Registries

Currently, AeroCMS relies on a monolithic `BlockRenderer.razor` file containing a hardcoded `switch` statement to dispatch blocks to their respective UI components based on persisted block type strings such as `"aero_hero"`.

The same block knowledge is also repeated in other places:

* `BlockBase` contains a long list of `JsonDerivedType` registrations for polymorphic JSON.
* Marten configuration must know the block subclass hierarchy.
* The editor and renderer both need to understand which model belongs to which block type.
* The renderer switch must be manually updated when a new block renderer is added.

This creates a drift-prone system. Adding a block requires coordinated edits across multiple files, and a missed registration can surface as a runtime rendering failure instead of a build-time diagnostic.

Traditional CMS platforms often solve this with runtime reflection and startup scanning. That approach is convenient, but it is a poor fit for trimming and future Native AOT work because unbounded runtime discovery, dynamic code loading, and reflection-based serialization are exactly the areas that Native AOT tooling flags.

---

## 2. The Solution: Source Generators + Generated Render Adapters

To eliminate the hardcoded switch and avoid runtime scanning, AeroCMS should use a Roslyn Incremental Source Generator to produce a compile-time block manifest and a set of generated render adapters.

The first version may still use Blazor's built-in `<DynamicComponent>` internally, but the preferred long-term shape is a generated adapter per block renderer. This gives AeroCMS a typed rendering boundary instead of only a `string -> Type` dictionary.

The block model should be the single source of truth for persisted block metadata. Renderer components should reference the block model type, and the generator should read the persisted discriminator, display name, category, and editor metadata from the model's existing `BlockMetadataAttribute`.

### The GoF Patterns Involved

This design intentionally uses several GoF and architectural patterns:

* **Adapter:** Each generated adapter converts the CMS's generic `IBlock` rendering contract into the concrete renderer component's strongly typed `[Parameter]`.
* **Strategy:** Each adapter is the rendering strategy for one block type.
* **Registry:** A generated registry maps persisted block type strings to adapters.
* **Composite:** The existing page structure remains `PageDocument -> LayoutRegion -> LayoutColumn -> BlockPlacement -> Block`.
* **Factory-like selection:** The registry selects the correct adapter for a block type without a central switch statement.

---

## 3. Where the Adapter Contracts Live

The rendering contracts should live in the shared rendering layer, close to the Razor components, not in the core domain model:

```text
src/Aero.Cms.Shared/Blocks/Rendering/
  ICmsBlockRenderAdapter.cs
  ICmsBlockRenderRegistry.cs
  BlockRenderContext.cs
  CmsBlockRendererAttribute.cs
```

The source generator should live in a separate analyzer project:

```text
src/Aero.Cms.SourceGenerators/
  BlockRendererGenerator.cs
```

Generated files are emitted under `obj/.../generated/...` during build and compiled into the assembly that contains the renderers, typically `Aero.Cms.Shared`.

Adapters are not database records. They are compiled application code.

---

## 4. Block Metadata & Renderer Attributes

AeroCMS already has model-side `BlockMetadataAttribute` usage. That should become the authoritative source for the persisted discriminator and editor metadata.

```csharp
[BlockMetadata("aero_hero", "Aero Hero", Category = "Aero")]
public sealed class AeroHeroBlock : BlockBase
{
    public override string BlockType => "aero_hero";
}
```

Renderer components should reference the model type only. They should not repeat the discriminator string.

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CmsBlockRendererAttribute : Attribute
{
    public Type ModelType { get; }

    public CmsBlockRendererAttribute(Type modelType) => ModelType = modelType;
}
```

Example renderer:

```razor
@* AeroHeroRenderer.razor *@
@attribute [CmsBlockRenderer(typeof(AeroHeroBlock))]

<div class="hero">
    <h1>@Block.Title</h1>
</div>

@code {
    [Parameter] public AeroHeroBlock Block { get; set; } = default!;
}
```

The generator should emit diagnostics when:

* Two renderers target the same block model.
* Two block models declare the same persisted discriminator.
* The renderer has no `[Parameter]` named `Block`.
* The `Block` parameter type does not match the targeted model type.
* The model type does not derive from `BlockBase` or implement `IBlock`.
* A block model has `BlockMetadataAttribute` but no renderer.
* A renderer targets a block model that has no `BlockMetadataAttribute`.
* The model's `BlockType` override does not match the discriminator in `BlockMetadataAttribute` when the generator can safely determine the constant value.

---

## 5. The Generated Block Manifest

The generator should produce a block manifest, not just a renderer dictionary. The manifest becomes the single compile-time source of truth for rendering metadata.

```csharp
public sealed record CmsBlockDescriptor(
    string BlockType,
    string DisplayName,
    string? Category,
    Type ModelType,
    Type RendererType,
    string RendererParameterName);
```

Generated example:

```csharp
// Auto-generated by Aero.Cms.SourceGenerators
#nullable enable
using System;
using System.Collections.Generic;

namespace Aero.Cms.Shared.Blocks.Rendering;

public static partial class CmsBlockManifest
{
    public static readonly IReadOnlyDictionary<string, CmsBlockDescriptor> Blocks =
        new Dictionary<string, CmsBlockDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["rich_text"] = new(
                "rich_text",
                "Rich Text",
                "Text",
                typeof(Aero.Cms.Abstractions.Blocks.Common.RichTextBlock),
                typeof(Aero.Cms.Shared.Blocks.Rendering.RichTextBlockRenderer),
                "Block"),

            ["aero_hero"] = new(
                "aero_hero",
                "Aero Hero",
                "Aero",
                typeof(Aero.Cms.Abstractions.Blocks.Common.AeroHeroBlock),
                typeof(Aero.Cms.Shared.Blocks.Rendering.AeroHeroRenderer),
                "Block"),
        };
}
```

This manifest can later support:

* Editor block palettes.
* generated JSON serializer context entries.
* generated Marten subclass configuration.
* block validation.
* diagnostics for missing renderers or unknown persisted block types.

The manifest should grow in phases. The first implementation can use it for rendering only. Later phases should use the same discovery data for JSON source generation, Marten subclass configuration, and editor metadata so block registration has one compile-time source of truth.

---

## 6. The Render Adapter Contract

The adapter is the bridge between the generic CMS rendering pipeline and a concrete Razor component.

```csharp
using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Blocks.Rendering;

public interface ICmsBlockRenderAdapter
{
    string BlockType { get; }
    Type ModelType { get; }

    RenderFragment Render(IBlock block, BlockRenderContext context);
}
```

The render context carries cross-cutting information that some renderers need without growing every block model:

```csharp
using Aero.Cms.Abstractions.Http.Clients;
using System.Globalization;

namespace Aero.Cms.Shared.Blocks.Rendering;

public sealed record BlockRenderContext(
    NavigationDetail? Navigation = null,
    bool IsPreview = false,
    bool IsHtmxRequest = false,
    string? HtmxTarget = null,
    CultureInfo? Culture = null);
```

`BlockRenderContext` is intentionally a rendering-layer concept. It carries cross-cutting rendering facts such as navigation data, preview mode, HTMX request state, and culture without pushing those concerns into persisted block entities.

Generated adapter example:

```csharp
// Auto-generated by Aero.Cms.SourceGenerators
#nullable enable
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Blocks.Rendering;

internal sealed class AeroHeroBlockRenderAdapter : ICmsBlockRenderAdapter
{
    public string BlockType => "aero_hero";
    public Type ModelType => typeof(AeroHeroBlock);

    public RenderFragment Render(IBlock block, BlockRenderContext context)
    {
        return builder =>
        {
            if (block is not AeroHeroBlock typedBlock)
            {
                builder.AddContent(0, $"Invalid block model for '{BlockType}'.");
                return;
            }

            builder.OpenComponent<AeroHeroRenderer>(1);
            builder.AddAttribute(2, "Block", typedBlock);
            builder.CloseComponent();
        };
    }
}
```

For special cases such as navigation, the generated adapter can pass additional context:

```csharp
builder.OpenComponent<NavigationBlockRenderer>(1);
builder.AddAttribute(2, "Block", typedBlock);
builder.AddAttribute(3, "Navigation", context.Navigation);
builder.CloseComponent();
```

For HTMX-aware blocks or partial refresh endpoints, the adapter can also use the context to pass render-mode state into components without changing the block model:

```csharp
builder.OpenComponent<SomeInteractiveBlockRenderer>(1);
builder.AddAttribute(2, "Block", typedBlock);
builder.AddAttribute(3, "IsHtmxRequest", context.IsHtmxRequest);
builder.AddAttribute(4, "HtmxTarget", context.HtmxTarget);
builder.CloseComponent();
```

HTMX behavior should remain opt-in per renderer. The context makes request state available, but adapters should not automatically decorate every block with HTMX attributes.

This is a true GoF Adapter: it adapts a renderer component with a concrete parameter type into the uniform `ICmsBlockRenderAdapter.Render(IBlock, BlockRenderContext)` contract expected by the CMS pipeline.

Because generated adapters use `RenderTreeBuilder`, the generator must treat render-tree construction as an advanced, correctness-sensitive API. Generated calls should use literal sequence numbers, not counters or calculated values. The generated output should remain small, predictable, and covered by snapshot tests so malformed render trees or unstable sequence behavior are caught early.

Do not generate sequence numbers with `seq++`, helper counters, or calculated offsets. Blazor sequence numbers represent source locations, not runtime call order. If an adapter needs conditional parameters, keep the sequence numbers literal inside each generated branch:

```csharp
builder.OpenComponent<NavigationBlockRenderer>(1);
builder.AddAttribute(2, "Block", typedBlock);

if (context.Navigation is not null)
{
    builder.AddAttribute(3, "Navigation", context.Navigation);
}

builder.CloseComponent();
```

If generated render logic grows beyond a small component open/attribute/close sequence, split it into smaller generated helper methods or use `OpenRegion` / `CloseRegion` so each region has its own clear sequence-number space. Avoid long hand-authored or generated render-tree bodies.

---

## 7. Generated Registration

Adapters should be registered at compile time. There should be no runtime assembly scanning.

```csharp
// Auto-generated by Aero.Cms.SourceGenerators
#nullable enable
using System;
using System.Collections.Generic;

namespace Aero.Cms.Shared.Blocks.Rendering;

public static partial class CmsBlockRenderRegistry
{
    private static readonly IReadOnlyDictionary<string, ICmsBlockRenderAdapter> Adapters =
        new Dictionary<string, ICmsBlockRenderAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["rich_text"] = new RichTextBlockRenderAdapter(),
            ["aero_hero"] = new AeroHeroBlockRenderAdapter(),
            ["navigation"] = new NavigationBlockRenderAdapter(),
        };

    public static bool TryGet(string blockType, out ICmsBlockRenderAdapter adapter)
        => Adapters.TryGetValue(blockType, out adapter!);
}
```

If dependency injection is needed later, the generator can also emit a DI registration extension:

```csharp
public static partial class CmsBlockRenderingServiceCollectionExtensions
{
    public static IServiceCollection AddGeneratedCmsBlockRenderers(
        this IServiceCollection services)
    {
        services.AddSingleton<ICmsBlockRenderRegistry, GeneratedCmsBlockRenderRegistry>();
        return services;
    }
}
```

For the initial implementation, a static generated registry is simpler and avoids unnecessary service resolution in the hot render path.

---

## 8. The Refactored Dispatcher

`BlockRenderer.razor` becomes a thin shell around the generated registry.

```razor
@using Aero.Cms.Abstractions.Blocks
@using Aero.Cms.Shared.Blocks.Rendering

@if (Block is not null)
{
    if (CmsBlockRenderRegistry.TryGet(Block.BlockType, out var adapter))
    {
        @adapter.Render(Block, new BlockRenderContext(Navigation))
    }
    else
    {
        <div class="p-4 border border-dashed border-gray-300 rounded text-gray-500 italic">
            Unknown block type: @Block.BlockType (@Block.GetType().Name)
        </div>
    }
}

@code {
    [Parameter] public IBlock? Block { get; set; }
    [Parameter] public NavigationDetail? Navigation { get; set; }
}
```

This removes the central switch, keeps the rendering decision O(1), and gives the generator room to enforce type safety.

Blazor's `<DynamicComponent>` remains a valid fallback or intermediate implementation. Microsoft documents it for rendering components by `Type` with an `IDictionary<string, object>` of parameters. The adapter approach is preferred because it avoids fragile parameter-name strings in application code and lets the generator validate the renderer/model pairing at build time.

---

## 9. Server-Side Rendering Path & BlockSliceRegistry

AeroCMS currently has a second server-side rendering abstraction: `BlockSliceRegistry`, `IBlockSliceRenderer`, and `IBlockVisitor`. This must be addressed explicitly. Otherwise the system still has two rendering paths that can drift:

```text
Path 1: BlockRenderer.razor -> generated adapters -> Razor components
Path 2: BlockSliceRegistry -> IBlockSliceRenderer -> IHtmlContent
```

The recommended direction is to make Razor components the single rendering implementation and retire the slice renderer path once all consumers can be migrated.

### Preferred End State

All block rendering flows through the generated adapter registry and Razor components:

```text
Page render
Preview render
API/email/static HTML render
    -> BlockRenderer / generated adapter registry
    -> compiled Razor component renderer
```

For non-interactive server-side HTML generation, use Blazor's `HtmlRenderer`. Microsoft documents `HtmlRenderer` for rendering Razor components to a string or stream outside an HTTP request, but calls must run through `HtmlRenderer.Dispatcher.InvokeAsync(...)`.

Conceptual bridge:

```csharp
public sealed class CmsBlockHtmlRenderer
{
    private readonly HtmlRenderer _htmlRenderer;

    public Task<string> RenderBlockAsync(IBlock block, BlockRenderContext context)
    {
        return _htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Block"] = block,
                ["Navigation"] = context.Navigation,
                ["IsPreview"] = context.IsPreview,
                ["IsHtmxRequest"] = context.IsHtmxRequest,
                ["HtmxTarget"] = context.HtmxTarget,
                ["Culture"] = context.Culture
            });

            var output = await _htmlRenderer.RenderComponentAsync<BlockRenderer>(parameters);
            using var writer = new StringWriter();
            output.WriteHtmlTo(writer);
            return writer.ToString();
        });
    }
}
```

This makes `BlockSliceRegistry` an adapter during migration, not a permanent parallel rendering stack.

### Migration Options

* **Option A: Deprecate `BlockSliceRegistry`.** Preferred if no hard dependency requires `IBlockSliceRenderer`.
* **Option B: Bridge `BlockSliceRegistry` to Blazor.** Implement a temporary `IBlockSliceRenderer` that delegates to `CmsBlockHtmlRenderer`.
* **Option C: Generate slice renderers.** Only use this if a non-Blazor rendering path must remain for a specific reason. This preserves the dual-path complexity and should be avoided unless there is a strong requirement.

The spec should treat Option A or B as the default path. Option C keeps the drift problem alive.

Temporary bridge shape:

```csharp
public sealed class LegacyBlockSliceRenderer : IBlockSliceRenderer
{
    private readonly CmsBlockHtmlRenderer htmlRenderer;

    public LegacyBlockSliceRenderer(CmsBlockHtmlRenderer htmlRenderer)
    {
        this.htmlRenderer = htmlRenderer;
    }

    public async Task<IHtmlContent> RenderAsync(
        BlockBase block,
        CancellationToken cancellationToken = default)
    {
        var html = await htmlRenderer.RenderBlockAsync(
            block,
            new BlockRenderContext(),
            cancellationToken);

        return new HtmlString(html);
    }
}
```

The exact interface signature should match the current AeroCMS `IBlockSliceRenderer` contract. The important migration rule is that legacy slice rendering becomes a pass-through adapter to the generated Blazor rendering path. It should not keep independent per-block HTML implementations alive.

---

## 10. Generator Scope Beyond Rendering

The first generator milestone should focus on the rendering path. Later milestones should expand from the same block discovery model.

If the team wants one larger first implementation, the JSON and Marten artifacts in this section can move into Phase 1. The lower-risk default is still to ship render adapters first, then expand the generator once the discovery model and diagnostics are proven.

### JSON Source Generation

`System.Text.Json` source generation should include every discovered block model and the common collection shapes used by the editor and preview APIs.

Generated example:

```csharp
// Auto-generated by Aero.Cms.SourceGenerators
#nullable enable
using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Blocks.Serialization;

[JsonSerializable(typeof(BlockBase))]
[JsonSerializable(typeof(AeroHeroBlock))]
[JsonSerializable(typeof(List<AeroHeroBlock>))]
[JsonSerializable(typeof(RichTextBlock))]
[JsonSerializable(typeof(List<RichTextBlock>))]
internal partial class BlockJsonContext : JsonSerializerContext
{
}
```

The app should prefer serializer overloads that take generated `JsonTypeInfo<T>` or a registered `JsonSerializerContext`. Reflection fallback should be treated as a development smell for block payloads.

### Marten Subclass Configuration

The generator should also emit a generated helper for Marten subclass mapping:

```csharp
// Auto-generated by Aero.Cms.SourceGenerators
public static partial class GeneratedBlockMartenConfiguration
{
    public static Type[] BlockTypes { get; } =
    [
        typeof(AeroHeroBlock),
        typeof(RichTextBlock),
        typeof(NavigationBlock)
    ];
}
```

The hand-written Marten configuration can then consume that generated list instead of maintaining a separate manual list:

```csharp
opts.Schema.For<BlockBase>()
    .AddSubClassHierarchy(GeneratedBlockMartenConfiguration.BlockTypes);
```

### Polymorphic Serialization

`JsonDerivedType` declarations on `BlockBase` are another drift point. The generator should either:

* generate the full polymorphic configuration in a resolver/context supported by `System.Text.Json`, or
* emit diagnostics when `BlockMetadataAttribute`, `JsonDerivedType`, and Marten subclass mapping diverge.

The first implementation can keep the existing attributes and emit diagnostics. A later implementation can remove the manual list entirely if the generated resolver approach proves clean.

### Block Schema Versioning

Persisted block models will evolve. AeroCMS should track block schema versions independently of renderer versions so existing Marten documents can be migrated, tolerated, or rendered with compatibility logic.

The simplest shape is to extend block metadata and/or the base model with an integer schema version:

```csharp
[BlockMetadata("aero_hero", "Aero Hero", Category = "Aero", SchemaVersion = 2)]
public sealed class AeroHeroBlock : BlockBase
{
    public override string BlockType => "aero_hero";
    public int SchemaVersion { get; set; } = 2;

    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
}
```

Recommended rules:

* Additive nullable properties usually do not require immediate content migration.
* Renames, required-property changes, and shape changes should get an explicit migration.
* The generated manifest should expose the current schema version for editor and migration tooling.
* Preview and rendering should tolerate older schema versions until a migration has run.
* Block migrations should be idempotent and testable against saved JSON fixtures.

---

## 11. What Is Stored In The Database?

Render adapters are not stored in the database.

The database stores content and content definitions:

```text
PageDocument
LayoutRegion
LayoutColumn
BlockPlacement
BlockBase-derived block documents
```

For runtime user-defined blocks, the database may store dynamic definitions:

```text
DynamicBlockDefinition
JsonSchema
ScribanTemplate
TemplateVersion
AllowedFields
```

Those records describe dynamic content and templates. They do not replace compiled adapters. Instead, one compiled adapter renders all dynamic template-backed blocks through a generic dynamic renderer.

---

## 12. The Two-Tiered Rendering Strategy

Because Native AOT does not allow runtime C# compilation, AeroCMS should support two rendering tiers.

### Tier 1: Core & Developer Blocks (Compile-Time)

* **Implementation:** C# models, Razor components, source-generated descriptors, and source-generated adapters.
* **Use Case:** Standard CMS components such as heroes, grids, forms, navigation, pricing, blog summaries, and contact blocks.
* **Benefits:** Type-safe, compiled, testable, trim-friendly, and ready for future Native AOT hosting support.
* **Tradeoff:** Adding or changing a renderer requires a rebuild and deployment.

### Tier 2: User-Defined / Dynamic Blocks (Runtime via Scriban)

* **Implementation:** A generic `DynamicTemplateBlockRenderer.razor` powered by Scriban.
* **Use Case:** Users need to define new simple UI fragments in the CMS Admin panel without touching source code or triggering a deployment.
* **How it works:**
    1. The user defines a JSON schema for the block data.
    2. The user writes a Scriban/Liquid-compatible template such as `<h1>{{ block.title }}</h1>`.
    3. The database stores the dynamic block definition, template, schema, and version.
    4. A compiled `DynamicTemplateBlockRenderAdapter` routes dynamic blocks to `DynamicTemplateBlockRenderer`.
    5. The renderer maps approved JSON data into Scriban `ScriptObject` and `ScriptArray` values.
    6. Scriban renders the template against that approved script object graph.

Scriban can be used in an AOT-compatible way, but only if AeroCMS avoids reflection-based APIs. Do not pass arbitrary POCOs, anonymous objects, or EF/Marten entities directly into Scriban. Use explicit `ScriptObject` and `ScriptArray` mapping instead.

The dynamic tier must include:

* template parse/result caching.
* a strict allowlist of exposed functions and properties.
* template length, loop, recursion, regex, and render timeout limits.
* HTML sanitization with `Ganss.Xss.HtmlSanitizer` when templates produce markup that came from untrusted users.
* versioned templates so published content can continue rendering if a draft template changes.
* save-time parsing and validation so syntax errors are caught before publish.
* schema-variable validation so missing or renamed fields can be reported to editors.
* admin editor affordances that list available variables from the schema.
* no arbitrary JavaScript execution or persisted custom JavaScript blocks.

HTMX and Scriban are the dynamic-content escape valves. AeroCMS should not add a database-backed custom JavaScript block type. If script output is needed for analytics or integrations, use typed/provider-specific blocks that validate provider IDs and emit known server-authored snippets. `RawHtmlBlock` sanitization must strip `<script>` elements and event-handler attributes.

Recommended runtime options:

```csharp
public sealed record SecureScribanTemplateOptions
{
    public int MaxTemplateLengthBytes { get; init; } = 50_000;
    public int LoopLimit { get; init; } = 1_000;
    public int RecursiveLimit { get; init; } = 50;
    public bool StrictVariables { get; init; } = true;
    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan RenderTimeout { get; init; } = TimeSpan.FromSeconds(3);
    public int MaxInputDepth { get; init; } = 10;
}
```

The first implementation should wrap Scriban behind a `SecureScribanRenderer` abstraction:

```csharp
public interface ISecureScribanRenderer
{
    Task<Result<string>> RenderAsync(
        DynamicBlockDefinition definition,
        JsonDocument? data,
        CancellationToken cancellationToken = default);
}
```

That wrapper should:

* reject templates over `MaxTemplateLengthBytes`.
* parse and cache templates by definition id and version.
* configure `TemplateContext.LoopLimit`, `RecursiveLimit`, `StrictVariables`, and `RegexTimeOut`.
* expose only approved `ScriptObject` and `ScriptArray` data.
* import only an allowlisted function set.
* enforce render timeout through cancellation and execution isolation where possible.
* sanitize rendered HTML before returning it to a Blazor `MarkupString` path.

Full AST security analysis should be phased. The first dynamic-block milestone should use runtime limits, strict variables, explicit script objects, and a tiny allowlist. A later hardening milestone can add AST linting for banned functions and schema-variable validation. A complete AST policy should not block the first safe implementation unless the dynamic tier is exposed to untrusted public users.

Recommended dynamic definition model:

```csharp
public sealed class DynamicBlockDefinition : Entity
{
    public string Name { get; set; } = string.Empty;
    public string BlockType { get; set; } = string.Empty;
    public string ScribanTemplate { get; set; } = string.Empty;
    public JsonDocument? DataSchema { get; set; }
    public int Version { get; set; }
}

public sealed class DynamicTemplateBlock : BlockBase
{
    public override string BlockType => "dynamic_template";
    public long DefinitionId { get; set; }
    public int DefinitionVersion { get; set; }
    public JsonDocument? Data { get; set; }
}
```

Recommended validator:

```csharp
public sealed class DynamicTemplateValidator
{
    public Result<Unit> Validate(string template, JsonDocument? schema)
    {
        var parsed = Template.Parse(template);

        if (parsed.HasErrors)
        {
            return Result.Fail(parsed.Messages.Select(m => m.Message));
        }

        // Phase 2: inspect the template AST and compare referenced variables
        // against the approved JSON schema.
        return Result.Ok(Unit.Value);
    }
}
```

---

## 13. Supporting Markdown And Raw HTML Authoring Blocks

Markdown and Custom HTML are both known compiled block types, so they belong in Tier 1. They should use AeroCMS block models for persistence and Radzen components for authoring and rendering support.

### Markdown Block

AeroCMS should keep the existing `MarkdownBlock` model as the persisted content shape. Do not introduce a vendor-specific `RadzenMarkdownBlock`; the persisted model should remain CMS-domain language, while the renderer can use Radzen.

```csharp
[BlockMetadata("markdown", "Markdown Text", Category = "Text")]
public sealed class MarkdownBlock : BlockBase
{
    public override string BlockType => "markdown";
    public string Content { get; set; } = string.Empty;
}
```

Recommended renderer:

```razor
@* MarkdownBlockRenderer.razor *@
@attribute [CmsBlockRenderer(typeof(MarkdownBlock))]

@if (!string.IsNullOrWhiteSpace(Block.Content))
{
    <RadzenMarkdown Text="@Block.Content"
                    AllowHtml="false"
                    AutoLinkHeadingDepth="3" />
}

@code {
    [Parameter]
    public MarkdownBlock Block { get; set; } = default!;
}
```

Use the `Text` parameter rather than child content for CMS-authored markdown. Radzen Markdown supports child content and can process Blazor tags placed inside the component, which is useful for developer-authored documentation but not appropriate for user-authored CMS content.

`AllowHtml` should default to `false` for regular editors. If AeroCMS later supports trusted Markdown-with-HTML, expose that as an explicit block option and sanitize any allowed HTML before it reaches `MarkupString` or Radzen's HTML rendering path.

### Custom HTML Block

A "Custom HTML" block also fits Tier 1 when it is a known compiled block type. It requires no complex templating and can render through Blazor's built-in `MarkupString`, but only after sanitization or an explicit trusted-content decision.

### The Model

```csharp
[BlockMetadata("raw_html", "Raw HTML", Category = "Advanced")]
public sealed class RawHtmlBlock : BlockBase
{
    public override string BlockType => "raw_html";
    public string Content { get; set; } = string.Empty;
}
```

### The Editor

The page editor should use `RadzenHtmlEditor` for `RawHtmlBlock.Content` instead of a plain textarea. The default Radzen toolbar includes many tools, including source editing and image insertion, so AeroCMS should provide a constrained toolbar based on role and block policy.

```razor
<RadzenHtmlEditor @bind-Value="block.Content"
                  UploadUrl="@HtmlEditorUploadUrl"
                  Paste="@SanitizePastedHtml"
                  Style="height: 320px">
    <RadzenHtmlEditorUndo />
    <RadzenHtmlEditorRedo />
    <RadzenHtmlEditorSeparator />
    <RadzenHtmlEditorBold />
    <RadzenHtmlEditorItalic />
    <RadzenHtmlEditorUnderline />
    <RadzenHtmlEditorSeparator />
    <RadzenHtmlEditorUnorderedList />
    <RadzenHtmlEditorOrderedList />
    <RadzenHtmlEditorSeparator />
    <RadzenHtmlEditorLink />
    <RadzenHtmlEditorUnlink />
    <RadzenHtmlEditorImage />
    @if (CanEditSource)
    {
        <RadzenHtmlEditorSeparator />
        <RadzenHtmlEditorSource />
    }
</RadzenHtmlEditor>
```

The source editor should be admin/developer only. For regular content editors, prefer a curated WYSIWYG toolbar and save-time sanitization.

### Image Uploads

Radzen HtmlEditor can insert pasted or uploaded images. Without `UploadUrl`, images can be inserted as base64 data URLs, which is a poor fit for CMS content because it bloats block JSON and bypasses the media library.

AeroCMS already has `MediaApi` at:

```text
src/Aero.Cms.Modules.Headless/Areas/Api/v1/MediaApi.cs
```

The existing media endpoint stores files under `/media/{FileName}` and returns media metadata including the public URL. The HtmlEditor integration should reuse this media pipeline, but Radzen's upload path posts `multipart/form-data` with a `file` field and expects a JSON response containing a `url` property.

Recommended approach:

* Add a Radzen-compatible media upload endpoint or overload under the existing Media API area.
* Accept `IFormFile file`.
* Validate content type, extension, size, and filename.
* Store the file through the same media asset pipeline used by `MediaApi`.
* Return `{ "url": media.Url }` so Radzen can insert `<img src="...">`.
* Keep `UploadUrl` pointed at this endpoint.
* Do not allow base64 image insertion for persisted CMS HTML except as a temporary fallback during local development.

Recommended constraints:

```csharp
public sealed record MediaUploadConstraints
{
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;
    public int MaxImageWidth { get; init; } = 4096;
    public int MaxImageHeight { get; init; } = 4096;

    public IReadOnlySet<string> AllowedMimeTypes { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif"
        };

    public IReadOnlySet<string> AllowedExtensions { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".gif"
        };
}
```

Avoid SVG upload through the HtmlEditor path unless AeroCMS adds a dedicated SVG sanitizer and a clear CSP policy. SVG can carry scriptable content and should not ride through the same path as raster images.

The upload endpoint should normalize filenames, generate collision-resistant storage names, reject path traversal attempts, inspect image dimensions server-side, and include a future virus-scanning hook for production deployments.

Conceptual endpoint shape:

```csharp
group.MapPost("/html-editor-upload", async (
    IFormFile file,
    IDocumentSession session,
    IWebHostEnvironment env,
    CancellationToken cancellationToken) =>
{
    // Validate file type, extension, length, and normalized file name.
    // Store through the same media asset workflow as UploadMediaRequest.
    // Return the public media URL expected by RadzenHtmlEditor.
    return TypedResults.Ok(new { url = media.Url });
});
```

### HTML Sanitization

Use `Ganss.Xss.HtmlSanitizer` from `mganss/HtmlSanitizer` as the explicit sanitizer for Custom HTML, trusted Markdown-with-HTML, and dynamic Scriban output that includes user-controlled markup.

`HtmlSanitizer` is allowlist-based and can configure allowed tags, attributes, CSS properties, at-rules, URI schemes, and URI-bearing attributes. Its `Sanitize()` and `SanitizeDocument()` methods are thread-safe after the sanitizer instance is configured, so AeroCMS can register a preconfigured sanitizer policy once and reuse it.

Recommended service boundary:

```csharp
using Ganss.Xss;

public interface ICmsHtmlSanitizer
{
    string Sanitize(string html, Uri? baseUri = null);
}

public sealed class CmsHtmlSanitizer : ICmsHtmlSanitizer
{
    private readonly HtmlSanitizer sanitizer;

    public CmsHtmlSanitizer()
    {
        sanitizer = new HtmlSanitizer();

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("mailto");

        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedAttributes.Add("target");
        sanitizer.AllowedAttributes.Add("rel");
    }

    public string Sanitize(string html, Uri? baseUri = null)
    {
        return baseUri is null
            ? sanitizer.Sanitize(html)
            : sanitizer.Sanitize(html, baseUri.ToString());
    }
}
```

The final allowlist should be product-specific rather than simply accepting all HtmlSanitizer defaults. In particular:

* Keep `script`, `iframe`, `object`, and event-handler attributes out of the allowed policy.
* Decide deliberately whether `style` attributes are allowed. They increase authoring flexibility but complicate visual consistency and CSP.
* Allow `img[src]` only for trusted schemes and preferably same-site `/media/...` URLs.
* Add `rel="noopener noreferrer"` when links open in a new tab.
* Sanitize at save time and again at render time unless the saved value is explicitly stored as a sanitized canonical value with a sanitizer policy version.

### The Renderer

```razor
@* RawHtmlRenderer.razor *@
@attribute [CmsBlockRenderer(typeof(RawHtmlBlock))]

@if (!string.IsNullOrWhiteSpace(Block.Content))
{
    @* MarkupString bypasses HTML encoding. Only render sanitized or trusted HTML. *@
    @((MarkupString)SanitizedContent)
}

@code {
    [Parameter]
    public RawHtmlBlock Block { get; set; } = default!;

    [Inject]
    public ICmsHtmlSanitizer HtmlSanitizer { get; set; } = default!;

    private string SanitizedContent => HtmlSanitizer.Sanitize(Block.Content);
}
```

**Security note:** Outputting raw HTML introduces significant XSS risk. This block should be restricted to Administrator/Developer roles, and `Content` must be passed through the configured sanitizer before rendering. `MarkupString` does not make raw HTML secure; it tells Blazor not to encode it.

---

## 14. Render Error Handling And Observability

Block rendering should fail locally, not take down the entire page or Blazor circuit. Use narrowly scoped Blazor error boundaries around rendered blocks wherever practical.

Conceptual dispatcher shape:

```razor
<ErrorBoundary>
    <ChildContent>
        @adapter.Render(Block, Context)
    </ChildContent>
    <ErrorContent>
        <div class="cms-block-error" role="status">
            Content temporarily unavailable.
        </div>
    </ErrorContent>
</ErrorBoundary>
```

Error UI should not expose exception messages to public users. Render failures should be logged with block type, model type, content id, page/post id when available, and preview/live context.

Do not put telemetry, caching, or diagnostics directly on `ICmsBlockRenderAdapter` in the first implementation. Keep that contract small. Add cross-cutting behavior later through wrappers, decorators, `CmsBlockHtmlRenderer`, or the page-level rendering pipeline. This keeps the adapter abstraction focused on adapting `IBlock` to a concrete component.

Output caching is a later optimization. When it is added, cache at the page/fragment/rendering-service layer with cache keys derived from content id, block id, schema version, template version, culture, preview/live mode, and relevant HTMX context. Avoid making every adapter own cache policy until there is a measured performance need.

---

## 15. The Live Preview Architecture

To provide a high-fidelity preview experience without duplicating rendering logic, the preview should use the same generated adapters as the live site.

Preview API ownership should live in the existing API module:

```text
src/Aero.Cms.Modules.Headless/Areas/Api/v1/PreviewApi.cs
```

`PreviewApi` already owns saved draft preview endpoints for pages and blog posts:

```text
GET /{HttpConstants.ApiPrefix}admin/preview/pages/{id:long}
GET /{HttpConstants.ApiPrefix}admin/preview/blog-posts/{id:long}
```

Those endpoints currently return a `PreviewResponse<T>` containing the draft document and content type. The next preview work should extend this same API area rather than introducing a second preview controller or endpoint group.

The preview architecture must account for Blazor render mode behavior:

* If the preview is rendered with static SSR, it cannot update through `DotNet.invokeMethodAsync` and `StateHasChanged()` because there is no live interactive component instance.
* If the preview needs as-you-type updates, the iframe must host an interactive Blazor component or use server-rendered fragment refreshes.

### Option A: Interactive IFrame Preview

Use an iframe that hosts an authenticated interactive preview component. The iframe can bootstrap from `PreviewApi` for saved page/blog-post drafts, then receive unsaved editor state through `postMessage`. The preview component updates its in-memory draft state and re-renders through the generated adapters.

Security requirements:

* Use an exact `targetOrigin`, not `"*"`.
* Validate `event.origin` inside the iframe.
* Avoid long-lived JWTs in query strings.
* Prefer same-site authentication plus a short-lived preview session id.
* Validate and deserialize preview JSON through source-generated `System.Text.Json` metadata.
* Debounce whole-page preview messages, typically 200-300ms.

Sender:

```javascript
const iframe = document.getElementById('preview-frame');
const targetOrigin = window.location.origin;

const sendPreviewUpdate = debounce(() => {
    iframe.contentWindow.postMessage({
        type: 'AERO_UPDATE_PREVIEW',
        payload: currentPageBlocks
    }, targetOrigin);
}, 300);
```

Receiver:

```javascript
window.addEventListener('message', (event) => {
    if (event.origin !== window.location.origin) {
        return;
    }

    if (event.data?.type === 'AERO_UPDATE_PREVIEW') {
        DotNet.invokeMethodAsync(
            'Aero.Cms.Modules.Pages',
            'UpdatePreviewState',
            event.data.payload);
    }
});
```

### Option B: Static SSR Fragment Preview

If AeroCMS wants to avoid an interactive preview island, use debounced server preview updates instead:

1. The editor posts draft JSON to a secured `PreviewApi` endpoint.
2. The endpoint validates and deserializes the draft through generated `System.Text.Json` metadata.
3. The server renders the same layout and block adapters.
4. The iframe or preview panel refreshes the returned HTML fragment.

This option is less instant than an interactive iframe, but it is simpler to secure and works naturally with static SSR.

Recommended endpoint additions:

```text
POST /{HttpConstants.ApiPrefix}admin/preview/pages/render-fragment
POST /{HttpConstants.ApiPrefix}admin/preview/blog-posts/render-fragment
POST /{HttpConstants.ApiPrefix}admin/preview/blocks/render-fragment
```

The saved-content `GET` endpoints should remain useful for opening a preview from persisted draft state. The new `POST` endpoints are for unsaved editor state and should return rendered HTML fragments generated through `CmsBlockHtmlRenderer` / `HtmlRenderer`, not a separate preview-only renderer.

### Option C: Inline Single-Block Preview

For keystroke-heavy editing of a single block, the fastest preview is often not the iframe. Render the current block preview in the same Blazor component tree as the editor form, using the same generated adapter registry. Use the iframe for full-page layout fidelity and cross-document CSS behavior.

---

## 16. Phased Implementation Plan

### Phase 0: Inventory & Safety Baseline

Phase 0 is a discovery and safety phase. It should not change rendering behavior. Its purpose is to make Phase 1 small, reversible, and measurable.

#### Phase 0A: Current-State Inventory

Create a short implementation inventory that lists:

* every block model with `BlockMetadataAttribute`.
* every `BlockBase` / `IBlock` subtype that does not have `BlockMetadataAttribute`.
* every renderer component that currently participates in block rendering.
* every block type handled by `BlockRenderer.razor`.
* every `JsonDerivedType` registration on `BlockBase`.
* every block type registered in `BlockJsonContext`.
* every block type registered in Marten subclass configuration.
* every use of `BlockSliceRegistry`, `IBlockSliceRenderer`, and `IBlockVisitor`.
* every preview path that renders blocks, including `PreviewApi` consumers.
* every editor path that creates or edits blocks.

The inventory should explicitly call out drift. For example, if `MarkdownBlock` exists in JSON metadata but is missing from Marten subclass configuration, record that as a Phase 0 finding rather than silently fixing it during discovery.

#### Phase 0B: Baseline Tests

Add enough tests or snapshots to make the current rendering behavior observable before replacing dispatch:

* one or two representative simple blocks.
* one block that requires extra render context, such as navigation.
* one unknown block fallback case.
* one raw HTML / sanitized HTML case if the sanitizer is already present.
* one saved draft preview path through `PreviewApi` if practical.

These tests do not need to cover every block before Phase 1. They need to protect the representative behaviors that the generated adapter pipeline will replace first.

#### Phase 0C: Source Generator Proof Boundary

Choose the first proof-of-concept block set before writing the generator. Recommended first set:

```text
MarkdownBlock       -> simple one-parameter renderer
RawHtmlBlock        -> renderer with sanitizer dependency or sanitizer path
NavigationBlock     -> renderer with BlockRenderContext.Navigation
```

This gives the generator three useful shapes without trying to solve every block on the first pass.

#### Phase 0D: ADR And Decision Record

Add an ADR explaining why AeroCMS is choosing source-generated adapters over runtime reflection and assembly scanning.

The ADR should capture:

* the current manual-dispatch problem.
* why runtime reflection/scanning is not preferred.
* why generated adapters are better for trim-safety and future Native AOT readiness.
* why the first milestone is adapter generation only.
* why JSON/Marten generation is phased after the renderer proof.
* why arbitrary custom JavaScript blocks are excluded.

#### Phase 0E: Definition Of Done

Phase 0 is complete when:

* the inventory exists and identifies current drift points.
* representative baseline tests or snapshots exist.
* the first proof-of-concept block set is chosen.
* the source-generator project location and target assembly are agreed.
* the ADR is written.
* Phase 1 can begin without making additional architectural decisions.

### Phase 1: Generated Render Adapters

* Add `CmsBlockRendererAttribute`, `ICmsBlockRenderAdapter`, `BlockRenderContext`, and the generated registry shape.
* Change renderer attributes to `@attribute [CmsBlockRenderer(typeof(MyBlock))]`.
* Generate render adapters from renderer components and model metadata.
* Replace the `BlockRenderer.razor` switch with `CmsBlockRenderRegistry.TryGet(...)`.
* Emit diagnostics for duplicate models, duplicate discriminators, missing `Block` parameters, and renderer/model mismatch.
* Use literal `RenderTreeBuilder` sequence numbers in generated adapters and add snapshot tests for representative generated output.
* Add generator debugging guidance for development builds, such as emitting generated source to disk or documenting where generated files are available under `obj`.
* Add narrowly scoped block error boundaries around adapter-rendered content.

### Phase 2: Single Source Of Truth For Block Registration

* Generate the richer `CmsBlockManifest`.
* Feed editor palette metadata from the generated manifest.
* Generate or assist `System.Text.Json` source-generation registration for all block models.
* Generate or assist Marten subclass registration for all block models.
* Emit diagnostics when `BlockMetadataAttribute`, `BlockType`, `JsonDerivedType`, and Marten mapping drift.
* Add schema-version metadata to the manifest and define the first migration policy for persisted block documents.
* This phase may be pulled into Phase 1 if the team prefers one larger generator milestone over a smaller adapter-first rollout.

### Phase 3: Server-Side Rendering Path Convergence

* Decide whether `BlockSliceRegistry` can be removed.
* Prefer deprecating `IBlockSliceRenderer` and `IBlockVisitor`.
* If a bridge is required, implement `CmsBlockHtmlRenderer` with `HtmlRenderer`.
* Keep any bridge temporary and route it through the generated adapter registry.
* Add a compatibility bridge example or implementation that proves legacy slice rendering delegates to the generated Blazor path.
* Track hidden dependencies and define a removal point for the legacy slice abstractions.

### Phase 4: Dynamic Scriban Tier

* Add `DynamicBlockDefinition` and `DynamicTemplateBlock`.
* Add save-time template parsing and validation.
* Convert JSON data to Scriban `ScriptObject` and `ScriptArray` explicitly.
* Add `SecureScribanTemplateOptions`, template caching, render limits, allowed function lists, and `Ganss.Xss.HtmlSanitizer` policy.
* Configure Scriban `TemplateContext` runtime safety controls: `LoopLimit`, `RecursiveLimit`, `StrictVariables`, and `RegexTimeOut`.
* Keep AST validation phased: start with parse diagnostics and obvious banned constructs, then add schema-variable analysis later.
* Explicitly reject arbitrary JavaScript/script blocks; use HTMX, Scriban templates, or typed provider blocks instead.
* Add admin tooling that exposes schema variables to template authors.

### Phase 5: Radzen Markdown, HtmlEditor, Media Uploads, And Sanitization

* Keep `MarkdownBlock` and `RawHtmlBlock` as vendor-neutral persisted models.
* Render Markdown blocks with `RadzenMarkdown Text=...`, not child content.
* Default Markdown HTML pass-through to disabled for regular editors.
* Replace the Custom HTML textarea editor with `RadzenHtmlEditor`.
* Add a constrained HtmlEditor toolbar, with source editing limited to admin/developer roles.
* Add a Radzen-compatible media upload endpoint under the existing Media API area that accepts `IFormFile file`, stores through the media asset pipeline, and returns `{ "url": media.Url }`.
* Enforce media upload constraints for size, MIME type, extension, filename normalization, image dimensions, and future virus scanning.
* Do not allow SVG in the HtmlEditor upload path without a dedicated SVG sanitizer and CSP review.
* Configure `RadzenHtmlEditor.UploadUrl` to use that endpoint so pasted/uploaded images become media-library URLs instead of base64 block content.
* Add `Ganss.Xss.HtmlSanitizer` as the CMS HTML sanitization dependency.
* Register a preconfigured `ICmsHtmlSanitizer` policy and use it for `RawHtmlBlock`, trusted Markdown-with-HTML, and dynamic Scriban output.
* Add tests for script removal, unsafe URL removal, allowed image URLs, link behavior, and render-time sanitized output.

### Phase 6: Preview Hardening

* Keep preview API ownership in `PreviewApi`.
* Preserve the existing saved draft `GET` preview endpoints for pages and blog posts.
* Add `PreviewApi` `POST` endpoints for unsaved page, blog-post, and single-block fragment rendering.
* Add whole-page iframe preview with strict origin validation.
* Add 200-300ms debounce for whole-page updates.
* Add inline single-block preview for keystroke-heavy editing.
* Route all preview rendering through the generated adapter registry.
