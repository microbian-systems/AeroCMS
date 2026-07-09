# SurrealDB → Marten Port Plan

**Status:** Active  
**Last updated:** 2026-07-09  
**Goal:** Replace Marten/PostgreSQL with AeroDB/SurrealDB ("Sable") across the entire AeroCMS. Marten code is preserved in `Aero.Cms.Db.Marten` as non-buildable reference.

---

## Progress

- [x] **Phase 1** — Marten code archived in `Aero.Cms.Db.Marten/Legacy/`
- [ ] **Phase 2a** — DI registration swap (`ConfigureMartenDb` → `AddAeroDB`)
- [ ] **Phase 2b** — Namespace swap (`using Marten;` → `using AeroDB;`)
- [ ] **Phase 2c** — Module system interface swap (`IConfigureMarten` → `IConfigureAeroDB`)
- [ ] **Phase 2d** — Repository refactoring (`GenericMartenRepository` → direct session)
- [ ] **Phase 3a** — Wire up `AeroDB.WolverineFx`
- [ ] **Phase 3b** — Wire up `AeroDB.AspNetIdentity`
- [ ] **Phase 3c** — Update seed/setup logic
- [ ] **Phase 4** — Strip Marten package/project references

---

## Key Discovery

AeroDB (`./AeroDB/src/AeroDB`) has **near-perfect Marten API parity**. The same type names exist in both — just different namespaces:

| Marten | AeroDB | Namespace |
|--------|--------|-----------|
| `IDocumentSession` | `IDocumentSession` | `Marten` → `AeroDB` |
| `IDocumentStore` | `IDocumentStore` | `Marten` → `AeroDB` |
| `IQuerySession` | `IQuerySession` | `Marten` → `AeroDB` |
| `StoreOptions` | `StoreOptions` | `Marten` → `AeroDB` |
| `IEvents` | `IEvents` | `Marten` → `AeroDB` |
| `IConfigureMarten` | `IConfigureAeroDB` | `Marten` → `AeroDB` |
| `IInitialData` | `IInitialData` | `Marten` → `AeroDB` |
| `DocumentTracking` | `DocumentTracking` | `Marten` → `AeroDB` |

Everything present: event sourcing (`StartStream`, `Append`, `FetchStream`, `AggregateStreamAsync`, projections, upcasting, async daemon), LINQ queries (`ISurrealDbQueryable<T>` implements `IOrderedQueryable<T>`), compiled queries, batch queries, soft deletes, multi-tenancy, metadata, patching, diagnostics, pagination.

Additionally, AeroDB provides drop-in replacements for:

- **Wolverine integration** — `AeroDB.WolverineFx` (outbox, saga storage, transport, scheduled jobs, subscriptions)
- **ASP.NET Identity** — `AeroDB.AspNetIdentity` (`AeroDBUserStore`, `AeroDBRoleStore`)
- **Source generators** — `AeroDB.SourceGenerators` (auto-discovers `IConfigureAeroDB` implementations)
- **Configuration scanning** — `IConfigureAeroDB` resolved from DI automatically by `DocumentStore`

This means the migration is a **namespace swap** (`using Marten;` → `using AeroDB;`) with minimal code changes beyond imports and DI registration.

---

## Migration Strategy: 1:1 Replacement

No multi-provider abstraction layer. No `IAeroDbSession`/`IAeroDbStore` interfaces. The existing `IDocumentSession`/`IDocumentStore` types are shared between both providers — just the namespace and DI registration change.

```
Before: services.ConfigureMartenDb(config, env, connString);
         |-- Marten.DocumentStore / Marten.IDocumentSession
         |-- WolverineFx.Marten outbox
         |-- MartenUserStore / MartenRoleStore (Aero.Marten.Identity)

After:  services.AddAeroDB(connString, opts => { ... });
         |-- AeroDB.DocumentStore / AeroDB.IDocumentSession
         |-- AeroDB.WolverineFx outbox
         |-- AeroDBUserStore / AeroDBRoleStore (AeroDB.AspNetIdentity)
```

---

## Phase 1 — `Aero.Cms.Db.Marten` (Marten Dormancy) ✅

**Done.** All Marten-specific code archived to `src/Aero.Cms.Db.Marten/Legacy/` as non-buildable reference.

