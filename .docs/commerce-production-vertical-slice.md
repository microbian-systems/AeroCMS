# Commerce Production Vertical Slice

Status: Active; WU-0 through WU-3 and WU-4a foundation complete; WU-4 provider adapters/routes remain
Created: 2026-07-20
Scope: Catalog contract/persistence safety, manager and public storefront UI,
production external-member issuance, Stripe/PayPal checkout continuation, and
end-to-end security/sandbox verification

## Outcome

Deliver the smallest coherent Commerce experience that a site can operate and a
customer can use without weakening the completed tenant/site/member ownership or
payment foundations.

The focused WU-1 through WU-6 slice completes:

- the existing public `/shop` entry point backed only by published listings for the
  host-resolved site and current culture;
- a first-class `/manager/commerce` area for products, site listings, pricing,
  publication, and tenant-pooled inventory;
- production issuance of the isolated external-member session through one
  tenant-selected Entra External ID or WorkOS authority;
- end-to-end Stripe and PayPal sandbox checkout; and
- two-tenant HTTP and browser tests proving the trust boundaries.

This task specializes the broader decisions in
[commerce-reliability-and-messaging.md](commerce-reliability-and-messaging.md),
[external-members.md](external-members.md), and
[multi-site-security-hardening.md](multi-site-security-hardening.md). Those
documents remain authoritative for their wider architecture and security phases.

## Current evidence

The current checkout contains the following server-side foundation:

- `ProductDocument` is tenant-owned; `ProductListingDocument` is site- and
  culture-owned.
- Commerce entity IDs are server-issued Snowflake `long` values. Provider
  references and idempotency values are opaque strings.
- Anonymous catalog APIs return narrow DTOs and resolve tenant/site from the
  request host.
- Basket, checkout, order, cancellation, and payment-status operations require
  the isolated external-member scheme and host-site assignment.
- Manager catalog endpoints use `/api/v1/admin/commerce/catalog/*`, carry exact
  `site:*` policies, fail closed without an assigned selected-site cookie, and
  derive the tenant from the persisted selected site.
- Checkout revalidates the current product/listing and atomically reserves
  tenant-pooled stock, creates the order, and clears the basket.
- Stripe and PayPal implement provider-neutral Strategy/Adapter/registry
  contracts with stable provider idempotency keys, verified callbacks, replay
  protection, and manual-review states.
- Sable provides effective save-transaction and optimistic-concurrency behavior
  for the existing Commerce workflows.

The public UI implementation has completed WU-3 acceptance review:

- public `ShopHome` and Razor Pages exist at `/shop`, `/shop/products`,
  `/shop/products/{slug}`,
  `/shop/cart`, `/shop/checkout`, `/shop/orders`, and `/shop/orders/{id}`;
- the `/shop` experience now has host-scoped public DTO rendering, navigation,
  filtering, bounded pagination, empty/error states, and explicit no-store
  metadata;
- full browser/Playwright verification remains part of WU-6 end-to-end security
  and sandbox verification.

The manager client now contributes explicit routable assemblies to both server
prerendering and WebAssembly, exposes `/manager/commerce` product/listing pages,
and loads data only after the renderer becomes interactive.

## Accepted ownership and identity model

1. Canonical products, SKUs, and first-release pooled stock are owned by
   `TenantId`.
2. Listings, culture, slug, merchandising, publication, and USD price are owned
   by `TenantId` plus `SiteId`.
3. The manager-selected site establishes the trusted tenant and site context.
   Request bodies never select either value.
4. A manager requires the exact `site:read`, `site:create`, `site:update`, or
   `site:delete` permission for the selected site. Product access is then
   restricted to that selected site's tenant; listing access is restricted to
   that exact site.
5. Storefront catalog reads use the host-resolved site and current culture, not
   the manager selected-site cookie.
6. Authenticated customer work uses the immutable local
   `ExternalMember.PrincipalId`, the isolated `.AeroCms.Member` cookie, and an
   active host-site assignment.
7. ASP.NET Core Identity remains internal to CMS administrators and managers.
   It is not the customer account store.
8. Each AeroCMS tenant selects one external customer authority:
   `EntraExternalId`, `WorkOS`, or `Disabled`. Provider identity establishes who
   authenticated; AeroCMS remains authoritative for tenant/site membership and
   permissions.
9. Initial CMS setup continues to create/configure the internal administrator.
   External-member providers are configured per tenant after setup; they are not
   mutually exclusive alternatives to the internal manager identity system.

