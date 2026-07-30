---
title: Documentation coverage
description: Baseline, covered features, API scope, build verification, evidence gaps, and remaining documentation risks.
---

Baseline commit: `47c299402cee06975dfdf32e071ba133179c15f2`, with Commerce verified against the current working tree and focused test suite.

## Documented at the current evidence depth

- documentation information architecture and Getting Started journey;
- startup/setup state and first-run handoff;
- site/tenant/culture resolution and manager selection boundary;
- pages, four page renderers, five fragment kinds, preview and publishing;
- posts and hierarchical documentation content;
- content types, built-in fields, hierarchy validation/traversal, public query API;
- manager/external-member identity boundaries, roles/site policies, API-key handling;
- AI settings/enhancement/translation/SSE; ephemeral public and durable member/manager assistants; manager-only explicit memory; exposure/grounding/process-local budget; and scoped MCP API-key/process-local rate-limit boundaries;
- cache layers, theme build ownership, health/telemetry status;
- Commerce product/listing modeling, public catalog and CMS route ownership, member basket/order flows, one-time payments, provider-owned subscriptions and cycles, manager/page-editor integration, A2A, and security/operations boundaries;
- public API scope, ingestion files, glossary, examples, and security checklist.

## Partially documented

- media documents its active API and production blockers, not a recommended production storage implementation;
- individual optional/stub feature modules are represented in the feature inventory/status rather than receiving separate product pages;
- deployment describes verified host requirements, not a certified platform-specific topology.
- MCP tests cover endpoint metadata and focused key, executor, and rate-limit behavior, not a complete HTTP authentication/multi-site-header lifecycle;
- manager assistant and API-key-management browser mutations still require an explicit origin/antiforgery hardening decision.

## Missing evidence

- an approved stable-release compatibility policy;
- a production threat model, distributed AI/MCP limiting strategy, and interactive external-client OAuth design;
- production media/object-storage design and migration path;
- provider-certified commerce/payment and operational recovery evidence;
- dependency-level readiness checks and SLOs.

## Public API reference

Included assemblies are the curated public surfaces of `Aero.Cms.Abstractions`, `Aero.Cms.Contracts`, `Aero.Cms.Html`, `Aero.Cms.Core`, `Aero.Cms.Web.Core`, and `Aero.Cms.Web.Bootstrap`.

Intentionally excluded: concrete feature-module assemblies other than the explicitly documented experimental Commerce module, hosts, UI/shared internals, data/persistence implementations, generated contexts, validators, legacy Marten code, tests, and every Git submodule.

Known XML documentation gaps are reported in `docfx/api-documentation-gaps.md`; executable code was not changed merely to silence warnings.

## Verification results

Verified on 2026-07-26 in the isolated `codex/aerocms-documentation` worktree:

| Check | Result |
| --- | --- |
| first-party API assembly builds | Pass — seven selected Release assemblies, including Commerce, built with zero errors; existing dependency/compiler warnings remain |
| Starlight dependency install | Pass — frozen pnpm lockfile and explicit build-script allowlist |
| ingestion generation and manifest validation | Pass — 23 public entries generated and validated |
| internal canonical-link and duplicate-path validation | Pass — 23 unique canonical paths |
| Starlight static build | Pass — 24 Starlight routes, sitemap, and Pagefind index generated |
| DocFX metadata/API build | Pass — 748 HTML files total: 746 managed/namespace pages (including seven navigation stubs) plus the API landing and TOC pages; new AI/MCP contract namespaces are present |
| built-site link and anchor crawl | Pass — 772 HTML files |
| submodule exclusion audit | Pass — no standalone API pages or source links for Git-submodule assemblies; manifest provenance rejects submodule roots |
| credential/PII ingestion audit | Pass — configuration-derived secret values checked with zero exact ingestion-corpus matches; no personal email literals |
| focused public-query tests | Blocked by existing repository configuration — `global.json` selects Microsoft.Testing.Platform while the test project is classified as VSTest; direct build also lacks three browser-variant asset files |

## Remaining risks

The product is changing quickly, so route, contract, and feature maturity drift is the primary documentation risk. Generated full-corpus text can become stale if contributors bypass `pnpm run generate`. The manifest validator confirms file/provenance/link shape but does not prove every behavioral sentence; review source and focused tests whenever a feature changes.

The canonical origin is set to `https://docs.getaerocms.net`, matching the current manager UI link, but it should be reconfirmed before the first production deployment. The public-query examples agree with the six current tests in `PublicCmsQueryApiTests`; those tests could not be freshly executed until the repository's mixed test-runner/browser-variant configuration is corrected.
