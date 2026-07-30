---
title: Commerce security and operations
description: Commerce trust boundaries, concurrency, webhook controls, configuration, verification, and production gaps.
---

Commerce crosses anonymous, member, manager, and provider trust planes. Deployment must preserve the boundaries below.

## Scope authority

| Flow | Authority |
| --- | --- |
| Public catalog and A2A | Resolved public host plus current culture |
| Basket, checkout, orders, payments, subscriptions | External-member principal plus host-site membership |
| Manager catalog and settings | Manager principal plus authorized selected site reloaded from persistence |
| Provider webhooks | Configured provider/account route plus cryptographic/provider verification |

Body and query values are data, never authority for tenant, site, member, ownership, price, stock, or provider account.

## Persistence integrity

Commerce enables Sable optimistic concurrency for products, listings, baskets, orders, payment attempts and receipts, subscriptions, cycles, and subscription receipts.

Important unique constraints include:

- tenant/SKU;
- site/culture/slug and site/culture/product;
- tenant/site/member basket;
- tenant/site/order payment attempt;
- provider/account/event webhook receipt;
- tenant/site/order subscription;
- provider operation, subscription cycle, provider cycle, and payment references.

Stale manager edits return conflict. Checkout and reconciliation batches fail rather than silently overwriting another writer. Product/listing writes also protect against the race where a product is deleted while another session associates a listing.

## Webhook rules

Keep payment and subscription webhook routes reachable by providers, but do not weaken their internal checks:

- exact configured route account;
- bounded raw request body;
- Stripe raw-body signature and timestamp verification or PayPal provider verification;
- event replay/idempotency record;
- tenant/site/provider/account/reference matching;
- authoritative amount and USD currency comparison;
- monotonic state transition;
- manual review on ambiguity or conflict.

Do not log raw payloads, signatures, credentials, client secrets, or webhook secrets. Current durable receipts intentionally store only safe provider references and reconciliation state.

## Configuration checklist

For each enabled payment account:

1. use normalized `stripe` or `paypal`;
2. choose a route-safe account key of letters, digits, `_`, or `-`;
3. bind exactly one tenant and site;
4. use an HTTPS provider base URL;
5. configure required provider credentials and webhook verification values;
6. ensure the provider webhook targets the matching account-key route;
7. configure listing price/plan bindings for recurring products;
8. test success, cancellation, replay, invalid signature, and mismatched amount in a provider sandbox.

Never place secret values in CMS-authored content, listing attributes, AI knowledge, A2A output, source control, or documentation.

## Operational signals

Monitor at least:

- payment attempts stuck in initiating, uncertain, or manual-review states;
- webhook verification failures and unknown account keys;
- repeated delivery/replay counts;
- subscription/order/cycle manual-review reasons;
- optimistic-concurrency conflicts;
- unique-index conflicts;
- checkout failures by site/provider;
- unpublished/inactive listing requests;
- stock rejection and cancellation-restock failures.

## Verification

The focused Commerce suite covers manager policies and scope, public DTO filtering, CMS route ownership, basket pricing and ownership, stock and cancellation, stale writes, provider initiation, Stripe/PayPal verification, webhook replay, recurring adapters, subscription lifecycle/cycles/visibility, page fragments, and A2A bounds.

Run the Commerce test slice after changes to the module, Sable concurrency, external-member authentication, site resolution, Pages routing, or AI projection behavior.

## Production gaps

The code does not establish:

- PCI, tax, shipping, privacy, accessibility, or regional commerce compliance;
- refunds, disputes, chargebacks, void/capture operations, and recovery automation;
- provider live-mode certification;
- distributed outbox/inbox and exactly-once fulfillment;
- multi-currency accounting;
- guest checkout;
- warehouse/fulfillment integration;
- a complete manual-review dashboard and operator runbook;
- service-level objectives, disaster recovery, or provider reconciliation jobs.

Treat these as explicit work, not behavior implied by the existing alpha surfaces.
