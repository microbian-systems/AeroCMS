# Aero CMS Content Types

## Purpose

This document defines the product and architecture direction for Aero CMS
content types. It is intended for developers and AI agents extending the content
type system.

Current implementation uses:

- `ContentTypeDefinition` in `Aero.Cms.Abstractions`
- `ContentTypeDocument` in `Aero.Cms.Core` for Marten persistence, with a Snowflake `long` id
- `ContentItem` in `Aero.Cms.Abstractions.Content`
- Manager UI pages in `Aero.Cms.Shared/Pages/Manager/ContentTypes`
- Admin APIs in `Aero.Cms.Modules.Content/Areas/Api/v1`

Content type identity follows the wider AeroCMS convention: the persisted
document has a Snowflake `long Id`, and `SiteId` is stored as a separate long
foreign key. Aliases are editor-facing handles, not primary keys. The database
must enforce uniqueness for `(SiteId, Alias)` so different sites can reuse the
same content type alias safely.

Older drafts of this document used `ContentEntryDocument` and described content
types as routable by default. That direction is superseded.

---

## Core Concept

A content type is a schema-defined structured content model with a draft/publish
lifecycle. It is useful for content such as:

- Team members
- Testimonials
- FAQs
- Job postings
- Product attributes
- Case studies
- Event listings

Content types are not full modules and are not page-builder blocks. They sit in
between:

| Capability | Module | Content Type | Block |
|------------|--------|--------------|-------|
| Example | Blog, Commerce, Forum | Team Member, Testimonial | Hero, Image, Feature Grid |
| Domain behavior | Yes | No, beyond CRUD/draft/publish | No |
| Orleans/event workflow | Usually | No by default | No |
| Authored in | Dedicated editor | Generic content editor | Page editor canvas |
| Reused across pages | Sometimes | Yes | Only with parent page/block data |
| Public URL | Yes when module defines one | Optional per content type | No |

Decision rule:

```text
Does this domain need behavior?
├── Yes: build a module
└── No: is it structured content that editors need to reuse?
    ├── Yes: use a content type
    └── No: use a block or page content
```

If every instance needs domain rules, events, workflows, or a dedicated read
model, build a module. If it is structured content authored by non-technical
users, use a content type.

---

## Embedded First

Content items are embedded-first. They are created as reusable structured
entries and rendered from pages, blocks, listings, or module-specific views.

Default behavior:

- A content type does not create public pages automatically.
- A content item can be published and reused without having a public route.
- The page or module that embeds the entry owns layout, navigation, and SEO.

Examples:

```text
PageDocument "About Us"
└── LayoutRegions
    ├── TextBlock
    ├── ContentEmbedBlock ──→ ContentItem "Alice" (team-member)
    ├── ContentEmbedBlock ──→ ContentItem "Bob"   (team-member)
    └── ImageBlock
```

```text
TeamListingBlock
└── Query ContentItems where ContentTypeAlias == "team-member"
    └── Render each published item inline
```

This keeps the non-technical workflow safe. Editors can model reusable content
without also deciding route design, SEO behavior, navigation placement, and slug
collision policy.

---

## Optional Public URLs

Some content types should have standalone detail pages. This is common in CMS
systems:

- A team member can have `/team/alice-smith`.
- A case study can have `/case-studies/retail-launch`.
- An event can have `/events/spring-open-house`.
- A job posting can have `/careers/senior-designer`.

Aero models this as an opt-in content type setting:

```csharp
public bool AllowPublicUrl { get; set; }
```

Manager UI label:

```text
Give each entry its own page
```

When `AllowPublicUrl` is false:

- The content item editor hides the public slug workflow.
- Lists show the entry/type as embedded-only.
- Published items are available to blocks and internal queries, but not as
  public routes.

When `AllowPublicUrl` is true:

- The content item editor asks for a slug.
- The type list labels the type as "Public pages".
- The public routing slice must resolve published content items through a
  site-scoped route/slug registry.

Important: the UI/model toggle exists now. Public request routing for content
items is a separate implementation slice so it can be designed safely around
site scoping, route precedence, and slug collisions.

---

## Domain Model

### `ContentTypeDefinition`

Schema used by services, API contracts, and editor flows.

```csharp
public sealed class ContentTypeDefinition : Entity
{
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Icon { get; set; }
    public bool AllowPublicUrl { get; set; }
    public bool HideFromSearch { get; set; }
    public List<ContentFieldDefinition> Fields { get; set; } = [];
    public string? ScribanTemplate { get; set; }
    public ContentTypeRenderMode RenderMode { get; set; } = ContentTypeRenderMode.DynamicBlock;
    public ContentTypeScheduleConfig? ScheduleConfig { get; set; }
}
```

`AllowPublicUrl` defaults to false because content types are embedded-first.
`HideFromSearch` defaults to false so entries are searchable unless an editor
explicitly hides private, helper, or reused-only content from site search.

### `ContentFieldDefinition`

```csharp
public sealed class ContentFieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public string FieldType { get; set; } = "text";
    public string? Label { get; set; }
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public string? Placeholder { get; set; }
    public Dictionary<string, object?> Settings { get; set; } = [];
}
```

Current manager-supported field types:

