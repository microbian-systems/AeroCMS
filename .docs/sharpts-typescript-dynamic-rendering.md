# SharpTS TypeScript Dynamic Pages and Elements

## Status

**Proposed architecture / implementation plan**

This document records the proposed SharpTS integration for AeroCMS. No runtime,
persistence, routing, editor, or package changes are implemented by this document.
The design must be validated with a focused SharpTS hosting spike before the
public contracts are treated as accepted.

Date: 2026-07-19
Updated: 2026-07-23

## Executive Summary

AeroCMS will add TypeScript as an optional server-side rendering engine alongside
the existing static HTML and Scriban renderers. A TypeScript template may render:

- a complete dynamic page from a `.page.ts` source file;
- a reusable dynamic element from a `.element.ts` source file; or
- a dynamic slot embedded in a page created by the existing HTML PageEditor.

The primary implementation belongs in a new module project:

```text
src/Aero.Cms.Modules.TypeScript/
  Aero.Cms.Modules.TypeScript.csproj
```

Use `TypeScript` casing in the assembly, namespace, directory, and module name.
`Aero.Cms.Modules.Typescript` is understandable, but `TypeScript` matches the
product and language name.

The module will reference the `SharpTS` NuGet package. It will own all
SharpTS-specific hosting, compilation, execution, persistence, caching,
capability, diagnostics, and administration behavior. SharpTS types must not
leak into `Aero.Cms.Core`, `Aero.Cms.Html`, `Aero.Cms.Modules.Pages`, or public
module contracts.

TypeScript pages may obtain their data in any of these modes:

1. an immutable model supplied by the caller;
2. a request-scoped read-only document query provider;
3. a request-scoped Orleans-backed query provider; or
4. a composite provider that routes individual capabilities to different
   backends.

The TypeScript-facing API remains the same in every mode. Templates receive an
AeroCMS capability object such as `context.data`; they do not receive raw
`IDocumentSession`, `IGrainFactory`, grain references, or the
application service provider.

Developer-authored, reviewed TypeScript may run in-process. TypeScript authored
or changed by CMS users must eventually run in a separate restricted worker
process. A collectible `AssemblyLoadContext` can help unload compiled artifacts,
but it is not a security boundary.

## Context

AeroCMS already has two relevant rendering paths:

- the PageEditor persists a constrained Living Standard HTML tree and renders it
  through `HtmlStaticRenderer`; and
- runtime-defined content types render CMS-authored Scriban through
  `SecureScribanRenderer`.

The Scriban renderer establishes several useful precedents:

- validate syntax and model shape before execution;
- cache parsed artifacts by definition and version;
- create isolated state for every render;
- impose time, recursion, loop, input, and output limits;
- disable runtime evaluation and broad CLR access;
- convert expected failures into `Result<T>`; and
- sanitize successful HTML.

SharpTS enables richer server-side logic and first-class TypeScript, but its
capability surface is much broader than a template engine. It supports:

- interpreted TypeScript;
- ahead-of-time TypeScript-to-.NET IL compilation;
- .NET type imports;
- .NET assembly references;
- Node-style built-in modules;
- asynchronous code and an event loop; and
- compiled assembly consumption from .NET.

That power makes it suitable for dynamic pages, but it means a `.ts` page is
server-side code rather than harmless markup. The architecture must make that
trust boundary explicit.

## Goals

- Render complete server-side pages from TypeScript.
- Render reusable TypeScript elements inside existing PageEditor pages.
- Support both caller-supplied models and template-initiated data queries.
- Allow document-backed or Orleans-backed data providers per template.
- Keep the TypeScript authoring contract independent of the selected provider.
- Preserve AeroCMS site, tenant, culture, publication, authorization, and cache
  boundaries.
- Reuse the existing HTML catalog, importer, validation, static rendering, output
  caching, and cache-tag invalidation infrastructure.
- Support interpreted execution for fast previews and AOT compilation for
  trusted published artifacts where it proves beneficial.
- Keep SharpTS isolated in one module and behind AeroCMS-owned contracts.
- Use Railway Oriented Programming for validation, compilation, data access, and
  rendering results.
- Provide deterministic diagnostics suitable for the manager UI and API.
- Avoid NPM and JavaScript runtime dependencies.

## Non-Goals

- Executing TypeScript in the browser.
- Allowing templates to emit client-side `<script>` elements or event-handler
  attributes.
- Exposing arbitrary CLR types, NuGet packages, assemblies, files, processes,
  network clients, environment variables, secrets, or the DI container.
- Allowing a page GET render to perform database or grain mutations.
- Treating `AssemblyLoadContext` as a sandbox.
- Adding a `SharpTs` value to `HtmlNodeKind`.
- Replacing Scriban or the existing static HTML renderer.
- Supporting arbitrary NPM JavaScript packages.
- Designing action scripts, form handlers, or mutation endpoints in the first
  delivery. A future `*.action.ts` pipeline would require separate authorization,
  CSRF, validation, transaction, and idempotency rules.
- Supporting a custom `.tshtml`, mixed Razor/TypeScript, or Vue/Svelte-style
  single-file component parser.

## SharpTS Package and Runtime Findings

### Package snapshot

The versions available from NuGet on 2026-07-19 are:

| Package | Version | Intended use |
|---|---:|---|
| `SharpTS` | `1.0.8` | Runtime interpreter/compiler API used by the AeroCMS module |
| `SharpTS.Sdk` | `1.0.7` | Optional MSBuild SDK for developer-authored AOT TypeScript projects |

The package versions are not currently in lockstep. They must be pinned and
tested independently.

The required central package version is:

```xml
<!-- src/Directory.Packages.props -->
<PackageVersion Include="SharpTS" Version="1.0.8" />
```

The module project then uses:

```xml
<PackageReference Include="SharpTS" />
```

Do not add `SharpTS.Sdk` as a normal `PackageReference` to the runtime module.
SharpTS documents it as an MSBuild project SDK:

```xml
<Project Sdk="SharpTS.Sdk/1.0.7">
```

If AeroCMS later adds a separate developer-authored TypeScript artifact project,
pin the MSBuild SDK under the applicable `global.json`:

```json
{
  "msbuild-sdks": {
    "SharpTS.Sdk": "1.0.7"
  }
}
```

The module itself remains a normal .NET/Razor project. It must not replace
`Microsoft.NET.Sdk.Razor` with `SharpTS.Sdk`.

### Execution modes

SharpTS supports:

- tree-walking interpreted execution for scripts and rapid startup; and
- AOT compilation to .NET assemblies.

