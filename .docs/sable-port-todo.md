# Sable Port TODO

## Completed
- [x] AeroDB.Sable as primary document store (replaces MartenDB)
- [x] `AeroEmbeddedDbService` — MysticMind Postgres → SurrealDB embedded (Sable)
- [x] `StartupServiceNames` — `Postgres` → `AeroDb`
- [x] `IInfrastructureReadinessSnapshot` — `PostgresReady` → `AeroDbReady`
- [x] `SetupStatusEndpoints` — Postgres refs → AeroDb
- [x] `AeroDbOptions` — stripped Postgres props, Sable-focused
- [x] `AeroAppServerConstants` — Postgres constants removed, Sable defaults added
- [x] `DatabaseBootstrapService` — embedded mode no longer writes Postgres conn string
- [x] `Setup.razor` — connection string placeholder updated to SurrealDB endpoint
- [x] `Aero.AppServer.csproj` — `MysticMind.PostgresEmbed` removed
- [x] `Setup.razor.cs` — `JSRuntime.InvokeVoidAsync("aero.setup.clearStorage")` removed
- [x] `RefreshTokenService` — already uses `IDocumentSession` (Sable)
- [x] `AeroDbContext` — removed unused DbSets, then **deleted entirely**
- [x] `ApiAuthRepository.cs` — deleted (zero consumers)
- [x] `AiUsageLogsRepository.cs` — deleted (zero consumers)
- [x] `AuthInitializationExtensions.cs` (EfCore) — deleted (zero callers)
- [x] `ApiAuthContextFactory.cs` — deleted (migrations factory)
- [x] `AeroDbExtensions.cs` — stripped of all EF Core Npgsql registrations
- [x] `PrepareAeroAppAsync` — removed `aeroContext.Database.MigrateAsync()`
- [x] `ServerTargetSetupExecutor.MigrateAsync` — returns `Task.CompletedTask`
- [x] `ApiKeyService` — ported from `AeroDbContext` to `IDocumentSession` (Sable)

### Commerce (`Aero.Cms.Modules.Commerce`) — **ported to Sable, re-enabled**
- [x] `IOrderService` — removed `IGenericEntityFrameworkRepository` inheritance, defined Sable contract
- [x] `OrderService` — rewritten to use `IDocumentSession`
- [x] `CommerceModule` — removed `AddDbContext<CommerceDbContext>(UseNpgsql...)` and `IStartupFilter`
- [x] `CommerceDbContext.cs`, `CommerceDbContextFactory.cs`, `CommerceStartupFilter.cs` — deleted
- [x] Migration files (5 files) — deleted
- [x] `CommerceModule.csproj` — removed `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`, `Aero.EfCore` reference; module re-enabled for compilation
- [x] `SeedDatabaseService` — re-enabled `ICommerceSeedService` parameter and `commerceSeedService.SeedAsync()` call
- [x] `ServerTargetSetupExecutor` — uncommented `commerceSeedService` parameter
- [x] **Catalog** (ProductDocument, ProductTranslation) — already Sable ✓
- [x] **Basket** (BasketDocument) — already Sable ✓
- [x] Full build passes

**EF Core Npgsql is fully removed from the active AeroCMS application. All persistence uses AeroDB.Sable.**

## Remaining (disabled / not active)

### Sites (`Aero.Cms.Modules.Sites`)
- `SiteStartupFilter` — calls `db.Database.Migrate()` via EF Core
- Likely uses EF Core for site/host data — needs investigation

### Legacy (not compiled)
- `src/Aero.Cms.Db.Marten/Legacy/` — entire folder is legacy Marten code, not part of build
