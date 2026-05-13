# Aero CMS — Content Types

## Purpose of this document

This document defines the content type system for Aero CMS — what content types are, when to use them instead of modules or blocks, how they are defined via source generators, how they render, and how they integrate with the block system and public-facing Razor Pages. It is intended as a reference for AI agents and developers implementing or extending the content type system.

---

## 1. Core Concept

### What is a Content Type?

A content type is a **schema-defined, admin-configurable structured data entry** with a draft/publish lifecycle. Content types are not page elements (blocks) and not full domain modules. They occupy the middle ground: structured content that needs authoring UI and a public URL but has no domain behavior beyond CRUD and draft/publish.

### The Decision Rule

Before creating a content type, apply this test:

```
Does this domain need behavior?
├── Yes — events, grain state, domain rules, lifecycle beyond draft/publish
│     └── BUILD A MODULE (Pages, Blog, Forum, Commerce, etc.)
└── No — just structured data that gets authored and rendered
      └── Does every instance need its own public URL?
            ├── Yes → Content Type with ContentEntry.cshtml route
            └── No  → Block type (rendered inside a PageDocument)
```

**Would this need an Orleans grain?** If yes → module. If no → content type.

### Content Types vs Blocks vs Modules

| | Module | Content Type | Block |
|---|---|---|---|
| Example | Blog, Commerce, Forum | Team Member, Job Posting | Hero, Feature Grid |
| Has behavior | Yes — grains, events | No — CRUD + draft/publish | No |
| Has own URL | Yes | Yes (`/content/{type}/{slug}`) | No — lives inside a page |
| Authored in | Dedicated editor | Generic content editor | Page editor canvas |
| Lifecycle | Domain-specific | Draft / Publish | With parent PageDocument |
| Query patterns | Complex, domain-specific | Simple fetch by type + slug | N/A |

---

## 2. When to Use Content Types

### Use content types for:

- **Simple structured content with no behavior** — FAQ entries, testimonials, job postings, team members, event listings
- **Speed** — faster than building a full module; no grain, no event stream, no dedicated page model
- **Non-developer authoring** — the admin UI generates forms automatically from the schema; marketing can define "Testimonial" with `quote`, `author`, `company`, `photo` fields and get a working editor with zero developer involvement
- **Per-customer extensibility** — extending a module's data without modifying the module (e.g. product attributes per vertical)

### Do NOT use content types for:

- Anything with **domain logic** (pricing rules, inventory management, order state machines, vote tallying)
- Anything that needs **its own event stream** (forum posts, blog posts)
- Anything that **other modules react to** via Wolverine events
- Anything requiring **complex queries** (full-text search across joins, aggregations)

### The Commerce Example

The product *catalog* is a module (hard-coded, Orleans grain):

```csharp
// Aero.Cms.Modules.Commerce — never changes
public sealed class ProductGrain : Grain
{
    public string Sku { get; private set; }
    public decimal Price { get; private set; }
    public int Inventory { get; private set; }        // behavior lives here
    public long? AttributeEntryId { get; private set; } // FK to content type
}
```

The product *attributes* are a content type (flexible, per-vertical):

```
Customer A (textile):     fabric_weight, thread_count, weave_pattern
Customer B (electronics): voltage, wattage, certifications, connector_type
Customer C (wine):        vintage_year, appellation, varietal, tasting_notes
```

Each customer defines their schema in the admin UI. Zero module changes. Zero deployments.

---

## 3. Domain Model

### `ContentTypeDocument`

Defines the schema for a content type. Stored as a Marten document, event-sourced.

```csharp
public sealed class ContentTypeDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public string Handle { get; set; } = string.Empty;      // "team-member"
    public string DisplayName { get; set; } = string.Empty; // "Team Member"
    public string? Description { get; set; }
    public List<ContentFieldDefinition> Fields { get; set; } = [];
    public bool AllowPublicUrl { get; set; } = true;        // enables /content/{handle}/{slug} route
}
```

### `ContentFieldDefinition`