The first implementation should support an AeroCMS abstraction for both modes,
but it should begin with the smallest mode that can satisfy the hosting spike.
The published artifact identifies its execution mode so a later change does not
silently alter runtime semantics.

### .NET interop

SharpTS supports `dotnet:` imports and `@DotNetType`. Its documentation states
that referenced assemblies run with full trust. The repository also includes
Node-style filesystem, process, child-process, networking, worker, and other
runtime modules.

The current interpreter source includes paths that call
`System.Environment.Exit`, including `process.exit()` and numeric `main()`
return handling. It also includes child-process implementations based on
`System.Diagnostics.Process`.

Consequences:

- arbitrary TypeScript cannot safely run inside the web process;
- an execution timeout alone is not a sufficient sandbox;
- arbitrary `dotnet:` imports and `sharpts.json` assembly/package references
  must be rejected for CMS-authored templates;
- a trusted in-process mode must remain clearly labeled and restricted; and
- untrusted execution requires a separate process with operating-system limits.

### Threading

SharpTS documents its interpreter as not thread-safe. Delegate callbacks that
enter the interpreter from another thread can cause races, corrupted state, or
crashes.

Consequences:

- never register one interpreter instance as a singleton;
- never execute two renders concurrently through the same interpreter;
- create a fresh execution context per render;
- do not allow guest timers or callbacks to escape the render lifetime;
- run one interpreter job at a time per isolated worker; and
- include concurrency and callback tests in the initial spike.

### Cancellation and hosting API

The current interpreter exposes a VM timeout token that is checked during
statement execution and supports registering globals. This is useful but does
not prove that every blocking built-in operation is interruptible.

The initial spike must verify:

- the supported source-to-AST and source-to-execution API;
- how to invoke an exported async `render` function and retrieve its value;
- how cancellation behaves in loops, promises, timers, I/O, and .NET calls;
- how stdout and stderr are captured;
- how guest errors map to host exceptions;
- whether built-in modules can be replaced or disabled using public APIs;
- how interpreter state is reset and disposed; and
- whether runtime compilation APIs are stable enough for persisted templates.

No production implementation should depend on private SharpTS members or copy
its CLI orchestration without an explicit compatibility decision.

## Architectural Decisions

### 0. Separate whole-page renderers from embedded fragment renderers

AeroCMS has two renderer levels:

```text
Page renderer
├── Aero composition
│   ├── ordinary visual elements
│   ├── Scriban fragment
│   ├── SharpTS fragment
│   └── HTMX island/fragment
├── Pure Scriban page
├── Pure SharpTS page
└── Pure HTMX page
```

The whole-page renderer is selected from page metadata. The initial stable
renderer identifiers are:

```text
aero.composition
aero.scriban
aero.sharpts
aero.htmx
```

Renderer identifiers are persisted as canonical strings and may be represented
by value objects in .NET; they are not enum members. A renderer registry
resolves the identifier to an `IPageRenderer`, so an optional rendering module
can add a renderer without modifying a closed Pages-module enum. Page role,
such as standard page, homepage, or blog listing, is independent of the
renderer identifier.

`aero.composition` is the existing visual page model. It may contain ordinary
HTML elements and registered Scriban, SharpTS, or HTMX fragments.

`aero.scriban` and `aero.sharpts` are code-first pages whose source owns the
page body rather than a visual composition tree.

`aero.htmx` is an HTMX-first page. It still produces an initial server-rendered
HTML response, then uses registered, authorized fragment endpoints for swaps.
It does not give persisted page source arbitrary endpoint URLs, database
access, or a new client-side execution environment. HTMX islands remain usable
inside `aero.composition`; the whole-page renderer is an additional authoring
and dispatch option.

The deployment-owned `Page.cshtml` remains the final public shell for every
renderer. It resolves the selected renderer through a coordinator and emits
the returned validated page result.

### 1. Add `Aero.Cms.Modules.TypeScript`

The feature belongs in:

```text
src/Aero.Cms.Modules.TypeScript/
```

The module owns:

- SharpTS package references;
- template definitions and published versions;
- source validation and diagnostics;
- interpreted and compiled execution adapters;
- runtime capability construction;
- TypeScript declaration generation;
- data-provider adapters;
- cache keys, dependency collection, and invalidation;
- minimal administration APIs;
- preview and publication workflows;
- optional manager UI components;
- OpenTelemetry activities and metrics; and
- module-specific TUnit and integration tests.

Provider-neutral extension points needed by the existing Pages or HTML projects
may live in `Aero.Cms.Core` or `Aero.Cms.Html`. Those contracts must not mention
SharpTS.

The expected module type is:

```csharp
[Module(nameof(TypeScriptModule))]
public sealed class TypeScriptModule : AeroWebModule, IConfigureAeroDB
{
    public override string Name => nameof(TypeScriptModule);

    public override IReadOnlyList<string> Dependencies =>
        ["CacheModule", "ContentModule", "PagesModule", "SitesModule"];
}
```

The exact dependency list must follow the module loader's verified names and
only include modules whose services are actually required.

### 2. Keep SharpTS-specific types inside the module

The Pages module should depend on a neutral renderer contract, for example:

```csharp
public readonly record struct PageRendererId(string Value);

public interface IPageRenderer
{
    PageRendererId Id { get; }

    Task<Result<PageRenderResult>> RenderAsync(
        PageRenderRequest request,
        CancellationToken cancellationToken);
}
```

The TypeScript module supplies the `aero.sharpts` implementation. The Pages
module owns the `aero.composition` coordinator. Scriban and HTMX integrations
provide their own implementations. A registry resolves renderers by stable ID;
no renderer-specific persisted enum is required.

### 3. Expose one stable TypeScript host contract

All template data access flows through an AeroCMS-owned context:

```csharp
public interface ITypeScriptTemplateContext
{
    TypeScriptRouteContext Route { get; }
    TypeScriptSiteContext Site { get; }
    TypeScriptRequestContext Request { get; }
    ITemplateDataContext Data { get; }
}
```

The TypeScript declaration presents a stable equivalent:

```ts
interface PageContext<TModel> {
    readonly model: TModel;
    readonly route: RouteContext;
    readonly site: SiteContext;
    readonly request: SafeRequestContext;
    readonly data: TemplateDataContext;
}
```

The contract is versioned using a host-contract version included in compiled
artifact and cache keys.

### 4. Support multiple data providers behind the same context

```csharp
public enum TemplateDataProviderKind
{
    SuppliedModel,
    DocumentQuery,
    Orleans,
    Composite
}
```

The provider is selected by the published template definition, not by arbitrary
guest imports.