## Public storefront requirements

The storefront remains server-rendered Razor Pages with code-behind and uses
the existing Commerce services rather than adding a client-side authority.

- `/shop` is the canonical store landing page. It presents published featured
  and recent listings for the host site and links to the full catalog.
- `/shop/products` provides server-side search, category filtering, pagination,
  and correct total counts.
- `/shop/products/{slug}` returns `404` for a missing, unpublished, foreign-site,
  or wrong-culture listing. Adding to the basket challenges an unauthenticated
  visitor and forbids an authenticated member without current-site membership.
- `/shop/cart`, `/shop/checkout`, `/shop/orders`, and order detail remain private
  and must never emit public/shared-cacheable customer content.
- Catalog output includes only the narrow public listing contract. It never
  exposes tenant/site ownership, stock counts, audit metadata, concurrency
  versions, unpublished state, or canonical product internals.
- Price, SKU, title, currency, publication, and stock are always resolved again
  server-side on basket mutation and checkout.
- Empty, loading, validation, authorization, provider-failure, and
  manual-review states have explicit user-facing behavior.

## Manager Commerce requirements

The manager UI uses Blazor/Razor code-behind, Radzen components where useful,
and the existing manager shell.

- `/manager/commerce` is the Commerce overview and navigation entry.
- `/manager/commerce/products` lists tenant-owned canonical products and pooled
  stock with search and pagination.
- `/manager/commerce/products/new` and
  `/manager/commerce/products/{id:long}` create/edit the canonical product,
  SKU, active state, attributes, tags, and inventory quantity.
- `/manager/commerce/listings` lists selected-site/culture listings and their
  publication, price, featured state, and product association.
- `/manager/commerce/listings/new` and
  `/manager/commerce/listings/{id:long}` create/edit the site presentation and
  publication state for an in-tenant product.
- The manager navigation exposes a Commerce section with Products and Listings.
- All pages require the internal manager authentication boundary. UI hiding is
  not authorization; every backing endpoint enforces its exact `site:*` policy.
- Cross-tenant product IDs and cross-site listing IDs return the established
  concealed `404` response and do not reveal existence.
- Deletes must not orphan live listings. Historical orders retain immutable
  product/listing snapshots, so deleting an otherwise-unreferenced product does
  not break order rendering. Until explicit archive semantics exist, reject
  deletion of products referenced by listings and prefer
  deactivation/unpublication.

### Manager API route and scoping correction

Move manager catalog APIs out of the anonymous catalog route family and under the
administrative API prefix recognized by `DefaultSiteContext`. Because AeroCMS is
pre-production, use the clean canonical routes without a compatibility alias:

```text
/api/v1/admin/commerce/catalog/products
/api/v1/admin/commerce/catalog/products/{id:long}
/api/v1/admin/commerce/catalog/listings
/api/v1/admin/commerce/catalog/listings/{id:long}
```

The product collection adds a paged/searchable manager `GET`, which is currently
missing. Product routes derive `TenantId` from the selected manager site and
never accept tenant ownership in route, query, or body. Listing routes derive
both `TenantId` and `SiteId`. Product/listing associations are valid only when
the referenced product belongs to the selected site's tenant.

The old `/api/commerce/catalog/manager/*` routes and the placeholder
`/admin/commerce/products` UI route are removed when the replacement is working.

## External-member provider decision

Production customer login follows the accepted provider-neutral boundary in
[external-members.md](external-members.md):

- implement provider links, tenant/organization bindings, invitations,
  callbacks, and upstream logout behind local interfaces;
- issue the local member cookie only after validating provider issuer/subject,
  callback state, tenant binding, invitation/provisioning policy, local member
  status, and site assignment;
- keep provider tokens and secrets server-side;
- use one configured authority per tenant and reject callback mix-up or an
  unbound organization; and
- let Commerce depend only on the local member principal/session contract, never
  on Entra- or WorkOS-specific claims.

The first production adapter requires the user decision listed below. The other
provider remains a subsequent adapter to the same boundary; it does not block
WU-1 storefront/manager work.

## Stripe and PayPal sandbox continuation

- Configure provider accounts and secrets per the accepted merchant-account
  ownership decision; never accept account keys or credentials from a customer.
- Replace any development transport stub with the real provider adapter while
  preserving the existing Strategy contract and canonical payment states.
- Register sandbox webhook endpoints and verify signatures against the exact raw
  body before parsing.