| Field type | Editor intent |
|------------|---------------|
| `text` | Short text |
| `richtext` | Longer formatted copy |
| `image` | Image/media URL |
| `number` | Numeric value |
| `boolean` | Yes/no |
| `url` | Link |
| `date` | Date |
| `reference` | Reference to another content item |

### `ContentItem`

```csharp
public sealed class ContentItem : Entity
{
    public long SiteId { get; set; }
    public string ContentTypeAlias { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Title { get; set; }
    public Dictionary<string, JsonElement> Fields { get; set; } = [];
    public ContentPublicationState PublicationState { get; set; } = ContentPublicationState.Draft;
    public DateTimeOffset? PublishedOn { get; set; }
    public int VersionNumber { get; set; }
    public DateTimeOffset? SchedulePublishUtc { get; set; }
    public DateTimeOffset? ScheduleUnpublishUtc { get; set; }
}
```

`Slug` is meaningful only when the associated content type allows public URLs.
It can still be stored on embedded content, but the UI should avoid forcing
non-technical users to think about it unless it matters.

---

## Rendering Model

Content items are rendered through the existing block/rendering infrastructure
where possible.

Primary path:

```text
PageDocument
└── Block references ContentItem
    └── Content type renderer/bridge
        └── DynamicTemplateBlock or generated field display
            └── Scriban / block renderer output
```

Display modes:

| Mode | Use |
|------|-----|
| Automatic display | Safe default; render fields in a standard layout |
| Custom Scriban template | Developer/editor-controlled output for a content type |
| Block layout | Advanced path for mapping fields into block instances |

The manager UI defaults to automatic display and hides custom Scriban behind the
Display tab.

---

## Admin UI

The manager UI lives in:

```text
src/Aero.Cms.Shared/Pages/Manager/ContentTypes/
├── ContentTypeList.razor
├── ContentTypeList.razor.cs
├── ContentTypeEditor.razor
├── ContentTypeEditor.razor.cs
├── ContentItemsList.razor
├── ContentItemsList.razor.cs
├── ContentItemEditor.razor
└── ContentItemEditor.razor.cs
```

Guidelines:

- Use `RadzenDataGrid` for list pages to match pages, docs, and posts.
- Use code-behind files for behavior.
- Use non-technical wording in the primary flow.
- Keep internal handles and templates available, but secondary.
- Do not expose public URL decisions as a default requirement.

The detailed admin UI contract is in `docs/content-type-admin-ui.md`.

---

## Admin API

Implemented endpoints are under `/api/v1/admin`.

| Method | Route |
|--------|-------|
| `GET` | `/api/v1/admin/content-types` |
| `GET` | `/api/v1/admin/content-types/{alias}` |
| `POST` | `/api/v1/admin/content-types` |
| `PUT` | `/api/v1/admin/content-types/{alias}` |
| `DELETE` | `/api/v1/admin/content-types/{alias}` |
| `GET` | `/api/v1/admin/content-items?contentType={alias}` |
| `GET` | `/api/v1/admin/content-items/{alias}/{id}` |
| `POST` | `/api/v1/admin/content-items/{alias}` |
| `PUT` | `/api/v1/admin/content-items/{alias}/{id}` |
| `DELETE` | `/api/v1/admin/content-items/{alias}/{id}` |
| `POST` | `/api/v1/admin/content-items/{alias}/{id}/publish` |
| `POST` | `/api/v1/admin/content-items/{alias}/{id}/unpublish` |

Typed HTTP clients live in
`src/Aero.Cms.Abstractions/Http/Clients/ContentTypesClient.cs`.

---

## Public Routing Future Slice

Public routes for content items should be implemented deliberately, not as a
side effect of the editor UI.

Recommended direction:

1. Keep embedded content as the default.
2. Add a `ContentSlugOwnerType.ContentItem` or equivalent route-owner model only
   when the public route feature is implemented.
3. Scope slug/route reservations by `SiteId`.
4. Preserve route precedence so PageDocument routes continue to win where
   appropriate.
5. Only route published content items whose content type has `AllowPublicUrl`.
6. Decide URL pattern explicitly, for example:
   - `/{typeBasePath}/{slug}` such as `/team/alice`
   - `/content/{type}/{slug}` as a simpler, lower-risk default

Open design questions for that future slice:

- Should each content type define a base path?
- Should public content item routes participate in navigation menus?
- Should content item routes use the existing slug registry or a new route
  registry abstraction?
- What canonical URL should be emitted when an entry is embedded on a page and
  also has a standalone URL?

---

## Non-Developer Path

```text
Create content type
→ Add fields
→ Leave public pages off unless a standalone page is needed
→ Save
→ Create entries
→ Save draft / publish
→ Embed entries in pages, blocks, or listings
```

This is the main Aero CMS content-type workflow.

---

## Developer Path

Developers can extend the system by adding:

- New field types
- Field validators
- Field renderers
- Search indexers
- Scriban snippets
- Optional public route handling
- Source-generator support for developer-defined content types

Source generators remain the preferred direction for developer-defined content
types and module registration. Avoid reflection-based discovery.

---

## Summary

Content types provide reusable, schema-driven structured content for
non-technical editors. They are embedded-first, public-route-optional, and
designed to be safer than making every structured entry a page by default.
