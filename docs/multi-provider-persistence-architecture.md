# Aero CMS Multi-Provider Persistence Architecture

> [!IMPORTANT]
> **SUPERSEDED — NOT ADOPTED.** The multi-provider abstraction (Marten/Postgres,
> Dali/SurrealDB, Polecat/MSSQL) was **rejected**. The actual architecture is a
> **1:1 replacement of Marten with AeroDB.Sable over SurrealDB** — no
> `IAeroDbSession`/`IAeroDocumentStore` abstraction layer. See
> [`surrealdb-marten-port.md`](surrealdb-marten-port.md). SurrealDB via
> AeroDB.Sable is the exclusive backend database. Keep this document only as a
> historical record of the rejected design.

## Goal

Allow Aero CMS to support multiple document database providers selected
by the user at installation time:

-   SurrealDB *(default)*
-   PostgreSQL
-   MSSQL

Internally, each provider uses a document DB abstraction layer that
sits on top of its database SDK:

- PostgreSQL → **Marten** (document DB layer on Npgsql)
- MSSQL → **Polecat** (document DB layer on SQL Server)
- SurrealDB → **Dali** (document DB layer on SurrealDB.Net)

The doc uses these internal names to reference the provider implementation
packages, while the Setup wizard and config use the database names.

The remainder of the application (modules, middleware, services,
manager, APIs, etc.) should remain provider-agnostic.

------------------------------------------------------------------------

# Design Goals

1.  Modules own their data and persistence mappings.
2.  Modules do **not** know which provider is active.
3.  The selected provider is chosen once during startup.
4.  DI resolves provider-specific implementations automatically.
5.  Provider-specific code lives in provider packages, never in modules.
6.  Modules expose persistence intent, not provider implementation.

------------------------------------------------------------------------

# Provider Mapping

| Setup dropdown | Config value | Internal codename | Database engine | Document DB layer |
|---------------|--------------|-------------------|-----------------|-------------------|
| SurrealDB *(default)* | `"SurrealDB"` | Dali | SurrealDB | Dali (on SurrealDB.Net) |
| PostgreSQL | `"PostgreSQL"` | Marten | PostgreSQL | Marten (on Npgsql) |
| MSSQL | `"MSSQL"` | Polecat | SQL Server | Polecat (on Microsoft.Data.SqlClient) |

Config values match the user-facing database names because the config
file is user-editable. Internal codenames are used in project packages
and class names.

------------------------------------------------------------------------

# High-Level Flow

Installation: 1. User selects SurrealDB, PostgreSQL, or MSSQL. 2.
Selects embedded or server mode. 3. Selection is persisted to
configuration.

Every application startup: 1. Read configured provider. 2. Register
provider infrastructure. 3. Create provider-specific
`IAeroPersistenceBuilder`. 4. Discover all modules implementing
`IAeroDataModule`. 5. Call `ConfigurePersistence(builder)` on every
module. 6. Register provider-specific service implementations. 7.
Runtime resolves only interfaces through DI.

-----------------------------------------------------------------------

# Setup Wizard UI

The Setup wizard (`Setup.cshtml`) exposes two separate controls for
provider selection:

- **Provider dropdown**: SurrealDB (default/selected), PostgreSQL,
  MSSQL
- **Embedded checkbox**: whether the database runs embedded (in-process)
  or connects to an external server

These are two orthogonal concerns — the user picks the engine, then
decides how to run it. Both are persisted to `appsettings.{environment}.json`
during installation.

## Config Persistence

Following the existing Setup wizard pattern (`DatabaseBootstrapService.PersistAsync`),
the selection is written under a new config section distinct from the
`AeroCms:Bootstrap` state tracking section:

``` json
{
  "AeroCms": {
    "Bootstrap": {
      "DatabaseMode": "Embedded",
      "State": "Running"
    },
    "Persistence": {
      "Provider": "SurrealDB",
      "Embedded": true
    }
  }
}
```