```csharp
public sealed record ContentFieldDefinition
{
    public string Handle { get; init; } = string.Empty;   // "job_title"
    public string Label { get; init; } = string.Empty;    // "Job Title"
    public ContentFieldType Type { get; init; }
    public bool Required { get; init; }
    public int SortOrder { get; init; }
    public JsonObject? Validation { get; init; }           // type-specific rules
}

public enum ContentFieldType
{
    Text,
    RichText,
    Number,
    Boolean,
    Date,
    Media,        // references a MediaDocument by ID
    Reference,    // references another ContentEntryDocument
    Json          // arbitrary JSON — escape hatch for complex nested data
}
```

### `ContentEntryDocument`

An instance of a content type. Stores data as a `JsonObject` validated against the schema at write time.

```csharp
public sealed class ContentEntryDocument : Entity, ISiteOwned
{
    public long SiteId { get; set; }
    public string ContentTypeHandle { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public JsonObject Data { get; set; } = new();
    public ContentEntryState State { get; set; } = ContentEntryState.Draft;
    public long? NavMenuOverrideId { get; set; }
    public long? CreatedByUserId { get; set; }
    public long? ModifiedByUserId { get; set; }
}

public enum ContentEntryState { Draft, Published, Archived }
```

---

## 4. Source Generators

Content types use Aero's source generator pipeline. Two attributes drive generation:

### `[ContentType]` — Schema Definition

Applied to a C# record. The generator emits:
- `GetDefinition()` — static factory returning the `ContentTypeDocument`
- `FromEntry()` — strongly typed deserializer from `ContentEntryDocument.Data`
- `Validate()` — compiled validation method, no reflection

```csharp
[ContentType("product-attributes")]
public sealed record ProductAttributes
{
    [ContentField(ContentFieldType.Text, Required = true)]
    public string Material { get; init; } = string.Empty;

    [ContentField(ContentFieldType.Text)]
    public string[] Sizes { get; init; } = [];

    [ContentField(ContentFieldType.Text)]
    public string[] Colors { get; init; } = [];

    [ContentField(ContentFieldType.RichText)]
    public string CareInstructions { get; init; } = string.Empty;
}
```

**Generated output:**

```csharp
// GENERATED
public sealed partial record ProductAttributes
{
    public static ContentTypeDocument GetDefinition() => new()
    {
        Handle = "product-attributes",
        DisplayName = "Product Attributes",
        Fields =
        [
            new ContentFieldDefinition
            {
                Handle = "material",
                Label = "Material",
                Type = ContentFieldType.Text,
                Required = true,
                SortOrder = 0
            },
            new ContentFieldDefinition
            {
                Handle = "sizes",
                Label = "Sizes",
                Type = ContentFieldType.Text,
                Required = false,
                SortOrder = 1
            },
            // ...
        ]
    };

    // Strongly typed — no reflection, AOT safe
    public static ProductAttributes FromEntry(ContentEntryDocument entry) => new()
    {
        Material         = entry.Data["material"]?.GetValue<string>() ?? string.Empty,
        Sizes            = entry.Data["sizes"]?.AsArray()
                               .Select(x => x?.GetValue<string>() ?? string.Empty)
                               .ToArray() ?? [],
        Colors           = entry.Data["colors"]?.AsArray()
                               .Select(x => x?.GetValue<string>() ?? string.Empty)
                               .ToArray() ?? [],
        CareInstructions = entry.Data["care_instructions"]?.GetValue<string>() ?? string.Empty
    };

    // Compiled validation — no reflection
    public static ValidationResult Validate(JsonObject data)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(data["material"]?.GetValue<string>()))
            errors.Add(new ValidationError("material", "Material is required."));
        return new ValidationResult(errors);
    }
}
```

### `[ContentTypeRenderer]` — Renderer Registration

Applied to a partial class implementing `IContentTypeRenderer`. The generator emits the `IContentTypeRenderer` bridge — the glue between `ContentEntryDocument` and the typed `Render(T model)` method.

```csharp
[ContentTypeRenderer("product-attributes")]
public sealed partial class ProductAttributesRenderer : IContentTypeRenderer
{
    // You write only the render logic — strongly typed model, not JsonObject
    public RenderFragment Render(ProductAttributes model) => builder =>
    {
        // Blazor render tree for editor preview
    };
}
```

**Generated output:**

```csharp
// GENERATED
public sealed partial class ProductAttributesRenderer : IContentTypeRenderer
{
    public string ContentTypeHandle => "product-attributes";

    public Task<RenderFragment> RenderAsync(
        ContentTypeDocument definition,
        ContentEntryDocument entry)
    {
        var model = ProductAttributes.FromEntry(entry);
        return Task.FromResult(Render(model));
    }
}
```

