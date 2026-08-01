# F# Scripting for AeroCMS

**Status:** Proposed future feature — not implemented or scheduled  
**Date:** 2026-08-01  
**Scope:** F# whole-page rendering, Aero page fragments, and trusted content lifecycle automation

## Purpose

AeroCMS may add F# as another server-side scripting language after the existing
Scriban and experimental SharpTS execution boundaries are stable. The feature
should provide an idiomatic F# environment rather than merely exposing C# host
objects that happen to be callable from F#.

The first design goal is:

> User-authored F# receives typed options, results, immutable content data, and
> bounded host capabilities instead of routine CLR nulls, service-provider
> access, `HttpContext`, or live database sessions.

This document is a proposal. It does not describe current runtime behavior.

## Intended rendering model

F# should participate through the renderer registry using stable string IDs,
not another persisted enum member.

```text
Page renderer
├── Aero composition
│   ├── ordinary visual elements
│   ├── Scriban fragment
│   ├── SharpTS fragment
│   ├── F# fragment
│   └── HTMX island/fragment
├── Pure Scriban page
├── Pure SharpTS page
├── Pure F# page
└── Pure HTMX page
```

Provisional identifiers:

| Surface | Stable identifier |
| --- | --- |
| Whole page | `aero.fsharp` |
| Aero composition fragment | `fsharp` |

These identifiers are tentative until the existing page and fragment registries
share the same extensible renderer contract.

The manager experience should eventually include:

- **F#** in the new-page renderer picker;
- an **F#** element in the Aero composition palette;
- the expandable Monaco source editor in F# language mode;
- the same AI-assistance entry point used by other source-backed renderers;
- preview diagnostics with source locations; and
- an explicit warning when changing renderer would discard incompatible source.

## Execution boundary

F# scripts must not compile or execute inside the trusted web process. Neither
`AssemblyLoadContext` nor an in-process F# compiler is a security boundary.

The preferred architecture is a killable worker process or container:

```text
AeroCMS web process
    -> validate source and capability profile
    -> create detached, site-scoped script context
    -> invoke F# worker with deadline and output limits

F# worker
    -> compile or load a versioned artifact
    -> execute the known entry point
    -> return a typed result and diagnostics

AeroCMS web process
    -> validate returned markup
    -> import/render through the normal Aero HTML boundary
```

Two hosting approaches require a focused spike:

1. run `.fsx` through an isolated `dotnet fsi` worker; or
2. host FSharp.Compiler.Service inside the isolated worker for compilation,
   diagnostics, and artifact caching.

The web application should depend only on an Aero-owned `IFSharpScriptEngine`
contract. Compiler APIs, assembly references, package restore, filesystem paths,
environment variables, process creation, and network clients must not be exposed
to ordinary rendering scripts.

## Idiomatic CLR-to-F# transition

The F# integration must normalize CLR nullability before user code receives a
value.

```text
C# host contract
    | may contain null and Nullable<T>
    v
F# transition layer
    | null        -> None
    | value       -> Some value
    | Nullable<T> -> 'T option
    v
User-authored F#
```

Typical mappings are:

| CLR host contract | F# script contract |
| --- | --- |
| `string` | `string` |
| `string?` | `string option` |
| `CustomerDto?` | `Customer option` |
| `int?` | `int option` |
| `DateTimeOffset?` | `DateTimeOffset option` |
| `Task<T?>` | `Task<T option>` |
| `IEnumerable<T?>` | `seq<T option>` |

FSharp.Core already provides `Option.ofObj` for nullable references and
`Option.ofNullable` for `Nullable<'T>`. F# coding conventions recommend keeping
null handling at API boundaries and using options in ordinary F# code.

### Script argument facade

The script should receive an F#-specific facade instead of the raw C# context:

```fsharp
type ScriptArgumentError =
    | ArgumentNotFound of name: string
    | IncorrectType of
        name: string * expectedType: Type * actualType: Type

type FSharpArguments =
    abstract TryGet<'T> : name: string -> 'T option
    abstract Get<'T> : name: string -> Result<'T option, ScriptArgumentError>
    abstract Require<'T> : name: string -> Result<'T, ScriptArgumentError>

type FSharpScriptContext =
    abstract Arguments : FSharpArguments
    abstract User : FSharpUser option
    abstract Page : FSharpPage
    abstract Site : FSharpSite
    abstract Content : FSharpContentQueries
    abstract Logger : FSharpScriptLogger
    abstract CancellationToken : CancellationToken
```

