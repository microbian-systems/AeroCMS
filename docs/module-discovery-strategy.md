
> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). Marten
> was migrated out in [`surrealdb-marten-port.md`](surrealdb-marten-port.md).
> This document is a historical implementation record; its Marten/PostgreSQL
> persistence details do not reflect the current stack.

# Module Discovery Strategy: Per-Project Source Generation + Host Aggregation

## Status

Proposed for Aero's final module-system refactor.

This document replaces the earlier "host generator sees all module source" strategy. That earlier topology is not sound for Roslyn source generators: a generator attached only to `Aero.Cms.Web` sees the host compilation and metadata references, not the source trees of every referenced module project.

The corrected strategy is:

```text
Generate per module project.
Aggregate in the host.
Apply runtime state/tenant policy after aggregation.
Fail loudly when the generated host manifest is missing or empty.
```

This aligns with `AGENTS.md`:

- Avoid reflection-based module discovery.
- Prefer source generators for discovery and generation.
- Keep module-specific logic inside module projects.
- Keep APIs minimal-API oriented.
- Preserve deterministic startup behavior.
- Use the existing module graph and runtime state model rather than replacing it with a purely static model.

---

## 1. Current State

### Module Discovery

`src/Aero.Cms.Modules.Modules/Services/ModuleDiscoveryService.cs` currently discovers modules at runtime:

```text
AppDomain.GetAssemblies()
  -> DependencyContext.RuntimeLibraries
  -> AdditionalScanPaths (*.dll)
    -> Assembly.GetTypes()
      -> IAeroModule assignability checks
        -> Activator.CreateInstance(type)
          -> read Name, Version, Order, Dependencies, Category, Tags, etc.
```

The same basic reflection/scanning risk appears in `src/Aero.AppServer/AeroAppServerExtensions.cs` for Wolverine handler registration:

```text
AppDomain.CurrentDomain.GetAssemblies()
  -> assembly.GetTypes()
  -> typeof(IAeroModule).IsAssignableFrom(type)
  -> opts.Discovery.IncludeAssembly(assembly)
```

### Problems

| Problem | Impact |
|---|---|
| `Assembly.GetTypes()` | Trimming / Native AOT hostile and can fail with partial loads. |
| `Activator.CreateInstance(type)` | Requires parameterless constructors and turns metadata extraction into runtime behavior. |
| `AppDomain.GetAssemblies()` | Depends on what has already been loaded. |
| `DependencyContext` and `AdditionalScanPaths` | Reintroduce runtime plugin-style discovery that Aero is trying to avoid for the main host. |
| Reflection catch blocks | Can hide partial-load and missing-dependency failures until startup. |
| No build-time graph validation | Duplicate module names, missing deps, and cycles fail late. |

---

## 2. Non-Negotiable Correction

### What A Host Generator Cannot Do

A source generator attached only to `Aero.Cms.Web.csproj` cannot inspect all referenced module projects' source files as `SyntaxTree` instances.

It can see:

- The host project's syntax trees.
- Generated syntax added to the host compilation.
- Referenced assemblies as metadata.
- Public symbols in those referenced assemblies.
- Additional files explicitly passed to the compilation.

It cannot see:

- Arbitrary source files inside `ProjectReference` dependencies as if they were part of the host project.
- Attribute syntax inside already-compiled module assemblies unless those projects generated metadata into their assemblies.

Therefore, the generator must run where the module source lives.

### Correct Rule

```text
Each module project runs the generator over its own source.
Each module project emits a small manifest provider into its own assembly.
The host aggregates manifest providers from referenced module assemblies.
```

This is the same boundary we already use conceptually for block generation: source generation works well when the generator is attached to the project containing the annotated source.

---

## 3. Target Architecture

