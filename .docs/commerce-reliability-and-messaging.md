# Commerce Reliability, Caching, and Messaging

## Status

**Deferred / TODO**

This document records the intended commerce architecture for future work. It does
not implement commerce persistence, caching, grains, messaging durability, payment
processing, or infrastructure.

## Context

Aero CMS currently runs as a modular monolith but should allow commerce workloads
to move into independently deployed services if scale or operational boundaries
require it. The design must therefore keep authoritative persistence, activation
state, caches, HTTP responses, and integration messages as separate concerns.

Commerce has stricter correctness requirements than ordinary content editing:

- duplicate commands can charge a customer or reserve inventory more than once;
- payment providers and message transports can retry;
- a process can fail after committing data but before publishing a message;
- product catalog reads benefit from aggressive caching;
- orders, payments, inventory, and fulfillment have different state ownership and
  failure boundaries.

## Decisions

### 1. Sable and SurrealDB remain authoritative

Sable/SurrealDB is the system of record for products, baskets, orders, and other
commerce documents. Neither Orleans activation state nor Garnet is authoritative.

A successful commerce command is defined by its durable Sable commit. Cache or
non-durable message publication failures after that commit must not make the caller
believe that the database operation failed.

### 2. Use entity-keyed grains

Grains use the aggregate entity identifier as their primary key:

| Grain | Key |
|---|---|
| Product grain | `productId` |
| Basket grain | `basketId` |
| Order grain | `orderId` |

Tenant- or site-keyed coordinator grains may be introduced only for operations that
must be serialized across an entire tenant or site. They must not become the default
route for entity operations because that would unnecessarily serialize unrelated
commerce traffic.

Grains provide:

- single-threaded command coordination per aggregate;
- an application boundary that can later move behind a network transport;
- activation-local working state for hot entities;
- thin orchestration over independently testable application services.

Business rules and persistence logic remain in application services:

```text
HTTP request
  -> entity-keyed grain
      -> application service
          -> Sable/SurrealDB
```

### 3. Grain state is volatile

The first implementation uses ordinary activation-local fields, not Orleans
`IPersistentState<T>`. Deactivation or process failure discards this state.

On activation, the grain loads the aggregate through the application cache and then
Sable:

```text
grain activation
  -> FusionCache local entry
  -> FusionCache distributed entry in Garnet
  -> Sable/SurrealDB
  -> activation-local aggregate
```

Persisted Orleans grain storage is intentionally deferred. It must not duplicate a
complete commerce document unless Orleans state is deliberately made authoritative
for a specific aggregate.

### 4. FusionCache and Garnet cache application data

FusionCache is the application cache abstraction:

- local FusionCache entries provide process-local speed;
- Garnet provides the shared distributed cache;
- cache stampede protection coordinates rehydration;
- cache entries are rebuildable from Sable.

After a successful Sable write:

1. update the activation-local aggregate;
2. write the canonical entity cache entry when the saved document is already
   available;
3. invalidate derived keys, lists, aliases, and old slug keys;
4. treat cache failure as a warning and fall back to invalidation or natural expiry.

Product catalog caches must vary by every input that changes the result, including
tenant, site, culture, currency, pricing context, publication state, and relevant
customer segment.

### 5. Output Cache stores rendered catalog responses

ASP.NET Core Output Cache remains separate from the application document cache,
even when both use the same Garnet server:

- FusionCache stores product and catalog data;
- Output Cache stores final HTTP responses;
- separate registrations and key prefixes prevent accidental overlap.

Product and catalog GET endpoints may use Output Cache. Writes invalidate affected
response tags; the next eligible GET renders the response and automatically
repopulates Output Cache.

Basket, checkout, account, payment, and customer-specific responses are not publicly
cacheable. Any private caching policy must be explicit and must vary safely by the
authenticated subject and all other relevant inputs.

### 6. Allocate IDs before retryable work

Resource identifiers are server-issued Snowflake `long` values. A product, basket,
order, payment attempt, or other aggregate ID is allocated before the retry boundary
and reused for every retry of that logical command.

Preferred creation semantics are idempotent:

```text
PUT /commerce/orders/{orderId}
```

The command also carries a request idempotency key. The server stores the key and
the resulting resource or operation result durably so that repeated requests can
return the original outcome.

The database ID and request idempotency key solve related but different problems:

- the Snowflake ID gives the aggregate a stable identity;
- the idempotency key identifies repeated execution of the same business request.

Client-generated Snowflake IDs are not permitted unless worker allocation can
guarantee uniqueness across every web and mobile client.

