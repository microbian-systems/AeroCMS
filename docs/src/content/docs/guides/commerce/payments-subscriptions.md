---
title: Commerce payments and subscriptions
description: Stripe and PayPal accounts, payment attempts, webhook verification, recurring checkout, cycles, and manual review.
---

Commerce keeps provider-specific HTTP and signature behavior behind adapter interfaces. Application services own tenant/site/member/order checks and durable state; Stripe and PayPal adapters own provider protocol translation.

## Provider accounts

Configuration is read from `Commerce:Payments:Accounts`. Each enabled account binds:

- normalized provider name (`stripe` or `paypal`);
- route-safe account key;
- one tenant and site;
- HTTPS provider base URL;
- provider credentials and webhook verification material;
- optional wallet capability flags.

An enabled tenant/site can map to a provider only once, and each provider/account-key pair must be unique. Secrets are configuration values and are not persisted in payment, subscription, cycle, or webhook documents.

## One-time payments

Authenticated member endpoints are:

```text
POST /api/commerce/payments/initiate
GET  /api/commerce/payments/status/{orderId}
```

Initiation requires an owned order, a configured provider for the order scope, and a bounded idempotency key. `PaymentAttemptDocument` records the durable operation before/around provider I/O and distinguishes success, customer action, failure, uncertainty, cancellation, and manual review.

Stripe uses hosted Checkout sessions. PayPal uses the configured Orders/approval flow. Return URLs are validated browser continuations; they are not payment proof.

The public provider callback is:

```text
POST /api/commerce/payments/webhooks/{provider}/{accountKey}
```

The route is anonymous because providers do not carry member cookies. Processing still fails closed unless:

- the route key resolves exactly one enabled account;
- the payload is within bounds;
- provider verification succeeds;
- the event ID has not already been consumed;
- order, amount, currency, tenant, and site agree;
- the transition is valid.

Stripe verification uses the raw body, HMAC SHA-256, constant-time comparison, and a five-minute timestamp window. PayPal verification uses the configured provider verification API and account webhook ID.

## Recurring catalog offers

A recurring listing is valid only when:

- the product fulfillment mode is `NonInventoryRecurring`;
- interval days are within the validated range;
- at least one effective provider binding exists: Stripe price or PayPal plan;
- a published offer has everything required for provider checkout.

The server snapshots provider offer references into basket, order, and subscription lines. It never stores provider credentials in those records.

## Subscription checkout

`ISubscriptionCheckoutService` resolves the owned recurring order, provider account, and offer bindings, then delegates provider checkout creation to a Stripe or PayPal subscription adapter.

The durable `SubscriptionDocument` is unique per tenant/site/order and provider operation. It begins in `PendingProviderConfirmation` and records:

- member, site, tenant, and order ownership;
- provider and account key;
- operation, checkout, subscription, and customer references;
- immutable line and amount snapshots;
- interval and current provider period;
- lifecycle and manual-review state.

Provider-owned recurring collection is intentional: AeroCMS does not schedule off-session charges.

## Subscription webhooks and cycles

The callback route is:

```text
POST /api/commerce/subscriptions/webhooks/{provider}/{accountKey}
```

Adapters verify and normalize provider events before reconciliation. Receipt documents deduplicate event IDs without retaining raw payloads, signatures, secrets, or browser URLs.

Paid invoices/captures create or update immutable `SubscriptionCycleDocument` snapshots. Cycles are unique by subscription/cycle number and by provider cycle/payment reference. Renewals append cycles and do **not** create additional Commerce orders or change inventory.

Lifecycle is monotonic:

- activation/update can establish or maintain active state;
- suspension, action-required, or payment failure can move a subscription to past due;
- cancellation and expiration are terminal;
- generic later events cannot reactivate terminal or manual-review records;
- a provider-specific reactivation is accepted only from a safe nonterminal state.

## Manual review

Commerce marks the subscription, cycle, and related order for manual review when authoritative facts conflict or are incomplete—for example:

- amount, currency, line/offer, period, or provider references disagree;
- a paid cycle later receives a conflicting failure;
- a paid callback lacks safe period bounds;
- an event ambiguously resolves more than one subscription.

Manual review is a fail-closed state. The current code persists the reason and evidence references, but a complete operations/recovery UI is still pending.
