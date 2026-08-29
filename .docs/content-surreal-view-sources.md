# Registered sources for Surreal Views

Status: implemented.

AeroCMS can build a query-backed Content View from an existing SurrealDB table
or materialized table view without taking ownership of that physical schema.
The consuming host registers both an `IContentShape` and an
`IContentViewSource`. The source registration names the allow-listed physical
table, its tenant and site fields, physical-to-logical field mapping, required
predicates, identity/title/search fields, and whether the source is a normal
table or a materialized view.

In the manager, the Surreal View editor lists only those registered sources
that are present in the current database and whose observed table kind matches
the registration. Choosing a source asks the server to generate explicit,
bounded list, exact-entry, and search statements. Every generated statement
contains the server-owned tenant and site predicates plus the source's required
predicates. The browser does not generate these statements and cannot supply
scope values.

Saving a generated draft persists the source alias and a fingerprint covering
the exact observed table, field, and index definitions plus the registered
mapping and predicates. The server regenerates the statements during save and
rechecks the fingerprint during publish. Physical or registration drift blocks
publication until an editor reloads and saves a new reviewed draft. Manually
editing a generated statement intentionally clears its source binding and
returns to the existing advanced-query workflow.

The runtime View executor is read-only and disables Sable schema auto-creation.
It projects physical row names to the registered logical shape, enforces row and
result limits, and rejects rows that do not validate against that shape. It does
not discover arbitrary database tables, create or alter DDL, run joins or graph
traversals, or expose database sessions to templates.

For content-entry reference fields, public Scriban rendering can expose a
bounded `references` scope. Only the provider-qualified selected record and the
field's configured `PreviewFields` are projected. Site scope, provider allow
lists, and exact stable identity are revalidated at render time; unresolved
required references fail closed.

Physical schema deployment, materialized-view refresh behavior, credentials,
and backup/restore remain responsibilities of the consuming application. A
schema writer and a read-only Content View credential should be separate from
the application writer, and schema tooling must never be run implicitly from
application startup.
