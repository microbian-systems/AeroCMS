# AeroCMS Documentation and API Reference Plan

## Status

Proposed. This plan covers AeroCMS only. The `Aero/`, `AeroDB/`, `NeoUI/`,
`ui/`, `hyperui/`, and `tiptap-dotnet/` repositories or submodules retain
their own documentation systems and are out of scope.

The current-source XML documentation inventory and comment pass was completed
on 2026-07-19. A full `src/Aero.Cms.slnx` build completed successfully with XML
documentation generation enabled and no documentation warnings originating
from the in-scope AeroCMS projects. Starlight, DocFX publishing, and CI work
remain separate phases of this plan.

The nested projects under `src/Aero.Cms.Db.Marten/Legacy/` and all
`src/Aero.Cms.Marten.*` projects are also out of scope. They are historical or
removal-bound reference material, are intentionally not part of the compilable
current product, and must not be presented as part of the current AeroCMS
architecture or API reference. Documentation work must not re-enable their
compile items or otherwise revive them.

## Goals

1. Separate internal engineering plans from published technical documentation.
2. Build an AeroCMS documentation site using Astro Starlight and pnpm.
3. Generate a curated .NET API reference using DocFX and XML documentation.
4. Add XML comments to public members across current AeroCMS projects.
5. Make the documentation pipeline reproducible locally and in CI.
6. Prevent obsolete Marten, NeoUI, and legacy block decisions from appearing
   as current guidance.

## Documentation Boundaries

### Internal project documentation: `.docs/`

Use `.docs/` for material that explains work in progress or internal decisions:

- implementation plans;
- agent orchestration instructions;
- progress trackers;
- refactor plans;
- deferred TODOs;
- exploratory architecture proposals;
- superseded designs retained for historical context;
- internal ADRs that are not intended as user-facing guides.

Examples include the Living Standard PageEditor plan, commerce reliability
TODOs, Sable migration notes, and agent workflow documents.

Deferred source comments removed during documentation cleanup are preserved in:

- [Analytics markup rendering follow-up](analytics-rendering-follow-up.md);
- [WebOptimizer bundling follow-up](weboptimizer-bundling-follow-up.md);
- [AppHost HTTPS endpoint note](apphost-https-endpoint-note.md); and
- [Custom TCP transport follow-up](tcp-transport-follow-up.md).

### Published AeroCMS documentation: `docs/`

Use `docs/` as the source for the Starlight site:

```text
docs/
  package.json
  pnpm-lock.yaml
  astro.config.mjs
  src/
    content/
      docs/       authored guides and conceptual documentation
      api/        generated DocFX output
    styles/
    components/
```

Published documentation should describe the current implementation and be
written for developers, operators, module authors, or AeroCMS users. It should
not be a raw dump of planning documents.

## Current Documentation Cleanup

Review every existing Markdown file under `docs/` and classify it as one of:

1. **Internal plan** — move to `.docs/`.
2. **Current developer documentation** — migrate into
   `docs/src/content/docs/` and add Starlight frontmatter.
3. **Historical or superseded design** — move to `.docs/archive/` or mark it
   explicitly as historical.
4. **Needs rewrite** — retain its useful subject matter but rewrite it against
   the current Sable/SurrealDB, HTML-first, Blazor WASM, and MAUI Hybrid design.

Documents describing Marten, PostgreSQL as the current primary store, NeoUI,
`NeoPageNode`, `LayoutRegions`, or the old block renderer must not be linked
from the current documentation sidebar unless clearly marked historical.

The existing `docs/00_README.md` is not a suitable current homepage because it
describes the older Marten/PostgreSQL/block-based direction. Replace it with a
current AeroCMS introduction after the site scaffold is complete.

## Starlight Site

Use the AeroDB documentation site as the implementation reference:

- `AeroDB/docs/package.json` pins Astro/Starlight dependencies.
- `AeroDB/docs/astro.config.mjs` defines branding, navigation, edit links, and
  generated API navigation.
- `AeroDB/docs/src/content.config.ts` registers the Starlight docs collection.
- `AeroDB/docs/src/styles/custom.css` contains site-specific theme tokens.
- AeroDB uses pnpm; npm must not be introduced into AeroCMS.

### Proposed sidebar

```text
Getting Started
  Introduction
  Local Development
  Setup and Bootstrap
  Configuration

Concepts
  Architecture
  Modules
  Tenants and Sites
  Pages and Publishing
  HTML Living Standard PageEditor
  Content Types
  Routing and Aliases
  Caching

Guides
  Create a Module
  Build a Page
  Manage Media
  Configure Navigation
  Configure Themes
  Work with Forms
  Use the Admin API

Operations
  Deployment
  Observability
  Cache Providers
  Data Seeding
  Troubleshooting

API Reference
  generated DocFX API navigation
```

