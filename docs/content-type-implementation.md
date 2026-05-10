# Content Type Implementation

## Runtime-defined content types without reflection for Aero CMS

---

## 1. Architecture Overview

The content type system bridges two worlds:

- **Developer-defined types**: C# entity classes + source generators → compile-time metadata
- **Runtime-defined types**: Manager UI → database schema → runtime metadata

Both converge on the same rendering pipeline.

> **📝 NOTE: ContentItem vs PageDocument — two models, one system**
>
> `PageDocument` and `ContentItem` are **not competing models**. They serve different
> purposes and work together:
>
> | | `PageDocument` | `ContentItem` |
> |---|---|---|
> | **Purpose** | A **page** — layout, navigation, SEO, header/footer | **Structured data** — follows a runtime-defined schema |
> | **Structure** | `LayoutRegions` → columns → block placements | `Dictionary<string, JsonElement>` field bag |
> | **Typical use** | Homepage, landing page, blog listing | "Team Member" profile, "Product" entry, "Case Study" |
> | **Has its own URL?** | Yes — resolved via slug | Optionally — if configured as standalone |
> | **Edits via** | Block editor (drag-drop layout) | Form editor (fields defined by ContentTypeDefinition) |
>
> **How they compose:** A `PageDocument` uses blocks as its content. One of those
> blocks — a `ContentEmbedBlock` — references a `ContentItem` by ID and renders
> it inline. The page owns the layout; the content item owns the structured data.
>
> ```text
> PageDocument "About Us"     ← has its own slug, layout, navigation
> └── LayoutRegions
>     ├── TextBlock: "Our mission is..."
>     ├── ContentEmbedBlock ─────→ ContentItem "Alice" (ContentType: "team-member")
>     │                                    ↑ schema defined at runtime
>     ├── ContentEmbedBlock ─────→ ContentItem "Bob" (ContentType: "team-member")
>     └── ImageBlock: "/team-photo.jpg"
> ```
>
> The existing `DynamicPageModel` (`Areas/Cms/Pages/Page.cshtml.cs`) never touches
> `ContentItem`. It continues to load `PageDocument` by slug and render it through
> `LayoutRegionRenderer`. ContentItems are added to pages via blocks — they are
> embedded data, not page replacements.

```
                        ┌─────────────────────────────┐
                        │   ContentTypeDefinition      │
                        │   (schema — DB or generated) │
                        └────────────┬────────────────┘
                                     │
                                     ▼
                        ┌─────────────────────────────┐
                        │      ContentItem             │
                        │   (field bag — JsonElement)  │
                        └────────────┬────────────────┘
                                     │
                         ┌───────────┴───────────┐
                         ▼                       ▼
            ┌────────────────────┐   ┌────────────────────┐
            │ DynamicBlockDef    │   │ DynamicBlockDef     │
            │ (auto-generated)   │   │ (custom Scriban)   │
            └────────┬───────────┘   └────────┬───────────┘
                     │                        │
                     ▼                        ▼
            ┌────────────────────┐   ┌────────────────────┐
            │ DynamicTemplateBlk │   │ DynamicTemplateBlk │
            │  .Data = Fields    │   │  .Data = Fields    │
            └────────┬───────────┘   └────────┬───────────┘
                     │                        │
                     └───────────┬────────────┘
                                 ▼
                    ┌────────────────────────┐
                    │ DynamicTemplateBlkRenderer │
                    │ → ISecureScribanRenderer    │
                    └────────────┬───────────┘
                                 ▼
                          HTML Output
```

**Alternative rendering path** (for complex layouts):

```
ContentItem.Fields
    │
    ├──→ BlockInstance (hero)       → IBlockRenderer → HTML
    ├──→ BlockInstance (rich-text)  → IBlockRenderer → HTML
    └──→ BlockInstance (cta)        → IBlockRenderer → HTML
```

---

## 2. Core Models

### 2.1 ContentTypeDefinition — the schema

```csharp
public sealed class ContentTypeDefinition : Entity
{
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Icon { get; set; }

    /// <summary>
    /// The fields that this content type defines.
    /// Used for admin UI rendering, validation, indexing, and Scriban template generation.
    /// </summary>
    public List<ContentFieldDefinition> Fields { get; set; } = [];

    /// <summary>
    /// Optional custom Scriban template. When null/empty, the system
    /// auto-generates one from Fields.
    /// </summary>
    public string? ScribanTemplate { get; set; }

    /// <summary>
    /// The rendering mode: as a single dynamic block, or as individual block instances.
    /// </summary>
    public ContentTypeRenderMode RenderMode { get; set; } = ContentTypeRenderMode.DynamicBlock;

    /// <summary>
    /// Optional scheduling configuration. When null, scheduling is not available
    /// for this content type.
    /// </summary>
    public ContentTypeScheduleConfig? ScheduleConfig { get; set; }
}

public enum ContentTypeRenderMode
{
    /// <summary>Renders the entire content type as one DynamicTemplateBlock</summary>
    DynamicBlock,
    /// <summary>Each field maps to a BlockInstance in the page layout</summary>
    BlockLayout
}
```

```csharp
public sealed class ContentFieldDefinition
{
    /// <summary>Field name used as the key in ContentItem.Fields</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Field type alias: "text", "richtext", "image", "url", "number", "date", "boolean", "media"</summary>
    public string FieldType { get; set; } = "text";

    /// <summary>Display label for the admin UI</summary>
    public string? Label { get; set; }

    public bool Required { get; set; }

    /// <summary>Default value when the field is empty</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Placeholder text for the admin UI editor</summary>
    public string? Placeholder { get; set; }

    /// <summary>Validation rules, editor hints, etc. consumed by FluentValidation + admin UI</summary>
    public Dictionary<string, object?> Settings { get; set; } = [];
}

/// <summary>
/// Scheduling configuration for ContentTypeDefinition.
/// When set, content items of this type can opt into scheduled publishing.
/// </summary>
public sealed record ContentTypeScheduleConfig
{
    public bool AllowScheduledPublish { get; init; }
    public bool AllowScheduledUnpublish { get; init; }
    public int? MaxPublishDelayDays { get; init; }
}
```

### 2.2 ContentItem — the field bag

```csharp
public sealed class ContentItem : Entity
{
    public long SiteId { get; set; }
    public string ContentTypeAlias { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Title { get; set; }

    /// <summary>
    /// Field values stored as JsonElement for AOT-safe serialization.
    /// Field renderers deserialize via source-generated JsonSerializerContext.
    /// </summary>
    public Dictionary<string, JsonElement> Fields { get; set; } = [];

    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    public DateTimeOffset? PublishedOn { get; set; }

    /// <summary>Monotonically incremented on each save. 0 = unsaved.</summary>
    public int VersionNumber { get; set; }

    /// <summary>If set, schedule this item for publishing at the given time.</summary>
    public DateTimeOffset? SchedulePublishUtc { get; set; }

    /// <summary>If set, schedule this item for unpublishing at the given time.</summary>
    public DateTimeOffset? ScheduleUnpublishUtc { get; set; }
}
```

**Why `JsonElement` over `object?`:**

| Concern | `Dictionary<string, object?>` | `Dictionary<string, JsonElement>` |
|---------|------|------|
| STJ round-trip fidelity | ❌ Numbers become `JsonElement`, strings lose type | ✅ `JsonElement` preserves the source token type |
| AOT-safe deserialization | ❌ Requires runtime polymorphic resolution | ✅ `element.Deserialize<T>(ctx.Options)` with source-generated context |
| Scriban integration | ❌ Extra conversion step needed | ✅ `JsonToScribanMapper` already converts `JsonElement` → `ScriptObject` |
| Linq/query in Marten | ⚠️ Fragile | ⚠️ Same — both require computed index workarounds |

**Field access helper:**

```csharp
public static class ContentItemExtensions
{
    public static T? Get<T>(this ContentItem item, string field)
    {
        if (!item.Fields.TryGetValue(field, out var element))
            return default;
        return JsonSerializer.Deserialize<T>(element.GetRawText(), AeroJsonContext.Default.Options);
    }

    public static T? Get<T>(this ContentItem item, string field, JsonSerializerContext context)
    {
        if (!item.Fields.TryGetValue(field, out var element))
            return default;
        return JsonSerializer.Deserialize(element.GetRawText(), typeof(T), context) is T value
            ? value
            : default;
    }
}
```

### 2.3 Block-level rendering model

For the `BlockLayout` render mode, each content field maps to a block instance in the page.

```csharp
public sealed class FieldBlockInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>The field name from ContentTypeDefinition.Fields</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>The component alias to render this field:
    /// Derived from ContentFieldDefinition.FieldType → ComponentDefinition mapping</summary>
    public string ComponentAlias { get; set; } = string.Empty;

    /// <summary>Optional overrides passed to the block renderer</summary>
    public Dictionary<string, JsonElement> Props { get; set; } = [];
}
```

### 2.4 ContentEmbedBlock — embedding ContentItems in pages

ContentItems are embedded inside PageDocument blocks, not rendered standalone.
`ContentEmbedBlock` is a block type in the existing `BlockBase` hierarchy that
references a ContentItem by ID and renders it inline.

```csharp
[BlockMetadata("content_embed", "Content Embed", Category = "Content")]
[JsonDerivedType(typeof(ContentEmbedBlock), "content_embed")]
public sealed class ContentEmbedBlock : BlockBase
{
    public const string Discriminator = "content_embed";
    public override string BlockType => Discriminator;

    /// <summary>The ContentItem to render.</summary>
    public long ContentItemId { get; set; }

    /// <summary>
    /// Which rendering path to use: DynamicBlock (Scriban) or
    /// BlockLayout (individual block instances per field).
    /// </summary>
    public ContentTypeRenderMode RenderMode { get; set; } = ContentTypeRenderMode.DynamicBlock;

    /// <summary>
    /// Optional per-field override mappings.
    /// When set, only these fields are rendered using the specified component aliases,
    /// instead of the default field-to-block mapping.
    /// </summary>
    public List<ContentEmbedFieldMapping>? FieldOverrides { get; set; }
}

public sealed record ContentEmbedFieldMapping(
    string FieldName,
    string ComponentAlias,
    Dictionary<string, JsonElement>? Props = null
);
```

