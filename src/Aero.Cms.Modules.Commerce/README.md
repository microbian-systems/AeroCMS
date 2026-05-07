# Aero.Cms.Modules.Commerce

Commerce module for Aero CMS — eCommerce reference application patterns into a single vertical-slice module.

## Run Migration
> dotnet ef migrations add MigrationName --project src/Aero.Cms.Modules.Commerce -s src/Aero.Cms.Web

## Architecture

```
Aero.Cms.Modules.Commerce          Server-side RCL (Minimal APIs, persistence, handlers)
Aero.Cms.Modules.Commerce.Client   WASM-safe RCL (admin UI pages, typed HTTP clients)
```

### Vertical Slices

| Slice | Persistence | Description |
|-------|------------|-------------|
| **Catalog** | MartenDB | Products, categories, full-text search |
| **Basket** | MartenDB | Shopping cart per customer |
| **Orders** | EF Core | Order aggregate with state machine, relational integrity |
| **Payments** | EF Core | Payment processing simulation |
| **Jobs** | TickerQ | Background processing (grace period expiry) |

### UI Surfaces

- **Public** (`Pages/Public/`): Server-rendered `.cshtml` pages (catalog browsing, checkout, order history)
- **Admin** (`.Client/Pages/Admin/`): WASM-based `.razor` components for the Aero Manager

### Key Patterns

- **Messaging**: WolverineFx (replaces RabbitMQ + MediatR from eShop)
- **Outbox**: Wolverine's built-in transactional outbox (replaces IntegrationEventLogEF)
- **Id generation**: Snowflake (`long`) for all entity IDs
- **Error handling**: Railway-Oriented Programming (`Result<T>`, `Option<T>`, `Map`, `Bind`)
- **Validation**: FluentValidation
- **State machine**: `OrderStateMachine` with railway-based transition engine

### eShop Mapping

| eShop Service | This Module |
|--------------|-------------|
| Basket.API | `Basket/` vertical slice |
| Catalog.API | `Catalog/` vertical slice |
| Ordering.API + Domain + Infrastructure | `Orders/` vertical slice |
| PaymentProcessor | `Payments/` vertical slice |
| OrderProcessor | `Jobs/GracePeriodJob` (TickerQ) |
| EventBus + EventBusRabbitMQ | WolverineFx |
| IntegrationEventLogEF | Wolverine outbox |

## Dependencies

- MartenDB (PostgreSQL document store) — Catalog, Basket
- EntityFramework Core + Npgsql — Orders, Payments
- WolverineFx — messaging and transactional outbox
- TickerQ — background jobs
- FluentValidation — input validation

## API Endpoints

All under `/api/commerce/` prefix, registered as Minimal APIs in `CommerceModule.cs`.

## Tests

- TUnit for unit tests
- Alba for integration tests (with embedded Postgres)
- NSubstitute / FakeItEasy for test doubles
- Bogus for test data
