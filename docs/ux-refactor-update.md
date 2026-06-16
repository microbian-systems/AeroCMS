# UX Refactor Update: DI-Backed Editor Definition Registry

## Decision Summary

Move the CMS page editor from a static, hardwired block registry to a DI-backed
definition registry. This registry becomes the backbone for discovering,
querying, rendering, editing, and composing page-editor blocks, primitives,
containers, custom components, and third-party package blocks.

This is the right architectural direction for a top-tier visual CMS editor,
especially for the .NET ecosystem. It is not sufficient by itself, but it is the
correct foundation: the editor cannot become extensible, testable, package
friendly, or tenant-aware while its definitions are stored in a mutable static
singleton and registered through hardcoded module calls.

## Current Problem

The current registry path has three hardwired barriers.

### 1. Static mutable singleton

`PageEditorBlockRegistry` is currently a public static class with process-wide
mutable state. That makes it difficult to:

- Replace in tests.
- Scope or filter by tenant.
- Override definitions deliberately.
- Reason about registration order.
- Support external packages cleanly.
- Avoid accidental runtime mutation under Blazor Server concurrency.

### 2. Hardcoded registration calls

Module extension methods instantiate providers directly and immediately mutate
the static registry:

```csharp
var provider = new NeoPageEditorBlockProvider();
PageEditorBlockRegistry.RegisterProviders([provider]);
services.AddSingleton<IPageEditorBlockProvider>(provider);
services.AddSingleton<IPageEditorDefinitionProvider>(provider);
```

This means a block package has to know too much about AeroCMS internals. An
external NuGet package should not need to call a static registry. It should only
register its provider with DI.

### 3. Two registration systems

The editor currently mixes:

- Static `PageEditorBlockRegistry` entries.
- DI-provided `IPageEditorBlockProvider` and `IPageEditorDefinitionProvider`
  entries.
- Source-generated `NeoEditorCatalogProvider` entries.
- Re-registration during page-editor component initialization.

This creates correctness risk. API paths, preview paths, background work, and
non-rendered services may not see the same definitions the visual editor sees.

### 4. Source generation cannot be the external package boundary

The source generator can help built-in assemblies, but third-party package
extensibility should not rely on Roslyn seeing compiled package implementation
details. Runtime providers must be the extension boundary for external NuGet
packages.

Source generation is still valuable and should be preserved for things it does
well:

- Built-in assembly metadata.
- `[BlockMetadata]` discovery used by the block model/rendering pipeline.
- Polymorphic JSON serialization support.
- Generated renderer adapters for first-party blocks.
- Boilerplate provider generation for blocks compiled in the current solution.

The rule is narrower: source generation may contribute definitions to the
registry, but it must not become a parallel catalog or the required discovery
path for third-party packages.

### 5. Switch-only blocks are not discoverable

Some currently supported blocks still exist only as switch-case behavior in
editor creation, mapping, preview, and property-editor paths. Those blocks are
not truly discoverable because no provider owns their metadata, default factory,
preview/editor components, composition capabilities, or public mapper.

Phase 0.5 must treat every switch-only block as a migration target. The exact
count should be verified from the current branch before implementation, but the
architecture issue is clear: a block is not extensible until it is represented
by a registered `PageEditorDefinitionDescriptor`.

## Architectural Decision

Introduce `IPageEditorDefinitionRegistry` as the single query abstraction for
page-editor definitions.

Prefer the `IPageEditorDefinitionRegistry` name over `IEditorDefinitionRegistry`
because the existing contracts already use:

- `IPageEditorCatalogDefinition`
- `IPageEditorDefinitionProvider`
- `IPageEditorBlockProvider`
- `PageEditorDefinitionDescriptor`

Keeping the prefix makes the interface family searchable and traceable.

## Proposed Contract

```csharp
namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Read-only lookup service for all page-editor definitions known to the app.
/// Implementations are built from DI-registered providers and are immutable
/// after construction.
/// </summary>
public interface IPageEditorDefinitionRegistry
{
    bool TryGetDescriptor(
        string? catalogId,
        out PageEditorDefinitionDescriptor descriptor);

    IReadOnlyCollection<PageEditorDefinitionDescriptor> AllDescriptors { get; }

    IReadOnlyCollection<IPageEditorBlockDefinition> LegacyDefinitions { get; }
}
```