### Archive structure

```
Legacy/
├── Aero.Marten/                  # Full shim library (28 files)
│   ├── GenericMartenRepository.cs, AeroDb.cs, MartenDbExtensions.cs
│   ├── Identity/ (UserStore, RoleStore, AeroDbIdentityOptions)
│   ├── Query/, Services/, Extensions/
├── AppServer/                    # Startup code (3 files)
│   ├── AeroEmbeddedDbService.cs, AeroDbOptions.cs, AeroAppServerExtensions.cs
├── Startup/                      # Infrastructure (6 files)
│   ├── InfrastructureConnectionStringResolver.cs
│   ├── ReadinessSnapshot, Coordinator, MultiStartupSignal, ServiceNames
├── Core/                         # Core Marten services (15 files)
│   ├── Blocks/ (MartenBlockService, BlockMartenConfiguration, +2)
│   ├── Content/ (MartenContentService, MartenContentQueryService, +3)
│   ├── Data/ (AeroCmsDB.cs)
│   └── Extensions/ (BlockServiceExtensions, ContentServiceExtensions)
├── Data/                         # Repositories & queries (16 files)
│   ├── Repositories/ (Alias, Category, Compiled, Site, Tag, Tenant, UserProfile)
│   └── Queries/ (Alias, Category, Docs, Pages, Posts, Settings, Sites, Tag, Tenant)
├── MartenIdentity/               # Aero.Cms.Marten.Identity (5 files)
├── Generated/                    # GeneratedMartenConfiguration.cs
├── Setup/                        # Marten-dependent setup (3 files)
│   ├── ServerTargetSetupExecutor.cs, SeedDataService.cs, SetupStateStore.cs
├── Modules/                      # Module-level Marten code (17 files)
│   ├── Banner/Commerce/Media/ (GenericMartenRepository usage)
│   └── IConfigureMartenImplementations/ (9 modules)
├── GlobalUsings/                 # 14 global using Marten; files preserved
├── ModuleCsprojReferences/       # 22 csproj files showing Marten refs
└── Directory.Packages.props      # Marten version pins
```

### csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Marten" VersionOverride="8.37.1" />
    <PackageReference Include="Marten.AspNetCore" VersionOverride="8.37.1" />
    <PackageReference Include="WolverineFx.Marten" VersionOverride="5.39.3" />
    <PackageReference Include="Weasel.Postgresql" VersionOverride="8.15.2" />
    <PackageReference Include="MysticMind.PostgresEmbed" VersionOverride="4.0.0" />
    <PackageReference Include="Npgsql" VersionOverride="9.0.3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Aero\src\Aero.Marten\Aero.Marten.csproj" />
    <ProjectReference Include="..\..\Aero\src\Aero.Core\Aero.Core.csproj" />
    <ProjectReference Include="..\..\Aero\src\Aero.Models\Aero.Models.csproj" />
    <ProjectReference Include="..\Aero.Cms.Core\Aero.Cms.Core.csproj" />
    <ProjectReference Include="..\Aero.Cms.Abstractions\Aero.Cms.Abstractions.csproj" />
    <ProjectReference Include="..\Aero.AppServer\Aero.AppServer.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Compile Remove="Legacy/**/*.cs" />
    <None Include="Legacy/**/*.cs" />
    <None Include="Legacy/**/*.csproj" />
    <None Include="Legacy/**/*.props" />
  </ItemGroup>