```text
Aero.Modular
  IAeroModule
  AeroModuleBase
  ModuleAttribute
  ModuleDescriptor
  IModuleManifestProvider
  ModuleManifestProviderAttribute

Aero.Cms.SourceGenerators
  ModuleManifestGenerator
    runs in every module project
    reads [Module] on module classes in that project
    emits <AssemblyName>ModuleManifestProvider
    emits [assembly: ModuleManifestProvider(typeof(...))]

  HostModuleManifestGenerator
    runs in Aero.Cms.Web
    reads manifest-provider assembly attributes from referenced assemblies
    emits GeneratedAeroModuleCatalog

Module projects
  [Module(...)]
  public sealed class PagesModule : AeroModuleBase, IUiModule, IConfigureMarten
  {
      ...
  }

Aero.Cms.Web
  references all built-in module projects
  references generator as Analyzer
  consumes GeneratedAeroModuleCatalog.Providers
  fails if generated catalog is missing or empty

Runtime
  aggregate generated descriptors
  merge stored module state
  apply production / tenant policy
  validate graph
  register modules and specialized interfaces
```

---

## 4. New Contracts

### `ModuleAttribute`

File:

```text
Aero/src/Aero.Modular/ModuleAttribute.cs
```

The attribute is compile-time metadata only. It should not replace `IAeroModule`; it gives the generator enough static data to build a manifest without instantiating the module.

```csharp
namespace Aero.Modular;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ModuleAttribute : Attribute
{
    public ModuleAttribute(string name, string? version = null, string? author = null)
    {
        Name = name;
        Version = version;
        Author = author;
    }

    public string Name { get; }
    public string? Version { get; }
    public string? Author { get; }
    public short Order { get; init; }
    public string[]? Dependencies { get; init; }
    public string[]? Category { get; init; }
    public string[]? Tags { get; init; }
    public bool DisabledInProduction { get; init; }
    public string? Description { get; init; }
}
```

Allowed attribute values must be compile-time constants:

| Allowed | Not Allowed |
|---|---|
| `"PagesModule"` | instance method calls |
| `nameof(PagesModule)` | runtime property reads |
| `AeroConstants.Version` if `const` | non-const static properties |
| `["content", "cms"]` | arrays built from runtime values |
| `true`, `false`, numeric constants | `DateTime.Now`, service calls, configuration |

### `IModuleManifestProvider`

File:

```text
Aero/src/Aero.Modular/IModuleManifestProvider.cs
```

```csharp
namespace Aero.Modular;

public interface IModuleManifestProvider
{
    static abstract IReadOnlyList<ModuleDescriptor> Descriptors { get; }
}
```

If static abstract interface members prove awkward for the generator or consumers, use a non-static provider contract instead:

```csharp
public interface IModuleManifestProvider
{
    IReadOnlyList<ModuleDescriptor> GetDescriptors();
}
```

The non-static version is more flexible for tests and reflection-free aggregation if the host generator emits direct `new Provider()` calls.

### `ModuleManifestProviderAttribute`

File:

```text
Aero/src/Aero.Modular/ModuleManifestProviderAttribute.cs
```

```csharp
namespace Aero.Modular;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ModuleManifestProviderAttribute : Attribute
{
    public ModuleManifestProviderAttribute(Type providerType)
    {
        ProviderType = providerType;
    }

    public Type ProviderType { get; }
}
```

The host generator reads this attribute from referenced assembly metadata. Runtime should not scan assemblies for it in the main host path.

Important limit: the host generator can read the provider type from this attribute, but it cannot execute `GetDescriptors()` at build time. Source generators inspect syntax, symbols, metadata, and additional files; they do not execute referenced assembly IL.

Therefore, this attribute is enough for host aggregation, but not enough for cross-project build-time graph validation.

### `ModuleDescriptor`

`ModuleDescriptor` should remain the startup-time DTO for module identity and graph construction, but it needs enough static flags to remove `IsAssignableFrom` checks from the main path.

Recommended additions:

```csharp
public bool IsUiModule { get; init; }
public bool IsApiModule { get; init; }
public bool IsBackgroundModule { get; init; }
public bool IsThemeModule { get; init; }
public bool IsAdminModule { get; init; }
public bool IsFilterModule { get; init; }
public bool IsContentDefinitionModule { get; init; }
public bool IsMartenConfigurator { get; init; }
public bool IsAsyncMartenConfigurator { get; init; }
public string? Description { get; init; }
```

This keeps the registration path reflection-free:

