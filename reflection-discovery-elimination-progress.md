# Reflection Discovery Elimination — Progress Tracker

## Status Legend
- [ ] Not started
- [~] In progress
- [x] Complete
- [!] Blocked

---

## Phase 1: Setup Identity Store (Workstream 3)

**Goal:** Replace assembly-scan lookup in CreateUserStore() with direct typed dependency.

[x] 1.1 — Add ProjectReference Setup → Aero.Cms.Modules.Identity
[x] 1.2 — Replace CreateUserStore() with `new UserStore<AeroUser, AeroRole>(session)`
[x] 1.3 — Remove `using System.Reflection;`

**Verify:** ✅ `dotnet build src/Aero.Cms.Modules.Setup/` — 0 errors
**Verify:** ✅ No `AppDomain.*GetAssemblies` hits

---

## Phase 2: Descriptor-Based Module Initialization

**Goal:** ModuleInitializationService accepts descriptors instead of calling discovery.

[x] 2.1 — Update IModuleInitializationService: `InitializeModulesAsync(IReadOnlyList<ModuleDescriptor>, CancellationToken)`
[x] 2.2 — Update ModuleInitializationService to use descriptor parameter, remove IModuleDiscoveryService dependency
[x] 2.3 — Update ServerTargetSetupExecutor to pass descriptors (via ExecuteAsync parameter or DI)
[x] 2.4 — Update SeedDataService / call chain to pass descriptors through
[x] 2.5 — (Skipped) IModuleInitializationService DI registration in ModulesModule.cs kept for backward compat; SeedDatabaseService resolves from DI correctly

**Verify:** `dotnet build src/Aero.Cms.Modules.Setup/ src/Aero.Cms.Modules.Modules/` — 0 errors ✅
**Verify:** No production code references `IModuleDiscoveryService` in modified files ✅

---

## Phase 3: Remove Main-Path Module Reflection

**Goal:** Production path never uses ModuleDiscoveryService.

[x] 3.1 — Remove `IModuleDiscoveryService` registration from AddModuleSystemServices()
[x] 3.2 — Make generatedDescriptors required, remove else-branch in AddAeroModulesAsync
[x] 3.3 — Simplify temp service provider — no discoveryService resolution needed
[ ] 3.4 — Update/remove RealModuleDiscoveryTests (no longer test removed path)
[x] 3.5 — (Cleanup) Delete ModuleDiscoveryService.cs, DatabaseBackedModuleLoader.cs
[ ] 3.6 — (Cleanup) Delete IModuleDiscoveryService.cs from Aero submodule (submodule change — separate PR)
[ ] 3.7 — Remove ModuleCatalogMode enum if only one mode remains (deprecated but kept for compat)

**Verify:** `dotnet build src/Aero.Cms.Web/` — 0 errors ✅
**Verify:** `rg "IModuleDiscoveryService\|ModuleDiscoveryService" src/ -g "!tests/**"` — no hits ✅
**Verify:** Full app startup uses generated descriptors only ✅

---

## Phase 4: Block Metadata to Generated Manifest

**Goal:** BlockEditingService uses generated CmsBlockManifest instead of reflection.

[x] 4.1 — Replace static constructor in BlockEditingService: uses `GeneratedBlockModelManifest.Blocks.Values`
[x] 4.2 — Remove `ScanBlockTypes()` method
[x] 4.3 — Remove `using System.Reflection;` from BlockEditingService.cs
[x] 4.4 — Delete BlockMetadataProvider.cs (no downstream clients)
[ ] 4.5 — (Optional) Add TryCreateBlock() factory to eliminate Activator.CreateInstance

**Verify:** `dotnet build src/Aero.Cms.Abstractions/` — 0 errors ✅
**Verify:** `rg "assembly.GetTypes\|GetCustomAttribute" src/Aero.Cms.Abstractions/Blocks/` — no hits ✅

---

## Phase 5: Social Plug Discovery

**Goal:** Replace per-instance `GetMethods()` scanning with startup-time cached catalog.

[x] 5.1 — Add `ISocialPlugCatalog` interface to Aero submodule
[x] 5.2 — Add static `SocialProviderBase.PlugCatalog` property; route `DiscoverPlugs()` and `GetPlug()` through it
[x] 5.3 — Create `ReflectionSocialPlugCatalog`: one-time startup scan, caches results
[x] 5.4 — Wire up in `Program.cs`: `SocialProviderBase.PlugCatalog = new ReflectionSocialPlugCatalog()`

**Verify:** `dotnet build src/Aero.Cms.Web/` — 0 errors ✅
**Verify:** `SocialProviderBase.DiscoverPlugs()` no longer scans per-instance in production ✅

---

## Phase 6: Analyzer Guardrails

**Goal:** Prevent reintroduction of reflection scanning in production code paths.

[x] 6.1 — Create `EmbeddedAttributesGenerator` that emits `[LegacyReflectionDiscovery]` marker
[x] 6.2 — Create `ReflectionDiscoveryAnalyzers` with AERO010, AERO011, AERO012 diagnostics
[x] 6.3 — Annotate `ReflectionSocialPlugCatalog` with `[LegacyReflectionDiscovery]`
[ ] 6.4 — AERO013: Broad interface scanning in generators (code review — not an analyzer rule)

**Verify:** `dotnet build src/Aero.Cms.Web/` — 0 errors ✅
**Verify:** Zero AERO010-AERO012 warnings on existing production code ✅

PowerShell:
```powershell
# 1. Full build
dotnet build src/Aero.Cms.Web/ --no-restore

# 2. Reflection discovery audit — should return NO hits in production code
rg -n "AppDomain\.CurrentDomain\.GetAssemblies" src/ Aero/src/ -g "*.cs" -g "!**/bin/**" -g "!**/obj/**" -g "!**/Generated/**"
rg -n "\.GetTypes\(\)" src/ Aero/src/ -g "*.cs" -g "!**/bin/**" -g "!**/obj/**" -g "!**/Generated/**" -g "!**/tests/**"
rg -n "Assembly\.LoadFrom\|DependencyContext\.RuntimeLibraries" src/ Aero/src/ -g "*.cs" -g "!**/bin/**" -g "!**/obj/**"

# 3. Full test suite
dotnet test tests/Aero.Cms.Core.Tests/

# 4. REMINDER: Run app verification
#    ╔══════════════════════════════════════════════════╗
#    ║  🔴 Run the app and verify:                     ║
#    ║  • Startup completes without errors             ║
#    ║  • Modules load                                 ║
#    ║  • Setup seeding works                           ║
#    ║  • Block editing in admin panel                  ║
#    ╚══════════════════════════════════════════════════╝
```

---

## Post-Verification Cleanup

- [ ] Delete `reflection-discovery-elimination-strategy.md` (spec merged)
- [ ] Delete `reflection-discovery-elimination-progress.md` (this file)
- [ ] Confirm no stale usings in edited files
