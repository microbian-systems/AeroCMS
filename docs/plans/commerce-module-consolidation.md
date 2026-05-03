# Commerce Module Consolidation Plan

> eShop Reference Application → Aero.Cms.Modules.Commerce vertical-slice module
> Date: 2026-05-02
> Status: Approved — ready for Phase 1 implementation

---

## 1. Objective

Consolidate the Microsoft eShop reference application (event-driven microservices with RabbitMQ, MediatR, EF Core, 6 services) into a single `Aero.Cms.Modules.Commerce` vertical-slice module within the AeroCMS platform. Replace RabbitMQ/MediatR with WolverineFx + MartenDB. Keep EF Core for the Orders (relational) data layer.

---

## 2. Key Architectural Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Module granularity | Single module: `Aero.Cms.Modules.Commerce` | Vertical slices as sub-namespaces. Matches AeroCMS modular philosophy. |
| Persistence | Hybrid: MartenDB (Catalog, Basket) + EF Core (Orders, Payments) | Products/basket are document-shaped; orders need relational integrity. |
| Content types | Use for Catalog products | L everages AeroCMS content type system for product catalog. |
| Handler pattern | Wolverine natively (`[WolverineHandler]` + `IWolverineHandler`) | No MediatR wrapper. AeroCMS source generators handle discovery. |
| Grace period | TickerQ cron-style job | Replaces eShop's polling-based OrderProcessor. |
| Outbox | Wolverine's built-in transactional outbox | Replaces eShop's IntegrationEventLogEF. |
| IDs | Snowflake `long` via `Snowflake.NewId()` | `IEntity<long>` convention across all entities. |
| Validation | FluentValidation | AeroCMS standard. |
| Error handling | Railway-Oriented Programming (`Result<T>`, `Option<T>`, `Map`, `Bind`) | Aero.Core.Railway — already in the codebase. |
| API style | Minimal APIs with Scalar | AeroCMS standard (no MVC controllers). |
| Real-time | Aero.SignalR | For order status notifications to webapp. |

---

## 3. Service Mapping

| eShop Service | AeroCMS Target | Notes |
|--------------|----------------|-------|
| **Basket.API** (gRPC + Redis) | Commerce/Cart vertical slice | Minimal APIs replacing gRPC. Marten documents replacing Redis. Garnet available for secondary cache. |
| **Catalog.API** (HTTP + pgvector) | Commerce/Catalog vertical slice | Marten documents for products. Content types integration. **pgvector deferred to Phase 5** — full-text search indexes on ProductDocument for initial pass. |
| **Ordering.API** + **Ordering.Domain** + **Ordering.Infrastructure** | Commerce/Orders vertical slice | EF Core for relational integrity. Order aggregate state machine preserved. Wolverine handlers replace MediatR. |
| **OrderProcessor** (polling) | Commerce/Jobs — TickerQ | Cron-style job for grace period expiry. |
| **PaymentProcessor** (simulated) | Commerce/Payments vertical slice | Wolverine handler. |
| **EventBus + EventBusRabbitMQ** | WolverineFx (already configured) | 1:1 replacement. Wolverine handles outbox natively. |
| **IntegrationEventLogEF** | Wolverine outbox | Built-in. No separate table needed. |
| **Identity.API** (Duende) | Aero.Cms.Modules.Identity | Already exists — no migration needed. |
| **Webhooks.API** | Aero.Cms.Modules.Export / separate | Already exists in AeroCMS. |
| **ServiceDefaults** | Aero.Cms.ServiceDefaults | Already exists with OTel, health checks, resilience. |
| **WebApp** (Blazor) | Existing Aero.Cms.Web | HTMX.NET + Radzen Blazor for frontend. |
| **ClientApp** (MAUI) | Existing HybridApp | Already exists in AeroCMS. |

---

## 4. Proposed Directory Structure