```csharp
if (descriptor.IsMartenConfigurator)
{
    services.TryAddEnumerable(
        ServiceDescriptor.Singleton(typeof(global::Marten.IConfigureMarten), descriptor.ModuleType));
}
```

Important current-code wrinkle: `AeroModuleBase` itself implements `IConfigureMarten`, so the generator will mark all subclasses as Marten configurators unless this is intentionally changed. That matches current reflection behavior but may be broader than desired.

---

## 5. Module Project Generator

### Name

```text
ModuleManifestGenerator
```

### Runs In

Every project that can declare modules:

- `src/Aero.Cms.Modules.*`
- `src/Aero.Cms.Banners`
- `src/Aero.Cms.CookiePolicy`
- any future module package/project
- any external consumer module project that wants compile-time discovery

Do not hardcode `src/Aero.Cms.Modules.*`; the current repo already has module projects outside that naming pattern.

### Input

Use:

```csharp
context.SyntaxProvider.ForAttributeWithMetadataName(
    "Aero.Modular.ModuleAttribute",
    static (node, _) => node is ClassDeclarationSyntax,
    static (ctx, ct) => ...)
```

This is valid because `[Module]` is an attribute and the generator is running in the project that owns the annotated class.

### Validation

Per project:

- `[Module]` target must be public/internal concrete non-abstract non-generic class.
- Target must implement `IAeroModule`.
- Module name must be non-empty.
- Attribute metadata should match runtime overrides where values are compile-time-readable or trivially comparable.
- Emit diagnostics for duplicate module names within the project.

Cross-project:

- Duplicate names across projects.
- Missing dependencies.
- Cycles.

For v1, cross-project validation belongs in the runtime graph validator, not the host source generator. The host generator can discover provider types through assembly-level attributes, but it cannot call provider methods or inspect the generated descriptor objects returned by provider IL.

If build-time cross-project validation becomes mandatory later, use one of these explicit metadata strategies:

- Encode flattened module metadata directly in assembly attributes.
- Emit an `AdditionalFiles` manifest and wire it into the host generator.
- Add a custom MSBuild validation task that runs after compilation.

Do not promise cross-project build-time graph validation from provider-type attributes alone.

### Output

Each module project emits:

```csharp
// <auto-generated />
using Aero.Modular;

[assembly: ModuleManifestProviderAttribute(
    typeof(Aero.Cms.Modules.Pages.Generated.PagesModuleManifestProvider))]

namespace Aero.Cms.Modules.Pages.Generated;

public sealed class PagesModuleManifestProvider : IModuleManifestProvider
{
    public IReadOnlyList<ModuleDescriptor> GetDescriptors() =>
    [
        new ModuleDescriptor
        {
            Name = "PagesModule",
            Version = "0.0.5-alpha",
            Author = "AeroCMS Team",
            ModuleType = typeof(PagesModule),
            AssemblyName = "Aero.Cms.Modules.Pages",
            PhysicalPath = null,
            Order = 0,
            Dependencies = [],
            Category = ["content", "cms"],
            Tags = ["pages", "cms"],
            IsUiModule = true,
            IsMartenConfigurator = true,
            DisabledInProduction = false,
            Disabled = false
        }
    ];
}
```

---

## 6. Host Aggregator Generator

### Name

```text
HostModuleCatalogGenerator
```

### Runs In

`src/Aero.Cms.Web/Aero.Cms.Web.csproj`.

### Input

The host generator reads metadata from referenced assemblies:

- `ModuleManifestProviderAttribute`
- Provider type symbol
- Provider assembly identity

It does not inspect module source trees, and it does not execute `IModuleManifestProvider.GetDescriptors()`.

This means host aggregation can be build-time, but cross-project graph validation is runtime-only unless module metadata is flattened into source-generator-readable inputs.

### Output

