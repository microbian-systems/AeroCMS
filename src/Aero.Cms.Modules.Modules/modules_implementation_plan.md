# Implementation Plan: Extract Concretions from Aero.Modular to Aero.Cms.Modules.Modules

## Objectives

1. **`Aero.Modular`** → Pure abstractions (interfaces, models used in interface contracts, exceptions, abstract base classes). Referenced by all applications including non-Aero.CMS ones.
2. **`Aero.Cms.Modules.Modules`** → All concrete implementations of module system interfaces. Encapsulated, SRP-aligned.
3. **Split `CmsModuleExtensions.cs`** → Block registration stays in `Aero.Cms.Core`, module orchestration moves to `Aero.Cms.Modules.Modules`.
4. **Dependency chain**: `Aero.Cms.Web → Aero.Cms.Modules.Setup → Aero.Cms.Modules.Modules`

## Key Decision: ModuleDocument

**Status: UNRESOLVED — needs your input.**

`ModuleDocument` (renamed from `ModuleStateDocument`) is referenced by `IModuleStateStore` and `IModuleStateLoader` as a return/parameter type. Both interfaces live in `Aero.Modular`. If `ModuleDocument` moves to `Aero.Cms.Modules.Modules`, it creates a circular dependency:

```
Aero.Cms.Modules.Modules → Aero.Modular (for interfaces)
Aero.Modular → Aero.Cms.Modules.Modules (for ModuleDocument type in interface signatures)  ← CIRCULAR
```

**Options:**

| Option | What | Impact |
|--------|------|--------|
| A | Keep `ModuleDocument` in `Aero.Modular` as a "contract model" | Simple. ModuleDocument is just a POCO — no Marten logic, no real behavior. Same as `ModuleGraph`, `ModuleValidationResult`, etc. |
| B | Keep `ModuleDocument` in `Aero.Modular` — it IS a ViewModel | Follows the same pattern as `ModuleGraph`. Interfaces reference it, concretions implement/use it. The `ModuleStateStore` is the real entity/persistence concern. |
| C | Extract `IModuleDocument` interface (you said no, listing for completeness) | Interface in Aero.Modular, concrete in Aero.Cms.Modules.Modules |
| D | Move `IModuleStateStore` / `IModuleStateLoader` to Aero.Cms.Modules.Modules | Then ModuleDocument can move too. But interfaces would no longer be in Aero.Modular for external consumers. |

**Recommendation: Option B** — ModuleDocument stays in Aero.Modular as a contract model/ViewModel, exactly like `ModuleGraph`, `ModuleDescriptor`, etc. The concrete `ModuleStateStore` (which does actual Marten work) moves to Aero.Cms.Modules.Modules, properly encapsulating the persistence concern.

---

## What Stays in Aero.Modular

After the refactoring, Aero.Modular will contain ONLY:

| File | Type | Reason |
|------|------|--------|
| `IAeroModule.cs` | Interface | Pure abstraction |
| `IUiModule`, `IApiModule`, etc. | Interface | Pure abstraction |
| `IModuleBuilder.cs` | Interface (`IAeroModuleBuilder` + marker interfaces) | Pure abstraction |
| `IModuleDiscoveryService.cs` | Interface | Pure abstraction |
| `IModuleGraphService.cs` | Interface | Pure abstraction |
| `IModuleStateLoader.cs` | Interface | Pure abstraction |
| `IModuleStateStore.cs` | Interface | Pure abstraction |
| `AeroModuleBase.cs` | Abstract class (exception) | Used externally, abstract |
| `ModuleGraph.cs` | Contract model | Used by `IModuleGraphService` interface |
| `ModuleValidationResult.cs` (in IModuleGraphService.cs) | Contract model | Return type of `IModuleGraphService.Validate()` |
| `ModuleValidationError.cs` (in IModuleGraphService.cs) | Contract model | Nested in validation result |
| `ModuleDependencyException` (in IModuleGraphService.cs) | Exception | Thrown by interface contract |
| `ModuleSystemExceptions.cs` | Exceptions | All 6 exception types, used by external apps |
| `ModuleExtensions.cs` | Exception (`ModuleSystemStartupException`) | Thrown by CmsModuleExtensions, used externally |
| `ModuleDescriptor.cs` | Contract model | Used by interfaces and external apps |
| `ModuleDiscoveryOptions.cs` | Options POCO | Configuration model |
| `ModuleGraphOptions.cs` | Options POCO | Configuration model |
| `ModuleDocument.cs` | Contract model/ViewModel | Used by `IModuleStateStore`, `IModuleStateLoader` interfaces |