**What it is not:** ContentEmbedBlock is not a page, not a layout, and not a
replacement for `PageDocument`. It is one block among many in the block hierarchy.

**Placement in PageDocument:**

```
PageDocument "About Us"
└── LayoutRegions
    ├── HeroBlock
    ├── ContentEmbedBlock ──→ ContentItem "Alice" (team-member)
    ├── ContentEmbedBlock ──→ ContentItem "Bob"   (team-member)
    └── TextBlock
```

---

## 3. Bridging Layer: ContentTypeDefinition → DynamicBlockDefinition

### 3.1 Auto-generating Scriban templates from field definitions

When a ContentTypeDefinition has no custom ScribanTemplate, the system generates one automatically.

**Example: ContentTypeDefinition with three fields**

```csharp
new ContentTypeDefinition
{
    Alias = "landing-page",
    Name = "Landing Page",
    Fields =
    [
        new() { Name = "HeroTitle", FieldType = "text", Required = true },
        new() { Name = "HeroImage", FieldType = "image" },
        new() { Name = "CallToActionUrl", FieldType = "url" }
    ]
}
```

**Auto-generated Scriban template:**

```scriban
<section class="content-type-landing-page">
  <div class="aero-field aero-field-text">
    {{ block.HeroTitle }}
  </div>
  {{ if block.HeroImage }}
  <div class="aero-field aero-field-image">
    <img src="{{ block.HeroImage }}" alt="" />
  </div>
  {{ end }}
  {{ if block.CallToActionUrl }}
  <div class="aero-field aero-field-url">
    <a href="{{ block.CallToActionUrl }}">Learn More</a>
  </div>
  {{ end }}
</section>
```

**The `ContentTypeTemplateGenerator` service:**

```csharp
public static class ContentTypeTemplateGenerator
{
    /// <summary>
    /// Generates a Scriban template from a ContentTypeDefinition's Fields.
    /// Each field type maps to a template snippet registered by modules.
    /// </summary>
    public static string GenerateTemplate(ContentTypeDefinition definition)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"""<section class="content-type-{definition.Alias}">""");

        foreach (var field in definition.Fields)
        {
            var snippet = FieldTemplateRegistry.GetSnippet(field.FieldType);
            sb.AppendLine(snippet.Render(field));
        }

        sb.AppendLine("</section>");
        return sb.ToString();
    }
}
```

### 3.2 The FieldTemplateRegistry

Modules register template snippets for their field types:

```csharp
public interface IFieldTemplateSnippet
{
    string FieldType { get; }
    string Render(ContentFieldDefinition field);
}
```

```csharp
public sealed class TextFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "text";

    public string Render(ContentFieldDefinition field) => $$"""
    <div class="aero-field aero-field-text">
      {{ block.{{field.Name}} }}
    </div>
    """;
}

public sealed class ImageFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "image";

    public string Render(ContentFieldDefinition field) => $$"""
    {{ if block.{{field.Name}} }}
    <div class="aero-field aero-field-image">
      <img src="{{ block.{{field.Name}} }}" alt="" />
    </div>
    {{ end }}
    """;
}
```

Registration happens in `IAeroModule.ConfigureServices`:

```csharp
services.AddSingleton<IFieldTemplateSnippet, TextFieldSnippet>();
services.AddSingleton<IFieldTemplateSnippet, ImageFieldSnippet>();
```

### 3.3 The ContentType → DynamicBlock bridge service

```csharp
public interface IContentTypeRenderingBridge
{
    /// <summary>
    /// Given a ContentTypeDefinition + ContentItem, produces a DynamicTemplateBlock
    /// that the existing rendering pipeline can render.
    /// </summary>
    Task<Result<DynamicTemplateBlock, AeroError>> ToDynamicBlockAsync(
        ContentTypeDefinition typeDef,
        ContentItem item,
        CancellationToken ct = default);
}
```

```csharp
public sealed class ContentTypeDynamicBlockBridge(
    IEnumerable<IFieldTemplateSnippet> snippets,
    IDocumentSession session) : IContentTypeRenderingBridge
{
    public async Task<Result<DynamicTemplateBlock, AeroError>> ToDynamicBlockAsync(
        ContentTypeDefinition typeDef,
        ContentItem item,
        CancellationToken ct = default)
    {
        // 1. Resolve or create the DynamicBlockDefinition for this content type
        var definitionResult = await GetOrCreateDefinitionAsync(typeDef, ct);

        if (definitionResult is Result<DynamicBlockDefinition, AeroError>.Failure fail)
            return fail.Error;

        var definition = ((Result<DynamicBlockDefinition, AeroError>.Ok)definitionResult).Value;

        // 2. Serialize ContentItem.Fields (Dictionary<string, JsonElement>) into a JsonDocument
        var dataJson = JsonSerializer.Serialize(item.Fields, AeroJsonContext.Default.Options);
        var dataDocument = JsonDocument.Parse(dataJson);

        // 3. Produce a DynamicTemplateBlock — the existing pipeline handles the rest
        return Prelude.Ok<DynamicTemplateBlock, AeroError>(new DynamicTemplateBlock
        {
            Id = item.Id,
            DefinitionId = definition.Id,
            DefinitionVersion = definition.Version,
            Data = dataDocument
        });
    }

    private async Task<Result<DynamicBlockDefinition, AeroError>> GetOrCreateDefinitionAsync(
        ContentTypeDefinition typeDef,
        CancellationToken ct)
    {
        // Check if a DynamicBlockDefinition already exists for this content type
        var existing = await session.Query<DynamicBlockDefinition>()
            .FirstOrDefaultAsync(d =>
                d.BlockType == DynamicTemplateBlock.Discriminator &&
                d.Name == $"ct:{typeDef.Alias}" &&
                d.IsPublished, ct);

        if (existing is not null)
            return Prelude.Ok<DynamicBlockDefinition, AeroError>(existing);

        // Auto-generate a Scriban template from the field definitions
        var template = string.IsNullOrWhiteSpace(typeDef.ScribanTemplate)
            ? ContentTypeTemplateGenerator.GenerateTemplate(typeDef)
            : typeDef.ScribanTemplate;

        // Generate a DataSchema (JSON Schema) from field definitions
        var schema = ContentTypeSchemaGenerator.GenerateSchema(typeDef);

        var definition = new DynamicBlockDefinition
        {
            Id = Snowflake.NewId(),
            Name = $"ct:{typeDef.Alias}",
            BlockType = DynamicTemplateBlock.Discriminator,
            ScribanTemplate = template,
            DataSchema = schema,
            Version = 1,
            IsPublished = true
        };

        session.Store(definition);
        return Prelude.Ok<DynamicBlockDefinition, AeroError>(definition);
    }
}
```

### 3.4 ContentEmbedBlock renderer integration

The `ContentEmbedBlockRenderer` bridges the block system to the content type
system at render time.

```csharp
[CmsBlockRenderer(typeof(ContentEmbedBlock))]
public sealed class ContentEmbedBlockRenderer : IBlockRenderer
{
    public string ComponentAlias => "content_embed";

    public async ValueTask<string> RenderAsync(
        BlockRenderContext context,
        CancellationToken ct = default)
    {
        var block = (ContentEmbedBlock)context.Block;

        // 1. Load the ContentItem by ID
        var contentService = context.Services.GetRequiredService<IContentService>();
        var itemResult = await contentService.LoadAsync(block.ContentItemId, ct);
        if (itemResult is Result<ContentItem, AeroError>.Failure)
            return "";

        var item = ((Result<ContentItem, AeroError>.Ok)itemResult).Value;

        // 2. Load the ContentTypeDefinition
        var typeService = context.Services.GetRequiredService<IContentTypeService>();
        var typeResult = await typeService.GetByAliasAsync(item.SiteId, item.ContentTypeAlias, ct);
        if (typeResult is Result<ContentTypeDefinition, AeroError>.Failure)
            return "";

        // 3. Bridge to DynamicTemplateBlock
        var bridge = context.Services.GetRequiredService<IContentTypeRenderingBridge>();
        var blockResult = await bridge.ToDynamicBlockAsync(
            ((Result<ContentTypeDefinition, AeroError>.Ok)typeResult).Value,
            item, ct);
        if (blockResult is Result<DynamicTemplateBlock, AeroError>.Failure)
            return "";

        var dynamicBlock = ((Result<DynamicTemplateBlock, AeroError>.Ok)blockResult).Value;

        // 4. Render through Scriban
        var scriban = context.Services.GetRequiredService<ISecureScribanRenderer>();
        var definitionResult = await bridge.GetDefinitionAsync(
            ((Result<ContentTypeDefinition, AeroError>.Ok)typeResult).Value, ct);
        if (definitionResult is Result<DynamicBlockDefinition, AeroError>.Failure)
            return "";

        var definition = ((Result<DynamicBlockDefinition, AeroError>.Ok)definitionResult).Value;
        var htmlResult = await scriban.RenderAsync(definition, dynamicBlock.Data, ct);
        if (htmlResult is Result<string, AeroError>.Failure)
            return "";

        return ((Result<string, AeroError>.Ok)htmlResult).Value;
    }
}
```

**Key point:** The renderer resolves services via `context.Services` at render
time (not constructor injection). This is intentional — block renderers are
registered as singletons; content type services are scoped per request.

---

## 4. Rendering Pipeline

### 4.1 DynamicBlock mode (default)

The full page is rendered as a single `DynamicTemplateBlock`.