```csharp
// <auto-generated />
using Aero.Modular;

namespace Aero.Cms.Web.Generated;

public static partial class GeneratedAeroModuleCatalog
{
    public static IReadOnlyList<IModuleManifestProvider> Providers { get; } =
    [
        new Aero.Cms.Modules.Pages.Generated.PagesModuleManifestProvider(),
        new Aero.Cms.Modules.Blog.Generated.BlogModuleManifestProvider(),
        new Aero.Cms.Banners.Generated.BannersModuleManifestProvider()
    ];

    public static IReadOnlyList<ModuleDescriptor> Descriptors { get; } =
        Providers.SelectMany(provider => provider.GetDescriptors()).ToArray();
}
```

### Failure Mode

The main host must fail loudly if the generated catalog is absent or empty.

Use two distinct semantics:

| Input | Meaning |
|---|---|
| `null` descriptors | Legacy fallback allowed for tests/tools that intentionally do not use generated manifests. |
| Empty generated descriptors | Error in `Aero.Cms.Web`, unless an explicit `AllowEmptyGeneratedModuleCatalog` option is set for a test host. |

Do not silently fall back to reflection when the source-generated main-host catalog is empty. That would mask a broken analyzer reference.

---

## 7. Runtime Flow

The generated catalog is not the final runtime truth. It is the static discovery input.

Runtime must still apply:

1. Stored module state.
2. Production policy.
3. Tenant enablement.
4. Dependency validation.
5. Load-order graph construction.
6. DI registration.
7. Module `Configure`, `ConfigureServices`, and `Run` lifecycle.

Correct order:

```text
GeneratedAeroModuleCatalog.Descriptors
  -> ModuleRuntimeStateMerger.Merge(...)
  -> ModuleRuntimePolicyFilter.Apply(...)
  -> ModuleGraphService.Validate(...)
  -> ModuleGraphService.BuildGraph(...)
  -> Register descriptors into DI
  -> Run module lifecycle
```

This fixes the earlier risk where the compiled descriptor path skipped `MergeWithStoredState()`.

### Suggested API Shape

```csharp
public static async Task<IServiceCollection> AddAeroModulesAsync(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment,
    IReadOnlyList<ModuleDescriptor>? generatedDescriptors = null,
    ModuleCatalogMode catalogMode = ModuleCatalogMode.LegacyFallbackAllowed)
```

```csharp
public enum ModuleCatalogMode
{
    LegacyFallbackAllowed,
    GeneratedRequired
}
```

Behavior:

- `GeneratedRequired` + `null` or empty descriptors -> throw `ModuleSystemStartupException`.
- `LegacyFallbackAllowed` + `null` -> use existing reflection fallback.
- `LegacyFallbackAllowed` + empty descriptors -> return no modules only if explicitly configured for that test/tool.

### State Merge

Move stored-state merge out of `ModuleDiscoveryService` into a reusable runtime service:

```text
IModuleRuntimeStateMerger
  MergeAsync(IReadOnlyList<ModuleDescriptor> discovered, CancellationToken ct)
```

That service can use `IModuleStateStore` without caring whether discovery came from source generation or legacy reflection.

### Tenant Policy

The full compile-time graph can be valid while a tenant-enabled subset is invalid. Runtime must validate tenant-specific enabled modules too:

- Missing enabled dependency -> startup/admin validation error.
- Disabled dependency of enabled module -> validation error or implicit enable, depending on Aero's product decision.
- Tenant disabled module should not be registered into that tenant's runtime surface.

This document does not decide the product behavior. It identifies the required validation seam.

---

## 8. Wolverine Strategy

### Evidence From Wolverine Docs

`wolverine-llms-full.txt` states:

- Handler discovery uses type scanning against an allow-list of assemblies, not the entire dependency tree.
- `opts.Discovery.IncludeAssembly(...)` adds assemblies for handler discovery.
- `opts.Discovery.DisableConventionalDiscovery()` completely disables automatic handler discovery through type scanning.
- `opts.Discovery.DisableConventionalDiscovery().IncludeType<SimpleHandler>()` is a documented explicit handler inclusion pattern.
- `ExtensionDiscovery.ManualOnly` disables automatic extension discovery/assembly scanning.

Relevant sections in `wolverine-llms-full.txt`:

- Message handler discovery: around lines 20427-20537.
- Disabling conventional handler discovery: around lines 20622-20638.
- Explicit inclusion after disabling conventions: around lines 20734-20752.
- Disabling assembly scanning / extension discovery: around lines 5454-5466.

