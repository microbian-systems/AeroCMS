---
title: Commerce manager, page editor, and A2A
description: Manager authoring, CMS Commerce fragments, seeded shop pages, and the optional read-only A2A catalog.
---

Commerce combines a Blazor manager application, typed admin APIs, native CMS page fragments, and an optional anonymous agent protocol.

## Manager routes and APIs

Manager pages are:

```text
/manager/commerce
/manager/commerce/products
/manager/commerce/products/{id}
/manager/commerce/listings
/manager/commerce/listings/{id}
/manager/commerce/subscriptions
/manager/commerce/subscriptions/{id}
```

The catalog APIs use the canonical prefix:

```text
/api/v1/admin/commerce/catalog/products
/api/v1/admin/commerce/catalog/listings
```

Read/create/update/delete operations require the corresponding `site:read`, `site:create`, `site:update`, or `site:delete` policy. Subscription manager reads require `site:read`.

The scope resolver requires an authorized selected-site context and reloads the persisted site to derive the tenant. Request JSON cannot override product or listing ownership. Missing, forged, foreign, or unassigned site selection fails closed.

Editors preserve version values so stale writes become HTTP 409 conflicts. The listing editor searches canonical products and preserves a selected product even when it is outside the current result page.

## Page editor integration

Commerce registers three server-rendered page fragments:

- catalog;
- search;
- product detail.

They use the request's resolved site and culture, not a manager cookie, request-supplied tenant, or slug lookalike. Links are generated through culture-aware Aero routes.

The Commerce seed factory creates the shop page tree idempotently for each site/culture through normal page services. Because the records are normal Aero pages, authors can compose surrounding content and use the standard metadata, navigation, preview, publishing, and alias workflows.

## A2A settings

A2A is disabled when no site setting exists. Manager settings are:

```text
GET /api/v1/admin/commerce/a2a/settings
PUT /api/v1/admin/commerce/a2a/settings
```

Reads require `site:read`; changes require `site:update` and the authorized selected site. Settings are isolated by tenant and site.

## Public A2A protocol

When explicitly enabled for the host site, Commerce exposes:

```text
GET  /.well-known/agent-card.json
POST /a2a/commerce
```

The protocol is JSON-RPC 2.0 and read-only. It supports two bounded skills:

- product search by query/category/pagination;
- product lookup by canonical slug.

The response uses a dedicated allowlisted A2A projection. It does not serialize storefront documents, subscription offers, manager fields, credentials, member data, inventory internals, or database metadata.

The agent sees only active, published listings for the host-resolved site and current culture. A selected-site cookie and request tenant field cannot change scope.

## Protocol constraints

The current endpoint:

- rejects oversize requests before catalog access;
- rejects unknown JSON members;
- accepts one structured data part in the supported media type;
- rejects unsupported method, version, skill, continuation/task, and part shapes with safe JSON-RPC errors;
- uses source-generated System.Text.Json metadata;
- conceals disabled, unresolved, or invalid-host availability;
- advertises no streaming, push notification, or extended-card capability.

A2A does not authorize writes, basket actions, checkout, order access, or subscriptions. Those remain member or manager workflows.