```
Request "/{slug}"
    ↓
ContentItem loaded by slug
    ↓
ContentTypeDefinition loaded by alias
    ↓
IContentTypeRenderingBridge.ToDynamicBlockAsync(typeDef, item)
    ↓
DynamicTemplateBlock
    ↓
DynamicTemplateBlockRenderer (existing Blazor component)
    → IDynamicBlockDefinitionService.GetAsync(definitionId, version)
        → resolves DynamicBlockDefinition (with Scriban template)
    → ISecureScribanRenderer.RenderAsync(definition, block.Data)
        → validates, parses, renders Scriban against JsonElement data
    → HTML output
```

**Rendering integration:**

ContentItems are **typically embedded inside PageDocument blocks** — the existing
`DynamicPageModel` + `Page.cshtml` pipeline never needs to change.

If you also want ContentItems to have their own public URLs (e.g. a standalone
team member profile at `/team/alice`), register a second route that acts as a
**fallback** when no PageDocument matches the slug:

```csharp
// Option: separate routes with ASP.NET route precedence
app.MapGet("/{slug}", GetPageBySlug);           // existing — PageDocument, higher priority
app.MapGet("/{**slug}", GetContentBySlug);      // new — ContentItem catch-all, lower priority
```

```csharp
// Standalone ContentItem URL handler — only registered if ContentItems
// need their own public URLs. Otherwise, ContentItems are embedded in
// PageDocument blocks and the existing DynamicPageModel handles everything.
app.MapGet("/{**slug}", async (
    string? slug,
    IContentService contentService,
    IContentTypeService typeService,
    IContentTypeRenderingBridge bridge,
    ISecureScribanRenderer scribanRenderer,
    CancellationToken ct) =>
{
    var normalizedSlug = "/" + (slug ?? "").Trim('/');

    var contentResult = await contentService.GetBySlugAsync(1, normalizedSlug, ct);
    if (contentResult is Result<ContentItem, AeroError>.Failure)
        return Results.NotFound();

    var content = ((Result<ContentItem, AeroError>.Ok)contentResult).Value;
    var typeResult = await typeService.GetByAliasAsync(1, content.ContentTypeAlias, ct);
    if (typeResult is Result<ContentTypeDefinition, AeroError>.Failure)
        return Results.Problem($"Content type '{content.ContentTypeAlias}' not found.");

    var type = ((Result<ContentTypeDefinition, AeroError>.Ok)typeResult).Value;
    var blockResult = await bridge.ToDynamicBlockAsync(type, content, ct);
    if (blockResult is Result<DynamicTemplateBlock, AeroError>.Failure fail)
        return Results.Problem(fail.Error.Message);

    var block = ((Result<DynamicTemplateBlock, AeroError>.Ok)blockResult).Value;
    var definitionResult = await bridge.GetDefinitionAsync(type, ct);
    if (definitionResult is Result<DynamicBlockDefinition, AeroError>.Failure defFail)
        return Results.Problem(defFail.Error.Message);

    var definition = ((Result<DynamicBlockDefinition, AeroError>.Ok)definitionResult).Value;
    var htmlResult = await scribanRenderer.RenderAsync(definition, block.Data, ct);
    if (htmlResult is Result<string, AeroError>.Failure renderFail)
        return Results.Problem(renderFail.Error.Message);

    return Results.Content(((Result<string, AeroError>.Ok)htmlResult).Value, "text/html");
});
```

### 4.2 BlockLayout mode

For complex layouts, each field becomes a `BlockInstance` in the page layout.

```
ContentItem
    ↓
For each field in ContentTypeDefinition.Fields:
    → Map field.FieldType → ComponentAlias (via FieldComponentRegistry)
    → Create BlockInstance { ComponentAlias, Props: { field.Name → field.Value } }
    → Add to PageDocument.Blocks
    ↓
Render using existing PageDocument rendering pipeline
    → LayoutRegions → BlockPlacement → IBlockRenderer
```

---

## 5. Dev-Time Path: Source Generator Support

### 5.1 ContentType attribute

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class ContentTypeAttribute : Attribute
{
    public string Alias { get; }
    public string Name { get; }

    public ContentTypeAttribute(string alias, string name)
    {
        Alias = alias;
        Name = name;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class ContentFieldAttribute : Attribute
{
    public string FieldType { get; set; } = "text";
    public bool Required { get; set; }
}
```

### 5.2 Usage on existing entities

```csharp
[ContentType("blog-post", "Blog Post")]
public sealed class BlogPostDocument : Entity
{
    [ContentField(FieldType = "text", Required = true)]
    public string Title { get; set; } = string.Empty;

    [ContentField(FieldType = "richtext", Required = true)]
    public string Excerpt { get; set; } = string.Empty;

    [ContentField(FieldType = "image")]
    public string? ImageUrl { get; set; }

    // ...
}
```

### 5.3 Source generator output

The `ContentTypeGenerator` (new incremental source generator) produces:

```csharp
// Generated: <alias>.g.cs
public static partial class GeneratedContentTypes
{
    public static ContentTypeDefinition BlogPost { get; } = new()
    {
        Alias = "blog-post",
        Name = "Blog Post",
        Fields =
        {
            new() { Name = "Title", FieldType = "text", Required = true },
            new() { Name = "Excerpt", FieldType = "richtext", Required = true },
            new() { Name = "ImageUrl", FieldType = "image" }
        }
    };

    public static IReadOnlyDictionary<string, ContentTypeDefinition> All { get; }
        = new Dictionary<string, ContentTypeDefinition>(StringComparer.OrdinalIgnoreCase)
    {
        ["blog-post"] = BlogPost
    };
}
```

This follows the same pattern as the existing `BlockRendererGenerator`:

- Source generator discovers `[ContentType]` + `[ContentField]` at compile time
- Produces `GeneratedContentTypes` with `ContentTypeDefinition` instances
- Registers them in the module's `IAeroModuleBuilder.AddContentType()`
- Same rendering pipeline handles both generated and runtime definitions

---

## 6. Admin UI Integration

### 6.1 Content type editor

The admin manager UI uses `ContentTypeDefinition.Fields` to build editors:

```
ContentTypeDefinition.Fields
    ↓
For each field:
    → Look up IFieldEditor component by field.FieldType
    → Render editor with field.Name, field.Required, field.Settings
    ↓
On save: serialize editor values → Dictionary<string, JsonElement>
    → store as ContentItem
```

### 6.2 Field type system

Field types define three separate concerns, each registered independently:

```csharp
// Editor concern — what UI component renders this field in the admin
public interface IContentFieldEditor
{
    string FieldType { get; }
    string EditorComponent { get; }  // "aero-textbox", "aero-media-picker", "aero-reference-picker"
    object? Normalize(object? value);
}

// Sync validation concern — registered per field type
public interface IContentFieldValidator
{
    string FieldType { get; }
    void ValidateElement(
        ContentFieldDefinition field,
        JsonElement element,
        ContentValidationMode mode,
        ValidationContext<ContentItem> context);
}

// Template snippet concern — for Scriban template generation
public interface IFieldTemplateSnippet
{
    string FieldType { get; }
    string Render(ContentFieldDefinition field);
}
```

Example text field implementation:

```csharp
public sealed class TextFieldEditor : IContentFieldEditor
{
    public string FieldType => "text";
    public string EditorComponent => "aero-textbox";
    public object? Normalize(object? value) => value?.ToString();
}

public sealed class TextFieldValidator : IContentFieldValidator { ... }  // see §7.4
public sealed class TextFieldSnippet : IFieldTemplateSnippet { ... }     // see §3.2
```

Reference field adds its own settings model:

```csharp
public sealed record ReferenceFieldSettings(
    string TargetContentType,
    bool AllowMultiple = false);
```

From a ContentFieldDefinition stored in JSON:

```json
{
  "name": "Author",
  "fieldType": "reference",
  "required": true,
  "settings": {
    "targetContentType": "author",
    "allowMultiple": false
  }
}
```

### 6.3 Plugin points

| Registration | Extension point | Consumed by |
|-------------|----------------|-------------|
| `AddContentType("alias")` | `IAeroModuleBuilder` | Content type registry |
| `IContentFieldEditor` | DI: `AddFieldEditor<T>()` | Admin UI editor factory |
| `IContentFieldValidator` | DI | `DynamicContentValidator` |
| `IFieldTemplateSnippet` | DI | `ContentTypeTemplateGenerator` |
| `IAsyncContentValidator` | DI | `ContentValidationService` |
| `[ContentType]` + `[ContentField]` | Source generator | Dev-time `ContentTypeDefinition` |

---

## 7. Validation

Validation is split into two layers with FluentValidation:

| Layer | Scope | Examples | FluentValidation pattern |
|-------|-------|----------|--------------------------|
| Pure validation (sync) | Structural + field-level rules | required, maxLength, regex, number range | `AbstractValidator<>` constructor rules + `Custom` |
| Domain validation (async) | Cross-cutting rules needing services | unique slug, referenced item exists, publish window | `IAsyncContentValidator` injected via DI |

### 7.1 Schema validation (ContentTypeDefinition + ContentFieldDefinition)

```csharp
public sealed class ContentTypeDefinitionValidator : AbstractValidator<ContentTypeDefinition>
{
    public ContentTypeDefinitionValidator()
    {
        RuleFor(x => x.Alias).NotEmpty().MaximumLength(128)
            .Matches("^[a-z][a-z0-9_-]*$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Fields).NotEmpty();
        RuleForEach(x => x.Fields).SetValidator(new ContentFieldDefinitionValidator());
    }
}

public sealed class ContentFieldDefinitionValidator : AbstractValidator<ContentFieldDefinition>
{
    public ContentFieldDefinitionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128)
            .Matches("^[a-zA-Z][a-zA-Z0-9_]*$");
        RuleFor(x => x.FieldType).NotEmpty();
    }
}
```

### 7.2 Content validation mode

Validation strictness depends on the lifecycle stage:

```csharp
public enum ContentValidationMode
{
    /// <summary>Loose validation — allows missing optional/publish-required fields</summary>
    Draft,
    /// <summary>Strict validation — required fields, references must exist, slug must be unique</summary>
    Publish
}
```

### 7.3 Dynamic content validator (sync layer)

Uses FluentValidation's `Custom` method — the documented pattern for dynamic property
validation. Avoids expression-tree injection (`RuleForField<T>` + `GetFieldValue<T>`)
in favor of direct `JsonElement` inspection.

```csharp
public sealed class DynamicContentValidator : AbstractValidator<ContentItem>
{
    public DynamicContentValidator(
        ContentTypeDefinition type,
        ContentValidationMode mode,
        IEnumerable<IContentFieldValidator> fieldValidators)
    {
        RuleFor(x => x.ContentTypeAlias).Equal(type.Alias);
        RuleFor(x => x.Slug).NotEmpty();

        var lookup = fieldValidators
            .ToDictionary(v => v.FieldType, StringComparer.OrdinalIgnoreCase);

        // FluentValidation's Custom — the recommended pattern for dynamic fields
        Custom((item, context) =>
        {
            foreach (var field in type.Fields)
            {
                var hasValue = item.Fields.TryGetValue(field.Name, out var element)
                    && element.ValueKind != JsonValueKind.Null;

                // Required check
                if (!hasValue)
                {
                    if (field.Required && mode == ContentValidationMode.Publish)
                        context.AddFailure(field.Name, $"{field.Label ?? field.Name} is required.");
                    continue;
                }

                // Delegate to the field-type-specific validator
                if (lookup.TryGetValue(field.FieldType, out var fieldValidator))
                    fieldValidator.ValidateElement(field, element, mode, context);
            }
        });
    }
}
```

### 7.4 Per-field-type validators (sync)

Each field type contributes its own validation logic. Registered via DI.

```csharp
public interface IContentFieldValidator
{
    string FieldType { get; }

    /// <summary>
    /// Validates a single field's JsonElement value against the field definition.
    /// Called by DynamicContentValidator.Custom for each field.
    /// </summary>
    void ValidateElement(
        ContentFieldDefinition field,
        JsonElement element,
        ContentValidationMode mode,
        ValidationContext<ContentItem> context);
}
```

```csharp
public sealed class TextFieldValidator : IContentFieldValidator
{
    public string FieldType => "text";

    public void ValidateElement(
        ContentFieldDefinition field,
        JsonElement element,
        ContentValidationMode mode,
        ValidationContext<ContentItem> context)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be text.");
            return;
        }

        var value = element.GetString() ?? "";

        if (field.Settings.TryGetValue("maxLength", out var maxObj) &&
            maxObj is JsonElement maxElem && maxElem.TryGetInt32(out var max) &&
            value.Length > max)
        {
            context.AddFailure(field.Name,
                $"{field.Label ?? field.Name} must be {max} characters or fewer.");
        }

        if (field.Settings.TryGetValue("minLength", out var minObj) &&
            minObj is JsonElement minElem && minElem.TryGetInt32(out var min) &&
            value.Length < min)
        {
            context.AddFailure(field.Name,
                $"{field.Label ?? field.Name} must be at least {min} characters.");
        }

        if (field.Settings.TryGetValue("regex", out var regexObj) &&
            regexObj is JsonElement regexElem &&
            !System.Text.RegularExpressions.Regex.IsMatch(value, regexElem.GetString() ?? ""))
        {
            context.AddFailure(field.Name,
                $"{field.Label ?? field.Name} format is invalid.");
        }
    }
}