| Key | Type | Values | Purpose |
|-----|------|--------|---------|
| `AeroCms:Persistence:Provider` | string | `"SurrealDB"`, `"PostgreSQL"`, `"MSSQL"` | Which database engine |
| `AeroCms:Persistence:Embedded` | bool | `true`, `false` | Whether the DB runs embedded |

The `AeroCms:Bootstrap` section continues to track operational mode and
setup state. The `AeroCms:Persistence` section tracks technology choices.
They are written in the same `DatabaseBootstrapService.PersistAsync` call
but serve different concerns.

## Relationship Between Embedded Flag and DatabaseMode

The existing `AeroCms:Bootstrap:DatabaseMode` (`"Embedded"` / `"Server"`)
and the new `AeroCms:Persistence:Embedded` (`true` / `false`) carry the
same information but for different consumers:

- `DatabaseMode` is consumed by the existing `InfrastructureConnectionStringResolver`
  during the bootstrapping pipeline to decide connection strings and
  infrastructure provisioning (e.g., spin up an embedded PostgreSQL or
  connect to a remote SurrealDB Cloud instance).
- `Embedded` is consumed during the DI registration phase to select the
  correct service implementations (e.g., embedded vs. server
  `IAeroDocumentStore`).

During the Setup wizard, the checkbox state is written to both keys to
keep them in sync. The two values should never conflict.

## Startup Read

At startup, the provider is read from config (see the Startup section):

``` csharp
var provider = config.GetValue<string>("AeroCms:Persistence:Provider") ?? "SurrealDB";
var embedded = config.GetValue<bool>("AeroCms:Persistence:Embedded");
```

------------------------------------------------------------------------

# Module Responsibilities

Each module owns: - Documents - Indexes - Projections -
Repositories/services - Persistence configuration

Example:

``` csharp
public sealed class PagesModule : AeroWebModule, IAeroDataModule
{
    public void ConfigurePersistence(IAeroPersistenceBuilder db)
    {
        db.Projections.Add<PageDocumentProjection>(ProjectionMode.Inline);

        db.For<PageDocument>()
            .Identity(x => x.Id)
            .Index(x => x.SiteId)
            .UniqueIndex(x => x.SiteId, x => x.Culture, x => x.ParentId, x => x.Slug)
            .SoftDeleted()
            .UseOptimisticConcurrency();
    }
}
```

The module never references provider-specific APIs.

------------------------------------------------------------------------

# IAeroPersistenceBuilder

Expose only provider-neutral operations.

``` csharp
public interface IAeroPersistenceBuilder
{
    IAeroProjectionBuilder Projections { get; }

    IAeroDocumentBuilder<T> For<T>() where T : class;
}
```

`IAeroDocumentBuilder<T>` exposes only provider-neutral document
configuration operations:

-   Identity()
-   Alias()
-   Index()
-   UniqueIndex()
-   SoftDeleted()
-   UseOptimisticConcurrency()

Provider-specific operations (e.g. Marten/PostgreSQL's NgramIndex,
FullTextIndex, Duplicate) are implemented as **extension methods** in
the provider project, not on the abstraction. Modules that use them opt
in to that provider's extensions, but the core interface remains
provider-agnostic.

These represent intent only.

------------------------------------------------------------------------

# Provider Builders

Each provider translates the intent.

``` text
MartenPersistenceBuilder (PostgreSQL)
    -> opts.Schema.For<T>()

PolecatPersistenceBuilder (MSSQL)
    -> Polecat mapping API

DaliPersistenceBuilder (SurrealDB)
    -> Dali mapping API
```

The module code remains identical regardless of provider.

------------------------------------------------------------------------

# Runtime Data Access

Avoid one giant application repository.

Instead use `IAeroDocumentStore` — a thin provider-neutral document store
abstraction. This is the **only** persistence abstraction that module
code (repositories, services) ever references.