```
src/Aero.Cms.Modules.Commerce/
├── CommerceModule.cs                    # [Module("Commerce")], AeroModuleBase, IConfigureMarten
├── Aero.Cms.Modules.Commerce.csproj
├── GlobalUsings.cs
│
├── Catalog/                             # Vertical slice: Product Catalog
│   ├── Models/
│   │   ├── ProductDocument.cs           # IEntity<long>, Marten document
│   │   └── ProductCategory.cs
│   ├── Services/
│   │   ├── IProductService.cs
│   │   └── ProductService.cs            # IGenericMartenRepository<ProductDocument>
│   ├── Validation/
│   │   └── ProductValidator.cs          # FluentValidation
│   ├── Handlers/
│   │   ├── ProductHandlers.cs           # [WolverineHandler] : IWolverineHandler
│   │   └── StockValidationHandler.cs
│   ├── Events/
│   │   ├── ProductCreated.cs
│   │   ├── ProductPriceChanged.cs
│   │   └── OrderStockConfirmed.cs
│   └── Api/
│       └── CatalogEndpoints.cs          # Minimal API endpoints
│
├── Basket/                              # Vertical slice: Shopping Cart
│   ├── Models/
│   │   ├── BasketDocument.cs            # IEntity<long>, Marten document
│   │   └── BasketItem.cs
│   ├── Services/
│   │   ├── IBasketService.cs
│   │   └── BasketService.cs
│   ├── Validation/
│   │   └── BasketItemValidator.cs
│   ├── Handlers/
│   │   ├── AddToBasketHandler.cs
│   │   ├── RemoveFromBasketHandler.cs
│   │   └── ClearBasketOnOrderHandler.cs
│   ├── Events/
│   │   └── ItemAddedToBasket.cs
│   └── Api/
│       └── BasketEndpoints.cs
│
├── Orders/                              # Vertical slice: Order Management
│   ├── Domain/
│   │   ├── OrderEntity.cs              # IEntity<long>, EF Core, aggregate root
│   │   ├── OrderItem.cs                 # IEntity<long>
│   │   ├── OrderStatus.cs               # Enum: Submitted→AwaitingValidation→StockConfirmed→Paid→Shipped
│   │   ├── Address.cs                   # ValueObject (record)
│   │   └── Buyer.cs                     # IEntity<long>
│   ├── Data/
│   │   ├── CommerceDbContext.cs         # EF Core DbContext
│   │   └── Migrations/
│   ├── Services/
│   │   ├── IOrderService.cs
│   │   └── OrderService.cs             # IGenericEntityFrameworkRepository<OrderEntity>
│   ├── Validation/
│   │   ├── CreateOrderValidator.cs
│   │   └── OrderItemValidator.cs
│   ├── Handlers/
│   │   ├── CreateOrderHandler.cs
│   │   └── OrderStatusHandlers.cs
│   ├── Events/
│   │   ├── OrderStarted.cs
│   │   ├── OrderStatusChangedToSubmitted.cs
│   │   ├── OrderStatusChangedToAwaitingValidation.cs
│   │   ├── OrderStatusChangedToStockConfirmed.cs
│   │   ├── OrderStatusChangedToPaid.cs
│   │   ├── OrderStatusChangedToShipped.cs
│   │   └── OrderStatusChangedToCancelled.cs
│   └── Api/
│       └── OrderEndpoints.cs
│
├── Payments/                            # Vertical slice: Payment Processing
│   ├── Models/
│   │   └── PaymentEntity.cs            # IEntity<long>, EF Core
│   ├── Handlers/
│   │   ├── ProcessPaymentHandler.cs
│   │   └── PaymentResultHandler.cs
│   ├── Events/
│   │   ├── OrderPaymentSucceeded.cs
│   │   └── OrderPaymentFailed.cs
│   └── Api/
│       └── PaymentEndpoints.cs
│
├── Jobs/                                # Background jobs (TickerQ)
│   └── GracePeriodJob.cs               # [TickerJob] for grace period expiry
│
├── Shared/                              # Cross-slice shared contracts
│   ├── StateMachine/
│   │   └── OrderStateMachine.cs        # Railway-based transition engine
│   └── ValueObjects/
│       └── Money.cs                    # record Money(decimal Amount, string Currency)
│
└── README.md
```

---

## 5. Order Aggregate State Machine

