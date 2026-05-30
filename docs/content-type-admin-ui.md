# Content Type Admin UI

## Status

Implemented as a Blazor/Radzen manager experience in
`src/Aero.Cms.Shared/Pages/Manager/ContentTypes/`.

This document is the current UX contract for the content type manager UI. Older
notes that described content entries as routable by default are superseded by
the embedded-first decision below.

---

## UX Decision: Embedded First, Public URLs Optional

Content items are structured entries first. They are meant to be embedded in
pages, blocks, listings, and other module experiences unless the editor
explicitly enables standalone pages for that content type.

The content type editor exposes this as:

```text
Public pages
[ ] Give each entry its own page
```

Default: off.

When disabled:

- Entries are still draftable, publishable, searchable, and reusable.
- The item editor hides the public slug workflow and shows the entry as
  "Embedded entry".
- Lists label the type or item as "Embedded" / "Embedded only".

When enabled:

- Entries collect a public URL slug in the item editor.
- The type list labels the type as "Public pages".
- A future routing slice must connect published entries to the public route and
  slug registry. The UI/model toggle exists now; the public request pipeline is
  intentionally separate.

This matches the non-technical UX goal: users can create reusable structured
content without accidentally publishing duplicate public pages.

---

## Routes

| Route | Page Component | Purpose |
|-------|----------------|---------|
| `/manager/content-types` | `ContentTypeList.razor` | List all content type schemas |
| `/manager/content-type/editor/{alias?}` | `ContentTypeEditor.razor` | Create/edit content type schema |
| `/manager/content/{alias}` | `ContentItemsList.razor` | Browse entries for one content type |
| `/manager/content/{alias}/editor/{id?}` | `ContentItemEditor.razor` | Create/edit one content item |

All pages live in `Aero.Cms.Shared/Pages/Manager/ContentTypes/` and use
code-behind files for behavior.

---

## Page 1: ContentTypeList

Component:

- `ContentTypeList.razor`
- `ContentTypeList.razor.cs`

Pattern: match the list editors for pages, docs, and posts by using
`RadzenDataGrid`.

The grid shows:

| Column | Source | Notes |
|--------|--------|-------|
| Type | `ContentTypeSummary.Name`, `Description`, `Alias` | Primary identifying column |
| Fields | `FieldCount` | Badge |
| Entries | `ItemCount` | Currently API-backed as `0`; count aggregation is a future refinement |
| URL | `AllowPublicUrl` | "Public pages" or "Embedded" badge |
| Display | `HasCustomTemplate` | "Custom template" or "Auto display" |
| Category | `Category` | Optional grouping |
| Actions | Edit/Delete | Stops propagation so row click still works |

Interactions:

- Row click opens `/manager/content-type/editor/{alias}`.
- Search filters the loaded list client-side.
- Delete uses `DialogService.Confirm`.
- Create opens `/manager/content-type/editor`.

---

## Page 2: ContentTypeEditor

Component:

- `ContentTypeEditor.razor`
- `ContentTypeEditor.razor.cs`

Goal: make schema creation understandable for non-technical users while still
leaving advanced handles/templates available for developers.

Tabs:

| Tab | Purpose |
|-----|---------|
| Basics | Name, category, description, public URL toggle, advanced handle/render mode |
| Fields | Field cards, field palette, selected-field settings |
| Display | Automatic display vs optional custom Scriban template |

Important UX details:

- The alias/internal handle is auto-generated from the display name.
- The public URL decision is phrased as an editor-friendly toggle:
  "Give each entry its own page".
- Field types are presented as a field library with familiar names and icons.
- Field cards support select, duplicate, move up/down, and delete.
- Required status, help text, default value, and type-specific validation live
  in the selected-field settings panel.
- Custom Scriban is hidden behind the Display tab and off by default.

Supported field types in the current UI:

| Field type | Editor intent |
|------------|---------------|
| `text` | Short text |
| `richtext` | Longer formatted copy |
| `image` | Media URL with media selector support in the item editor |
| `number` | Numeric value |
| `boolean` | Yes/no |
| `url` | Link |
| `date` | Date |
| `reference` | Reference to another entry |