## Proposed Implementation Shape

```csharp
public sealed class PageEditorDefinitionRegistry : IPageEditorDefinitionRegistry
{
    private readonly IReadOnlyDictionary<string, PageEditorDefinitionDescriptor> _definitions;

    public PageEditorDefinitionRegistry(
        IEnumerable<IPageEditorBlockProvider> blockProviders,
        IEnumerable<IPageEditorDefinitionProvider> nativeProviders)
    {
        _definitions = BuildDefinitions(blockProviders, nativeProviders);
    }

    public bool TryGetDescriptor(
        string? catalogId,
        out PageEditorDefinitionDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            descriptor = default!;
            return false;
        }

        return _definitions.TryGetValue(catalogId, out descriptor!);
    }

    public IReadOnlyCollection<PageEditorDefinitionDescriptor> AllDescriptors =>
        _definitions.Values.ToList();

    public IReadOnlyCollection<IPageEditorBlockDefinition> LegacyDefinitions =>
        _definitions.Values
            .Select(definition => definition.LegacyDefinition)
            .OfType<IPageEditorBlockDefinition>()
            .ToList();

    private static IReadOnlyDictionary<string, PageEditorDefinitionDescriptor> BuildDefinitions(
        IEnumerable<IPageEditorBlockProvider> blockProviders,
        IEnumerable<IPageEditorDefinitionProvider>? nativeProviders)
    {
        var builder = new Dictionary<string, PageEditorDefinitionDescriptor>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var provider in blockProviders)
        {
            foreach (var definition in provider.GetDefinitions())
            {
                var adapter = new LegacyPageEditorDefinitionAdapter(definition);
                var descriptor = adapter.ToDescriptor();
                if (!builder.TryAdd(descriptor.CatalogId, descriptor))
                {
                    ThrowDuplicate(descriptor.CatalogId);
                }
            }
        }

        if (nativeProviders is not null)
        {
            foreach (var provider in nativeProviders)
            {
                foreach (var descriptor in provider.GetEditorDefinitions())
                {
                    if (!builder.TryAdd(descriptor.CatalogId, descriptor))
                    {
                        ThrowDuplicate(descriptor.CatalogId);
                    }
                }
            }
        }

        return builder;
    }

    private static void ThrowDuplicate(string catalogId)
    {
        throw new InvalidOperationException(
            $"Duplicate page-editor catalog ID '{catalogId}'. " +
            "Two providers registered the same ID with no explicit override policy. " +
            "Use an override registration method or remove the conflicting provider.");
    }
}
```

The registry should be immutable after construction. Avoid a public `Populate`
method unless a specific hosting scenario proves it is required. Constructor
population is simpler, safer, and works in more places than an `IHostedService`
only model.

## Registration Model

Built-in modules and third-party packages should only register providers:

```csharp
public static IServiceCollection AddAeroCmsNeoUiBlocks(
    this IServiceCollection services)
{
    services.AddSingleton<NeoPageEditorBlockProvider>();
    services.AddSingleton<IPageEditorBlockProvider>(
        sp => sp.GetRequiredService<NeoPageEditorBlockProvider>());
    services.AddSingleton<IPageEditorDefinitionProvider>(
        sp => sp.GetRequiredService<NeoPageEditorBlockProvider>());

    return services;
}
```

External packages follow the same pattern:

```csharp
public static IServiceCollection AddMyAeroBlocks(
    this IServiceCollection services)
{
    services.AddSingleton<IPageEditorBlockProvider, MyBlockProvider>();
    return services;
}
```

The host application registers the registry once:

```csharp
services.AddSingleton<IPageEditorDefinitionRegistry, PageEditorDefinitionRegistry>();
```

No static registry calls. No provider `new()` calls inside module bootstrap. No
page-editor component re-registration.

## Tenant Visibility