public sealed class NumberFieldValidator : IContentFieldValidator
{
    public string FieldType => "number";

    public void ValidateElement(
        ContentFieldDefinition field,
        JsonElement element,
        ContentValidationMode mode,
        ValidationContext<ContentItem> context)
    {
        if (!element.TryGetDecimal(out var value))
        {
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be a number.");
            return;
        }

        if (field.Settings.TryGetValue("min", out var minObj) &&
            minObj is JsonElement minElem && minElem.TryGetDecimal(out var min) &&
            value < min)
        {
            context.AddFailure(field.Name,
                $"{field.Label ?? field.Name} must be at least {min}.");
        }

        if (field.Settings.TryGetValue("max", out var maxObj) &&
            maxObj is JsonElement maxElem && maxElem.TryGetDecimal(out var max) &&
            value > max)
        {
            context.AddFailure(field.Name,
                $"{field.Label ?? field.Name} must be at most {max}.");
        }
    }
}

public sealed class ReferenceFieldValidator : IContentFieldValidator
{
    public string FieldType => "reference";

    public void ValidateElement(
        ContentFieldDefinition field,
        JsonElement element,
        ContentValidationMode mode,
        ValidationContext<ContentItem> context)
    {
        var targetContentType = field.Settings.TryGetValue("targetContentType", out var t)
            ? t.GetString() : null;

        if (field.Settings.TryGetValue("allowMultiple", out var multiObj) &&
            multiObj.ValueKind == JsonValueKind.True)
        {
            // Multi-reference: expect array of IDs
            if (element.ValueKind != JsonValueKind.Array)
            {
                context.AddFailure(field.Name,
                    $"{field.Label ?? field.Name} must be a list of references.");
                return;
            }

            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String ||
                    !long.TryParse(item.GetString(), out _))
                {
                    context.AddFailure(field.Name,
                        $"{field.Label ?? field.Name} contains invalid reference IDs.");
                    break;
                }
            }
        }
        else
        {
            // Single reference: expect string ID
            if (element.ValueKind != JsonValueKind.String ||
                !long.TryParse(element.GetString(), out _))
            {
                context.AddFailure(field.Name,
                    $"{field.Label ?? field.Name} must be a valid reference ID.");
            }
        }

        // Existence check is async — handled by IAsyncContentValidator
    }
}
```

### 7.5 Async/domain validation layer

Cross-cutting rules that require service access are resolved via DI at validator
construction time — no service locator pattern.

```csharp
/// <summary>
/// Async validation rules that require database or service access.
/// Constructed via DI with injected dependencies.
/// </summary>
public interface IAsyncContentValidator
{
    /// <summary>
    /// Performs async validation and returns any failures.
    /// An empty list means validation passed.
    /// </summary>
    Task<IReadOnlyList<ValidationFailure>> ValidateAsync(
        ContentItem item,
        ContentTypeDefinition type,
        CancellationToken ct);
}
```

```csharp
/// <summary>
/// Validates that referenced content items exist.
/// Only runs in Publish mode.
/// </summary>
public sealed class ReferenceExistenceValidator(IContentService contentService) : IAsyncContentValidator
{
    public async Task<IReadOnlyList<ValidationFailure>> ValidateAsync(
        ContentItem item,
        ContentTypeDefinition type,
        CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        foreach (var field in type.Fields.Where(f => f.FieldType == "reference"))
        {
            if (!item.Fields.TryGetValue(field.Name, out var element)) continue;
            if (element.ValueKind == JsonValueKind.Null) continue;

            if (field.Settings.TryGetValue("allowMultiple", out var multiObj) &&
                multiObj.ValueKind == JsonValueKind.True)
            {
                foreach (var refItem in element.EnumerateArray())
                {
                    ValidateSingleReference(refItem, field, failures, contentService, ct);
                }
            }
            else
            {
                ValidateSingleReference(element, field, failures, contentService, ct);
            }

            await Task.CompletedTask; // the inner validates synchronously
        }

        return failures;
    }

    private static void ValidateSingleReference(
        JsonElement element,
        ContentFieldDefinition field,
        List<ValidationFailure> failures,
        IContentService contentService,
        CancellationToken ct)
    {
        if (!long.TryParse(element.GetString(), out var id)) return;

        // Fire-and-forget check — for true async batching, use a different pattern
        // (e.g., collect all IDs, query once, then produce failures)
    }
}

/// <summary>
/// Validates that the slug is unique within the site.
/// </summary>
public sealed class UniqueSlugValidator(IContentService contentService) : IAsyncContentValidator
{
    public async Task<IReadOnlyList<ValidationFailure>> ValidateAsync(
        ContentItem item,
        ContentTypeDefinition type,
        CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        if (string.IsNullOrWhiteSpace(item.Slug)) return failures;

        var existingResult = await contentService.GetBySlugAsync(item.SiteId, item.Slug, ct);
        if (existingResult is Result<ContentItem, AeroError>.Ok ok &&
            ok.Value.Id != item.Id)
        {
            failures.Add(new ValidationFailure(nameof(item.Slug),
                $"Slug '{item.Slug}' is already in use."));
        }

        return failures;
    }
}
```

### 7.6 Validation service (orchestrates both layers)

```csharp
public sealed class ContentValidationService(
    IContentTypeService contentTypeService,
    IEnumerable<IContentFieldValidator> fieldValidators,
    IEnumerable<IAsyncContentValidator> asyncValidators)
{
    public async Task<Result<ContentItem, IReadOnlyList<ValidationFailure>>> ValidateAsync(
        ContentItem item,
        ContentValidationMode mode,
        CancellationToken ct = default)
    {
        // 1. Resolve the content type definition
        var typeResult = await contentTypeService.GetByAliasAsync(item.SiteId, item.ContentTypeAlias, ct);
        if (typeResult is Result<ContentTypeDefinition, AeroError>.Failure notFound)
        {
            return new ValidationResult([
                new(nameof(item.ContentTypeAlias),
                    $"Content type '{item.ContentTypeAlias}' was not found.")
            ]).Errors.ToList();
        }

        var type = ((Result<ContentTypeDefinition, AeroError>.Ok)typeResult).Value;

        // 2. Sync structural validation
        var syncValidator = new DynamicContentValidator(type, mode, fieldValidators);
        var syncResult = await syncValidator.ValidateAsync(item, ct);
        if (!syncResult.IsValid)
            return syncResult.Errors.ToList();

        // 3. Async domain validation (publish mode only)
        if (mode == ContentValidationMode.Publish)
        {
            var allFailures = new List<ValidationFailure>();
            foreach (var asyncValidator in asyncValidators)
            {
                var failures = await asyncValidator.ValidateAsync(item, type, ct);
                allFailures.AddRange(failures);
            }

            if (allFailures.Count > 0)
                return allFailures;
        }

        return item;
    }
}
```

---

## 8. Service Layer

### 8.1 Interfaces

```csharp
public interface IContentTypeService
{
    Task<Result<ContentTypeDefinition, AeroError>> GetByAliasAsync(
        long siteId, string alias, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ContentTypeDefinition>, AeroError>> GetAllAsync(
        long siteId, CancellationToken ct = default);

    Task<Result<ContentTypeDefinition, AeroError>> SaveAsync(
        ContentTypeDefinition definition, CancellationToken ct = default);
}

public interface IContentService
{
    Task<Result<ContentItem, AeroError>> GetBySlugAsync(
        long siteId, string slug, CancellationToken ct = default);