- Exercise authorization/capture behavior, redirect/approval return, duplicate
  initiation, duplicate/out-of-order callbacks, delayed callbacks, amount or
  currency mismatch, provider outage, and timeout/unknown-result recovery.
- Reuse the same provider idempotency key for every retry of one logical
  operation. Never perform a blind payment retry after an ambiguous result.
- Keep wallet options such as Link, Google Pay, and Apple Pay as
  provider-advertised capabilities until a distinct direct-provider lifecycle is
  intentionally designed.

## Threat boundaries

- Host resolution is authoritative for public site scope; the selected-site
  cookie is authoritative only after manager policy validation.
- Manager and member cookies remain separate schemes. A storefront member cannot
  satisfy manager authorization, and a manager cookie does not identify a
  Commerce customer.
- Browser-supplied tenant, site, customer, price, currency, SKU, stock, payment
  account, provider reference, order total, or lifecycle state is untrusted.
- A Snowflake ID is an identifier, not authorization. Every ID lookup repeats
  tenant/site/member ownership predicates before mutation or disclosure.
- Provider callbacks are hostile input until signature, account binding,
  replay/idempotency, amount, currency, and legal state transition checks pass.
- Provider API completion and Sable commit cannot share a transaction. The
  focused slice uses provider status lookup for an unknown result; scheduled
  reconciliation is deferred, and unknown outcomes are never guessed or blindly
  retried.
- Post-commit event delivery is not yet durable. Correctness-critical workflows
  cannot depend on best-effort publication after a successful commit.
- Product inventory is shared across a tenant in this release. A manager who can
  mutate a product for one site can affect other listings for the same tenant;
  this is an accepted scope and must be visible in the UI and audit record.

## Phased implementation checklist

### WU-0 — Documentation

- [x] Record current evidence, accepted ownership, trust boundaries, routes,
      phased scope, acceptance tests, deferred work, and minimum user decisions.
- [x] Link the focused task from the Commerce reliability, external-member,
      multi-site hardening, and module README documents.

### WU-1 — Catalog manager contract and persistence safety

- [x] Move manager endpoints to `/api/v1/admin/commerce/*`, add the missing
      paged/searchable product collection query, and remove the legacy routes.
- [x] Preserve exact `site:*` policies, selected-site context resolution,
      tenant-scoped product lookup, site-scoped listing lookup, and concealed
      cross-scope `404` behavior.
- [x] Reject cross-tenant product/listing associations and destructive deletion
      of products referenced by live listings while preserving independent order
      snapshots.
- [x] Add focused contract, persistence, concurrency, and two-site scoping tests.

### WU-2 — Registered `/manager/commerce` UI

- [x] Replace `/admin/commerce/products` with the manager overview, Products,
      Listings, and code-behind create/edit routes.
- [x] Register the client services and manager navigation entries.
- [x] Provide search, pagination, validation, loading/empty/error states, and the
      accepted tenant-pooled inventory warning.
- [x] Add component/HTTP tests proving pages call only the corrected manager API
      and do not treat UI visibility as authorization.

### WU-3 — Public storefront correctness

- [x] Complete the existing `/shop` home and catalog navigation, search,
      filtering, pagination, empty/error states, and public DTO rendering.
- [x] Preserve host-site/culture scoping and concealed missing, unpublished,
      foreign-site, and wrong-culture behavior on product detail.
- [x] Preserve the external-member boundary for cart, checkout, and order pages
      while keeping browsing anonymous.
- [x] Add focused Razor/API tests for public serialization, totals, pagination,
      and two-site catalog isolation.

### WU-4 — Production external-member issuance

- [x] Implement the WU-4a provider-neutral identity links, tenant/organization bindings,
      invitations, and callback state using Snowflake IDs.
- [ ] Implement login, callback, failure, local/upstream logout, cookie issuance,
      and session revocation for the selected first provider.
- [ ] Issue the local member cookie only after provider, tenant binding, local
      member, invitation/provisioning, and site-assignment validation succeeds.
- [ ] Prove callback state, provider/tenant mix-up, account-linking, invitation,
      revocation, scheme-isolation, and cross-site boundaries.

WU-4a is a local issuance foundation only. It validates an adapter-supplied
external identity and consumes callback state plus any supplied/required
invitation. Completion creates member, link, and assignment documents only when
provisioning. A returning sign-in creates a session and consumes callback state;
if an invitation was supplied, it is validated and consumed in the same commit.
Returning members with an active exact identity link and active exact-site
assignment can create a new local session without another invitation. Any new
principal, link, or missing assignment remains invitation-gated and requires a
fresh provider-verified matching email. Opaque one-time handles expose the
Snowflake ID plus a 256-bit secret, while persistence retains only its digest.
It does not map login/callback routes, issue the ASP.NET cookie, or integrate an
Entra/WorkOS SDK; therefore WU-4 remains incomplete.

