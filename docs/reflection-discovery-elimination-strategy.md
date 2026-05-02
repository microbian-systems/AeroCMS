# Reflection Discovery Elimination Strategy

## Status

Proposed follow-up spec for Aero's final refactor.

This document covers the remaining production paths that still perform runtime reflection-based discovery after the module and Wolverine source-generator work.

The goal is not to ban every use of reflection. The goal is to remove startup/runtime discovery that scans assemblies, scans every type in an assembly, or scans every public method on a provider to discover framework extension points.

## Objective

Replace the remaining reflection-based discovery paths with compile-time generated registries or explicit typed dependencies.

Success looks like this:

- `Aero.Cms.Web` does not need runtime assembly/type scanning to discover modules, blocks, Wolverine handlers, setup identity stores, or social plugs.
- Reflection fallback paths remain available only for explicitly configured legacy/test/tool scenarios.
- Generated registries use marker attributes and `ForAttributeWithMetadataName`, not broad `AllInterfaces` or full-compilation class scanning.
- Runtime invocation reflection remains allowed where it is targeted, explicit, and not doing discovery.

## Current Inventory

### Must Replace

| File | Current Discovery Pattern | Target |
|---|---|---|
| `src/Aero.Cms.Modules.Modules/Services/ModuleDiscoveryService.cs` | `AppDomain`, `DependencyContext`, `Assembly.Load*`, `Assembly.GetTypes()`, `Activator.CreateInstance` | Generated module descriptors in `GeneratedAeroModuleCatalog`, with legacy fallback opt-in only. |
| `src/Aero.Cms.Abstractions/Blocks/Editing/BlockMetadataProvider.cs` | Constructor scans provided assemblies with `assembly.GetTypes()` for `BlockBase` subclasses. | Source-generated block metadata provider backed by `GeneratedBlockModelManifest` / `CmsBlockManifest`. |
| `src/Aero.Cms.Abstractions/Blocks/Editing/BlockEditingService.cs` | Static constructor scans `typeof(BlockBase).Assembly.GetTypes()` and instantiates blocks by `Activator.CreateInstance`. | Generated block catalog/factory with explicit block constructors. |
| `src/Aero.Cms.Modules.Setup/ServerTargetSetupExecutor.cs` | `AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())` to locate a known identity user-store type. | Explicit typed dependency or generated setup bootstrap registry. |
| `Aero/src/Aero.Social/Abstractions/SocialProviderBase.cs` | `GetType().GetMethods()` + `GetCustomAttribute<PlugAttribute>()` / `PostPlugAttribute` for plug discovery. | Generated plug registry keyed by provider type and plug identifier. |

### Do Not Replace In This Refactor

These are reflection uses, but they are not cross-assembly discovery. Keep them unless a later AOT/trimming pass requires deeper changes.

| File | Pattern | Reason To Keep For Now |
|---|---|---|
| `Aero/src/Aero.Social/Plugs/PlugExecutor.cs` | invokes a known plug method and reads `Task<T>.Result`. | Targeted invocation after a plug has already been selected. |
| `Aero/src/Aero.Core/DataStructures/Trees/Persistence/Documents/IndexRebuildService.cs` | dynamic/index operation reflection. | Domain-specific runtime operation, not assembly discovery. |
| `Aero/src/Aero.Core/DataStructures/Trees/Persistence/Linq/Translation/ExpressionEvaluator.cs` | expression/runtime value access. | LINQ expression evaluation. |
| `Aero/src/Aero.Core/DataStructures/Trees/Persistence/Linq/Planning/QueryPlanner.cs` | expression/runtime property planning. | Query planning, not extension-point discovery. |
| `Aero/src/Aero.Social/Providers/FacebookProvider.cs` | targeted `GetType().GetProperty(...)`. | Dynamic provider payload access. |
| `Aero/src/Aero.Social/Providers/LinkedInPageProvider.cs` | targeted `GetType().GetProperty(...)`. | Dynamic provider payload access. |