``` csharp
public interface IAeroDocumentStore : IDisposable, IAsyncDisposable
{
    Task StoreAsync<T>(T entity) where T : Entity;
    Task StoreAsync<T>(IEnumerable<T> entities) where T : Entity;
    Task<Option<T>> LoadAsync<T>(long id) where T : Entity;
    Task<IReadOnlyList<T>> LoadManyAsync<T>(IEnumerable<long> ids) where T : Entity;
    Task DeleteAsync<T>(T entity) where T : Entity;
    Task DeleteAsync<T>(long id) where T : Entity;
    Task DeleteWhereAsync<T>(Expression<Func<T, bool>> predicate) where T : Entity;
    Task<long> CountAsync<T>(Expression<Func<T, bool>>? predicate = null) where T : Entity;
    Task<bool> AnyAsync<T>(Expression<Func<T, bool>> predicate) where T : Entity;
    IQueryable<T> Query<T>() where T : Entity;
    Task<PagedResult<T>> QueryPagedAsync<T>(...);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

Key design decisions:
- Returns `Option<T>` (not `T?`) — aligns with Railway Oriented Programming
  pattern used throughout the codebase (Result/Option/Bind/Map from Aero.Core)
- `IQueryable<T>` is the standard .NET query abstraction. Providers
  implement `IQueryProvider`; unsupported expressions throw
  `NotSupportedException` at runtime.
- `PagedResult<T>` is a record type (not a tuple) for future extensibility.
- Does NOT expose any provider-specific session type (no `IDocumentSession`,
  no generic parameter).

Nothing provider-specific should leak from this interface.

Do NOT expose Marten's IDocumentSession.

## Transaction Capability

Not all document DB providers support multi-document transactions. Use an
optional capability interface instead of forcing all providers to implement
`IAsyncUnitOfWork` transaction methods that are no-ops:

``` csharp
/// <summary>
/// Optional capability. If a provider supports multi-document transactions,
/// its IAeroDocumentStore implementation also implements this interface.
/// </summary>
public interface ISupportsTransactions
{
    Task StartTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
```

Code that needs transactions:

``` csharp
await using var store = _storeFactory.Create();
if (store is ISupportsTransactions tx)
    await tx.StartTransactionAsync();

await store.StoreAsync(doc1);
await store.StoreAsync(doc2);
await store.SaveChangesAsync();
```

Simple single-entity operations never touch transaction APIs.

------------------------------------------------------------------------

# Orleans Grain Session Management

Orleans grains are long-lived activations and **cannot inject scoped DI
services** (like `IDocumentSession`). They must create and dispose their
own sessions per operation.

## IAeroDocumentStoreFactory

Grains inject a singleton factory instead:

``` csharp
/// <summary>
/// Singleton factory for creating short-lived IAeroDocumentStore instances.
/// This is the primary injection target for Orleans grains.
/// Callers MUST dispose the returned store (implementing IAsyncDisposable).
/// </summary>
public interface IAeroDocumentStoreFactory
{
    IAeroDocumentStore Create();
}
```

## Grain Usage Pattern

``` csharp
// Before (Marten-coupled):
public sealed class AeroPostGrain(
    ILogger<AeroActor> log,
    IDocumentStore store,
    IServiceProvider services) : Grain, IAeroPostGrain
{
    public async Task<PostViewModel> LoadAsync(long id, CancellationToken ct)
    {
        await using var session = store.LightweightSession();
        var service = new PostContentService(session, ...);
        return await service.LoadAsync(id, ct);
    }
}

// After (provider-agnostic):
public sealed class AeroPostGrain(
    ILogger<AeroActor> log,
    IAeroDocumentStoreFactory storeFactory,
    IServiceProvider services) : Grain, IAeroPostGrain
{
    public async Task<PostViewModel> LoadAsync(long id, CancellationToken ct)
    {
        await using var store = storeFactory.Create();
        var service = new PostContentService(store, ...);
        return await service.LoadAsync(id, ct);
    }
}
```

## Session Per Operation

- **Always** create a new store per grain method call
- Document DB sessions track changes; holding one for a grain's lifetime
  (potentially days) would leak memory and connections
- A failed transaction would poison all subsequent operations on a
  long-held session
- The existing codebase already follows this pattern correctly with
  `IDocumentStore.LightweightSession()`

## Disposal Safety

The same risk exists in the current codebase (~87 `_store.LightweightSession()`
call sites). Mitigations:
- Use `await using` (same as current pattern)
- Add `.editorconfig` rule for `IAsyncDisposable` usage (CA2007)
- Consider a delegate-based wrapper for critical paths:
  `storeFactory.ExecuteAsync(db => db.LoadAsync<T>(id))`

## IQueryable<T> in Orleans

Safe. The `IQueryable<T>` is never serialized across silo boundaries.
The query is materialized (`.ToListAsync()`, `.AnyAsync()`, etc.) inside
the grain method body, and only the materialized results cross the silo
boundary.

------------------------------------------------------------------------

# Provider-Specific Services

Some services may require provider-specific implementations.

Example:

``` text
IPageContentService