### 7. Publish canonical Wolverine events immediately

The first implementation publishes non-durable Wolverine events after successful
commits so other modules can subscribe without coupling directly to commerce
services.

Canonical events should be immutable integration DTOs, for example:

- `ProductChanged`;
- `BasketChanged`;
- `OrderPlaced`;
- `OrderStatusChanged`;
- `PaymentStatusChanged`;
- `InventoryReservationChanged`;
- `FulfillmentStatusChanged`.

Events carry stable identifiers and relevant committed data:

```text
eventId
occurredOn
tenantId
siteId
entityId
entityVersion
changeKind
actor
correlationId
causationId
idempotencyKey
immutable event snapshot or explicit changed fields
```

Do not publish mutable database documents, editor models, or service-layer objects.
Events are published only after the Sable commit. Until a durable outbox exists,
subscribers must accept that a process failure can lose a post-commit event.

Critical cache correctness must not depend exclusively on these non-durable events.
The command path performs required cache coordination directly; events enable
optional and cross-module reactions.

### 8. Wolverine durability is opt-in

Registering Wolverine does not by itself provide a durable inbox, outbox, or durable
local queue. Durable messaging requires explicit Wolverine persistence and transport
configuration.

Durable delivery is at least once. Wolverine's durable inbox/outbox and message
deduplication can provide effectively-once message handling within their configured
boundaries, but handlers and external side effects must remain idempotent.

No architecture may assume literal end-to-end exactly-once delivery.

### 9. Add a Sable transactional outbox before durable workflows

Before commerce relies on durable events, add a Sable transactional outbox:

```text
Sable transaction
  -> persist aggregate changes
  -> persist outbox message documents
  -> commit

outbox dispatcher
  -> claim unpublished messages
  -> publish through Wolverine
  -> mark publication result
```

The aggregate update and outbox message must commit in the same SurrealDB
transaction. The dispatcher must support retries, leases or claims, deduplication,
and safe recovery after process failure.

The Wolverine side must use durable queues and inbox processing where required.
Consumers must use `eventId` or another stable message identity to deduplicate
domain side effects.

### 10. Keep aggregate state boundaries explicit

Commerce concepts do not share a single mutable document:

| Boundary | Owns |
|---|---|
| Product/catalog | Product description, variants, catalog visibility, merchandising metadata |
| Basket | Selected items, quantities, pricing observations, customer/session association |
| Order | Committed purchase intent, immutable line snapshots, totals, customer and address snapshots |
| Payment | Provider attempt IDs, authorization/capture/refund status, provider references |
| Inventory | Availability, reservations, releases, adjustments |
| Fulfillment | Shipment, delivery, pickup, and fulfillment state |

An order stores immutable commercial snapshots needed to explain the purchase. It
must not depend on the current product document for historical price, title, tax,
discount, or variant information.

Cross-boundary work is coordinated by commands and events, not by directly mutating
another boundary's document.

### 11. Payment operations require provider idempotency

Each payment authorization, capture, refund, and void uses a stable
payment-provider idempotency key. A retried handler must query or safely replay the
same provider operation rather than create a second charge.

#### Payment provider strategy boundary

Stripe and PayPal are the first-release payment providers. The Commerce module
uses a provider-neutral Strategy boundary rather than allowing provider SDK types
to escape into checkout, order, or payment domain contracts.

The intended shape is:

```text
checkout/payment application service
  -> payment-provider resolver/factory
      -> IPaymentProviderStrategy
          -> Stripe adapter
          -> PayPal adapter
```

The strategy contract owns provider operations such as creating or confirming a
payment, authorization, capture, void, refund, status lookup/reconciliation, and
verified webhook translation. It returns canonical Commerce results and state;
provider request/response models remain inside the adapter.

The provider resolver selects only from server-configured providers that are
enabled for the authoritative tenant/site. A caller may select among offered
checkout options, but it cannot select an unconfigured provider, account, or
credential.

Link, Google Pay, and Apple Pay are modeled as provider-reported checkout
capabilities/payment methods, not as peer processor strategies. For example,
Stripe can surface supported wallets through its Payment Element when the
underlying configuration, domain, browser, device, and customer are eligible.
A wallet becomes its own provider strategy only if a future direct integration
owns a distinct authorization, capture, refund, reconciliation, and webhook
lifecycle.

Use supporting patterns only at boundaries where they add a concrete guarantee:

- **Adapter** isolates Stripe and PayPal SDK/API models.
- **Factory/registry** resolves the configured strategy without reflection.
- **Decorator** adds telemetry, audit, and idempotency enforcement around provider
  calls; it must not perform blind payment retries.