### Platform Patterns

Do not chase these as part of this spec:

- `AppSettingsExtensions.cs`: `AddUserSecrets(...)`.
- `MauiProgram.cs`: embedded resources.
- `ModuleStateDocument.cs`: unused `System.Reflection.Metadata` import.

## Source-Generation Rules

Use Microsoft Learn/Roslyn-supported generator patterns:

- Use `SyntaxProvider.ForAttributeWithMetadataName(...)` for attributed extension points.
- Use `MetadataReferencesProvider` only when reading assembly-level registration attributes from referenced assemblies.
- Use `AdditionalFiles` only when an explicit flattened manifest file is required.
- Do not scan every class for implemented interfaces through `AllInterfaces`.
- Do not rely on a host generator seeing referenced project source trees.
- Do not execute referenced assembly code from a source generator.

## Target Architecture

```text
Per-project generators
  -> generate project-local registries
  -> emit assembly-level provider attributes where host aggregation is needed

Host generators
  -> read referenced assembly provider attributes
  -> emit host-level aggregate registries

Runtime
  -> consume generated registries
  -> merge state/policy where applicable
  -> validate
  -> fail loudly when generated-required catalogs are missing
```

## Workstream 1: Module Discovery Cleanup

### Current State

The module generator path exists:

- `src/Aero.Cms.SourceGenerators/ModuleManifestGenerator.cs`
- `src/Aero.Cms.SourceGenerators/HostModuleCatalogGenerator.cs`
- `Aero/src/Aero.Modular/ModuleManifestProviderAttribute.cs`
- `src/Aero.Cms.Web/Program.cs` passes `GeneratedAeroModuleCatalog.Descriptors` with `ModuleCatalogMode.GeneratedRequired`.

The legacy scanner still exists and is still registered:

- `src/Aero.Cms.Modules.Modules/Services/ModuleDiscoveryService.cs`
- `ModuleOrchestrationExtensions.AddModuleSystemServices()`
- `ModuleInitializationService`
- `DatabaseBackedModuleLoader`
- `ServerTargetSetupExecutor`

### Target

Keep `ModuleDiscoveryService` only as a legacy/test/tool fallback. The main web host and setup flow must not require it.

### Implementation Tasks

1. Add a generated-descriptor path to module initialization.

   Files:

   - `src/Aero.Cms.Modules.Modules/Services/ModuleInitializationService.cs`
   - `src/Aero.Cms.Modules.Modules/Services/IModuleInitializationService.cs`

   Acceptance:

   - `ModuleInitializationService` can initialize from an explicit `IReadOnlyList<ModuleDescriptor>`.
   - It does not call `IModuleDiscoveryService.DiscoverAsync()` when descriptors are supplied.

2. Split legacy discovery registration from generated-required registration.

   Files:

   - `src/Aero.Cms.Modules.Modules/Services/ModuleOrchestrationExtensions.cs`

   Acceptance:

   - `Aero.Cms.Web` can register module services without registering `ModuleDiscoveryService` as a required main-path service.
   - Legacy fallback remains opt-in for tests/tools.

3. Convert `DatabaseBackedModuleLoader` to consume generated descriptors.

   Files:

   - `src/Aero.Cms.Modules.Modules/Services/DatabaseBackedModuleLoader.cs`

   Acceptance:

   - Database state merges into generated descriptors.
   - Loader does not call reflection discovery in generated-required mode.

4. Keep `ModuleDiscoveryService` behind an explicit legacy API.

   Acceptance:

   - Production `Aero.Cms.Web` startup does not call `AppDomain.CurrentDomain.GetAssemblies()` through module discovery.
   - Tests that intentionally exercise reflection fallback use an explicit legacy mode.

## Workstream 2: Block Metadata And Block Editing

### Current State

The existing `BlockRendererGenerator` already emits useful generated block metadata:

- `GeneratedBlockModelManifest`
- `GeneratedBlockJsonRegistration`
- `CmsBlockManifest`
- `CmsBlockManifestEditorMetadata`

But two services still scan:

- `BlockMetadataProvider` scans caller-provided assemblies.
- `BlockEditingService` has a static constructor that scans `typeof(BlockBase).Assembly`.

### Target

Make block editor metadata and block creation use generated manifests.

### Recommended Shape

Add or adapt generated block APIs:

```csharp
public static partial class GeneratedBlockEditorCatalog
{
    public static IReadOnlyList<BlockTypeInfo> GetAvailableBlockTypes();
    public static bool TryGetBlockTypeInfo(string blockType, out BlockTypeInfo info);
    public static bool TryCreateBlock(string blockType, int order, out BlockBase block);
}
```

This can be emitted by extending `BlockRendererGenerator`, or by adding a dedicated `BlockEditorCatalogGenerator` that consumes `[BlockMetadata]`.

### Implementation Tasks

1. Replace `BlockMetadataProvider` internals.

   Files:

   - `src/Aero.Cms.Abstractions/Blocks/Editing/BlockMetadataProvider.cs`

   Acceptance:

   - Default path reads generated block metadata.
   - Constructor overloads that accept assemblies are removed, deprecated, or clearly marked legacy.
   - No `assembly.GetTypes()` remains in the production path.

2. Replace `BlockEditingService` static scan.

   Files:

   - `src/Aero.Cms.Abstractions/Blocks/Editing/BlockEditingService.cs`

   Acceptance:

   - No static constructor scans block assemblies.
   - `GetAvailableBlockTypes()` delegates to generated metadata.
   - `CreateBlock(...)` delegates to generated constructors or a generated factory.

3. Remove hardcoded block-type switches where generated metadata can replace them.

   Files:

   - `src/Aero.Cms.Abstractions/Blocks/Editing/BlockEditingService.cs`

   Acceptance:

   - Default properties and editor metadata come from generated metadata when possible.
   - Any remaining switch statements are explicitly validation/business logic, not discovery.

4. Add tests for generated block catalog behavior.

   Acceptance:

   - All `[BlockMetadata]` block types appear in the generated catalog.
   - Duplicate block names fail build through existing `AERO006` or an equivalent diagnostic.
   - `BlockEditingService.CreateBlock(...)` creates known block types without `Activator.CreateInstance`.

## Workstream 3: Setup Identity Store Resolution

### Current State

`ServerTargetSetupExecutor.CreateUserStore(...)` scans every loaded assembly and every type to find:

```text
Aero.MartenDB.Identity.UserStore`2
```

from:

```text
Aero.Cms.Modules.Identity
```

This is the most expensive single scan in the list because it walks every type in every loaded assembly to locate one known type.

### Preferred Target

Use a direct typed dependency instead of a generator.

If setup is allowed to reference the identity store assembly directly, replace the scan with:

```csharp
new UserStore<AeroUser, AeroRole>(session, logger)
```

or a small factory:

```csharp
public interface ISetupUserStoreFactory
{
    IUserStore<AeroUser> Create(IDocumentSession session, IServiceProvider services);
}
```

### Generator Alternative

Only use a generator if direct references are architecturally forbidden.

Possible generated shape:

```csharp
public static partial class GeneratedSetupIdentityStoreFactory
{
    public static IUserStore<AeroUser> Create(
        IDocumentSession session,
        ILoggerFactory loggerFactory);
}
```

The generator should be driven by an explicit marker attribute, not by interface scanning.

### Implementation Tasks

1. Decide whether setup may reference the identity module or `Aero.Marten`.

   Recommended v1 decision:

   - Add an explicit project reference if needed.
   - Use a direct generic type construction.
   - Do not introduce a generator for one known closed generic type unless dependency boundaries demand it.

2. Replace the scan in `CreateUserStore(...)`.

   Files:

   - `src/Aero.Cms.Modules.Setup/ServerTargetSetupExecutor.cs`

   Acceptance:

   - No `AppDomain.CurrentDomain.GetAssemblies()` or `Assembly.GetTypes()` remains.
   - Setup seeding still creates `UserManager<AeroUser>` with the same behavior.

3. Add a focused setup test.

   Acceptance:

   - `CreateUserStore` path can be exercised without loading/scanning unrelated assemblies.

## Workstream 4: Social Plug Discovery

### Current State

`SocialProviderBase.DiscoverPlugs()` scans public instance methods on each provider at runtime:

```csharp
GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
method.GetCustomAttribute<PlugAttribute>()
method.GetCustomAttribute<PostPlugAttribute>()
```

Current plug examples are in:

- `Aero/src/Aero.Social/Providers/LinkedInPageProvider.cs`

### Target

Generate plug metadata per provider and avoid method scanning for discovery.

### Recommended Shape

Add generated provider-specific plug catalogs:

```csharp
public static partial class GeneratedSocialPlugCatalog
{
    public static IReadOnlyList<GeneratedPlugDescriptor> GetPlugs(Type providerType);
    public static bool TryGetPlug(Type providerType, string identifier, out GeneratedPlugDescriptor descriptor);
}
```

Descriptor:

```csharp
public sealed record GeneratedPlugDescriptor(
    Type ProviderType,
    string MethodName,
    string Identifier,
    string Title,
    string Description,
    bool IsPostPlug,
    IReadOnlyList<GeneratedPlugFieldDescriptor> Fields);