## DocFX API Reference

Create an AeroCMS-specific `docfx/` configuration based on the AeroDB pattern.
The configuration should explicitly select current reusable AeroCMS assemblies
from Release output rather than scanning every executable, test, or legacy
project.

The initial API allowlist should prioritize:

- `Aero.Cms.Abstractions`;
- `Aero.Cms.Contracts`;
- `Aero.Cms.Core.Abstractions`;
- `Aero.Cms.Core.Entities`;
- `Aero.Cms.Core`;
- `Aero.Cms.Html`;
- `Aero.Cms.Shared.Models`;
- public HTTP client contracts;
- current module contracts and public extension points.

Executable hosts, test assemblies, generated implementation details, and
`src/Aero.Cms.Db.Marten/Legacy/` should not appear in the public API navigation.

DocFX metadata should be generated from compiled assemblies and their XML files,
then copied into the Starlight content tree as generated content. Generated
YAML/manifest files must never be hand-edited.

Pin the DocFX tool in an AeroCMS `dotnet-tools.json`, matching the reproducible
tooling approach used by AeroDB.

## XML Documentation Strategy

`src/Directory.Build.props` already enables:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
```

The remaining work is to document the current public surface.

### Scope

Include current AeroCMS projects under `src/`, excluding:

- `src/Aero.Cms.Db.Marten/Legacy/**`;
- `src/Aero.Cms.Marten.*/**`;
- generated source output;
- test projects from the published API site;
- external submodules and repositories.

### Workflow

1. Build a CS1591 inventory for the current project set.
2. Document public interfaces, records, enums, and contracts first.
3. Use `<inheritdoc />` for implementations where the interface description is
   authoritative.
4. Add XML comments to public constructors, methods, properties, fields,
   events, delegates, and extension methods.
5. Include parameter, return-value, exception, and example documentation where
   the behavior is non-obvious or externally consumed.
6. Keep comments focused on intent, constraints, and observable behavior rather
   than restating a member name.
7. Re-run the inventory after each project batch until no current public member
   remains undocumented.

### Project batches

1. Abstractions and contracts
2. Core entities and core services
3. HTML and PageEditor-facing models
4. HTTP clients and shared models
5. Current modules and extension points
6. Application services and host integration
7. Internal current projects

Legacy and removal-bound `Aero.Cms.Marten.*` projects, tests, and generated code
remain outside the published API reference scope.

## CI Documentation Workflow

Add an AeroCMS documentation workflow modeled on
`AeroDB/.github/workflows/docs.yml`:

1. Checkout AeroCMS without requiring submodule documentation builds.
2. Install the pinned .NET SDK.
3. Restore and build the current AeroCMS solution in Release mode.
4. Generate XML documentation files.
5. Run the pinned DocFX tool.
6. Install Starlight dependencies with pnpm and a frozen lockfile.
7. Build the Starlight site.
8. Optionally generate `llms-full.txt` from the published content.
9. Upload or deploy the generated site.

The workflow must not build or publish the documentation sites belonging to
Aero, AeroDB, or other submodules.

## Validation Requirements

Before calling the documentation system complete:

- all current public members have XML documentation or an explicitly recorded
  generated-code exclusion;
- DocFX metadata generation succeeds from a clean Release build;
- Starlight builds with `pnpm run build`;
- internal `.docs/` files are not included in the published site accidentally;
- obsolete Marten/NeoUI guidance is absent from the current sidebar;
- API links resolve in the generated site;
- local build instructions work on a clean checkout;
- CI reproduces the local documentation build.

## Proposed Implementation Order

1. Create this plan and confirm scope.
2. Classify and rehome internal planning/orchestration documents.
3. Scaffold the AeroCMS Starlight application and pnpm lockfile.
4. Add the pinned DocFX tool and AeroCMS DocFX configuration.
5. Add the initial current-assembly API allowlist and generated API navigation.
6. Add the CS1591 inventory and document public members in batches.
7. Migrate and rewrite the current developer guides.
8. Add CI build and deployment workflow.
9. Perform a stale-documentation audit and mark superseded material.

## Non-Goals

- Rebuilding or documenting AeroDB, Aero, NeoUI, or other submodule sites.
- Treating Marten legacy projects as current architecture.
- Publishing internal planning documents as user-facing guides.
- Hand-editing generated DocFX output.
- Adding npm-based tooling.
