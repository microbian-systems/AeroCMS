# Managed Table content types

**Status:** Future proposal — not implemented and not required by current
AeroCMS consumers.

**Last updated:** 2026-08-13

## Purpose

Allow an authorized AeroCMS administrator to define structured records whose
physical SurrealDB table and schema are managed by AeroCMS. This complements
the existing document-content and read-only Surreal View models; it does not
replace either one.

Managed Tables are intended for reusable structured data such as calculator
definitions, coefficients, measurement bands, controlled reference data, and
other records that need a stable schema, indexed fields, and native
relationships without requiring a new compiled CLR type for every site-owned
model.

This proposal is deliberately deferred and must not expand a current
consumer's launch scope. Existing content, View, and relationship capabilities
should be stabilized before Managed Tables expand the platform's
schema-management responsibilities.

## Current baseline

AeroCMS currently has three related but distinct capabilities:

- ordinary content types store editorial entries in the shared CMS content
  model and support localization, drafts, review, publication, and search;
- `IContentShape` and `IContentViewSource` are code-owned registration points
  for the shape and authorized physical source of query-backed entries; and
- `ContentSurrealViewRevision` with `IContentSurrealViewService` stores,
  publishes, and executes bounded, site-owned read/query projections.

The relationship catalog can describe declared and discovered record links,
graph edges, association records, self hierarchies, and explicit field joins.
Its current physical DDL application remains intentionally fail-closed.

These capabilities provide most of the read and relationship-discovery side,
but they do not let a site administrator create a new physical table and then
perform validated row-level writes through the CMS.

## Proposed storage modes

A content model should expose an explicit storage mode rather than making a
View or a normal content type silently acquire schema ownership.

| Storage mode | Physical storage | CMS writes rows | Primary use |
| --- | --- | --- | --- |
| Document | Shared CMS content storage | Yes | Localized editorial content, drafts, review, and publishing |
| Managed Table | CMS-managed SurrealDB table | Yes | Structured records, indexed data, calculator configuration, and relationship targets |
| Virtual View | Existing registered tables and bounded queries | No | Read-only projections over CMS or application-owned data |

The storage mode is immutable after data exists unless an explicit migration
copies and validates every record. Switching modes is not a routine content
type edit.

## Runtime schema model

Managed Tables should use a persisted, versioned schema descriptor rather than
generating a CLR class at runtime. A published schema revision should contain:

- a stable model ID and administrator-facing alias;
- a server-generated physical table name;
- stable field IDs and normalized physical field names;
- scalar, array, object, and record-reference types from a strict allow-list;
- required, optional, default, unique, and index settings;
- tenant/site ownership and mandatory scope fields;
- record identity strategy;
- relationship target, cardinality, physical representation, and lifecycle
  policy;
- draft, applied, drifted, retired, and migration-required state;
- an exact normalized physical-schema fingerprint; and
- the schema revision used when each row was validated.

Rows can be represented at the CMS boundary as validated dictionaries or JSON
values. The runtime service validates every value against the published
descriptor before producing parameterized database operations. A compiled
`Schema.For<T>()` mapping remains the preferred path for code-owned domain
tables, including WildNutriPro's F# taxonomy records.

## Proposed platform responsibilities

Names below describe responsibilities, not accepted public API names.

- **Managed schema service:** stores drafts, validates definitions, computes an
  exact change plan, previews DDL, applies authorized changes, and detects
  drift.
- **Managed row service:** performs scoped, bounded CRUD using the currently
  applied schema revision and optimistic concurrency.
- **Generated content shape:** exposes the table's approved fields to the
  existing entry-provider and editor infrastructure.
- **Generated View source:** registers the physical table and its tenant/site
  scope with the existing Surreal View security boundary.
- **Relationship registration:** publishes declared record links, graph edges,
  or association records to the existing relationship catalog.
- **Migration coordinator:** executes explicit, resumable data migrations when
  a change is not safely additive.

## Schema lifecycle

1. An administrator saves a schema draft. No DDL runs.
2. The server validates identifiers, types, scope, indexes, relationships, and
   lifecycle policies.
3. The server compares the draft to exact physical schema evidence and shows a
   deterministic change plan.