```

The descriptor may keep `MethodInfo` as an optional lazy runtime lookup if invocation still uses `PlugExecutor`, but discovery metadata must be generated.

### Generator Input

Use `ForAttributeWithMetadataName` for:

- `Aero.Social.Plugs.PlugAttribute`
- `Aero.Social.Plugs.PostPlugAttribute`

Also gather field metadata from method-level `PlugFieldAttribute` attributes.

Do not scan provider methods with `GetMethods()` for discovery.

### Implementation Tasks

1. Add `SocialPlugGenerator`.

   Files:

   - `src/Aero.Cms.SourceGenerators/SocialPlugGenerator.cs` or a new generator project if `Aero.Social` should not depend on CMS generator packaging.

   Acceptance:

   - Emits a generated plug catalog for methods with `[Plug]` and `[PostPlug]`.
   - Emits field descriptors for `[PlugField]`.
   - Reports duplicate plug identifiers per provider.

2. Refactor `SocialProviderBase.DiscoverPlugs()`.

   Files:

   - `Aero/src/Aero.Social/Abstractions/SocialProviderBase.cs`

   Acceptance:

   - Uses generated catalog for discovery.
   - Does not call `GetType().GetMethods(...)` in the production path.
   - Keeps an explicit legacy fallback only if configured for tests/tools.

3. Decide invocation strategy.

   Recommended v1 decision:

   - Keep `PlugExecutor` reflection invocation as targeted runtime invocation.
   - Do not generate invocation delegates in v1.

   Future option:

   - Generate strongly typed delegates per plug to remove invocation reflection too.

4. Add tests.

   Acceptance:

   - LinkedIn plug methods are discovered from generated metadata.
   - Duplicate plug identifiers fail build.
   - `GetPlug(identifier)` returns the same logical metadata as the old reflection path.

## Workstream 5: Analyzer Guardrails

### Target

Add analyzer diagnostics to prevent future reintroduction of reflection scanning in production paths.

### Suggested Diagnostics

| Diagnostic | Rule |
|---|---|
| `AERO010` | Do not call `AppDomain.CurrentDomain.GetAssemblies()` in production code without an explicit legacy/fallback annotation. |
| `AERO011` | Do not call `Assembly.GetTypes()` for discovery in production code. |
| `AERO012` | Do not call `Type.GetMethods()` for extension-point discovery in production code. |
| `AERO013` | Discovery generators must use marker attributes rather than broad interface scanning. |

### Allowlist

Allow reflection in these contexts:

- Source generator implementation code itself, where it is analyzing Roslyn symbols, not runtime assemblies.
- Tests.
- Explicit legacy fallback classes annotated with a new attribute such as `[LegacyReflectionDiscovery]`.
- Targeted runtime invocation paths documented in this spec.

## Implementation Order

1. Finish module path cleanup.
2. Replace setup identity store scan because it is the broadest and simplest offender.
3. Replace block metadata/editor scans using the existing generated block manifest foundation.
4. Add generated social plug discovery.
5. Add analyzer guardrails.
6. Run a final reflection-discovery audit.

## Commands

Use these as checkpoints:

```powershell
dotnet build src\Aero.Cms.Web\Aero.Cms.Web.csproj /p:UseSharedCompilation=false
dotnet build src\Aero.Cms.SourceGenerators\Aero.Cms.SourceGenerators.csproj /p:UseSharedCompilation=false
dotnet test tests\Aero.Cms.Core.Tests\Aero.Cms.Core.Tests.csproj /p:UseSharedCompilation=false
rg -n "AppDomain\.CurrentDomain\.GetAssemblies|Assembly\.LoadFrom|\.GetTypes\(|\.GetMethods\(" src Aero -g "*.cs"
```

The final `rg` command will still return allowed targeted reflection. The acceptance criterion is that no remaining hit performs broad discovery in the production paths listed above.

## Boundaries

Always:

- Preserve generated-required startup for `Aero.Cms.Web`.
- Keep runtime state merge and policy filtering after generated discovery.
- Prefer marker attributes and generated catalogs over interface scanning.
- Keep legacy reflection fallback opt-in and visible in logs.

Ask first:

- Adding new package dependencies.
- Moving `Aero.Social` source generation into a new analyzer package.
- Changing module/package dependency boundaries, especially Setup -> Identity.
- Removing public APIs instead of deprecating them.

Never:

- Reintroduce host-only source generators that expect referenced project source trees.
- Use source generators to execute provider code.
- Use `AllInterfaces` across every type as a discovery strategy.
- Hide generated-catalog failures by silently falling back to reflection in the main host.

## Acceptance Criteria

- `Aero.Cms.Web` starts with generated module and Wolverine catalogs and no reflection discovery fallback.
- Setup seeding does not scan all loaded assemblies to locate the identity user store.
- Block editing metadata and creation do not scan assemblies for `BlockBase` subclasses.
- Social plug discovery does not scan provider methods at runtime.
- Legacy fallback paths are opt-in and covered by tests.
- Analyzer guardrails fail builds for new broad reflection-discovery calls in production code.
- The final audit has no unapproved production uses of:

```text
AppDomain.CurrentDomain.GetAssemblies()
Assembly.GetTypes()
Assembly.LoadFrom()
DependencyContext.RuntimeLibraries for discovery
Type.GetMethods() for extension-point discovery
```

## Open Questions

1. Should Setup directly reference `Aero.Cms.Modules.Identity`, or should identity store creation live behind a generated/bootstrap factory?
2. Should `BlockMetadataProvider` remain public as a compatibility shim, or should it be deprecated in favor of generated block metadata APIs?
3. Should social plug invocation remain reflection-based in v1, or should the plug generator also emit typed invocation delegates?
4. Should analyzer guardrails live in `src/Aero.Cms.SourceGenerators`, or should shared `Aero` submodule rules get their own analyzer package?
5. Should legacy reflection fallback be compiled out for production builds, or runtime-disabled with `ModuleCatalogMode.GeneratedRequired`?

## References

- `module-discovery-strategy.md`
- Microsoft Learn: `SyntaxValueProvider.ForAttributeWithMetadataName`
- Microsoft Learn: `IncrementalGeneratorInitializationContext.MetadataReferencesProvider`
- Microsoft Learn: source generators can read the compilation and additional files
