# Module Discovery Strategy: Source-Generated Compile-Time Manifest

## 1. Context

### Current State — Reflection-Based Discovery

Modules (`IAeroModule` implementations) are discovered at **runtime** by scanning assemblies:

```
AppDomain.GetAssemblies()
  → DependencyContext.RuntimeLibraries
  → AdditionalScanPaths (Directory.GetFiles("*.dll"))
    → Assembly.GetTypes()
      → IsAssignableFrom(typeof(IAeroModule))
        → Activator.CreateInstance(type)
          → read property values (Name, Version, Order, Dependencies, etc.)
```

This happens in `ModuleDiscoveryService.cs` and duplicates in `AeroAppServerExtensions.cs` (Wolverine scanning).

### Problems

| Problem | Impact |
|---|---|
| `Assembly.GetTypes()` | Breaks with Native AOT / trimming — types can be stripped |
| `Activator.CreateInstance(type)` | Requires parameterless constructors, throws at runtime |
| `AppDomain.GetAssemblies()` | Unreliable — loaded assemblies depend on what's been touched |
| Reflection catch blocks | Silent failures, `ReflectionTypeLoadException` masking real issues |
| Runtime graph rebuild | Kahn's algorithm + allocations on every startup |
| Startup temp DI container | Extra `ServiceCollection` + `BuildServiceProvider` for discovery only |
| No build-time validation | Missing dependency = startup crash, cycle = startup crash |

### Target State — Compile-Time Manifest

All module discovery AND Wolverine handler registration happen at **compile time** via `IIncrementalGenerator` instances:

```
[Module] attribute on each module class
  → ModuleDiscoveryGenerator (Roslyn IIncrementalGenerator)
    → GeneratedModuleManifest (static partial class, emitted into host assembly)
      → AddAeroModulesAsync consumes the manifest
        → No assembly scanning, no Activator, no temp DI container

[IWolverineHandler] implementors with Handle(T) methods
  → ModuleDiscoveryGenerator (same generator, additional pipeline)
    → WolverineHandlerRegistration (static partial class)
      → ConfigureWolverine callback from Program.cs
        → No AppDomain.GetAssemblies(), no IncludeAssembly()
```

---

## 2. Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│                    Aero.Modular (submodule)                              │
│                                                                          │
│  IAeroModule  │  AeroModuleBase  │  ModuleAttribute  │  ModuleDescriptor │
│  IModuleDiscoveryService         │  IModuleGraphService                  │
│  IWolverineHandler (marker, from Wolverine package)                      │
└──────────────────────────┬───────────────────────────────────────────────┘
                           │ references
                           ▼
┌──────────────────────────────────────────────────────────────────────────┐
│               Aero.Cms.SourceGenerators                                  │
│                                                                          │
│  BlockRendererGenerator (existing)                                       │
│  ModuleDiscoveryGenerator  ◄── NEW — IIncrementalGenerator               │
│      • Pipeline 1: ForAttributeWithMetadataName("ModuleAttribute")       │
│        → Resolve const values, detect marker interfaces, topo sort       │
│        → Emit GeneratedModuleManifest (Descriptors[] + ModuleTypes[])    │
│      • Pipeline 2: ForAttributeWithMetadataName("IWolverineHandler")     │
│        → Scan Handle(T) / HandleAsync(T) methods, extract message types  │
│        → Emit WolverineHandlerRegistration (explicit HandlerGraph add)   │
└──────────────────────────┬───────────────────────────────────────────────┘
                           │ referenced as Analyzer by
                           ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                    Aero.Cms.Web (host)                                   │
│                                                                          │
│  References: ALL ~50 module projects                                     │
│              + Aero.Cms.SourceGenerators (as Analyzer)                   │
│              + Aero.Modular                                              │
│                                                                          │
│  GeneratedModuleManifest (partial class, generated)                      │
│      └─ Descriptors: IReadOnlyList<ModuleDescriptor>                     │
│      └─ ModuleTypes: IReadOnlyList<Type>                                 │
│                                                                          │
│  WolverineHandlerRegistration (partial class, generated)                 │
│      └─ RegisterHandlers(WolverineOptions) — explicit handler chains     │
│                                                                          │
│  Program.cs wires everything at the composition root:                    │
│      await builder.AddAeroApplicationServer(                             │
│          configureWolverine: WolverineHandlerRegistration.Register);     │
│      var (_, log) = await builder.AddAeroCmsRuntimeAsync<Program>(       │
│          compiledDescriptors: GeneratedModuleManifest.Descriptors);      │
└──────────────────────────┬───────────────────────────────────────────────┘
                           │ passes descriptors through
                           ▼
┌──────────────────────────────────────────────────────────────────────────┐
│               Aero.Cms.Modules.Modules                                   │
│                                                                          │
│  ModuleOrchestrationExtensions.AddAeroModulesAsync()                     │
│      └─ If compiledDescriptors != null: use directly;                    │
│         skip temp provider, skip reflection discovery                    │
│      └─ Else: fall back to existing reflection path                      │
│                                                                          │
│  ModuleGraphService.BuildGraph() ← still runs on compile-time data       │
│      └─ Validation (duplicates, cycles, missing deps)                    │
│      └─ Topological sort for load order                                  │
│                                                                          │
│  ModuleDiscoveryService                                                  │
│      └─ Primary path: DiscoverAsync() returns compiledDescriptors        │
│      └─ Fallback path: DiscoverViaReflectionAsync() (unchanged)          │
│      └─ MergeWithStoredState() still works for DB overrides              │
└──────────────────────────────────────────────────────────────────────────┘
```

### Data Flow

```
compile time:
  [Module] attr          ──→  Roslyn pipeline  ──→  GeneratedModuleManifest
  (on each module class)                              ├── Descriptors[]
                                                     └── ModuleTypes[]

  IWolverineHandler     ──→  Roslyn pipeline  ──→  WolverineHandlerRegistration
  (Handle methods)                                    └── Register(WolverineOptions)
                                                          └── Handlers.Add(chain => ...)