Do not make tenant filtering the registry's first responsibility.

The registry should answer: "What definitions are installed in this app?"

A separate policy should answer: "Which installed definitions may this tenant,
site, role, or plan use?"

Recommended future interface:

```csharp
public interface IPageEditorDefinitionVisibilityPolicy
{
    bool IsVisible(
        PageEditorDefinitionDescriptor definition,
        PageEditorDefinitionVisibilityContext context);
}
```

This keeps the registry stable and app-wide while allowing SaaS-specific
feature gating later.

## Override And Ordering Rules

Do not rely on accidental `GroupBy(...).Last()` behavior.

Define explicit precedence:

1. Host application overrides.
2. Site/tenant package overrides if later supported.
3. First-party AeroCMS packages.
4. Third-party packages.
5. Legacy adapters.

If two definitions register the same `CatalogId` without an explicit override
policy, the registry should fail fast with a clear startup error. Silent
replacement is dangerous in a visual editor.

## Relationship To Existing Contracts

The registry does not replace the existing contract family. It completes it:

```text
IPageEditorCatalogDefinition
  -> describes metadata, editor capability groups, composition rules

INeoNodeFactory
  -> creates default node instances

INeoNodeBlockMapper
  -> maps node definitions to public block models where needed

IPageEditorBlockProvider / IPageEditorDefinitionProvider
  -> package-owned provider contracts

IPageEditorDefinitionRegistry
  -> app-owned read-only lookup service built from providers

CanvasNode / Palette / PropertyEditor / Renderer
  -> registry consumers
```

This follows the Phase 0.5 catalog-consolidation plan in
`docs/ux-refactor.md`.

## GoF And SOLID Fit

- **Dependency Inversion:** editor UI depends on `IPageEditorDefinitionRegistry`,
  not a concrete static registry.
- **Open/Closed:** adding a block package adds a provider; it does not modify
  central editor switch statements.
- **Single Responsibility:** providers produce definitions; the registry stores
  and queries them; policies decide visibility; renderers render; commands
  mutate editor state.
- **Strategy:** providers are interchangeable sources of editor definitions.
- **Factory Method:** each definition/factory creates default nodes or legacy
  editor blocks.
- **Adapter:** `LegacyPageEditorDefinitionAdapter` remains a temporary bridge
  for existing canned blocks.
- **Composite:** `NeoPageNode.Children` remains the composition tree.

## Migration Plan

**Current implementation note (2026-06-16):** Phases 0.5a through the
production-path portion of 0.5d are implemented. The editor/palette/preview
components, preview fragment endpoint, publish/save mapper, and grain-backed
page service now resolve definitions through injected services. The static
registry remains only as a deprecated shim and bridge hook. Legacy alias block
IDs that previously lived only in the publish/save mapper switch are now
registered through `LegacyPageEditorBlockProvider`. `PageEditor.CreateBlock`,
`GetBlockBaseForEditor`, and `MapEditorBlockToNeoNode` are now registry-first;
their remaining switches are compatibility fallbacks for legacy editor UI
parity. Block-rendering test scaffolding now registers the same registry and
action-provider services as the app, and mapper tests cover legacy alias
mapping through the provider.

### Phase 0.5a: Add the registry abstraction

- [x] Add `IPageEditorDefinitionRegistry` to abstractions.
- [x] Add XML comments explaining it is the single definition lookup service.
- [x] Add `PageEditorDefinitionRegistry` in Shared.
- [x] Build the registry immutably from DI-provided block and native providers.

### Phase 0.5b: Add a temporary shim

- [x] Keep `PageEditorBlockRegistry` only as a deprecated compatibility shim.
- [x] New code must inject `IPageEditorDefinitionRegistry`.
- [ ] Remove the shim after all external/static consumers migrate and parity
  tests cover all canned blocks.

### Phase 0.5c: Stop hardcoded provider creation

- [x] Remove direct static registration from Neo package extension methods.
- [x] Remove direct static registration from Hyper package extension methods.
- [x] Register providers through DI only.
- [x] Remove page-editor component re-registration.

### Phase 0.5d: Migrate consumers

