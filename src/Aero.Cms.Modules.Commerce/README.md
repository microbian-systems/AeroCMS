# Aero.Cms.Modules.Commerce

Commerce module for Aero CMS. The current implementation provides a multi-site
catalog, authenticated external-member baskets, authoritative USD checkout,
scoped customer orders, and provider-neutral Stripe/PayPal payment initiation
and reconciliation.

The active storefront, manager UI, external-login, and payment production task is
[commerce-production-vertical-slice.md](../../.docs/commerce-production-vertical-slice.md).

## Architecture

```
Aero.Cms.Modules.Commerce          Server-side RCL (Minimal APIs, persistence, handlers)
Aero.Cms.Modules.Commerce.Client   WASM-safe RCL (admin UI pages, typed HTTP clients)
```

### Vertical Slices

| Slice | Persistence | Description |
|-------|------------|-------------|
| **Catalog** | Sable/SurrealDB | Tenant-owned canonical products and site/culture listings |
| **Basket** | Sable/SurrealDB | Tenant/site/external-member cart with authoritative listing snapshots |
| **Orders** | Sable/SurrealDB | Scoped immutable order snapshots and customer cancellation |
| **Payments** | Sable/SurrealDB | Durable attempts/receipts behind Stripe and PayPal Strategy/Adapter boundaries |
| **Jobs** | TickerQ | Tenant/site-aware grace-period expiry |

### UI Surfaces

- **Public** (`Areas/Commerce/Pages/`): Server-rendered `.cshtml` pages
  (catalog browsing, authenticated cart/checkout, and order history)
- **Admin** (`.Client/Pages/Admin/`): WASM-based `.razor` components for the Aero Manager

### Key Patterns

- **Ownership**: manager services enforce tenant/site ownership predicates, but
  the current manager API prefix must move beneath `/api/v1/admin` before
  `DefaultSiteContext` can reliably derive selected-site scope; customer writes
  derive identity from the isolated external-member cookie and host-site policy
- **Messaging**: immediate best-effort Wolverine events after durable commits
- **Transactions**: each Commerce save batch is one Sable/SurrealDB transaction;
  optimistic concurrency rejects competing checkout, cancellation, and payment writes
- **Concurrency**: Sable optimistic concurrency for Commerce versioned documents
- **Id generation**: Snowflake (`long`) for all entity IDs
- **Provider identifiers**: external references and idempotency keys remain opaque strings
- **Error handling**: Railway-Oriented Programming (`Result<T>`, `Option<T>`, `Map`, `Bind`)
- **Validation**: FluentValidation
- **State machine**: `OrderStateMachine` with railway-based transition engine

## Dependencies

- AeroDB Sable / SurrealDB — authoritative Commerce persistence
- WolverineFx — immediate application messaging
- TickerQ — background jobs
- FluentValidation — input validation
- ASP.NET Core authorization — manager `site:*` policies and isolated
  external-member policies

## API Endpoints

All routes are registered as Minimal APIs by `CommerceModule`.

- `/api/commerce/catalog/listings`: anonymous, published host-site catalog DTOs
- `/api/v1/admin/commerce/catalog/*`: manager product/listing routes with exact
  `site:*` policies and trusted selected-site context resolution
- `/api/commerce/basket/*`: external member plus host-site membership
- `/api/commerce/orders/*`: external member plus host-site membership
- `/api/commerce/payments/initiate`: ownership-checked, idempotent payment initiation
- `/api/commerce/payments/status/{orderId}`: ownership-checked payment status
- `/api/commerce/payments/webhooks/{provider}/{accountKey}`: size-limited,
  signature-verified provider reconciliation

Storefront basket requests contain only listing identity and quantity. Customer,
tenant, site, title, SKU, and price are never selected by the caller. Checkout
re-resolves the current listing and canonical product, reserves pooled stock,
creates the order, and clears the basket in one transaction.

## Deferred reliability and payment work

- durable request idempotency and a Sable transactional outbox/inbox
- refunds, voids/capture management, compensation automation, and manual-review UI
- provider sandbox/live integration suites and scheduled reconciliation
- Orleans grains and Commerce caching
- guest checkout

## Tests

- TUnit
- Sable embedded in-memory integration harness
- Commerce ownership/pricing tests in
  `tests/Aero.Cms.Core.Tests/Commerce/CommerceCheckoutOwnershipTests.cs`
- payment/provider/replay tests in
  `tests/Aero.Cms.Core.Tests/Commerce/CommercePaymentsTests.cs`
