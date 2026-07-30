# AeroCMS documentation workspace

This directory contains the canonical Starlight source, public AI-ingestion files, manifest, generated DocFX output, and validation scripts.

## Build the documentation site

```powershell
cd docs
pnpm install --frozen-lockfile
pnpm run build
pnpm run check:dist
```

The build regenerates `llms.txt`, `llms-aero-full.txt`, and the public manifest before rendering the site.

## Refresh the public .NET API reference

Install DocFX as a .NET tool, then run:

```powershell
cd docs
pnpm run build:all
```

The API script builds only the selected first-party AeroCMS assemblies. It does not generate standalone API pages for Git submodules, concrete feature modules, persistence internals, generated contexts, validators, or legacy code.