Preserving eShop's state machine with Railway-Oriented Programming:

```
OrderStatus lifecycle:
  Submitted ─► AwaitingValidation ─► StockConfirmed ─► Paid ─► Shipped
       │              │                    │               │
       └──Cancelled←──┘                    └──Cancelled←───┘
```

Implementation pattern:

```csharp
public static Result<OrderEntity, AeroError> Transition(
    OrderEntity order, OrderStatus newStatus)
{
    return (order.Status, newStatus) switch
    {
        (Submitted, AwaitingValidation) => Ok(order with { Status = AwaitingValidation }),
        (AwaitingValidation, StockConfirmed) => Ok(order with { Status = StockConfirmed }),
        (StockConfirmed, Paid) => Ok(order with { Status = Paid }),
        (Paid, Shipped) => Ok(order with { Status = Shipped }),
        (Submitted, Cancelled) => Ok(order with { Status = Cancelled }),
        (AwaitingValidation, Cancelled) => Ok(order with { Status = Cancelled }),
        (StockConfirmed, Cancelled) => Ok(order with { Status = Cancelled }),
        _ => AeroError("INVALID_TRANSITION", $"Cannot transition from {order.Status} to {newStatus}")
    };
}
```

---

## 6. Event Catalog (Wolverine Messages)

All eShop integration events become Wolverine messages (plain records). Wolverine's built-in outbox ensures atomicity.

| eShop Event | Wolverine Message | Publisher | Subscriber |
|------------|-------------------|-----------|------------|
| `OrderStartedIntegrationEvent` | `OrderStarted` | Orders/CreateOrderHandler | Basket/ClearBasketHandler |
| `OrderStatusChangedToSubmitted` | `OrderStatusChangedToSubmitted` | Orders | WebApp (SignalR) |
| `GracePeriodConfirmed` | `GracePeriodConfirmed` | Jobs/GracePeriodJob | Orders/SetOrderStatusHandler |
| `OrderStatusChangedToAwaitingValidation` | `OrderStatusChangedToAwaitingValidation` | Orders | Catalog/StockValidationHandler |
| `OrderStockConfirmed` | `OrderStockConfirmed` | Catalog | Orders, Payments |
| `OrderStockRejected` | `OrderStockRejected` | Catalog | Orders |
| `OrderPaymentSucceeded` | `OrderPaymentSucceeded` | Payments | Orders |
| `OrderPaymentFailed` | `OrderPaymentFailed` | Payments | Orders |
| `OrderStatusChangedToPaid` | `OrderStatusChangedToPaid` | Orders | Catalog, Webhooks, WebApp |
| `OrderStatusChangedToShipped` | `OrderStatusChangedToShipped` | Orders | Webhooks, WebApp |
| `OrderStatusChangedToCancelled` | `OrderStatusChangedToCancelled` | Orders | WebApp |
| `ProductPriceChanged` | `ProductPriceChanged` | Catalog | Webhooks |

---

## 7. Choreographed Saga Flow (12 Steps, Adapted)

```
Step 1:  WebApp submits order → MapPost /api/commerce/orders
         └► IMessageBus.InvokeAsync<PlaceOrder>
         
Step 2:  CreateOrderHandler ([WolverineHandler])
         └► OrderStateMachine transition: Submitted
         └► OrderEntity created via IGenericEntityFrameworkRepository
         └► Basket cleared via Wolverine outbox (dual persistence)
         └► OrderStatusChangedToSubmitted published

Step 3:  OrderStatusChangedToSubmitted → Basket/ClearBasketHandler
         └► BasketDocument cleared in Marten

Step 4:  GracePeriodJob (TickerQ cron) runs every N seconds
         └► Finds Submitted orders past grace period
         └► Publishes GracePeriodConfirmed

Step 5:  GracePeriodConfirmed → Orders → SetAwaitingValidationStatus
         └► StateMachine transition: AwaitingValidation

Step 6:  OrderStatusChangedToAwaitingValidation → Catalog/StockValidationHandler
         └► Validates stock for each line item
         └► Publishes OrderStockConfirmed or OrderStockRejected

Step 7:  OrderStockConfirmed → Orders → SetStockConfirmedStatus
         └► StateMachine transition: StockConfirmed

Step 8:  OrderStatusChangedToStockConfirmed → Payments/ProcessPaymentHandler
         └► Simulates payment
         └► Publishes OrderPaymentSucceeded or OrderPaymentFailed

Step 9:  OrderPaymentSucceeded → Orders → SetPaidStatus
         └► StateMachine transition: Paid

Step 10-12: OrderStatusChangedToPaid → Catalog, Webhooks, WebApp
         └► Catalog marks items as sold
         └► Webhooks dispatched
         └► WebApp notified via SignalR
```