4. A privileged operator applies an approved additive plan.
5. AeroCMS records the applied revision and physical fingerprint.
6. Row writes use only the applied revision and fail closed on drift.
7. Destructive changes require a separate migration plan, backup evidence,
   bounded execution, and post-migration verification.

Renames, removals, narrowing type changes, identity changes, scope changes, and
delete-policy changes are never inferred or applied as ordinary field edits.
No browser request may submit arbitrary SurrealQL as schema DDL.

## Relationship and View integration

Every applied Managed Table becomes an allow-listed physical schema target and
potential View source. Its relationships use the same descriptor and drift
model as relationships across ordinary content types and existing application
tables.

- Record links are appropriate when ownership belongs on one record and the
  delete/reference policy is explicit.
- Graph edges are the default for traversal-oriented relationships that do not
  belong inside either endpoint record.
- Association records are appropriate when the relationship has provenance,
  ordering, effective dates, review state, or other attributes.
- Field joins remain metadata-only compatibility declarations and do not
  create referential integrity.

Discovery never grants write permission. Existing tables remain application-
owned until an administrator adopts an exact, scope-provable descriptor, and
adoption alone does not authorize AeroCMS to change their schema or rows.

## Calculator use case

A future calculator model could use Managed Tables for component definitions,
input ranges, units, coefficients, formula versions, validation constraints,
and Animal/Species applicability relationships. The calculator editor should
compose allow-listed components such as sliders, numeric inputs, selects, and
result panels.

Executable calculator behavior must use a bounded, versioned expression or
rule model. Managed Tables must not become a route for storing and executing
arbitrary C#, F#, TypeScript, JavaScript, or SurrealQL supplied by a content
editor. SharpTS or another generator could later produce an optional typed SDK
or build-time plugin from an applied schema, but generated code would not be
the schema's source of truth.

## Localization and publication boundary

The recommended first version treats Managed Table rows as versioned
structured records, not as full CMS editorial documents. Localized narrative
content remains in Document content types and can reference Managed Table
records.

Adding culture variants, translation groups, review, scheduling, and
publication to arbitrary physical tables would substantially expand the
storage and concurrency model. That work requires a separate accepted design
and should not be implied by this proposal.

## Security and operational requirements

- Physical table names are server-generated and never accepted directly from a
  browser request.
- Tenant and site scope are server-owned and present in every generated query.
- DDL and row operations use typed descriptors and bound values.
- Field counts, index counts, value sizes, query limits, and relationship depth
  are bounded.
- Schema application requires a privileged capability separate from ordinary
  content editing.
- Delete policies default to restrictive behavior. Cascade is never inferred.
- Drift blocks writes and publication until an operator reconciles the exact
  physical schema.
- Backups and restore rehearsal are prerequisites for destructive migrations.
- Domain-owned tables cannot be silently converted into CMS-owned tables.

## Delivery outline

### Phase 1 — descriptor and preview

- Persist versioned Managed Table schema drafts.
- Validate the allow-listed field model.
- Generate exact, read-only DDL previews and fingerprints.
- Do not apply DDL or write rows.

### Phase 2 — additive schema and row CRUD

- Apply privileged additive schema plans.
- Provide scoped, versioned row CRUD.
- Generate content shapes and View-source registrations.
- Add drift detection, audit records, and focused concurrency tests.

### Phase 3 — relationships and calculators

- Integrate Managed Tables with the relationship catalog and bounded View
  planner.
- Add calculator component and rule schemas without arbitrary executable code.
- Add migration tooling for approved non-additive changes.

## Decisions required before implementation

1. Whether a Managed Table is configured as a content-type storage mode or as
   a separate data-model resource that content types and Views bind to.
2. Which identity strategies are allowed and whether site scope is mandatory
   for every table.
3. Which additive DDL operations may be automated and which require an
   operator approval ceremony.
4. Whether any Managed Table rows need publication state in the first version.
5. The bounded calculator expression model and its versioning rules.
6. Whether the reusable runtime schema-descriptor primitive belongs solely in
   AeroCMS or later becomes a separately approved AeroDB/Sable contract.

Until these decisions are accepted, Managed Tables remain a future proposal
and must not be presented as implemented functionality or as a dependency for
current AeroCMS consumers.
