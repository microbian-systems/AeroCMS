# Aero CMS Multi-Provider Persistence Architecture

## Goal

Allow Aero CMS to support multiple document database providers selected
by the user at installation time:

-   Marten
-   Polecat
-   Dali

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

# High-Level Flow

Installation: 1. User selects Marten, Polecat, or Dali. 2. Selection is
persisted to configuration.

Every application startup: 1. Read configured provider. 2. Register
provider infrastructure. 3. Create provider-specific
`IAeroPersistenceBuilder`. 4. Discover all modules implementing
`IAeroDataModule`. 5. Call `ConfigurePersistence(builder)` on every
module. 6. Register provider-specific service implementations. 7.
Runtime resolves only interfaces through DI.

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

The module never references Marten, Polecat, or Dali APIs.

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

Provider-specific operations (e.g. Marten's NgramIndex, FullTextIndex,
Duplicate) are implemented as **extension methods** in the provider
project, not on the abstraction. Modules that use them opt in to that
provider's extensions, but the core interface remains provider-agnostic.

These represent intent only.

------------------------------------------------------------------------

# Provider Builders

Each provider translates the intent.

``` text
MartenPersistenceBuilder
    -> opts.Schema.For<T>()

PolecatPersistenceBuilder
    -> Polecat mapping API

DaliPersistenceBuilder
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

    MartenPageContentService

    PolecatPageContentService

    DaliPageContentService
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

Startup chooses one provider.

``` csharp
switch (provider)
{
    case Marten:
        Register Marten infrastructure (IDocumentStore, etc.)
        services.AddAeroCmsMartenDb();
        break;

    case Polecat:
        Register Polecat infrastructure
        services.AddAeroCmsPolecatDb();
        break;

    case Dali:
        Register Dali infrastructure
        services.AddAeroCmsDaliDb();
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
// Marten provider registration:
services.AddSingleton<IAeroDocumentStoreFactory>(sp =>
    new MartenDocumentStoreFactory(sp.GetRequiredService<IDocumentStore>()));
```

------------------------------------------------------------------------

# Startup

``` csharp
// 1. Read configured provider
var provider = config.GetValue<string>("AeroCms:Persistence:Provider") ?? "Marten";

// 2. Register provider infrastructure + DI
switch (provider)
{
    case "Marten":
        services.ConfigureMartenDb(config, env, connString);
        services.AddAeroCmsMartenDb();
        break;
    // Polecat, Dali ...
}

// 3. Create provider-specific persistence builder
IAeroPersistenceBuilder builder = provider switch
{
    "Marten" => new MartenPersistenceBuilder(storeOptions),
    "Polecat" => new PolecatPersistenceBuilder(...),
    "Dali" => new DaliPersistenceBuilder(...),
};

// 4. Let all IAeroDataModule modules configure their schema
foreach (var module in modules.OfType<IAeroDataModule>())
{
    module.ConfigurePersistence(builder);
}

// 5. Apply the built configuration (Marten: storeOptions → document store)
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

Aero.Cms.Db.Marten                          (Marten implementation)
    MartenDocumentStore : IAeroDocumentStore
    MartenDocumentStoreFactory : IAeroDocumentStoreFactory
    MartenPersistenceBuilder : IAeroPersistenceBuilder
    MartenDocumentBuilder<T> : IAeroDocumentBuilder<T>
    Marten-specific extensions (NgramIndex, FullTextIndex, etc.)
    ServiceCollectionExtensions (AddAeroCmsMartenDb)

Aero.Cms.Db.Polecat                         (Polecat implementation)
    PolecatDocumentStore : IAeroDocumentStore
    PolecatDocumentStoreFactory : IAeroDocumentStoreFactory
    PolecatPersistenceBuilder : IAeroPersistenceBuilder
    ServiceCollectionExtensions (AddAeroCmsPolecatDb)

Aero.Cms.Db.Dali                            (Dali implementation)
    DaliDocumentStore : IAeroDocumentStore
    DaliDocumentStoreFactory : IAeroDocumentStoreFactory
    DaliPersistenceBuilder : IAeroPersistenceBuilder
    ServiceCollectionExtensions (AddAeroCmsDaliDb)

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

### Phase 2: Marten Provider (zero impact)
- Implement `MartenDocumentStore`, `MartenDocumentStoreFactory`,
  `MartenPersistenceBuilder`, `MartenDocumentBuilder<T>`
- Register `AddAeroCmsMartenDb()` in startup
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