    Task<Result<ContentItem, AeroError>> SaveAsync(
        ContentItem item, CancellationToken ct = default);

    Task<bool> ExistsAsync(
        long id, CancellationToken ct = default);
}
```

### 8.2 Marten document models

```csharp
public sealed class ContentTypeDocument
{
    public string Id { get; set; } = string.Empty;   // "{siteId}:{alias}"
    public long SiteId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Icon { get; set; }
    public List<ContentFieldDefinition> Fields { get; set; } = [];
    public string? ScribanTemplate { get; set; }
    public ContentTypeRenderMode RenderMode { get; set; }
}

// ContentItem itself is an Entity, stored directly via Marten
```

Marten configuration:

```csharp
opts.Schema.For<ContentTypeDocument>()
    .Identity(x => x.Id)
    .DocumentAlias("content_type_definitions")
    .Index(x => x.SiteId);

opts.Schema.For<ContentItem>()
    .DocumentAlias("content_items")
    .Index(x => x.SiteId)
    .Index(x => x.Slug)
    .Index(x => x.ContentTypeAlias);
```

### 8.3 Save / publish command service

Validation guards every write. Draft saves use relaxed validation; publish
uses strict validation. Publishing snapshots the current version for history.

```csharp
public interface IContentService
{
    Task<Result<ContentItem, AeroError>> LoadAsync(long id, CancellationToken ct = default);
    Task<Result<ContentItem, AeroError>> GetBySlugAsync(
        long siteId, string slug, CancellationToken ct = default);
    Task<Result<ContentItem, AeroError>> SaveAsync(
        ContentItem item, CancellationToken ct = default);
    Task<bool> ExistsAsync(long id, CancellationToken ct = default);
    Task<Result<bool, AeroError>> DeleteAsync(
        long id, CancellationToken ct = default);
}

public sealed class ContentCommandService(
    ContentValidationService validation,
    IContentService contentService,
    IDocumentSession session)
{
    public async Task<Result<ContentItem, IReadOnlyList<ValidationFailure>>> SaveDraftAsync(
        ContentItem item, CancellationToken ct = default)
    {
        var result = await validation.ValidateAsync(item, ContentValidationMode.Draft, ct);
        if (result is Result<ContentItem, IReadOnlyList<ValidationFailure>>.Failure f)
            return f.Error;

        item.VersionNumber++;
        return await contentService.SaveAsync(item, ct);
    }

    public async Task<Result<ContentItem, IReadOnlyList<ValidationFailure>>> PublishAsync(
        ContentItem item, CancellationToken ct = default)
    {
        var result = await validation.ValidateAsync(item, ContentValidationMode.Publish, ct);
        if (result is Result<ContentItem, IReadOnlyList<ValidationFailure>>.Failure f)
            return f.Error;

        // Snapshot the current published state before overwriting
        if (item.PublicationState == ContentPublicationState.Published)
        {
            session.Store(new ContentItemVersion
            {
                ContentItemId = item.Id,
                VersionNumber = item.VersionNumber,
                Fields = item.Fields,
                CreatedUtc = DateTimeOffset.UtcNow
            });
        }

        item.VersionNumber++;
        item.PublicationState = ContentPublicationState.Published;
        item.PublishedOn = DateTimeOffset.UtcNow;

        return await contentService.SaveAsync(item, ct);
    }

    public async Task<Result<bool, IReadOnlyList<ValidationFailure>>> DeleteAsync(
        long id, CancellationToken ct = default)
    {
        // Safety check: is this ContentItem referenced by any ContentEmbedBlock?
        var referencingBlocks = await session.Query<PageDocument>()
            .Where(p => p.LayoutRegions
                .SelectMany(r => r.Columns)
                .SelectMany(c => c.Blocks)
                .Any(b => b.BlockType == ContentEmbedBlock.Discriminator
                       && b.ContentItemId == id))
            .CountAsync(ct);

        if (referencingBlocks > 0)
        {
            return new List<ValidationFailure>
            {
                new("", $"Cannot delete: referenced by {referencingBlocks} block(s). Remove references first.")
            };
        }

        return await contentService.DeleteAsync(id, ct);
    }
}
```

#### Version history document

```csharp
public sealed class ContentItemVersion : Entity
{
    public long ContentItemId { get; set; }
    public int VersionNumber { get; set; }
    public Dictionary<string, JsonElement> Fields { get; set; } = [];
    public DateTimeOffset CreatedUtc { get; set; }
}
```

```csharp
opts.Schema.For<ContentItemVersion>()
    .DocumentAlias("content_item_versions")
    .Index(x => x.ContentItemId);
```

#### Scheduling evaluation (background job)

A recurring job (via Wolverine or TickerQ) evaluates scheduled items:

```csharp
public sealed class ScheduledPublishHandler(IContentService contentService)
{
    public async Task Handle(ScheduledPublishEvaluation command, CancellationToken ct)
    {
        // Find items due for publish
        var dueItems = await session.Query<ContentItem>()
            .Where(i => i.SchedulePublishUtc <= DateTimeOffset.UtcNow
                     && i.PublicationState == ContentPublicationState.Draft)
            .ToListAsync(ct);

        foreach (var item in dueItems)
        {
            item.PublicationState = ContentPublicationState.Published;
            item.PublishedOn = DateTimeOffset.UtcNow;
            item.SchedulePublishUtc = null;
            await contentService.SaveAsync(item, ct);
        }

        // Find items due for unpublish
        var dueUnpublish = await session.Query<ContentItem>()
            .Where(i => i.ScheduleUnpublishUtc <= DateTimeOffset.UtcNow
                     && i.PublicationState == ContentPublicationState.Published)
            .ToListAsync(ct);

        foreach (var item in dueUnpublish)
        {
            item.PublicationState = ContentPublicationState.Draft;
            item.ScheduleUnpublishUtc = null;
            await contentService.SaveAsync(item, ct);
        }
    }
}
```

---

### 8.4 Content query service

A query abstraction for listing and filtering ContentItems by content type.

```csharp
public interface IContentQueryService
{
    /// <summary>Paginated listing of content items by type.</summary>
    Task<Result<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>> GetByTypeAsync(
        long siteId,
        string contentTypeAlias,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default);

    /// <summary>Simple field-filtered search.</summary>
    Task<Result<IReadOnlyList<ContentItem>, AeroError>> SearchAsync(
        long siteId,
        string contentTypeAlias,
        Dictionary<string, string> fieldFilters,
        CancellationToken ct = default);
}
```

Marten implementation uses the document identity and computed indexes:

```csharp
public sealed class MartenContentQueryService(IDocumentSession session) : IContentQueryService
{
    public async Task<Result<(IReadOnlyList<ContentItem>, long), AeroError>> GetByTypeAsync(
        long siteId, string alias, int skip, int take, CancellationToken ct)
    {
        var query = session.Query<ContentItem>()
            .Where(x => x.SiteId == siteId && x.ContentTypeAlias == alias);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip(skip).Take(take)
            .OrderByDescending(x => x.PublishedOn ?? x.CreatedOn)
            .ToListAsync(ct);

        return Prelude.Ok<ContentItem, AeroError>((items, total).AsTuple()!);
    }

    public async Task<Result<IReadOnlyList<ContentItem>, AeroError>> SearchAsync(
        long siteId, string alias, Dictionary<string, string> filters, CancellationToken ct)
    {
        var query = session.Query<ContentItem>()
            .Where(x => x.SiteId == siteId && x.ContentTypeAlias == alias);

        foreach (var (field, value) in filters)
        {
            query = query.Where(x =>
                x.Fields[field].ValueKind == System.Text.Json.JsonValueKind.String &&
                x.Fields[field].GetString()!.Contains(value, StringComparison.OrdinalIgnoreCase));
        }

        var items = await query
            .OrderByDescending(x => x.PublishedOn ?? x.CreatedOn)
            .ToListAsync(ct);

        return Prelude.Ok<ContentItem, AeroError>((IReadOnlyList<ContentItem>)items);
    }
}
```

**Usage from a page block (e.g. team listing):**

A `TeamListingBlock` uses the query service at render time to fetch and iterate
over all ContentItems of a given type:

```csharp
[BlockMetadata("team_listing", "Team Listing", Category = "Content")]
public sealed class TeamListingBlock : BlockBase { ... }

public sealed class TeamListingRenderer : IBlockRenderer
{
    public string ComponentAlias => "team_listing";