</Project>
```

---

## Phase 2 — Namespace Swap (Core + Modules)

Replace `using Marten;` with `using AeroDB;` across all active source files.

### What changes

| Current | New |
|---------|-----|
| `using Marten;` | `using AeroDB;` |
| `using Marten.Pagination;` | `using AeroDB.Pagination;` |
| `using Marten.Events;` | `using AeroDB.Events;` |
| `using Marten.Events.Projections;` | `using AeroDB.Events.Projections;` |
| `using Marten.Linq;` | `using AeroDB.Linq;` |
| `using Marten.Metadata;` | `using AeroDB.Metadata;` |
| `using Aero.Marten;` | `using AeroDB;` |
| `using Aero.Marten.Identity;` | `using AeroDB.AspNetIdentity;` |
| `global using Marten;` (14 GlobalUsings.cs) | `global using AeroDB;` |
| `IConfigureMarten` | `IConfigureAeroDB` |
| `StoreOptions` (Marten's) | `StoreOptions` (AeroDB's) |
| `MartenPageContentService` | `PageContentService` (rename if desired) |
| `GenericMartenRepository<T>` | Remove inheritance (use `IDocumentSession` directly or AeroDB-native patterns) |
| `services.ConfigureMartenDb(...)` | `services.AddAeroDB(...)` |
| `Schema.For<T>()` (Marten) | `Schema.For<T>()` (AeroDB) |

### Effect on project references

| Project | Before | After |
|---------|--------|-------|
| `Aero.Cms.Db.Abstractions` | n/a | (not needed — types are shared) |
| Module `.csproj` files | `PackageReference Marten` + `ProjectReference Aero.Marten` | `ProjectReference AeroDB` |
| `Aero.AppServer` | `PackageReference Marten` + `Marten.AspNetCore` + `WolverineFx.Marten` | `ProjectReference AeroDB` + `AeroDB.WolverineFx` |
| `Aero.Cms.Web` | `PackageReference WolverineFx.Marten` | `ProjectReference AeroDB.WolverineFx` |

### Specific changes by area

#### A) DI registration (`AeroAppServerExtensions.cs`)

```csharp
// Before
services.ConfigureMartenDb(config, env, connString);
services.AddWolverine(opts => { opts.Discovery.DisableConventionalDiscovery(); ... });