### Important Distinction

Wolverine's runtime execution pipeline is already code-generated and does not use reflection while processing messages. The problem in Aero is startup/configuration discovery:

```text
Aero current startup:
  AppDomain assembly scan
  assembly.GetTypes()
  IncludeAssembly(assembly)

Target startup:
  generated known handler types
  DisableConventionalDiscovery()
  IncludeType<ConcreteHandler>()
```

### Corrected Handler Plan

Use only attribute-based handler discovery in the source generator.

Required marker:

```csharp
using Wolverine.Attributes;

[WolverineHandler]
public sealed class SitemapInvalidationHandler
{
    public Task Handle(InvalidateSitemapCommand command, CancellationToken ct)
    {
        ...
    }
}
```

Generator input:

```csharp
context.SyntaxProvider.ForAttributeWithMetadataName(
    "Wolverine.Attributes.WolverineHandlerAttribute",
    static (node, _) => node is ClassDeclarationSyntax,
    static (ctx, ct) => ...)
```

This follows the Roslyn incremental generator guidance to use marker attributes with `ForAttributeWithMetadataName`. Microsoft Learn documents that `ForAttributeWithMetadataName` transforms only nodes with a matching attribute, and the Roslyn cookbook says this approach is at least 99x more efficient than broad `CreateSyntaxProvider` scanning.

Do not use this wrong pattern:

```csharp
ForAttributeWithMetadataName("Wolverine.IWolverineHandler", ...)
```

`IWolverineHandler` is an interface, not an attribute.

Also do not use this wrong pattern:

```csharp
context.SyntaxProvider.CreateSyntaxProvider(
    predicate: static (node, _) => node is ClassDeclarationSyntax,
    transform: static (ctx, ct) =>
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct);
        return symbol?.AllInterfaces.Any(i => i.Name == "IWolverineHandler") == true
            ? symbol
            : null;
    })
```

The Roslyn cookbook explicitly warns against scanning all types for indirectly implemented interfaces because it forces `AllInterfaces` checks across the compilation on keystrokes or file saves. For Aero, `[WolverineHandler]` must be mandatory, not optional.

Add an analyzer rule to enforce the convention:

```text
AERO002: Wolverine handler missing [WolverineHandler] attribute
```

The analyzer should report an error when a class is clearly intended to be a Wolverine handler but lacks `[WolverineHandler]`. The analyzer can afford a narrower check than the generator because it is enforcing a rule, but it should still avoid broad solution-wide interface crawling where possible. Acceptable triggers include:

- A class directly declaring `IWolverineHandler` in its base list.
- A class with `Handle(...)`, `Consume(...)`, or known Wolverine handler method shapes and a module namespace/package convention.
- A class using known Wolverine handler adapter interfaces in its base list.

If we cannot enforce every possible handler shape cheaply, prefer documentation plus targeted analyzer diagnostics over reintroducing generator-wide interface scanning.

For the first safe implementation, emit explicit `IncludeType<THandler>()` calls, not hand-authored `HandlerChain` construction:

```csharp
namespace Aero.Cms.Modules.SiteMap.Generated;

public static partial class SiteMapWolverineHandlers
{
    public static void Register(WolverineOptions opts)
    {
        opts.Discovery.IncludeType<SitemapInvalidationHandler>();
    }
}
```

Host aggregation then emits:

```csharp
namespace Aero.Cms.Web.Generated;

public static partial class GeneratedWolverineHandlerCatalog
{
    public static void Register(WolverineOptions opts)
    {
        opts.Discovery.DisableConventionalDiscovery();

        Aero.Cms.Modules.Aliases.Generated.AliasesWolverineHandlers.Register(opts);
        Aero.Cms.Modules.SiteMap.Generated.SiteMapWolverineHandlers.Register(opts);
    }
}
```

Then `Aero.AppServer` accepts the callback:

```csharp
public static Task<IHostApplicationBuilder> AddAeroApplicationServer(
    this IHostApplicationBuilder builder,
    Action<WolverineOptions>? configureWolverine = null)
{
    services.AddWolverine(ExtensionDiscovery.ManualOnly, opts =>
    {
        opts.Discovery.DisableConventionalDiscovery();
        configureWolverine?.Invoke(opts);
    });

    return Task.FromResult(builder);
}
```

And `Program.cs` calls:

```csharp
await builder.AddAeroApplicationServer(
    configureWolverine: GeneratedWolverineHandlerCatalog.Register);
```

### Why Not Generate Handler Chains Yet?

Earlier drafts proposed generating:

```csharp
opts.Handlers.Add(chain => ...);
```

That is not the right first step. The local Wolverine 5.32.1 docs clearly support `DisableConventionalDiscovery()` and `IncludeType<T>()`; hand-building handler chains is lower-level, more brittle, and needs a separate spike against Wolverine internals/public API.

Use explicit handler type inclusion first. It removes Aero's assembly scanning while letting Wolverine continue to validate and build handler methods.

### FluentValidation Warning

`wolverine-llms-full.txt` also notes that `UseFluentValidation()` does type scanning to discover validators unless explicit registration behavior is used.

If Aero requires a strict "no startup type scanning" policy for Wolverine, then:

```csharp
opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration);
```

should be used, and validators should be registered through DI/module generated metadata. This is a follow-up decision, but the final refactor should not accidentally reintroduce scanning through validation middleware.

---

## 9. Marten Notes

`marten-llms-full.txt` is useful for adjacent startup policy:

- Marten supports explicit `IncludeType<T>()` allow-list patterns for projections/subscriptions.
- Marten docs mention automatic type discovery/assembly scanning in some features.
- Event subscriptions can use explicit event-type filters for performance and determinism.

This does not replace the Wolverine handler strategy, but it supports the broader Aero direction: prefer explicit generated type lists over runtime assembly scanning.

Potential follow-up:

- Have modules with Marten projections/subscriptions emit generated Marten registration metadata.
- Keep module-owned Marten configuration explicit through `IConfigureMarten` or generated configurators.

---

## 10. Implementation Order

### Step 0: Prove The Generator Boundary

Before the main refactor, add a tiny spike/test:

1. Put `[Module]` on a class in a module project.
2. Attach a generator only to `Aero.Cms.Web`.
3. Prove it does not see that module class as a syntax-tree candidate.
4. Attach the generator to the module project.
5. Prove it emits a module-local provider.
6. Add host aggregation from provider metadata.

This protects the final refactor from the original unsound assumption.

### Step 1: Add Contracts

Files:

- `Aero/src/Aero.Modular/ModuleAttribute.cs`
- `Aero/src/Aero.Modular/IModuleManifestProvider.cs`
- `Aero/src/Aero.Modular/ModuleManifestProviderAttribute.cs`
- `Aero/src/Aero.Modular/ModuleDescriptor.cs`

Also remove the misleading `[Obsolete]` on `ModuleDescriptor`; descriptors are still the correct startup DTO.

### Step 2: Module Manifest Generator

File:

- `src/Aero.Cms.SourceGenerators/ModuleManifestGenerator.cs`

Attach it to module projects.

Recommended long-term packaging:

- Add an MSBuild props/package reference that module projects can use consistently.
- Avoid manually adding analyzer references to 50 projects one by one if possible.

### Step 3: Host Catalog Generator

File:

- `src/Aero.Cms.SourceGenerators/HostModuleCatalogGenerator.cs`

Attach it to:

- `src/Aero.Cms.Web/Aero.Cms.Web.csproj`

The host generator emits:

- `GeneratedAeroModuleCatalog`
- `GeneratedWolverineHandlerCatalog`

The host generator does not perform cross-project graph validation unless module metadata is flattened into attributes/additional files. For v1, emit the catalog and let runtime graph validation produce clear startup diagnostics.

### Step 4: Runtime Merge/Policy

Files:

- `src/Aero.Cms.Modules.Modules/Services/ModuleOrchestrationExtensions.cs`
- new `IModuleRuntimeStateMerger`
- new `ModuleRuntimeStateMerger`
- optional `IModuleRuntimePolicyFilter`

