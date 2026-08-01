---
title: Feature status
description: Implemented, experimental, partial, planned, and tentative AeroCMS capabilities.
---

All statuses are relative to commit `47c299402cee06975dfdf32e071ba133179c15f2` plus the current Commerce working-tree verification.

## Implemented, alpha

- setup bootstrap, durable setup state, initial tenant/site/admin seeding;
- source-generated module/Wolverine/Orleans catalogs;
- site/culture resolution and manager selected-site context;
- manager local identity, configured federation boundaries, external-member local/external authority contracts;
- page hierarchy, Aero composition editor, exact source versions, preview, publishing, aliases;
- posts/blogs and hierarchical documentation content;
- flat/hierarchical content types, built-in fields, references, bounded traversal, public content URL rendering;
- anonymous published read-only page/post/docs/content query facade;
- navigation, footer, theme/style-profile selection, committed asset pipeline;
- FusionCache and named ASP.NET Core output-cache policies.

“Implemented” does not mean production-ready; see the limitations below.

## Experimental or alpha-with-extra-risk

- pure Scriban pages and Scriban fragments;
- pure HTMX pages/fragments;
- SharpTS pages/fragments (explicitly alpha, trusted authors only);
- AI provider settings, enhancement, translation, and SSE;
- ephemeral public assistant, durable scoped member/manager conversations, and manager-only explicit memory;
- public AI corpus exposure controls, grounding citations, token budgets, and AI/MCP rate limits;
- scoped service-key authentication and Streamable HTTP MCP;
- semantic content search;
- commerce catalog/storefront, basket/orders, Stripe and PayPal payments, provider-owned subscriptions, manager/page-editor integration, and optional read-only A2A;
- external identity federation beyond the recovery/local authority.

## Partial

- media: active UI/API/actor flow, but site ownership, general filename containment, content inspection, and transactional cleanup are incomplete;
- health: aggregate endpoints exist, but dependency readiness checks are not supplied by the Health module;
- telemetry: service defaults can register OTLP instrumentation, while the OpenTelemetry feature-module assembly is a placeholder;
- AI/MCP limits: token budgets and request/tool rate limiters are process-local rather than cluster-global;
- cache coherence: targeted invalidation exists, but cross-layer invalidation is not transactional and some tags are coarse;
- backup/deployment: building blocks exist; no verified turnkey production topology or restore automation is supplied.

## Planned or tentative—not implemented

- native SurrealDB graph edges as the persisted content hierarchy (current hierarchy uses parent IDs and bounded queries);
- a dedicated OAuth authorization server, delegated-consent, and client-registration flow for interactive external MCP clients;
- production-grade media object storage/scanning pipeline;
- complete commerce compliance and fulfillment guarantees;
- treating historical `.docs/` design material as current product behavior.

Planned and tentative items are excluded from examples and API claims except where a page explicitly identifies the boundary as missing.
