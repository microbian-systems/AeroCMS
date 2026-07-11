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

## Pending Modules (EF Core Npgsql → Sable)
These modules still use EF Core Npgsql for their relational data and need porting to AeroDB.Sable:

### Commerce (`Aero.Cms.Modules.Commerce`)
- **Currently disabled from compilation** (marked as None in csproj)
- `CommerceModule.cs:79-82` — `CommerceDbContext` uses `UseNpgsql(ConnectionStrings:aero)` for Orders
- `CommerceStartupFilter` — calls `db.Database.Migrate()` on startup
- `ICommerceSeedService` — required by `SeedDatabaseService` (DI failure if Commerce module excluded)
- Models to port:
  - `CommerceDbContext` → `IDocumentStore` / `IDocumentSession`
  - `OrderEntity` → document model
  - Catalog: `ProductDocument`, `ProductTranslation` (already Sable via `IConfigureAeroDB`)
  - Basket: `BasketDocument` (already Sable via `IConfigureAeroDB`)

### Sites (`Aero.Cms.Modules.Sites`)
- `SiteStartupFilter` — calls `db.Database.Migrate()` via EF Core
- Likely uses EF Core for site/host data

### Other EF Core Consumers
- `AeroDbContext` (Identity — `AeroDBUserStore` already migrated)
- `ServerTargetSetupExecutor.MigrateAsync` — uses `UseNpgsql` for Identity migrations