`TryGet` intentionally treats both a missing key and a present null value as
`None`. Mutation and patch operations sometimes require three states, so their
context should expose a separate discriminated union:

```fsharp
type OptionalValue<'T> =
    | Missing
    | Null
    | Value of 'T
```

This avoids confusing “leave unchanged” with “explicitly clear this value.”

### Generated facades

Nullable host DTOs should be projected into F#-friendly facades by an
incremental source generator. AeroCMS must not discover scripting contracts
through runtime reflection.

The generator should consume the C# nullable annotations at build time and
emit deterministic F# contracts or adapter metadata. Generated contracts are
versioned with the capability profile so cached script artifacts cannot run
against an incompatible host surface.

### Return transition

The reverse boundary should normalize F# values explicitly:

```text
F# None       -> successful empty/null host value
F# Some value -> successful host value
F# Ok value   -> successful ScriptResult
F# Error err  -> failed ScriptResult with a safe diagnostic
F# Async<T>   -> awaited worker operation
F# Task<T>    -> awaited worker operation
```

An F# reference option may use `null` to represent `None` at the CLR boundary.
The adapter must pattern-match and normalize it; C# host code must not infer
success or failure from the raw CLR representation.

## Script entry point

The initial renderer contract should require one known function:

```fsharp
let render
    (context: FSharpScriptContext)
    : Task<Result<AeroMarkup, FSharpScriptError>> =
    task {
        // User-authored rendering logic.
        return Ok(AeroMarkup.text "Hello from F#")
    }
```

The exact `AeroMarkup` API needs a spike. Preferred output strategies, in order,
are:

1. an Aero-owned typed HTML DSL that produces validated nodes;
2. an allowlisted RazorEngineCore template key with a detached typed model; or
3. a bounded HTML string that still passes through the strict HTML importer.

F# does not introduce `.fshtml`, a custom Razor parser, or user-authored runtime
`.cshtml`. Razor remains ordinary Razor plus C# behind a separate, allowlisted
host capability.

## Content and query access

Rendering scripts receive the same immutable, bounded, pre-shaped hierarchy and
query results used by Scriban and SharpTS. They traverse children and projected
fields; they do not query Sable or Orleans directly.

The F# facade may expose Snowflake IDs as `int64`, because F# runs on .NET and
can represent the full value safely. Engine-neutral JSON contracts may continue
to serialize IDs as canonical decimal strings; the F# transition layer is
responsible for checked conversion and a typed failure when input is invalid.

Query parameters, culture, site, publication state, permissions, node limits,
depth limits, and output limits are resolved by AeroCMS before worker execution.

## Capability profiles

At least two distinct trust profiles are needed:

### `rendering.safe-v1`

For whole-page and fragment rendering:

- immutable page, site, user, culture, query, and request-parameter snapshots;
- no `HttpContext`, dependency-injection container, raw Sable session, Orleans
  client, filesystem, arbitrary networking, process creation, or package restore;
- bounded time, memory, diagnostics, and output; and
- validated markup before preview or publication.

### `automation.power-v1`

For a later Power User-only pre/post CRUD scripting feature:

- explicit create, update, publish, unpublish, and delete event contracts;
- auditable script identity, version, initiating user, site, and operation;
- pre-event scripts may validate or transform a proposed mutation;
- post-event scripts may schedule follow-up work but cannot alter the committed
  transaction retroactively; and
- database authority must remain tied to the resolved site and operation.

This trusted profile requires a separate threat model. It must not be enabled
merely because the rendering language is F# or because the author can access the
manager. Infinite loops, resource exhaustion, tenant-boundary mistakes, and
unsafe .NET interop remain possible in server-side code.

## Persistence, compilation, and caching

Script source should use the existing immutable source-version/publication model:

- Sable remains the source of truth for source, renderer ID, capability profile,
  culture, validation state, and published version;
- a source hash includes normalized source, host-contract version, F# compiler
  version, referenced allowlist, and capability-profile version;
- diagnostics and normalized source may use FusionCache;
- compiled assembly bytes may use a binary artifact cache; and
- loaded assemblies, delegates, and worker state remain worker-local and are
  never serialized to Garnet or another distributed cache.

Compilation must not occur on every public request. Publication should validate
or compile the selected source version, while public rendering uses a compatible
cached artifact or a controlled one-time worker compilation. Source publication,
capability changes, query dependency changes, and theme changes invalidate the
appropriate output-cache entries.

