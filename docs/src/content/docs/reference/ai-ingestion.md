---
title: AI ingestion
description: AeroCMS documentation discovery files, normalized corpus, manifest, provenance, and trust classes.
---

The checked-in ingestion artifacts are generated from the canonical Starlight sources:

- `/llms.txt`: concise public map for agent discovery;
- `/llms-aero-full.txt`: normalized public corpus with stable headings and provenance;
- `/documentation-manifest.json`: machine-readable metadata;
- `docs/llms.txt`, `docs/llms-aero-full.txt`, and `docs/documentation-manifest.json`: repository source copies.
- `docs/manager-assistant-corpus.json`: generated structured corpus embedded into the AI module and reconciled into the manager-only search projection.

Run `pnpm run generate` in `docs/` after changing a canonical page or manifest entry.

## Manifest fields

Each entry contains:

- `title`
- `canonical_path`
- `document`
- `audience`
- `feature_area`
- `maturity`
- `source_files`

The manifest root records the last verified commit, generation rules, excluded roots, and trust-class definitions.

## Trust classes

- **Public documentation** is included in both llms files and the full corpus.
- **Manager/internal documentation** can be present in the manifest but is excluded from public corpus generation until an explicit authenticated distribution exists.
- **Security-sensitive documentation** is excluded from public ingestion. This includes secret values, credential locations beyond safe configuration keys, incident data, customer data, PII, and exploit-enabling operational details.

Design history under `.docs/` is not a documentation input. A behavior first needs current source/test verification and original product wording.

The manager assistant corpus includes public and `manager-internal` entries, but never `security-sensitive` entries. Commerce documentation is part of this corpus alongside the other curated product areas.

At build time the generated corpus is embedded into `Aero.Cms.Modules.Ai`. When the application starts, a hosted reconciliation pass:

1. fails closed unless the embedded snapshot has the supported schema, the `manager-internal` root trust class, allowlisted audiences, unique canonical paths, and complete source provenance;
2. compares its Git revision, corpus checksum, and search-schema version with `ai_manager_documentation_corpus_states`;
3. creates, updates, or removes records in `ai_manager_documentation_chunks`;
4. generates embeddings when a compatible 384-dimension provider is registered;
5. commits the chunks and optimistic-concurrency corpus-state record together.

Git remains authoritative. Both SurrealDB tables are disposable search projections and can be rebuilt from the embedded corpus. A missing or invalid new corpus does not erase the last valid projection.

Full-text retrieval is always available after a successful reconciliation. Hybrid vector retrieval is enabled only when the corpus-state record confirms that every current chunk was embedded by the active model with the expected dimensions. Without an embedding provider, AeroCMS persists and searches the full-text projection. Startup reconciliation uses fresh scopes for a bounded three-attempt retry so transient database conflicts or embedding-provider failures can recover without publishing a partially ready vector index.

Only a manager-audience retrieval request queries these documentation tables and merges bounded product-documentation matches with the tenant/site CMS corpus. Public and member retrieval return before the documentation projection is accessed.

## Chunking

The full corpus preserves page titles, canonical paths, feature area, maturity, provenance, and descriptive headings. It removes frontmatter, component imports, and rendered diagram markup. Sections remain page-local so a retriever can chunk at `##` headings without losing the page's trust metadata.