Aero.Modular.csproj still needs:
- `Marten` package (for `IConfigureMarten` on `AeroModuleBase`)
- `Aero.Cms.Abstractions` project reference
- `Aero.Core` project reference

---

## What Moves to Aero.Cms.Modules.Modules

### 1. Concrete Service Implementations

| File | Type | Reason |
|------|------|--------|
| `ModuleDiscoveryService.cs` | Concrete impl of `IModuleDiscoveryService` | Uses reflection, assembly scanning, DI |
| `ModuleGraphService.cs` | Concrete impl of `IModuleGraphService` | Topological sort, validation logic |
| `ModuleStateStore.cs` | Concrete impl of `IModuleStateStore` | Marten-backed persistence |
| `DatabaseBackedModuleLoader.cs` | Concrete impl of `IModuleStateLoader` | Merges reflection + DB state |
| `AeroModuleBuilder` class (in `IModuleBuilder.cs`) | Concrete impl of `IAeroModuleBuilder` | Assembly scanning metadata collector |
| `ModuleServiceExtensions.cs` | DI registration helper | Dead code — delete or move |

### 2. Orchestration Logic (from CmsModuleExtensions.cs split)

The module-related parts of `CmsModuleExtensions.cs` move to a new file in Aero.Cms.Modules.Modules:

| New File | Methods | Reason |
|----------|---------|--------|
| `Services/ModuleOrchestrationExtensions.cs` | `AddModuleSystemServices()` (module parts), `AddAeroModulesAsync()`, `AddAeroModules()`, `AddAeroCmsModules()`, `RegisterSpecializedInterfaces()`, `GetModules<T>()`, `GetUiModules()` etc. | Orchestrates concretions that now live here |

### 3. Dependencies Added to Aero.Cms.Modules.Modules.csproj

```
Current:
  Aero.Cms.Core
  Aero.Modular

Need to add:
  Microsoft.Extensions.DependencyInjection (already implicit via ASP.NET)
  Microsoft.Extensions.Logging.Abstractions (for ModuleGraphService, etc.)
  Microsoft.Extensions.Options (for ModuleDiscoveryOptions, ModuleGraphOptions)
```

No new project references needed beyond what concretions already require.

---

## What Stays in Aero.Cms.Core (Block Registration)

`CmsModuleExtensions.cs` is split. The block registration survives as:

| File | Method | 
|------|--------|
| `Extensions/BlockServiceExtensions.cs` (new name) | `AddBlockSystemServices()` → registers `IBlockService→MartenBlockService`, `BlockMartenConfiguration` |

---

## Dependency Graph After Refactoring

```
Aero.Modular (abstractions only)
  ↑
Aero.Cms.Modules.Modules (all concretions + orchestration)
  ↑                    ↑
  │              Aero.Cms.Core (blocks only)
  │                    ↑
  └── Aero.Cms.Modules.Setup ──┘
          ↑
    Aero.Cms.Web.Core
          ↑
      Aero.Cms.Web (Setup app → calls Setup, Modules)
      Aero.Cms.Web (Main app → calls all except Setup)
```

---

## Files That Depend on Concretions Being Moved

These are the files that currently reference the concrete classes (direct construction, DI registration, or type usage) and will need updating:

| # | File | What it references | Update needed |
|---|------|-------------------|---------------|
| 1 | `Aero.Cms.Modules.Modules/ModulesModule.cs` | `ModuleStateStore` (DI registration) | Already compiles (same project after move); update `using` |
| 2 | `Aero.Cms.Modules.Modules/Services/ModuleInitializationService.cs` | `ModuleDocument.FromDescriptor()` | Update `using` |
| 3 | `Aero.Cms.Core/Extensions/CmsModuleExtensions.cs` | `ModuleDiscoveryService`, `ModuleGraphService`, `ModuleDiscoveryOptions`, `ModuleGraphOptions`, `AeroModuleBuilder`, `ModuleSystemStartupException` | **Split** — block part stays, module orchestration moves |
| 4 | `Aero.Cms.Web.Core/Eextensions/AeroWebAppExtensions.cs` | `AddModuleSystemServices()`, `AddAeroModulesAsync()`, `ModuleGraph` | Add project ref + update `using` |
| 5 | `Aero.Cms.Modules.Setup/ServerTargetSetupExecutor.cs` | `new ModuleStateStore(session)`, `IModuleDiscoveryService` | Update `using` |
| 6 | `Aero.Cms.Modules.Setup/SeedDataService.cs` | `IModuleInitializationService` (via using) | Update `using` |
| 7 | `Aero.Cms.Modules.Setup/SetupModule.cs` | `Aero.Cms.Web.Core.Modules` (for `AeroModuleBase`) | Update `using` |
| 8 | All 40+ module `*Module.cs` files | `Aero.Cms.Web.Core.Modules` (for `AeroModuleBase`, `IAeroModule`) | Update `using` |
| 9 | `Aero.Cms.Web.Core/Modules/AeroWebModule.cs` | Namespace `Aero.Cms.Web.Core.Modules` | Keep as-is (it's the old namespace holder) |
| 10 | `tests/Aero.Cms.Core.Tests/Integration/RealModuleDiscoveryTests.cs` | `new ModuleDiscoveryService(...)`, `ModuleDiscoveryOptions`, `ModuleGraph` | Add project ref + update `using` |
| 11 | `tests/Aero.Cms.Core.Tests/Integration/MartenSchemaCompositionTests.cs` | `new AeroModuleBuilder(...)`, `AddModuleSystemServices()` | Add project ref + update `using` |
| 12 | `tests/Aero.Cms.Core.Tests/Integration/ModuleHostIntegrationTests.cs` | `new AeroModuleBuilder(...)` | Add project ref + update `using` |
| 13 | `tests/Aero.Cms.Core.Tests/Models/ModuleBuilderTests.cs` | `new AeroModuleBuilder(...)` | Add project ref + update `using` |
| 14 | `tests/Aero.Cms.Core.Tests/Models/ModuleModelTests.cs` | `ModuleGraph`, `using Aero.Cms.Web.Core.Modules` | Update `using` |
| 15 | `tests/Aero.Cms.Core.Tests/Services/ModuleDependencyResolver.cs` | `ModuleGraph` | Update `using` |
| 16 | `tests/Aero.Cms.Core.Tests/TestModules/TestModules.cs` | `AeroModuleBase`, `IAeroModuleBuilder` | Update `using` |
| 17 | `tests/Aero.Cms.Core.Tests/Extensions/ModuleExtensionsTests.cs` | `using Aero.Cms.Web.Core.Modules` | Update `using` |
| 18 | `tests/Aero.Cms.Core.Tests/DependencyResolution/*.cs` | `using Aero.Cms.Web.Core.Modules` | Update `using` |
| 19 | `tests/Aero.Cms.Core.Tests/Integration/SetupCompletionServiceTests.cs` | `IModuleDiscoveryService`, `IModuleStateStore`, `using` | Update `using` |

---

## Implementation Steps

### Step 1: Create new files in Aero.Cms.Modules.Modules (additive, no breakage)

1. **Move** these files physically from `Aero.Modular/` → `Aero.Cms.Modules.Modules/Services/`:
   - `ModuleDiscoveryService.cs`
   - `ModuleGraphService.cs`
   - `ModuleStateStore.cs`
   - `DatabaseBackedModuleLoader.cs`

2. **Extract** `AeroModuleBuilder` class from `Aero.Modular/IModuleBuilder.cs` into `Aero.Cms.Modules.Modules/Services/AeroModuleBuilder.cs`

3. **Create** `Aero.Cms.Modules.Modules/Services/ModuleOrchestrationExtensions.cs` containing the orchestration logic extracted from `CmsModuleExtensions.cs`

4. **Delete** `Aero.Modular/ModuleServiceExtensions.cs` (dead code — `AddModuleSystem()` is never called)

5. **Update `Aero.Cms.Modules.Modules.csproj`** to add any missing package refs (options, logging)

**Build at this point**: Breaks for callers that reference moved types from old location.

### Step 2: Split CmsModuleExtensions.cs in Aero.Cms.Core

1. **Rename** `CmsModuleExtensions.cs` → `BlockServiceExtensions.cs`
2. **Strip** it down to only contain `AddBlockSystemServices()` (block registration)
3. Remove `AddAeroModulesAsync()`, `AddAeroModules()`, `RegisterSpecializedInterfaces()`, `GetModules<T>()` and helpers

**Build**: Still broken (callers of orchestration in AeroWebAppExtensions break).

### Step 3: Update Aero.Cms.Modules.Setup

1. Update `ServerTargetSetupExecutor.cs`: update `using` statements, verify `ModuleStateStore` still accessible (now in same dependency chain)
2. Update `SeedDataService.cs`: verify imports
3. Update `SetupModule.cs`: verify imports

### Step 4: Update Aero.Cms.Web.Core

1. Add `<ProjectReference Include="..\Aero.Cms.Modules.Modules\Aero.Cms.Modules.Modules.csproj" />` to `Aero.Cms.Web.Core.csproj`
2. Update `AeroWebAppExtensions.cs`:
   - Change `using Aero.Cms.Core.Extensions` → only for `AddBlockSystemServices()`
   - Add `using Aero.Cms.Modules.Modules.Services` for `AddModuleSystemServices()` and `AddAeroModulesAsync()`
   - If `ModuleGraph` is used by type, update that `using` too

### Step 5: Update all Module Projects (40+)

For each module project `Aero.Cms.Modules.*`:
1. Add `using Aero.Modular;` (for `AeroModuleBase`, interfaces) — may already be present
2. Remove `using Aero.Cms.Web.Core.Modules;` if it was only used for Aero.Modular types (not `AeroWebModule`)

**If a module extends `AeroWebModule`** (not `AeroModuleBase`), keep `using Aero.Cms.Web.Core.Modules;` for that type.

Bulk operation — can be scripted with `Select-String` to find which modules need what.

### Step 6: Update Tests

1. Add `<ProjectReference Include="..\..\src\Aero.Cms.Modules.Modules\Aero.Cms.Modules.Modules.csproj" />` to `tests/Aero.Cms.Core.Tests.csproj`
2. Update all `using Aero.Cms.Web.Core.Modules;` across test files
3. For tests that directly construct concretions (`new ModuleDiscoveryService(...)`, `new AeroModuleBuilder(...)`), verify namespaces resolve

### Step 7: Clean up Aero.Modular

1. Delete moved files from `Aero.Modular/`
2. Verify `IModuleBuilder.cs` no longer contains `AeroModuleBuilder` class (just interfaces)
3. Remove unused package refs from `Aero.Modular.csproj` if any

**Build at this point**: Should be green.

---

## Verification

1. `dotnet build` on entire solution passes
2. `dotnet test` on Aero.Cms.Core.Tests passes
3. Verify Setup app can discover and persist modules
4. Verify main web app can discover and register all modules except Setup

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `ModuleDocument` circular dependency | High if moving to Modules | High | Resolve by keeping in Aero.Modular (Option B) or moving interfaces (Option D) |
| 50+ files with stale `using Aero.Cms.Web.Core.Modules` | Certain | Medium | Bulk replace with `using Aero.Modular;` and/or `using Aero.Cms.Modules.Modules.Services;` |
| Test projects missing new project ref | Certain | Medium | Add ref in Step 6 |
| `AeroModuleBuilder` extracted from `IModuleBuilder.cs` — test modules use `IAeroModuleBuilder` interface | Low | Low | Interface stays in Aero.Modular; tests just need `using Aero.Modular;` |
| `ModuleSystemStartupException` used in orchestration — still accessible | Low | Low | It stays in Aero.Modular; orchestrator references it from there |