The TypeScript template calls the same `context.data` methods regardless of the
provider. AeroCMS may therefore move a data path from direct document queries to
Orleans without rewriting templates.

### 5. Never inject raw infrastructure

Do not inject:

- `IDocumentSession`;
- `IGrainFactory`;
- `IClusterClient`;
- existing broad grain references;
- `IServiceProvider`;
- `HttpContext`;
- `IConfiguration`;
- logging providers;
- caches; or
- message buses.

`IDocumentSession` exposes mutation and commit behavior. Existing AeroCMS grain
interfaces also combine reads with publish, unpublish, save, and delete methods.
Exposing either directly would let a public GET render mutate application state.

Instead, provide narrow read-only template capabilities. Their implementations
may use document sessions or grains internally.

### 6. Treat page rendering as read-only

Dynamic page rendering must be safe to retry, cache, execute concurrently, and
invoke during previews. It therefore cannot perform commands.

If future TypeScript actions are introduced, they must be a separate feature
with separate file kinds, endpoints, authorization, antiforgery protection,
validation, idempotency, audit records, and transaction semantics.

### 7. Validate generated HTML through the existing HTML pipeline

SharpTS output must never be passed directly to `Html.Raw`.

The output pipeline is:

```text
SharpTS string or fragment
  -> output size check
  -> HTML fragment import
  -> catalog and attribute validation
  -> parent/child containment validation
  -> sanitization
  -> HtmlStaticRenderer
  -> final markup
```

Scripts, event-handler attributes, unsupported elements, unsafe URLs, arbitrary
styles, and invalid nesting remain rejected.

### 8. Keep `HtmlNodeKind` closed

Do not add `SharpTs` to the persisted HTML node enum. The page tree remains:

- fragment;
- catalog-backed element; and
- encoded text.

PageEditor integration uses a real inert HTML `<template>` placeholder plus
structured sidecar metadata. Dynamic output is expanded before final static
rendering.

### 9. Separate trust and execution mode

```csharp
public enum TypeScriptTrustMode
{
    TrustedDeveloper,
    CmsAuthoredIsolated
}

public enum TypeScriptExecutionMode
{
    Interpreted,
    Compiled
}
```

Trust and execution are different decisions. Compiled code is not safer merely
because it is compiled, and interpreted code is not sandboxed merely because it
has a timeout.

## High-Level Rendering Flow

```text
HTTP request
  -> resolve site, culture, route, and publication state
  -> resolve page-renderer strategy
  -> load published TypeScript template version
  -> resolve validated artifact from artifact cache
  -> create request-scoped capability context
  -> execute render(context)
      -> supplied model and/or data-provider calls
      -> dependency collector records every data dependency
  -> validate and import generated HTML
  -> static HTML rendering
  -> return markup, diagnostics, and cache dependencies
  -> output cache stores response with collected tags
```

Preview uses the draft template version and is always `no-store`.

## Proposed Project Layout

```text
src/Aero.Cms.Modules.TypeScript/
  Aero.Cms.Modules.TypeScript.csproj
  TypeScriptModule.cs

  Configuration/
    TypeScriptRenderingOptions.cs
    TypeScriptCapabilityOptions.cs

  Definitions/
    TypeScriptTemplateDefinition.cs
    TypeScriptTemplateVersion.cs
    TypeScriptTemplateKind.cs
    TypeScriptExecutionMode.cs
    TypeScriptTrustMode.cs
    TemplateDataProviderKind.cs
    TemplateCachePolicy.cs

  Compilation/
    ITypeScriptCompiler.cs
    SharpTsTypeScriptCompiler.cs
    TypeScriptCompilationRequest.cs
    TypeScriptCompilationResult.cs
    TypeScriptDiagnostic.cs
    TypeScriptArtifact.cs
    TypeScriptArtifactCache.cs

  Execution/
    ITypeScriptExecutor.cs
    SharpTsInterpretedExecutor.cs
    SharpTsCompiledExecutor.cs
    TypeScriptExecutionRequest.cs
    TypeScriptExecutionResult.cs
    TypeScriptExecutionScope.cs

  Hosting/
    TypeScriptTemplateContext.cs
    TypeScriptRouteContext.cs
    TypeScriptSiteContext.cs
    SafeRequestContext.cs
    TypeScriptHostContractVersion.cs

  Data/
    ITemplateDataContext.cs
    ITemplateDataProvider.cs
    SuppliedModelTemplateDataProvider.cs
    DocumentTemplateDataProvider.cs
    OrleansTemplateDataProvider.cs
    CompositeTemplateDataProvider.cs
    TemplateDataQuery.cs
    TemplateDataPage.cs
    TemplateValue.cs

  Caching/
    TypeScriptCacheKeys.cs
    TypeScriptCacheTags.cs
    RenderDependencyCollector.cs
    TypeScriptCacheInvalidator.cs

  Rendering/
    TypeScriptPageRenderStrategy.cs
    TypeScriptElementRenderer.cs
    TypeScriptHtmlResultValidator.cs
    TypeScriptRenderResult.cs

  Publishing/
    TypeScriptTemplatePublishingService.cs
    TypeScriptPublicationValidator.cs

  Areas/Api/v1/
    TypeScriptTemplatesApi.cs

  Areas/TypeScript/Pages/
    Templates/
      Index.cshtml
      Index.cshtml.cs
      Edit.cshtml
      Edit.cshtml.cs

  Declarations/
    AeroTypeScriptDeclarations.cs
    GeneratedModelDeclarations.cs
```

If CMS-authored TypeScript is implemented, add a separate executable:

```text
src/Aero.Cms.TypeScript.Worker/
  Aero.Cms.TypeScript.Worker.csproj
```

That worker is an isolation boundary and should not be hidden inside the web
module project.

If compile-time generation of `.d.ts` declarations is adopted, a separate
analyzer/source-generator project may also be justified:

```text
src/Aero.Cms.TypeScript.Generators/
```

Those additional projects are deferred until their boundaries are required.
The primary feature project remains `Aero.Cms.Modules.TypeScript`.

## Proposed Module Project

The initial project should remain a normal Razor class library:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="SharpTS" />
    <PackageReference Include="ZiggyCreatures.FusionCache" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Aero\src\Aero.Core\Aero.Core.csproj" />
    <ProjectReference Include="..\..\Aero\src\Aero.Modular\Aero.Modular.csproj" />
    <ProjectReference Include="..\Aero.Cms.Abstractions\Aero.Cms.Abstractions.csproj" />
    <ProjectReference Include="..\Aero.Cms.Core\Aero.Cms.Core.csproj" />
    <ProjectReference Include="..\Aero.Cms.Core.Entities\Aero.Cms.Core.Entities.csproj" />
    <ProjectReference Include="..\Aero.Cms.Html\Aero.Cms.Html.csproj" />
    <ProjectReference Include="..\..\AeroDB\src\AeroDB.Sable\AeroDB.Sable.csproj" />
  </ItemGroup>