### Assembly-Level Registration Generator

Scans all `[ContentTypeRenderer]` in the assembly and emits a DI registration extension:

```csharp
// GENERATED — per module assembly
public static class CommerceModuleContentTypeExtensions
{
    public static IServiceCollection AddCommerceContentTypeRenderers(
        this IServiceCollection services)
    {
        services.AddSingleton<IContentTypeRenderer, ProductAttributesRenderer>();
        services.AddSingleton<IContentTypeRenderer, ProductSpecificationsRenderer>();
        return services;
    }
}
```

Called from the module's `IAeroModule.ConfigureServices`:

```csharp
public sealed class CommerceModule : IAeroModule
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCommerceContentTypeRenderers(); // generated
    }
}
```

### Schema Registry Generator

Emits a `FrozenDictionary` of validators for grain-time validation. No reflection at runtime:

```csharp
// GENERATED
public static class CommerceContentTypeSchemas
{
    private static readonly FrozenDictionary<string, Func<JsonObject, ValidationResult>>
        _validators = new Dictionary<string, Func<JsonObject, ValidationResult>>
        {
            ["product-attributes"]     = ProductAttributes.Validate,
            ["product-specifications"] = ProductSpecifications.Validate,
        }.ToFrozenDictionary();

    public static ValidationResult Validate(string handle, JsonObject data)
        => _validators.TryGetValue(handle, out var validator)
            ? validator(data)
            : ValidationResult.NoSchema;
}
```

### AOT Safety

All generated code uses explicit property assignments and compiled lambdas. No `JsonSerializer.Deserialize<T>()`, no reflection. Fully compatible with AOT compilation (`Cms.Api` publish target).

---

## 5. Renderer Pipeline

### `IContentTypeRenderer`

```csharp
public interface IContentTypeRenderer
{
    string ContentTypeHandle { get; }
    Task<RenderFragment> RenderAsync(
        ContentTypeDocument definition,
        ContentEntryDocument entry);
}
```

### `ContentTypeRendererResolver`

Three-tier fallback. Always tries registered → template → generic:

```csharp
public sealed class ContentTypeRendererResolver(
    IEnumerable<IContentTypeRenderer> registered,
    ITemplateResolver templates)
{
    // For Blazor editor — returns RenderFragment
    public async Task<RenderFragment> ResolveAsync(
        ContentTypeDocument definition,
        ContentEntryDocument entry)
    {
        var renderer = registered
            .FirstOrDefault(r => r.ContentTypeHandle == definition.Handle);
        if (renderer is not null)
            return await renderer.RenderAsync(definition, entry);

        // Falls through to generic Blazor renderer
        return RenderGenericFragment(definition, entry);
    }

    // For Razor Pages public site — returns IHtmlContent
    public async Task<IHtmlContent> RenderViewAsync(
        ContentTypeDocument definition,
        ContentEntryDocument entry,
        ViewContext viewContext)
    {
        // Tier 1 — registered C# renderer with a .cshtml partial override
        var partialName = $"ContentTypes/_{definition.Handle}";
        if (await templates.ExistsAsync(partialName))
            return await viewContext.RenderPartialAsync(partialName, entry);

        // Tier 2 — generic field-by-field HTML fallback
        return RenderGenericHtml(definition, entry);
    }

    private IHtmlContent RenderGenericHtml(
        ContentTypeDocument definition,
        ContentEntryDocument entry)
    {
        var builder = new HtmlContentBuilder();
        foreach (var field in definition.Fields)
        {
            var value = entry.Data[field.Handle];
            if (value is null) continue;

            builder.AppendHtml(field.Type switch
            {
                ContentFieldType.Text =>
                    $"<p class=\"aero-field aero-field--text\">{value.GetValue<string>()}</p>",
                ContentFieldType.RichText =>
                    $"<div class=\"aero-field aero-field--richtext\">{value.GetValue<string>()}</div>",
                ContentFieldType.Media =>
                    $"<img class=\"aero-field aero-field--media\" src=\"{value.GetValue<string>()}\" />",
                ContentFieldType.Date =>
                    $"<time class=\"aero-field aero-field--date\">{value.GetValue<DateTimeOffset>():MMM d, yyyy}</time>",
                _ => string.Empty
            });
        }
        return builder;
    }
}
```

