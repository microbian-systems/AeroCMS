# Content Type Implementation

## Runtime-defined content types without reflection for Aero CMS

---

## 1. Architecture Overview

The content type system bridges two worlds:

- **Developer-defined types**: C# entity classes + source generators → compile-time metadata
- **Runtime-defined types**: Manager UI → database schema → runtime metadata

Both converge on the same internal model and the same rendering pipeline.

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

**Minimal API endpoint:**

```csharp
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

    var htmlResult = await scribanRenderer.RenderAsync(definition, block.Data, ct);
    if (htmlResult is Result<string, AeroError>.Failure renderFail)
        return Results.Problem(renderFail.Error.Message);

    var html = ((Result<string, AeroError>.Ok)htmlResult).Value;
    return Results.Content(html, "text/html");
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
uses strict validation.

```csharp
public sealed class ContentCommandService(
    ContentValidationService validation,
    IContentService contentService)
{
    public async Task<Result<ContentItem, IReadOnlyList<ValidationFailure>>> SaveDraftAsync(
        ContentItem item, CancellationToken ct = default)
    {
        var result = await validation.ValidateAsync(item, ContentValidationMode.Draft, ct);
        if (result is Result<ContentItem, IReadOnlyList<ValidationFailure>>.Failure f)
            return f.Error;

        return await contentService.SaveAsync(item, ct);
    }

    public async Task<Result<ContentItem, IReadOnlyList<ValidationFailure>>> PublishAsync(
        ContentItem item, CancellationToken ct = default)
    {
        var result = await validation.ValidateAsync(item, ContentValidationMode.Publish, ct);
        if (result is Result<ContentItem, IReadOnlyList<ValidationFailure>>.Failure f)
            return f.Error;

        item.PublicationState = ContentPublicationState.Published;
        item.PublishedOn = DateTimeOffset.UtcNow;

        return await contentService.SaveAsync(item, ct);
    }
}
```

---

## 9. Module Registration

Add to the existing `IAeroModule` interface (one new method):

```csharp
public interface IAeroModule
{
    void ConfigureServices(IServiceCollection services, ...);
    void Configure(IServiceProvider services, StoreOptions opts);
    void ConfigureMarten(StoreOptions opts);

    // New:
    void ConfigureContentTypes(ContentTypeRegistry registry);
}
```

Module example:

```csharp
[Module(nameof(MarketingModule))]
public sealed class MarketingModule : AeroModuleBase
{
    public override void ConfigureServices(IServiceCollection services, ...)
    {
        // Field rendering (template snippets)
        services.AddSingleton<IFieldTemplateSnippet, TextFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, ImageFieldSnippet>();

        // Field validation
        services.AddSingleton<IContentFieldValidator, TextFieldValidator>();
        services.AddSingleton<IContentFieldValidator, NumberFieldValidator>();
        services.AddSingleton<IContentFieldValidator, ReferenceFieldValidator>();

        // Field editors (admin UI)
        services.AddSingleton<IContentFieldEditor, TextFieldEditor>();
        services.AddSingleton<IContentFieldEditor, ReferenceFieldEditor>();

        // Async validators
        services.AddSingleton<IAsyncContentValidator, ReferenceExistenceValidator>();

        // Block renderers (for BlockLayout mode)
        services.AddSingleton<IBlockRenderer, HeroBlockRenderer>();
    }

    public override void ConfigureContentTypes(ContentTypeRegistry registry)
    {
        registry.Register(new ContentTypeDefinition
        {
            Alias = "landing-page",
            Name = "Landing Page",
            Fields =
            {
                new() { Name = "HeroTitle", FieldType = "text", Required = true },
                new() { Name = "HeroImage", FieldType = "image" },
                new() { Name = "CallToActionUrl", FieldType = "url" }
            }
        });
    }
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
| `IAeroModuleBuilder` | Registration surface for content types, parts, editors |
| `IAeroModule` | Module lifecycle — add `ConfigureContentTypes` |
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

## 12. Summary

```
Runtime path:
  Manager UI → ContentTypeDefinition + ContentItem → bridge → DynamicTemplateBlock → Scriban → HTML

Dev-time path:
  C# class + [ContentType] → source generator → ContentTypeDefinition → same bridge → same pipeline

Both converge on the same rendering pipeline.
No reflection, no runtime CLR types, no Activator.CreateInstance.
```
