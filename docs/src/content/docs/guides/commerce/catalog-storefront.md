---
title: Commerce catalog and storefront
description: Canonical products, localized listings, public APIs, CMS storefront pages, and AI/search visibility.
---

The Commerce catalog uses two document types so one tenant product can be merchandised differently across sites and cultures.

## Canonical products

`ProductDocument` is tenant-owned. It stores:

- normalized, tenant-unique SKU;
- canonical name and optional description;
- fulfillment mode;
- stock quantity;
- active state;
- string attributes and tags;
- audit timestamps and optimistic-concurrency version.

Fulfillment modes are:

| Mode | Meaning |
| --- | --- |
| `Inventory` | Stock-managed product. Checkout validates and decrements available stock. |
| `NonInventoryOneTime` | One-time product without stock management. |
| `NonInventoryRecurring` | Provider-owned recurring product eligible for a subscription offer. |

A product cannot be deleted while any tenant listing references it. Deactivation is the safe alternative. Product updates also refresh the search/AI projections of their bounded localized listings.

## Site and culture listings

`ProductListingDocument` links a product to a site and culture and owns storefront merchandising:

- route-safe slug;
- localized name, summary, description, category, and image URL;
- USD price and optional compare-at price;
- publication and featured flags;
- search and public-AI flags;
- optional provider subscription offer;
- optimistic-concurrency version.

The database enforces one `(site, culture, slug)` and one `(site, culture, product)` listing. Server validation canonicalizes culture and slug values and rejects stale manager versions.

A published recurring listing must point to an eligible `NonInventoryRecurring` product and carry a valid interval plus a Stripe price ID or PayPal plan ID. Non-recurring products cannot carry subscription offers.

## Anonymous catalog API

The active public endpoints are:

```text
GET /api/commerce/catalog/listings
GET /api/commerce/catalog/listings/by-slug/{slug}
GET /api/commerce/catalog/categories
```

Listing queries support bounded search, category, skip, and take values. Results are allowlisted public DTOs—not database documents—and include only:

- the host-resolved site;
- the current UI culture;
- published listings;
- active products.

Filtering happens before count and pagination. Missing or noncanonical slugs are concealed as not found; an actual database failure is not rewritten as a 404.

## CMS-owned public routes

Public catalog pages are native CMS pages rather than competing hard-coded Razor routes. The Commerce seed service creates idempotent, culture-aware `PageDocument` records through the normal page services. Registered fragments render:

- shop catalog/home;
- product search;
- product detail.

This means normal CMS route ownership, culture routing, page metadata, navigation, aliases, preview, and publication remain authoritative. Private stateful routes such as `/shop/cart`, `/shop/checkout`, and `/shop/orders` continue to use Razor Pages and take precedence over the CMS catch-all.

## Search and AI exposure

Each listing has two independent controls:

- `IncludeInSearch` admits the listing to the search corpus.
- `IncludeInPublicAi` permits the published listing to cross into the public AI corpus.

The public AI projection requires an active product, a published listing, search inclusion, and public-AI inclusion. The projection contains only allowlisted listing copy, category, and displayed price. Manager knowledge can include the same listing when search is enabled, but never bypasses tenant/site scope.

## Manager lifecycle

The manager creates a canonical product first, then creates one or more site/culture listings. Updates require the version last read by the editor. Competing edits return conflict rather than overwriting another writer.

Catalog writes are one Sable save batch. A listing association also stores the linked product version so a concurrent product deletion cannot commit an orphaned listing.