Make generated descriptors flow through the same state and policy layer as reflection descriptors.

### Step 5: Wolverine Callback

File:

- `src/Aero.AppServer/AeroAppServerExtensions.cs`
- `src/Aero.Cms.SourceGenerators/Analyzers/*` for `AERO002`

Replace current `AppDomain` scan and `IncludeAssembly()` loop with:

- `ExtensionDiscovery.ManualOnly`
- `DisableConventionalDiscovery()`
- composition-root callback
- generated `IncludeType<THandler>()` calls

Require `[WolverineHandler]` on every handler that should be discovered by the generator. Add analyzer coverage so interface-only or convention-only handlers do not silently disappear when conventional discovery is disabled.

### Step 6: Migrate Modules

Apply `[Module]` to every module class that should be part of the closed-world host catalog.

Apply `[WolverineHandler]` to every Wolverine handler that should be part of the generated handler catalog.

Inventory should include:

- `src/Aero.Cms.Modules.*`
- `src/Aero.Cms.Banners`
- `src/Aero.Cms.CookiePolicy`
- any other referenced assembly containing an `IAeroModule` implementation

### Step 7: Delete Main-Host Reflection Path

Only after tests prove the generated path works:

- Keep legacy reflection fallback for tests/tools/submodule consumers.
- Do not use fallback in `Aero.Cms.Web`.
- Make fallback opt-in and visible in logs.

---

## 11. Testing Strategy

### Source Generator Tests

| Test | Verifies |
|---|---|
| `ModuleManifest_ProducesProvider_ForModuleClass` | `[Module]` creates a module-local provider. |
| `ModuleManifest_DoesNotRequireInstantiation` | No parameterless constructor is needed for metadata extraction. |
| `ModuleManifest_DetectsMarkerInterfaces` | UI/API/background/theme/admin/filter/content/Marten flags are emitted. |
| `ModuleManifest_ReportsInvalidTarget` | Static/abstract/generic/non-`IAeroModule` targets fail. |
| `HostCatalog_AggregatesReferencedProviders` | Host catalog includes provider types from referenced assemblies. |
| `HostCatalog_DoesNotExecuteProviders` | Host generator only reads provider metadata and does not promise descriptor validation. |
| `WolverineCatalog_UsesAttributeScanning` | Handler generator uses `ForAttributeWithMetadataName("Wolverine.Attributes.WolverineHandlerAttribute", ...)`. |
| `WolverineCatalog_IncludesHandlerTypes` | Generated callback emits explicit handler type registration. |
| `WolverineCatalog_DoesNotScanAllInterfaces` | Generator code has no `AllInterfaces`-based discovery pipeline. |
| `WolverineHandler_MissingAttribute_BuildError` | `AERO002` reports intended handlers missing `[WolverineHandler]`. |
| `WolverineHandler_WithAttribute_NoError` | Attributed handlers are accepted. |

### Runtime Tests

| Test | Verifies |
|---|---|
| `GeneratedRequired_WithNullCatalog_Throws` | Main host cannot silently fall back. |
| `GeneratedRequired_WithEmptyCatalog_Throws` | Broken generator/analyzer reference is caught. |
| `GeneratedCatalog_MergesStoredState` | DB overrides still apply to generated descriptors. |
| `GeneratedCatalog_AppliesProductionPolicy` | `DisabledInProduction` is respected after state merge. |
| `GeneratedCatalog_RegistersSpecializedInterfaces` | Marker interfaces and Marten configurators are registered without `IsAssignableFrom`. |
| `GeneratedCatalog_DuplicateNames_Throws` | Duplicate names across module projects fail startup with clear diagnostics. |
| `GeneratedCatalog_MissingDependency_Throws` | Missing module dependency fails startup with clear diagnostics. |
| `GeneratedCatalog_CircularDependency_Throws` | Cycles fail startup with clear diagnostics. |
| `GeneratedWolverineCatalog_DisablesConventionalDiscovery` | No automatic handler type scanning. |
| `GeneratedWolverineCatalog_IncludesKnownHandlers` | Known handler types are explicitly included. |

### Regression Tests