runtime:
  Program.cs
    │
    ├── AddAeroApplicationServer(configureWolverine: WolverineHandlerRegistration.Register)
    │     └── Wolverine opts.Discovery.DisableConventionalDiscovery()
    │     └── NO IncludeAssembly() anywhere
    │     └── opts.Handlers.Add() for each compiled handler
    │
    └── AddAeroCmsRuntimeAsync(compiledDescriptors)
          └── AddAeroModulesAsync(compiledDescriptors)
                ├── SKIP: temp ServiceCollection
                ├── SKIP: BuildServiceProvider
                ├── SKIP: ModuleDiscoveryService reflection path
                ├── USE: compiledDescriptors directly
                ├── ModuleGraphService.BuildGraph(compiledDescriptors)
                │     └── validation + topological sort
                └── Register singletons in load order
```

### Architectural Principle: SRP

`Aero.AppServer` configures infrastructure (Orleans, Marten, TickerQ, Wolverine bootstrap) but **does not know about any specific handlers or modules**. Handler registration is the responsibility of the composition root (`Program.cs` via the source-generated `WolverineHandlerRegistration`). The AppServer accepts an optional `Action<WolverineOptions>?` callback for this purpose — clean SRP separation.

---

## 3. `[Module]` Attribute

**File:** `Aero/src/Aero.Modular/ModuleAttribute.cs`

```csharp
namespace Aero.Modular;