## Security and operational requirements

- F# is server-side .NET code, not a browser sandbox.
- In-process compilation is not permitted for CMS-authored scripts.
- Every invocation has a deadline, cancellation, memory/output bounds, and a
  killable worker boundary.
- References and packages are host-owned allowlists. Scripts cannot use `#r` or
  `#load` to escape them.
- Secrets, raw connection strings, cookies, API keys, and environment variables
  are never included in the script context or diagnostics.
- Preview uses the same execution and markup-validation boundary as public
  rendering.
- Logs and errors are site-scoped, redacted, bounded, and correlated with a
  source version and invocation ID.
- Publication fails closed when the worker, artifact, capability profile, or
  host contract is unavailable or incompatible.

## Proposed implementation phases

### Phase 0 — hosting and threat spike

- Compare an isolated `dotnet fsi` host with FSharp.Compiler.Service in a worker.
- Prove deadlines, cancellation, forced termination, diagnostics, and repeated
  invocation behavior.
- Prove reference/package restrictions and attempt deliberate escapes.
- Verify the nullable/option and result-return adapters.

### Phase 1 — neutral engine contracts

- Reuse or complete stable page and fragment renderer IDs.
- Define `IFSharpScriptEngine`, detached request/response contracts, capability
  versions, and Railway-oriented host adapters.
- Add the generated F# facade pipeline without runtime reflection.

### Phase 2 — full-page renderer

- Add immutable source versions, Monaco authoring, validation, preview,
  publication, artifact caching, public dispatch, and output-cache dependencies.
- Keep the page shell deployment-owned.

### Phase 3 — Aero composition fragment

- Add the F# palette element and editor dialog.
- Reuse the same worker, context, markup import, diagnostics, and AI affordance.

### Phase 4 — trusted lifecycle automation

- Design pre/post CRUD event contracts and Power User permissions separately.
- Add auditing, idempotency, retry rules, and transaction-boundary tests.
- Do not reuse the safe rendering profile for mutation authority.

## Acceptance criteria before implementation is considered complete

- F# source can render a full page and an Aero composition fragment through the
  same public/preview validation boundary.
- User F# code receives options and native F# results instead of ordinary CLR
  null handling.
- No script can obtain undeclared host services or references.
- Timeouts terminate the worker rather than merely cancelling a task in the web
  process.
- Compiled artifacts are versioned and are not rebuilt on every request.
- Save/reload, preview, publish, public rendering, culture selection, query
  parameters, and output-cache invalidation have automated coverage.
- Invalid source, missing entry points, type mismatches, oversized output,
  worker crashes, and incompatible artifacts fail closed with useful diagnostics.
- Any future CRUD capability has separate permissions, auditing, site isolation,
  transaction semantics, and tests.

## Open decisions

1. Whether the first worker uses `dotnet fsi` or FSharp.Compiler.Service.
2. Whether the initial markup API is a typed F# HTML DSL or strict imported HTML.
3. Whether RazorEngineCore templates are available in the first release or a
   later capability profile.
4. Which FSharp.Core and compiler versions define the initial artifact contract.
5. Whether compiled artifacts are stored only in cache or also as versioned
   publication artifacts in Sable/object storage.
6. Whether lifecycle automation belongs in the eventual F# module or a neutral
   scripting-automation module shared with SharpTS.

## Related documents

- [Neo Rendering and Content Types Strategy](neo-rendering+content-types-staretgy.md)
- [SharpTS TypeScript Dynamic Pages and Elements](../sharpts-typescript-dynamic-rendering.md)
- [Aero Scripting Security](../aero-scripting-security.md)
- [Tentative Future Features](../future-feature-list.md)

## References

- [Microsoft Learn: F# Interactive and `.fsx` scripting](https://learn.microsoft.com/dotnet/fsharp/tools/fsharp-interactive/)
- [Microsoft Learn: F# options](https://learn.microsoft.com/dotnet/fsharp/language-reference/options)
- [Microsoft Learn: F# null values](https://learn.microsoft.com/dotnet/fsharp/language-reference/values/null-values)
- [Microsoft Learn: F# coding conventions for nulls and defaults](https://learn.microsoft.com/dotnet/fsharp/style-guide/conventions#nulls-and-default-values)
- [Microsoft Learn: nullable value types in F#](https://learn.microsoft.com/dotnet/fsharp/language-reference/nullable-value-types)