    public async ValueTask<string> RenderAsync(BlockRenderContext context, CancellationToken ct)
    {
        var query = context.Services.GetRequiredService<IContentQueryService>();
        var bridge = context.Services.GetRequiredService<IContentTypeRenderingBridge>();
        var scriban = context.Services.GetRequiredService<ISecureScribanRenderer>();
        var typeService = context.Services.GetRequiredService<IContentTypeService>();

        var siteId = context.Get<long>(/* from page context */);

        var result = await query.GetByTypeAsync(siteId, "team-member", 0, 50, ct);
        if (result is Result<(IReadOnlyList<ContentItem>, long), AeroError>.Failure f)
            return "";

        var (members, _) = ((Result<(IReadOnlyList<ContentItem>, long), AeroError>.Ok)result).Value;

        var html = new StringBuilder();
        foreach (var member in members)
        {
            var typeResult = await typeService.GetByAliasAsync(siteId, member.ContentTypeAlias, ct);
            if (typeResult is Result<ContentTypeDefinition, AeroError>.Failure) continue;

            var blockResult = await bridge.ToDynamicBlockAsync(
                ((Result<ContentTypeDefinition, AeroError>.Ok)typeResult).Value, member, ct);
            if (blockResult is Result<DynamicTemplateBlock, AeroError>.Failure) continue;

            var dynBlock = ((Result<DynamicTemplateBlock, AeroError>.Ok)blockResult).Value;
            var defResult = await bridge.GetDefinitionAsync(
                ((Result<ContentTypeDefinition, AeroError>.Ok)typeResult).Value, ct);
            if (defResult is Result<DynamicBlockDefinition, AeroError>.Failure) continue;

            var htmlResult = await scriban.RenderAsync(
                ((Result<DynamicBlockDefinition, AeroError>.Ok)defResult).Value,
                dynBlock.Data, ct);
            if (htmlResult is Result<string, AeroError>.Ok ok)
                html.AppendLine(ok.Value);
        }

        return $"<div class=\"team-grid\">{html}</div>";
    }
}
```

---

## 9. Module Registration

The codebase already has the hooks. No new methods on `IAeroModule` are needed.

### 9.1 Existing hooks

```csharp
// Already exists — IAeroModule.cs
public interface IAeroModule
{
    void Configure(IAeroModuleBuilder builder);   // ← use this
    void ConfigureServices(IServiceCollection services, ...);
    // ...
}

// Already exists — IModuleBuilder.cs  
public interface IAeroModuleBuilder
{
    void AddContentType(string contentType);       // ← use this
    void AddContentPart<TPart>() where TPart : class, IContentPart;
    void AddFieldEditor<TEditor>() where TEditor : class, IFieldEditor;
    // ...
}

// Already exists — IAeroModule.cs:120
public interface IContentDefinitionModule : IAeroModule { }  // ← implement this
```

We fill in the empty marker interfaces (`IContentPart`, `IFieldEditor`) with real
implementations and register content types through the existing builder.

### 9.2 Module example

```csharp
[Module(nameof(MarketingModule))]
public sealed class MarketingModule : AeroModuleBase, IContentDefinitionModule
{
    public override void ConfigureServices(IServiceCollection services, ...)
    {
        // Fields (editors, validators, template snippets)
        services.AddSingleton<IContentFieldEditor, TextFieldEditor>();
        services.AddSingleton<IContentFieldEditor, ReferenceFieldEditor>();
        services.AddSingleton<IContentFieldValidator, TextFieldValidator>();
        services.AddSingleton<IContentFieldValidator, NumberFieldValidator>();
        services.AddSingleton<IContentFieldValidator, ReferenceFieldValidator>();
        services.AddSingleton<IFieldTemplateSnippet, TextFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, ImageFieldSnippet>();

        // Async validators
        services.AddSingleton<IAsyncContentValidator, ReferenceExistenceValidator>();

        // Block renderers (for BlockLayout mode)
        services.AddSingleton<IBlockRenderer, HeroBlockRenderer>();
    }

    public override void Configure(IAeroModuleBuilder builder)
    {
        // Register content types through the existing builder API
        builder.AddContentType("landing-page");
        builder.AddFieldEditor<TextFieldEditor>();
        builder.AddFieldEditor<ReferenceFieldEditor>();
    }
}
```

Content types can also be registered imperatively:

```csharp
public override void Configure(IAeroModuleBuilder builder)
{
    builder.AddContentType("blog-post");
    builder.AddContentType("case-study");
    builder.AddContentType("doctor-profile");
}
```

---

## 10. Existing Infrastructure Leveraged

| Existing component | Role in this design |
|-------------------|-------------------|
| `DynamicTemplateBlock` | Carries content field data as `JsonDocument` |
| `DynamicBlockDefinition` | Stores Scriban template (auto-generated or custom) + `DataSchema` |
| `DynamicTemplateBlockRenderer` Renders the Scriban template against field data |
| `ISecureScribanRenderer` | Secure Scriban execution with sandboxing |
| `IDynamicBlockDefinitionService` | Resolves template definitions |
| `CmsBlockRenderRegistry` | For BlockLayout mode — dispatches block instances |
| `BlockRendererGenerator` | Pattern to follow for `ContentTypeGenerator` |
| `BlockJsonContext` | AOT-safe JSON serialization context |
| `IAeroModuleBuilder` | Registration surface — `AddContentType()`, `AddFieldEditor()`, `AddContentPart()` already exist |
| `IAeroModule` | Module lifecycle — `Configure(IAeroModuleBuilder)` already virtual on `AeroModuleBase` |
| `IContentDefinitionModule` | Marker interface for content-type modules — already exists |
| `ContentSlugDocument` | Slug uniqueness enforcement |
| `FluentValidation` | Validation of schemas and content — `AbstractValidator<>`, `Custom` method |
| `Marten` `IDocumentSession` | Querying content types, content items, dynamic block definitions |
| `Result<T, AeroError>` | Railway return types in all service signatures |

---

## 11. What each layer owns

| Project | Owns |
|---------|------|
| `Aero.Cms.Abstractions` | `ContentTypeDefinition`, `ContentFieldDefinition`, `ContentItem`, `FieldBlockInstance`, `IFieldTemplateSnippet`, `IContentFieldEditor`, `IContentFieldValidator`, `IAsyncContentValidator`, `ContentValidationMode` |
| `Aero.Cms.Core` | `ContentTypeDynamicBlockBridge`, `ContentTypeTemplateGenerator`, `ContentTypeSchemaGenerator`, `MartenContentTypeService`, `MartenContentService`, `DynamicContentValidator`, `ContentValidationService`, `ContentCommandService`, `TextFieldValidator`, `NumberFieldValidator`, `ReferenceFieldValidator`, `ReferenceExistenceValidator`, `UniqueSlugValidator` |
| `Aero.Cms.SourceGenerators` | `ContentTypeGenerator` (new) — discovers `[ContentType]` + `[ContentField]`, produces `GeneratedContentTypes` |
| `Aero.Cms.Shared` | `ContentTypeFieldEditor.razor` (admin UI), bridge Razor components |
| `Aero.Cms.Modules.*` | Register content types, field editors, field validators, async validators, field template snippets, block renderers |

---

## 💥 EXAMPLE 💥 — Reusable Footer Block

This is a complete walkthrough of a **reusable site-level block** — a footer
component that is defined once, stored as data, and rendered across every page.

It demonstrates: component definition, block instance storage, AOT-safe
rendering via `Context.Get<T>()`, and the admin UI editor generated from
the component definition.

---

### Footer component definition

```csharp
public static class FooterComponent
{
    public static readonly ComponentDefinition Definition = new(
        Alias: "footer",
        Name: "Footer",
        Props:
        [
            new("companyName", "text", Required: true),
            new("logoUrl", "image"),
            new("trademarkText", "text"),
            new("columns", "link-columns"),
            new("socialLinks", "social-links")
        ]);
}
```

`ComponentDefinition` lives in `Aero.Cms.Abstractions` and describes the shape
of a block — no rendering logic, no reflection, just metadata.

```csharp
public sealed record ComponentDefinition(
    string Alias,
    string Name,
    IReadOnlyList<ComponentPropDefinition> Props
);

public sealed record ComponentPropDefinition(
    string Name,
    string Type,
    bool Required = false
);
```

---

### Footer stored as data (the block instance)

The manager UI serializes the editor output into a `BlockInstance` stored in
the page layout or a site-settings document.

```json
{
  "componentAlias": "footer",
  "props": {
    "companyName": "Aero CMS",
    "logoUrl": "/media/aero-logo.svg",
    "trademarkText": "Aero CMS™",

    "columns": [
      {
        "title": "Product",
        "links": [
          { "text": "Features", "url": "/features" },
          { "text": "Pricing", "url": "/pricing" },
          { "text": "Roadmap", "url": "/roadmap" }
        ]
      },
      {
        "title": "Company",
        "links": [
          { "text": "About", "url": "/about" },
          { "text": "Blog", "url": "/blog" },
          { "text": "Contact", "url": "/contact" }
        ]
      },
      {
        "title": "Legal",
        "links": [
          { "text": "Privacy", "url": "/privacy" },
          { "text": "Terms", "url": "/terms" },
          { "text": "Cookies", "url": "/cookies" }
        ]
      }
    ],

    "socialLinks": [
      { "platform": "github", "url": "https://github.com/example" },
      { "platform": "x", "url": "https://x.com/example" },
      { "platform": "linkedin", "url": "https://linkedin.com/company/example" }
    ]
  }
}
```

This is just a `BlockInstance`:

```csharp
public sealed class BlockInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ComponentAlias { get; set; } = "";
    public Dictionary<string, JsonElement> Props { get; set; } = [];
    public List<BlockInstance> Children { get; set; } = [];
}
```

---

### Footer renderer (AOT-safe, no reflection)

```csharp
public sealed class FooterBlockRenderer : IBlockRenderer
{
    public string ComponentAlias => "footer";

