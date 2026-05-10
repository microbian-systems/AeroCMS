# Content Type Admin UI — Design Spec

## Guiding Principles

1. **Purely additive** — zero changes to existing pages, layouts, or styles
2. **Follow existing patterns** — match `Pages.razor`, `Posts.razor`, `PageEditor.razor` conventions exactly
3. **Nav additions are minimal** — one new section in the sidebar, no top-bar changes
4. **Dynamic forms driven by schema** — content item editors render fields from `ContentFieldDefinition` at runtime

---

## Route Map

| Route | Page Component | Purpose |
|-------|---------------|---------|
| `/manager/content-types` | `ContentTypeList.razor` | List all content type schemas |
| `/manager/content-type/editor/{alias?}` | `ContentTypeEditor.razor` | Define fields, validation, Scriban template |
| `/manager/content/{alias}` | `ContentItemsList.razor` | Browse content items of a given type |
| `/manager/content/{alias}/editor/{id?}` | `ContentItemEditor.razor` | Edit a content item via dynamic field form |

All pages live in `Aero.Cms.Shared/Pages/Manager/ContentTypes/` (WASM-compatible), share `@layout ManagerShellLayout` via the parent `_Imports.razor`, and use `[Authorize]`.

---

## Content Item Model: Slug is Optional, Embedding is Primary

Content items are **embedded data**, not standalone pages. The `ContentItem.Slug` field is **optional** — it only matters if you need a standalone public URL for that item (e.g. `/team/alice`). The default and primary pattern is embedding inside `PageDocument` blocks:

```
PageDocument "About Us"
└── LayoutRegions
    ├── TextBlock
    ├── TeamListingBlock ──→ fetches ALL ContentItems of type "team-member"
    │                       └── renders each via Scriban inline
    └── ContentEmbedBlock ──→ renders single ContentItem by ID
```

Content items are **not pages** — they don't have layouts, navigation, regions, or SEO metadata. They are structured field bags that get rendered wherever a block references them.

When used in a public Razor Page (`.cshtml` in `Aero.Cms.Modules.Content`), the route is **type-indexed**, not slug-indexed:

| Route | Purpose | How |
|-------|---------|-----|
| `/products` | List all product items | Query `IContentQueryService.GetByTypeAsync("product")` |
| `/team` | List all team member items | Query `"team-member"` type |
| `/{slug}` | *Optional* catch-all for detail pages | Only if `ContentItem.Slug` is set |

The slug field on a `ContentItem` is purely for the **exceptional** case where structured content needs its own URL. In normal use, content items exist only within the context of the pages that embed them.

---

## Page 1: ContentTypeList — `/manager/content-types`

## Page 1: ContentTypeList — `/manager/content-types`

**Pattern:** Identical to existing `Pages.razor`.

**Header:**
- Title: "Content Types"
- Subtitle: "Manage structured content schemas for your site."
- Search bar (`RadzenTextBox`)
- "New Content Type" button → `/manager/content-type/editor`

**Grid:** `RadzenDataGrid<ContentTypeSummary>` with server-side pagination via `LoadData`:

| Column | Source | Render |
|--------|--------|--------|
| Name | `def.Name` | Bold text |
| Alias | `def.Alias` | `<span class="text-xs font-mono bg-gray-100 px-2 py-1 rounded">` |
| Fields | `def.Fields.Count` | Badge: `"{n} fields"` |
| Category | `def.Category` | Badge if set |
| Items | API count | Number of content items of this type |
| Status | ScribanTemplate presence | "Custom template" / "Auto-generated" badge |

**Actions:**
- Row click → navigate to `/manager/content-type/editor/{alias}`
- Delete → `RadzenConfirmDialog` → `DELETE /api/manager/content-types/{alias}`
- "New Content Type" → `/manager/content-type/editor`

**Route registration in `AeroContentModule.ConfigureServices()`:**
```csharp
services.Configure<RazorPagesOptions>(options =>
{
    options.Conventions.AddAreaPageRoute("Content", "/ContentTypeList", "/manager/content-types");
});
```

---

## Page 2: ContentTypeEditor — `/manager/content-type/editor/{alias?}`

**Pattern:** Tabbed editor similar to `PageEditor.razor` but with two tabs: "Definition" and "Template".

### Tab 1: Definition

**Meta section:**

| Field | Component | Rules |
|-------|-----------|-------|
| Name | `<RadzenTextBox>` | Required, max 256 |
| Alias | `<RadzenTextBox>` | Auto-gen from Name, editable. Pattern: `^[a-z][a-z0-9_-]*$`, max 128 |
| Description | `<RadzenTextArea>` | Optional, max 512 |
| Category | `<RadzenDropDown>` | Free-text or select existing |
| Render Mode | `<RadzenDropDown>` | `DynamicBlock` (default), `BlockLayout` |

**Fields section — the core UI:**