/// <summary>
/// Declares a class as an Aero CMS module with compile-time discoverable metadata.
/// Place on any class that implements <see cref="IAeroModule"/> (typically by
/// extending <see cref="AeroModuleBase"/>).
/// </summary>
/// <remarks>
/// The <c>ModuleDiscoveryGenerator</c> source generator reads this attribute to
/// produce the <c>GeneratedModuleManifest</c> — a static list of all modules in
/// the compilation, eliminating runtime reflection-based discovery.
///
/// All property values must be compile-time constants. References to <c>const</c>
/// fields (e.g., <c>AeroConstants.Version</c>) are resolved by Roslyn to their
/// literal values. Collection expressions (<c>["x", "y"]</c>) are supported for
/// array-typed properties.
///
/// This attribute coexists with the <c>IAeroModule</c> property overrides on the
/// module class. The attribute is read by the generator for the manifest; the
/// overrides are the runtime contract used by <c>AeroModuleBase</c> consumers.
/// Both should report the same values.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ModuleAttribute : Attribute
{
    /// <param name="name">Module name — should match <c>nameof(ModuleClass)</c>.</param>
    /// <param name="version">Semantic version string — typically <c>AeroConstants.Version</c>.</param>
    /// <param name="author">Author/organization name — typically <c>AeroConstants.Author</c>.</param>
    public ModuleAttribute(string name, string? version = null, string? author = null)
    {
        Name = name;
        Version = version;
        Author = author;
    }

    /// <summary>Module name (must be unique across all modules).</summary>
    public string Name { get; }

    /// <summary>Semantic version (e.g. "0.0.5-alpha").</summary>
    public string? Version { get; }

    /// <summary>Author or organization.</summary>
    public string? Author { get; }

    /// <summary>Load priority. Lower values load first. Default: 0.</summary>
    public short Order { get; init; }

    /// <summary>
    /// Names of modules this module depends on. Modules listed here are
    /// guaranteed to load before this module. Use <c>nameof()</c> where
    /// possible: <c>Dependencies = [nameof(PagesModule)]</c>.
    /// </summary>
    public string[]? Dependencies { get; init; }

    /// <summary>Categories for grouping (e.g. "Security", "Content").</summary>
    public string[]? Category { get; init; }

    /// <summary>Tags for discovery (e.g. "blog", "cms", "seo").</summary>
    public string[]? Tags { get; init; }

    /// <summary>If true, the module is skipped in production environments.</summary>
    public bool DisabledInProduction { get; init; }
}
```

### Constraint: Values Must Be Compile-Time Constants

The generator extracts values from `AttributeData.ConstructorArguments` and `NamedArguments`. These only contain values that Roslyn can evaluate at compile time:

| Allowed | Not Allowed |
|---|---|
| String literals: `"SetupModule"` | Instance method calls |
| `const` fields: `AeroConstants.Version` | `new Uri(...)` expressions |
| Collection expressions: `["a", "b"]` | Array allocations with runtime values |
| Numeric constants: `-32768` | Property reads from other instances |
| Boolean literals: `true` | `DateTime.Now` or other runtime values |
| `nameof(SomeType)` | String interpolation with non-const values |

> **Note:** `AeroConstants.Version` and `AeroConstants.Author` are `public const string` — they resolve correctly.

---

## 4. `ModuleDiscoveryGenerator`

**File:** `src/Aero.Cms.SourceGenerators/ModuleDiscoveryGenerator.cs`

### Pipeline: Module Manifest

```
Initialize()
  │
  ├── Pipeline 1: ForAttributeWithMetadataName("Aero.Modular.ModuleAttribute")
  │     └── filter: node is ClassDeclarationSyntax
  │     └── transform: extract ModuleCandidate (symbol + attribute data)
  │
  ├── Pipeline 2: ForAttributeWithMetadataName("Wolverine.IWolverineHandler")
  │     └── filter: node is ClassDeclarationSyntax
  │     └── transform: extract HandlerCandidate (symbol, Handle methods, message types)
  │
  └── RegisterSourceOutput(combined → 2 source files)
        │
        ├── File 1: GeneratedModuleManifest
        │     │
        │     ├── For each [Module] candidate:
        │     │     ├── Name from attribute constructor arg
        │     │     ├── Version from attribute constructor arg (or fallback)
        │     │     ├── Author from attribute constructor arg (or fallback)
        │     │     ├── Order from named arg (default: 0)
        │     │     ├── Dependencies from named arg (default: [])
        │     │     ├── Category from named arg (default: [])
        │     │     ├── Tags from named arg (default: [])
        │     │     ├── DisabledInProduction from named arg (default: false)
        │     │     ├── ModuleType = typeof(ConcreteModule)
        │     │     ├── AssemblyName = symbol.ContainingAssembly.Name
        │     │     ├── IsUiModule = symbol.AllInterfaces.Contains(IUiModule)
        │     │     └── (ApiModule, BackgroundModule, etc.)
        │     │
        │     ├── Validate: duplicate names → diagnostic error
        │     ├── Validate: missing dependency names → diagnostic error
        │     ├── Validate: circular deps (DFS) → diagnostic error
        │     │
        │     ├── Topological sort (Kahn's algorithm, alphabetical tie-break)
        │     │
        │     └── Emit:
        │           ├── GeneratedModuleManifest.Descriptors (ordered)
        │           └── GeneratedModuleManifest.ModuleTypes
        │
        └── File 2: WolverineHandlerRegistration
              │
              ├── For each IWolverineHandler candidate:
              │     ├── HandlerType = typeof(ConcreteHandler)
              │     ├── For each public Handle/HandleAsync method:
              │     │     └── Extract message type from first parameter
              │     │     └── (e.g., AeroEvent<PageViewModel>.PageCreated,
              │     │            SlugUpdated, etc.)
              │     └── Map handler → message type pairs
              │
              └── Emit:
                    └── WolverineHandlerRegistration.Register(WolverineOptions)
                          └── opts.Handlers.Add(chain => ...)
                          └── One chain per handler, one Handle per message type
```

### What the Generator Sees

The generator runs as an analyzer attached to `Aero.Cms.Web.csproj`. From that compilation, it sees:

- All ~50 module projects (referenced as `<ProjectReference>` in the host)
- Their source files (as `SyntaxTree` instances)
- The `[Module]` attribute applied to module classes
- `AeroConstants` (from `Aero.Cms.Core`, referenced transitively)
- `IAeroModule`, `IUiModule`, etc. (from `Aero.Modular`)
- All `const` field values via `IFieldSymbol.ConstantValue`

### Marker Interface Detection

The generator checks each module symbol's `AllInterfaces` for the specialized marker interfaces and maps them to `ModuleDescriptor` boolean flags:

| Interface | Descriptor Property |
|---|---|
| `IUiModule` | `IsUiModule = true` |
| `IApiModule` | → used by `RegisterSpecializedInterfaces` |
| `IBackgroundModule` | → used by `RegisterSpecializedInterfaces` |
| `IThemeModule` | → used by `RegisterSpecializedInterfaces` |
| `IAdminModule` | → used by `RegisterSpecializedInterfaces` |
| `IFilterModule` | → used by `RegisterSpecializedInterfaces` |
| `IContentDefinitionModule` | → used by `RegisterSpecializedInterfaces` |

The generator emits the interface checks as booleans on each descriptor so that `RegisterSpecializedInterfaces` can branch without reflection.

### Generated Output

```csharp
// Auto-generated by ModuleDiscoveryGenerator
namespace Aero.Cms.Web.Generated;

public static partial class GeneratedModuleManifest
{
    /// <summary>
    /// All discovered module descriptors, ordered by topological load order.
    /// When no dependency relationship exists, alphabetical order is used
    /// for deterministic ordering.
    /// </summary>
    public static readonly IReadOnlyList<ModuleDescriptor> Descriptors = new List<ModuleDescriptor>
    {
        new()
        {
            Name = "SetupModule",
            Version = "0.0.5-alpha",
            Author = "AeroCMS Team",
            ModuleType = typeof(SetupModule),
            AssemblyName = "Aero.Cms.Modules.Setup",
            PhysicalPath = null,
            Order = -32768,
            Dependencies = Array.Empty<string>(),
            Category = new[] { "setup", "bootstrap" },
            Tags = new[] { "setup", "bootstrap" },
            IsUiModule = false,
            DisabledInProduction = false,
            Disabled = false
        },
        new()
        {
            Name = "PagesModule",
            Version = "0.0.5-alpha",
            Author = "AeroCMS Team",
            ModuleType = typeof(PagesModule),
            AssemblyName = "Aero.Cms.Modules.Pages",
            Order = 0,
            Dependencies = Array.Empty<string>(),
            Category = new[] { "content", "cms" },
            Tags = new[] { "pages", "cms" },
            IsUiModule = true,
        },
        new()
        {
            Name = "BlogModule",
            Version = "0.0.5-alpha",
            Author = "AeroCMS Team",
            ModuleType = typeof(BlogModule),
            AssemblyName = "Aero.Cms.Modules.Blog",
            Order = 0,
            Dependencies = new[] { "PagesModule" },
            Category = new[] { "content", "blog" },
            Tags = new[] { "content", "blog", "cms" },
            IsUiModule = true,
        },
        // ... every module in load order
    };

    /// <summary>
    /// All discovered module <see cref="Type"/> instances, for use cases
    /// that only need the types (e.g. Wolverine assembly registration).
    /// </summary>
    public static readonly IReadOnlyList<Type> ModuleTypes = new List<Type>
    {
        typeof(SetupModule),
        typeof(PagesModule),
        typeof(BlogModule),
        // ...
    };
}
```

### Generated Output: Wolverine Handler Registration

```csharp
// Auto-generated by ModuleDiscoveryGenerator
namespace Aero.Cms.Web.Generated;

public static partial class WolverineHandlerRegistration
{
    public static void Register(WolverineOptions opts)
    {
        // Explicit handler chains — zero reflection, zero assembly scanning

        opts.Handlers.Add(chain =>
        {
            chain.Message<AeroEvent<PageViewModel>.PageCreated>();
            chain.HandleWith<SitemapInvalidationHandler>(h => h.Handle(default!));
        });

        opts.Handlers.Add(chain =>
        {
            chain.Message<AeroEvent<PageViewModel>.PageUpdated>();
            chain.HandleWith<SitemapInvalidationHandler>(h => h.Handle(default!));
        });

        opts.Handlers.Add(chain =>
        {
            chain.Message<AeroEvent<PageViewModel>.PageDeleted>();
            chain.HandleWith<SitemapInvalidationHandler>(h => h.Handle(default!));
        });

        opts.Handlers.Add(chain =>
        {
            chain.Message<AeroEvent<PostViewModel>.PostCreated>();
            chain.HandleWith<SitemapInvalidationHandler>(h => h.Handle(default!));
        });

        opts.Handlers.Add(chain =>
        {
            chain.Message<AeroEvent<PostViewModel>.PostUpdated>();
            chain.HandleWith<SitemapInvalidationHandler>(h => h.Handle(default!));
        });

        opts.Handlers.Add(chain =>
        {
            chain.Message<AeroEvent<PostViewModel>.PostDeleted>();
            chain.HandleWith<SitemapInvalidationHandler>(h => h.Handle(default!));
        });

        opts.Handlers.Add(chain =>
        {
            chain.Message<SlugUpdated>();
            chain.HandleWith<SlugUpdatedHandler>(h => h.Handle(default!));
        });

        // ... all handler-message pairs from the compilation
    }
}
```

Key points:
- Each handler is registered with explicit message type + handler method
- No `IncludeAssembly()` anywhere
- No reflection — the generator extracted `Handle(T)` method signatures at compile time
- New handlers added in any module project are automatically picked up on next build
- Removed handlers cause build errors (missing type reference)

### Diagnostic Errors

The generator emits `Diagnostic` errors when validation fails:

| Diagnostic ID | Condition |
|---|---|
| `AERO010` | Duplicate module `Name` across two classes |
| `AERO011` | Class has `[Module]` but does not implement `IAeroModule` |
| `AERO012` | `Dependencies` references a module name not found in the compilation |
| `AERO013` | Circular dependency detected in the module graph |
| `AERO014` | `[Module]` applied to a static/abstract/generic class |

These are **compile-time errors** — the build fails, preventing deployment of a broken module graph.

---

## 5. Call Chain Changes

### 5a. `ModuleOrchestrationExtensions.AddAeroModulesAsync`

**File:** `src/Aero.Cms.Modules.Modules/Services/ModuleOrchestrationExtensions.cs`

```csharp
public static async Task<IServiceCollection> AddAeroModulesAsync(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment,
    IReadOnlyList<ModuleDescriptor>? compiledDescriptors = null)
{
    IReadOnlyList<ModuleDescriptor> descriptors;

    if (compiledDescriptors is { Count: > 0 })
    {
        // ── Compile-time path ──
        // No reflection, no Activator, no temp DI container.
        // Descriptors are already fully populated by the source generator.
        descriptors = compiledDescriptors;
    }
    else
    {
        // ── Runtime reflection fallback ──
        // Used when the source generator is not referenced
        // (e.g., unit tests, submodule consumers, console apps).
        var discoveryServices = new ServiceCollection();
        discoveryServices.AddSingleton(environment);
        discoveryServices.AddLogging();
        discoveryServices.AddOptions();
        discoveryServices.AddModuleSystemServices();
        discoveryServices.Configure<ModuleDiscoveryOptions>(
            configuration.GetSection("ModuleDiscovery"));

        await using var discoveryProvider = discoveryServices.BuildServiceProvider();
        using var scope = discoveryProvider.CreateScope();
        var discoveryService = scope.ServiceProvider
            .GetRequiredService<IModuleDiscoveryService>();
        descriptors = await discoveryService.DiscoverAsync();
    }

    // ── Shared path (same for both sources) ──

    if (descriptors.Count == 0)
    {
        // Register empty module set
        return services;
    }

    // Validate
    var validation = graphService.Validate(descriptors);
    if (!validation.IsValid)
    {
        var error = validation.Errors.First();
        throw new ModuleSystemStartupException(
            $"Module validation failed: {error.Message} ({error.ErrorType})");
    }

    // Build dependency graph + load order
    var graph = graphService.BuildGraph(descriptors);

    // Register modules as singletons in dependency order
    var moduleBuilder = new AeroModuleBuilder(services, configuration, environment);
    foreach (var descriptor in graph.LoadOrder)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton(typeof(IAeroModule), descriptor.ModuleType));
        services.TryAddSingleton(descriptor.ModuleType);
        RegisterSpecializedInterfaces(services, descriptor);
    }

    // Configure and ConfigureServices
    await using var moduleProvider = services.BuildServiceProvider();
    foreach (var descriptor in graph.LoadOrder)
    {
        var module = (IAeroModule?)moduleProvider.GetService(descriptor.ModuleType);
        if (module != null)
        {
            module.Configure(moduleBuilder);
        }
    }

    foreach (var descriptor in graph.LoadOrder)
    {
        var module = (IAeroModule?)moduleProvider.GetService(descriptor.ModuleType);
        if (module != null)
        {
            module.ConfigureServices(services, configuration, environment);
        }
    }

    services.AddSingleton(graph);
    return services;
}
```

**Changes from current:**
1. New `compiledDescriptors` parameter added
2. When provided, the entire `ServiceCollection` + `BuildServiceProvider` + `IModuleDiscoveryService` block is skipped
3. Validation + graph building still runs (quick — no I/O)
4. Everything downstream (graph building, registration, ConfigureServices) is identical

### 5b. `AeroWebAppExtensions.AddAeroCmsRuntimeAsync`

**File:** `src/Aero.Cms.Web.Core/Eextensions/AeroWebAppExtensions.cs`

```csharp
public static async Task<(WebApplicationBuilder, ReloadableLogger)> AddAeroCmsRuntimeAsync<T>(
    this WebApplicationBuilder builder,
    IReadOnlyList<ModuleDescriptor>? compiledDescriptors = null,
    string[]? args = null)
    where T : class
{
    var config = builder.Configuration;
    var services = builder.Services;
    var env = builder.Environment;

    _ = config.AddConfiguration<T>(env);
    var log = await services.ConfigureLogging(config);

    services.AddBlockSystemServices();
    services.AddScoped<HtmlRenderer>();
    services.AddScoped<CmsBlockHtmlRenderer>();
    services.AddScoped<IBlockSliceRenderer, CmsBlockSliceRenderer>();
    services.AddModuleSystemServices();
    await services.AddAeroModulesAsync(config, env, compiledDescriptors);
    services.AddAeroDataLayer(config, env);

    return (builder, log);
}
```

**Changes:** New `compiledDescriptors` parameter, passed through to `AddAeroModulesAsync`.

### 5c. `AeroAppServerExtensions.AddAeroApplicationServer` — Zero-Reflection Redesign

**File:** `src/Aero.AppServer/AeroAppServerExtensions.cs`

**Principle:** `Aero.AppServer` configures infrastructure only (Orleans, Marten, TickerQ, Wolverine bootstrap). It must **not** know about specific Aero CMS handlers or modules. Handler registration is the composition root's job.

**Rule:** `IncludeAssembly()` is never called — it always triggers type enumeration and method inspection internally.

```csharp
public static Task<IHostApplicationBuilder> AddAeroApplicationServer(
    this IHostApplicationBuilder builder,
    Action<WolverineOptions>? configureWolverine = null)   // ← SRP callback
{
    var services = builder.Services;
    var config = builder.Configuration;

    // ... existing Orleans, Marten, TickerQ setup (unchanged) ...

    // ── Wolverine: zero reflection ──
    services.AddWolverine(ExtensionDiscovery.ManualOnly, opts =>
    {
        // Disable ALL conventional discovery — no naming conventions,
        // no assembly scanning, no type enumeration
        opts.Discovery.DisableConventionalDiscovery();

        // The AppServer does NOT know what handlers exist.
        // The composition root (Program.cs) provides them via the callback.
        // This preserves SRP: AppServer = infrastructure, Program.cs = composition.
        configureWolverine?.Invoke(opts);
    });

    return Task.FromResult(builder);
}
```

**Key design decisions:**

| Decision | Rationale |
|---|---|
| `Action<WolverineOptions>?` instead of `IReadOnlyList<Type>` | The callback can register handlers, configure middleware, set policies — not just list assemblies. Generic enough for any composition root. |
| No `IncludeAssembly()` | Even with `DisableConventionalDiscovery`, `IncludeAssembly` triggers internal type enumeration. The spec forbids it for zero-reflection. |
| No fallback reflection path | If the callback is null, Wolverine runs with zero handlers — any handlers must be registered explicitly. The old `AppDomain.GetAssemblies()` + `GetTypes()` path is deleted entirely. |
| Callback lives in `Aero.Cms.Web` | The source-generated `WolverineHandlerRegistration.Register()` is called from `Program.cs`, keeping `Aero.AppServer` dependency-free. |

### 5d. `Program.cs`

**File:** `src/Aero.Cms.Web/Program.cs`

```csharp
using Aero.Cms.Web.Generated;  // namespace of generated classes