Move these paths from static lookups to injected registry lookups:

- [x] Palette construction.
- [x] `CreateBlock`.
- [x] Preview component resolution.
- [x] Property editor resolution.
- [x] `EditorBlockMapper`.
- [x] `BlockEditorPreviewHost`.
- [x] `BlockEditorHost`.
- [x] `EditorBlockPropertyPanel`.
- [x] `PageEditorCanvas`.
- [x] Preview fragment endpoint.
- [x] Page publish/save service.
- [x] Grain-backed page service construction.

### Phase 0.5e: Eliminate switch fallbacks

- [x] Register legacy alias canned blocks used by the publish/save mapper as
  definitions.
- [x] Give those transitional definitions a default editor-block factory path.
- [x] Add focused mapper coverage proving legacy aliases map through the
  registered provider instead of a central mapper switch.
- [ ] Move each legacy block toward `CompositionNodes`.
- [x] Remove the publish/save mapper's legacy alias switch.
- [ ] Remove editor UI, preview, and action switch cases only after that block
  has registry-based tests and editor parity.

### Phase 0.5f: Consolidate generated and runtime catalogs

- Runtime registry is authoritative for editor definitions.
- Source generation may contribute built-in definitions, render adapters, or
  metadata, but it should not be required for third-party package discovery.
- External package blocks must work through provider registration.

Preferred source-generator integration:

1. The source generator emits one or more generated provider classes that
   implement `IPageEditorDefinitionProvider`, `IPageEditorBlockProvider`, or a
   narrower generated-provider contract if needed.
2. Those generated providers are registered in DI like any hand-written
   provider.
3. `PageEditorDefinitionRegistry` consumes generated and hand-written providers
   through the same constructor path.
4. `NeoEditorCatalogProvider` stops being a separate editor lookup source once
   registry parity is reached.

Rejected source-generator integration paths:

- Do not let generated code mutate the registry directly through static calls.
- Do not keep `NeoEditorCatalogProvider` as a second palette/catalog source.
- Do not require source generation for third-party NuGet package definitions.

Safe middle-ground target:

```text
Source generator
  -> Generated IPageEditorDefinitionProvider
    -> DI
      -> IPageEditorDefinitionRegistry
        -> Palette / CanvasNode / PropertyEditor / Renderer
```

This preserves the generator's value for built-in metadata while ensuring there
is one authoritative runtime lookup path.

## Verification Gates

Before calling this architecture complete:

- A test package can add a block by registering only an
  `IPageEditorBlockProvider` or `IPageEditorDefinitionProvider`.
- The palette shows the package block without static calls.
- The package block can be inserted, edited, saved, previewed, published, and
  rendered.
- A duplicate `CatalogId` fails with a clear error unless an explicit override
  policy allows it.
- Page editor tests can substitute `IPageEditorDefinitionRegistry`.
- No page-editor component needs to re-register providers during initialization.
- No module extension method mutates a static registry.
- Static `PageEditorBlockRegistry` is either removed or marked obsolete and
  unused by production paths.

## Is This The Right Way For A Top-Notch Visual CMS?

Yes, this is the right foundation.

For a top-notch CMS visual editor, block discovery must be:

- Package-friendly.
- DI-native.
- Immutable after startup.
- Queryable through one abstraction.
- Decoupled from source generation.
- Decoupled from tenant visibility rules.
- Testable without static state.
- Capable of supporting built-in, custom, and third-party definitions.

This registry decision is necessary, but not the entire editor architecture.
The full "best in class" experience also requires:

- A unified `CanvasNode` interaction path.
- Capability-aware context menus.
- Command plus Memento undo/redo.
- A robust composition policy.
- Strong property editors.
- Responsive and RTL-first style contracts.
- A package-safe renderer contract.
- Browser-level regression coverage.

So the answer is:

This is absolutely the right way to go for the registry and discoverability
backbone. It becomes "100% right" only when paired with the rest of Phase 0.5:
catalog consolidation, explicit override rules, package renderer support,
tenant visibility policy, and removal of switch-based editor paths.
