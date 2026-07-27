---
title: AI ingestion
description: AeroCMS documentation discovery files, normalized corpus, manifest, provenance, and trust classes.
---

The checked-in ingestion artifacts are generated from the canonical Starlight sources:

- `/llms.txt`: concise public map for agent discovery;
- `/llms-aero-full.txt`: normalized public corpus with stable headings and provenance;
- `/documentation-manifest.json`: machine-readable metadata;
- `docs/llms.txt`, `docs/llms-aero-full.txt`, and `docs/documentation-manifest.json`: repository source copies.
- `docs/manager-assistant-corpus.json`: generated structured corpus embedded into the AI module for manager-only product grounding.

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

The manager assistant corpus includes public and `manager-internal` entries, but never `security-sensitive` entries. At build time it is embedded into `Aero.Cms.Modules.Ai`; at runtime only a manager-audience retrieval request can merge those product-documentation matches with the tenant/site CMS corpus. Public and member retrieval never query the embedded product corpus.

## Chunking

The full corpus preserves page titles, canonical paths, feature area, maturity, provenance, and descriptive headings. It removes frontmatter, component imports, and rendered diagram markup. Sections remain page-local so a retriever can chunk at `##` headings without losing the page's trust metadata.