A dynamic sortable list of field definitions:

```
┌─────────────────────────────────────────────────────────────────┐
│  Fields                                                         │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ ⠿ Name: [Title    ]  Type: [text     ▼]  Req: [x]  [🗑]   ││
│  │ ⠿ Name: [Excerpt  ]  Type: [richtext ▼]  Req: [ ]  [🗑]   ││
│  │ ⠿ Name: [HeroImage]  Type: [image    ▼]  Req: [ ]  [🗑]   ││
│  └─────────────────────────────────────────────────────────────┘│
│  [+ Add Field]                                                  │
│                                                                │
│  → Field Settings (when a row is selected):                    │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ Label: [Hero Title]                                         ││
│  │ Default: [Welcome]                                          ││
│  │ Placeholder: [Enter title...]                               ││
│  │ ── Validation ──                                            ││
│  │ Max Length: [80]   Min Length: [2]                          ││
│  │ Regex: [^[a-zA-Z].*]                                        ││
│  └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

**FieldType dropdown** populated dynamically from all `IContentFieldEditor` DI registrations (reading their `FieldType` property): text, richtext, image, number, boolean, url, date, reference.

**Selected-row Settings panel** shows field-type-specific options from `ContentFieldDefinition.Settings`.

### Tab 2: Template

- `ScribanTemplate` textarea with monospace font
- "Auto-generate from fields" button (populates from `ContentTypeTemplateGenerator`)
- Preivew section showing rendered Scriban output (future)

### Actions
- Save → `POST /api/manager/content-types` (create) or `PUT /api/manager/content-types/{alias}` (update)
- Cancel → back to `/manager/content-types`

---

## Page 3: ContentItemsList — `/manager/content/{alias}`

**Pattern:** Follows `Posts.razor` pattern.

**Header (dynamic):**
- Title: `"{ContentType.Name}"`
- Subtitle: `"Managing {n} {name} items."`
- Search bar
- "New {ContentType.Name}" button → `/manager/content/{alias}/editor`

**Grid:** `RadzenDataGrid<ContentItemSummary>`:

| Column | Source | Render |
|--------|--------|--------|
| Title | `ContentItem.Title` | Bold |
| Slug | `ContentItem.Slug` | mono badge |
| First Field | `Fields[type.Fields[0].Name]` | First 50 chars (text), thumbnail (image) |
| Status | `PublicationState` | Green "Published" / Yellow "Draft" badge |
| Published | `PublishedOn` | `MMM dd, yyyy` |

**Actions:**
- Row click → `/manager/content/{alias}/editor/{id}`
- Delete → confirmation → `DELETE /api/manager/content-types/{alias}/items/{id}`

---

## Page 4: ContentItemEditor — `/manager/content/{alias}/editor/{id?}`

**Pattern:** Tabbed editor with "Content" and "Metadata" tabs.

### Tab: Content (dynamic form)

Reads `ContentTypeDefinition.Fields` and renders each field:

| FieldType | Rendered As | Component Alias |
|-----------|-------------|-----------------|
| `text` | `<RadzenTextBox>` | `aero-textbox` |
| `richtext` | `<RadzenRichTextEditor>` | `aero-richtext-editor` |
| `image` | Media picker + preview thumbnail | `aero-media-picker` |
| `number` | `<RadzenNumeric>` | `aero-numberbox` |
| `boolean` | `<RadzenCheckBox>` with label | `aero-checkbox` |
| `url` | `<RadzenTextBox>` with URL validation | `aero-urlbox` |
| `date` | `<RadzenDatePicker>` | `aero-datepicker` |
| `reference` | Content type picker with search | `aero-reference-picker` |

Each field renders:
- Label (from `ContentFieldDefinition.Label` or `Name`)
- Required indicator (red asterisk if `Required && mode == Publish`)
- Help text from `Placeholder`
- Validation messages from `IContentFieldValidator`

### Tab: Metadata

| Field | Component | Notes |
|-------|-----------|-------|
| Title | `<RadzenTextBox>` | Display title for lists |
| Slug | `<RadzenTextBox>` | Auto-gen from title, editable |
| Publication State | Badge | Current: Published / Draft |
| Schedule Publish | `<RadzenDatePicker>` w/ time | Only if `ScheduleConfig.AllowScheduledPublish` |
| Schedule Unpublish | `<RadzenDatePicker>` w/ time | Only if `ScheduleConfig.AllowScheduledUnpublish` |

### Tab: Version History

- Collapsible panel showing past versions
- Each entry: version number, timestamp
- Click to view version fields (read-only)

### Actions

| Button | Validation Mode | API Call |
|--------|----------------|----------|
| Save Draft | `Draft` | `POST /api/manager/content-types/{alias}/items` |
| Publish | `Publish` | `POST /api/manager/content-types/{alias}/items/{id}/publish` |
| Unpublish | — | `POST /api/manager/content-types/{alias}/items/{id}/unpublish` |
| Delete | — | `DELETE /api/manager/content-types/{alias}/items/{id}` |

Save on create, Save Draft + Publish on edit.

---

## Nav Menu — Additive Change

### Sidebar (`NavMenu.razor`)

Insert one new `NavMenuSection` after "Pages" (position 4), before "Docs" (position 5):

```razor
@* 4. Content Types *@
<NavMenuSection Href="/manager/content-types" Label="Content" 
    Icon="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" 
    IsCollapsed="IsCollapsed">
    <NavMenuItem Href="/manager/content-types" Label="Types" IsCollapsed="IsCollapsed"/>
