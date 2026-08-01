# Content type references and composition

## Purpose

Aero CMS content types need three distinct concepts. They should not be
presented as variations of the same feature:

1. **Hierarchy** organizes content entries into a parent/child tree.
2. **References** link one content entry to another entry or to a Page, Post,
   or Doc.
3. **Schema composition** reuses field definitions across content types.

Hierarchy and references are implemented. Schema composition is a future
feature and should not be represented as content inheritance until its editing,
versioning, and migration rules are defined.

## Field requirement semantics

Every field has an explicit **Required** setting.

- Drafts may omit required values so editors can save incomplete work.
- Publishing rejects an omitted required value.
- An optional list may be empty.
- If an optional list has values, its configured minimum and maximum item
  counts apply.
- A required list must contain at least one value when published, in addition
  to any configured minimum item count.

This keeps `Required` independent from length or item-count validation.

## Content entry references

Use the **Related content** field when an entry must point to an entry of
another custom content type. The field definition records one target content
type. The entry editor then provides a searchable, site-scoped selection of
the actual matching entries stored in the database.

For example, an `Animal` content type can define a field labeled `Species`
whose related content type is `species`. When an editor creates a Dog entry,
the `Species` field searches the current site's Species entries and can select
`K-9`. The stored field value is the selected Species content-item ID; the
Species fields are not copied into Dog.

Use the **Hierarchy entry** field when the target is part of a hierarchy and
the editor also needs hierarchy-aware filtering or cascading behavior.

References store identity, not copied values. Rendering and application logic
can traverse a reference to read the target entry's current fields.

## Site content references

Use the **Site content** field to reference an existing:

- Page
- Post
- Doc
- Public content-item page

Public content-item sources are generated from content types whose
`AllowPublicUrl` setting is enabled. Their stable source key contains the
content-type alias, such as `content:species`.

The entry editor first selects a source and then shows a searchable list of
records from that source. The stored value is a typed reference:

```json
{
  "source": "pages",
  "id": "1530221140281556994"
}
```

For a dynamic content-item page the shape is the same:

```json
{
  "source": "content:species",
  "id": "1530221140281556995"
}
```

The ID remains a string across the browser JSON boundary. The server resolves
it as a Snowflake `long`, validates that the provider exists, and verifies that
the referenced record belongs to the current site.

## Hierarchy is not schema inheritance

A hierarchical content type relates entries of that content type:

```text
Animalia
└── Chordata
    └── Mammalia
```

It does not cause one content type to inherit another content type's fields,
and it does not copy field values between entries.

For taxonomy, each node can be an entry of a `Taxon` content type while a
separate `Species` content type references the appropriate taxon. Following
the reference or hierarchy provides the classification chain without
duplicating it into every species record.

## Future schema composition

If reusable schemas are added, prefer named field sets or base schemas over
general multiple inheritance.

The composition contract should be:

- Reuse **field definitions only**.
- Never inherit or copy entry values.
- Show inherited fields as inherited/read-only in the child type editor.
- Allow the child type to add fields without mutating the base definition.
- Detect field-name collisions and composition cycles before saving.
- Version the effective schema so a base-schema change does not silently
  corrupt existing entries.
- Flatten the effective field definition for validation and rendering.

Entry values always belong to the entry being edited. If a value should come
from another record, use a reference and resolve it at query or render time.
Automatic value copying creates stale duplicates and ambiguous ownership.

## Editor interaction

Selecting a field keeps its settings in the right-side inspector. Double
clicking a field, or using its edit action, opens the same settings in a modal.
The modal edits a copy and applies changes only when the author confirms them.

This shared settings surface must include the explicit Required setting and all
field-type-specific options.