</Project>
```

This is a design sketch, not a compile-ready final reference list. Implementation
must prove the minimum dependency set and avoid depending directly on entire
feature modules when contracts can live in Abstractions or Core.

The project will be added to:

- `src/Aero.Cms.slnx`;
- the server host project or generated module catalog;
- the module meta-bundle if TypeScript rendering is part of the default
  distribution; and
- test projects that exercise dynamic rendering.

## Persistence Model

### Template definition

```csharp
public sealed class TypeScriptTemplateDefinition : Entity
{
    public long SiteId { get; set; }
    public required string Name { get; set; }
    public required string Alias { get; set; }
    public TypeScriptTemplateKind Kind { get; set; }
    public string DraftSource { get; set; } = string.Empty;
    public int DraftVersion { get; set; }
    public int? PublishedVersion { get; set; }
    public TemplateDataProviderKind DataProvider { get; set; }
    public TypeScriptTrustMode TrustMode { get; set; }
    public TypeScriptExecutionMode ExecutionMode { get; set; }
    public TemplateCachePolicy CachePolicy { get; set; } = new();
    public string HostContractVersion { get; set; } = string.Empty;
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset UpdatedOn { get; set; }
    public string? UpdatedBy { get; set; }
}
```

The entity uses the existing Snowflake-backed `long` identity inherited from
`Entity`.

### Immutable published version

```csharp
public sealed class TypeScriptTemplateVersion : Entity
{
    public long SiteId { get; set; }
    public long DefinitionId { get; set; }
    public int Version { get; set; }
    public required string Source { get; set; }
    public required string SourceHash { get; set; }
    public required string SharpTsVersion { get; set; }
    public required string HostContractVersion { get; set; }
    public TypeScriptExecutionMode ExecutionMode { get; set; }
    public string? ArtifactLocation { get; set; }
    public IReadOnlyList<TypeScriptDiagnostic> Diagnostics { get; set; } = [];
    public DateTimeOffset PublishedOn { get; set; }
    public string? PublishedBy { get; set; }
}
```

Publishing creates an immutable version only after validation and compilation
succeed. Public requests never execute an unvalidated draft.

Large compiled artifacts should not automatically be embedded in the main
definition document. Store artifact metadata in the definition/version record
and use an artifact store appropriate to the deployment. The initial
implementation may use a bounded process-local artifact cache while the
artifact persistence boundary is evaluated.

### Page renderer reference

Avoid adding SharpTS-specific fields directly to `PageDocument`. Use a neutral
renderer reference:

```csharp
public sealed record PageRendererReference
{
    public required PageRendererId RendererId { get; init; }
    public long? DefinitionId { get; init; }
    public int? Version { get; init; }
}
```

Examples:

```text
RendererId = "aero.composition"
RendererId = "aero.scriban"
RendererId = "aero.sharpts"
RendererId = "aero.htmx"
```

`aero.composition` remains the default when no reference is present.

## TypeScript Authoring Contract

### Page files

Complete pages use `.page.ts`:

```ts
interface ArticlePageModel {
    title: string;
    summary?: string;
    author: string;
}

export default definePage<ArticlePageModel>(async ({ model, route, data }) => {
    const related = await data.content.list<ArticleSummary>({
        type: "article",
        culture: route.culture,
        take: 3
    });

    return html`
        <main class="article-page">
            <article>
                <h1>${model.title}</h1>
                ${model.summary
                    ? html`<p class="summary">${model.summary}</p>`
                    : empty}
                <footer>${model.author}</footer>
            </article>

            <aside>
                <h2>Related articles</h2>
                <ul>
                    ${related.items.map(item =>
                        html`<li><a href="${item.url}">${item.title}</a></li>`)}
                </ul>
            </aside>
        </main>
    `;
});
```

### Element files

Reusable PageEditor elements use `.element.ts`:

```ts
interface ProductCardModel {
    id: AeroId;
    name: string;
    price: Money;
    imageUrl?: string;
}

export default defineElement<ProductCardModel>(({ model }) =>
    html`
        <article class="product-card">
            ${model.imageUrl
                ? html`<img src="${model.imageUrl}" alt="" />`
                : empty}
            <h3>${model.name}</h3>
            <p>${model.price.formatted}</p>
        </article>
    `
);
```

### `html` tagged-template behavior

The `html` tagged-template helper is the selected SharpTS authoring syntax.
AeroCMS will not add a custom `.tshtml` or mixed Razor/TypeScript source format.

Required behavior:

- strings are HTML-encoded by default;
- `null`, `undefined`, and `empty` emit no markup;
- arrays of fragments are flattened in order;
- nested `HtmlFragment` values are accepted;
- arbitrary objects are rejected;
- attribute contexts are encoded and validated;
- URL attributes pass the existing safe-URL policy;
- output length is bounded;
- unsupported raw HTML values are rejected; and
- a narrowly named `safeHtml` escape hatch is unavailable unless the value was
  produced by an AeroCMS HTML validator.

The helper should produce an internal fragment representation when practical,
not merely concatenate unchecked strings.

### Identifier representation

AeroCMS Snowflake identifiers are .NET `long` values. JavaScript/TypeScript
`number` cannot precisely represent every 64-bit integer.

Identifiers therefore cross the TypeScript boundary as strings:

```ts
type AeroId = string & { readonly __aeroId: unique symbol };
```

The host validates and converts those strings back to `long`. Do not marshal
Snowflake IDs as ordinary TypeScript numbers.

## Data Access Contract

### Stable surface

```ts
interface TemplateDataContext {
    readonly content: ContentQueries;
    readonly pages: PageQueries;
    readonly site: SiteQueries;
}

interface ContentQueries {
    getById<T>(type: string, id: AeroId): Promise<T | null>;
    getBySlug<T>(
        type: string,
        slug: string,
        culture?: string
    ): Promise<T | null>;
    list<T>(query: ContentListQuery): Promise<ResultPage<T>>;
}

interface ContentListQuery {
    type: string;
    culture?: string;
    skip?: number;
    take?: number;
    orderBy?: string;
    descending?: boolean;
    filters?: Readonly<Record<string, string>>;
}