// ── Inside RunMainAppAsync ──

// Pass compile-time module types to Wolverine — no AppDomain scanning,
// no IncludeAssembly(), no reflection. The generated registration
// class contains explicit HandlerGraph.Add() calls for every handler.
await builder.AddAeroApplicationServer(
    configureWolverine: WolverineHandlerRegistration.Register);

// ... middleware setup ...

// Pass compile-time descriptors — no reflection, no Activator,
// no temp ServiceCollection, no BuildServiceProvider
var (_, log) = await builder.AddAeroCmsRuntimeAsync<Program>(
    compiledDescriptors: GeneratedModuleManifest.Descriptors);
```

**Changes:** Two call sites updated. `Program.cs` is the **only** file that references the generated types — all intermediate APIs (`AddAeroModulesAsync`, `AddAeroCmsRuntimeAsync`, `AddAeroApplicationServer`) just forward `IReadOnlyList<ModuleDescriptor>?` and `Action<WolverineOptions>?` without knowing what they contain.

---

## 6. `ModuleDescriptor` Update

**File:** `Aero/src/Aero.Modular/ModuleDescriptor.cs`

Changes:
1. Remove `[Obsolete]` attribute (line 6)
2. Replace the summary with comprehensive XML documentation covering:
   - What it is (discovery-time metadata DTO)
   - How it's created (source generator primary, reflection fallback)
   - Full lifecycle: discovery → merging → validation → graph building → registration → runtime
   - Relationship to `IAeroModule` / `AeroModuleBase`
   - Thread safety (immutable, init-only)

```csharp
namespace Aero.Modular;