### WU-5 — Checkout payment continuation

- [ ] Connect the existing Stripe and PayPal strategies to real sandbox adapters
      and server-owned tenant/site merchant configuration.
- [ ] Complete checkout initiation, approval/redirect return, cancellation, and
      unknown-result status lookup experiences for both providers.
- [ ] Register and verify sandbox webhooks while preserving existing canonical
      state, signature, amount/currency, replay, and idempotency rules.
- [ ] Prove retries, duplicates, out-of-order callbacks, mismatch, cancellation,
      timeout, and provider-failure behavior without duplicate charges or stock
      reservations.

### WU-6 — End-to-end security and sandbox verification

- [ ] Run anonymous, manager-cookie, member-cookie, forged-cookie, and
      two-tenant/two-site HTTP and Playwright matrices.
- [ ] Verify Stripe and PayPal sandbox happy paths plus callback replay, mismatch,
      timeout, provider outage, and browser return behavior.
- [ ] Verify provider-secret isolation, Data Protection sharing assumptions,
      correlation, failure logging, and operational sandbox setup instructions.
- [ ] Complete independent security/architecture review and document the focused
      slice rollout and rollback procedures.

## Acceptance tests

The vertical slice is accepted only when automated tests prove:

1. `/shop` and catalog pages show only published listings for the host-resolved
   site and current culture; a foreign or unpublished slug returns `404`.
2. Anonymous catalog DTOs do not serialize ownership, audit, version, stock, or
   unpublished fields.
3. A manager assigned to site A cannot read or mutate tenant B products or site B
   listings, including by direct IDs and poisoned request bodies.
4. Product and listing create/update inputs cannot re-home tenant or site
   ownership; a cross-tenant product/listing association is rejected.
5. Product inventory changes are concurrency-safe and a referenced product
   cannot be destructively deleted.
6. A member cookie cannot enter `/manager/commerce`, and a manager cookie cannot
   identify a basket/order customer.
7. A validated provider callback issues a local member session only for the
   bound tenant and an active local assignment; forged/mixed/replayed callbacks
   fail closed.
8. Stripe and PayPal sandbox flows reuse stable idempotency keys and duplicate or
   out-of-order callbacks cannot create duplicate payment effects.
9. Customer pages and APIs disclose only the current member's host-site basket,
   orders, and payment status.

## Explicitly deferred

- Transactional Commerce outbox/inbox delivery and durable Wolverine workflows.
- Scheduled payment/provider reconciliation and reconciliation-drift tooling.
- Refund, void, delayed/partial capture, automated compensation, and manager
  manual-review/order operations beyond the current fail-closed state.
- External-provider directory lifecycle webhooks and scheduled member
  reconciliation beyond safe session revocation in the focused issuance unit.
- Guest checkout and guest-to-member basket/account merge.
- Non-USD presentment and settlement.
- Site-allocated, warehouse, location, back-order, and reservation-ledger
  inventory models beyond the accepted tenant-pooled first release.
- Shipping, tax engines, discounts/promotions, fulfillment, returns/RMA, and
  accounting/ERP integration.
- Direct wallet-provider strategies for Link, Google Pay, or Apple Pay.
- Orleans grain extraction, FusionCache/Garnet catalog caching, and Output Cache
  optimization until the functional/security slice is proven.
- Public API compatibility aliases for the pre-production manager routes.

## Minimum user decisions

WU-1 can start without new product decisions. Before the later units reach their
integration points, confirm:

1. Which external-member adapter ships first: Entra External ID or WorkOS?
2. Is first membership invite-only, or may a tenant opt into verified-domain JIT
   provisioning?
3. If WorkOS is first, may its SDK's transitive Newtonsoft.Json dependency have
   a narrow adapter-only exception, or must the adapter use direct
   `System.Text.Json` HTTP calls?
4. Are Stripe/PayPal merchant accounts owned per AeroCMS tenant or per site, and
   who supplies the sandbox credentials/webhook registrations?
5. Does first-release checkout authorize then capture after validation, or
   capture immediately? Refund, void, delayed/partial capture, and later-state
   manager workflows remain deferred from this focused slice.