</NavMenuSection>
```

The icon SVG represents a document with text lines (structured data metaphor). Only one sub-item ("Types") — the content type items are accessed from the type's detail page, not from the nav.

No changes to the top bar needed — the existing sidebar is the primary navigation.

---

## API Endpoints (to be implemented in Headless module)

| Method | Route | Service Method |
|--------|-------|---------------|
| `GET` | `/api/manager/content-types` | `IContentTypeService.GetAllAsync(siteId)` |
| `GET` | `/api/manager/content-types/{alias}` | `IContentTypeService.GetByAliasAsync(siteId, alias)` |
| `POST` | `/api/manager/content-types` | `IContentTypeService.SaveAsync(definition)` |
| `PUT` | `/api/manager/content-types/{alias}` | `IContentTypeService.SaveAsync(definition)` |
| `DELETE` | `/api/manager/content-types/{alias}` | (new) delete type if no items |
| `GET` | `/api/manager/content-types/{alias}/items` | `IContentQueryService.GetByTypeAsync(...)` |
| `GET` | `/api/manager/content-types/{alias}/items/{id}` | `IContentService.LoadAsync(id)` |
| `POST` | `/api/manager/content-types/{alias}/items` | `ContentCommandService.SaveDraftAsync(item)` |
| `PUT` | `/api/manager/content-types/{alias}/items/{id}` | `ContentCommandService.SaveDraftAsync(item)` |
| `DELETE` | `/api/manager/content-types/{alias}/items/{id}` | `ContentCommandService.DeleteAsync(id)` |
| `POST` | `/api/manager/content-types/{alias}/items/{id}/publish` | `ContentCommandService.PublishAsync(item)` |
| `POST` | `/api/manager/content-types/{alias}/items/{id}/unpublish` | Set Draft + save |
| `GET` | `/api/manager/content-types/{alias}/items/{id}/versions` | Query `ContentItemVersion` by `ContentItemId` |

---

## File Manifest

### New files — Content module (`Aero.Cms.Modules.Content`)

```
Areas/Content/Pages/
├── _Imports.razor                      # @layout ManagerShellLayout, @using, [Authorize]
├── ContentTypeList.razor               # List all schemas
├── ContentTypeList.razor.cs
├── ContentTypeEditor.razor             # Create/edit schema
├── ContentTypeEditor.razor.cs
├── ContentItemsList.razor              # Browse items of a type
├── ContentItemsList.razor.cs
├── ContentItemEditor.razor             # Dynamic form editor
└── ContentItemEditor.razor.cs
```

### New files — Headless module (`Aero.Cms.Modules.Headless`)

```
Areas/Api/v1/
├── ContentTypesApi.cs                  # CRUD for content type definitions
└── ContentItemsApi.cs                  # CRUD + publish/unpublish for content items
```

### Modified files (additive, 6 lines)

```
Aero.Cms.Shared/Layout/NavMenu.razor    # +1 NavMenuSection (6 lines, no existing lines changed)
Aero.Cms.Modules.Content/AeroContentModule.cs  # +AddRazorPages() + route conventions
```

### Untouched (zero changes)

- `Pages.razor`, `Posts.razor`, `Docs.razor` — separate features
- `PageEditor.razor`, `PostEditor.razor` — different editor paradigms
- `ManagerShellLayout.razor` — shared layout, unchanged
- `BlockBase` hierarchy — `ContentEmbedBlock` already exists
- `IPageContentService` — content types are not pages
- All existing services — content type system is independent

---

## Implementation Order

1. **NavMenu.razor** — add the Content section (5 mins, static HTML)
2. **API layer** — `ContentTypesApi.cs` + `ContentItemsApi.cs` (wraps existing services)
3. **ContentTypeList.razor** — list page (copy `Pages.razor` pattern)
4. **ContentTypeEditor.razor** — field definition UI (the most complex page)
5. **ContentItemsList.razor** — items list (copy `Posts.razor` pattern)
6. **ContentItemEditor.razor** — dynamic form (reads type schema at runtime)
7. **Module registration** — `AddRazorPages()`, `AddAreaPageRoute()` in `AeroContentModule`
