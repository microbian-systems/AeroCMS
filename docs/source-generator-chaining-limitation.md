
> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). Marten
> was migrated out in [`surrealdb-marten-port.md`](surrealdb-marten-port.md).
> This document is a historical implementation record; its Marten/PostgreSQL
> persistence details do not reflect the current stack.

# Source Generator Chaining Limitation

## Context

AeroCMS uses Roslyn incremental source generators to eliminate runtime reflection
for block model discovery, renderer dispatch, JSON serialization, and Marten document
mapping. One task requires a custom source generator to emit `[JsonSerializable]`
attributes that the built-in `System.Text.Json.JsonSourceGenerator` can consume.

## Root Cause

Roslyn incremental source generators **cannot be chained**. Generator A's output text
(from `RegisterSourceOutput`) is not deterministically visible to Generator B when
both run in the same compilation. The Roslyn compiler runs all generators in a single
pass with non-deterministic ordering, so results vary by build.

## Official Sources

| Source | Link | Statement |
|--------|------|-----------|
| Roslyn issue tracker | [dotnet/roslyn#57239](https://github.com/dotnet/roslyn/issues/57239) | Open since 2020 — "Source generators cannot depend on other source generators" |
| STJ runtime issue | [dotnet/runtime#93439](https://github.com/dotnet/runtime/issues/93439) | STJ team confirms: "SGs in a given project are run in a non-deterministic order" |
| STJ runtime issue | [dotnet/runtime#108317](https://github.com/dotnet/runtime/issues/108317) | "STJ generator won't recognize generated context in scope" |
| STJ runtime issue | [dotnet/runtime#113584](https://github.com/dotnet/runtime/issues/113584) | "Source generators can't see each other" |
| Roslyn incremental generators cookbook | [docs/features/incremental-generators.cookbook.md](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md) | "Code rewriting is out of scope for source generators" |

## Workaround: Multi-Project Shim Pattern

The officially endorsed workaround from the STJ team ([dotnet/runtime#93439 comment](https://github.com/dotnet/runtime/issues/93439#issuecomment-1761099638)):

> *"As a better interim solution, break up your components into separate projects: one project running the STJ source generator and another project running yours on top of the generated code."*

**Architecture:**

```
Aero.Cms.SourceGenerators (analyzer, OutputItemType="Analyzer")
    │  emits partial classes with [JsonSerializable]
    ▼
Aero.Cms.Generated.Json  ← SHIM project — class library
    │  STJ JsonSourceGenerator runs here, sees the emitted attributes
    │  Produces REAL compiled JsonSerializerContext in the output DLL
    ▼
Consuming projects reference Aero.Cms.Generated.Json directly
```

The shim project:
- Is a plain class library (`net10.0`)
- References the source generator project as an analyzer: `OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`
- References the projects containing the real types (`Aero.Cms.Abstractions`, `Aero.Cms.Core`)
- Builds to a DLL containing the compiled `JsonSerializerContext`

## Future Resolution

The .NET 10 preview includes a proposed `[assembly: DefaultJsonSerializerContext]`
attribute ([dotnet/runtime#124889](https://github.com/dotnet/runtime/issues/124889))
that would allow one assembly to declare its canonical `JsonSerializerContext` and
have consuming assemblies auto-chain it without manual wiring. This is under discussion
and not yet merged.

## Impact on AeroCMS

This limitation is why `BlockJsonContext.cs` (in `Aero.Cms.Abstractions`) remains
a manually maintained file with 100+ lines of hand-written `[JsonSerializable]`
attributes. The `BlockRendererGenerator` currently emits `GeneratedBlockJsonRegistration.g.cs`
(metadata arrays only) instead of a real `JsonSerializerContext`.

The shim project `Aero.Cms.Generated.Json` is the chosen workaround. When Roslyn
supports generator chaining or `DefaultJsonSerializerContext` ships, this project
can be retired and the generator can emit directly into the consuming assemblies.
