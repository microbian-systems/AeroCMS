---
title: Commerce status
description: Verified AeroCMS catalog, basket, order, payment, manager, and storefront commerce boundaries.
---

Commerce is experimental and must not be represented as a production storefront. The current code provides a bounded catalog, authenticated member basket/orders, payment orchestration hooks, and manager product/listing screens.

## Implemented routes

- Anonymous catalog: `/api/commerce/catalog/listings`, `/listings/by-slug/{slug}`, and `/categories`. Queries use the resolved site and current UI culture and filter to public/active data.
- External-member basket: `/api/commerce/basket` and `/items`; requires the external-member policy and site policy.
- External-member orders: `/api/commerce/orders`; list/get, checkout, and cancel use the current member, tenant, and site.
- Payments: authenticated initiate/status endpoints plus an anonymous provider webhook route.
- Manager catalog: `/api/v1/admin/commerce/catalog/products` and `/listings` with the normal `site:*` read/create/update/delete policies.
- Manager UI: `/manager/commerce`, `/products`, and `/listings`.

Products are reusable catalog records; listings bind a product to site/culture merchandising and publication/availability state. Snowflake `long` IDs remain authoritative.

## Safety boundaries

The anonymous catalog still requires authoritative host-to-site resolution. Member operations must never accept a member or site ID from the body as authority. Order/payment services re-check tenant, site, member, listing, price, and state transitions.

Payment webhooks are intentionally anonymous at the ASP.NET authorization layer because providers cannot carry member cookies. The provider/account route, signature verification, replay protection, payload size, idempotency, and order ownership must all succeed inside the payment boundary.

## Not established

This baseline does not establish PCI scope, tax/shipping compliance, inventory reservation guarantees, refund/dispute workflows, production provider certification, multi-currency accounting, or end-to-end exactly-once fulfillment. Treat these as missing product/reliability work, not implicit features.