interface ResultPage<T> {
    readonly items: readonly T[];
    readonly totalCount: number;
    readonly skip: number;
    readonly take: number;
}
```

The actual first-release surface should remain smaller than this example and
expand additively. Templates will depend on every observable behavior, so
ordering, null semantics, errors, paging defaults, and field naming must be
intentional.

### Supplied model provider

The caller resolves all data before rendering and supplies immutable JSON-like
values. This is the most deterministic and cacheable mode.

Use it for:

- simple content pages;
- reusable elements;
- places where an application service already owns the query; and
- high-volume pages where query planning should not be template-controlled.

### Document query provider

The document provider:

- creates a read-only query session per render;
- disposes it at render completion;
- forces the current site and culture;
- applies publication-state rules;
- applies a maximum `take`;
- applies a maximum number of calls per render;
- rejects arbitrary query expressions and type names;
- exposes only registered content projections;
- returns plain immutable values; and
- records cache dependencies.

The provider should wrap existing read-only services such as
`IContentQueryService` rather than expose a session directly.

### Orleans provider

The Orleans provider:

- resolves grains internally;
- invokes only approved read methods;
- does not expose `IGrainFactory`;
- does not expose broad existing actor interfaces to TypeScript;
- applies a per-call deadline;
- limits calls and fan-out;
- converts Orleans view models into template values; and
- records the same cache dependencies as the document provider.

Orleans grain references implement their grain interfaces. Existing AeroCMS
actor interfaces include mutations, so direct injection would expose publish,
unpublish, save, and delete behavior.

Where possible, introduce query-only application or actor contracts rather than
filtering a mixed command/query interface after the fact.

### Composite provider

The composite provider can route capabilities independently:

```text
data.content -> Orleans
data.pages   -> document query
data.site    -> supplied request snapshot
```

The template does not know which provider fulfilled the call.

### Data budgets

Suggested initial defaults:

| Limit | Initial default |
|---|---:|
| Data calls per render | 16 |
| Maximum `take` per call | 100 |
| Total returned items | 250 |
| Total serialized data | 1 MiB |
| Provider timeout | bounded by the render timeout |

These values belong in options and require load testing before acceptance.

## Capability Profiles

Each published template references an allowlisted capability profile:

```csharp
public sealed class TypeScriptCapabilityProfile : Entity
{
    public long SiteId { get; set; }
    public required string Name { get; set; }
    public IReadOnlySet<string> Capabilities { get; set; } = new HashSet<string>();
    public IReadOnlySet<string> ContentTypes { get; set; } = new HashSet<string>();
    public int MaxDataCalls { get; set; }
    public int MaxReturnedItems { get; set; }
}
```

Example capabilities:

```text
content.read
pages.read
site.settings.read-public
media.resolve-public-url
```

Capabilities are additive and explicitly granted. Absence means denial.

Secrets, authentication tokens, private settings, user records, write
operations, filesystem access, process access, and network access are never
template capabilities.

## PageEditor Integration

### Placeholder representation

Use the native inert HTML `<template>` element:

```html
<template
    data-aero-slot="featured-article"
    data-aero-template-id="784921337102">
</template>
```

Attributes are useful for editor display and diagnostics, but they are not the
authoritative execution configuration.

Persist structured sidecar data keyed by `HtmlNode.NodeId`:

```csharp
public sealed record DynamicTemplateSlot
{
    public long NodeId { get; init; }
    public long DefinitionId { get; init; }
    public int Version { get; init; }
    public required string ModelBindingKey { get; init; }
    public TemplateFailureMode FailureMode { get; init; }
}
```

The PageEditor:

- displays the placeholder as a dynamic element;
- lets the editor choose a published element template;
- shows the expected input schema;
- provides structured bindings to an approved model source;
- requests a server-rendered preview;
- never executes SharpTS in WebAssembly; and
- keeps undo/redo operations over the placeholder and sidecar metadata.

### Expansion

Before final rendering:

1. validate that the placeholder and sidecar entry agree;
2. load the published template version;
3. resolve the slot model;
4. execute the element;
5. import the returned fragment;
6. validate the fragment against the placeholder's parent;
7. replace the placeholder in an ephemeral render tree; and
8. pass that tree to `HtmlStaticRenderer`.

The persisted draft/published HTML tree remains unchanged by request-time
expansion.

### Failure modes

Supported failure modes should be explicit:

```csharp
public enum TemplateFailureMode
{
    FailPage,
    RenderNothing,
    RenderEditorFallback
}
```

`RenderEditorFallback` is preview-only. Public rendering must not expose
diagnostics or stack traces in markup.

The recommended default is `FailPage` for complete pages and `RenderNothing`
only for explicitly optional elements.

## Compilation and Publication Lifecycle

### Draft save

Saving a draft:

1. validates source size and encoding;
2. parses and type-checks the TypeScript;
3. validates imports and capabilities;
4. validates the exported entry-point signature;
5. produces structured diagnostics;
6. increments the draft version; and
7. does not affect public rendering or public caches.

### Preview

Preview:

1. uses the draft source;
2. runs under preview limits;
3. uses preview-safe data providers;
4. records diagnostics and resource usage;
5. sanitizes and validates output; and
6. returns `no-store`.

### Publish

Publishing:

1. repeats validation against the current SharpTS and host-contract versions;
2. compiles an artifact when compiled mode is selected;
3. runs contract and smoke validation;
4. creates an immutable template version;
5. updates the definition's published pointer atomically;
6. invalidates artifact and output caches by template tag; and
7. records publisher identity and diagnostics.

A failed compilation or smoke render leaves the previous published version
active.

### Upgrade behavior

SharpTS is a young dependency and its hosting behavior may change. Published
versions therefore record:

- SharpTS package version;
- AeroCMS host-contract version;
- execution mode;
- source hash; and
- artifact format version.

Upgrading SharpTS requires:

1. restoring the new pinned package;
2. running the compatibility and security suite;
3. recompiling representative published templates;
4. comparing rendered output;
5. validating unload behavior and memory usage; and
6. deliberately republishing or rebuilding artifacts.

Do not silently execute an old compiled artifact under an incompatible host
contract.

## Caching and Invalidation

### Artifact cache

Key:

```text
typescript:artifact:
  {definitionId}:
  {version}:
  {sourceHash}:
  {sharpTsVersion}:
  {hostContractVersion}:
  {executionMode}
