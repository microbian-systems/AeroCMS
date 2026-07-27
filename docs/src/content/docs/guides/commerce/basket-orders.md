---
title: Commerce basket and orders
description: External-member baskets, authoritative checkout, stock, immutable order snapshots, cancellation, and visibility.
---

Basket and order operations require the isolated external-member authentication scheme and the host-site membership policy. The server derives the member, tenant, and site; clients do not select them.

## Basket API

The member basket group is rooted at:

```text
/api/commerce/basket
```

It supports reading the current basket, adding an item, changing quantity, removing an item, and clearing the basket. Mutation requests contain only the listing ID and quantity.

When an item is added or changed, Commerce reloads the current site/culture listing and canonical product. The persisted basket item is an authoritative snapshot of the displayed product, SKU, price, billing kind, and optional subscription offer. Wrong-site, unpublished, inactive, incompatible, or unavailable-stock listings are rejected.

Reading a missing basket is non-mutating: it returns an empty view without creating a database document.

## Checkout

The order API group is rooted at:

```text
/api/commerce/orders
```

The checkout request supplies shipping and optional billing addresses. Commerce then:

1. reloads the owned basket;
2. reloads each listing and canonical product;
3. verifies site, culture, publication, active state, price, billing mode, and quantity;
4. decrements stock only for inventory products;
5. creates an immutable order line snapshot;
6. creates the member/site/tenant-scoped order;
7. clears only that member's owned basket;
8. commits the batch through Sable.

The client cannot preserve a stale or manipulated price. Checkout uses the current listing price even if the basket snapshot is older.

One-time and recurring lines cannot be silently mixed into an invalid provider flow. Recurring lines preserve provider offer references and interval information needed by subscription checkout.

## Orders and status

`OrderEntity` contains buyer, address, line, amount, payment, status, and audit snapshots. Customer list/get operations always include tenant, site, and external-member predicates.

The order state machine and Wolverine handlers represent transitions including submitted, awaiting validation, stock confirmed, paid, shipped, and cancelled. Current messaging is immediate best effort after durable work; it is not a claim of a complete transactional outbox.

## Cancellation and stock release

A member can cancel only an owned order in an allowed state. Cancellation is rejected for another site/member and for states where cancellation would be unsafe. Inventory is restored once when the permitted transition succeeds.

Competing checkout, cancellation, and update operations rely on optimistic concurrency so stale writes return conflict rather than silently changing stock or order state.

## Storefront pages

Private storefront Razor Pages include:

```text
/shop/cart
/shop/cart/add
/shop/checkout
/shop/account
/shop/orders
/shop/orders/{id}
/shop/subscriptions
```

The add-to-cart journey is private and authorization-scoped. Public CMS fragments link authorized members into this journey without embedding manager credentials or antiforgery material in public catalog output.

## Current limits

- Currency is fixed to USD.
- Guest checkout is not implemented.
- Shipping calculation, tax calculation, warehouse allocation, and fulfillment automation are not established.
- Long-running inventory reservations and distributed exactly-once guarantees are not established.
