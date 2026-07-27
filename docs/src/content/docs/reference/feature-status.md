---
title: Feature status
description: Implemented, experimental, partial, planned, and tentative AeroCMS capabilities.
---

All statuses are relative to commit `35ec154fb3b57e838d4fe6211f9d9f193e53d812`.

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
- manager assistant and Streamable HTTP MCP;
- semantic content search;
- commerce catalog, basket, orders, payments, and manager UI;
- external identity federation beyond the recovery/local authority.

## Partial

- media: active UI/API/actor flow, but site ownership, general filename containment, content inspection, and transactional cleanup are incomplete;
- health: aggregate endpoints exist, but dependency readiness checks are not supplied by the Health module;
- telemetry: service defaults can register OTLP instrumentation, while the OpenTelemetry feature-module assembly is a placeholder;
- cache coherence: targeted invalidation exists, but cross-layer invalidation is not transactional and some tags are coarse;
- backup/deployment: building blocks exist; no verified turnkey production topology or restore automation is supplied.

## Planned or tentative—not implemented

- native SurrealDB graph edges as the persisted content hierarchy (current hierarchy uses parent IDs and bounded queries);
- a dedicated OAuth authorization server/client-registration flow for arbitrary external MCP clients;
- production-grade media object storage/scanning pipeline;
- complete commerce compliance and fulfillment guarantees;
- treating historical `.docs/` design material as current product behavior.

Planned and tentative items are excluded from examples and API claims except where a page explicitly identifies the boundary as missing.