```

The cached value may contain:

- parsed or compiled immutable artifact data;
- validated entry-point metadata;
- generated declarations;
- normalized diagnostics; and
- artifact location.

Do not cache a mutable or concurrently shared interpreter instance.

### Output cache

Key inputs:

- site ID;
- culture;
- normalized route;
- template definition and published version;
- model/content version;
- safe vary-by values;
- layout/theme version; and
- host-contract version.

Drafts and previews are never output-cached.

### Dependency collection

Every data-provider call records tags:

```text
typescript-template-{definitionId}
content-type-{siteId}-{alias}
content-item-{siteId}-{itemId}
page-id-{pageId}
page-slug-{slug}
site-{siteId}
culture-{culture}
```

The final render result includes the collected tags:

```csharp
public sealed record TypeScriptRenderResult(
    string Html,
    IReadOnlySet<string> CacheTags,
    bool IsCacheable,
    TypeScriptRenderMetrics Metrics);
```

The output-cache policy applies those tags. Publishing or changing a dependency
evicts affected output.

### Automatic no-cache conditions

Rendering becomes private or uncacheable if a template accesses:

- authenticated user data;
- authorization-sensitive data;
- request-specific headers or cookies;
- current time;
- random values;
- non-deterministic external data; or
- any capability whose provider cannot report reliable dependencies.

The capability object, not guest code, decides cacheability.

## Execution Isolation

### Trusted in-process mode

Trusted mode is limited to source that is:

- developer-authored;
- code-reviewed;
- deployed with the application or explicitly approved by an administrator;
- unable to import arbitrary packages or assemblies; and
- covered by the template test suite.

Even in trusted mode:

- use a per-render interpreter/execution scope;
- enforce time, output, query, and data limits;
- capture stdout and stderr;
- disable dynamic code generation where supported;
- reject arbitrary built-in modules;
- sanitize output; and
- do not expose raw infrastructure.

### CMS-authored isolated mode

CMS-authored TypeScript requires a separate worker process. The worker receives:

- source or a validated artifact;
- plain immutable render context;
- a capability token scoped to one render;
- a render deadline;
- resource limits; and
- a correlation ID.

The worker returns:

- rendered HTML;
- normalized diagnostics;
- data-capability requests and responses;
- resource metrics; and
- dependency identifiers.

The worker must run:

- as an unprivileged identity;
- with no application secrets;
- without direct database credentials;
- without direct Orleans cluster credentials;
- without arbitrary network access;
- without writable application directories;
- with CPU and memory limits;
- with a hard process termination deadline; and
- with a bounded request and response size.

Data access crosses a narrow RPC boundary back to the host. The host validates
the render token, site, culture, capability, query, row limit, and deadline
before querying a document provider or Orleans.

### Why `AssemblyLoadContext` is insufficient

A collectible `AssemblyLoadContext` supports unloading sets of managed
assemblies, but unloading is cooperative. References, callbacks, threads,
timers, and static state can keep an artifact loaded.

It does not prevent:

- process termination;
- filesystem access;
- child process creation;
- network access;
- environment or secret access;
- memory exhaustion; or
- calls into host assemblies.

Use it only as an unloadability mechanism for trusted compiled artifacts.

## Configuration

```csharp
public sealed class TypeScriptRenderingOptions
{
    public int MaxSourceBytes { get; set; } = 100_000;
    public int MaxOutputCharacters { get; set; } = 1_000_000;
    public int MaxDataCalls { get; set; } = 16;
    public int MaxItemsPerCall { get; set; } = 100;
    public int MaxTotalItems { get; set; } = 250;
    public int MaxSerializedDataBytes { get; set; } = 1_048_576;
    public TimeSpan PreviewTimeout { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan PublicRenderTimeout { get; set; } = TimeSpan.FromSeconds(1);
    public bool AllowTrustedInProcessExecution { get; set; }
    public bool RequireIsolationForCmsAuthoredSource { get; set; } = true;
}
```

These are proposed starting values, not accepted production defaults.

The options validator must reject unsafe combinations such as allowing
CMS-authored source while isolation is disabled.

## Administration API

Use minimal APIs:

```text
GET    /api/v1/admin/typescript/templates
POST   /api/v1/admin/typescript/templates
GET    /api/v1/admin/typescript/templates/{id}
PATCH  /api/v1/admin/typescript/templates/{id}
DELETE /api/v1/admin/typescript/templates/{id}

POST   /api/v1/admin/typescript/templates/{id}/validate
POST   /api/v1/admin/typescript/templates/{id}/preview
POST   /api/v1/admin/typescript/templates/{id}/publish
GET    /api/v1/admin/typescript/templates/{id}/versions
GET    /api/v1/admin/typescript/templates/{id}/versions/{version}
```

Required authorization concepts:

```text
typescript.templates.read
typescript.templates.create
typescript.templates.edit
typescript.templates.preview
typescript.templates.publish
typescript.templates.delete
typescript.templates.manage-capabilities
```

Managing capability profiles is more privileged than editing source.

Validation and publication responses use structured diagnostics:

```json
{
  "isValid": false,
  "diagnostics": [
    {
      "code": "SHARPTS001",
      "severity": "error",
      "message": "Type 'string' is not assignable to type 'number'.",
      "line": 15,
      "column": 10
    }
  ]
}
```

Do not expose host stack traces, filesystem paths, connection details, or
internal exception text to template authors.

## Error Semantics

Compilation, publication, and rendering use Aero's `Result<T>` shorthand,
whose error type is `AeroError`.

Expected error categories:

- validation error: invalid source, imports, model, capability, or HTML;
- not found: missing template, version, model, or data item;
- unauthorized/forbidden: missing manager permission or template capability;
- conflict: stale draft version or publication race;
- timeout: execution or data provider deadline;
- resource limit: source, output, call, row, or memory limit;
- unavailable: isolated worker or Orleans dependency unavailable; and
- internal error: unexpected host or SharpTS failure.

Public rendering:

- emits no partial output;
- logs the correlation ID and normalized failure;
- does not expose source or diagnostics to anonymous users; and
- follows the page-level failure policy.

## Observability

Create an OpenTelemetry activity for:

```text
Aero.Cms.TypeScript.Validate
Aero.Cms.TypeScript.Compile
Aero.Cms.TypeScript.Render
Aero.Cms.TypeScript.DataCall
Aero.Cms.TypeScript.Publish
```

Useful tags:

- site ID;
- definition ID;
- version;
- template kind;
- trust mode;
- execution mode;
- provider kind;
- cache hit/miss;
- data call count;
- returned item count;
- source size;
- output size;
- duration;
- timeout or limit type; and
- success/failure category.

Do not record template source, returned content values, secrets, or rendered HTML
as telemetry attributes.

Suggested metrics:

- validation duration;
- compilation duration;
- render duration;
- artifact-cache hit rate;
- output-cache hit rate;
- render failure count by category;
- data calls per render;
- output bytes;
- isolated-worker restarts;
- hard-killed renders; and
- loaded compiled artifact count.

## Testing Strategy

### SharpTS hosting spike

Before feature implementation, build a small test harness that proves:

- parse and type-check;
- interpreted exported-function invocation;
- async exported-function invocation;
- supplied model binding;
- capability method calls;
- stdout/stderr capture;
- exception and diagnostic mapping;
- cancellation of an infinite loop;
- cancellation of pending async work;
- handling of `process.exit`;
- denial or containment of `child_process`;
- denial or containment of filesystem and network access;
- parallel renders with separate interpreters;
- disposal and timer cleanup;
- AOT compilation;
- compiled artifact invocation;
- collectible `AssemblyLoadContext` unloading; and
- behavior across the pinned `SharpTS` and `SharpTS.Sdk` versions.

### Unit tests

Use TUnit for:

- definition validation;
- source-size and import policies;
- entry-point signature validation;
- HTML escaping;
- fragment composition;
- Snowflake ID conversion;
- capability authorization;
- data-call and row limits;
- dependency collection;
- cache-key stability;
- cacheability decisions;
- diagnostics normalization; and
- failure-mode behavior.

### Integration tests

Cover:

- document provider site/culture scoping;
- Orleans provider parity with document provider;
- cross-site access denial;
- public versus draft publication behavior;
- template publish atomicity;
- cache eviction when a template changes;
- cache eviction when queried content changes;
- output HTML validation;
- page route strategy dispatch; and
- PageEditor placeholder expansion.

### Security tests

Attempt:

- arbitrary `dotnet:` imports;
- arbitrary assembly and NuGet references;
- `process.exit`;
- child processes;
- filesystem reads and writes;
- environment-variable access;
- network access;
- reflection;
- DI/service resolution;
- database writes;
- grain command calls;
- infinite loops;
- recursive promises/timers;
- very large source, input, and output;
- query amplification;
- cross-site IDs and slugs;
- HTML scripts and event handlers; and
- host-contract spoofing.

### Browser tests

Use Microsoft Playwright for:

- creating and validating a draft;
- displaying source diagnostics;
- previewing a page;
- publishing a valid version;
- retaining the previous published version after a failed publish;
- selecting a dynamic element in PageEditor;
- binding its model;
- previewing the element;
- saving and reloading the page; and
- public cache invalidation after publication.

## Implementation Phases

### Phase 0: SharpTS compatibility and threat spike

- Add no production module wiring.
- Reference SharpTS from a disposable test harness.
- Verify public APIs, async behavior, cancellation, threading, built-ins, AOT,
  and unloadability.
- Record exact unsupported or unsafe capabilities.
- Decide whether the first production mode is interpreted, compiled, or both.

Exit condition: the host can render a typed model into HTML and the team
understands which operations require process isolation.

### Phase 1: Module foundation and trusted supplied-model rendering

- Create `Aero.Cms.Modules.TypeScript`.
- Add the central `SharpTS` package version and module package reference.
- Add module registration and options validation.
- Implement definition/version persistence.
- Implement validation, diagnostics, artifact cache, and trusted execution.
- Implement `definePage`, `defineElement`, and `html`.
- Accept only supplied immutable models.
- Validate output through the HTML pipeline.

Exit condition: a developer-authored `.page.ts` renders a published page without
database or grain access.

### Phase 2: Data-provider contract

- Add `ITemplateDataContext`.
- Add the supplied-model and document providers.
- Add site/culture/publication enforcement.
- Add limits and dependency collection.
- Add TypeScript declarations.
- Add cache tags and invalidation.

Exit condition: a page can obtain its own published content through a read-only
document provider and invalidate correctly.

### Phase 3: Orleans provider

- Add the Orleans adapter.
- Introduce query-only contracts where existing actors are too broad.
- Verify parity with document-backed results.
- Add deadline, fan-out, serialization, and failure tests.

Exit condition: the same `.page.ts` works with either document or Orleans data
providers without source changes.

### Phase 4: Page routing and PageEditor elements

- Add neutral page-render strategy selection.
- Add PageEditor `<template>` placeholders and sidecar slots.
- Add server preview.
- Add ephemeral tree expansion and containment validation.
- Add publish and browser tests.

Exit condition: a published `.element.ts` can render inside a static PageEditor
page while preserving HTML validation and cache invalidation.

### Phase 5: CMS-authored isolated execution

- Create `Aero.Cms.TypeScript.Worker`.
- Add the render RPC protocol and capability callbacks.
- Apply OS process, identity, filesystem, network, CPU, memory, and time limits.
- Add hard-kill and worker-restart behavior.
- Enable source editing only after isolation tests pass.

Exit condition: an authorized CMS editor can change TypeScript without granting
the script the web process's authority.

### Phase 6: Optional AOT developer template projects

- Evaluate `SharpTS.Sdk`.
- Pin its version separately.
- Add a sample developer template project.
- Validate build, clean, publish, diagnostics, and package output.
- Decide whether AOT artifacts are deployment-time or publication-time assets.

Exit condition: checked-in TypeScript templates compile reproducibly in CI
without NPM.

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| TypeScript terminates or corrupts the web process | Require worker isolation for CMS-authored source; trusted mode only for reviewed code |
| Raw database session enables writes | Expose only `ITemplateDataContext` and query providers |
| Grain reference exposes commands | Use a query-only facade or query-only grain contract |
| Cross-site data leakage | Host forces site and culture; ignore guest-supplied site IDs |
| Cached output becomes stale | Collect dependencies on every data call and tag output |
| Interpreter race or state leakage | One execution scope per render; never share interpreter instances |
| Infinite or blocking work ignores cancellation | Hard worker deadline and process termination |
| Compiled assemblies accumulate | Versioned collectible load contexts for trusted code; unload tests and metrics |
| CLR reflection leaks capabilities | Reject arbitrary imports; generated declarations and narrow host adapters |
| Snowflake ID precision loss | Marshal IDs as validated strings |
| HTML bypasses PageEditor policy | Import, validate, sanitize, and statically render output |
| SharpTS package changes behavior | Pin versions, record artifact versions, run compatibility suite before upgrades |
| Template queries cause N+1 or large scans | Call, row, byte, and duration budgets; batch-oriented APIs |
| User-specific content is publicly cached | Capability use automatically marks response private/no-store |
| SDK/runtime versions diverge | Pin and test `SharpTS` and `SharpTS.Sdk` independently |

## Alternatives Considered

### Continue using Scriban only

Scriban is safer and already integrated, but it deliberately offers less
application logic and TypeScript ergonomics. It remains the preferred engine
for simple CMS-authored templates.

Decision: retain Scriban and add TypeScript as an explicit higher-capability
option.

### Inject `IDocumentSession`

This is simple but exposes storage-specific APIs, mutation, commit behavior,
query-provider internals, and lifetime concerns.

Decision: rejected in favor of a request-scoped read-only data capability.

### Inject Orleans grains directly

Grain references are convenient, but existing interfaces expose command methods
and make cache dependency tracking implicit. They also couple templates to
Orleans types and actor organization.

Decision: rejected in favor of an Orleans-backed data facade. Query-only grain
contracts remain an internal implementation option.

### Pass only preloaded models

This is the most deterministic approach, but it prevents richer pages from
choosing related content or composing multiple datasets.

Decision: supported as one provider, not the only provider.

### Add `SharpTs` to `HtmlNodeKind`

This would make the persisted HTML tree executable and complicate every tree
operation, validator, renderer, importer, outline, and preview.

Decision: rejected. Use native `<template>` placeholders and sidecar metadata.

### Use a custom `.tshtml` or `<template>` single-file format

This offers attractive authoring syntax but requires AeroCMS to create and
maintain a compiler front end, source maps, diagnostics remapping, and editor
tooling.

Decision: rejected. Use standard `.ts` files and the `html` tagged template.

### Load all compiled pages into the web process

This could provide high throughput but makes unloadability, full-trust access,
and process stability difficult.

Decision: permitted only for trusted developer code after compatibility tests.
It is not the isolation strategy for CMS-authored code.

## Open Decisions

These decisions must be resolved before their corresponding phase:

1. Are first-release `.ts` files developer-authored only, or can privileged CMS
   administrators edit them before worker isolation exists?
2. Is interpreted execution sufficient for the first public release?
3. Does a complete TypeScript page share the existing `PageDocument` metadata
   and route, or use a separate route-definition document linked to a page?
4. Which content/page/site read operations belong in the first stable
   `TemplateDataContext`?
5. Which existing actors need query-only interfaces?
6. Where are compiled artifacts stored in distributed deployments?
7. Is optional element failure allowed publicly, and what markup replaces it?
8. Should publication include a required smoke model for templates that cannot
   render without route data?
9. Which host-contract changes are additive, and which require artifact rebuild
   or republish?
10. Is the TypeScript module included in the default meta bundle or shipped as
    an optional package?
11. Which OS isolation mechanisms are required for Windows and Linux hosts?
12. Does the first editor use an existing code editor component, and how are
    SharpTS diagnostics and `.d.ts` files delivered to it?

## Acceptance Criteria

The first complete release is acceptable when:

- SharpTS is referenced only by `Aero.Cms.Modules.TypeScript` and optional
  TypeScript-specific worker/build projects;
- a `.page.ts` template renders validated server-side HTML;
- a `.element.ts` template renders through a PageEditor placeholder;
- templates can use supplied-model, document, or Orleans data providers through
  the same TypeScript contract;
- raw sessions, grain factories, broad grain references, DI, files, processes,
  network, and arbitrary CLR imports are unavailable;
- every data access is site-scoped, bounded, cancellable, and dependency-tracked;
- Snowflake IDs preserve precision;
- drafts and previews are `no-store`;
- publishing is atomic and retains the previous version after failure;
- public output is sanitized and validated through the existing HTML pipeline;
- cache keys include source, engine, host-contract, and dependency versions;
- cache invalidation is proven by integration tests;
- interpreter instances are not shared concurrently;
- CMS-authored code runs outside the web process;
- timeout and resource-limit tests cannot hang or terminate the test host;
- public failures expose no source, stack trace, secret, or internal path; and
- focused builds, TUnit tests, integration tests, and Playwright scenarios pass.

## References

### SharpTS

- [SharpTS website](https://sharpts.dev/)
- [SharpTS GitHub repository](https://github.com/nickna/SharpTS)
- [SharpTS v1.0.8 release](https://github.com/nickna/SharpTS/releases/tag/v1.0.8)
- [SharpTS NuGet package](https://www.nuget.org/packages/SharpTS)
- [SharpTS.Sdk NuGet package](https://www.nuget.org/packages/SharpTS.Sdk)
- [SharpTS README](https://github.com/nickna/SharpTS/blob/main/README.md)
- [SharpTS execution modes](https://github.com/nickna/SharpTS/blob/main/docs/execution-modes.md)
- [SharpTS .NET integration](https://github.com/nickna/SharpTS/blob/main/docs/dotnet-integration.md)
- [SharpTS .NET types and security notes](https://github.com/nickna/SharpTS/blob/main/docs/dotnet-types.md)
- [SharpTS MSBuild SDK guide](https://github.com/nickna/SharpTS/blob/main/docs/msbuild-sdk.md)
- [SharpTS interpreter source](https://github.com/nickna/SharpTS/blob/main/Execution/Interpreter.cs)
- [SharpTS process built-ins](https://github.com/nickna/SharpTS/blob/main/Runtime/BuiltIns/ProcessBuiltIns.cs)
- [SharpTS child-process interpreter](https://github.com/nickna/SharpTS/blob/main/Runtime/BuiltIns/Modules/Interpreter/ChildProcessModuleInterpreter.cs)

### .NET and Orleans

- [.NET assembly unloadability](https://learn.microsoft.com/dotnet/standard/assembly/unloadability)
- [Orleans grain references](https://learn.microsoft.com/dotnet/orleans/grains/grain-references)
- [Orleans clients](https://learn.microsoft.com/dotnet/orleans/host/client)
- [Orleans best practices](https://learn.microsoft.com/dotnet/orleans/resources/best-practices)

### AeroCMS

- [PageEditor Living Standard design](page-editor-html-living-standard.md)
- [Tentative future features](future-feature-list.md)
- [`HtmlNodeKind`](../src/Aero.Cms.Html/HtmlNodeKind.cs)
- [`HtmlStaticRenderer`](../src/Aero.Cms.Html/HtmlStaticRenderer.cs)
- [`HtmlFragmentImporter`](../src/Aero.Cms.Html/HtmlFragmentImporter.cs)
- [`SecureScribanRenderer`](../src/Aero.Cms.Core/Content/Templating/SecureScribanRenderer.cs)
- [`IContentQueryService`](../src/Aero.Cms.Core/Content/Services/IContentQueryService.cs)
- [`IAeroCmsActors`](../src/Aero.Cms.Abstractions/Actors/IAeroCmsActors.cs)
- [`PageCacheTags`](../src/Aero.Cms.Modules.Pages/Caching/PageCacheTags.cs)
- [AeroCMS documentation plan](documentation-plan.md)
