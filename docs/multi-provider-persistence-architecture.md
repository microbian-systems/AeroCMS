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

`IAeroDocumentBuilder<T>` should expose things such as:

-   Identity()
-   Alias()
-   Index()
-   UniqueIndex()
-   Duplicate()
-   NgramIndex()
-   SoftDeleted()
-   UseOptimisticConcurrency()

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

# Startup

Pseudo-code:

``` csharp
IAeroPersistenceBuilder builder =
    provider switch
    {
        Marten => new MartenPersistenceBuilder(...),
        Polecat => new PolecatPersistenceBuilder(...),
        Dali => new DaliPersistenceBuilder(...),
    };

foreach (var module in modules.OfType<IAeroDataModule>())
{
    module.ConfigurePersistence(builder);
}
```

Modules never inspect which provider is selected.

------------------------------------------------------------------------

# Runtime Data Access

Avoid one giant application repository.

Instead use a thin provider-neutral session abstraction.

``` csharp
IAeroCmsDb
```

Responsibilities:

-   Query`<T>`{=html}()
-   Load`<T>`{=html}()
-   Store`<T>`{=html}()
-   Delete`<T>`{=html}()
-   SaveChangesAsync()

Nothing provider-specific should leak from this interface.

Do NOT expose Marten's IDocumentSession.

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
IAeroCmsDb
```

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

Each implementation injects its provider-specific session.

Examples:

``` text
Marten
    IDocumentSession

Polecat
    IPolecatSession

Dali
    IDaliSession
```

Consumers inject only:

``` csharp
IPageContentService
```

------------------------------------------------------------------------

# DI Registration

Startup chooses one provider.

Example:

``` csharp
switch(provider)
{
    case Marten:
        Register Marten infrastructure
        Register Marten services
        break;

    case Polecat:
        Register Polecat infrastructure
        Register Polecat services
        break;

    case Dali:
        Register Dali infrastructure
        Register Dali services
        break;
}
```

------------------------------------------------------------------------

# Recommended Project Layout

``` text
Aero.Cms.Core
    IAeroCmsDb
    IAeroPersistenceBuilder
    IAeroDocumentBuilder
    IAeroDataModule

Aero.Cms.Data.Marten
    MartenPersistenceBuilder
    MartenAeroCmsDb

Aero.Cms.Data.Polecat
    PolecatPersistenceBuilder
    PolecatAeroCmsDb

Aero.Cms.Data.Dali
    DaliPersistenceBuilder
    DaliAeroCmsDb

Aero.Cms.Modules.Pages
    PagesModule
    IPageRepository
    IPageContentService

Aero.Cms.Modules.Media
...

Aero.Cms.Modules.Navigation
...
```

------------------------------------------------------------------------

# Guiding Principles

-   Modules own schema.
-   Providers own implementation.
-   Startup selects the provider once.
-   Modules express persistence intent only.
-   Runtime depends only on abstractions.
-   No provider-specific APIs should leak into module code.
-   Keep `IAeroCmsDb` as a thin unit-of-work/session abstraction, not a
    god repository.