    MartenPageContentService (PostgreSQL)

    PolecatPageContentService (MSSQL)

    DaliPageContentService (SurrealDB)
```

**Module services** (used by most code) depend only on `IAeroDocumentStore`:

``` csharp
public sealed class PostContentService(
    IAeroDocumentStore store,
    ISiteContext siteContext,
    IMessageBus? bus = null) : IPostContentService
{
    // All persistence uses store.Query<T>(), store.LoadAsync<T>(id), etc.
}
```

**Provider-specific services** (rare, for provider-specific features)
may inject the provider-specific session directly — but they are
registered only when that provider is selected, and their interface is
still provider-agnostic.

Consumers inject only:

``` csharp
IPageContentService
```

------------------------------------------------------------------------

# Vertical Slice Ownership

Every module owns its own repositories/services.

Example:

``` text
Pages
    IPageRepository
    PageRepository
    IPageContentService

Media
    IMediaRepository
    MediaRepository

Navigation
    INavigationRepository
```

Repositories depend only on:

``` csharp
IAeroDocumentStore
```

------------------------------------------------------------------------

# DI Registration

Startup chooses one provider. Config values use the user-facing database
names. Internally, each maps to a provider package:

``` csharp
switch (provider)
{
    case "SurrealDB":
        Register Dali (SurrealDB) infrastructure
        services.AddAeroCmsDaliDb();
        break;

    case "PostgreSQL":
        Register Marten (PostgreSQL) infrastructure
        services.AddAeroCmsMartenDb();
        break;

    case "MSSQL":
        Register Polecat (MSSQL) infrastructure
        services.AddAeroCmsPolecatDb();
        break;
}
```

Each `AddAeroCms*Db()` extension registers:

| Service | Lifetime | Consumers |
|---------|----------|-----------|
| `IAeroDocumentStore` | Scoped | HTTP controllers, background jobs |
| `IAeroDocumentStoreFactory` | Singleton | Orleans grains |

The factory is registered via a delegate to avoid unnecessary
abstraction:

``` csharp
// PostgreSQL / Marten provider registration:
services.AddSingleton<IAeroDocumentStoreFactory>(sp =>
    new MartenDocumentStoreFactory(sp.GetRequiredService<IDocumentStore>()));
```

------------------------------------------------------------------------

# Startup

``` csharp
// 1. Read configured provider
var provider = config.GetValue<string>("AeroCms:Persistence:Provider") ?? "SurrealDB";
var embedded = config.GetValue<bool>("AeroCms:Persistence:Embedded");

// 2. Register provider infrastructure + DI
switch (provider)
{
    case "SurrealDB":
        services.ConfigureDaliDb(config, env, connString);
        services.AddAeroCmsDaliDb();
        break;
    case "PostgreSQL":
        services.ConfigureMartenDb(config, env, connString, embedded);
        services.AddAeroCmsMartenDb();
        break;
    case "MSSQL":
        services.ConfigurePolecatDb(config, env, connString);
        services.AddAeroCmsPolecatDb();
        break;
}

// 3. Create provider-specific persistence builder
IAeroPersistenceBuilder builder = provider switch
{
    "SurrealDB" => new DaliPersistenceBuilder(...),
    "PostgreSQL" => new MartenPersistenceBuilder(storeOptions),
    "MSSQL" => new PolecatPersistenceBuilder(...),
};

// 4. Let all IAeroDataModule modules configure their schema
foreach (var module in modules.OfType<IAeroDataModule>())
{
    module.ConfigurePersistence(builder);
}

// 5. Apply the built configuration
//    Done by the provider infrastructure registration step.
```

Modules never inspect which provider is selected.

------------------------------------------------------------------------

# Recommended Project Layout

``` text
Aero.Cms.Core.Abstractions                  (no provider dependencies)
    IAeroDocumentStore
    IAeroDocumentStoreFactory
    ISupportsTransactions
    PagedResult<T>
    IAeroPersistenceBuilder
    IAeroDocumentBuilder<T>
    IAeroDataModule

Aero.Cms.Core                               (legacy — to be deprecated)
    [Obsolete] IAeroCmsDb
    [Obsolete] AeroCmsDB

Aero.Cms.Db.Dali                            (SurrealDB document DB)
    DaliDocumentStore : IAeroDocumentStore
    DaliDocumentStoreFactory : IAeroDocumentStoreFactory
    DaliPersistenceBuilder : IAeroPersistenceBuilder
    DaliDocumentBuilder<T> : IAeroDocumentBuilder<T>
    Dali LINQ provider (Expression → SurrealQL)
    ServiceCollectionExtensions (AddAeroCmsDaliDb)

Aero.Cms.Db.Marten                          (PostgreSQL document DB)
    MartenDocumentStore : IAeroDocumentStore
    MartenDocumentStoreFactory : IAeroDocumentStoreFactory
    MartenPersistenceBuilder : IAeroPersistenceBuilder
    MartenDocumentBuilder<T> : IAeroDocumentBuilder<T>
    Marten-specific extensions (NgramIndex, FullTextIndex, etc.)
    ServiceCollectionExtensions (AddAeroCmsMartenDb)

Aero.Cms.Db.Polecat                         (MSSQL document DB)
    PolecatDocumentStore : IAeroDocumentStore
    PolecatDocumentStoreFactory : IAeroDocumentStoreFactory
    PolecatPersistenceBuilder : IAeroPersistenceBuilder
    ServiceCollectionExtensions (AddAeroCmsPolecatDb)

Aero.Cms.Modules.Pages
    PagesModule : AeroWebModule, IAeroDataModule
    IPageRepository
    IPageContentService

Aero.Cms.Modules.Media
    ...

Aero.Cms.Modules.Navigation
    ...
```

------------------------------------------------------------------------

# Migration Strategy

## Phases

### Phase 1: Core Abstractions (zero impact)
- Create `IAeroDocumentStore`, `IAeroDocumentStoreFactory`,
  `ISupportsTransactions`, `PagedResult<T>` in
  `Aero.Cms.Core.Abstractions`
- Create `IAeroDataModule`, `IAeroPersistenceBuilder`,
  `IAeroDocumentBuilder<T>` in `Aero.Cms.Core.Abstractions`
- No existing code changes needed

### Phase 2: Provider Implementations (backward-compatible)
- Implement `SurrealDocumentStore`, `SurrealPersistenceBuilder`,
  `SurrealDocumentBuilder<T>` for the SurrealDB provider
- Implement `MartenDocumentStore`, `MartenPersistenceBuilder`,
  `MartenDocumentBuilder<T>` for the PostgreSQL provider
- Implement `PolecatDocumentStore`, `PolecatPersistenceBuilder` for the
  MSSQL provider
- Register `AddAeroCmsSurrealDb()`, `AddAeroCmsMartenDb()`,
  `AddAeroCmsPolecatDb()` in startup
- Old `IAeroCmsDb`/`AeroCmsDB` still works alongside new abstractions

### Phase 3: One Module Migration (prove the pattern)
- Migrate PagesModule from `IConfigureMarten` to
  `IAeroDataModule.ConfigurePersistence()`
- Refactor `PostContentService` from `IDocumentSession` to
  `IAeroDocumentStore`
- Refactor `AeroPostGrain` from `IDocumentStore` to
  `IAeroDocumentStoreFactory`
- Verify build + tests

### Phase 4: Remaining Modules
- Migrate Navigation, Media, Posts, Content (same pattern)
- Each module independently

### Phase 5: Cleanup
- Mark old `IAeroCmsDb`/`AeroCmsDB` as `[Obsolete]`
- Remove Marten package reference from `Aero.Cms.Core`
- Remove `IConfigureMarten` implementations from modules

## Coexistence Strategy

During migration, both persistence paths work simultaneously:

| Component | Old Path | New Path |
|-----------|----------|----------|
| Module interface | `IConfigureMarten` | `IAeroDataModule` |
| Session abstraction | `IAeroCmsDb` (Marten-coupled) | `IAeroDocumentStore` (provider-neutral) |
| Grain session | `IDocumentStore.OpenSession()` | `IAeroDocumentStoreFactory.Create()` |
| Module services | `IDocumentSession` | `IAeroDocumentStore` |

Migration is module-by-module. Unmigrated modules continue to use the
old path without interference.

## Existing Code in Aero Submodule

| Component | Action |
|-----------|--------|
| `IAeroDb` / `AeroDb` | Keep. Deprecate after multi-provider is stable. |
| `IGenericMartenRepository<T>` | Keep for backward compat. New code uses `IAeroDocumentStore` directly. |
| `IAsyncUnitOfWork` | Superseded by `IAeroDocumentStore` + `ISupportsTransactions`. |

------------------------------------------------------------------------

# Implementation Plan

SurrealDB is the first new provider to implement (Marten/PostgreSQL is
already implemented; Polecat/MSSQL is last).

## Provider Implementation Order

```
Dali (SurrealDB) →  Phase 1-2 (first — new provider, proves abstractions)
Marten (PostgreSQL) → Already implemented (refactored to new abstractions)
Polecat (MSSQL)  →  Phase 7 (last — only after abstractions are stable)
```

## Technical Equivalents: SurrealDB vs PostgreSQL/Marten

| Feature | PostgreSQL / Marten | SurrealDB / Dali |
|---------|-------------------|-------------------|
| **Embedded mode** | MysticMind.PostgresEmbed (spawns PG subprocess) | Spawn `surreal start` subprocess; file-backed via `surrealkv://` or ephemeral via `memory` |
| **Provisioning** | `NpgsqlCommand`: `CREATE DATABASE`, `CREATE ROLE` | `SurrealDbClient.QueryAsync()`: `DEFINE NAMESPACE`, `DEFINE DATABASE`, `DEFINE LOGIN` |
| **Connection URL** | PostgreSQL conn string (`Host=localhost;Port=5433`) | WebSocket URL (`ws://localhost:8000/rpc`) |
| **Schema DDL** | Marten `StoreOptions` → PostgreSQL DDL | Dali mapping API → SurrealQL DDL |
| **Test cleanup** | Marten `Advanced.Clean.CompletelyRemoveAllAsync()` | SurrealQL `REMOVE TABLE` or restart with `memory` |
| **Identity** | Marten `UserStore<AeroUser>` wrapping `IDocumentSession` | Provider-agnostic `UserStore` backed by `IAeroDocumentStore` |
| **IQueryable<T>** | Marten's built-in LINQ provider (Expression → PostgreSQL) | Dali LINQ provider (Expression → SurrealQL) |
| **Cluster mgmt** | Citus: `master_add_node` via `NpgsqlCommand` | SurrealDB clustering via `--cluster` mode (separate code path) |

## Raw SQL Patterns Needing Change

The following files use raw PostgreSQL-specific SQL and need provider-aware
branching or provider-agnostic rewrites:

| File | Current Pattern | Dali/SurrealDB Equivalent |
|------|----------------|--------------------------|
| `AeroEmbeddedDbService.cs` | `NpgsqlCommand` for embedded PG provisioning | `AeroEmbeddedDbService.cs`: spawn `surreal start` subprocess, health-check via SDK |
| `manager/app.cs` | `master_add_node` / `master_remove_node` for Citus | SurrealDB clustering API (separate); gate behind PostgreSQL check |
| `PlaywrightE2EFixture.cs` | `NpgsqlCommand` for test DB reset | SDK: `REMOVE TABLE` or restart embedded with `memory` |
| Test files with `PgServer` | `NpgsqlCommand.ExecuteNonQueryAsync` for test DB setup | `AeroDocumentStoreFixture` that creates provider-agnostic stores |

## Key Risks

### 1. Identity UserStore for Dali/SurrealDB

ASP.NET Core Identity requires `IUserStore<TUser>` and `IRoleStore<TRole>`.
The current `UserStore<AeroUser, AeroRole>` from `Aero.Marten.Identity`
wraps `IDocumentSession`.

**Mitigation**: Build a provider-agnostic `DocumentStoreUserStore` backed
by `IAeroDocumentStore` methods (`LoadAsync`, `StoreAsync`, `Query<T>`).
This works for any provider and avoids Dali-specific Identity code.

## Implementation Phases

### Phase 0: Core Abstractions (zero impact, ~1 week)

- Create `IAeroDocumentStore`, `IAeroDocumentStoreFactory`,
  `ISupportsTransactions`, `PagedResult<T>` in
  `Aero.Cms.Core.Abstractions`
- Create `IAeroDataModule`, `IAeroPersistenceBuilder`,
  `IAeroDocumentBuilder<T>`, `IAeroProjectionBuilder` in
  `Aero.Cms.Core.Abstractions`
- No existing code changes needed

### Phase 1: Dali (SurrealDB) Infrastructure (~2-3 weeks)

1. **DaliOptions** — options class with SurrealDB endpoint, Namespace,
   Database, credentials; connection URL builder (`ws://{Host}:{Port}/rpc`)
2. **AeroEmbeddedDbService branching** — when provider is SurrealDB,
   spawn `surreal start` subprocess instead of PG; perform health checks,
   provision namespace/database via `SurrealDbClient`
3. **InfrastructureConnectionStringResolver branching** — reads
   `AeroCms:Persistence:Provider`, branches on SurrealDB to return
   WebSocket URL instead of PG connection string
4. **DI registration** — `AeroAppServerExtensions` switches on provider;
   SurrealDB case calls `services.ConfigureDaliDb(config, env, url)`
   and `services.AddAeroCmsDaliDb()`
5. **ServiceCollectionExtensions** — registers `IAeroDocumentStore`
   (scoped) and `IAeroDocumentStoreFactory` (singleton)

### Phase 2: DaliDocumentStore (~2-3 weeks)

Dali provides a LINQ provider and document session API on top of
`SurrealDB.Net`, analogous to Marten on Npgsql. The `DaliDocumentStore`
wraps a Dali session and translates `IAeroDocumentStore` calls to Dali's API:

| `IAeroDocumentStore` method | Dali implementation |
|-----------------------------|---------------------|
| `StoreAsync<T>(T)` | `_session.Store(entity)` + `_session.SaveChangesAsync()` |
| `StoreAsync<T>(IEnumerable<T>)` | Loop `_session.Store()` + single `SaveChangesAsync()` |
| `LoadAsync<T>(long id)` | `_session.LoadAsync<T>(id)` → `Option<T>` |
| `LoadManyAsync<T>(IEnumerable<long>)` | `_session.LoadManyAsync<T>(ids)` |
| `DeleteAsync<T>(entity)` / `DeleteAsync<T>(id)` | `_session.Delete(entity/id)` + `SaveChangesAsync()` |
| `DeleteWhereAsync<T>(predicate)` | Dali LINQ: `_session.Query<T>().Where(predicate).Delete()` |
| `CountAsync<T>(predicate)` | Dali LINQ: `_session.Query<T>().CountAsync()` |
| `AnyAsync<T>(predicate)` | Dali LINQ: `_session.Query<T>().AnyAsync()` |
| `Query<T>()` | `_session.Query<T>()` (Dali LINQ provider → SurrealQL) |
| `QueryPagedAsync<T>(...)` | Dali LINQ: `Skip()` + `Take()` + `ToListAsync()` |
| `SaveChangesAsync()` | `_session.SaveChangesAsync()` |

**DaliPersistenceBuilder**: Accepts schema state from
`IAeroDataModule` modules, configures Dali document mappings (indexes,
identity, soft delete, optimistic concurrency).

**DaliDocumentBuilder**: Maps provider-neutral operations to Dali
configuration API:

| Operation | Dali Configuration |
|-----------|-------------------|
| `Identity(x => x.Id)` | Dali identity mapping on `Id` property |
| `Index(x => field)` | Dali index definition on field |
| `UniqueIndex(a, b, c)` | Dali unique composite index |
| `SoftDeleted()` | Dali soft-delete filter configuration |
| `UseOptimisticConcurrency()` | Dali version field configuration |

### Phase 3: Module Migration (Pages first, ~3 weeks)

Migrate one module at a time. Each module:

1. Adds `IAeroDataModule` alongside existing `IConfigureMarten`
2. Implements `ConfigurePersistence(IAeroPersistenceBuilder)`
3. Migrates services from `IDocumentSession` → `IAeroDocumentStore`
4. Migrates grains from `IDocumentStore` → `IAeroDocumentStoreFactory`

**Module order**: Pages → Navigation → Media → Footer → Tenant →
Sites → Modules → Commerce → Docs → Blocks → Content

### Phase 4: Setup & Bootstrap Changes (~1 week)

1. **Setup wizard** — provider dropdown includes SurrealDB (default),
   PostgreSQL, MSSQL
2. **DatabaseBootstrapModel** — add `string Provider` field
3. **DatabaseBootstrapService** — write `AeroCms:Persistence:Provider`
   during setup persistence
4. **ServerTargetSetupExecutor** — branch on provider: SurrealDB path
   configures schema via `DaliPersistenceBuilder` instead of
   `DocumentStore.For(...)`

### Phase 5: Test Infrastructure (~1 week)

1. **Dali test fixture** — manages embedded `surreal start` subprocess
   lifecycle for tests; creates `DaliDocumentStore`
2. **AeroDocumentStoreFixture** — provider-agnostic test fixture
   (creates either Marten or Dali store)
3. **Migrate test files** — rename Marten-prefixed tests to
   provider-agnostic names; replace `IDocumentSession` mocks with
   `IAeroDocumentStore` substitutes

### Phase 6: Manager App (low effort)

- Citus cluster management logic (`master_add_node`) remains
  PostgreSQL-specific; gated behind provider check
- SurrealDB clustering via `--cluster` mode added separately if needed

### Phase 7: Aero Submodule & Source Generators (~1 week)

1. **Source generator** — detect `IAeroDataModule` (add
   `IsAeroDataModule` to `ModuleDescriptor`)
2. **AeroModuleBase** — add `IAeroDataModule` implementation
   (default empty)
3. **ModuleDescriptor** — thread `IsAeroDataModule` through to
   runtime discovery

## Parallelism

Phases 4, 5, 6, and 7 can overlap with Phase 3 (module migration).
The critical path is: Phase 0 → Phase 1 → Phase 2 → Phase 3.

```
Phase 0: Core Abstractions         ████████
Phase 1: SurrealDB Infrastructure  ████████████████
Phase 2: SurrealDocumentStore      ████████████████
Phase 3: Module Migration          ████████████████████
Phase 4: Setup & Bootstrap         ████████████        (overlaps P3)
Phase 5: Test Infrastructure       ████████████        (overlaps P3)
Phase 6: Manager App               ██████              (overlaps P3)
Phase 7: Aero Submodule            ████████████        (overlaps P3)
```

-----------------------------------------------------------------------

# Guiding Principles

- Modules own schema.
- Providers own implementation.
- Startup selects the provider once.
- Modules express persistence intent only.
- Runtime depends only on abstractions.
- No provider-specific APIs should leak into module code.
- Keep `IAeroDocumentStore` as a thin unit-of-work/session abstraction, not a
  god repository.
- `IQueryable<T>` is the query abstraction — but document its deferred-execution
  disposal coupling.
- `Option<T>` return values for ROP-compatible null handling.
- Provider-specific persistence builder methods are extension methods in the
  provider project, not on the abstraction interface.
