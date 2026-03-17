# Aero.Cms Spec: Module System, Discovery, Loading, and Dependency Graph

## Goal

Define the core module system for Aero.Cms so the platform can load features in a deterministic, extensible, and testable way.

A module may contribute:
- services
- endpoints
- middleware
- admin UI
- shapes
- content parts
- field editors
- permissions
- background jobs
- localization resources
- theme assets
- search index handlers
- media processors

## Core Principles

1. Modules must be independently deployable.
2. Modules must declare dependencies explicitly.
3. Modules must load in deterministic order.
4. Modules must be tenant-aware: a tenant can enable only a subset of modules.
5. Modules must be discoverable without hardcoding them into the host.
6. Module loading must be observable and diagnosable.

## Base Interfaces

```csharp
public interface IModule
{
    string Name { get; }
    string Version { get; }
    string Author { get; }
    IReadOnlyList<string> Dependencies { get; }

    void ConfigureServices(IServiceCollection services);
    void Init(IEndpointRouteBuilder endpoints);
    void Configure(IModuleBuilder builder);
}
```

Recommended optional specialization interfaces:

```csharp
public interface IUiModule : IModule { }
public interface IApiModule : IModule { }
public interface IBackgroundModule : IModule { }
public interface IThemeAwareModule : IModule { }
public interface IContentDefinitionModule : IModule { }
```

## Module Descriptor

Do not use raw reflection types everywhere. Normalize discovered modules into descriptors.

```csharp
public sealed class ModuleDescriptor
{
    public string Name { get; init; }
    public string Version { get; init; }
    public string Author { get; init; }
    public Type ModuleType { get; init; }
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    public string AssemblyName { get; init; }
    public string PhysicalPath { get; init; }
    public bool IsUiModule { get; init; }
}
```

## Module Discovery

### Discovery Sources

Support multiple discovery strategies:

1. Assemblies already referenced by the host
2. A modules folder on disk
3. Tenant-specific module enablement configuration
4. Future plugin marketplace installs

### Discovery Pipeline

1. Enumerate assemblies
2. Load candidate assemblies safely
3. Scan for non-abstract, non-generic types implementing `IModule`
4. Build `ModuleDescriptor`
5. Validate uniqueness of module names
6. Validate semantic version format if enforced
7. Persist/load discovery cache if needed

### Discovery Service

```csharp
public interface IModuleDiscoveryService
{
    Task<IReadOnlyList<ModuleDescriptor>> DiscoverAsync(CancellationToken ct = default);
}
```

## Dependency Graph

### Rules

- If `BlogModule` depends on `MediaModule`, then `MediaModule` must load first.
- Missing dependency is a startup error.
- Circular dependency is a startup error.
- A tenant enabling a module implicitly requires all of that module's dependencies.

### Graph Representation

```csharp
public sealed class ModuleGraph
{
    public IReadOnlyDictionary<string, ModuleDescriptor> Modules { get; init; }
    public IReadOnlyList<ModuleDescriptor> LoadOrder { get; init; }
}
```

### Topological Sort

Use Kahn's algorithm or DFS topological sorting.

Pseudo-flow:

1. Build adjacency list from module → dependencies
2. Compute indegrees
3. Enqueue nodes with indegree = 0
4. Pop in stable order
5. Reduce dependent indegrees
6. If count != discovered modules, dependency cycle exists

### Diagnostics

When dependency resolution fails, error must include:
- offending module
- missing dependency or cycle members
- tenant context if tenant-specific

## Module Builder

The `IModuleBuilder` is the composition surface for non-DI registration.

```csharp
public interface IModuleBuilder
{
    void AddPermission(string permission);
    void AddAdminMenuContributor<T>() where T : class, IAdminMenuContributor;
    void AddShapeContributor<T>() where T : class, IShapeContributor;
    void AddDashboardWidget<T>() where T : class, IDashboardWidget;
    void AddContentType(string contentType);
    void AddContentPart<TPart>() where TPart : class, IContentPart;
    void AddFieldEditor<TEditor>() where TEditor : class, IFieldEditor;
    void AddSearchIndexer<TIndexer>() where TIndexer : class, ISearchIndexer;
}
```

Keep this builder metadata-oriented. It should not replace DI.

## Tenant-Aware Module Enablement

Each tenant has:
- enabled modules
- disabled modules
- optional feature flags per module

Tenant configuration example:

```json
{
  "tenant": "site1",
  "enabledModules": [
    "Core",
    "Users",
    "Auth.Jwt",
    "Blog",
    "Media",
    "Search"
  ]
}
```

### Effective Module Set

When a tenant enables a module:
1. add module
2. recursively add dependencies
3. validate discovered set
4. order by dependency graph
5. create tenant shell using only effective set

## Module Load Phases

Recommended phases:

1. Discovery
2. Descriptor validation
3. Dependency resolution
4. ConfigureServices
5. Build tenant service provider
6. Module builder contributions
7. Endpoint/UI initialization
8. Post-start hooks (optional)

Optional future interface:

```csharp
public interface IModuleStartupTask
{
    Task StartAsync(IServiceProvider services, CancellationToken ct);
}
```

## RCL and UI Modules

UI modules should commonly live in Razor Class Libraries.

Pattern:
- `Aero.Cms.Modules.Blog` → API/services/content definitions
- `Aero.Cms.Modules.Blog.UI` → Razor Pages, MVC views, Blazor components, static assets

Module descriptor may link both assemblies under one logical module.

## Failure Modes

Detect and fail fast on:
- duplicate module name
- missing dependency
- circular dependency
- assembly load failure
- module type construction failure
- tenant enabling unknown module

## Testing Requirements

### Unit Tests
- module discovery finds valid modules
- abstract classes ignored
- duplicate names rejected
- dependency order correct
- cycles rejected
- missing dependencies rejected

### Integration Tests
- tenant shell loads only enabled modules
- endpoints appear only when module enabled
- admin UI contributors appear only when module enabled

## Deliverables

1. `IModule` contracts
2. descriptor model
3. discovery service
4. dependency graph service
5. load order resolver
6. tenant-effective module resolution
7. diagnostics and startup logging
8. tests

## Recommended Folder Structure

```text
src/
  Aero.Cms.Core/
    Modules/
      IModule.cs
      ModuleDescriptor.cs
      ModuleGraph.cs
      IModuleDiscoveryService.cs
      IModuleGraphService.cs
      IModuleBuilder.cs
  Aero.Cms.Modules.Blog/
  Aero.Cms.Modules.Blog.UI/
```
