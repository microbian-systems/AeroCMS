---
title: Feature inventory
description: Evidence chains and maturity for the major AeroCMS feature areas.
---

This inventory is the evidence map used to create the rest of the documentation. “Module” means an AeroCMS feature assembly, not a Git submodule.

| Area | Entry boundary | Authorization | Application/persistence | Output and tests | Status |
| --- | --- | --- | --- | --- | --- |
| Startup/setup | `Program.cs`, `/setup`, `/setup/status` | setup allowlist and readiness gate | bootstrap handoff, setup state, Sable seed services | setup UI; bootstrap integration tests | Implemented, alpha |
| Sites/cultures | host middleware, `/api/v1/admin/sites`, culture endpoint | manager auth plus site policies | site lookup, selected-site cookie, Sable site records | Razor/Blazor UI; site authorization tests | Implemented; hardening required |
| Manager identity | `/manager/login`, admin auth APIs | local recovery plus configured federation | ASP.NET Identity/Sable stores, durable authority binding | manager shell; identity tests | Implemented, alpha |
| External members | `/api/v1/member`, local activation/reset, callback | dedicated external-member scheme and site policy | local or configured authority, invitation/reset records | cookie principal; auth integration tests | Implemented, alpha |
| Pages | manager Pages APIs and PageEditor | `site:*` policies | page service, publishing workflow, exact source versions, Sable | public Razor route, preview, renderer tests | Implemented; renderers vary in maturity |
| Posts | manager blogs APIs and PostEditor | `site:*` policies | posts services and Sable documents | blog routes, preview, cache tests | Implemented, alpha |
| Documentation content | manager Docs APIs and editor | `site:*` policies | docs service, hierarchy, Sable documents | `/docs`, draft preview, API tests | Implemented, alpha |
| Structured content | manager content type/item APIs | `site:*` policies | type/item services, validators, Sable sessions | public content route/query API; hierarchy tests | Implemented, alpha |
| Public query API | `/api/v1/query/*` | anonymous, published-only | fresh site/culture-scoped query service | JSON or encoded HTML; web API tests | Implemented |
| AI authoring | admin AI APIs and manager AI page | authenticated admin group | settings store, protected keys, provider clients | JSON and SSE; AI tests | Experimental |
| MCP/assistant | `/mcp`, admin assistant JSON/SSE | authenticated plus `site:read`; per-tool permissions | shared tool executor and current-site context | MCP streamable HTTP and manager drawer; boundary tests | Experimental |
| Caching | output-cache policies and FusionCache | manager/admin no-store | Redis-compatible stores, local Garnet or server mode | cached HTTP/application data; cache tests | Implemented with coherence limits |
| Themes/assets | manager theme/site APIs and build script | `site:*` policies | versioned theme selection and style profile | committed Tailwind/SCSS assets | Implemented; build-owned |
| Media/navigation/footer | manager routes and APIs | `site:*` policies | Sable services and media storage abstraction | public navigation/footer/media references; tests | Implemented, alpha |
| Commerce | storefront and manager catalog/payment APIs | anonymous storefront; member/manager policies by route | product/listing/order/payment services | storefront/manager UI; focused tests | Partial and experimental |
| Health/telemetry | `/health` outside Development; service defaults | no explicit health endpoint policy | shared health checks, Serilog/OpenTelemetry wiring | plain aggregate health output | Partial |

## Evidence rules

Each deeper page lists its source files in `documentation-manifest.json`. Tests prove a bounded behavior, not the absence of every security or concurrency defect. A feature is “implemented” only when a current entry boundary reaches active application code and a persisted or rendered result. Source stubs, TODO-only registrations, and design notes are classified as partial, planned, or tentative.
