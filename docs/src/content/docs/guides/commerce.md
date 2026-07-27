---
title: Commerce
description: Current AeroCMS Commerce architecture, capabilities, maturity, and documentation map.
---

AeroCMS Commerce is an experimental, multi-site commerce vertical slice built on Sable and SurrealDB. It provides a reusable tenant catalog, site-and-culture storefront listings, authenticated member shopping flows, provider-neutral payments, provider-owned subscriptions, manager authoring, CMS page fragments, and an optional read-only A2A catalog.

It is suitable for alpha development and integration testing. It is **not** a certification of PCI scope, tax or shipping compliance, accounting correctness, provider production readiness, or exactly-once fulfillment.

## Choose the right guide

- [Catalog and storefront](/guides/commerce/catalog-storefront): canonical products, localized listings, public catalog APIs, CMS-owned shop pages, and search/AI exposure.
- [Basket and orders](/guides/commerce/basket-orders): member identity, authoritative repricing, stock, checkout, cancellation, and order history.
- [Payments and subscriptions](/guides/commerce/payments-subscriptions): Stripe and PayPal accounts, idempotent initiation, signed webhooks, recurring checkout, cycles, and manual review.
- [Manager, page editor, and A2A](/guides/commerce/manager-page-editor-a2a): manager routes and UI, page fragments, seeded shop pages, and the optional agent protocol.
- [Security and operations](/guides/commerce/security-operations): ownership rules, concurrency, secrets, limits, deployment checks, and known gaps.

## Architecture at a glance

Commerce separates tenant-owned product facts from site-specific merchandising:

```text
Tenant
  ProductDocument
    SKU, fulfillment mode, stock, attributes, active state

Site + culture
  ProductListingDocument
    product link, slug, copy, category, image, price,
    publication, search/AI flags, optional subscription offer

External member + site
  BasketDocument -> OrderEntity -> PaymentAttemptDocument
                              \-> SubscriptionDocument -> cycles
```

All database identities are Snowflake `long` values. Provider identifiers, account route keys, idempotency keys, checkout sessions, subscriptions, invoices, captures, and webhook event IDs remain opaque strings.

## Active surfaces

| Surface | Current implementation |
| --- | --- |
| Public catalog | Host-site and culture scoped Minimal APIs plus CMS `PageDocument` routes rendered by registered Commerce fragments |
| Private storefront | Razor Pages for cart, checkout, account, orders, order detail, and subscriptions |
| Manager | Blazor WebAssembly pages and typed HTTP clients for products, listings, subscriptions, and A2A settings |
| Persistence | Sable/SurrealDB documents, unique indexes, and optimistic concurrency |
| Payments | Provider Strategy/Adapter boundary with Stripe and PayPal implementations |
| Subscriptions | Provider-owned recurring checkout, durable lifecycle/cycle snapshots, verified webhook reconciliation |
| AI/search | Listing-level `IncludeInSearch` and `IncludeInPublicAi` controls |
| Agent protocol | Disabled-by-default anonymous, read-only A2A product search and lookup |

## Important boundaries

- The public host determines the site. A manager selected-site cookie cannot change public catalog scope.
- Member IDs, tenant IDs, site IDs, titles, SKUs, prices, and stock values are not accepted as customer authority.
- Checkout reloads listings and products, snapshots authoritative commercial values, changes stock when appropriate, creates the order, and clears only the owned basket.
- Payment and subscription webhooks are anonymous only at the HTTP authorization layer. Account lookup, payload bounds, provider signature verification, replay protection, scope matching, and monotonic state checks still apply.
- Subscription renewals create cycle snapshots; they do not create new Commerce orders or decrement inventory.
- Public Commerce AI knowledge exists only for active, published listings explicitly enabled for both search and public AI.

## Current maturity

Implemented alpha behaviors have focused TUnit/Sable coverage for scope isolation, pricing, stock, routing, stale writes, provider adapters, signature verification, webhook replay, subscription visibility, page fragments, and A2A protocol bounds.

Still missing or not certified:

- taxes, shipping rates, fulfillment orchestration, refunds, disputes, and inventory reservation across long-running workflows;
- production provider certification and operational reconciliation tooling;
- multi-currency accounting—the current contract is USD;
- guest checkout;
- durable distributed outbox/inbox guarantees for all post-commit messages;
- a complete manual-review operations UI and provider recovery playbooks.