---

## Page 3: ContentItemsList

Component:

- `ContentItemsList.razor`
- `ContentItemsList.razor.cs`

Pattern: `RadzenDataGrid<ContentItemSummary>` matching the other manager list
experiences.

The grid shows:

| Column | Source | Notes |
|--------|--------|-------|
| Entry | `Title`, `FirstFieldValue` | Main scanning column |
| URL | `Slug`, gated by `AllowPublicUrl` | Shows "Embedded only" when public URLs are off |
| Status | `PublicationState` | Draft/Published badge |
| Published | `PublishedOn` | Date or dash |
| Version | `VersionNumber` | Numeric badge |
| Actions | Edit/Delete | Stops row-click propagation |

Interactions:

- Row click opens `/manager/content/{alias}/editor/{id}`.
- Search calls the content items API with a `search` query.
- Delete uses confirmation.
- Create opens `/manager/content/{alias}/editor`.

---

## Page 4: ContentItemEditor

Component:

- `ContentItemEditor.razor`
- `ContentItemEditor.razor.cs`

Goal: let editors fill structured content as a familiar form, not as a schema or
JSON editor.

Layout:

- Main field form on the left.
- Metadata/publish panel on the right.

Dynamic controls:

| Field type | Current Radzen control |
|------------|------------------------|
| `text` | `RadzenTextBox` |
| `richtext` | `RadzenTextArea` |
| `image` | Preview + URL textbox + `MediaSelectorModal` |
| `number` | `RadzenNumeric<decimal?>` |
| `boolean` | `RadzenCheckBox` |
| `url` | `RadzenTextBox` |
| `date` | `RadzenDatePicker<DateTime?>` |
| `reference` | Textbox for referenced entry ID |

Publishing behavior:

- Save Draft allows incomplete entries.
- Publish validates required fields first.
- Publish saves the latest draft before calling the publish endpoint.
- Unpublish is available for published entries.

Public URL behavior:

- If `AllowPublicUrl` is false, the editor displays the item as an embedded
  entry and does not ask non-technical users to manage a slug.
- If `AllowPublicUrl` is true, the editor shows a slug field and auto-generates
  it from the title until the user edits it.

---

## API Endpoints

The implemented admin endpoints are under `/api/v1/admin`.

| Method | Route | Purpose |
|--------|-------|---------|
| `GET` | `/api/v1/admin/content-types` | List content types |
| `GET` | `/api/v1/admin/content-types/{alias}` | Get one content type |
| `POST` | `/api/v1/admin/content-types` | Create content type |
| `PUT` | `/api/v1/admin/content-types/{alias}` | Update content type |
| `DELETE` | `/api/v1/admin/content-types/{alias}` | Delete content type |
| `GET` | `/api/v1/admin/content-items?contentType={alias}&skip={n}&take={n}&search={q}` | List/search entries |
| `GET` | `/api/v1/admin/content-items/{alias}/{id}` | Get one entry |
| `POST` | `/api/v1/admin/content-items/{alias}` | Create entry |
| `PUT` | `/api/v1/admin/content-items/{alias}/{id}` | Update entry |
| `DELETE` | `/api/v1/admin/content-items/{alias}/{id}` | Delete entry |
| `POST` | `/api/v1/admin/content-items/{alias}/{id}/publish` | Publish entry |
| `POST` | `/api/v1/admin/content-items/{alias}/{id}/unpublish` | Unpublish entry |

Typed clients live in
`src/Aero.Cms.Abstractions/Http/Clients/ContentTypesClient.cs`.

---

## Implementation Notes

- Lists must use Radzen grids for consistency with pages, docs, and posts.
- Keep non-technical labels in the primary flow; keep internal handles and
  templates in advanced/secondary areas.
- Continue using code-behind for page behavior.
- Prefer reusable Page Editor property-panel classes where they fit the manager
  experience.
- Public entry routing should be implemented as a separate architecture slice
  that updates the route registry/slug registry deliberately.