| Scenario | Expected |
|---|---|
| Test/tool intentionally omits generated catalog | Reflection fallback only when explicitly allowed. |
| Main host generated catalog is empty | Startup/build failure. |
| Tenant enables a module without dependencies | Tenant-specific validation failure or explicit dependency auto-enable decision. |
| New module project added but analyzer missing | Host catalog missing module; test fails before shipping. |

---

## 12. Open Decisions

These should be decided before implementation starts:

1. Cross-project validation strategy.
2. Wolverine handler discovery enforcement.
3. Tenant dependency behavior.
4. `AeroModuleBase` and `IConfigureMarten`.
5. Manifest provider contract shape.
6. Analyzer/generator distribution.
7. Wolverine FluentValidation registration.
8. External module packages.

### Decision 1: Cross-Project Validation Strategy

Recommended v1 decision: runtime validation with clear startup diagnostics.

Why:

- The host generator can read `[assembly: ModuleManifestProviderAttribute(typeof(...))]` from referenced assemblies.
- The host generator cannot execute `provider.GetDescriptors()` at build time.
- The host generator therefore cannot know duplicate names, dependency edges, or cycles unless that data is flattened into source-generator-readable inputs.

Runtime validation must cover:

- Duplicate module names across assemblies.
- Missing dependencies.
- Circular dependencies.
- Tenant-specific invalid enabled subsets.

If build-time validation is required later, choose one of these instead of pretending provider-type metadata is enough:

- Add flattened module metadata to assembly attributes.
- Emit and consume an `AdditionalFiles` manifest.
- Add an MSBuild validation task.

### Decision 2: Wolverine Handler Discovery Enforcement

Required decision: Wolverine handler discovery must be attribute-based only.

Non-negotiable rule:

```text
Every generated-discovery Wolverine handler must have [WolverineHandler].
Do not scan all classes for IWolverineHandler through AllInterfaces.
```

Why:

- Roslyn's cookbook recommends marker attributes plus `ForAttributeWithMetadataName`.
- The cookbook says `ForAttributeWithMetadataName` is at least 99x more efficient than broad `CreateSyntaxProvider` scanning.
- The cookbook explicitly warns that indirect interface scanning forces `AllInterfaces` checks across the compilation and is disastrous for IDE performance.

Implementation requirements:

- Make `[WolverineHandler]` mandatory in examples and module docs.
- Generate handler catalogs only from `Wolverine.Attributes.WolverineHandlerAttribute`.
- Add `AERO002` analyzer coverage for intended Wolverine handlers missing the attribute.
- Keep `IWolverineHandler` useful for Wolverine/runtime semantics if the app wants it, but do not use it as the source-generator discovery mechanism.

### Remaining Decisions

3. Should tenant enablement auto-enable dependencies, or fail with a clear validation message?
4. Should `AeroModuleBase` continue implementing `IConfigureMarten`, causing every module to be registered as an `IConfigureMarten`?
5. Should module manifests use a static abstract provider contract or a simple instance provider?
6. Should we require every module project to reference a shared analyzer props/package?
7. Should Wolverine FluentValidation use explicit validator registration to avoid validator type scanning?
8. Should external module packages be required to ship generated manifest providers?

---

## 13. Final Recommendation

Proceed with source-generated module discovery, but only with the corrected architecture:

```text
Per-module generator -> module assembly manifest provider
Host generator -> aggregate referenced manifest providers
Runtime -> merge state, apply policy, validate graph, register modules
Wolverine -> mandatory [WolverineHandler] + generated IncludeType<THandler>()
```

Do not proceed with a host-only generator that expects to see every module project's source trees. That strategy will miss modules and handlers and could make the final refactor fail in exactly the place we most need determinism.

Do not proceed with source-generator handler discovery based on `IWolverineHandler` interface scanning. That strategy is correct at runtime for Wolverine conventions, but it is the wrong input shape for Roslyn incremental generators. Use marker attributes for generator discovery and analyzers to enforce the convention.

Do not promise cross-project build-time graph validation from provider-type attributes alone. For v1, aggregate providers at build time and validate descriptors at runtime after provider execution and before graph construction.