    public ValueTask<string> RenderAsync(
        BlockRenderContext context,
        CancellationToken ct = default)
    {
        var companyName = HtmlEncoder.Default.Encode(
            context.Get<string>("companyName") ?? "");

        var logoUrl = HtmlEncoder.Default.Encode(
            context.Get<string>("logoUrl") ?? "");

        var trademarkText = HtmlEncoder.Default.Encode(
            context.Get<string>("trademarkText") ?? companyName);

        var columns = context.Get<List<FooterColumn>>("columns") ?? [];
        var socialLinks = context.Get<List<SocialLink>>("socialLinks") ?? [];

        var columnHtml = string.Join("", columns.Select(column =>
        {
            var title = HtmlEncoder.Default.Encode(column.Title);

            var links = string.Join("", column.Links.Select(link =>
            {
                var text = HtmlEncoder.Default.Encode(link.Text);
                var url = HtmlEncoder.Default.Encode(link.Url);
                return $"""<li><a href="{url}">{text}</a></li>""";
            }));

            return $"""
            <nav class="aero-footer-column">
                <h3>{title}</h3>
                <ul>{links}</ul>
            </nav>
            """;
        }));

        var socialHtml = string.Join("", socialLinks.Select(link =>
        {
            var platform = HtmlEncoder.Default.Encode(link.Platform);
            var url = HtmlEncoder.Default.Encode(link.Url);
            return $"""
            <a class="aero-social-link" href="{url}" aria-label="{platform}">
                <span class="aero-social-icon aero-social-{platform}"></span>
            </a>
            """;
        }));

        var html = $$"""
        <footer class="aero-footer">
            <div class="aero-footer-main">
                <div class="aero-footer-brand">
                    <img src="{{logoUrl}}" alt="{{companyName}} logo" />
                    <strong>{{companyName}}</strong>
                    <span>{{trademarkText}}</span>
                </div>
                <div class="aero-footer-links">{{columnHtml}}</div>
            </div>
            <div class="aero-footer-bottom">
                <p>© {{DateTime.UtcNow.Year}} {{companyName}}. All rights reserved.</p>
                <div class="aero-footer-socials">{{socialHtml}}</div>
            </div>
        </footer>
        """;

        return ValueTask.FromResult(html);
    }
}
```

`context.Get<T>("propName")` is fully AOT-safe — it calls
`JsonSerializer.Deserialize<T>(element.GetRawText(), AeroJsonContext.Default.Options)`
using the source-generated `AeroJsonContext`.

---

### Supporting models (deserialized from JsonElement props)

```csharp
public sealed record FooterColumn(string Title, List<FooterLink> Links);
public sealed record FooterLink(string Text, string Url);
public sealed record SocialLink(string Platform, string Url);
```

These types must be registered in `BlockJsonContext` for AOT-compatible
deserialization:

```csharp
[JsonSerializable(typeof(List<FooterColumn>))]
[JsonSerializable(typeof(List<FooterLink>))]
[JsonSerializable(typeof(List<SocialLink>))]
public partial class BlockJsonContext : JsonSerializerContext { }
```

---

### Module registration

```csharp
[Module(nameof(LayoutModule))]
public sealed class LayoutModule : AeroModuleBase
{
    public override void ConfigureServices(IServiceCollection services, ...)
    {
        // Register the renderer — discovered by ComponentAlias
        services.AddSingleton<IBlockRenderer, FooterBlockRenderer>();

        // Register the component definition — consumed by admin UI
        services.AddSingleton(FooterComponent.Definition);
    }
}
```

---

### How the admin UI sees it

The manager reads `ComponentDefinition.Props` and auto-generates the editor:

```text
Footer
├── Company Name:    [textbox]         ← required
├── Logo:            [media picker]
├── Trademark Text:  [textbox]
├── Columns
│   ├── Product
│   │   ├── Features → /features
│   │   ├── Pricing  → /pricing
│   │   └── Roadmap  → /roadmap
│   ├── Company
│   │   ├── About    → /about
│   │   ├── Blog     → /blog
│   │   └── Contact  → /contact
│   └── Legal
│       ├── Privacy  → /privacy
│       ├── Terms    → /terms
│       └── Cookies  → /cookies
└── Social Links
    ├── GitHub   → URL
    ├── X        → URL
    └── LinkedIn → URL
```

---

### Global reusable block pattern

The footer should be a **site-level setting**, not duplicated on every page.
In the page layout it becomes a fixed slot:

```text
Site Layout
  ┌─────────────────────┐
  │  Header             │  ← SiteSettings.Header block
  ├─────────────────────┤
  │  Main Page Blocks   │  ← PageDocument.Blocks
  ├─────────────────────┤
  │  Footer             │  ← SiteSettings.Footer block
  └─────────────────────┘
```

This can be modeled as a `SiteSettingsDocument : Entity` stored in Marten:

```csharp
public sealed class SiteSettingsDocument : Entity
{
    public List<BlockInstance> GlobalBlocks { get; set; } = [];
    // "header", "footer" — resolved by ComponentAlias
}
```

The page renderer prepends and appends global blocks around the page's own blocks.

---

### What this example demonstrates

| Concept | Where shown |
|---------|-------------|
| `ComponentDefinition` as schema | FooterComponent.Definition — no rendering, no reflection |
| `BlockInstance` as stored data | JSON block with props — pure data |
| `IBlockRenderer` with DI | FooterBlockRenderer — registered in module, resolved by ComponentAlias |
| `Context.Get<T>()` AOT-safe access | `context.Get<string>("companyName")`, `context.Get<List<FooterColumn>>("columns")` |
| `BlockJsonContext` source generation | `[JsonSerializable(typeof(List<FooterColumn>))]` for AOT deserialization |
| Module registration pattern | LayoutModule — registers renderer + component definition |
| Admin UI auto-generation | `ComponentDefinition.Props` → editor controls |
| Global reusable blocks | SiteSettingsDocument — edit once, render everywhere |

---

## 12. Integration with Existing Codebase

This design is ~95% additive. The existing codebase does not need to be restructured.

### 12.1 What's already there (found during codebase analysis)

| Existing artifact | File | How it's used |
|-------------------|------|---------------|
| `IAeroModule.Configure(IAeroModuleBuilder)` | `Aero/src/Aero.Modular/IAeroModule.cs:66` | Modules register content types, field editors, content parts |
| `IAeroModuleBuilder.AddContentType(string)` | `Aero/src/Aero.Modular/IModuleBuilder.cs:35` | Exists — fill with `ContentTypeDefinition` metadata |
| `IAeroModuleBuilder.AddContentPart<TPart>()` | `IModuleBuilder.cs:40` | Exists as empty marker — implement `IContentPart` |
| `IAeroModuleBuilder.AddFieldEditor<TEditor>()` | `IModuleBuilder.cs:45` | Exists — implement `IContentFieldEditor` |
| `IContentDefinitionModule : IAeroModule` | `Aero/src/Aero.Modular/IAeroModule.cs:120` | Marker for content-type modules — implement it |
| `AeroModuleBase.Configure(IAeroModuleBuilder)` | `Aero/src/Aero.Modular/AeroModuleBase.cs:53` | Virtual — override in modules |
| `PageRouteHandler.MapPageRoutes()` | `Pages/PageRouteHandler.cs:15` | Existing `GET /{slug}` for `PageDocument` |
| `IPageContentService.FindBySlugAsync()` | `Pages/PageContentService.cs:21` | Existing page resolution |
| `ContentSlugDocument` | `Pages/SlugRegistry.cs:13` | Slug uniqueness — add `ContentItem = 3` to `ContentSlugOwnerType` |
| `BlockBase` hierarchy | `Abstractions/Blocks/BlockBase.cs` | Unchanged — `ContentItem` is parallel, not replacement |
| `DynamicTemplateBlock` | `Abstractions/Blocks/Common/DynamicTemplateBlock.cs` | Unchanged — receives bridged data |
| `DynamicBlockDefinition` | `Core/Blocks/Dynamic/DynamicBlockDefinition.cs` | Unchanged — auto-generated templates stored here |
| `ISecureScribanRenderer` | `Core/Blocks/Dynamic/ISecureScribanRenderer.cs` | Unchanged — renders bridged templates |
| `BlockRendererGenerator` | `SourceGenerators/BlockRendererGenerator.cs` | Pattern to follow for `ContentTypeGenerator` |

### 12.2 Integration points that need a small change

Only **one enum value** and **two route registrations** touch existing code:

```csharp
// 1. ContentSlugDocument.cs — add one value
public enum ContentSlugOwnerType
{
    Page = 0,
    BlogPost = 1,
    Custom = 2,
    ContentItem = 3   // ← new
}
```

```csharp
// 2. PageRouteHandler.cs — add ContentItem fallback in the handler,
//    OR register a separate catch-all route that ASP.NET routing
//    automatically prioritizes below the existing /{slug} route.
//    See §4.1 for both options.
```

```csharp
// 3. Marten StoreOptions — add schema configs (additive, not breaking)
opts.Schema.For<ContentTypeDocument>()
    .Identity(x => x.Id)
    .DocumentAlias("content_type_definitions")
    .Index(x => x.SiteId);

opts.Schema.For<ContentItem>()
    .DocumentAlias("content_items")
    .Index(x => x.SiteId)
    .Index(x => x.Slug)
    .Index(x => x.ContentTypeAlias);
```

### 12.3 What requires zero changes

- `PageDocument` — unchanged, block-based pages continue working
- `BlogPostDocument` — unchanged (can optionally add `[ContentType]` attribute later)
- `BlockBase` and all 20+ derived block types — unchanged
- `CmsBlockRenderRegistry` — unchanged
- `BlockRenderer.razor` — unchanged
- `DynamicTemplateBlockRenderer` — unchanged
- `IAeroModule` / `AeroModuleBase` — no new methods needed
- `IPageContentService` — unchanged
- `Wolverine` event sourcing — unchanged
- `Orleans` service layer — unchanged

### 12.4 How modules compose

```
AeroModuleBase (existing)
    │
    ├── ConfigureServices()  ← register IContentFieldValidator, IContentFieldEditor, etc.
    ├── Configure(builder)   ← builder.AddContentType("alias"), builder.AddFieldEditor<T>()
    └── Configure(opts)      ← opts.Schema.For<ContentTypeDocument>() (per module if needed)