/// <summary>
/// Represents the static metadata and runtime identity of a single Aero CMS module.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="ModuleDescriptor"/> is the canonical representation of a module's
/// discovery-time properties. It is distinct from the module's runtime instance
/// (which implements <see cref="IAeroModule"/>) — the descriptor captures only
/// the information needed to register, order, and validate modules before any
/// module instance is created.
/// </para>
///
/// <h3>Creation</h3>
/// <para>
/// Descriptors are created in one of two ways:
/// <list type="bullet">
///   <item>
///     <b>Compile-time (preferred):</b> The <c>ModuleDiscoveryGenerator</c>
///     source generator emits <c>GeneratedModuleManifest</c>, a static partial
///     class containing an <c>IReadOnlyList&lt;ModuleDescriptor&gt;</c> with
///     every <c>[Module]</c>-decorated class in the compilation. All metadata
///     values are extracted from the <c>[Module]</c> attribute's constructor
///     and named arguments at compile time. No reflection or
///     <c>Activator.CreateInstance</c> is involved.
///   </item>
///   <item>
///     <b>Runtime fallback:</b> <c>ModuleDiscoveryService.CreateDescriptor()</c>
///     instantiates each <c>IAeroModule</c> implementation found via
///     <c>Assembly.GetTypes()</c> and reads its property values. This path
///     exists for projects, tests, or environments that do not reference
///     the source generator.
///   </item>
/// </list>
/// </para>
///
/// <h3>Lifecycle</h3>
/// <para>
/// A descriptor passes through several stages:
/// <list type="number">
///   <item>
///     <b>Discovery:</b> Created by the source generator or
///     <c>ModuleDiscoveryService</c>. Populates <see cref="Name"/>,
///     <see cref="Version"/>, <see cref="ModuleType"/>,
///     <see cref="AssemblyName"/>, and marker interfaces.
///   </item>
///   <item>
///     <b>Merging (optional):</b> If an <c>IModuleStateStore</c> is present,
///     <see cref="Order"/>, <see cref="Disabled"/>, <see cref="Category"/>,
///     and <see cref="Tags"/> can be overridden from persisted state.
///     <see cref="Name"/> and <see cref="ModuleType"/> are never overridden
///     as they are the identity key.
///   </item>
///   <item>
///     <b>Validation:</b> <c>ModuleGraphService.Validate()</c> checks for
///     duplicate names, missing dependencies, and circular dependencies.
///   </item>
///   <item>
///     <b>Graph building:</b> <c>ModuleGraphService.BuildGraph()</c>
///     performs a topological sort on <see cref="Dependencies"/> to produce
///     the deterministic module load order.
///   </item>
///   <item>
///     <b>Registration:</b> <c>AddAeroModulesAsync()</c> iterates descriptors
///     in load order, registering each <see cref="ModuleType"/> as a singleton
///     <c>IAeroModule</c> in DI along with specialized interfaces.
///   </item>
///   <item>
///     <b>Runtime:</b> After startup, descriptors are not accessed. The live
///     <c>IAeroModule</c> instances (resolved from DI) are the runtime
///     contract. The descriptor is a startup-time artifact only.
///   </item>
/// </list>
/// </para>
///
/// <h3>Relationship to IAeroModule / AeroModuleBase</h3>
/// <para>
/// <see cref="ModuleDescriptor"/> is a DTO — it has no behavior. It carries
/// the metadata that the module system needs <b>before</b> it can instantiate
/// modules: the load order, dependency graph, DI registration targets, and
/// user-facing discovery fields (<see cref="Category"/>, <see cref="Tags"/>).
/// <see cref="IAeroModule"/> (and <c>AeroModuleBase</c>) define the runtime
/// behavior: configuration, service registration, startup hooks. They are
/// complementary: the descriptor answers "what is this module, when does it
/// load, and what does it depend on?" while the module instance answers
/// "what does it do?"
/// </para>
///
/// <h3>Thread Safety</h3>
/// <para>
/// All properties are <c>init</c>-only, making the type immutable after
/// construction. Instances are safe to cache and share across threads. The
/// source generator emits descriptors as a static
/// <c>IReadOnlyList&lt;ModuleDescriptor&gt;</c> populated once at module
/// initialization and never modified.
/// </para>
/// </remarks>
public sealed class ModuleDescriptor
{
    /// <summary>
    /// Unique module name. Must not conflict with any other module's name.
    /// Used as the identity key for dependency resolution
    /// (<see cref="Dependencies"/> references modules by name) and for
    /// state store lookups.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Semantic version string (e.g. "0.0.5-alpha").</summary>
    public required string Version { get; init; }