---

## 8. Wolverine Middleware (Replacing MediatR Pipeline Behaviors)

| eShop MediatR Behavior | Wolverine Equivalent |
|----------------------|---------------------|
| `LoggingBehavior` | Wolverine's built-in ILogger injection + diagnostic hooks |
| `ValidatorBehavior` | FluentValidation + custom Wolverine middleware |
| `TransactionBehavior` | Wolverine Marten/EF Core integration (auto transaction) |

---

## 9. Anti-Patterns to Avoid

1. ❌ Don't carry over `IntegrationEventLogEF` — Wolverine outbox replaces it
2. ❌ Don't use MediatR — no `IMediator` wrapper
3. ❌ Don't poll for grace period — TickerQ scheduled jobs
4. ❌ Don't use `Guid` — use `Snowflake.NewId()` → `long`
5. ❌ Don't create new repository abstractions — use `IGenericMartenRepository<T>` / `IGenericEntityFrameworkRepository<T>`
6. ❌ Don't use reflection — use `[WolverineHandler]` + source generators
7. ❌ Don't use `newtonsoft.json` — `System.Text.Json` only
8. ❌ Don't use XUnit/NUnit/MSTest — TUnit only

---

## 10. Incremental Implementation Phases

### Phase 1: Foundation (pgvector deferred to Phase 5)
- Create project `Aero.Cms.Modules.Commerce`
- `CommerceModule.cs` entry point
- Catalog vertical slice: `ProductDocument`, `IProductService`, `ProductService`, `CatalogEndpoints`
  - Full-text search indexes on relevant fields (name, description)
  - **No pgvector/embeddings** — deferred to Phase 5
- Content types integration for products
- `CommerceDbContext` + `OrderEntity` skeleton

### Phase 2: Basket
- `BasketDocument`, `IBasketService`, `BasketService`
- Add/Remove basket handlers
- `BasketEndpoints`

### Phase 3: Orders + Saga
- Full `OrderEntity` with state machine
- `CreateOrderHandler` with outbox
- Full event catalog
- TickerQ grace period job

### Phase 4: Payments
- `PaymentEntity`, `ProcessPaymentHandler`
- Payment saga integration

### Phase 5: Cross-Cutting
- SignalR notifications
- DLQ (Wolverine built-in)
- Integration tests (Alba + embedded Postgres)
- Performance tuning
- Documentation

---

## 11. Testing Strategy

| Layer | Framework | Approach |
|-------|-----------|----------|
| Unit tests | TUnit + NSubstitute + Bogus | Test state machine transitions, validators, handlers with mocked repos |
| Integration tests | Alba + MysticMind.PostgresEmbed | Full Wolverine pipeline, Marten + EF Core in same test |
| GUI tests | Playwright | Checkout flow end-to-end |

---

## 12. Risks

| Risk | Mitigation |
|------|------------|
| Dual persistence (Marten + EF Core) in single Wolverine transaction | Proof-of-concept Phase 1; test atomicity early |
| 12-step saga state management leaks | Explicit `OrderStateMachine` with unit tests on every transition |
| Wolverine outbox for EF Core requires `AddNpgsqlDataSource().IntegrateWithWolverine()` | Add to `AeroAppServerExtensions` before handler registration |
| TickerQ cron expression correctness | Unit test the job logic; integration test with time mocking |
| Snowflake ID collisions in high-throughput commerce | Already proven in AeroCMS; monitor in load test |
