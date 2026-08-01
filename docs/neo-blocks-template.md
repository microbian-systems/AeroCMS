# Neo Block Template — 6-File + Registration Pattern

> **Based on:** `Hero01Block` (`src/Aero.Cms.Ui.Neo/Blocks/Hero/`)
> **Example used:** `CenteredHeroBlock` (Phase 1, block #1 — `neo.hero.centered`)
>
> Use this template for every one of the 33 new Neo blocks in Phases 1-6.

---

## Block Registration Overview

Every Neo block requires these **8 touch points**:

| # | File | Purpose |
|---|------|---------|
| 1 | `[BlockName]Block.cs` | Block model + `[BlockMetadata]` |
| 2 | `[BlockName]BlockMapper.cs` | Bidirectional serialization ↔ `NeoPageNode` |
| 3 | `[BlockName]BlockRenderer.razor` | Public page renderer (NeoUI components) |
| 4 | `[BlockName]EditorBlockDefinition.cs` | `IPageEditorBlockDefinition` implementation |
| 5 | `[BlockName]BlockEditor.razor` | Property editor panel |
| 6 | `[BlockName]BlockEditorPreview.razor` | Canvas thumbnail preview |
| 7 | `…/Blocks/RendererMarkers.cs` | `[CmsBlockRenderer]` static marker |
| 8 | `…/NeoPageEditorBlockProvider.cs` | DI registration |

Files 1-6 go in `src/Aero.Cms.Ui.Neo/Blocks/{Category}/{Name}/`.

---

## Folder Layout

```
src/Aero.Cms.Ui.Neo/Blocks/
├── RendererMarkers.cs                       ← ALL [CmsBlockRenderer] markers
├── Hero/                                    ← Hero01Block (existing)
│   ├── Hero01Block.cs
│   ├── Hero01BlockMapper.cs
│   ├── Hero01BlockRenderer.razor
│   ├── Hero01EditorBlockDefinition.cs
│   ├── Hero01BlockEditor.razor
│   └── Hero01BlockEditorPreview.razor
├── CenteredHero/                            ← Phase 1 example
│   ├── CenteredHeroBlock.cs
│   ├── CenteredHeroBlockMapper.cs
│   ├── CenteredHeroBlockRenderer.razor
│   ├── CenteredHeroEditorBlockDefinition.cs
│   ├── CenteredHeroBlockEditor.razor
│   └── CenteredHeroBlockEditorPreview.razor
├── SplitHero/                               ← Phase 1
├── CtaBanner/                               ← Phase 1
└── ...                                      ← Phases 2-6
```

---

## File 1: Block Model (`CenteredHeroBlock.cs`)

```csharp
using Aero.Cms.Abstractions.Blocks;
using Microsoft.AspNetCore.Html;

namespace Aero.Cms.Ui.Neo.Blocks.CenteredHero;

[BlockMetadata(
    "neo.hero.centered",        // CatalogId — unique type discriminator
    "Centered Hero",            // Display Name
    Category = "Neo",           // Routes to Neo palette section via source generator
    Icon = "sparkles",          // Lucide icon name
    SortOrder = 10,
    SchemaVersion = 1)]
public sealed class CenteredHeroBlock : BlockBase
{
    public const string BlockTypeId = "neo.hero.centered";
    public override string BlockType => BlockTypeId;

    // Properties matched to NeoUI component parameters.
    // Each property gets auto-discovered by the source generator via [BlockMetadata].
    public string Eyebrow { get; set; } = "Introducing NeoUI v3";
    public string Title { get; set; } = "Build beautiful Blazor apps";
    public string Highlight { get; set; } = "faster than ever";
    public string Description { get; set; } =
        "100+ production-ready components for .NET Blazor. Accessible, customizable, and built for speed.";
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

**Rules:**
- BlockTypeId constant MUST match `[BlockMetadata]` first arg
- Namespace: `Aero.Cms.Ui.Neo.Blocks.{FolderName}`
- Properties have default values (shown in fresh editor blocks)
- No `NgsqlTsVector SearchVector` (HyperUI blocks have this, Neo blocks don't need it)
- `Accept(IBlockVisitor)` is required by `BlockBase`

> **Note on SearchVector:** Neo blocks use `PublicStaticSsrSafe = false` — the search vector indexing path in `BlockSearchProjection` checks `SafeForStaticSsr` and skips blocks with it set to false, so no NgsqlTsVector is needed on the block model.

---

## File 2: Mapper (`CenteredHeroBlockMapper.cs`)

Serializes block properties to/from `NeoPageNode.Properties` (`Dictionary<string, JsonElement>`).

```csharp
using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.CenteredHero;

public static class CenteredHeroBlockMapper
{
    public static NeoPageNode ToNode(CenteredHeroBlock block) => new()
    {
        NodeId = string.Empty,
        CatalogId = CenteredHeroBlock.BlockTypeId,
        Kind = NeoPageNodeKind.Block,
        Properties = new Dictionary<string, JsonElement>
        {
            ["eyebrow"]        = JsonSerializer.SerializeToElement(block.Eyebrow),
            ["title"]          = JsonSerializer.SerializeToElement(block.Title),
            ["highlight"]      = JsonSerializer.SerializeToElement(block.Highlight),
            ["description"]    = JsonSerializer.SerializeToElement(block.Description),
            ["primaryText"]    = JsonSerializer.SerializeToElement(block.PrimaryText),
            ["primaryUrl"]     = JsonSerializer.SerializeToElement(block.PrimaryUrl),
            ["secondaryText"]  = JsonSerializer.SerializeToElement(block.SecondaryText),
            ["secondaryUrl"]   = JsonSerializer.SerializeToElement(block.SecondaryUrl),
            ["trustMarkers"]   = JsonSerializer.SerializeToElement(block.TrustMarkers),
        }
    };

    public static CenteredHeroBlock FromNode(NeoPageNode node) => new()
    {
        Eyebrow       = GetString(node, "eyebrow",       "Introducing NeoUI v3"),
        Title         = GetString(node, "title",         "Build beautiful Blazor apps"),
        Highlight     = GetString(node, "highlight",      "faster than ever"),
        Description   = GetString(node, "description",   string.Empty),
        PrimaryText   = GetString(node, "primaryText",   "Get started for free"),
        PrimaryUrl    = GetString(node, "primaryUrl",    "#"),
        SecondaryText = GetString(node, "secondaryText", "View on GitHub"),
        SecondaryUrl  = GetString(node, "secondaryUrl",  "#"),
        TrustMarkers  = node.Properties.TryGetValue("trustMarkers", out var element)
            && element.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<string>>(element.GetRawText()) ?? []
                : [],
    };

    private static string GetString(NeoPageNode node, string key, string fallback) =>
        node.Properties.TryGetValue(key, out var value)
            ? value.GetString() ?? fallback
            : fallback;
}
```

**Rules:**
- Property keys in `Properties` dictionary use **camelCase**
- Default values in `FromNode` MUST match block model defaults
- String properties use `GetString()` helper
- List/collection properties use `Deserialize<List<T>>` with null-coalesce
- `CatalogId` and `NodeId` are always set

---

## File 3: Renderer (`CenteredHeroBlockRenderer.razor`)

Public-facing renderer. **Uses actual NeoUI Blazor components** (unlike Hero01 which uses raw HTML).

```razor
@* CenteredHeroBlockRenderer - Public page renderer *@

@if (Block != null)
{
    <section class="@container flex flex-col items-center justify-center min-h-80 w-full bg-background px-6 py-16 text-center">
        <Badge Variant="BadgeVariant.Secondary" Class="mb-4 gap-1.5">
            <LucideIcon Name="sparkles" Size="12" Class="text-primary" />
            @Block.Eyebrow
        </Badge>

        <h1 class="text-4xl md:text-5xl font-bold tracking-tight text-foreground max-w-3xl leading-tight mb-4">
            @Block.Title<br class="hidden md:block" />
            <span class="text-primary">@Block.Highlight</span>
        </h1>

        <p class="text-lg text-muted-foreground max-w-xl leading-relaxed mb-8">
            @Block.Description
        </p>

        <div class="flex flex-col md:flex-row items-center gap-3">
            <Button Size="ButtonSize.Large" Class="gap-2 px-6">
                @Block.PrimaryText
                <LucideIcon Name="arrow-right" Size="16" />
            </Button>
            <Button Variant="ButtonVariant.Outline" Size="ButtonSize.Large" Class="gap-2 px-6">
                <LucideIcon Name="github" Size="16" />
                @Block.SecondaryText
            </Button>
        </div>

        <div class="mt-12 flex items-center gap-6 text-sm text-muted-foreground flex-wrap justify-center">
            @foreach (var marker in Block.TrustMarkers)
            {
                <div class="flex items-center gap-1.5">
                    <LucideIcon Name="circle-check" Size="14" Class="text-primary" />
                    @marker
                </div>
            }
        </div>
    </section>
}

@code {
    [Parameter, EditorRequired]
    public CenteredHeroBlock? Block { get; set; }
}
```

**Rules:**
- Uses NeoUI Blazor components (`<Badge>`, `<Button>`, `<LucideIcon>`, etc.)
- `[Parameter, EditorRequired]` on the block property — name MUST be `Block` (source generator expects this)
- Null-guard with `@if (Block != null)`
- Tailwind utility classes for layout (container queries via `@container`, responsive via `@md:`)
- No `@page` directive — non-routable component

> **⚠️ JS interop guard:** If your block's NeoUI components use JS interop (animations, charts, rich editors), wrap JS-dependent code with `@if (RendererInfo.IsInteractive)` or defer to `OnAfterRenderAsync(firstRender: true)`.

---

## File 4: Editor Block Definition (`CenteredHeroEditorBlockDefinition.cs`)

Implements `IPageEditorBlockDefinition` for the runtime provider.

```csharp
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Neo.Blocks.CenteredHero;

public sealed class CenteredHeroEditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => CenteredHeroBlock.BlockTypeId;

    public string DisplayName => "Centered Hero";

    public string? Description => "A NeoUI centered hero section with eyebrow, title, highlight, dual CTAs, and trust markers.";

    public string Category => "Neo";

    public string Kind => "Block";

    public string IconName => "sparkles";

    public int SortOrder => 10;

    public bool PublicStaticSsrSafe => false;

    public Type? PreviewComponentType => typeof(CenteredHeroBlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(CenteredHeroBlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type          = CatalogId,
            Eyebrow       = "Introducing NeoUI v3",
            MainText      = "Build beautiful Blazor apps",
            Highlight     = "faster than ever",
            SubText       = "100+ production-ready components for .NET Blazor. Accessible, customizable, and built for speed.",
            CtaText       = "Get started for free",
            CtaUrl        = "#",
            CtaText2      = "View on GitHub",
            CtaUrl2       = "#",
            TrustMarkers  = ["Free & open source", ".NET 8+ compatible", "Dark mode included", "100+ components"],
            BackgroundImage = string.Empty
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToBlock(editorBlock);
        return CenteredHeroBlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToBlock(editorBlock);

    private static CenteredHeroBlock ToBlock(EditorBlock editor) => new()
    {
        Eyebrow       = FirstNonEmpty(editor.Eyebrow,       "Introducing NeoUI v3"),
        Title         = FirstNonEmpty(editor.MainText,      "Build beautiful Blazor apps"),
        Highlight     = FirstNonEmpty(editor.Highlight,     "faster than ever"),
        Description   = FirstNonEmpty(editor.SubText,       string.Empty),
        PrimaryText   = FirstNonEmpty(editor.CtaText,       "Get started for free"),
        PrimaryUrl    = FirstNonEmpty(editor.CtaUrl,        "#"),
        SecondaryText = FirstNonEmpty(editor.CtaText2,      "View on GitHub"),
        SecondaryUrl  = FirstNonEmpty(editor.CtaUrl2,       "#"),
        TrustMarkers  = editor.TrustMarkers.Count > 0
            ? editor.TrustMarkers
            : [],                                              // Block model defaults handle empty-list fallback
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
```

**Rules:**
- `PublicStaticSsrSafe` is `false` for Neo blocks (uses `ServerPrerendered`)
- `PreviewComponentType` / `PropertyEditorComponentType` point to `.razor` types
- `CreateDefaultEditorBlock()` sets default values matching the block model
- `ToBlock()` helper maps from `EditorBlock` flat bag to typed block model
- Deliberately uses `EditorBlock` flat properties (MainText, SubText, CtaText, etc.) — this is the established convention for editor ↔ block mapping

---

## File 5: Property Editor (`CenteredHeroBlockEditor.razor`)

The property panel shown in the page editor sidebar when a block is selected.

```razor
<div class="pe-property-panel">
    <div class="pe-property-group">
        <label class="pe-property-label">Eyebrow</label>
        <RadzenTextBox @bind-Value="Block.Eyebrow" Class="w-full" />
    </div>
    <div class="pe-property-group">
        <label class="pe-property-label">Title *</label>
        <RadzenTextBox @bind-Value="Block.Title" Class="w-full" />
    </div>
    <div class="pe-property-group">
        <label class="pe-property-label">Highlight</label>
        <RadzenTextBox @bind-Value="Block.Highlight" Class="w-full" />
    </div>
    <div class="pe-property-group">
        <label class="pe-property-label">Description</label>
        <RadzenTextArea @bind-Value="Block.Description" Class="w-full" Rows="3" />
    </div>
    <div class="pe-property-group">
        <label class="pe-property-label">Primary Button Text</label>
        <RadzenTextBox @bind-Value="Block.PrimaryText" Class="w-full" />
    </div>
    <div class="pe-property-group">
        <label class="pe-property-label">Primary Button URL</label>
        <RadzenTextBox @bind-Value="Block.PrimaryUrl" Class="w-full" />
    </div>
    <div class="pe-property-group">
        <label class="pe-property-label">Secondary Button Text</label>
        <RadzenTextBox @bind-Value="Block.SecondaryText" Class="w-full" />
    </div>
    <div class="pe-property-group">
        <label class="pe-property-label">Secondary Button URL</label>
        <RadzenTextBox @bind-Value="Block.SecondaryUrl" Class="w-full" />
    </div>
    <div class="pe-property-group">
        <label class="pe-property-label">Trust Markers (comma separated)</label>
        <RadzenTextArea @bind-Value="TrustMarkersText" Class="w-full" Rows="2" />
    </div>
</div>

@code {
    [Parameter, EditorRequired]
    public CenteredHeroBlock Block { get; set; } = default!;

    private string TrustMarkersText
    {
        get => string.Join(", ", Block.TrustMarkers);
        set => Block.TrustMarkers = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
```

**Rules:**
- Uses Radzen input components (`RadzenTextBox`, `RadzenTextArea`)
- Property panel CSS classes: `pe-property-panel`, `pe-property-group`, `pe-property-label`
- Complex types (lists, objects) use a computed string bridge property for Radzen binding
- Block parameter is non-null (`Block { get; set; } = default!;`)

---

## File 6: Editor Preview (`CenteredHeroBlockEditorPreview.razor`)

Canvas thumbnail. Renders the block in a scaled preview within the page editor canvas.

```razor
<div class="pe-neo-preview pe-neo-hero-preview">
    <CenteredHeroBlockRenderer Block="PreviewBlock" />
</div>

@code {
    [Parameter, EditorRequired]
    public NeoPageNode Node { get; set; } = default!;

    private CenteredHeroBlock PreviewBlock => CenteredHeroBlockMapper.FromNode(Node);
}
```

**Rules:**
- Wraps the same renderer used on public pages (`<CenteredHeroBlockRenderer Block="..." />`)
- Takes `NeoPageNode` as input, not the block model directly
- Uses `Mapper.FromNode(Node)` to deserialize from the node
- CSS class: `pe-neo-preview`

---

## Registration 1: Renderer Marker (`RendererMarkers.cs`)

Add to `src/Aero.Cms.Ui.Neo/Blocks/RendererMarkers.cs`:

```csharp
using Aero.Cms.Abstractions.Blocks.Rendering;

// Phase 1 — Hero & Marketing
namespace Aero.Cms.Ui.Neo.Blocks.CenteredHero
{
    [CmsBlockRenderer(typeof(CenteredHeroBlock))] public partial class CenteredHeroBlockRenderer;
}

namespace Aero.Cms.Ui.Neo.Blocks.SplitHero
{
    [CmsBlockRenderer(typeof(SplitHeroBlock))] public partial class SplitHeroBlockRenderer;
}
// ... continue for each block
```

**What this does:** The source generator's `ForAttributeWithMetadataName` pipeline discovers these markers and generates:
- `CmsBlockRenderRegistry` entries (runtime render adapter lookup)
- `GeneratedNeoEditorCatalog.g.cs` entries (catalog items with `PublicRendererComponentType`)

Each marker declares a **partial class** — the `.razor` file provides the implementation.

---

## Registration 2: Provider (`NeoPageEditorBlockProvider.cs`)

Add to `src/Aero.Cms.Ui.Neo/NeoPageEditorBlockProvider.cs`:

```csharp
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Ui.Neo.Blocks.CenteredHero;

namespace Aero.Cms.Ui.Neo;

public sealed class NeoPageEditorBlockProvider : IPageEditorBlockProvider, ICmsBlockModelProvider
{
    private static readonly IReadOnlyCollection<IPageEditorBlockDefinition> Definitions =
    [
        new Hero01EditorBlockDefinition(),
        new CenteredHeroEditorBlockDefinition(),  // + Phase 1
        new SplitHeroEditorBlockDefinition(),      // + Phase 1
        // ... add each block
    ];

    private static readonly IReadOnlyCollection<CmsBlockModelRegistration> BlockModels =
    [
        new(Hero01Block.BlockTypeId,          typeof(Hero01Block)),
        new(CenteredHeroBlock.BlockTypeId,    typeof(CenteredHeroBlock)),   // + Phase 1
        new(SplitHeroBlock.BlockTypeId,       typeof(SplitHeroBlock)),      // + Phase 1
        // ... add each block
    ];

    public IReadOnlyCollection<IPageEditorBlockDefinition> GetDefinitions() => Definitions;
    public IReadOnlyCollection<CmsBlockModelRegistration> GetBlockModels() => BlockModels;
}
```

**What this does:** Registers each block's `IPageEditorBlockDefinition` into `PageEditorBlockRegistry` at startup via `NeoUiServiceCollectionExtensions.AddAeroCmsNeoUiBlocks()`.

---

## Source Generator Fixes Required for 33 Blocks

Three changes needed in `src/Aero.Cms.SourceGenerators/BlockRendererGenerator.cs` plus one project reference update:

### Prerequisite: Add project reference

`Aero.Cms.Shared/Aero.Cms.Shared.csproj` **MUST** reference `Aero.Cms.Ui.Neo/Aero.Cms.Ui.Neo.csproj`. The source-generated `GeneratedNeoEditorCatalog.g.cs` is emitted into `Aero.Cms.Shared` and contains `typeof()` references to Neo editor component types. Without this reference, all 33 blocks fail to compile:

```xml
<ItemGroup>
    <ProjectReference Include="..\Aero.Cms.Ui.Neo\Aero.Cms.Ui.Neo.csproj" />
</ItemGroup>
```

### Fix 1: `MapCatalogSection()` — Add `"Neo"` (line 1086)

```csharp
_ => "AeroUi"
// becomes:
"Neo" => "Neo",
_ => "AeroUi"
```

### Fix 2: Category-to-Namespace mapping for editor types

Replace the hardcoded `AeroUi.Hero01` paths in `GetBlockModelCandidate()` (line 249) and `RenderNeoEditorCatalog()` (lines 1057-1063) with a `CategoryEditorNamespace` dictionary.

**Editor types for Neo blocks live in `Aero.Cms.Ui.Neo.Blocks.{FolderName}`** (not in `Aero.Cms.Shared`). The folder name is derived from the model type by stripping the `Block` suffix:

```csharp
private static readonly Dictionary<string, string> CategoryEditorNamespace = new(StringComparer.OrdinalIgnoreCase)
{
    ["Aero UI"] = "Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.",
    ["Neo"]     = "Aero.Cms.Ui.Neo.Blocks.",
};
```

Then in `RenderNeoEditorCatalog()`, replace the `null` assignments (lines 1062-1063) with:

```csharp
var modelShortName = rd.ModelTypeName.Substring(rd.ModelTypeName.LastIndexOf('.') + 1);
// Derive folder name: "CenteredHeroBlock" → "CenteredHero"
var folderName = modelShortName.EndsWith("Block", StringComparison.Ordinal)
    ? modelShortName.Substring(0, modelShortName.Length - "Block".Length)
    : modelShortName;
var ns = CategoryEditorNamespace.TryGetValue(rd.Category ?? "Aero UI", out var n)
    ? n
    : "Aero.Cms.Shared.Pages.Manager.PageEditor.AeroUi.";
sb.AppendLine($"            EditorPreviewComponentType = typeof({ns}{folderName}.{modelShortName}EditorPreview),");
sb.AppendLine($"            PropertyEditorComponentType = typeof({ns}{folderName}.{modelShortName}Editor),");
```

**These three changes make the source generator automatically handle all 33 blocks** — only the 6 files + 2 registrations per block are needed.

---

## Per-Block Checklist

When creating a new Neo block, verify:

- [ ] `[BlockMetadata]` first arg matches `BlockTypeId` constant
- [ ] Category is `"Neo"`
- [ ] Namespace is `Aero.Cms.Ui.Neo.Blocks.{FolderName}`
- [ ] Mapper property keys are camelCase
- [ ] Mapper default values match block model defaults
- [ ] Renderer uses NeoUI Blazor components (not raw HTML)
- [ ] Renderer `[Parameter]` is named `Block`
- [ ] Editor definition: `PublicStaticSsrSafe = false`
- [ ] Editor definition: `PreviewComponentType` / `PropertyEditorComponentType` set
- [ ] Editor definition: `CreateDefaultEditorBlock()` sets correct defaults
- [ ] Editor preview takes `NeoPageNode Node` as parameter
- [ ] Editor preview uses `Mapper.FromNode(Node)` for deserialization
- [ ] `[CmsBlockRenderer]` marker added to `RendererMarkers.cs`
- [ ] Definition + block model added to `NeoPageEditorBlockProvider.cs`
- [ ] `@using Aero.Cms.Ui.Neo.Blocks.{FolderName}` added to `src/Aero.Cms.Ui.Neo/_Imports.razor`

## Block Model Property Naming Convention

| Pattern | Field Type | Example |
|---------|-----------|---------|
| Simple text | `string` | `Title`, `Eyebrow`, `Highlight` |
| Long text | `string` (semantic name) | `Description`, `Summary`, `Content` |
| URL | `string` (ends with `Url`/`Uri`) | `PrimaryUrl`, `ImageUrl` |
| Boolean toggle | `bool` | `ShowForgotPassword`, `RememberMe` |
| Number | `int` / `double` | `PageSize`, `SortOrder`, `Rating` |
| String list | `List<string>` | `TrustMarkers`, `Features`, `Tags` |
| Complex list | `List<T>` (record type) | `List<ColumnDefinition>`, `List<NavItem>` |