    /// <summary>Author or organization that created the module.</summary>
    public required string Author { get; init; }

    /// <summary>The <see cref="Type"/> that implements <see cref="IAeroModule"/>.
    /// Used as the DI registration target and for Wolverine assembly discovery.</summary>
    public required Type ModuleType { get; init; }

    /// <summary>
    /// Names of modules this module depends on. The dependency graph ensures
    /// that all modules listed here are loaded and configured before this
    /// module. Resolved at compile time from the <c>[Module]</c> attribute.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The assembly name of the module's containing assembly
    /// (e.g. "Aero.Cms.Modules.Setup"). Used for diagnostics and logging.
    /// </summary>
    public required string AssemblyName { get; init; }

    /// <summary>
    /// Physical file path of the module assembly, if known. Populated only
    /// by the runtime reflection fallback path. Null when the descriptor
    /// is source-generated (all compile-time assemblies are loaded by the
    /// runtime without explicit file path tracking).
    /// </summary>
    public string? PhysicalPath { get; init; }

    /// <summary>
    /// True if this module implements <see cref="IUiModule"/> (contributes
    /// UI components to the Aero CMS editor). The source generator detects
    /// this via the symbol's interface list at compile time.
    /// </summary>
    public bool IsUiModule { get; init; }

    /// <summary>
    /// Load order priority. Lower values load first within the same
    /// dependency tier. Does not override dependency ordering — if module A
    /// depends on module B, B always loads first regardless of Order values.
    /// Default: 0.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Categories for grouping and filtering in the admin UI
    /// (e.g. "Security", "Content", "Infrastructure", "Setup").
    /// </summary>
    public IReadOnlyList<string> Category { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Tags for user-facing module discovery (e.g. "blog", "cms", "seo").
    /// Users can search/filter modules by these tags in the admin panel.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// When true, the module is automatically disabled in production
    /// environments, regardless of the <see cref="Disabled"/> setting.
    /// Used for development-only or setup-only modules.
    /// </summary>
    public bool DisabledInProduction { get; init; }

    /// <summary>
    /// When true, the module is disabled by the user and will not be loaded.
    /// Determined at runtime by merging with the <c>IModuleStateStore</c>.
    /// The source generator always emits <c>false</c>; the value may be
    /// overridden from the database during startup.
    /// </summary>
    public bool Disabled { get; init; }
}
```

---

## 7. Module Migration: Adding `[Module]` to All Modules

Every `*Module.cs` file gets the `[Module]` attribute. The property **values** in the attribute match what the existing property overrides return.

### Pattern

```csharp
[Module(nameof(BlogModule), AeroConstants.Version, AeroConstants.Author,
    Dependencies = [nameof(PagesModule)],
    Category = ["content", "blog"],
    Tags = ["content", "blog", "cms"])]
public sealed class BlogModule : AeroModuleBase, IUiModule
{
    public override string Name => nameof(BlogModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [nameof(Pages.PagesModule)];
    public override IReadOnlyList<string> Category => ["content", "blog"];
    public override IReadOnlyList<string> Tags => ["content", "blog", "cms"];
    // ...
}
```

### Mapping Rules

| `IAeroModule` property | `[Module]` attribute mapping |
|---|---|
| `Name` | Constructor arg 1: `nameof(ModuleClass)` |
| `Version` | Constructor arg 2: `AeroConstants.Version` |
| `Author` | Constructor arg 3: `AeroConstants.Author` |
| `Order` | Named arg `Order` — explicit `short` value |
| `Dependencies` | Named arg `Dependencies` — string array of module names |
| `Category` | Named arg `Category` — string array |
| `Tags` | Named arg `Tags` — string array |
| `DisabledInProduction` | Named arg `DisabledInProduction` — bool |
| `IUiModule` / `IApiModule` etc. | Detected automatically from class interface list |

### Modules to Migrate (~54)

All files under `src/Aero.Cms.Modules.*/` and any project in the solution that contains a class implementing `IAeroModule`.

---

## 8. Eliminated Code Paths

### Removed from Runtime (when compiledDescriptors is provided + handler registration callback)

| File | Lines | What |
|---|---|---|
| `ModuleDiscoveryService.cs` | 210–281 | `GetAssembliesToScan()` — `AppDomain.GetAssemblies()`, `DependencyContext`, `Directory.GetFiles()` |
| `ModuleDiscoveryService.cs` | 329–349 | `ScanAssemblyForModules()` — `Assembly.GetTypes()` + `IsValidModuleType()` |
| `ModuleDiscoveryService.cs` | 372–409 | `CreateDescriptor()` — `Activator.CreateInstance(type)` + property reads |
| `ModuleDiscoveryService.cs` | 351–369 | `IsDisabledInProduction()` — static property reflection |
| `AeroAppServerExtensions.cs` | 76–106 | Entire Wolverine assembly scanning block — `AppDomain.GetAssemblies()` + `GetTypes().Any(IsAssignableFrom)` + `IncludeAssembly()` |
| `AeroAppServerExtensions.cs` | 109 | `opts.Discovery.IncludeAssembly(Assembly.GetEntryAssembly()!)` |
| `ModuleOrchestrationExtensions.cs` | 58–74 | Temp `ServiceCollection` + `BuildServiceProvider` + `IModuleDiscoveryService` injection |
| `ModuleOrchestrationExtensions.cs` | 146–193 | `RegisterSpecializedInterfaces()` — `IsAssignableFrom` checks (replaced by descriptor booleans) |

### Kept as Fallback (for tests, console apps, submodule consumers)

| File | What | Why kept |
|---|---|---|
| `ModuleDiscoveryService.cs` | Full `DiscoverViaReflectionAsync()` path | Tests that don't use the generator |
| `ModuleOrchestrationExtensions.cs` | Temp provider + reflection path | Same — backward compatibility |

The fallback reflection path is **dead code** in the main `Aero.Cms.Web` host, but preserved for compatibility. For Wolverine, no fallback exists — handler registration is always explicit (either via source generator or manual `Handlers.Add()`). If the `configureWolverine` callback is null, Wolverine runs with zero handlers.

---

## 9. Implementation Order

### Step 1: `ModuleDescriptor` Documentation + `[Module]` Attribute

**Files to touch:**
- `Aero/src/Aero.Modular/ModuleDescriptor.cs` — remove `[Obsolete]`, add extensive docs
- `Aero/src/Aero.Modular/ModuleAttribute.cs` — **new file**

**Verification:** `dotnet build` on `Aero.Modular` succeeds.

### Step 2: `ModuleDiscoveryGenerator`

**Files to touch:**
- `src/Aero.Cms.SourceGenerators/ModuleDiscoveryGenerator.cs` — **new file**

**Two pipelines in one generator:**

1. **Module manifest pipeline** — `ForAttributeWithMetadataName("Aero.Modular.ModuleAttribute")` → emits `GeneratedModuleManifest` (Descriptors, ModuleTypes, pre-computed load order)
2. **Handler registration pipeline** — `ForAttributeWithMetadataName("Wolverine.IWolverineHandler")` → scans `Handle(T)` / `HandleAsync(T)` methods on each implementor, extracts message types → emits `WolverineHandlerRegistration` (explicit `Handlers.Add(chain => ...)` calls)

**Verification:** Generator compiles against `netstandard2.0`. Unit tests:
- Reference from a test project with a mock `[Module]`-decorated class → verify `GeneratedModuleManifest`
- Reference from a test project with a mock `IWolverineHandler` class → verify `WolverineHandlerRegistration`

### Step 3: Wire Generator to `Aero.Cms.Web`

**Files to touch:**
- `src/Aero.Cms.Web/Aero.Cms.Web.csproj` — add `<ProjectReference>` as Analyzer

**Verification:** Build `Aero.Cms.Web`. Verify `GeneratedModuleManifest` appears in the compilation output (look in `obj/Debug/net10.0/generated/`).

### Step 4: Update Call Chain

**Files to touch:**
- `src/Aero.Cms.Modules.Modules/Services/ModuleOrchestrationExtensions.cs` — add `compiledDescriptors` parameter, branch on null
- `src/Aero.Cms.Web.Core/Eextensions/AeroWebAppExtensions.cs` — pipe `compiledDescriptors` through
- `src/Aero.AppServer/AeroAppServerExtensions.cs` — **replace** `compiledModuleTypes` + `IncludeAssembly()` block with `Action<WolverineOptions>? configureWolverine` callback. No reflection fallback path — if callback is null, Wolverine runs with zero handlers.
- `src/Aero.Cms.Web/Program.cs` — pass both `GeneratedModuleManifest.Descriptors` and `WolverineHandlerRegistration.Register`

**Verification:** Application starts without reflection scanning. Module registration order matches pre-change behavior. Wolverine registers exact handler chains via the generated `WolverineHandlerRegistration` — no `IncludeAssembly()` calls anywhere.

### Step 5: Migrate All Modules

**Files to touch:** ~50 `*Module.cs` files across `src/Aero.Cms.Modules.*/`

**Verification:** Full `dotnet build` succeeds. All modules appear in `GeneratedModuleManifest` with correct metadata.

### Step 6: Clean Up

- Remove dead code paths (optional — fallback path preserved for compatibility)
- Update unit tests that mock `IModuleDiscoveryService` if needed

---

## 10. Testing Strategy

### Unit Tests (in `Aero.Cms.SourceGenerators` tests)

| Test | What it verifies |
|---|---|
| `ModuleAttribute_ProducesDescriptor` | A class with `[Module]` generates a matching `ModuleDescriptor` |
| `ModuleAttribute_ResolvesConstReferences` | `AeroConstants.Version` resolves to its literal value |
| `ModuleAttribute_DetectsMarkerInterfaces` | `IUiModule` on class sets `IsUiModule = true` |
| `ModuleAttribute_DuplicateName_Error` | Two classes with same module name produce `AERO010` |
| `ModuleAttribute_MissingDependency_Error` | `Dependencies` referencing unknown module produces `AERO012` |
| `ModuleAttribute_CircularDependency_Error` | A → B → A produces `AERO013` |
| `ModuleAttribute_NotAeroModule_Error` | Class with `[Module]` not implementing `IAeroModule` produces `AERO011` |
| `GeneratedManifest_ModuleTypes` | `ModuleTypes` list contains `typeof()` for each module |

### Integration Tests

| Test | What it verifies |
|---|---|
| `AddAeroModulesAsync_WithCompiledDescriptors` | Modules registered correctly, in load order, with specialized interfaces |
| `AddAeroApplicationServer_WithHandlerCallback` | Wolverine registers exact handler chains via the callback; no `IncludeAssembly()` used |
| `WolverineHandlerRegistration_Register_AllHandlers` | Every `IWolverineHandler` in the compilation produces a `Handlers.Add()` entry with correct message type mapping |
| `FullStartup_NoReflectionCall` | No `Assembly.GetTypes()`, `Activator.CreateInstance()`, `AppDomain.GetAssemblies()`, or `IncludeAssembly()` is called during startup |

### Regression Tests

| Scenario | Expected behavior |
|---|---|
| Start without generator (no compiledDescriptors) | Falls back to reflection — same as before |
| Start with empty compiledDescriptors | Falls back to reflection |
| Mix of generator + AdditionalScanPaths | Generator handles known modules, reflection handles external DLLs |

---

## 11. Appendix: Summary of All Files Changed

### New Files

| File | Purpose |
|---|---|
| `Aero/src/Aero.Modular/ModuleAttribute.cs` | `[Module]` attribute definition |
| `src/Aero.Cms.SourceGenerators/ModuleDiscoveryGenerator.cs` | `IIncrementalGenerator` implementation |

### Modified Files

| File | Change |
|---|---|
| `Aero/src/Aero.Modular/ModuleDescriptor.cs` | Remove `[Obsolete]`, add comprehensive docs |
| `src/Aero.Cms.Web/Aero.Cms.Web.csproj` | Add analyzer reference to generator |
| `src/Aero.Cms.Modules.Modules/Services/ModuleOrchestrationExtensions.cs` | Add `compiledDescriptors` parameter, branch on null |
| `src/Aero.Cms.Web.Core/Eextensions/AeroWebAppExtensions.cs` | Pipe `compiledDescriptors` to `AddAeroModulesAsync` |
| `src/Aero.AppServer/AeroAppServerExtensions.cs` | **Replace** `compiledModuleTypes` + `IncludeAssembly()` with `Action<WolverineOptions>? configureWolverine` callback |
| `src/Aero.Cms.Web/Program.cs` | Pass `GeneratedModuleManifest.Descriptors` + `WolverineHandlerRegistration.Register` |
| `src/Aero.Cms.Modules.*/.../...Module.cs` (~50 files) | Add `[Module]` attribute to each module class |

### Generated Output (in `Aero.Cms.Web` assembly)

| File | Content |
|---|---|
| `GeneratedModuleManifest.g.cs` | `GeneratedModuleManifest` — static `Descriptors` + `ModuleTypes` from `[Module]` attributes |
| `WolverineHandlerRegistration.g.cs` | `WolverineHandlerRegistration.Register(WolverineOptions)` — explicit handler chains from `IWolverineHandler` implementors |