// After
services.AddAeroDB(connString, opts => {
    opts.Schema.For<AeroRole>().Identity(x => x.Id);
    opts.Schema.For<AeroUser>().Identity(x => x.Id);
});
services.AddWolverine(opts => {
    opts.Discovery.DisableConventionalDiscovery();
    opts.UseAeroDBPersistence();  // from AeroDB.WolverineFx
    ...
});
```

#### B) Module system — `IConfigureMarten` → `IConfigureAeroDB`

- `Aero.Cms.Modules.Modules/Services/ModuleOrchestrationExtensions.cs:216` — Switch interface check
- `Aero.Cms.Modules.Modules/Services/AeroModuleBuilder.cs` — `AddMartenConfiguration<T>()` → `AddDbConfiguration<T>()`
- `Aero.Cms.SourceGenerators/ModuleManifestGenerator.cs` — Switch interface detection
- All 10 module classes implementing `IConfigureMarten` → implement `IConfigureAeroDB` instead
- `Aero/src/Aero.Modular/AeroModuleBase.cs` — Remove `IConfigureMarten` from base class (submodule change)

#### C) Remove `using Aero.Marten;` references

6 files in `src/` reference `Aero.Marten`:
- `src/Aero.AppServer/AeroAppServerExtensions.cs` → Remove, replaces with `AeroDB`
- `src/Aero.Cms.Data/Repositories/*.cs` → Remove, move to direct `AeroDB.IDocumentSession` usage
- `src/Aero.Cms.Core/Data/AeroCmsDB.cs` → This file moves to Db.Marten (Legacy)
- `src/Aero.Cms.Modules.*/*.cs` → Replace with `using AeroDB;`

#### D) Remove `global using Marten;`

14 `GlobalUsings.cs` files in modules. Replace with `global using AeroDB;`.

#### E) `IAeroCmsDb` / `AeroCmsDB`

The AeroCMS-specific database facade that wraps `IDocumentSession`. Since the interface (`IAeroCmsDb`) exposes Marten-specific types:

```csharp
public interface IAeroCmsDb : IAeroDb  // IAeroDb from Aero.Marten
{
    IDocumentSession session { get; }
    ...
}
```

This needs to be replaced with direct `IDocumentSession` usage (from AeroDB namespace now). The `IAeroCmsDb` abstraction is a thin wrapper — modules can inject `IDocumentSession` directly.

#### F) `GenericMartenRepository<T>` usage

5 service classes inherit from it. These need to be refactored to either:
- Inject `IDocumentSession` directly and use it for CRUD operations
- Or use a lightweight base class that takes `IDocumentSession`

The simplest approach: replace `GenericMartenRepository<T>` with a new `RepositoryBase<T>` that does the same operations via AeroDB's `IDocumentSession`.

#### G) Orleans grains

13 grains inject `IDocumentStore`. No change needed to the interface name — just the namespace import changes from `Marten` to `AeroDB`.

---

## Phase 3 — AeroDB Integration

### Steps

1. Add `AeroDB` project reference to `Directory.Packages.props` (or the solution-level package management)
2. Wire up AeroDB DI: `services.AddAeroDB()` with connection string
3. Wire up Wolverine: `opts.UseAeroDBPersistence()`
4. Wire up Identity: `services.AddAeroDBStores<AeroUser, AeroRole>()`
5. Replace `ServerTargetSetupExecutor` with AeroDB-native seed logic (uses the same `IDocumentSession`/`IEvents`/`IInitialData` patterns)
6. Configure schema/entity mappings

### Configuration example

```csharp
// Program.cs / AeroAppServerExtensions.cs
services.AddAeroDB(connString, opts => {
    opts.DatabaseSchemaName = "aero";
    opts.Events.StreamIdentity = StreamIdentity.AsString;
    opts.UseSystemTextJsonForSerialization();
    opts.Schema.For<AeroRole>().Identity(x => x.Id);
    opts.Schema.For<AeroUser>().Identity(x => x.Id);
});
```

---

## Phase 4 — Remove Marten References

Once all modules compile against AeroDB:

- Remove `PackageReference Marten` from all module csproj files
- Remove `PackageReference WolverineFx.Marten`
- Remove `ProjectReference Aero.Marten` from all module csproj files
- Delete `src/Aero.Cms.Marten.Identity/` (functionality replaced by `AeroDB.AspNetIdentity`)
- Delete `src/Aero.Cms.Data/` (repositories either moved to Db.Marten or refactored)
- Delete or gut `src/Aero.Cms.Core/Data/AeroCmsDB.cs` (replaced by direct `IDocumentSession` usage)
- Clean up `src/Aero.AppServer/Startup/` of PG-specific files
- Delete `src/Aero.AppServer/AeroEmbeddedDbService.cs` (moved to Db.Marten)
- Delete `src/Aero.AppServer/AeroDbOptions.cs` (moved to Db.Marten)

---

## Execution Order

```
[x] Phase 1: Move Marten code into Aero.Cms.Db.Marten (Legacy/)
[ ] Phase 2a: Replace DI registration (ConfigureMartenDb → AddAeroDB)
[ ] Phase 2b: Namespace swap across all active code
[ ] Phase 2c: Module system interface swap (IConfigureMarten → IConfigureAeroDB)
[ ] Phase 2d: Repository refactoring (GenericMartenRepository → direct session)
[ ] Phase 3a: Wire up AeroDB.WolverineFx
[ ] Phase 3b: Wire up AeroDB.AspNetIdentity
[ ] Phase 3c: Update seed/setup logic
[ ] Phase 4: Strip Marten references
```

---

## Key Design Decisions

1. **No abstraction layer** — AeroDB already mirrors Marten's API surface identically. The "abstraction" is the shared `IDocumentSession`/`IDocumentStore` contract. The migration is a namespace swap + DI swap.

2. **Dormant Marten** — All Marten code moves to `Aero.Cms.Db.Marten` as non-buildable reference. To re-activate Marten later, restore file compilation and swap the DI registration.

3. **Single provider at runtime** — No multi-provider strategy. AeroDB/Sable is the only active provider. Multi-provider support is deferred to a future task.

4. **`GenericMartenRepository<T>` removal** — Replace with direct `IDocumentSession` injection in the 5 services that use it. AeroDB intentionally avoids this repository pattern — it uses the session directly.

5. **`IConfigureMarten` → `IConfigureAeroDB`** — The Aero source submodule's `AeroModuleBase` changes from implementing `IConfigureMarten` to implementing `IConfigureAeroDB`. Module system registration follows suit.

6. **Minimal code changes per module** — Because the interface names are identical, most modules only need namespace import changes. The most invasive changes are in:
   - `AeroAppServerExtensions.cs` (DI registration)
   - `Aero.Cms.Core` data/block/content services
   - Module system (orchestrator + builder + source generator)
   - Repository-reliant services (Banner, Commerce, Media)
