# Module Discovery Strategy — Implementation Progress

> **Status:** ✅ Complete (build verified)  
> **Started:** 2026-05-01  
> **Completed:** 2026-05-01  
> **Strategy doc:** `module-discovery-strategy.md`  

---

## ✅ Step 0: Prove Generator Boundary
- [x] Architecture verified: per-module generator + host aggregation pattern
- [x] Host `HostModuleCatalogGenerator` conditionally skips generation when Aero.Modular/Wolverine types aren't available

## ✅ Step 1: Add Contracts (Aero/src/Aero.Modular/)
- [x] `ModuleAttribute.cs` — compile-time metadata for source-generated discovery
- [x] `IModuleManifestProvider.cs` — instance-based provider contract (`GetDescriptors()`)
- [x] `ModuleManifestProviderAttribute.cs` — assembly-level attribute for host aggregation
- [x] `WolverineHandlersRegistrationAttribute.cs` — assembly-level attribute for handler registration
- [x] `ModuleDescriptor.cs` — removed `[Obsolete]`, added marker interface flags + `Description`

## ✅ Step 2: Module Manifest Generator
- [x] `src/Aero.Cms.SourceGenerators/ModuleManifestGenerator.cs`
  - Uses `ForAttributeWithMetadataName("Aero.Modular.ModuleAttribute")`
  - Validates targets (concrete, non-abstract, non-generic IAeroModule)
  - Detects marker interfaces (IUiModule, IApiModule, etc.) + IConfigureMarten
  - Checks for duplicate names within project
  - Emits `[assembly: ModuleManifestProviderAttribute]` + provider class

## ✅ Step 3: Host Catalog Generator
- [x] `src/Aero.Cms.SourceGenerators/HostModuleCatalogGenerator.cs`
  - Reads `ModuleManifestProviderAttribute` + `WolverineHandlersRegistrationAttribute` from referenced assemblies
  - Emits `GeneratedAeroModuleCatalog` with `Providers` + `Descriptors`
  - Emits `GeneratedWolverineHandlerCatalog` with `Register(WolverineOptions)`
  - Conditionally skips generation when Aero.Modular/Wolverine not referenced
- [x] `src/Aero.Cms.SourceGenerators/WolverineHandlerGenerator.cs`
  - Uses `ForAttributeWithMetadataName("Wolverine.Attributes.WolverineHandlerAttribute")`
  - Emits `IncludeType<T>()` calls + assembly-level `WolverineHandlersRegistrationAttribute`

## ✅ Step 4: Runtime Merge/Policy
- [x] `IModuleRuntimeStateMerger` — interface for merging discovered descriptors with stored state
- [x] `ModuleRuntimeStateMerger` — implementation using `IModuleStateStore`
- [x] `ModuleOrchestrationExtensions.cs` — updated with `ModuleCatalogMode` enum + generated descriptor support
- [x] `ModuleSystemStartupException` — already existed in `ModuleExtensions.cs`

## ✅ Step 5: Wolverine Callback + AERO002 Analyzer
- [x] `AeroAppServerExtensions.cs` — accepts `Action<WolverineOptions>?` callback parameter
- [x] `Program.cs` — passes `GeneratedWolverineHandlerCatalog.Register`
- [x] `Analyzers/Aero002WolverineHandlerRequired.cs` — reports warning when IWolverineHandler classes lack `[WolverineHandler]`

## ✅ Step 6: Migrate Modules
- [x] Added `[Module(nameof(XxxModule))]` to 36 module classes
- [x] Added `using Aero.Modular;` to 5 module files that were missing it
- [x] Confirmed `[WolverineHandler]` already present on `SitemapInvalidationHandler` and `SlugUpdatedHandler`

## ✅ Step 7: Wire Generators via Directory.Build.props
- [x] `src/Directory.Build.props` — adds `Aero.Cms.SourceGenerators` as analyzer to all src/ projects (except itself)

## 🔲 Step 8: Delete Main-Host Reflection Path (deferred)
- [ ] Reflection fallback still in place for backward compatibility
- [ ] Only remove after full testing suite proves generated path works

---

## Files Created

| File | Location | Purpose |
|------|----------|---------|
| `ModuleAttribute.cs` | `Aero/src/Aero.Modular/` | `[Module]` compile-time attribute |
| `IModuleManifestProvider.cs` | `Aero/src/Aero.Modular/` | Provider contract |
| `ModuleManifestProviderAttribute.cs` | `Aero/src/Aero.Modular/` | Assembly-level marker |
| `WolverineHandlersRegistrationAttribute.cs` | `Aero/src/Aero.Modular/` | Handler registration marker |
| `ModuleManifestGenerator.cs` | `src/Aero.Cms.SourceGenerators/` | Per-module manifest generator |
| `HostModuleCatalogGenerator.cs` | `src/Aero.Cms.SourceGenerators/` | Host aggregator generator |
| `WolverineHandlerGenerator.cs` | `src/Aero.Cms.SourceGenerators/` | Per-module handler generator |
| `Aero002WolverineHandlerRequired.cs` | `src/Aero.Cms.SourceGenerators/Analyzers/` | AERO002 analyzer |
| `IModuleRuntimeStateMerger.cs` | `src/Aero.Cms.Modules.Modules/Services/` | State merger interface |
| `ModuleRuntimeStateMerger.cs` | `src/Aero.Cms.Modules.Modules/Services/` | State merger impl |
| `module-discovery-strategy-progress.md` | `./` | This progress doc |

## Files Modified

| File | Change |
|------|--------|
| `Aero/src/Aero.Modular/ModuleDescriptor.cs` | Removed `[Obsolete]`, added marker flags |
| `src/Directory.Build.props` | Added source generator analyzer reference |
| `src/Aero.Cms.Modules.Modules/Services/ModuleOrchestrationExtensions.cs` | Added `ModuleCatalogMode`, generated descriptor support |
| `src/Aero.AppServer/AeroAppServerExtensions.cs` | Added `configureWolverine` callback parameter |
| `src/Aero.Cms.Web/Program.cs` | Passes `GeneratedWolverineHandlerCatalog.Register` |
| 36 module files | Added `[Module(nameof(XxxModule))]` |
| 5 module files | Added `using Aero.Modular;` |