IContentDefinitionModule : IAeroModule (existing marker)
    └── implement this on content-type modules
```

No new lifecycle methods. No new interfaces on `IAeroModule`. No breaking changes to
existing modules.

### 12.5 Route resolution flow

ContentItems are **typically embedded inside PageDocument blocks**. The
existing `DynamicPageModel` + `Page.cshtml` pipeline handles this without
any changes:

```
Request: GET /about-us
    │
    ▼
DynamicPageModel.OnGetAsync(?Slug=about-us)
    → IPageContentService.FindBySlugAsync("about-us")
    → PageDocument found? → render Page.cshtml with LayoutRegions
        │                        ↓
        │                 Block placements include ContentEmbedBlock
        │                 that references ContentItem by ID
        │                        ↓
        │                 ContentEmbedBlockRenderer fetches ContentItem
        │                 → bridge → DynamicTemplateBlock → Scriban → HTML
        │
    → not found → 404
```

A standalone `GET /{**slug}` route for ContentItem is optional — only needed
if you want structured content to have its own public URL (e.g.
`/team/alice` renders a "team-member" ContentItem directly). In that case,
register it as a catch-all with lower precedence than the existing
`/{slug}` route.

### 12.6 Summary of integration surface

```
Integration point              Change type
─────────────────────────────────────────────────
ContentTypeDefinition          New (additive)
ContentItem : Entity           New (additive)
ContentTypeDocument (Marten)   New (additive)
IContentFieldValidator         New (additive)
IAsyncContentValidator         New (additive)
IContentFieldEditor            New (fills existing IFieldEditor marker)
DynamicContentValidator        New (additive)
ContentValidationService       New (additive)
ContentCommandService          New (additive)
ContentTypeGenerator (src gen) New (additive)
IModuleBuilder usage           Existing — already has the hooks
IAeroModule usage              Existing — Configure(builder) already virtual
IContentDefinitionModule       Existing — just implement it
PageRouteHandler               Small change: add fallback OR separate route
ContentSlugOwnerType           Add 1 enum value
Marten StoreOptions            Add 3 schema configs
PageDocument                   Zero changes
BlockBase hierarchy            Zero changes
DynamicTemplateBlock           Zero changes
ISecureScribanRenderer         Zero changes
```

> **95% additive. 5% extension (routes, slug enum, Marten config). Zero breaking changes.**

---

## 14. Search Indexing

ContentItems have structured fields. Each field type should contribute its value
to the search index for site-wide or content-type-scoped search.

### 14.1 Field indexer interface

```csharp
/// <summary>
/// Contributes field values to the search index.
/// Registered per field type via DI.
/// </summary>
public interface IContentFieldIndexer
{
    string FieldType { get; }

    /// <summary>
    /// Returns one or more text tokens to index from this field's value.
    /// </summary>
    IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value);
}
```

### 14.2 Concrete indexers

```csharp
public sealed class TextFieldIndexer : IContentFieldIndexer
{
    public string FieldType => "text";

    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            yield return value.GetString() ?? "";
    }
}

public sealed class RichTextFieldIndexer : IContentFieldIndexer
{
    public string FieldType => "richtext";

    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            // Strip HTML tags before indexing
            var text = System.Text.RegularExpressions.Regex.Replace(
                value.GetString() ?? "", "<[^>]*>", "");
            yield return text;
        }
    }
}

public sealed class ReferenceFieldIndexer : IContentFieldIndexer
{
    public string FieldType => "reference";

    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        // References contribute the referenced item's title to the parent index.
        // This lookup is handled by the search service, not the indexer.
        // The indexer yields the raw ID for cross-reference indexing.
        if (value.ValueKind == JsonValueKind.String)
            yield return value.GetString() ?? "";
    }
}
```

### 14.3 Index document

When a ContentItem is saved, its field values are extracted into a search index
document. The existing search infrastructure (documented in `docs/07_search_indexing.md`)
consumes these documents.

```csharp
public sealed class ContentSearchDocument
{
    public string Id { get; set; } = string.Empty;  // "content:{siteId}:{contentItemId}"
    public long SiteId { get; set; }
    public long ContentItemId { get; set; }
    public string ContentTypeAlias { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>Concatenated tokens from all indexed fields.</summary>
    public string FullText { get; set; } = string.Empty;

    /// <summary>Per-field tokens for faceted search.</summary>
    public Dictionary<string, List<string>> FieldTokens { get; set; } = [];
}
```

### 14.4 Index population

The `ContentCommandService.SaveAsync/ PublishAsync` trigger index update:

```csharp
public sealed class ContentIndexService(
    IEnumerable<IContentFieldIndexer> indexers,
    IContentTypeService typeService)
{
    public async Task<ContentSearchDocument> BuildIndexAsync(
        ContentItem item, CancellationToken ct = default)
    {
        var typeResult = await typeService.GetByAliasAsync(item.SiteId, item.ContentTypeAlias, ct);
        if (typeResult is Result<ContentTypeDefinition, AeroError>.Failure)
            return new ContentSearchDocument { Id = $"content:{item.SiteId}:{item.Id}" };

        var type = ((Result<ContentTypeDefinition, AeroError>.Ok)typeResult).Value;
        var lookup = indexers.ToDictionary(x => x.FieldType, StringComparer.OrdinalIgnoreCase);

        var doc = new ContentSearchDocument
        {
            Id = $"content:{item.SiteId}:{item.Id}",
            SiteId = item.SiteId,
            ContentItemId = item.Id,
            ContentTypeAlias = item.ContentTypeAlias,
            Slug = item.Slug,
            Title = item.Title ?? ""
        };

        foreach (var field in type.Fields)
        {
            if (!item.Fields.TryGetValue(field.Name, out var element)) continue;
            if (!lookup.TryGetValue(field.FieldType, out var indexer)) continue;

            var tokens = indexer.GetIndexTokens(field, element).ToList();
            doc.FieldTokens[field.Name] = tokens;
            doc.FullText += string.Join(" ", tokens) + " ";
        }

        return doc;
    }
}
```

### 14.5 Module registration

```csharp
services.AddSingleton<IContentFieldIndexer, TextFieldIndexer>();
services.AddSingleton<IContentFieldIndexer, RichTextFieldIndexer>();
services.AddSingleton<IContentFieldIndexer, ReferenceFieldIndexer>();
```

---

## 15. Walkthrough: Building a Custom Field Type

This section walks through adding a **color** field type end-to-end. The color
picker stores a hex value like `#FF6600`.

### 15.1 Editor

```csharp
public sealed class ColorFieldEditor : IContentFieldEditor
{
    public string FieldType => "color";
    public string EditorComponent => "aero-color-picker";
    public object? Normalize(object? value) =>
        value?.ToString()?.TrimStart('#').ToUpperInvariant();
}
```

### 15.2 Validator

```csharp
public sealed class ColorFieldValidator : IContentFieldValidator
{
    private static readonly System.Text.RegularExpressions.Regex HexRegex =
        new("^[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public string FieldType => "color";

    public void ValidateElement(
        ContentFieldDefinition field,
        JsonElement element,
        ContentValidationMode mode,
        ValidationContext<ContentItem> context)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            context.AddFailure(field.Name, $"{field.Label ?? field.Name} must be a hex color.");
            return;
        }

        var value = element.GetString()?.TrimStart('#') ?? "";
        if (!HexRegex.IsMatch(value))
        {
            context.AddFailure(field.Name,
                $"{field.Label ?? field.Name} must be a valid hex color (e.g. #FF6600).");
        }
    }
}
```

### 15.3 Scriban template snippet

```csharp
public sealed class ColorFieldSnippet : IFieldTemplateSnippet
{
    public string FieldType => "color";

    public string Render(ContentFieldDefinition field) => $$"""
    {{ if block.{{field.Name}} }}
    <span class="aero-swatch" style="background-color: #{{ block.{{field.Name}} }}"></span>
    {{ end }}
    """;
}
```

### 15.4 Search indexer

```csharp
public sealed class ColorFieldIndexer : IContentFieldIndexer
{
    public string FieldType => "color";

    public IEnumerable<string> GetIndexTokens(ContentFieldDefinition field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            yield return value.GetString() ?? "";
    }
}
```

### 15.5 Module registration

```csharp
public sealed class ColorFieldModule : AeroModuleBase
{
    public override void ConfigureServices(IServiceCollection services, ...)
    {
        services.AddSingleton<IContentFieldEditor, ColorFieldEditor>();
        services.AddSingleton<IContentFieldValidator, ColorFieldValidator>();
        services.AddSingleton<IFieldTemplateSnippet, ColorFieldSnippet>();
        services.AddSingleton<IContentFieldIndexer, ColorFieldIndexer>();
    }

    public override void Configure(IAeroModuleBuilder builder)
    {
        builder.AddFieldEditor<ColorFieldEditor>();
    }
}
```

### 15.6 What was added

| Interface | File | Purpose |
|-----------|------|---------|
| `IContentFieldEditor` | `ColorFieldEditor.cs` | Admin UI — color picker component |
| `IContentFieldValidator` | `ColorFieldValidator.cs` | Sync validation — hex format check |
| `IFieldTemplateSnippet` | `ColorFieldSnippet.cs` | Scriban rendering — swatch element |
| `IContentFieldIndexer` | `ColorFieldIndexer.cs` | Search — color hex value |

No reflection. No runtime code generation. Every integration point is a
DI-registered service keyed by a `FieldType` string.

---

## 16. Summary

```
Runtime path:
  Manager UI → ContentTypeDefinition + ContentItem → bridge → DynamicTemplateBlock → Scriban → HTML

Dev-time path:
  C# class + [ContentType] → source generator → ContentTypeDefinition → same bridge → same pipeline

Both converge on the same rendering pipeline.
No reflection, no runtime CLR types, no Activator.CreateInstance.
```