- **Observer** is implemented through canonical post-commit/outbox events rather
  than provider-specific events leaking across modules.
- The existing order/payment state machines remain authoritative for legal
  transitions; provider status strings are translated at the adapter boundary.

Each strategy declares capabilities such as authorization, delayed capture,
partial/full refund, void, reconciliation, and supported wallet methods. Checkout
must use capability checks instead of provider-name conditionals.

References:

- [Stripe Payment Element and wallet behavior](https://docs.stripe.com/stripe-js/reference)

Payment workflows require explicit compensation rules, for example:

- release inventory when authorization fails;
- void or refund payment when a later mandatory step cannot complete;
- preserve an auditable failed state when automatic compensation fails;
- route irrecoverable cases to manual review.

Durable messaging improves recovery but does not replace provider idempotency,
domain-level state transitions, reconciliation, or compensation.

### 12. Post-commit failure semantics

The command boundary is:

```text
validate command
  -> commit Sable transaction
  -> update activation-local state
  -> coordinate caches
  -> publish immediate non-durable event
  -> return committed result
```

After the Sable commit:

- a cache failure is logged and recovered through invalidation, expiry, or refill;
- a non-durable event failure is logged and must not report the committed command as
  a database failure;
- once the transactional outbox exists, message dispatch failures remain pending in
  the outbox and are retried asynchronously;
- clients can safely retry only when the command's ID and idempotency key are reused.

## Modular Monolith to Microservices

The initial implementation remains in-process:

```text
HTTP -> grain interface -> application service -> Sable
```

Future extraction should preserve the grain and service contracts while changing
hosting and transport boundaries. Extraction must not require changing commerce
documents merely because the service moved to another process.

Before extracting a module:

- replace shared implementation calls with explicit contracts;
- ensure canonical events are versioned integration DTOs;
- configure durable transport and inbox/outbox processing;
- establish ownership of each SurrealDB table or database boundary;
- remove assumptions about shared memory, transactions, and cache invalidation;
- add observability for correlation, causation, retries, and dead letters.

## Acceptance Tests for Future Implementation

The commerce implementation is not complete until automated tests demonstrate:

1. Repeating a create request with the same resource ID and idempotency key produces
   one aggregate and the same response.
2. Concurrent commands for one order are serialized while unrelated orders proceed
   independently.
3. Grain deactivation loses no durable data and the next activation rehydrates from
   FusionCache or Sable.
4. Product writes update the canonical application cache entry and invalidate old
   aliases, lists, and Output Cache tags.
5. Output Cache varies correctly by tenant, site, culture, currency, and other
   configured catalog dimensions.
6. Basket, checkout, payment, and account responses cannot leak between users.
7. A cache outage after a Sable commit does not cause the committed command to be
   reported as a persistence failure.
8. Immediate Wolverine subscribers receive the canonical immutable event during
   normal operation.
9. The future transactional outbox survives a crash after the database commit and
   eventually dispatches the pending message.
10. Duplicate durable delivery does not duplicate inventory reservations, payment
    operations, fulfillment actions, or notifications.
11. Payment-provider retries reuse the same provider idempotency key.
12. Failed workflow steps execute the defined compensation or create an auditable
    manual-review state.

## Deferred Implementation Checklist

- [ ] Define commerce aggregate documents and state-transition invariants.
- [ ] Define entity-keyed product, basket, and order grain interfaces.
- [ ] Keep grain activation state volatile and implement cache/Sable rehydration.
- [ ] Configure FusionCache with Garnet as the distributed application cache.
- [ ] Configure product/catalog Output Cache with isolated Garnet key prefixes.
- [ ] Define catalog cache keys, variation dimensions, tags, and invalidation rules.
- [ ] Add server-side Snowflake ID allocation before retryable work.
- [ ] Add durable request-idempotency records and idempotent create APIs.
- [ ] Define immutable canonical commerce event contracts.
- [ ] Publish non-durable post-commit Wolverine events for initial module subscribers.
- [ ] Ensure post-commit cache or publication failures do not misreport committed
      Sable writes.
- [ ] Design and implement a Sable transactional outbox document and dispatcher.
- [ ] Explicitly configure Wolverine durable queues and inbox/outbox persistence.
- [ ] Make every durable handler idempotent.
- [ ] Define order, payment, inventory, and fulfillment ownership boundaries.
- [ ] Define payment-provider idempotency, reconciliation, and compensation policies.
- [ ] Add the acceptance tests listed above.
- [ ] Document and test the modular-monolith-to-microservices extraction boundary.