### The Three Tiers Explained

**Tier 1 — Registered `IContentTypeRenderer` (C# class)**

Used when the rendering has **logic** — conditional UI, component composition, data lookups at render time, or complex visual output that can't be expressed in a Razor partial.

Example use cases:
- Product attributes (needs to check inventory state for size picker availability)
- Team member card (needs to resolve a `MediaDocument` for the photo)
- Any content type owned by a module that wants full control over its rendering

**Tier 2 — Razor partial by convention**

Used when the rendering is **simple Razor markup** with no logic. A developer drops a `.cshtml` file in the right location — no C# class required.

Convention path: `/Views/Shared/ContentTypes/_{handle}.cshtml`

Example:
```cshtml
@* /Views/Shared/ContentTypes/_team-member.cshtml *@
@model ContentEntryDocument

@{
    var name     = Model.Data["name"]?.GetValue<string>();
    var role     = Model.Data["role"]?.GetValue<string>();
    var bio      = Model.Data["bio"]?.GetValue<string>();
    var photoUrl = Model.Data["photo"]?.GetValue<string>();
}

<div class="team-member">
    <img src="@photoUrl" alt="@name" class="team-member__photo rounded-full size-24" />
    <div class="team-member__info ms-4">
        <h3 class="text-base font-semibold">@name</h3>
        <p class="text-sm text-muted-foreground">@role</p>
        <div class="text-sm mt-2">@Html.Raw(bio)</div>
    </div>
</div>
```

**Tier 3 — Generic field renderer**

Zero configuration. The resolver iterates `ContentTypeDocument.Fields` and renders each field by its `ContentFieldType`. Functional output, not beautiful — sufficient for non-developer users who define a content type in the admin UI and want their data displayed without any code.

This tier is the zero-friction path for SMB users.

---

## 6. Public Route — `ContentEntry.cshtml`

Content entries are routable at `/content/{type}/{slug}`. This follows the same pattern as `Page.cshtml` and `Post.cshtml`.

### Route Convention

```
/pages/{slug}            → Page.cshtml        (PageDocument)
/blog/{slug}             → Post.cshtml        (BlogPostDocument)
/content/{type}/{slug}   → ContentEntry.cshtml (ContentEntryDocument)
```

### Page Model

```csharp
public sealed class ContentEntryModel : PageModel
{
    private readonly IClusterClient _orleans;
    private readonly NavMenuContext _navMenuContext;

    public ContentEntryModel(IClusterClient orleans, NavMenuContext navMenuContext)
    {
        _orleans = orleans;
        _navMenuContext = navMenuContext;
    }

    public ContentTypeDocument Definition { get; private set; } = default!;
    public ContentEntryDocument Entry { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(string type, string slug)
    {
        var grain = _orleans.GetGrain<IContentEntryGrain>($"{type}/{slug}");
        var result = await grain.GetPublishedAsync();

        if (result is null)
            return NotFound();

        await _navMenuContext.OverrideIfNeededAsync(
            result.Entry.NavMenuOverrideId,
            HttpContext.RequestServices.GetRequiredService<INavMenuService>());

        Definition = result.Definition;
        Entry = result.Entry;

        return Page();
    }
}
```

### `ContentEntry.cshtml`

```cshtml
@page "/content/{type}/{slug}"
@model ContentEntryModel
@inject ContentTypeRendererResolver RendererResolver

@{
    Layout = "_CmsLayout";
    ViewData["Title"] = Model.Entry.Data["title"]?.GetValue<string>() ?? string.Empty;
    ViewData["MetaDescription"] = Model.Entry.Data["meta_description"]?.GetValue<string>();
}

@section Head {
    @if (ViewData["MetaDescription"] is string metaDesc)
    {
        <meta name="description" content="@metaDesc" />
    }
    <link rel="canonical" href="/content/@Model.Definition.Handle/@Model.Entry.Slug" />
}

@await RendererResolver.RenderViewAsync(Model.Definition, Model.Entry, ViewContext)
```

The page is a pure dispatch surface — it knows nothing about specific content types. The resolver owns all tier logic.

---

## 7. Integration with the Block System

Content type entries integrate with pages via the `ContentEntryBlock`:

```csharp
[BlockMetadata("content_entry", "Content Entry", Category = "Content")]
public sealed class ContentEntryBlock : BlockBase
{
    public override string BlockType => "content_entry";

    public string ContentTypeHandle { get; set; } = string.Empty;
    public long ContentEntryId { get; set; }
    public string? TemplateOverride { get; set; }

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
```

This allows a `PageDocument` to embed a content type entry inline — rendered via the same three-tier resolver pipeline, displayed within the page's block layout rather than at its own URL.

### Full T-Shirt Product Example

```
ProductGrain (hard-coded module — behavior)
├── Sku: "TSHIRT-BLK-L"
├── Price: 29.99
├── Inventory: 142             ← grain owns this behavior
└── AttributeEntryId: 42      ← FK to content type entry

ContentEntryDocument (id: 42)
├── ContentTypeHandle: "product-attributes"
└── Data:
      ├── sizes: ["S","M","L","XL"]
      ├── colors: ["Black","White","Navy"]
      ├── material: "100% Cotton"
      └── care_instructions: "Machine wash cold"

PageDocument (Product Page)
└── Blocks:
      ├── ProductGalleryBlock     ← registered commerce block
      ├── PriceBlock              ← registered commerce block
      ├── AddToCartBlock          ← registered commerce block
      └── ContentEntryBlock       ← renders AttributeEntryId: 42
            └── ProductAttributesRenderer (Tier 1)
                  └── size picker, material badge, care label
```

---

## 8. Admin UI — Automatic Form Generation

The Blazor editor generates form UI automatically from `ContentTypeDocument.Fields`. This is the zero-developer path for non-technical users.

For each field in `Fields`, the editor renders the appropriate NeoUI input:

```
ContentFieldType.Text       → <Input>
ContentFieldType.RichText   → <RichTextEditor>
ContentFieldType.Number     → <NumericInput>
ContentFieldType.Boolean    → <Switch>
ContentFieldType.Date       → <DatePicker>
ContentFieldType.Media      → <AeroMediaPicker>
ContentFieldType.Reference  → <AeroContentEntryPicker>
ContentFieldType.Json       → <Textarea> (raw JSON fallback)
```

A non-developer can:
1. Navigate to Content Types in the admin UI
2. Click "New Content Type"
3. Name it "Testimonial", add fields: `quote` (RichText), `author` (Text), `company` (Text), `photo` (Media)
4. Save the schema
5. Navigate to Content Entries → New → Testimonial
6. Fill in the form
7. Publish
8. The entry is available at `/content/testimonial/{slug}` via the Tier 3 generic renderer with zero code written

---

## 9. Non-Developer vs Developer Paths

```
Non-developer (admin UI only)
──────────────────────────────
Define schema in UI          → ContentTypeDocument saved to Marten
Create entries in UI         → ContentEntryDocument saved to Marten
Publish                      → Available at /content/{type}/{slug}
Rendered by                  → Tier 3 generic field renderer
Output                       → Functional, unstyled fields

Developer (code path)
──────────────────────────────
Define [ContentType] record  → Source generator emits schema + deserializer
Define [ContentTypeRenderer] → Source generator emits IContentTypeRenderer bridge
Write Render(T model)        → Full control over Blazor RenderFragment (editor)
Write _handle.cshtml         → Full control over public HTML output (Tier 2)
OR write IContentTypeRenderer → Full control via Tier 1 C# renderer
```

Both paths use the same `ContentEntryDocument` storage, same draft/publish lifecycle, same public route. The difference is only in rendering fidelity and developer involvement.

---

## 10. Value Proposition Summary

Content types exist to answer three specific needs:

**1. No-code structured content for non-developers**
Admin UI → schema → form → publish → rendered. Zero developer involvement for simple structured data.

**2. Speed for developers on simple structured content**
A `[ContentType]` record + optional Razor partial = routable, draft/published, editable content in under 30 minutes. No module, no grain, no event stream, no dedicated page model.

**3. Per-customer module extensibility without module changes**
Modules (Commerce, Docs, etc.) link to `ContentEntryDocument` for customer-specific extended fields. Each customer configures their own schema. The module never changes.

The rule: if it needs a grain → build a module. If it's just data that gets authored and rendered → use a content type.
